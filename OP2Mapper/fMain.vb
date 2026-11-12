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


' OP2Mapper
' https://github.com/leviathan400/OP2Mapper
'
' Outpost 2 map editor and map maker. Edit the tiles and the cell types of a .map file.
'
'
' Outpost 2: Divided Destiny is a real-time strategy video game released in 1997.


''' <summary>
''' Main form for the OP2Mapper application
''' </summary>
Public Class fMain

    ' Keep references to tool forms so they stay in OpenForms collection
    Private tileSetForm As fTileSet         ' Tool 1
    Private cellTypesForm As fCellTypes     ' Tool 2
    Private tileSelectForm As fTileSelect   ' Tool 3
    Private tileGroupsForm As fTileGroups   ' Tool 4
    Private miniMapForm As fMiniMap         ' Tool 5

#Region "Private Fields"

    ' Map and resource data
    Public currentMap As Map
    Private currentMapFile As String
    Private currentMapPath As String
    Private resourceManager As OP2UtilityDotNet.ResourceManager
    Private mapRenderer As MapRenderer

    ' View state
    Private zoomLevel As Single = 1.0F
    Private offsetX As Integer = 0
    Private offsetY As Integer = 0
    Private isDragging As Boolean = False
    Private lastMouseX As Integer = 0
    Private lastMouseY As Integer = 0

    ' So that we dont show the menu when using right click to drag
    Private isRightDragging As Boolean = False
    ' Track right mouse behavior
    Private rightClickStartPosition As Point = Point.Empty

    ' Selection and UI state
    Private currentTileX As Integer = -1
    Private currentTileY As Integer = -1
    Private selectedTileImage As Bitmap = Nothing

    ' For multi-edit 
    Private isEditDragging As Boolean = False
    Private lastEditTileX As Integer = -1
    Private lastEditTileY As Integer = -1

    ' Track where the mouse position is
    Private contextMenuOpenLocation As Point = Point.Empty
    ' Variables to track paste preview state
    Private isPasteMode As Boolean = False
    Private pastePreviewX As Integer = -1
    Private pastePreviewY As Integer = -1
    Private pastePreviewImage As Bitmap = Nothing

    ' Menu items
    Private fileMenu As New ToolStripMenuItem("File")
    Private editMenu As New ToolStripMenuItem("Edit")
    Private viewMenu As New ToolStripMenuItem("View")
    Private renderMenu As New ToolStripMenuItem("Render")
    Private imageMenu As New ToolStripMenuItem("Image")
    Private helpMenu As New ToolStripMenuItem("Help")

    ' UI controls
    Private btnEditMode As New Button()
    Private statusStrip As New StatusStrip()
    Private lblToolStripMapName As New ToolStripStatusLabel()
    Private lblToolStripMapPath As New ToolStripStatusLabel()
    Private lblToolStripCoordinates As New ToolStripStatusLabel()
    ' Map menu
    Private mapContextMenu As ContextMenuStrip

    ' Undo/Redo support
    Private undoStack As New Stack(Of Command)
    Private redoStack As New Stack(Of Command)
    Private isUndoRedo As Boolean = False

    ' Tracks whether the loaded map has unsaved changes.
    Private mapIsDirty As Boolean = False

    ' Debounce timer for mini-map thumbnail refreshes. Edits during a drag
    ' fire ExecuteCommand many times a second; without throttling the mini-map
    ' would re-render the full map thumbnail on every step.
    Private miniMapRefreshTimer As Timer = Nothing

    ' Recent Files menu state. List is most-recent-first; persisted to INI.
    Private recentFiles As New List(Of String)
    Private mnuRecentFiles As ToolStripMenuItem = Nothing

    ' Highlight-enclosed-areas overlay state. When non-Nothing, MapPanel_Paint
    ' draws a semi-transparent magenta fill on each listed tile so the user
    ' can see which tiles "Find & Fix Enclosed Areas" would convert.
    Private _highlightedEnclosedTiles As List(Of Point) = Nothing
    Private mnuHighlightEnclosed As ToolStripMenuItem = Nothing

    ' Copy timer
    Private copyConfirmationTimer As Timer

#End Region

#Region "Properties"

    ' Expose necessary objects to other forms

    ''' <summary>
    ''' The currently loaded map, or Nothing when no map is open. Read by the tool palette forms.
    ''' </summary>
    Public ReadOnly Property CurrentLoadedMap As Map
        Get
            Return currentMap
        End Get
    End Property

    ''' <summary>
    ''' Resource manager for the configured Outpost 2 folder (tileset art etc.).
    ''' </summary>
    Public ReadOnly Property MapResourceManager As ResourceManager
        Get
            Return resourceManager
        End Get
    End Property

    ''' <summary>
    ''' Renderer for the currently loaded map, or Nothing when no map is open.
    ''' </summary>
    Public ReadOnly Property CurrentMapRenderer As MapRenderer
        Get
            Return mapRenderer
        End Get
    End Property
#End Region


#Region "Initialization and Setup"

    ''' <summary>
    ''' Form load - restores window position, runs first-launch setup if the OP2 path
    ''' isn't configured, builds the UI, and loads a map passed on the command line.
    ''' </summary>
    Private Sub fMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ToolPaletteHelper.RestoreMainFormPosition(Me)

        Debug.WriteLine("--- " & ApplicationName & " Started ---")
        'Me.Icon = My.Resources.lists
        Me.Icon = My.Resources.IconLarge
        RefreshWindowTitle()

        My.Settings.WorkingPath = My.Settings.OP2Path

        ' Tile-group export location. Default to <exe>\Tilegroups on first run
        ' (or if the user cleared it), and create the folder if it doesn't
        ' exist so the export code can write straight to it.
        If String.IsNullOrEmpty(My.Settings.Tilegroups) Then
            My.Settings.Tilegroups = Path.Combine(Application.StartupPath, "Tilegroups")
            My.Settings.Save()
        End If
        Try
            If Not Directory.Exists(My.Settings.Tilegroups) Then
                Directory.CreateDirectory(My.Settings.Tilegroups)
            End If
        Catch ex As Exception
            Debug.WriteLine($"Could not create Tilegroups folder at {My.Settings.Tilegroups}: {ex.Message}")
        End Try

        ' Check if OP2 path is set. On first launch fMain has just been shown
        ' but hasn't fully laid out — without hiding it the user sees the form
        ' with placeholder "lblMapName" etc. labels behind the welcome dialog.
        ' Opacity=0 keeps the form in the show sequence (avoids re-layout
        ' flicker) while making it visually invisible.
        If String.IsNullOrEmpty(My.Settings.OP2Path) OrElse Not Directory.Exists(My.Settings.OP2Path) Then
            Me.Opacity = 0
            Try
                MessageBox.Show("Welcome. First step is to browse for the Outpost 2 (1.4.1) folder. Click the browse button to locate the folder.", "Welcome", MessageBoxButtons.OK, MessageBoxIcon.Information)
                ShowSettingsDialog()
            Finally
                Me.Opacity = 1
            End Try
        End If

        ' Initialize UI
        InitializeUI()

        ' Populate the Recent Files submenu from the INI. Must come AFTER
        ' InitializeUI (which creates the menu structure).
        recentFiles = ToolPaletteHelper.LoadRecentFiles()
        RefreshRecentFilesMenu()

        ' Initialize Labels
        InitializeLabels()

        ' Add tileset info labels
        AddTilesetInfoLabels()

        ' Initialize state
        EditModeEnabled = False
        btnEditMode.Enabled = False

        picSelectedTool.Hide()

        Dim args = Environment.GetCommandLineArgs()
        If args.Length > 1 Then
            Dim filePath = args(1)
            'Debug.WriteLine("GetCommandLineArgs: " & filePath)

            ' Check if the file exists
            If File.Exists(filePath) Then
                ' Try to load the map file
                If LoadMapFile(filePath) Then
                    Debug.WriteLine(" - Map loaded successfully")

                Else
                    Debug.WriteLine(" - Failed to load map")

                End If
            Else
                Debug.WriteLine(" - File not found")
                'MessageBox.Show("Map file not found: " & filePath, "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning)

            End If
        End If

        'Dim player As New SoundPlayer(My.Resources.earthwrk)
        'player.Play()
    End Sub

#Region "Window Snapping (screen edges only)"

    ' Snap distance for fMain. Same value the tool palettes use so the feel
    ' is consistent across the app.
    Private Const MainFormSnapDistance As Integer = 20

    ' Windows messages — WM_MOVING fires during a title-bar drag, WM_SIZING
    ' during an edge/corner resize.
    Private Const WM_MOVING As Integer = &H216
    Private Const WM_SIZING As Integer = &H214

    ' WM_SIZING wParam values identify which edge or corner the user is
    ' dragging, so we only snap that edge (not all four).
    Private Const WMSZ_LEFT As Integer = 1
    Private Const WMSZ_RIGHT As Integer = 2
    Private Const WMSZ_TOP As Integer = 3
    Private Const WMSZ_TOPLEFT As Integer = 4
    Private Const WMSZ_TOPRIGHT As Integer = 5
    Private Const WMSZ_BOTTOM As Integer = 6
    Private Const WMSZ_BOTTOMLEFT As Integer = 7
    Private Const WMSZ_BOTTOMRIGHT As Integer = 8

    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure SNAP_RECT
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    ''' <summary>
    ''' Snaps fMain to screen edges while moving or resizing. Holding Ctrl
    ''' during the drag bypasses snapping for fine placement. Tool palettes
    ''' are deliberately NOT considered — the user only wants the main form
    ''' to snap to the monitor edges.
    ''' </summary>
    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_MOVING OrElse m.Msg = WM_SIZING Then
            ' Ctrl held → free move/resize, no snapping.
            If (Control.ModifierKeys And Keys.Control) <> Keys.Control Then
                Dim r As SNAP_RECT = Runtime.InteropServices.Marshal.PtrToStructure(Of SNAP_RECT)(m.LParam)

                If m.Msg = WM_MOVING Then
                    SnapMainFormForMove(r)
                Else
                    SnapMainFormForResize(r, m.WParam.ToInt32())
                End If

                Runtime.InteropServices.Marshal.StructureToPtr(r, m.LParam, True)
            End If
        End If

        MyBase.WndProc(m)
    End Sub

    ''' <summary>
    ''' Move-snap: translate the whole rect by a single (dx, dy) so any
    ''' edge near a screen edge clicks into place. All four screen edges
    ''' across every connected monitor are candidates.
    ''' </summary>
    Private Sub SnapMainFormForMove(ByRef r As SNAP_RECT)
        Dim dx As Integer = 0
        Dim dy As Integer = 0

        For Each screen As Screen In Screen.AllScreens
            Dim work As Rectangle = screen.WorkingArea
            If dx = 0 Then
                If Math.Abs(r.Left - work.Left) < MainFormSnapDistance Then dx = work.Left - r.Left
                If Math.Abs(r.Right - work.Right) < MainFormSnapDistance Then dx = work.Right - r.Right
            End If
            If dy = 0 Then
                If Math.Abs(r.Top - work.Top) < MainFormSnapDistance Then dy = work.Top - r.Top
                If Math.Abs(r.Bottom - work.Bottom) < MainFormSnapDistance Then dy = work.Bottom - r.Bottom
            End If
        Next

        r.Left += dx
        r.Right += dx
        r.Top += dy
        r.Bottom += dy
    End Sub

    ''' <summary>
    ''' Resize-snap: only the edge(s) being dragged snap to screen edges.
    ''' Lets the user drag, say, the right edge against the screen-right
    ''' to "dock" three sides flush with the screen while keeping the
    ''' left edge wherever it is.
    ''' </summary>
    Private Sub SnapMainFormForResize(ByRef r As SNAP_RECT, wparam As Integer)
        Dim leftActive As Boolean =
            wparam = WMSZ_LEFT OrElse wparam = WMSZ_TOPLEFT OrElse wparam = WMSZ_BOTTOMLEFT
        Dim rightActive As Boolean =
            wparam = WMSZ_RIGHT OrElse wparam = WMSZ_TOPRIGHT OrElse wparam = WMSZ_BOTTOMRIGHT
        Dim topActive As Boolean =
            wparam = WMSZ_TOP OrElse wparam = WMSZ_TOPLEFT OrElse wparam = WMSZ_TOPRIGHT
        Dim bottomActive As Boolean =
            wparam = WMSZ_BOTTOM OrElse wparam = WMSZ_BOTTOMLEFT OrElse wparam = WMSZ_BOTTOMRIGHT

        For Each screen As Screen In Screen.AllScreens
            Dim work As Rectangle = screen.WorkingArea
            If leftActive AndAlso Math.Abs(r.Left - work.Left) < MainFormSnapDistance Then
                r.Left = work.Left
            End If
            If rightActive AndAlso Math.Abs(r.Right - work.Right) < MainFormSnapDistance Then
                r.Right = work.Right
            End If
            If topActive AndAlso Math.Abs(r.Top - work.Top) < MainFormSnapDistance Then
                r.Top = work.Top
            End If
            If bottomActive AndAlso Math.Abs(r.Bottom - work.Bottom) < MainFormSnapDistance Then
                r.Bottom = work.Bottom
            End If
        Next
    End Sub

#End Region

    ''' <summary>
    ''' Handles FormClosing - fallback save of the window position (primary save is in fMain_Closing).
    ''' </summary>
    Private Sub fMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        ' Kept as a fallback save in case some path fires FormClosing but not
        ' Closing. Closing is where the primary save happens (see fMain_Closing).
        Debug.WriteLine("fMain_FormClosing")
        If e.Cancel Then Return
        ToolPaletteHelper.SaveMainFormPosition(Me)
    End Sub

    ''' <summary>
    ''' Handles Closing - prompts about unsaved changes (cancelling the close if asked)
    ''' and saves the window position. This is the canonical shutdown hook.
    ''' </summary>
    Private Sub fMain_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        Debug.WriteLine("fMain_Closing")

        ' Closing fires before FormClosing — do the unsaved-changes check here
        ' so cancellation propagates correctly to the rest of shutdown.
        If Not ConfirmDiscardUnsavedChanges() Then
            e.Cancel = True
            Return
        End If

        ' Save fMain's window position+size+state here as well as in FormClosing.
        ' Earlier debugging showed FormClosing doesn't always fire on every
        ' system — Closing has proven reliable (the unsaved-changes prompt
        ' demonstrates it), so this is the canonical save location.
        ToolPaletteHelper.SaveMainFormPosition(Me)
    End Sub

    ''' <summary>
    ''' Shows the settings dialog and, if accepted, (re)creates the resource manager
    ''' for the chosen Outpost 2 folder.
    ''' </summary>
    Private Sub ShowSettingsDialog()
        Dim settings As New fSettings()
        If settings.ShowDialog() = DialogResult.OK Then
            Try
                ' Initialize resource manager with the selected path
                resourceManager = New ResourceManager(My.Settings.OP2Path)
            Catch ex As Exception
                MessageBox.Show("Error initializing resource manager: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    ''' <summary>
    ''' Resets all property-panel and status-bar labels to their N/A placeholder text.
    ''' </summary>
    Private Sub InitializeLabels()
        ' Initialize label text with placeholder values
        lblMapName.Text = "Map Name: N/A"
        lblMapSize.Text = "Map Size: N/A"
        lblMapImage.Text = "Map Image: N/A"
        lblTileCount.Text = "Tile Count: N/A"
        lblTileset.Text = "Tileset: N/A"
        lblTilesetImage.Text = "Image Index: N/A"
        lblMappingIndex.Text = "Mapping Index: N/A"
        lblCoordinates.Text = "Coordinates: N/A"
        lblCellType.Text = "Cell Type: N/A"
        lblZoomLevel.Text = "Zoom Level: N/A"

        ' Initialize tool and placement labels
        lblCurrentTool.Text = "No Tool Selected"
        lblCurrentPlacement.Text = "No Placement Selected"
        lblCurrentTool.ForeColor = System.Drawing.SystemColors.GrayText
        lblCurrentPlacement.ForeColor = System.Drawing.SystemColors.GrayText

        ' Initialize status bar
        lblToolStripMapName.Text = ""
        lblToolStripMapPath.Text = "N/A"
        lblToolStripCoordinates.Text = "0,0"
    End Sub

    ''' <summary>
    ''' Builds the runtime UI: menus, context menu, status bar, map panel event hookups,
    ''' double buffering and the copy-confirmation timer.
    ''' </summary>
    Private Sub InitializeUI()
        ' Set up menu items
        SetupMenus()

        ' Initialize the map context menu
        InitializeMapContextMenu()

        ' Set up status bar
        SetupStatusBar()

        ' Enable double buffering for map panel
        pnlMap.GetType().GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(pnlMap, True)

        ' Set up map panel event handlers
        AddHandler pnlMap.Paint, AddressOf MapPanel_Paint
        AddHandler pnlMap.MouseMove, AddressOf MapPanel_MouseMove
        AddHandler pnlMap.MouseDown, AddressOf MapPanel_MouseDown
        AddHandler pnlMap.MouseUp, AddressOf MapPanel_MouseUp
        AddHandler pnlMap.MouseWheel, AddressOf MapPanel_MouseWheel

        ' Configure panel properties
        pnlMap.Dock = DockStyle.Fill
        pnlMap.AutoScroll = True
        pnlMap.BackColor = System.Drawing.Color.DarkGray

        ' Just in case
        picSelectedTool.BorderStyle = BorderStyle.FixedSingle
        picSelectedTool.SizeMode = PictureBoxSizeMode.Normal
        picSelectedTool.BackColor = System.Drawing.Color.Black

        ' Configure properties panel
        pnlProperties.Dock = DockStyle.Right

        ' Create and configure the copy timer
        copyConfirmationTimer = New Timer()
        copyConfirmationTimer.Interval = 2000 ' 2 seconds
        copyConfirmationTimer.Enabled = False

        ' Add the elapsed event handler
        AddHandler copyConfirmationTimer.Tick, AddressOf CopyConfirmationTimer_Tick
    End Sub

    ''' <summary>
    ''' Creates the main menu strip (File, Edit, View, Render, Image, Help) and wires
    ''' every menu item to its handler. Map-dependent items start disabled.
    ''' </summary>
    Private Sub SetupMenus()
        ' Create main menu strip
        Dim menuStrip As New MenuStrip()

        '=============================================
        ' FILE MENU
        '=============================================
        fileMenu.Name = "mnuFile"
        fileMenu.Text = "File"

        ' Create New Map menu item
        Dim newMapMenuItem As New ToolStripMenuItem("New Map") With {
            .Name = "mnuNewMap",
            .ShortcutKeys = Keys.Control Or Keys.N
        }
        AddHandler newMapMenuItem.Click, AddressOf NewMap_Click
        fileMenu.DropDownItems.Add(newMapMenuItem)

        ' Create Open Map menu item
        Dim openMapMenuItem As New ToolStripMenuItem("Open Map") With {
            .Name = "mnuOpenMap",
            .ShortcutKeys = Keys.Control Or Keys.O
        }
        AddHandler openMapMenuItem.Click, AddressOf OpenMap_Click
        fileMenu.DropDownItems.Add(openMapMenuItem)

        ' Recent Files submenu — populated from the INI on form load and
        ' refreshed each time a map is opened.
        mnuRecentFiles = New ToolStripMenuItem("Recent Files") With {
            .Name = "mnuRecentFiles"
        }
        fileMenu.DropDownItems.Add(mnuRecentFiles)

        ' Create Save Map menu item (disabled until a map is loaded)
        Dim saveMapMenuItem As New ToolStripMenuItem("Save Map") With {
            .Name = "mnuSaveMap",
            .ShortcutKeys = Keys.Control Or Keys.S,
            .Enabled = False
        }
        AddHandler saveMapMenuItem.Click, AddressOf SaveMap_Click
        fileMenu.DropDownItems.Add(saveMapMenuItem)

        ' Add remaining file menu items (Close Map disabled until a map is loaded)
        Dim closeMapMenuItem As ToolStripMenuItem = fileMenu.DropDownItems.Add("Close Map", Nothing, AddressOf CloseMap_Click)
        closeMapMenuItem.Name = "mnuCloseMap"
        closeMapMenuItem.Enabled = False
        fileMenu.DropDownItems.Add("Settings", Nothing, AddressOf Settings_Click).Name = "mnuSettings"
        fileMenu.DropDownItems.Add("-") ' Separator
        fileMenu.DropDownItems.Add("Launch Outpost 2", Nothing, AddressOf LaunchOutpost2_Click).Name = "mnuLaunchOutpost2"
        fileMenu.DropDownItems.Add("Launch OP2 into DLL Picker", Nothing, AddressOf LaunchDllPicker_Click).Name = "mnuLaunchDllPicker"
        fileMenu.DropDownItems.Add("Open Outpost 2 Folder", Nothing, AddressOf OpenOutpost2Folder_Click).Name = "mnuOpenOutpost2Folder"
        fileMenu.DropDownItems.Add("-") ' Separator
        fileMenu.DropDownItems.Add("Exit", Nothing, AddressOf Exit_Click).Name = "mnuExit"

        '=============================================
        ' EDIT MENU
        '=============================================
        editMenu.Name = "mnuEdit"
        editMenu.Text = "Edit"

        ' Create undo/redo menu items
        Dim undoMenuItem As New ToolStripMenuItem("Undo") With {
        .Name = "mnuUndo",
        .ShortcutKeys = Keys.Control Or Keys.Z,
        .Enabled = False
        }
        AddHandler undoMenuItem.Click, AddressOf Undo_Click
        Dim redoMenuItem As New ToolStripMenuItem("Redo") With {
        .Name = "mnuRedo",
        .ShortcutKeys = Keys.Control Or Keys.Y,
        .Enabled = False
        }
        AddHandler redoMenuItem.Click, AddressOf Redo_Click

        ' Add undo/redo to edit menu
        editMenu.DropDownItems.Add(undoMenuItem)
        editMenu.DropDownItems.Add(redoMenuItem)

        editMenu.DropDownItems.Add(New ToolStripSeparator())

        ' Create copy menu item
        Dim copyMenuItem As New ToolStripMenuItem("Copy Tiles") With {
        .Name = "mnuCopy",
        .ShortcutKeys = Keys.Control Or Keys.C,
        .Enabled = False
        }
        AddHandler copyMenuItem.Click, AddressOf Copy_Click
        editMenu.DropDownItems.Add(copyMenuItem)

        ' Create paste menu item
        Dim pasteMenuItem As New ToolStripMenuItem("Paste Tiles") With {
        .Name = "mnuPaste",
        .ShortcutKeys = Keys.Control Or Keys.V,
        .Enabled = False
        }
        AddHandler pasteMenuItem.Click, AddressOf Paste_Click
        editMenu.DropDownItems.Add(pasteMenuItem)

        editMenu.DropDownItems.Add(New ToolStripSeparator())

        Dim singleTileModeMenuItem As New ToolStripMenuItem("Single Tile Edit Mode") With {
        .Name = "mnuSingleTileMode",
        .CheckOnClick = True,
        .Checked = GlobalVariables.SingleTileEditMode
        }
        AddHandler singleTileModeMenuItem.Click, AddressOf ToggleSingleTileMode_Click
        editMenu.DropDownItems.Add(singleTileModeMenuItem)

        editMenu.DropDownItems.Add(New ToolStripSeparator())

        ' Map-analysis tool — finds passable regions unreachable from the main
        ' play area (sealed off by cliffs/walls) and offers to convert them to
        ' Impassable 1 so they show as red in-game.
        Dim findEnclosedMenuItem As New ToolStripMenuItem("Find && Fix Enclosed Areas...") With {
            .Name = "mnuFindEnclosed"
        }
        AddHandler findEnclosedMenuItem.Click, AddressOf FindEnclosedAreas_Click
        editMenu.DropDownItems.Add(findEnclosedMenuItem)

        ' Toggle a magenta highlight overlay on every tile the "Find & Fix"
        ' tool would convert — for visually reviewing before committing.
        mnuHighlightEnclosed = New ToolStripMenuItem("Highlight Enclosed Areas") With {
            .Name = "mnuHighlightEnclosed",
            .CheckOnClick = True
        }
        AddHandler mnuHighlightEnclosed.CheckedChanged, AddressOf HighlightEnclosedAreas_CheckedChanged
        editMenu.DropDownItems.Add(mnuHighlightEnclosed)

        '=============================================
        ' VIEW MENU
        '=============================================
        viewMenu.Name = "mnuView"
        viewMenu.Text = "View"

        ' Create view-related menu items
        Dim gridMenuItem As New ToolStripMenuItem("Show Grid") With {
        .Name = "mnuGrid",
        .ShortcutKeys = Keys.Control Or Keys.G,
        .CheckOnClick = True
        }
        AddHandler gridMenuItem.Click, AddressOf ToggleGrid_Click

        Dim largeGridMenuItem As New ToolStripMenuItem("Show Large Grid") With {
        .Name = "mnuLargeGrid",
        .ShortcutKeys = Keys.Control Or Keys.L,
        .CheckOnClick = True
        }
        AddHandler largeGridMenuItem.Click, AddressOf ToggleLargeGrid_Click

        Dim showCoordsMenuItem As New ToolStripMenuItem("Show Coordinates") With {
        .Name = "mnuShowCoords",
        .ShortcutKeys = Keys.Control Or Keys.D,
        .CheckOnClick = True
        }
        AddHandler showCoordsMenuItem.Click, AddressOf ToggleCoordinates_Click

        Dim enableZoomMenuItem As New ToolStripMenuItem("Enable Zoom") With {
        .Name = "mnuEnableZoom",
        .ShortcutKeys = Keys.Control Or Keys.E,  ' Changed to avoid conflict with Undo
        .CheckOnClick = True,
        .Checked = True  ' Default is enabled
        }
        AddHandler enableZoomMenuItem.Click, AddressOf ToggleZoomEnabled_Click

        Dim resetZoomMenuItem As New ToolStripMenuItem("Reset Zoom") With {
        .Name = "mnuResetZoom",
        .ShortcutKeys = Keys.Control Or Keys.R
        }
        AddHandler resetZoomMenuItem.Click, AddressOf ResetZoom_Click


        ' Add view menu items
        viewMenu.DropDownItems.Add(gridMenuItem)
        viewMenu.DropDownItems.Add(largeGridMenuItem)
        viewMenu.DropDownItems.Add(showCoordsMenuItem)
        viewMenu.DropDownItems.Add(enableZoomMenuItem)
        viewMenu.DropDownItems.Add(resetZoomMenuItem)
        viewMenu.DropDownItems.Add(New ToolStripSeparator())

        ' Toggle for keeping the 5 tool palettes (TileSet, CellTypes,
        ' TileSelect, TileGroups, MiniMap) above other windows. Persists in
        ' Mapper3.ini and applies to currently-open palettes immediately.
        Dim toolsTopMostMenuItem As New ToolStripMenuItem("Tool Windows Always On Top") With {
            .Name = "mnuToolsTopMost",
            .CheckOnClick = True,
            .Checked = ToolPaletteHelper.GetToolWindowsAlwaysOnTop()
        }
        AddHandler toolsTopMostMenuItem.CheckedChanged, AddressOf ToolsTopMost_CheckedChanged
        viewMenu.DropDownItems.Add(toolsTopMostMenuItem)
        viewMenu.DropDownItems.Add(New ToolStripSeparator())

        ' Configuration options
        Dim configGridSize As New ToolStripMenuItem("Configure Grid Size...") With {
        .Name = "mnuConfigGridSize"
        }
        AddHandler configGridSize.Click, AddressOf ConfigureGridSize_Click
        viewMenu.DropDownItems.Add(configGridSize)

        '=============================================
        ' RENDER MENU
        '=============================================
        renderMenu.Name = "mnuRender"
        renderMenu.Text = "Render"

        ' Add render mode menu items
        renderMenu.DropDownItems.Add("GameMap", Nothing, AddressOf RenderModeGameMap_Click).Name = "mnuRenderGameMap"
        renderMenu.DropDownItems.Add("CellTypeOverlay", Nothing, AddressOf RenderModeCellTypeOverlay_Click).Name = "mnuRenderCellTypeOverlay"
        renderMenu.DropDownItems.Add("CellTypeOverlayAll", Nothing, AddressOf RenderModeCellTypeOverlayAll_Click).Name = "mnuRenderCellTypeOverlayAll"
        renderMenu.DropDownItems.Add("CellTypeBasic", Nothing, AddressOf RenderModeCellTypeBasic_Click).Name = "mnuRenderCellTypeBasic"
        renderMenu.DropDownItems.Add("CellTypeAll", Nothing, AddressOf RenderModeCellTypeAll_Click).Name = "mnuRenderCellTypeAll"

        ' Disabled until a map is loaded
        renderMenu.Enabled = False

        '=============================================
        ' IMAGE MENU
        '=============================================
        imageMenu.Name = "mnuImage"
        imageMenu.Text = "Image"

        ' Create Export Image submenu
        Dim exportImageMenu As New ToolStripMenuItem("Export Image") With {
        .Name = "mnuExportImage"
        }

        ' Add export size options
        exportImageMenu.DropDownItems.Add("Full Size", Nothing, AddressOf ExportImageFullSize_Click).Name = "mnuExportFullSize"
        exportImageMenu.DropDownItems.Add("1/8th Size", Nothing, AddressOf ExportImageEighthSize_Click).Name = "mnuExportEighthSize"
        exportImageMenu.DropDownItems.Add("1/16th Size", Nothing, AddressOf ExportImageSixteenthSize_Click).Name = "mnuExportSixteenthSize"
        exportImageMenu.DropDownItems.Add("Full Size (Showing Grid)", Nothing, AddressOf ExportImageFullSizeWithGrid_Click).Name = "mnuExportFullSizeWithGrid"
        exportImageMenu.DropDownItems.Add("Full Size (Showing Large Grid)", Nothing, AddressOf ExportImageFullSizeWithLargeGrid_Click).Name = "mnuExportFullSizeWithLargeGrid"

        ' Screenshot — capture exactly what the user sees in the map panel.
        ' F12 hotkey makes it quick to grab snapshots for forum posts etc.
        Dim screenshotMenuItem As New ToolStripMenuItem("Screenshot") With {
            .Name = "mnuScreenshot",
            .ShortcutKeys = Keys.F12
        }
        AddHandler screenshotMenuItem.Click, AddressOf Screenshot_Click
        imageMenu.DropDownItems.Add(screenshotMenuItem)
        imageMenu.DropDownItems.Add("-") ' Separator

        ' Add Export menu items
        imageMenu.DropDownItems.Add(exportImageMenu)
        imageMenu.DropDownItems.Add("-") ' Separator
        imageMenu.DropDownItems.Add("Export Overview", Nothing, AddressOf ExportImageMenu_Click).Name = "mnuExportOverview"

        ' Disabled until a map is loaded
        imageMenu.Enabled = False

        '=============================================
        ' HELP MENU
        '=============================================
        helpMenu.Name = "mnuHelp"
        helpMenu.Text = "Help"

        ' Add help menu items
        helpMenu.DropDownItems.Add("About", Nothing, AddressOf About_Click).Name = "mnuAbout"

        '=============================================
        ' ADD MENUS TO MENUSTRIP
        '=============================================
        menuStrip.Items.Add(fileMenu)
        menuStrip.Items.Add(editMenu)
        menuStrip.Items.Add(viewMenu)
        menuStrip.Items.Add(renderMenu)
        menuStrip.Items.Add(imageMenu)
        menuStrip.Items.Add(helpMenu)

        ' Set as main menu and add to controls
        MainMenuStrip = menuStrip
        Controls.Add(menuStrip)
    End Sub



    ''' <summary>
    ''' Creates the status bar with map name, map path and cursor coordinate labels.
    ''' </summary>
    Private Sub SetupStatusBar()
        lblToolStripMapName.Name = "lblToolStripMapName"
        lblToolStripMapName.Text = ""

        lblToolStripMapPath.Name = "lblToolStripMapPath"
        lblToolStripMapPath.Text = "N/A"

        lblToolStripCoordinates.Name = "lblCoordinates"
        lblToolStripCoordinates.Text = "0,0"

        statusStrip.Items.Add(lblToolStripMapName)
        statusStrip.Items.Add(lblToolStripMapPath)
        statusStrip.Items.Add(lblToolStripCoordinates)

        Controls.Add(statusStrip)
    End Sub

    ''' <summary>
    ''' Builds the right-click context menu for the map panel (tile select plus
    ''' grid/coordinate/zoom toggles) and assigns it to pnlMap.
    ''' </summary>
    Private Sub InitializeMapContextMenu()
        ' Create the context menu
        mapContextMenu = New ContextMenuStrip()

        ' Add "Select Tile Under Cursor" as the first option
        Dim mnuSelectTileUnderCursor As New ToolStripMenuItem("Select Tile Under Cursor") With {
        .Name = "mnuSelectTileUnderCursor"
    }
        AddHandler mnuSelectTileUnderCursor.Click, AddressOf SelectTileUnderCursor_Click

        ' Create menu items with checkable state
        Dim mnuShowGrid As New ToolStripMenuItem("Enable Grid") With {
        .Name = "mnuContextGrid",
        .CheckOnClick = True,
        .Checked = GlobalVariables.ShowGrid
    }
        AddHandler mnuShowGrid.Click, AddressOf ContextShowGrid_Click

        Dim mnuShowLargeGrid As New ToolStripMenuItem("Show Large Grid") With {
        .Name = "mnuContextLargeGrid",
        .CheckOnClick = True,
        .Checked = GlobalVariables.ShowLargeGrid
    }
        AddHandler mnuShowLargeGrid.Click, AddressOf ContextShowLargeGrid_Click

        Dim mnuShowCoords As New ToolStripMenuItem("Show Coordinates") With {
        .Name = "mnuContextCoords",
        .CheckOnClick = True,
        .Checked = GlobalVariables.ShowCoordinates
    }
        AddHandler mnuShowCoords.Click, AddressOf ContextShowCoords_Click

        ' Add zoom toggle item
        Dim mnuEnableZoom As New ToolStripMenuItem("Enable Zoom") With {
        .Name = "mnuContextEnableZoom",
        .CheckOnClick = True,
        .Checked = GlobalVariables.ZoomEnabled
    }
        AddHandler mnuEnableZoom.Click, AddressOf ContextEnableZoom_Click

        ' Add all items to the context menu in the requested order
        mapContextMenu.Items.Add(mnuSelectTileUnderCursor)
        mapContextMenu.Items.Add(mnuShowGrid)
        mapContextMenu.Items.Add(mnuShowLargeGrid)
        mapContextMenu.Items.Add(mnuShowCoords)
        mapContextMenu.Items.Add(mnuEnableZoom)

        ' Assign to map panel
        pnlMap.ContextMenuStrip = mapContextMenu

        ' Add opening event to update checked states
        AddHandler mapContextMenu.Opening, AddressOf MapContextMenu_Opening
    End Sub

    ''' <summary>
    ''' Handles the context menu Enable Zoom toggle - delegates to ToggleZoomEnabled.
    ''' </summary>
    Private Sub ContextEnableZoom_Click(sender As Object, e As EventArgs)
        ToggleZoomEnabled()
    End Sub



    ''' <summary>
    ''' Handles the context menu Opening event - cancels it mid-drag or with no map
    ''' loaded, records the open location, and syncs the checked states.
    ''' </summary>
    Private Sub MapContextMenu_Opening(sender As Object, e As System.ComponentModel.CancelEventArgs)
        ' If we're in the middle of a drag operation, cancel the menu
        If isDragging OrElse isRightDragging Then
            e.Cancel = True
            Return
        End If

        ' Only enable the context menu if a map is loaded
        If currentMap Is Nothing Then
            e.Cancel = True
            Return
        End If

        ' Store the current mouse position relative to map panel when opening context menu
        contextMenuOpenLocation = pnlMap.PointToClient(Control.MousePosition)
        'Debug.WriteLine($"Context menu opened at: {contextMenuOpenLocation}")

        ' Update checked states to match current settings
        Dim mnuGrid As ToolStripMenuItem = DirectCast(mapContextMenu.Items("mnuContextGrid"), ToolStripMenuItem)
        Dim mnuLargeGrid As ToolStripMenuItem = DirectCast(mapContextMenu.Items("mnuContextLargeGrid"), ToolStripMenuItem)
        Dim mnuCoords As ToolStripMenuItem = DirectCast(mapContextMenu.Items("mnuContextCoords"), ToolStripMenuItem)
        Dim mnuZoom As ToolStripMenuItem = DirectCast(mapContextMenu.Items("mnuContextEnableZoom"), ToolStripMenuItem)

        ' Set checked states based on global variables
        mnuGrid.Checked = GlobalVariables.ShowGrid
        mnuLargeGrid.Checked = GlobalVariables.ShowLargeGrid
        mnuCoords.Checked = GlobalVariables.ShowCoordinates
        mnuZoom.Checked = GlobalVariables.ZoomEnabled
    End Sub

    ''' <summary>
    ''' Handles the context menu Enable Grid toggle - delegates to ToggleGrid.
    ''' </summary>
    Private Sub ContextShowGrid_Click(sender As Object, e As EventArgs)
        ToggleGrid()
    End Sub

    ''' <summary>
    ''' Handles the context menu Show Large Grid toggle - delegates to ToggleLargeGrid.
    ''' </summary>
    Private Sub ContextShowLargeGrid_Click(sender As Object, e As EventArgs)
        ToggleLargeGrid()
    End Sub

    ''' <summary>
    ''' Handles the context menu Show Coordinates toggle - delegates to ToggleCoordinates.
    ''' </summary>
    Private Sub ContextShowCoords_Click(sender As Object, e As EventArgs)
        ToggleCoordinates()
    End Sub

    ''' <summary>
    ''' Adds the runtime-created controls to the properties panel: tile preview boxes,
    ''' Reset Zoom button, and the Edit Mode / Clear Tool buttons.
    ''' </summary>
    Private Sub AddTilesetInfoLabels()
        ' Define starting Y position
        Dim yPos As Integer = 240

        ' Add tile preview label
        Dim lblTilePreview As New Label With {
        .Text = "Tile Preview:",
        .AutoSize = True,
        .Location = New Point(10, yPos)
    }
        pnlProperties.Controls.Add(lblTilePreview)

        ' Configure the tile preview picture boxes
        picTilePreview.Name = "picTilePreview"
        picTilePreview.Size = New Size(32, 32)
        picTilePreview.Location = New Point(20, yPos + 20)
        picTilePreview.BorderStyle = BorderStyle.FixedSingle
        picTilePreview.SizeMode = PictureBoxSizeMode.Normal
        picTilePreview.BackColor = System.Drawing.Color.Black
        'pnlProperties.Controls.Add(picTilePreview)

        picTilePreviewBig.Name = "picTilePreviewBig"
        picTilePreviewBig.Size = New Size(96, 96)
        picTilePreviewBig.Location = New Point(70, yPos + 20)
        picTilePreviewBig.BorderStyle = BorderStyle.FixedSingle
        picTilePreviewBig.SizeMode = PictureBoxSizeMode.StretchImage
        picTilePreviewBig.BackColor = System.Drawing.Color.Black
        'pnlProperties.Controls.Add(picTilePreview)

        picSelectedTool.Name = "picSelectedTool"
        picSelectedTool.Size = New Size(32, 32)
        'picTilePreviewBig.Location = New Point(70, yPos + 20)
        picSelectedTool.BorderStyle = BorderStyle.FixedSingle
        picSelectedTool.SizeMode = PictureBoxSizeMode.StretchImage
        picSelectedTool.BackColor = System.Drawing.Color.Black

        ' Add Reset Zoom button
        Dim btnResetZoom As New Button With {
        .Name = "btnResetZoom",
        .Text = "Reset Zoom",
        .Size = New Size(80, 23),
        .Location = New Point(lblZoomLevel.Location.X + 120, lblZoomLevel.Location.Y - 3)
    }
        AddHandler btnResetZoom.Click, AddressOf ResetZoom_Click
        pnlProperties.Controls.Add(btnResetZoom)

        ' EDITING SECTION
        Dim lblEditingSeparator As New Label With {
        .Text = "------- Editing Tools -------",
        .AutoSize = False,
        .Width = 220,
        .Height = 20,
        .Location = New Point(10, yPos + 130),
        .TextAlign = ContentAlignment.MiddleCenter
    }
        pnlProperties.Controls.Add(lblEditingSeparator)

        ' Add Edit Mode button
        btnEditMode.Name = "btnEditMode"
        btnEditMode.Text = "Enable Edit Mode"
        btnEditMode.Size = New Size(150, 28)
        btnEditMode.Location = New Point(50, yPos + 160)
        AddHandler btnEditMode.Click, AddressOf EditMode_Click
        pnlProperties.Controls.Add(btnEditMode)

        ' Add Clear Tool button
        Dim btnClearTool As New Button With {
        .Name = "btnClearTool",
        .Text = "Clear Tool Selection",
        .Size = New Size(150, 28),
        .Location = New Point(50, yPos + 195) ' Position below btnEditMode
    }
        AddHandler btnClearTool.Click, AddressOf ClearTool_Click
        pnlProperties.Controls.Add(btnClearTool)
    End Sub

#End Region
























#Region "Cleanup and Resource Management"

    ''' <summary>
    ''' Handles cleanup operations when the form is closing
    ''' </summary>
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        MyBase.OnFormClosing(e)

        ' The Closing handler (unsaved-changes prompt) may have cancelled the
        ' close — clean up only when we are actually shutting down.
        If e.Cancel Then Return

        Try
            ' Clean up map resources using the existing method
            CleanupMapResources()

            ' Dispose resource manager if it exists
            If resourceManager IsNot Nothing Then
                resourceManager.Dispose()
                resourceManager = Nothing
            End If

            Debug.WriteLine("--- Application Closed ---")

        Catch ex As Exception
            Debug.WriteLine($"Error during resource cleanup: {ex.Message}")
        End Try
    End Sub

#End Region








End Class


