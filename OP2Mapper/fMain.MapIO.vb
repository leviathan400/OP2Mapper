Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Media
Imports System.Reflection
Imports System.Security.Policy
Imports System.Windows.Forms
Imports OP2MapJsonTools
Imports OP2UtilityDotNet
Imports OP2UtilityDotNet.Bitmap
Imports OP2UtilityDotNet.OP2Map
Imports OP2UtilityDotNet.Sprite

' fMain.MapIO.vb
' Partial class of fMain (split out of fMain.vb).
' Map loading/saving, the recent-files list, drag-drop open and new-map initialization.
Partial Public Class fMain

#Region "Map Loading and Handling"

    ''' <summary>
    ''' Loads a map file and initializes the renderer
    ''' </summary>
    Public Function LoadMapFile(mapFilePath As String) As Boolean
        Debug.WriteLine("Attempting to load map: " & mapFilePath)

        ' Drop the enclosed-areas highlight overlay — its tile list is tied
        ' to the OLD map's coordinates and would be misleading on the new one.
        ClearEnclosedHighlight()

        ' Check if edit mode is currently enabled, and if so, disable it
        If EditModeEnabled Then
            Debug.WriteLine("Disabling Edit Mode before loading new map")
            ToggleEditMode() ' This will disable edit mode, close the editing forms, etc.
        End If

        ' Check if resource manager is initialized
        If resourceManager Is Nothing Then
            If String.IsNullOrEmpty(My.Settings.OP2Path) OrElse Not Directory.Exists(My.Settings.OP2Path) Then
                ShowSettingsDialog()

                If resourceManager Is Nothing Then
                    Return False
                End If
            Else
                Try
                    resourceManager = New ResourceManager(My.Settings.OP2Path)
                Catch ex As Exception
                    MessageBox.Show("Error initializing resource manager: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return False
                End Try
            End If
        End If

        Try
            ' Load the map file
            currentMap = Map.ReadMap(mapFilePath)
            currentMapPath = Path.GetDirectoryName(mapFilePath)

            ' Calculate and store the last row index - As its a special row - does no get rendered in-game
            ' And cellTypes should stay as impassable 
            GlobalVariables.MapLastRowIndex = currentMap.HeightInTiles - 1

            Debug.WriteLine($"Map dimensions: Width={currentMap.WidthInTiles()}, Height={currentMap.HeightInTiles}")
            'Debug.WriteLine($"Last row index: {GlobalVariables.MapLastRowIndex}")

            ' Initialize the renderer with the map. The MapRenderer constructor
            ' already loads the map's tilesets, so there's no separate
            ' LoadMapTilesets() call here — doing both ran the full
            ' BMP-load/convert/slice pipeline twice on every map open.
            InitializeMapRenderer()

            ' Check tileset loading status
            ShowTilesetLoadingStatus()

            ' Clear the undo/redo stacks for the new map
            ClearUndoRedoStacks()

            ' Clear copy/paste state — the clipboard's tile mappings reference
            ' the previous map's tilesets and shouldn't carry over.
            ClearClipboardData()

            ' Set the default render mode to GameMap
            CurrentRenderMode = RenderMode.GameMap

            currentMapFile = Path.GetFileName(mapFilePath)

            ' Fresh load — no unsaved changes yet.
            mapIsDirty = False
            RefreshWindowTitle()
            lblToolStripMapName.Text = currentMapFile
            lblToolStripMapPath.Text = currentMapPath & "\" & currentMapFile

            lblMapName.Text = "Map Name: " & currentMapFile
            lblMapSize.Text = "Map Size: " & currentMap.WidthInTiles & " x " & currentMap.HeightInTiles

            ' Add the map image size information
            Dim mapWidthPixels As Integer = CInt(currentMap.WidthInTiles()) * 32
            Dim mapHeightPixels As Integer = CInt(currentMap.HeightInTiles()) * 32
            lblMapImage.Text = "Map Image: " & mapWidthPixels & " x " & mapHeightPixels & " pixels"

            lblTileCount.Text = "Tile Count: " & currentMap.TileCount
            lblZoomLevel.Text = "Zoom Level: 100%"

            ' Reset view
            zoomLevel = 1.0F
            offsetX = 0
            offsetY = 0

            ' Enable save and close options (look up by Name — index-based
            ' lookup was off by one and never enabled Close Map).
            Dim saveMenuItem As ToolStripMenuItem = DirectCast(fileMenu.DropDownItems("mnuSaveMap"), ToolStripMenuItem)
            saveMenuItem.Enabled = True

            Dim closeMenuItem As ToolStripMenuItem = DirectCast(fileMenu.DropDownItems("mnuCloseMap"), ToolStripMenuItem)
            closeMenuItem.Enabled = True

            ' Reset view settings when loading a new map
            ResetViewSettings()

            ' Enable render + image menu
            renderMenu.Enabled = True
            imageMenu.Enabled = True

            ' Enable the zoom and center buttons
            Dim btnResetZoom As Button = DirectCast(FindControl("btnResetZoom"), Button)
            If btnResetZoom IsNot Nothing Then
                btnResetZoom.Enabled = True
            End If

            Dim btnCenterView As Button = DirectCast(FindControl("btnCenterView"), Button)
            If btnCenterView IsNot Nothing Then
                btnCenterView.Enabled = True
            End If

            ' Enable edit mode button now that we have loaded a map, so that it can be clicked
            ' We don't enable Edit Mode itself until the user clicks the button
            btnEditMode.Enabled = True
            btnEditMode.Text = "Enable Edit Mode"
            btnEditMode.BackColor = SystemColors.Control

            ' If the mini-map form is still open from a previous edit session
            ' (Close Map leaves it open and blanked), populate it for the new map.
            RefreshMiniMap()

            ' Remember this file for the Recent Files menu.
            AddToRecentFiles(mapFilePath)

            ' Refresh display
            pnlMap.Invalidate()

            Debug.WriteLine(" Map loaded successfully: " & currentMapFile)

            Return True

        Catch ex As Exception
            MessageBox.Show("Error loading map: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function


    ''' <summary>
    ''' Handles File > New Map (Ctrl+N) - shows the new map form after the unsaved-changes check.
    ''' </summary>
    Private Sub NewMap_Click(sender As Object, e As EventArgs)
        ' Start a new map

        If Not ConfirmDiscardUnsavedChanges() Then Return

        fNewMap.Show()      'Show the new map form
        fNewMap.Focus()

    End Sub

    ''' <summary>
    ''' Handles File > Open Map (Ctrl+O) - shows the open dialog and loads the chosen map.
    ''' </summary>
    Private Sub OpenMap_Click(sender As Object, e As EventArgs)
        If Not ConfirmDiscardUnsavedChanges() Then Return

        ' Open map file dialog
        Using openDialog As New OpenFileDialog()
            openDialog.Filter = "Outpost 2 Map Files (*.map)|*.map|All Files (*.*)|*.*"
            openDialog.Title = "Open Outpost 2 Map"
            openDialog.InitialDirectory = My.Settings.OP2Path

            If openDialog.ShowDialog() = DialogResult.OK Then
                If LoadMapFile(openDialog.FileName) Then
                    ' Show message that map has been loaded
                    MessageBox.Show("Map loaded successfully.", "Map Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            End If
        End Using
    End Sub

#Region "Recent Files"

    ''' <summary>
    ''' Pulls the recent-files list from the INI and (re)populates the
    ''' submenu. Called once on startup, plus whenever a map is loaded.
    ''' </summary>
    Private Sub RefreshRecentFilesMenu()
        If mnuRecentFiles Is Nothing Then Return

        mnuRecentFiles.DropDownItems.Clear()

        If recentFiles.Count = 0 Then
            Dim placeholder As New ToolStripMenuItem("(empty)")
            placeholder.Enabled = False
            mnuRecentFiles.DropDownItems.Add(placeholder)
            Return
        End If

        For i As Integer = 0 To recentFiles.Count - 1
            Dim filePath As String = recentFiles(i)
            Dim item As New ToolStripMenuItem($"{i + 1}. {Path.GetFileName(filePath)}") With {
                .Tag = filePath,
                .ToolTipText = filePath
            }
            AddHandler item.Click, AddressOf RecentFile_Click
            mnuRecentFiles.DropDownItems.Add(item)
        Next
    End Sub

    ''' <summary>
    ''' Pushes a path to the top of the recent-files list, de-duplicating and
    ''' trimming to MaxRecentFiles. Persists to INI and refreshes the menu.
    ''' </summary>
    Private Sub AddToRecentFiles(path As String)
        If String.IsNullOrWhiteSpace(path) Then Return

        ' Remove existing occurrences (case-insensitive) so the newest add
        ' becomes the most-recent entry.
        recentFiles.RemoveAll(Function(p) String.Equals(p, path, StringComparison.OrdinalIgnoreCase))
        recentFiles.Insert(0, path)

        ' Cap at the configured max.
        If recentFiles.Count > ToolPaletteHelper.MaxRecentFiles Then
            recentFiles.RemoveRange(ToolPaletteHelper.MaxRecentFiles,
                                    recentFiles.Count - ToolPaletteHelper.MaxRecentFiles)
        End If

        ToolPaletteHelper.SaveRecentFiles(recentFiles)
        RefreshRecentFilesMenu()
    End Sub

    ''' <summary>
    ''' Handles a Recent Files entry click - loads the map, or removes the entry if the
    ''' file no longer exists on disk.
    ''' </summary>
    Private Sub RecentFile_Click(sender As Object, e As EventArgs)
        Dim item As ToolStripMenuItem = TryCast(sender, ToolStripMenuItem)
        If item Is Nothing Then Return
        Dim filePath As String = TryCast(item.Tag, String)
        If String.IsNullOrEmpty(filePath) Then Return

        ' Same unsaved-changes prompt as Open Map.
        If Not ConfirmDiscardUnsavedChanges() Then Return

        If Not File.Exists(filePath) Then
            MessageBox.Show($"File no longer exists:" & vbCrLf & filePath,
                            "Recent File Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            ' Drop the dead entry and refresh.
            recentFiles.Remove(filePath)
            ToolPaletteHelper.SaveRecentFiles(recentFiles)
            RefreshRecentFilesMenu()
            Return
        End If

        LoadMapFile(filePath)
    End Sub

#End Region

    ''' <summary>
    ''' Creates a fresh MapRenderer for the current map (disposing any previous one)
    ''' and resets the render mode to GameMap.
    ''' </summary>
    Private Sub InitializeMapRenderer()
        ' Create new renderer if we're using the MapRenderer class
        If mapRenderer IsNot Nothing Then
            mapRenderer.Dispose()
        End If

        mapRenderer = New MapRenderer(currentMap, resourceManager)
        mapRenderer.ShowGrid = GlobalVariables.ShowGrid

        ' Set global render mode to match
        GlobalVariables.CurrentRenderMode = GlobalVariables.RenderMode.GameMap

        mapRenderer.RenderMode = CType(GlobalVariables.CurrentRenderMode, MapRenderer.MapRenderMode)

    End Sub

    ''' <summary>
    ''' Refreshes titles, labels and menu state for a newly opened map, and turns off
    ''' edit mode (closing its tool forms) if it was left on from a previous map.
    ''' </summary>
    Private Sub UpdateUIForLoadedMap()
        'We call this when we open a map

        ' Update window title and status bar
        RefreshWindowTitle()
        lblToolStripMapName.Text = currentMapFile
        lblToolStripMapPath.Text = currentMapPath & "\" & currentMapFile

        ' Update property labels
        lblMapName.Text = "Map Name: " & currentMapFile
        lblMapSize.Text = "Map Size: " & currentMap.HeightInTiles & " x " & currentMap.WidthInTiles

        ' Add the map image size information
        Dim mapWidthPixels As Integer = CInt(currentMap.WidthInTiles()) * 32
        Dim mapHeightPixels As Integer = CInt(currentMap.HeightInTiles()) * 32
        lblMapImage.Text = "Map Image: " & mapWidthPixels & " x " & mapHeightPixels & " pixels"

        lblTileCount.Text = "Tile Count: " & currentMap.TileCount
        lblZoomLevel.Text = "Zoom Level: 100%"

        ' Enable menu options
        EnableMenuOptions(True)

        If EditModeEnabled = True Then
            'If a map is currently open and we are opening another map, check
            'if edit mode is currently enabled, if so disable it

            EditModeEnabled = False
            btnEditMode.Enabled = True
            btnEditMode.Text = "Enable Edit Mode"
            btnEditMode.BackColor = SystemColors.Control

            ' Create a list to store forms that need to be closed
            Dim formsToClose As New List(Of Form)

            ' Find forms to close without modifying the collection yet
            For Each form As Form In Application.OpenForms
                If TypeOf form Is fCellTypes OrElse TypeOf form Is fTileSet Then
                    formsToClose.Add(form)
                End If
            Next
            ' Now close the forms
            For Each form As Form In formsToClose
                form.Close()
            Next

            Debug.WriteLine("Opening map while Edit Mode is Enabled. Disable it.")

        ElseIf EditModeEnabled = False Then

            'Map loaded. Enable Edit Mode button.
            btnEditMode.Enabled = True
            btnEditMode.Text = "Enable Edit Mode"
            btnEditMode.BackColor = SystemColors.Control

        End If


    End Sub

    ''' <summary>
    ''' Enables/disables the map-dependent menu items (Save, Close, Render, Image).
    ''' </summary>
    Private Sub EnableMenuOptions(enabled As Boolean)
        ' Open Map is intentionally NOT toggled here — the user should always
        ' be able to open a map, including immediately after closing one.
        ' Look up by Name (not index) — the historical index-based lookup was
        ' off by one and silently never touched Close Map.
        Dim saveMenuItem As ToolStripMenuItem = DirectCast(fileMenu.DropDownItems("mnuSaveMap"), ToolStripMenuItem)
        Dim closeMenuItem As ToolStripMenuItem = DirectCast(fileMenu.DropDownItems("mnuCloseMap"), ToolStripMenuItem)

        saveMenuItem.Enabled = enabled
        closeMenuItem.Enabled = enabled
        renderMenu.Enabled = enabled
        imageMenu.Enabled = enabled
    End Sub

    ''' <summary>
    ''' Handles File > Save Map (Ctrl+S) - shows the save dialog, writes the map and
    ''' clears the dirty flag on success.
    ''' </summary>
    Private Sub SaveMap_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then
            Return
        End If

        Using saveDialog As New SaveFileDialog()
            saveDialog.Filter = "Outpost 2 Map Files (*.map)|*.map|All Files (*.*)|*.*"
            saveDialog.Title = "Save Outpost 2 Map"

            ' Default to the map's current folder. For a freshly-created New
            ' Map this is My.Settings.WorkingPath; for a loaded map it's where
            ' it was loaded from / last saved.
            If Not String.IsNullOrEmpty(currentMapPath) AndAlso Directory.Exists(currentMapPath) Then
                saveDialog.InitialDirectory = currentMapPath
            End If
            If Not String.IsNullOrEmpty(currentMapFile) Then
                saveDialog.FileName = currentMapFile
            End If

            If saveDialog.ShowDialog() = DialogResult.OK Then
                Try
                    currentMap.Write(saveDialog.FileName)
                    currentMapFile = Path.GetFileName(saveDialog.FileName)
                    currentMapPath = Path.GetDirectoryName(saveDialog.FileName)
                    SetMapDirty(False)
                    RefreshWindowTitle()
                    MessageBox.Show("Map saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Error saving map: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

    ''' <summary>
    ''' Handles File > Close Map - confirms with the user (Save/Discard if dirty), then
    ''' tears down edit mode, clipboard, undo stacks and map resources, and resets the UI.
    ''' </summary>
    Private Sub CloseMap_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then
            Return
        End If

        ' If there are unsaved changes, ask Save/Discard/Cancel.
        ' Otherwise, fall back to a simple Yes/No confirmation.
        Dim proceed As Boolean
        If mapIsDirty Then
            proceed = ConfirmDiscardUnsavedChanges()
        Else
            proceed = MessageBox.Show("Close the current map?", "Close Map",
                                      MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes
        End If

        If proceed Then

            ' If edit mode is on, toggle it off first — that closes every tool
            ' palette (TileSet, CellTypes, TileSelect, TileGroups, MiniMap)
            ' consistently instead of relying on CloseEditorForms which only
            ' covered three of them.
            If GlobalVariables.EditModeEnabled Then
                ToggleEditMode()
            End If

            ' Clear clipboard data
            ClearClipboardData()

            ' Close any open editor forms (no-op if edit mode was on, since
            ' ToggleEditMode already closed them; covers the unusual case
            ' where a tool palette is open without edit mode).
            CloseEditorForms()

            ' Clear the undo/redo stacks
            ClearUndoRedoStacks()

            ' Clean up resources
            CleanupMapResources()

            ' Reset UI
            ResetUI()

            Debug.WriteLine("Map closed.")

        End If
    End Sub

    ''' <summary>
    ''' Closes any open editor tool forms (CellTypes, TileSet, TileSelect).
    ''' </summary>
    Private Sub CloseEditorForms()
        Dim formsToClose As New List(Of Form)

        ' Find forms to close without modifying the collection
        For Each form As Form In Application.OpenForms
            If TypeOf form Is fCellTypes OrElse TypeOf form Is fTileSet OrElse TypeOf form Is fTileSelect Then
                formsToClose.Add(form)
            End If
        Next

        ' Close the forms
        For Each form As Form In formsToClose
            form.Close()
        Next
    End Sub

    ''' <summary>
    ''' Releases per-map state: clears the map references, disposes the renderer and
    ''' cached tile image, and resets the tile selection.
    ''' </summary>
    Private Sub CleanupMapResources()
        ' Clean up the current map
        currentMap = Nothing
        currentMapFile = Nothing
        currentMapPath = Nothing

        ' Reset last row tracking
        GlobalVariables.MapLastRowIndex = Nothing

        ' Dispose the renderer
        If mapRenderer IsNot Nothing Then
            mapRenderer.Dispose()
            mapRenderer = Nothing
        End If

        ' Clean up image resources
        If selectedTileImage IsNot Nothing Then
            selectedTileImage.Dispose()
            selectedTileImage = Nothing
        End If

        ' Reset selection
        currentTileX = -1
        currentTileY = -1

        Debug.WriteLine("CleanupMapResources")
    End Sub

    ''' <summary>
    ''' Returns the UI to its no-map-loaded state: placeholder labels, blank previews
    ''' and mini-map, disabled menus and edit mode button.
    ''' </summary>
    Private Sub ResetUI()
        ' Reset window title and status
        mapIsDirty = False
        RefreshWindowTitle()

        InitializeLabels()

        ' Clear tile previews
        picTilePreview.Image = Nothing
        picTilePreviewBig.Image = Nothing

        ' Blank the mini-map (currentMap is already Nothing by the time we get
        ' here, so RefreshThumbnail will clear its cached bitmap).
        If miniMapForm IsNot Nothing AndAlso Not miniMapForm.IsDisposed Then
            miniMapForm.ClearThumbnail()
        End If

        ' Drop the enclosed-areas highlight overlay on map close too.
        ClearEnclosedHighlight()

        ' Disable menu options
        EnableMenuOptions(False)

        ' Reset edit mode
        EditModeEnabled = False
        btnEditMode.Enabled = False
        btnEditMode.Text = "Enable Edit Mode"
        btnEditMode.BackColor = SystemColors.Control

        ' Clear the selected tool image
        If picSelectedTool IsNot Nothing Then
            If picSelectedTool.Image IsNot Nothing Then
                picSelectedTool.Image.Dispose()
                picSelectedTool.Image = Nothing
            End If
            picSelectedTool.BackColor = System.Drawing.Color.Black
        End If

        ' Refresh display
        pnlMap.Invalidate()
    End Sub

    ''' <summary>
    ''' Warns the user if some or all of the map's tilesets failed to load
    ''' (the map would render with missing tiles).
    ''' </summary>
    Private Sub ShowTilesetLoadingStatus()
        ' Check if any tilesets were loaded successfully
        Dim loadedTilesets As Integer = 0
        Dim totalNeededTilesets As Integer = 0

        If mapRenderer IsNot Nothing Then
            ' Get tileset loading status from MapRenderer
            Dim status = mapRenderer.GetTilesetLoadingStatus()
            loadedTilesets = status.Item1
            totalNeededTilesets = status.Item2

            ' Provide feedback if no tilesets were loaded
            If loadedTilesets = 0 And totalNeededTilesets > 0 Then
                ' No tilesets were loaded - inform the user
                Dim msg As String = "Could not load any tileset files. The map may not display correctly."
                MessageBox.Show(msg, "Tileset Loading Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            ElseIf loadedTilesets < totalNeededTilesets Then
                ' Some tilesets were loaded but not all
                Dim msg As String = $"Loaded {loadedTilesets} of {totalNeededTilesets} tilesets. Some tiles may not display correctly."
                MessageBox.Show(msg, "Tileset Loading Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        End If
    End Sub


#End Region

#Region "Drag Drop"

    ''' <summary>
    ''' Handles drag enter event to allow file dropping onto the map panel
    ''' </summary>
    Private Sub pnlMap_DragEnter(sender As Object, e As DragEventArgs) Handles pnlMap.DragEnter
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        Else
            e.Effect = DragDropEffects.None
        End If
    End Sub

    ''' <summary>
    ''' Processes files dropped onto the map panel and attempts to load them
    ''' </summary>
    Private Sub pnlMap_DragDrop(sender As Object, e As DragEventArgs) Handles pnlMap.DragDrop
        Dim files() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
        If files Is Nothing OrElse files.Length = 0 Then Return

        ' Only the first dropped file is loaded — loading several maps in a row
        ' would just replace each other.
        Dim file As String = files(0)
        If Not IO.File.Exists(file) Then Return

        If Not ConfirmDiscardUnsavedChanges() Then Return

        Debug.WriteLine("File dragged onto form: " & file)
        If LoadMapFile(file) Then
            Debug.WriteLine(" - Map loaded successfully")
            MessageBox.Show("Map loaded successfully.", "Map Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Else
            Debug.WriteLine(" - Failed to load map")
        End If
    End Sub

#End Region

#Region "Initialize New Map"

    ''' <summary>
    ''' Forces a full repaint of the map panel. Called by other forms (e.g. fNewMap)
    ''' after they change the map.
    ''' </summary>
    Public Sub RedrawMap()
        If pnlMap IsNot Nothing Then
            pnlMap.Invalidate()
        End If
    End Sub

    ''' <summary>
    ''' Sets the map filename (and optionally its folder) and updates the title/status
    ''' labels. Pass mapPath to override the directory the map is associated with —
    ''' used by New Map so the file isn't shown as living under the BlankMaps templates folder.
    ''' </summary>
    Public Sub SetMapFileName(fileName As String, Optional mapPath As String = Nothing)
        currentMapFile = fileName
        If mapPath IsNot Nothing Then
            currentMapPath = mapPath
        End If
        lblToolStripMapName.Text = fileName
        lblMapName.Text = "Map Name: " & fileName
        RefreshWindowTitle()

        ' Update the map path in the status bar
        If lblToolStripMapPath IsNot Nothing Then
            If Not String.IsNullOrEmpty(currentMapPath) Then
                lblToolStripMapPath.Text = Path.Combine(currentMapPath, fileName)
            Else
                lblToolStripMapPath.Text = fileName
            End If
        End If
    End Sub

#End Region

End Class
