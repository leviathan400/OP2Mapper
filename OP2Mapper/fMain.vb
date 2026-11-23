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
    ' Set on right-button-up after a pan so the context menu WinForms opens
    ' next (WM_CONTEXTMENU) is cancelled. Consumed by MapContextMenu_Opening.
    Private suppressNextContextMenu As Boolean = False

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

        ' Command line: [/op2dir <Outpost 2 folder>] [map file]
        Dim commandLineOp2Dir As String = Nothing
        Dim commandLineMapFile As String = Nothing
        ParseCommandLine(commandLineOp2Dir, commandLineMapFile)

        ' An Outpost 2 folder given on the command line replaces the saved one
        ' (and is saved), so startup skips the first-run folder prompt.
        If commandLineOp2Dir IsNot Nothing Then
            If Directory.Exists(commandLineOp2Dir) Then
                AppSettings.OP2Path = commandLineOp2Dir
                Debug.WriteLine("Outpost 2 folder from command line: " & commandLineOp2Dir)
            Else
                MessageBox.Show("The Outpost 2 folder given on the command line doesn't exist:" &
                                Environment.NewLine & Environment.NewLine & commandLineOp2Dir,
                                "Outpost 2 Folder Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        End If

        ' Default folder for opening and saving maps: the Outpost 2 folder until
        ' the user picks another in Settings. (This used to be reset to the
        ' Outpost 2 folder on every start, so a chosen folder never stuck.)
        If String.IsNullOrEmpty(AppSettings.WorkingPath) Then AppSettings.WorkingPath = AppSettings.OP2Path

        ' Tile-group export location. Default to <exe>\Tilegroups on first run
        ' (or if the user cleared it), and create the folder if it doesn't
        ' exist so the export code can write straight to it.
        If String.IsNullOrEmpty(AppSettings.Tilegroups) Then
            AppSettings.Tilegroups = Path.Combine(Application.StartupPath, "Tilegroups")
        End If
        Try
            If Not Directory.Exists(AppSettings.Tilegroups) Then
                Directory.CreateDirectory(AppSettings.Tilegroups)
            End If
        Catch ex As Exception
            Debug.WriteLine($"Could not create Tilegroups folder at {AppSettings.Tilegroups}: {ex.Message}")
        End Try

        ' Check if OP2 path is set. On first launch fMain has just been shown
        ' but hasn't fully laid out — without hiding it the user sees the form
        ' with placeholder "lblMapName" etc. labels behind the welcome dialog.
        ' Opacity=0 keeps the form in the show sequence (avoids re-layout
        ' flicker) while making it visually invisible.
        If String.IsNullOrEmpty(AppSettings.OP2Path) OrElse Not Directory.Exists(AppSettings.OP2Path) Then
            Me.Opacity = 0
            Try
                MessageBox.Show("Welcome. First step is to browse for the Outpost 2 (1.4.1) folder. Click the browse button to locate the folder.", "Welcome", MessageBoxButtons.OK, MessageBoxIcon.Information)
                ShowSettingsDialog()
            Finally
                Me.Opacity = 1
            End Try

            ' The mapper can't load tilesets without the Outpost 2 folder, so
            ' if Settings was cancelled without one, explain and quit.
            If String.IsNullOrEmpty(AppSettings.OP2Path) OrElse Not Directory.Exists(AppSettings.OP2Path) Then
                Me.Opacity = 0
                MessageBox.Show("No Outpost 2 folder is set, so " & ApplicationName & " will now close." &
                                Environment.NewLine & Environment.NewLine &
                                "Run it again and choose the Outpost 2 (1.4.1) folder in Settings, " &
                                "or start it with /op2dir ""<folder>"".",
                                "No Outpost 2 Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Application.Exit()
                Return
            End If
        End If

        ' Initialize UI
        InitializeUI()

        ' Populate the Recent Files submenu from the INI. Must come AFTER
        ' InitializeUI (which creates the menu structure).
        recentFiles = ToolPaletteHelper.LoadRecentFiles()
        RefreshRecentFilesMenu()

        ' Initialize Labels
        InitializeLabels()

        ' Lay out the properties sidebar (sections, previews, buttons)
        BuildSidebar()

        ' Initialize state
        EditModeEnabled = False
        btnEditMode.Enabled = False

        picSelectedTool.Hide()

        If commandLineMapFile IsNot Nothing Then
            Dim filePath = commandLineMapFile

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

    ''' <summary>
    ''' Another launch of OP2Mapper while this one is running (see
    ''' ApplicationEvents.vb): restore and activate this window, and open the
    ''' map from that launch's command line if it gave one, asking first about
    ''' unsaved changes. /op2dir is ignored here, since the folder is already set.
    ''' </summary>
    Friend Sub HandleNextInstance(commandLine As IList(Of String))
        If WindowState = FormWindowState.Minimized Then WindowState = FormWindowState.Normal
        Activate()

        Dim mapFile As String = Nothing
        Dim i As Integer = 0
        While commandLine IsNot Nothing AndAlso i < commandLine.Count
            Dim arg As String = commandLine(i)
            Dim name As String = arg.TrimStart("/"c, "-"c)
            If arg.Length > name.Length AndAlso name.StartsWith("op2dir", StringComparison.OrdinalIgnoreCase) Then
                If name.Length = "op2dir".Length Then i += 1     ' "/op2dir path": skip the path too
            ElseIf mapFile Is Nothing Then
                mapFile = arg
            End If
            i += 1
        End While

        If String.IsNullOrEmpty(mapFile) Then Return
        If Not File.Exists(mapFile) Then
            MessageBox.Show("Map file not found:" & vbCrLf & mapFile, "Open Map",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If ConfirmDiscardUnsavedChanges() Then LoadMapFile(mapFile)
    End Sub

    ''' <summary>
    ''' Reads the command line. Supported forms, in any order:
    '''   /op2dir "C:\Games\Outpost 2"     (also -op2dir, --op2dir, /op2dir:path, --op2dir=path)
    '''   "C:\Maps\mymap.map"              (the first plain argument is a map to open)
    ''' Returns Nothing for anything not given.
    ''' </summary>
    Private Shared Sub ParseCommandLine(ByRef op2Dir As String, ByRef mapFile As String)
        op2Dir = Nothing
        mapFile = Nothing

        Dim args() As String = Environment.GetCommandLineArgs()
        Dim i As Integer = 1   ' args(0) is the exe
        While i < args.Length
            Dim arg As String = args(i)
            Dim name As String = arg.TrimStart("/"c, "-"c)

            If arg.Length > name.Length AndAlso
               name.StartsWith("op2dir", StringComparison.OrdinalIgnoreCase) Then
                Dim rest As String = name.Substring("op2dir".Length)
                If rest.StartsWith(":") OrElse rest.StartsWith("=") Then
                    ' /op2dir:path or --op2dir=path
                    op2Dir = rest.Substring(1)
                ElseIf rest.Length = 0 AndAlso i + 1 < args.Length Then
                    ' /op2dir path
                    i += 1
                    op2Dir = args(i)
                End If

                ' A quoted path ending in a backslash ("C:\Outpost 2\") reaches
                ' us with a stray quote, because Windows reads \" as an escaped
                ' quote. Strip it, and any trailing slash.
                If op2Dir IsNot Nothing Then op2Dir = op2Dir.Trim().Trim(""""c).TrimEnd("\"c, "/"c)
                If String.IsNullOrEmpty(op2Dir) Then op2Dir = Nothing
            ElseIf mapFile Is Nothing Then
                mapFile = arg
            End If

            i += 1
        End While
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

    ''' <summary>
    ''' Win32 RECT (screen coordinates) as passed in the WM_MOVING / WM_SIZING
    ''' lParam; read, adjusted for snapping, then written back.
    ''' </summary>
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
    ''' Saves the window position as soon as the user finishes moving or
    ''' resizing the window (ResizeEnd fires at the end of a move too), so it
    ''' survives a crash or forced exit, not just a clean close.
    ''' </summary>
    Private Sub fMain_ResizeEnd(sender As Object, e As EventArgs) Handles Me.ResizeEnd
        ToolPaletteHelper.SaveMainFormPosition(Me)
    End Sub

    ''' <summary>Window state at the last SizeChanged, to spot maximize / restore.</summary>
    Private lastWindowState As FormWindowState = FormWindowState.Normal

    ''' <summary>
    ''' Saves the position when the window is maximized or restored. Those
    ''' (caption buttons, title-bar double-click, Win+Up) don't raise ResizeEnd.
    ''' </summary>
    Private Sub fMain_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
        If WindowState = lastWindowState Then Return
        lastWindowState = WindowState

        ' Skip the state change RestoreMainFormPosition makes during Load
        ' (the form isn't visible yet), and minimizing, which
        ' SaveMainFormPosition would store as Normal anyway.
        If Not Visible OrElse WindowState = FormWindowState.Minimized Then Return
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
        AppLog.Write("Stop", "Main window closing")

        ' Save fMain's window position+size+state here as well as in FormClosing.
        ' Earlier debugging showed FormClosing doesn't always fire on every
        ' system — Closing has proven reliable (the unsaved-changes prompt
        ' demonstrates it), so this is the canonical save location.
        ToolPaletteHelper.SaveMainFormPosition(Me)

        ' Save the open tool palettes too. Only TileSet is owned by fMain, so
        ' the other four are destroyed at exit without their own FormClosing
        ' ever firing; without this their positions were never saved.
        ToolPaletteHelper.SaveAllToolPositions()
    End Sub

    ''' <summary>
    ''' Shows the settings dialog and, if accepted, (re)creates the resource manager
    ''' for the chosen Outpost 2 folder.
    ''' </summary>
    Private Sub ShowSettingsDialog()
        Dim settings As New fSettings()
        If settings.ShowDialog() = DialogResult.OK Then
            ' The Outpost 2 folder may have changed: reload the edit-mode sound from it next time.
            editModeSoundPlayer = Nothing

            Try
                ' Initialize resource manager with the selected path, then free
                ' the old one (ResourceManager is IDisposable). Nothing caches
                ' the instance: fTileSet reads MapResourceManager on each use,
                ' and MapRenderer only stores its reference.
                Dim oldManager As ResourceManager = resourceManager
                resourceManager = New ResourceManager(AppSettings.OP2Path)
                If oldManager IsNot Nothing Then oldManager.Dispose()
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

        ' Empty the Tilesets list (no map loaded)
        UpdateTilesetList()

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

        ' Generate New Map: op2maplib's map generator (fGenerateMap)
        Dim generateMapMenuItem As New ToolStripMenuItem("Generate New Map...") With {
            .Name = "mnuGenerateMap",
            .ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.N
        }
        AddHandler generateMapMenuItem.Click, AddressOf GenerateMap_Click
        fileMenu.DropDownItems.Add(generateMapMenuItem)

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

        ' Whether a paste also writes the copied cell types (on by default).
        ' Copies always capture cell types, so this can be changed after copying.
        ' Remembered in Mapper3.ini.
        GlobalVariables.PasteCellTypes = Not String.Equals(
            ToolPaletteHelper.ReadToolSetting(KeyPasteCellTypes, "True"), "False",
            StringComparison.OrdinalIgnoreCase)
        Dim pasteCellTypesMenuItem As New ToolStripMenuItem("Paste Includes Cell Types") With {
            .Name = "mnuPasteCellTypes",
            .CheckOnClick = True,
            .Checked = GlobalVariables.PasteCellTypes
        }
        AddHandler pasteCellTypesMenuItem.CheckedChanged, AddressOf PasteCellTypes_CheckedChanged
        editMenu.DropDownItems.Add(pasteCellTypesMenuItem)

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
        ' Right-button-up just ended a pan: this is the menu WinForms opens
        ' for that release — don't show it.
        If suppressNextContextMenu Then
            suppressNextContextMenu = False
            e.Cancel = True
            Return
        End If

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

    ' ---- Sidebar layout ----------------------------------------------------
    ' The properties sidebar is laid out here, in code, as a stack of titled
    ' sections. The designer still creates the labels and picture boxes (other
    ' code refers to them by name); this moves them into the sections.

    ''' <summary>Width of the sidebar panel.</summary>
    Private Const SidebarWidth As Integer = 264
    ''' <summary>Gap at the sidebar's edges.</summary>
    Private Const SidebarMargin As Integer = 6
    ''' <summary>Vertical gap between sidebar sections.</summary>
    Private Const SidebarSectionGap As Integer = 14
    ''' <summary>
    ''' Most tilesets the Tilesets list shows before it scrolls. Typical maps
    ''' use 10-16, so they all show at once.
    ''' </summary>
    Private Const MaxTilesetRows As Integer = 20

    ''' <summary>The sidebar sections, top to bottom (see RestackSidebar).</summary>
    Private ReadOnly sidebarSections As New List(Of GroupBox)
    ''' <summary>Height of one line of text in a sidebar section.</summary>
    Private Const SidebarLineHeight As Integer = 17

    ''' <summary>List of the tileset BMPs the loaded map uses (Tilesets section).</summary>
    Private lstTilesets As ListBox
    ''' <summary>The Tilesets section, whose title shows the tileset count.</summary>
    Private grpTilesets As GroupBox

    ''' <summary>
    ''' Builds the properties sidebar: Map, Tilesets, Tile Under Cursor, View
    ''' and Editing sections stacked top to bottom. The sidebar scrolls when
    ''' the window is too short to show it all.
    ''' </summary>
    Private Sub BuildSidebar()
        pnlProperties.SuspendLayout()
        pnlProperties.Width = SidebarWidth
        pnlProperties.AutoScroll = True

        ' Leave room for a vertical scrollbar so sections never need a
        ' horizontal one.
        Dim sectionWidth As Integer = SidebarWidth - SystemInformation.VerticalScrollBarWidth - SidebarMargin * 2
        Dim innerWidth As Integer = sectionWidth - 16
        Dim y As Integer = SidebarMargin

        ' --- Map ---
        Dim grpMap As GroupBox = AddSidebarSection("Map", y, sectionWidth)
        Dim rowY As Integer = AddSidebarLabels(grpMap, innerWidth, lblMapName, lblMapSize, lblMapImage, lblTileCount)
        grpMap.Height = rowY + 6
        y = grpMap.Bottom + SidebarSectionGap

        ' --- Tilesets (new) ---
        grpTilesets = AddSidebarSection("Tilesets", y, sectionWidth)
        ' Height is set to fit the map's tilesets by UpdateTilesetList.
        lstTilesets = New ListBox With {
            .Name = "lstTilesets",
            .Location = New Point(8, 20),
            .Width = innerWidth,
            .IntegralHeight = False,
            .HorizontalScrollbar = True,
            .SelectionMode = SelectionMode.None,
            .BorderStyle = BorderStyle.FixedSingle
        }
        grpTilesets.Controls.Add(lstTilesets)
        SizeTilesetList(1)
        y = grpTilesets.Bottom + SidebarSectionGap

        ' --- Tile under the cursor: details plus small and large previews ---
        Dim grpTile As GroupBox = AddSidebarSection("Tile Under Cursor", y, sectionWidth)
        rowY = AddSidebarLabels(grpTile, innerWidth, lblCoordinates, lblTileset, lblTilesetImage, lblMappingIndex, lblCellType)

        picTilePreview.Name = "picTilePreview"
        picTilePreview.Size = New Size(34, 34)          ' 32 px tile + border
        picTilePreview.Location = New Point(8, rowY + 6)
        picTilePreview.BorderStyle = BorderStyle.FixedSingle
        picTilePreview.SizeMode = PictureBoxSizeMode.Normal
        picTilePreview.BackColor = System.Drawing.Color.Black
        grpTile.Controls.Add(picTilePreview)

        picTilePreviewBig.Name = "picTilePreviewBig"
        picTilePreviewBig.Size = New Size(98, 98)       ' 96 px (3x) + border
        picTilePreviewBig.Location = New Point(picTilePreview.Right + 10, rowY + 6)
        picTilePreviewBig.BorderStyle = BorderStyle.FixedSingle
        picTilePreviewBig.SizeMode = PictureBoxSizeMode.StretchImage
        picTilePreviewBig.BackColor = System.Drawing.Color.Black
        grpTile.Controls.Add(picTilePreviewBig)

        grpTile.Height = picTilePreviewBig.Bottom + 8
        y = grpTile.Bottom + SidebarSectionGap

        ' --- View: zoom level and Reset Zoom on one row ---
        Dim grpView As GroupBox = AddSidebarSection("View", y, sectionWidth)
        Dim btnResetZoom As New Button With {
            .Name = "btnResetZoom",
            .Text = "Reset Zoom",
            .Size = New Size(84, 24)
        }
        btnResetZoom.Location = New Point(sectionWidth - btnResetZoom.Width - 8, 18)
        AddHandler btnResetZoom.Click, AddressOf ResetZoom_Click
        grpView.Controls.Add(btnResetZoom)
        PlaceSidebarLabel(grpView, lblZoomLevel, New Point(8, 22), btnResetZoom.Left - 12)
        grpView.Height = btnResetZoom.Bottom + 8
        y = grpView.Bottom + SidebarSectionGap

        ' --- Editing: mode buttons, then the active tool and its icon ---
        Dim grpEdit As GroupBox = AddSidebarSection("Editing", y, sectionWidth)

        btnEditMode.Name = "btnEditMode"
        btnEditMode.Text = "Enable Edit Mode"
        btnEditMode.SetBounds(8, 20, innerWidth, 28)
        AddHandler btnEditMode.Click, AddressOf EditMode_Click
        grpEdit.Controls.Add(btnEditMode)

        Dim btnClearTool As New Button With {
            .Name = "btnClearTool",
            .Text = "Clear Tool Selection"
        }
        btnClearTool.SetBounds(8, btnEditMode.Bottom + 6, innerWidth, 28)
        AddHandler btnClearTool.Click, AddressOf ClearTool_Click
        grpEdit.Controls.Add(btnClearTool)

        picSelectedTool.Name = "picSelectedTool"
        picSelectedTool.Size = New Size(34, 34)
        picSelectedTool.Location = New Point(8, btnClearTool.Bottom + 10)
        picSelectedTool.BorderStyle = BorderStyle.FixedSingle
        picSelectedTool.SizeMode = PictureBoxSizeMode.StretchImage
        picSelectedTool.BackColor = System.Drawing.Color.Black
        grpEdit.Controls.Add(picSelectedTool)

        ' Tool and placement text sit beside the tool icon.
        Dim textLeft As Integer = picSelectedTool.Right + 8
        Dim textWidth As Integer = sectionWidth - textLeft - 8
        PlaceSidebarLabel(grpEdit, lblCurrentTool, New Point(textLeft, picSelectedTool.Top), textWidth)
        PlaceSidebarLabel(grpEdit, lblCurrentPlacement, New Point(textLeft, picSelectedTool.Top + SidebarLineHeight), textWidth)

        grpEdit.Height = picSelectedTool.Bottom + 10

        pnlProperties.ResumeLayout(True)
    End Sub

    ''' <summary>
    ''' Adds an empty titled section to the sidebar at the given top position.
    ''' The caller fills it and sets its height.
    ''' </summary>
    Private Function AddSidebarSection(title As String, top As Integer, width As Integer) As GroupBox
        Dim section As New GroupBox With {
            .Text = title,
            .Location = New Point(SidebarMargin, top),
            .Width = width
        }
        pnlProperties.Controls.Add(section)
        sidebarSections.Add(section)
        Return section
    End Function

    ''' <summary>
    ''' Re-positions the sidebar sections top to bottom with SidebarSectionGap
    ''' between them. Called when a section changes height (the Tilesets list
    ''' grows or shrinks with the map's tileset count).
    ''' </summary>
    Private Sub RestackSidebar()
        ' Section positions are relative to the scrolled content, so allow
        ' for the current scroll offset (AutoScrollPosition.Y is <= 0).
        Dim y As Integer = SidebarMargin + pnlProperties.AutoScrollPosition.Y
        For Each section In sidebarSections
            section.Top = y
            y = section.Bottom + SidebarSectionGap
        Next
    End Sub

    ''' <summary>
    ''' Sizes the Tilesets list to show the given number of rows (at least one,
    ''' at most MaxTilesetRows, beyond which it scrolls), sizes its section to
    ''' match, and moves the sections below.
    ''' </summary>
    Private Sub SizeTilesetList(rows As Integer)
        If lstTilesets Is Nothing Then Return
        rows = Math.Max(1, Math.Min(MaxTilesetRows, rows))

        ' Rows plus the 1 px border top and bottom; add room for the
        ' horizontal scrollbar only when a name is too long to fit.
        Dim height As Integer = rows * lstTilesets.ItemHeight + 4
        Dim widestItem As Integer = 0
        For Each item In lstTilesets.Items
            widestItem = Math.Max(widestItem, TextRenderer.MeasureText(item.ToString(), lstTilesets.Font).Width)
        Next
        If widestItem > lstTilesets.Width - 4 Then
            height += SystemInformation.HorizontalScrollBarHeight
        End If
        lstTilesets.Height = height
        grpTilesets.Height = lstTilesets.Bottom + 8
        RestackSidebar()
    End Sub

    ''' <summary>
    ''' Moves the given labels into a section as one line each, starting just
    ''' under the section title. Returns the y position below the last line.
    ''' </summary>
    Private Function AddSidebarLabels(section As GroupBox, width As Integer, ParamArray labels As Label()) As Integer
        Dim rowY As Integer = 20
        For Each lbl As Label In labels
            PlaceSidebarLabel(section, lbl, New Point(8, rowY), width)
            rowY += SidebarLineHeight
        Next
        Return rowY
    End Function

    ''' <summary>
    ''' Moves a label into a section as a fixed-width single line. Text that
    ''' doesn't fit ends in "…", and hovering the label shows all of it.
    ''' </summary>
    Private Sub PlaceSidebarLabel(section As GroupBox, lbl As Label, location As Point, width As Integer)
        lbl.AutoSize = False
        lbl.AutoEllipsis = True
        lbl.SetBounds(location.X, location.Y, width, SidebarLineHeight)
        section.Controls.Add(lbl)
    End Sub

    ''' <summary>
    ''' Fills the Tilesets section with every tileset BMP the loaded map uses,
    ''' in the map's tileset order, with each one's tile count; a tileset that
    ''' couldn't be loaded shows "not found". Empties it when no map is loaded.
    ''' </summary>
    Private Sub UpdateTilesetList()
        If lstTilesets Is Nothing Then Return ' Sidebar not built yet

        lstTilesets.BeginUpdate()
        lstTilesets.Items.Clear()

        Dim count As Integer = 0
        Dim missing As Integer = 0
        If currentMap IsNot Nothing Then
            Dim tileCounts As Dictionary(Of String, Integer) =
                If(mapRenderer IsNot Nothing, mapRenderer.ListLoadedTilesets(), New Dictionary(Of String, Integer))

            For Each source In currentMap.tilesetSources
                If source.IsEmpty() Then Continue For
                count += 1

                Dim name As String = source.tilesetFilename
                Dim fileName As String = If(name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase), name, name & ".bmp")
                Dim tiles As Integer = 0
                tileCounts.TryGetValue(name, tiles)

                If tiles > 0 Then
                    lstTilesets.Items.Add($"{fileName}  ({tiles} {If(tiles = 1, "tile", "tiles")})")
                Else
                    lstTilesets.Items.Add($"{fileName}  (not found)")
                    missing += 1
                End If
            Next
        End If

        lstTilesets.EndUpdate()

        ' Grow (or shrink) the list so every tileset shows without scrolling.
        SizeTilesetList(lstTilesets.Items.Count)

        ' Section title carries the count, and flags any that failed to load.
        If currentMap Is Nothing Then
            grpTilesets.Text = "Tilesets"
        ElseIf missing > 0 Then
            grpTilesets.Text = $"Tilesets ({count}, {missing} not found)"
        Else
            grpTilesets.Text = $"Tilesets ({count})"
        End If
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


