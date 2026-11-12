Imports System.IO
Imports System.Drawing
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

' fTileGroups.vb - Tool 4: "Tile Groups" palette window.
' Browses tile-group .json files (by folder/category), previews the selected
' group, and starts tile-group placement mode on the main form.
Public Class fTileGroups

#Region "Window Snapping"

    Private Const WM_MOVING As Integer = &H216

    ''' <summary>
    ''' Intercepts WM_MOVING to snap this window to screen edges and other
    ''' tool palettes (see the snap algorithm comment in ToolPalette.vb)
    ''' </summary>
    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_MOVING Then
            Dim r As RECT = Runtime.InteropServices.Marshal.PtrToStructure(Of RECT)(m.LParam)
            Dim snapped As Point = ToolPaletteHelper.GetSnappedPosition(Me, New Point(r.Left, r.Top))

            Dim dx As Integer = snapped.X - r.Left
            Dim dy As Integer = snapped.Y - r.Top

            r.Left += dx
            r.Right += dx
            r.Top += dy
            r.Bottom += dy

            Runtime.InteropServices.Marshal.StructureToPtr(r, m.LParam, True)
        End If

        MyBase.WndProc(m)
    End Sub

    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure RECT
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

#End Region

    ' Used to display and select tile groups for placement

    Private mainForm As fMain = Nothing
    Private TileGroupPreview As Bitmap = Nothing      ' Current bitmap of tile group
    'Private TileGroupTileMappings As Integer(,) = Nothing

    Public TileGroupsFolder As String
    Private TileGroupsSubFolder As String   ' Eg "Greenworld'
    Private SelectedTileSet As String       ' We need to include TileSet as well as TileGroup as we can have 
    Private SelectedTileGroup As String     ' the same TileGroup name in different TileSet's

    Private NoGroupsMsg As String = "No Tile Groups Found"

    ' Sentinel cmbFolder entry representing the root of the Tilegroups
    ' directory (i.e. loose .json files not inside a subfolder). Picked to
    ' contain characters that aren't legal in Windows folder names so it
    ' can never collide with a real subfolder name.
    Private Const RootCategoryLabel As String = "(Default)"

    ' Tracks whether any tile groups were discovered on the last folder
    ' scan. Drives the empty-state message in pnlTileGroupViewport_Paint.
    Private noTileGroupsAvailable As Boolean = True

    ' For use in loading tile groups from json files
    Private currentTileGroupData As JObject = Nothing
    Private TileGroupTiles As JArray = Nothing
    'Private TileGroupWidth As Integer = 0
    'Private TileGroupHeight As Integer = 0
    Private TileSetName As String = ""
    Private TileGroupName As String = ""
    Private TileGroupBmp As String = ""
    Private TileGroupNotes As String = ""

    ''' <summary>
    ''' Constructor
    ''' </summary>
    Public Sub New()
        InitializeComponent()

        ' Set up panel for double buffering to reduce flicker
        pnlTileGroupViewport.GetType().GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(pnlTileGroupViewport, True)

        ' Configure panel properties
        pnlTileGroupViewport.AutoScroll = True
        pnlTileGroupViewport.BorderStyle = BorderStyle.FixedSingle
        pnlTileGroupViewport.BackColor = System.Drawing.Color.DarkGray

        ' Initialize context menu - Do we need one??
        'InitializeContextMenu()
    End Sub

    ''' <summary>
    ''' Form load event handler - restores position and scans the Tilegroups folder
    ''' </summary>
    Private Sub fTileGroups_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ToolPaletteHelper.ConfigureAsToolPalette(Me)
        ToolPaletteHelper.RestorePosition(Me, "TileGroups")

        ' Get reference to main form
        mainForm = TryCast(Application.OpenForms("fMain"), fMain)

        ' Set form properties (TopMost is set by ConfigureAsToolPalette per
        ' the View → Tool Windows Always On Top setting)
        Me.FormBorderStyle = FormBorderStyle.FixedToolWindow
        Me.Text = "Tile Groups"
        Me.Icon = My.Resources.lists

        ' Load tile groups from the user-configurable location (defaulted to
        ' <exe>\Tilegroups on first run by fMain_Load). This is the same
        ' folder fTileSelect writes exports to, so a tile group exported on
        ' one launch is visible in this list on the next.
        TileGroupsFolder = My.Settings.Tilegroups

        ResetLabels()

        LoadTileGroupFolders(TileGroupsFolder)
    End Sub

    ''' <summary>
    ''' Form closing event handler - saves position and unregisters the palette
    ''' </summary>
    Private Sub fTileGroups_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        ToolPaletteHelper.SavePosition(Me, "TileGroups")
        ToolPaletteHelper.UnregisterToolForm(Me)
    End Sub

    ''' <summary>
    ''' Re-scans the Tilegroups directory and rebuilds the folder list. If
    ''' <paramref name="selectFolder"/> is supplied and exists in the new
    ''' list, it's selected (which triggers cmbFolder_SelectedIndexChanged
    ''' and populates the tile-group dropdown).
    '''
    ''' Called by fTileSelect right after exporting a tile group so the new
    ''' file shows up in the list.
    ''' </summary>
    Public Sub RefreshFromDisk(Optional selectFolder As String = Nothing)
        ' Re-scan from the configured location (may have changed since Load).
        TileGroupsFolder = My.Settings.Tilegroups
        LoadTileGroupFolders(TileGroupsFolder)

        ' Try to select the requested folder.
        If Not String.IsNullOrEmpty(selectFolder) Then
            Dim foundIndex As Integer = -1
            For i As Integer = 0 To cmbFolder.Items.Count - 1
                If String.Equals(cmbFolder.Items(i).ToString(), selectFolder, StringComparison.OrdinalIgnoreCase) Then
                    foundIndex = i
                    Exit For
                End If
            Next

            If foundIndex >= 0 Then
                If cmbFolder.SelectedIndex <> foundIndex Then
                    cmbFolder.SelectedIndex = foundIndex ' fires cmbFolder_SelectedIndexChanged → populates cmbTileGroup
                Else
                    ' SelectedIndex was already on the requested folder, so
                    ' the assignment above wouldn't fire the event. This is
                    ' the case right after an export to an existing folder —
                    ' we still need to re-scan the .json files. Invoke the
                    ' handler directly.
                    cmbFolder_SelectedIndexChanged(cmbFolder, EventArgs.Empty)
                End If
            End If
        End If
    End Sub

    ''' <summary>
    ''' True when cmbTileGroup is empty or only contains the "No Tile Groups
    ''' Found" placeholder — i.e. there's nothing selectable in the current
    ''' folder.
    ''' </summary>
    Private Function CurrentFolderHasNoGroups() As Boolean
        If cmbTileGroup.Items.Count = 0 Then Return True
        If cmbTileGroup.Items.Count = 1 AndAlso
           cmbTileGroup.Items(0).ToString() = NoGroupsMsg Then
            Return True
        End If
        Return False
    End Function

    ''' <summary>
    ''' Clears the tile-group info labels (size, tileset, name, notes, path)
    ''' </summary>
    Private Sub ResetLabels()
        lblWidth.Text = ""
        lblHeight.Text = ""
        lblTileset.Text = ""
        lblTileGroupName.Text = ""
        lblNotes.Text = ""
        lblPath.Text = ""
    End Sub

    ''' <summary>
    ''' Paint event handler - draws the tile-group preview, or an empty-state message
    ''' </summary>
    Private Sub pnlTileGroupViewport_Paint(sender As Object, e As PaintEventArgs) Handles pnlTileGroupViewport.Paint

        ' Empty state — explain what to do based on what the user can actually
        ' choose right now.
        If TileGroupPreview Is Nothing Then
            e.Graphics.Clear(System.Drawing.Color.LightGray)

            Dim message As String
            If noTileGroupsAvailable Then
                ' Whole Tilegroups directory is empty (or unconfigured).
                message = "No tile groups found." & vbCrLf & vbCrLf &
                          "Drop .json files in:" & vbCrLf &
                          If(String.IsNullOrEmpty(TileGroupsFolder), "(Tilegroups folder not configured)", TileGroupsFolder)
            ElseIf CurrentFolderHasNoGroups() Then
                ' A folder is selected but it has no .json files — don't tell
                ' the user to pick from a list with nothing in it.
                message = ""
            Else
                message = "Select a tile group from the list above."
            End If

            If message <> "" Then
                Using font As New Font("Arial", 9)
                    Using brush As New SolidBrush(System.Drawing.Color.DimGray)
                        e.Graphics.DrawString(message, font, brush, 10, 10)
                    End Using
                End Using
            End If
            Return
        End If

        ' Draw the copied tiles preview
        e.Graphics.DrawImage(TileGroupPreview,
                            -Math.Abs(pnlTileGroupViewport.AutoScrollPosition.X),
                            -Math.Abs(pnlTileGroupViewport.AutoScrollPosition.Y))
    End Sub

    ''' <summary>
    ''' Scans the Tilegroups root and populates the folder/category combo box
    ''' </summary>
    Private Sub LoadTileGroupFolders(ByVal RootFolderPath As String)
        ' Build the cmbFolder list:
        '   - If the root directly contains .json files, prepend a "(Default)"
        '     entry that represents those loose files.
        '   - Then add every subfolder name. This means both layouts work:
        '     "Tilegroups\name.json" and "Tilegroups\Category\name.json".

        If String.IsNullOrWhiteSpace(RootFolderPath) OrElse Not Directory.Exists(RootFolderPath) Then
            Debug.WriteLine("LoadTileGroupFolders - The specified root folder does not exist.")
            cmbFolder.DataSource = Nothing
            cmbTileGroup.Items.Clear()
            noTileGroupsAvailable = True
            pnlTileGroupViewport.Invalidate()
            Return
        End If

        Try
            Dim entries As New List(Of String)

            ' Root-level JSONs → "(Default)" pseudo-folder
            Dim rootJsonCount As Integer = Directory.EnumerateFiles(RootFolderPath, "*.json").Count()
            If rootJsonCount > 0 Then
                entries.Add(RootCategoryLabel)
            End If

            ' Real subfolders
            For Each d In Directory.EnumerateDirectories(RootFolderPath)
                entries.Add(Path.GetFileName(d))
            Next

            noTileGroupsAvailable = (entries.Count = 0)

            cmbFolder.DataSource = entries

        Catch ex As Exception
            Debug.WriteLine("LoadTileGroupFolders - An error occurred: " & ex.Message)
            noTileGroupsAvailable = True
        End Try

        pnlTileGroupViewport.Invalidate()
    End Sub

    ''' <summary>
    ''' Placeholder - not yet implemented (folder scanning happens in
    ''' cmbFolder_SelectedIndexChanged instead)
    ''' </summary>
    Private Sub LoadTileGroups(ByVal FolderPath As String)
        ' Load list of tile groups from .json file group files
        '
        ' We should support multiple folders
        ' So that we can have default OP2 tile groups and then user's own tile groups in another folder
        ' etc

    End Sub

    ''' <summary>
    ''' Folder combo change event handler - lists the .json tile groups in the
    ''' selected folder (or the Tilegroups root for "(Default)")
    ''' </summary>
    Private Sub cmbFolder_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbFolder.SelectedIndexChanged
        ' Change the folder we are looking in for tile group .json files
        ResetLabels()

        ' Clear the previous list of tile groups
        cmbTileGroup.Items.Clear()

        ' --- Safety Checks ---
        ' Check if an item is actually selected and it's not the default placeholder
        If cmbFolder.SelectedItem Is Nothing Then
            Return ' Exit if nothing valid is selected
        End If

        ' --- Build the Target Folder Path ---
        ' Get the name of the selected folder (e.g., "Fantasy", "Sci-Fi"), or
        ' the "(Default)" sentinel for loose .json files at the Tilegroups root.
        Dim selectedFolderName As String = cmbFolder.SelectedItem.ToString()

        TileGroupsSubFolder = selectedFolderName

        Dim targetFolderPath As String
        If selectedFolderName = RootCategoryLabel Then
            targetFolderPath = TileGroupsFolder
        Else
            targetFolderPath = Path.Combine(TileGroupsFolder, selectedFolderName)
        End If

        ' --- Find and Add the JSON Files ---
        Try
            Dim TileGroupCount As Integer = 0

            ' Check if the folder actually exists before trying to read it
            If Directory.Exists(targetFolderPath) Then
                ' Get all files in the directory that have a .json extension
                For Each jsonFilePath As String In Directory.EnumerateFiles(targetFolderPath, "*.json")
                    ' Get the file name without the extension (e.g., "Orcs.json" -> "Orcs")
                    Dim fileNameWithoutExtension As String = Path.GetFileNameWithoutExtension(jsonFilePath)

                    ' Add the clean name to the cmbTileGroup ComboBox
                    cmbTileGroup.Items.Add(fileNameWithoutExtension)

                    TileGroupCount = TileGroupCount + 1
                Next

                'Debug.WriteLine("TileGroupCount (" & selectedFolderName & "): " & TileGroupCount)
                lblTileGroups.Text = TileGroupCount.ToString
            End If

            If TileGroupCount = 0 Then
                cmbTileGroup.Items.Add(NoGroupsMsg)
            End If

            ' Select the first item by default
            If cmbTileGroup.Items.Count > 0 Then
                cmbTileGroup.SelectedIndex = 0
            End If

        Catch ex As UnauthorizedAccessException
            ' This happens if the program doesn't have permission to read the folder
            MessageBox.Show($"Access denied to the folder: {selectedFolderName}", "Permission Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            ' Catch any other unexpected errors
            MessageBox.Show($"An error occurred while loading tile groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Tile-group combo change event handler - imports the selected group's JSON,
    ''' validates its tile data and updates the labels and preview
    ''' </summary>
    Private Sub cmbTileGroup_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbTileGroup.SelectedIndexChanged
        ' Change the selected tile group to display in the Viewport

        ' Dont look for the file "No Tile Groups Found.json"
        If cmbTileGroup.SelectedItem.ToString = NoGroupsMsg Then
            'Debug.WriteLine("Skip '" & NoGroupsMsg & ".json'")

            ' Clear the preview and the path label
            If TileGroupPreview IsNot Nothing Then
                TileGroupPreview.Dispose()
                TileGroupPreview = Nothing
            End If
            lblPath.Text = ""
            pnlTileGroupViewport.Invalidate()

            Return
        End If

        Dim selectedFileName As String = cmbTileGroup.SelectedItem.ToString()
        Dim selectedFileNamePath As String
        If TileGroupsSubFolder = RootCategoryLabel Then
            selectedFileNamePath = Path.Combine(TileGroupsFolder, selectedFileName & ".json")
        Else
            selectedFileNamePath = Path.Combine(TileGroupsFolder, TileGroupsSubFolder, selectedFileName & ".json")
        End If
        SelectedTileGroup = selectedFileName
        lblPath.Text = selectedFileNamePath
        'Debug.WriteLine(selectedFileNamePath)

        ' Set the global name from the file name and not whats in the json under "name"
        GlobalVariables.TileGroupFileName = selectedFileName

        ' Import the tile group from the JSON file, then convert the JArray
        ' rows into the 2D mapping array used for placement. Either step can
        ' fail on a malformed/hand-edited file.
        If Not ImportSingleTileGroup(selectedFileNamePath) OrElse
           Not ConvertJArrayToIntegerArray(TileGroupTiles) Then
            ' Malformed or unreadable tile group — clear every bit of state that
            ' already points at the new selection (name, labels, preview,
            ' armed placement mode) so the previously selected group can't be
            ' shown or placed under this group's name
            SelectedTileGroup = ""
            GlobalVariables.TileGroupFileName = ""
            GlobalVariables.TileGroupPlacementMode = False

            If TileGroupPreview IsNot Nothing Then
                TileGroupPreview.Dispose()
                TileGroupPreview = Nothing
            End If
            lblWidth.Text = ""
            lblHeight.Text = ""
            lblTileset.Text = ""
            lblTileGroupName.Text = ""
            lblNotes.Text = "N/A"
            pnlTileGroupViewport.Invalidate()

            MessageBox.Show(
                $"The tile group file could not be read:{vbCrLf}{selectedFileNamePath}{vbCrLf}{vbCrLf}The file may be malformed or hand-edited incorrectly.",
                "Invalid Tile Group", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Check if tile group data was loaded successfully
        If TileGroupTiles IsNot Nothing Then
            'Debug.WriteLine("TileGroupWidth: " & TileGroupWidth)
            'Debug.WriteLine("TileGroupHeight: " & TileGroupHeight)
            'Debug.WriteLine("TileSetName: " & TileSetName)
            'Debug.WriteLine("TileGroupName: " & TileGroupName)
            'Debug.WriteLine("TileGroupBmp: " & TileGroupBmp)
            'Debug.WriteLine("TileGroupNotes:" & TileGroupNotes)

            lblWidth.Text = TileGroupWidth
            lblHeight.Text = TileGroupHeight
            lblTileset.Text = TileSetName
            lblTileGroupName.Text = TileGroupName

            If TileGroupNotes = "" Then
                'Debug.WriteLine("No Notes")
                lblNotes.Text = "N/A"
            Else
                lblNotes.Text = TileGroupNotes
            End If

            ' Update viewport
            UpdateTileGroupPreview()
        End If
    End Sub

    ''' <summary>
    ''' Converts the tile-group JArray rows into the 2D integer array stored in
    ''' GlobalVariables.TileGroupTileMappings; returns False on malformed data
    ''' </summary>
    Private Function ConvertJArrayToIntegerArray(ByVal TileGroupTilesJArray As JArray) As Boolean
        ' Takes TileGroupTilesJArray and converts to integer array
        ' Saves the output in global variable called TileGroupTileMappings
        '
        ' Tile group JSONs are user-shareable and hand-editable, so the data
        ' can't be trusted: rows may be missing, jagged, non-arrays, or hold
        ' non-numeric tokens. Any of those clears the mappings and returns
        ' False instead of throwing out of the SelectedIndexChanged handler.

        If TileGroupTilesJArray Is Nothing OrElse TileGroupTilesJArray.Count = 0 Then
            GlobalVariables.TileGroupTileMappings = Nothing
            Return False
        End If

        Try
            ' Determine dimensions from the first row
            Dim rows As Integer = TileGroupTilesJArray.Count
            Dim firstRow As JArray = TryCast(TileGroupTilesJArray(0), JArray)
            If firstRow Is Nothing OrElse firstRow.Count = 0 Then
                GlobalVariables.TileGroupTileMappings = Nothing
                Return False
            End If
            Dim cols As Integer = firstRow.Count

            ' Build into a local first so a failure partway through can't
            ' leave a half-populated global array
            Dim mappings(cols - 1, rows - 1) As Integer

            For i As Integer = 0 To rows - 1
                Dim rowArray As JArray = TryCast(TileGroupTilesJArray(i), JArray)
                If rowArray Is Nothing OrElse rowArray.Count <> cols Then
                    ' Non-array or jagged row
                    GlobalVariables.TileGroupTileMappings = Nothing
                    Return False
                End If
                For j As Integer = 0 To cols - 1
                    mappings(j, i) = rowArray(j).Value(Of Integer)()  ' Swapped indices
                Next
            Next

            GlobalVariables.TileGroupTileMappings = mappings
            Return True

        Catch ex As Exception
            ' e.g. non-numeric token in a cell
            Debug.WriteLine("Malformed tile group data: " & ex.Message)
            GlobalVariables.TileGroupTileMappings = Nothing
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Imports a single tile group from a JSON file. Returns False if the file
    ''' is missing, unparseable or not in the expected format; on failure the
    ''' module-level tile group fields are left in their reset (empty) state.
    ''' </summary>
    ''' <param name="jsonFilePath">Path to the JSON file containing the tile group</param>
    Private Function ImportSingleTileGroup(jsonFilePath As String) As Boolean
        ' Load a single tile group
        Try
            ' Reset global variables
            currentTileGroupData = Nothing
            TileGroupTiles = Nothing
            TileGroupWidth = 0
            TileGroupHeight = 0
            TileSetName = ""
            TileGroupName = ""
            TileGroupBmp = ""
            TileGroupNotes = ""

            ' Read JSON file
            Dim jsonText As String = File.ReadAllText(jsonFilePath)
            Dim fileName As String = Path.GetFileNameWithoutExtension(jsonFilePath)

            ' Parse JSON
            currentTileGroupData = JObject.Parse(jsonText)

            ' Extract header information
            If currentTileGroupData.ContainsKey("header") Then
                Dim header = currentTileGroupData("header")
                TileGroupWidth = header("width").Value(Of Integer)()
                TileGroupHeight = header("height").Value(Of Integer)()
                TileSetName = header("tileset").Value(Of String)()
                TileGroupName = header("name").Value(Of String)()
                TileGroupBmp = header("bmp").Value(Of String)()
                TileGroupNotes = header("notes").Value(Of String)()
            Else
                Debug.WriteLine($"  Error: No header found in tile group JSON: {fileName}")
                Return False
            End If

            ' Store the tiles data
            If currentTileGroupData.ContainsKey("tiles") Then
                Dim tilesData = currentTileGroupData("tiles")

                ' Check if tiles is an array of arrays (perRow format)
                If tilesData.Type = JTokenType.Array AndAlso
               tilesData.Count > 0 AndAlso
               tilesData(0).Type = JTokenType.Array Then

                    ' It's in perRow format
                    TileGroupTiles = DirectCast(tilesData, JArray)

                    ' Verify dimensions from the actual data
                    If TileGroupHeight <> tilesData.Count Then
                        TileGroupHeight = tilesData.Count
                    End If

                    If TileGroupWidth <> tilesData(0).Count Then
                        TileGroupWidth = tilesData(0).Count
                    End If
                Else
                    Debug.WriteLine($"  Error: JSON file '{fileName}' is not in the expected per-row format")
                    Return False
                End If
            Else
                Debug.WriteLine($"  Error: No tiles found in JSON: {fileName}")
                Return False
            End If

            Return True
        Catch ex As Exception
            Debug.WriteLine("  Error importing tile group: " & ex.Message)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Renders the loaded tile group into the preview bitmap and refreshes the viewport
    ''' </summary>
    Public Sub UpdateTileGroupPreview()
        ' Update viewport on form

        Try
            ' Clean up previous bitmap if it exists
            If TileGroupPreview IsNot Nothing Then
                TileGroupPreview.Dispose()
                TileGroupPreview = Nothing
            End If

            ' Check if we have tiles
            If TileGroupTileMappings Is Nothing OrElse
               TileGroupWidth <= 0 OrElse
               TileGroupHeight <= 0 Then

                Debug.WriteLine("Tile Group - No Tiles")
                pnlTileGroupViewport.Invalidate()
                Return
            End If

            ' Get reference to map and renderer
            If mainForm Is Nothing OrElse mainForm.CurrentLoadedMap Is Nothing OrElse mainForm.CurrentMapRenderer Is Nothing Then
                Debug.WriteLine("Cannot update tile group tiles preview - missing required references")
                pnlTileGroupViewport.Invalidate()
                Return
            End If

            ' Create a bitmap to show the copied tiles
            Dim tileSize As Integer = 32 ' Standard OP2 tile size
            Dim width As Integer = TileGroupWidth * tileSize
            Dim height As Integer = TileGroupHeight * tileSize

            TileGroupPreview = New Bitmap(width, height)

            ' Get references we need for rendering
            Dim map = mainForm.CurrentLoadedMap
            Dim renderer = mainForm.CurrentMapRenderer

            ' Render each copied tile
            Using g As Graphics = Graphics.FromImage(TileGroupPreview)

                ' Draw a checkered background to indicate transparency
                Using checkerBrush As New SolidBrush(System.Drawing.Color.LightGray)
                    g.FillRectangle(checkerBrush, 0, 0, width, height)
                End Using

                ' Draw each tile
                For y As Integer = 0 To TileGroupHeight - 1
                    For x As Integer = 0 To TileGroupWidth - 1
                        Dim mappingIndex As Integer = TileGroupTileMappings(x, y)

                        ' Skip invalid mapping indices
                        If mappingIndex < 0 OrElse mappingIndex >= map.tileMappings.Count Then
                            Continue For
                        End If

                        ' Get the tileset index and image index for this mapping
                        Dim tilesetIndex As Integer = map.tileMappings(mappingIndex).tilesetIndex
                        Dim imageIndex As Integer = map.tileMappings(mappingIndex).tileGraphicIndex

                        ' Skip invalid indices
                        If tilesetIndex < 0 OrElse imageIndex < 0 Then
                            Continue For
                        End If

                        ' Try to get the tile image from the renderer.
                        ' NOTE: GetTileImageByIndices returns a REFERENCE to a
                        ' bitmap owned by the renderer's tileset cache — do not
                        ' dispose it, or subsequent renders (and the main map
                        ' view) lose that tile and show black/fallback.
                        Dim tileImage As Bitmap = Nothing
                        Try
                            tileImage = renderer.GetTileImageByIndices(tilesetIndex, imageIndex)
                            If tileImage Is Nothing Then
                                Continue For
                            End If

                            ' Draw the tile
                            g.DrawImage(tileImage, x * tileSize, y * tileSize)
                        Catch ex As Exception
                            Debug.WriteLine($"Error drawing tile at {x},{y}: {ex.Message}")
                        End Try
                    Next
                Next

                '' Draw border around the entire region
                'g.DrawRectangle(New Pen(System.Drawing.Color.DarkBlue, 2), 0, 0, width - 1, height - 1)
            End Using

            ' Set panel size to match the bitmap
            pnlTileGroupViewport.AutoScrollMinSize = New Size(width, height)

            ' Refresh display
            pnlTileGroupViewport.Invalidate()

            Debug.WriteLine($"Tile Group preview: {TileGroupWidth}×{TileGroupHeight} tiles")

        Catch ex As Exception
            Debug.WriteLine($"Error updating Tile Group preview: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Place button click event handler - tells the main form to enter
    ''' tile-group placement mode for the currently loaded group
    ''' </summary>
    Private Sub btnPlaceTileGroup_Click(sender As Object, e As EventArgs) Handles btnPlaceTileGroup.Click
        ' Make sure we have a tile group loaded
        If GlobalVariables.TileGroupTileMappings Is Nothing OrElse
       GlobalVariables.TileGroupWidth <= 0 OrElse
       GlobalVariables.TileGroupHeight <= 0 Then
            MessageBox.Show("No tile group selected. Please select a tile group first.",
                       "Cannot Place", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Make sure main form exists
        If mainForm Is Nothing Then
            MessageBox.Show("Cannot access main form.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Debug.WriteLine($"Initiating tile group placement: {GlobalVariables.TileGroupWidth}x{GlobalVariables.TileGroupHeight}")

        ' Tell the main form to enter tile group placement mode
        mainForm.InitializeTileGroupPlacementMode()
    End Sub

End Class