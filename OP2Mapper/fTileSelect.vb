Imports System.IO
Imports System.Diagnostics
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Text
Imports OP2UtilityDotNet
Imports OP2UtilityDotNet.OP2Map
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports OP2MapJsonTools

' fTileSelect.vb - Tool 3: "Tile Selection" palette window.
' Shows a preview of the currently copied tile region and lets the user
' export it as a reusable tile group (BMP + JSON).
Public Class fTileSelect

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

    ''' <summary>
    ''' Reference to main form
    ''' </summary>
    Private mainForm As fMain = Nothing

    ''' <summary>
    ''' Current bitmap of copied tiles
    ''' </summary>
    Private copiedTilesPreview As Bitmap = Nothing

    ''' <summary>
    ''' Variables for panel interaction
    ''' </summary>
    Private isDragging As Boolean = False
    Private startScrollPos As Point

    ''' <summary>
    ''' Last known copied region dimensions
    ''' </summary>
    Private lastKnownWidth As Integer = 0
    Private lastKnownHeight As Integer = 0

    ''' <summary>
    ''' Constructor
    ''' </summary>
    Public Sub New()
        InitializeComponent()

        ' Set up panel for double buffering to reduce flicker
        pnlTilePasteViewport.GetType().GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(pnlTilePasteViewport, True)

        ' Configure panel properties
        pnlTilePasteViewport.AutoScroll = True
        pnlTilePasteViewport.BorderStyle = BorderStyle.FixedSingle
        pnlTilePasteViewport.BackColor = System.Drawing.Color.DarkGray

        ' Add event handlers
        AddHandler pnlTilePasteViewport.Paint, AddressOf PnlTilePasteViewport_Paint
        AddHandler pnlTilePasteViewport.MouseDown, AddressOf PnlTilePasteViewport_MouseDown
        AddHandler pnlTilePasteViewport.MouseMove, AddressOf PnlTilePasteViewport_MouseMove
        AddHandler pnlTilePasteViewport.MouseUp, AddressOf PnlTilePasteViewport_MouseUp

        ' Initialize context menu
        InitializeContextMenu()
    End Sub

    ''' <summary>
    ''' Form load event handler
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Event arguments</param>
    Private Sub fTileSelect_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ToolPaletteHelper.ConfigureAsToolPalette(Me)
        ToolPaletteHelper.RestorePosition(Me, "TileSelect")

        ' Get reference to main form
        mainForm = TryCast(Application.OpenForms("fMain"), fMain)

        ' Set form properties (TopMost is set by ConfigureAsToolPalette per
        ' the View → Tool Windows Always On Top setting)
        Me.FormBorderStyle = FormBorderStyle.FixedToolWindow
        Me.Text = "Tile Selection"
        Me.Icon = My.Resources.lists

        Me.Width = 498
        pnlTilePasteViewport.Width = 480

        'Me.Height = 284
        'pnlTilePasteViewport.Height = 243
        Me.Height = 484
        'Me.Height = 498
        pnlTilePasteViewport.Height = Me.Height - 41

        ' Initialize the copied tiles preview if we already have data
        UpdateCopiedTilesPreview()
    End Sub

    ''' <summary>
    ''' Form closing event handler
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Form closing event arguments</param>
    Private Sub fTileSelect_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        ToolPaletteHelper.SavePosition(Me, "TileSelect")
        ToolPaletteHelper.UnregisterToolForm(Me)

        If copiedTilesPreview IsNot Nothing Then        ' Clean up resources
            copiedTilesPreview.Dispose()
            copiedTilesPreview = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Initializes the context menu for the tile selection panel
    ''' </summary>
    Private Sub InitializeContextMenu()
        ' Create a new context menu
        Dim contextMenu As New ContextMenuStrip()

        ' Add Export Tile Group menu item
        Dim exportTileGroupMenuItem As New ToolStripMenuItem("Export Tile Group")
        AddHandler exportTileGroupMenuItem.Click, AddressOf ExportTileGroup_Click
        contextMenu.Items.Add(exportTileGroupMenuItem)

        ' Assign the context menu to the panel
        pnlTilePasteViewport.ContextMenuStrip = contextMenu
    End Sub

    ''' <summary>
    ''' Updates the display with the current copied tiles
    ''' </summary>
    Public Sub UpdateCopiedTilesPreview()
        Try
            ' Clean up previous bitmap if it exists
            If copiedTilesPreview IsNot Nothing Then
                copiedTilesPreview.Dispose()
                copiedTilesPreview = Nothing
            End If

            ' Check if we have copied tiles
            If GlobalVariables.CopiedTileMappings Is Nothing OrElse
               GlobalVariables.CopiedRegionWidth <= 0 OrElse
               GlobalVariables.CopiedRegionHeight <= 0 Then

                ' Update form title to indicate no copied tiles
                Me.Text = "Tile Selection - No Copied Tiles"
                pnlTilePasteViewport.Invalidate()
                Return
            End If

            ' Save dimensions for comparison later
            lastKnownWidth = GlobalVariables.CopiedRegionWidth
            lastKnownHeight = GlobalVariables.CopiedRegionHeight

            ' Update form title with dimensions
            Me.Text = $"Tile Selection - {lastKnownWidth}×{lastKnownHeight} Tiles"

            ' Get reference to map and renderer
            If mainForm Is Nothing OrElse mainForm.CurrentLoadedMap Is Nothing OrElse mainForm.CurrentMapRenderer Is Nothing Then
                Debug.WriteLine("Cannot update copied tiles preview - missing required references")
                pnlTilePasteViewport.Invalidate()
                Return
            End If

            ' Create a bitmap to show the copied tiles
            Dim tileSize As Integer = 32 ' Standard OP2 tile size
            Dim width As Integer = GlobalVariables.CopiedRegionWidth * tileSize
            Dim height As Integer = GlobalVariables.CopiedRegionHeight * tileSize

            copiedTilesPreview = New Bitmap(width, height)

            ' Get references we need for rendering
            Dim map = mainForm.CurrentLoadedMap
            Dim renderer = mainForm.CurrentMapRenderer

            ' Render each copied tile
            Using g As Graphics = Graphics.FromImage(copiedTilesPreview)
                ' Draw a checkered background to indicate transparency
                Using checkerBrush As New SolidBrush(System.Drawing.Color.LightGray)
                    g.FillRectangle(checkerBrush, 0, 0, width, height)
                End Using

                ' Draw each tile
                For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                    For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                        Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)

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

                        ' Try to get the tile image from the renderer
                        Dim tileImage As Bitmap = Nothing
                        Try
                            tileImage = renderer.GetTileImageByIndices(tilesetIndex, imageIndex)
                            If tileImage Is Nothing Then
                                Continue For
                            End If

                            ' Draw the tile
                            g.DrawImage(tileImage, x * tileSize, y * tileSize)

                            ' Draw grid line around the tile
                            g.DrawRectangle(Pens.Gray, x * tileSize, y * tileSize, tileSize, tileSize)

                        Catch ex As Exception
                            Debug.WriteLine($"Error drawing copied tile at {x},{y}: {ex.Message}")
                        Finally
                            ' Ensure we dispose the tile image
                            If tileImage IsNot Nothing Then
                                tileImage.Dispose()
                            End If
                        End Try
                    Next
                Next

                ' Draw border around the entire region
                g.DrawRectangle(New Pen(System.Drawing.Color.DarkBlue, 2), 0, 0, width - 1, height - 1)
            End Using

            ' Set panel size to match the bitmap
            pnlTilePasteViewport.AutoScrollMinSize = New Size(width, height)

            ' Refresh display
            pnlTilePasteViewport.Invalidate()

            Debug.WriteLine($"Updated copied tiles preview: {GlobalVariables.CopiedRegionWidth}×{GlobalVariables.CopiedRegionHeight} tiles")

        Catch ex As Exception
            Debug.WriteLine($"Error updating copied tiles preview: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Check if copied region has changed and update if needed
    ''' </summary>
    Public Sub CheckForCopiedRegionChanges()
        ' If dimensions have changed, update the preview
        If GlobalVariables.CopiedRegionWidth <> lastKnownWidth OrElse
           GlobalVariables.CopiedRegionHeight <> lastKnownHeight OrElse
           (GlobalVariables.CopiedTileMappings Is Nothing AndAlso copiedTilesPreview IsNot Nothing) OrElse
           (GlobalVariables.CopiedTileMappings IsNot Nothing AndAlso copiedTilesPreview Is Nothing) Then

            UpdateCopiedTilesPreview()
        End If
    End Sub

    ''' <summary>
    ''' Paint event handler for the tile paste viewport panel
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Paint event arguments</param>
    Private Sub PnlTilePasteViewport_Paint(sender As Object, e As PaintEventArgs)
        ' If no preview exists, show a message
        If copiedTilesPreview Is Nothing Then
            e.Graphics.Clear(System.Drawing.Color.LightGray)
            Using font As New Font("Arial", 10)
                e.Graphics.DrawString("No tiles copied", font, Brushes.Black, 10, 10)
                e.Graphics.DrawString("Use Copy (Ctrl+C) to copy tiles", font, Brushes.Black, 10, 30)
            End Using
            Return
        End If

        ' Draw the copied tiles preview
        e.Graphics.DrawImage(copiedTilesPreview,
                            -Math.Abs(pnlTilePasteViewport.AutoScrollPosition.X),
                            -Math.Abs(pnlTilePasteViewport.AutoScrollPosition.Y))
    End Sub

    ''' <summary>
    ''' Mouse down event handler for the tile paste viewport panel
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Mouse event arguments</param>
    Private Sub PnlTilePasteViewport_MouseDown(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Right Then
            'Debug.WriteLine("PnlTilePasteViewport_MouseDown")

            ' Start dragging for panning the view
            isDragging = True
            startScrollPos = e.Location
            pnlTilePasteViewport.Cursor = Cursors.Hand
        End If
    End Sub

    ''' <summary>
    ''' Mouse move event handler for the tile paste viewport panel
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Mouse event arguments</param>
    Private Sub PnlTilePasteViewport_MouseMove(sender As Object, e As MouseEventArgs)
        If isDragging AndAlso e.Button = MouseButtons.Right Then
            ' Calculate the scroll delta
            Dim deltaX As Integer = startScrollPos.X - e.X
            Dim deltaY As Integer = startScrollPos.Y - e.Y

            ' Apply the scroll (note: AutoScrollPosition returns negative values)
            Dim newX As Integer = Math.Abs(pnlTilePasteViewport.AutoScrollPosition.X) + deltaX
            Dim newY As Integer = Math.Abs(pnlTilePasteViewport.AutoScrollPosition.Y) + deltaY

            pnlTilePasteViewport.AutoScrollPosition = New Point(newX, newY)

            startScrollPos = e.Location
        End If
    End Sub

    ''' <summary>
    ''' Mouse up event handler for the tile paste viewport panel
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Mouse event arguments</param>
    Private Sub PnlTilePasteViewport_MouseUp(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Right Then
            isDragging = False
            pnlTilePasteViewport.Cursor = Cursors.Default

            ' Only show context menu if there was no drag operation
            Dim dragDistance As Double = Math.Sqrt(Math.Pow(e.X - startScrollPos.X, 2) + Math.Pow(e.Y - startScrollPos.Y, 2))
            If dragDistance < 5 AndAlso copiedTilesPreview IsNot Nothing Then
                ' Show context menu at this position
                pnlTilePasteViewport.ContextMenuStrip.Show(pnlTilePasteViewport, e.Location)
            End If
        End If
    End Sub

    ''' <summary>
    ''' Positions this form relative to other editor forms
    ''' </summary>
    ''' <param name="referenceForm">The form to position relative to</param>
    Public Sub PositionWindowRelativeToOthers(referenceForm As Form)
        If referenceForm Is Nothing Then Return

        ' Get the screen working area to ensure we stay within bounds
        Dim workingArea As Rectangle = Screen.FromControl(referenceForm).WorkingArea

        ' Find other editor forms
        Dim tileSetForm As Form = Nothing
        For Each form As Form In Application.OpenForms
            If TypeOf form Is fTileSet Then
                tileSetForm = form
                Exit For
            End If
        Next

        If tileSetForm IsNot Nothing Then
            ' Position below the TileSet form
            Dim newX As Integer = tileSetForm.Location.X
            Dim newY As Integer = tileSetForm.Location.Y + tileSetForm.Height + 1

            ' Ensure we stay on screen
            If newY + Me.Height > workingArea.Bottom Then
                ' Not enough room below - try to position to the right instead
                newX = tileSetForm.Location.X + tileSetForm.Width + 1
                newY = tileSetForm.Location.Y

                ' If still not enough room, position at bottom of screen
                If newX + Me.Width > workingArea.Right Then
                    newX = workingArea.Right - Me.Width
                    newY = workingArea.Bottom - Me.Height
                End If
            End If

            Me.Location = New Point(newX, newY)
        Else
            ' No tile set form - position to the right of the reference form
            Dim newX As Integer = referenceForm.Location.X + referenceForm.Width + 1
            Dim newY As Integer = referenceForm.Location.Y

            ' Ensure we stay on screen
            If newX + Me.Width > workingArea.Right Then
                newX = workingArea.Right - Me.Width
            End If

            If newY + Me.Height > workingArea.Bottom Then
                newY = workingArea.Bottom - Me.Height
            End If

            Me.Location = New Point(newX, newY)
        End If
    End Sub

    ''' <summary>
    ''' Handles the Export Tile Group menu item click
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Event arguments</param>
    Private Sub ExportTileGroup_Click(sender As Object, e As EventArgs)
        ' Check if we have any tiles to export
        If copiedTilesPreview Is Nothing Then
            MessageBox.Show("No tiles to export. Please copy a tile group first.", "Export Tiles", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Ask the user for a name for this tile group
        Dim tileName As String = InputBox("Enter a name for this tile group:", "Export Tile Group", $"TileGroup_{GlobalVariables.CopiedRegionWidth}x{GlobalVariables.CopiedRegionHeight}")

        ' If user cancels or enters an empty name, abort
        If String.IsNullOrEmpty(tileName) Then
            Return
        End If

        ' Sanitize the filename (remove invalid characters)
        tileName = String.Join("_", tileName.Split(Path.GetInvalidFileNameChars()))

        ' Tile-group exports always land in <Tilegroups>\Exported\. Keeping
        ' user exports in their own subfolder means the Tile Groups window
        ' shows them under a dedicated "Exported" category alongside any
        ' curated/shared sets the user has dropped in by hand.
        If String.IsNullOrEmpty(My.Settings.Tilegroups) Then
            My.Settings.Tilegroups = Path.Combine(Application.StartupPath, "Tilegroups")
        End If
        Dim exportFolder As String = Path.Combine(My.Settings.Tilegroups, "Exported")
        If Not Directory.Exists(exportFolder) Then
            Directory.CreateDirectory(exportFolder)
        End If

        ' Export the BMP + JSON together so the Tile Groups window can use both
        ExportTileGroupToBmp(tileName)
        ExportTileGroupToJson(tileName, JsonExportFormat.PerRowPadded)

        ' Refresh the Tile Groups window (if open) so the export shows up in
        ' the list immediately. We auto-select the "Exported" folder; the user
        ' can pick the specific tile group themselves.
        Dim tg As fTileGroups = TryCast(Application.OpenForms("fTileGroups"), fTileGroups)
        If tg IsNot Nothing Then
            tg.RefreshFromDisk("Exported")
        End If

        'Debug.WriteLine($"Tile group {tileName} export complete")
    End Sub

    ''' <summary>
    ''' Exports a tile group to a JSON file
    ''' </summary>
    ''' <param name="tileGroupName">Name of the tile group</param>
    ''' <param name="format">Format to use for export</param>
    Private Sub ExportTileGroupToJson(tileGroupName As String, format As JsonExportFormat)
        ' Should this function become part of OP2MapJsonToolsLibrary ??

        Try
            ' Make sure we have valid copied tile data
            If GlobalVariables.CopiedTileMappings Is Nothing Then
                Debug.WriteLine("No tile data to export")
                Return
            End If

            ' Create full path for the exported file (goes in <Tilegroups>\Exported\)
            Dim filePath As String = Path.Combine(My.Settings.Tilegroups, "Exported", $"{tileGroupName}.json")

            ' Get reference to the map
            Dim map = mainForm.CurrentLoadedMap
            If map Is Nothing Then
                Debug.WriteLine("No map loaded, cannot export tile data")
                Return
            End If

            ' Create the JSON object
            Dim json As New JObject()

            ' Identify the tileset used for this tile group
            Dim tilesetName As String = "well00" 'well00' is the default value and default tileset prefix

            ' Find the first valid tile to determine the tileset
            Dim foundValidTile As Boolean = False
            For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                    If foundValidTile Then Continue For

                    Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)

                    ' Skip invalid mapping indices
                    If mappingIndex >= 0 AndAlso mappingIndex < map.tileMappings.Count Then
                        ' Get the tileset index for this mapping
                        Dim tilesetIndex As Integer = map.tileMappings(mappingIndex).tilesetIndex

                        ' Skip invalid tileset indices
                        If tilesetIndex >= 0 AndAlso tilesetIndex < map.tilesetSources.Count Then
                            ' Get the tileset filename
                            Dim tilesetFilename As String = map.tilesetSources(tilesetIndex).tilesetFilename
                            If Not String.IsNullOrEmpty(tilesetFilename) AndAlso tilesetFilename.Length >= 6 Then
                                ' Extract the 6-character prefix
                                tilesetName = tilesetFilename.Substring(0, 6)
                                foundValidTile = True
                            End If
                        End If
                    End If
                Next
                If foundValidTile Then Exit For
            Next

            ' Add header information
            Dim header As New JObject()
            header.Add("width", GlobalVariables.CopiedRegionWidth)
            header.Add("height", GlobalVariables.CopiedRegionHeight)
            header.Add("tileset", tilesetName)
            header.Add("name", tileGroupName)
            header.Add("bmp", tileGroupName & ".bmp")
            header.Add("notes", "Exported from OP2Mapper")

            '' Add format information ??
            'If format = JsonExportFormat.Original Then
            '    header.Add("format", "original")
            'Else
            '    header.Add("format", "perrow")
            'End If

            json.Add("header", header)

            ' Add tile mapping data based on selected format
            If format = JsonExportFormat.Original Then
                'Get rid of this format..

                ' Original flat array format
                Dim tilesArray As New JArray()
                For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                    For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                        Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)
                        tilesArray.Add(mappingIndex)
                    Next
                Next
                json.Add("tiles", tilesArray)

                ' Write with standard formatting
                File.WriteAllText(filePath, JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8)

            Else ' PerRow or PerRowPadded
                ' Use array of arrays format
                Dim tilesArray As New JArray()
                For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                    Dim rowArray As New JArray()
                    For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                        Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)
                        rowArray.Add(mappingIndex)
                    Next
                    tilesArray.Add(rowArray)
                Next
                json.Add("tiles", tilesArray)

                ' Write to file based on format
                If format = JsonExportFormat.PerRow Then
                    'Get rid of this format..

                    File.WriteAllText(filePath, JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8)

                    ' Process the file to format rows on single lines with proper commas
                    Dim fileContent As String = File.ReadAllText(filePath)
                    Dim processedContent As String = FormatJsonArrays(fileContent, False)
                    File.WriteAllText(filePath, processedContent)

                Else    ' PerRowPadded
                    ' Format to keep. Make things more simple, One uniform format.

                    File.WriteAllText(filePath, JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8)

                    ' Process the file to format rows with padded numbers and proper commas
                    Dim fileContent As String = File.ReadAllText(filePath)
                    Dim processedContent As String = FormatJsonArrays(fileContent, True)
                    File.WriteAllText(filePath, processedContent)
                End If
            End If

            Debug.WriteLine("Tile group exported to JSON: " & filePath)

        Catch ex As Exception
            MessageBox.Show($"Error exporting tile group to JSON: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Debug.WriteLine($"Error in ExportTileGroupToJson: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Exports a tile group to a BMP image file
    ''' </summary>
    ''' <param name="tileName">Name of the tile group</param>
    Private Sub ExportTileGroupToBmp(tileName As String)
        Try
            ' Make sure we have a valid copiedTilesPreview
            If copiedTilesPreview Is Nothing Then
                Debug.WriteLine("No tile preview to export")
                Return
            End If

            ' Create full path for the exported file (goes in <Tilegroups>\Exported\)
            Dim filePath As String = Path.Combine(My.Settings.Tilegroups, "Exported", $"{tileName}.bmp")

            ' Create a copy of the preview without the grid lines and border
            Dim exportBitmap As New Bitmap(copiedTilesPreview.Width, copiedTilesPreview.Height)

            ' Get references we need for rendering
            Dim map = mainForm.CurrentLoadedMap
            Dim renderer = mainForm.CurrentMapRenderer

            ' Re-render each copied tile without grid lines
            Using g As Graphics = Graphics.FromImage(exportBitmap)
                ' Fill with transparent background
                g.Clear(System.Drawing.Color.Transparent)

                ' Draw each tile
                For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                    For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                        Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)

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

                        ' Try to get the tile image from the renderer
                        Dim tileImage As Bitmap = Nothing
                        Try
                            tileImage = renderer.GetTileImageByIndices(tilesetIndex, imageIndex)
                            If tileImage Is Nothing Then
                                Continue For
                            End If

                            ' Draw the tile without any grid or borders
                            g.DrawImage(tileImage, x * 32, y * 32)

                        Catch ex As Exception
                            Debug.WriteLine($"Error drawing export tile at {x},{y}: {ex.Message}")
                        Finally
                            ' Ensure we dispose the tile image
                            If tileImage IsNot Nothing Then
                                tileImage.Dispose()
                            End If
                        End Try
                    Next
                Next
            End Using

            ' Save the image as BMP
            exportBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp)

            ' Dispose the temporary bitmap
            exportBitmap.Dispose()

            ' Optionally open the folder containing the exported file
            If MessageBox.Show("Do you want to open the folder containing the exported file?", "Open Folder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                Process.Start("explorer.exe", $"/select,{filePath}")
            End If

            Debug.WriteLine("Tile group exported to BMP: " & filePath)

        Catch ex As Exception
            MessageBox.Show($"Error exporting tile group: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Debug.WriteLine($"Error in ExportTileGroupToBmp: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Formats JSON array content with proper commas between elements
    ''' </summary>
    ''' <param name="jsonContent">The JSON content to format</param>
    ''' <param name="padNumbers">Whether to pad numbers for alignment</param>
    ''' <returns>Formatted JSON string with proper commas</returns>
    Private Function FormatJsonArrays(jsonContent As String, padNumbers As Boolean) As String
        Dim lines As String() = jsonContent.Split(New String() {Environment.NewLine}, StringSplitOptions.None)
        Dim result As New List(Of String)

        Dim inTilesSection As Boolean = False
        Dim inArrayRow As Boolean = False
        Dim currentRow As String = ""

        For i As Integer = 0 To lines.Length - 1
            Dim line As String = lines(i).TrimEnd()

            ' Check if we're entering the tiles section
            If line.Contains("""tiles""") Then
                inTilesSection = True
                result.Add(line)
                Continue For
            End If

            ' Check if we're at the end of the tiles section
            If inTilesSection AndAlso line = "}" Then
                inTilesSection = False
                result.Add(line)
                Continue For
            End If

            ' Handle lines within the tiles section
            If inTilesSection Then
                ' Start of an array row
                If line.TrimStart().StartsWith("[") AndAlso Not line.TrimEnd().EndsWith("]") Then
                    inArrayRow = True
                    currentRow = line.TrimEnd()

                    ' End of an array row
                ElseIf inArrayRow AndAlso (line.TrimEnd().EndsWith("],") OrElse line.TrimEnd().EndsWith("]")) Then
                    currentRow += " " + line.Trim()

                    ' Apply padding if requested
                    If padNumbers Then
                        currentRow = PadNumbersInRow(currentRow)
                    End If

                    result.Add(currentRow)
                    inArrayRow = False
                    currentRow = ""

                    ' Middle of an array row
                ElseIf inArrayRow Then
                    currentRow += " " + line.Trim()

                    ' Regular line in the tiles section (not part of an array row)
                Else
                    result.Add(line)
                End If

                ' Lines outside the tiles section
            Else
                result.Add(line)
            End If
        Next

        Return String.Join(Environment.NewLine, result)
    End Function

    ''' <summary>
    ''' Pads numbers in a JSON array row for alignment while ensuring commas are preserved
    ''' </summary>
    ''' <param name="rowLine">The row line to pad</param>
    ''' <returns>A string with properly padded numbers and commas</returns>
    Private Function PadNumbersInRow(rowLine As String) As String
        ' Extract the beginning bracket part
        Dim startBracketIndex As Integer = rowLine.IndexOf("[")
        Dim prefix As String = rowLine.Substring(0, startBracketIndex + 1)

        ' Extract the end bracket part
        Dim endBracketIndex As Integer = rowLine.LastIndexOf("]")
        Dim suffix As String = rowLine.Substring(endBracketIndex)

        ' Extract the content between brackets
        Dim content As String = rowLine.Substring(startBracketIndex + 1, endBracketIndex - startBracketIndex - 1).Trim()

        ' Split by commas to get individual numbers
        Dim numberParts As String() = content.Split(","c)

        ' Trim and collect all numbers
        Dim trimmedNumbers As New List(Of String)
        For Each part As String In numberParts
            trimmedNumbers.Add(part.Trim())
        Next

        ' Find the maximum width needed for padding
        Dim maxWidth As Integer = 4 ' Default minimum width
        For Each numStr As String In trimmedNumbers
            If numStr.Length > maxWidth Then
                maxWidth = numStr.Length
            End If
        Next

        ' Pad each number to the maximum width
        Dim paddedNumbers As New List(Of String)
        For Each numStr As String In trimmedNumbers
            paddedNumbers.Add(numStr.PadLeft(maxWidth))
        Next

        ' Reconstruct the row with padded numbers and proper commas
        Return prefix & " " & String.Join(", ", paddedNumbers) & " " & suffix
    End Function

    ''' <summary>
    ''' Helper function for inline assignment in While loops
    ''' </summary>
    Private Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
        target = value
        Return value
    End Function
End Class