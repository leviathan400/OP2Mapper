Imports System.Drawing
Imports System.Windows.Forms
Imports System.IO

' ToolPalette.vb - Shared helper module for the floating tool palette windows.
' Handles palette registration, INI-based position save/restore, recent files,
' edge/window snapping, and first-run smart layout of the tool windows.
Module ToolPaletteHelper

    Private Const SideBorderOffset As Integer = 12
    Private Const BottomBorderOffset As Integer = 5
    ' Bumped from 12 — at 12 px users had to nudge windows very precisely to
    ' get them to dock at the screen top / against other palettes.
    Private Const SnapDistance As Integer = 20
    Private Const IniFileName As String = "Mapper3.ini"
    Private Const WindowSection As String = "Window"
    Private ToolForms As New List(Of Form)

    ' Names used as INI keys + when looking up tool forms for smart layout.
    ' Must match the strings passed to RestorePosition / SavePosition from
    ' each form's Load / FormClosing handler.
    Private Const KeyTileSet As String = "Tileset"
    Private Const KeyCellTypes As String = "CellTypes"
    Private Const KeyTileSelect As String = "TileSelect"
    Private Const KeyTileGroups As String = "TileGroups"
    Private Const KeyMiniMap As String = "MiniMap"

    ' Recent-files list (most-recent-first). Stored as RecentFile1..N in the
    ' INI's [Window] section — reusing the existing section to avoid extra
    ' parsing complexity.
    Public Const MaxRecentFiles As Integer = 5
    Private Const RecentFileKeyPrefix As String = "RecentFile"

    ' "View → Tool Windows Always On Top" — defaults to True so first-run
    ' behavior matches the previous hardcoded TopMost = True.
    Private Const KeyToolWindowsAlwaysOnTop As String = "ToolWindowsAlwaysOnTop"

#Region "Form Tracking"
    ''' <summary>
    ''' Adds a form to the tracked tool-palette list (no-op if already registered)
    ''' </summary>
    Public Sub RegisterToolForm(form As Form)
        If Not ToolForms.Contains(form) Then
            ToolForms.Add(form)
            Debug.WriteLine($"Registered tool form: {form.Name}")
        End If
    End Sub

    ''' <summary>
    ''' Removes a form from the tracked tool-palette list (called on form close)
    ''' </summary>
    Public Sub UnregisterToolForm(form As Form)
        ToolForms.Remove(form)
        'Debug.WriteLine($"Unregistered Tool Form: {form.Name}")
    End Sub

    ''' <summary>
    ''' Returns the list of currently registered tool-palette forms
    ''' </summary>
    Public Function GetToolForms() As List(Of Form)
        Return ToolForms
    End Function

#End Region

#Region "Form Configuration"

    ''' <summary>
    ''' Configures a form as a tool palette with standard appearance
    ''' </summary>
    Public Sub ConfigureAsToolPalette(form As Form)
        Debug.WriteLine("ConfigureAsToolPalette: " & form.Name.ToString)

        form.FormBorderStyle = FormBorderStyle.FixedToolWindow
        ' TopMost is user-controllable via View → Tool Windows Always On Top.
        ' Stored in Mapper3.ini; defaults to True so first-run matches the
        ' previous hardcoded behavior.
        form.TopMost = GetToolWindowsAlwaysOnTop()
        form.ShowInTaskbar = False
        form.Icon = My.Resources.lists
        form.Tag = "ToolPalette"
        RegisterToolForm(form)
    End Sub

    ''' <summary>
    ''' Reads the "Tool Windows Always On Top" preference from the INI.
    ''' Defaults to True if not set.
    ''' </summary>
    Public Function GetToolWindowsAlwaysOnTop() As Boolean
        Return ReadIniBoolean(KeyToolWindowsAlwaysOnTop, defaultValue:=True)
    End Function

    ''' <summary>
    ''' Persists the "Tool Windows Always On Top" preference to the INI.
    ''' </summary>
    Public Sub SetToolWindowsAlwaysOnTop(value As Boolean)
        WriteIniValue(KeyToolWindowsAlwaysOnTop, value.ToString())
    End Sub

    ''' <summary>
    ''' Applies the supplied TopMost value to every registered tool palette
    ''' that's currently open. Called by fMain when the user toggles the
    ''' "Tool Windows Always On Top" menu item.
    ''' </summary>
    Public Sub ApplyTopMostToAllToolForms(topMost As Boolean)
        For Each f As Form In GetToolForms()
            If f IsNot Nothing AndAlso Not f.IsDisposed Then
                f.TopMost = topMost
            End If
        Next
    End Sub

#End Region

#Region "INI File Handling"

    ''' <summary>
    ''' Gets the full path to the INI file
    ''' </summary>
    Private Function GetIniFilePath() As String
        Dim exeDir As String = Path.GetDirectoryName(Application.ExecutablePath)
        Return Path.Combine(exeDir, IniFileName)
    End Function

    ''' <summary>
    ''' Reads a value from the INI file
    ''' </summary>
    Private Function ReadIniValue(key As String, Optional defaultValue As String = "") As String
        Dim iniPath As String = GetIniFilePath()

        If Not File.Exists(iniPath) Then
            Return defaultValue
        End If

        Try
            Dim lines() As String = File.ReadAllLines(iniPath)
            Dim inWindowSection As Boolean = False

            For Each line As String In lines
                Dim trimmedLine As String = line.Trim()

                ' Check for section header
                If trimmedLine.StartsWith("[") AndAlso trimmedLine.EndsWith("]") Then
                    inWindowSection = trimmedLine.Equals($"[{WindowSection}]", StringComparison.OrdinalIgnoreCase)
                    Continue For
                End If

                ' Look for key in Window section
                If inWindowSection AndAlso trimmedLine.StartsWith(key & "=", StringComparison.OrdinalIgnoreCase) Then
                    Return trimmedLine.Substring(key.Length + 1)
                End If
            Next

        Catch ex As Exception
            Debug.WriteLine($"Error reading INI file: {ex.Message}")
        End Try

        Return defaultValue
    End Function

    ''' <summary>
    ''' Writes a value to the INI file
    ''' </summary>
    Private Sub WriteIniValue(key As String, value As String)
        Dim iniPath As String = GetIniFilePath()
        Dim lines As New List(Of String)
        Dim keyFound As Boolean = False
        Dim sectionFound As Boolean = False
        Dim inWindowSection As Boolean = False
        Dim windowSectionEndIndex As Integer = -1

        ' Read existing file if it exists
        If File.Exists(iniPath) Then
            Try
                lines.AddRange(File.ReadAllLines(iniPath))
            Catch ex As Exception
                Debug.WriteLine($"Error reading INI file: {ex.Message}")
            End Try
        End If

        ' Process existing lines
        For i As Integer = 0 To lines.Count - 1
            Dim trimmedLine As String = lines(i).Trim()

            ' Check for section header
            If trimmedLine.StartsWith("[") AndAlso trimmedLine.EndsWith("]") Then
                If inWindowSection Then
                    windowSectionEndIndex = i
                End If
                inWindowSection = trimmedLine.Equals($"[{WindowSection}]", StringComparison.OrdinalIgnoreCase)
                If inWindowSection Then
                    sectionFound = True
                End If
                Continue For
            End If

            ' Update key if found in Window section
            If inWindowSection AndAlso trimmedLine.StartsWith(key & "=", StringComparison.OrdinalIgnoreCase) Then
                lines(i) = $"{key}={value}"
                keyFound = True
            End If
        Next

        ' Track end of file if we ended in Window section
        If inWindowSection AndAlso windowSectionEndIndex = -1 Then
            windowSectionEndIndex = lines.Count
        End If

        ' Add section if not found
        If Not sectionFound Then
            If lines.Count > 0 AndAlso Not String.IsNullOrWhiteSpace(lines(lines.Count - 1)) Then
                lines.Add("")
            End If
            lines.Add($"[{WindowSection}]")
            lines.Add($"{key}={value}")
            keyFound = True
        ElseIf Not keyFound Then
            ' Add key to existing section
            If windowSectionEndIndex >= 0 Then
                lines.Insert(windowSectionEndIndex, $"{key}={value}")
            Else
                lines.Add($"{key}={value}")
            End If
        End If

        ' Write file
        Try
            File.WriteAllLines(iniPath, lines)
        Catch ex As Exception
            Debug.WriteLine($"Error writing INI file: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Reads an integer value from the INI file
    ''' </summary>
    Private Function ReadIniInteger(key As String, Optional defaultValue As Integer = 0) As Integer
        Dim strValue As String = ReadIniValue(key, defaultValue.ToString())
        Dim result As Integer
        If Integer.TryParse(strValue, result) Then
            Return result
        End If
        Return defaultValue
    End Function

    ''' <summary>
    ''' Reads a Boolean value from the INI file. Accepts True/False (any case)
    ''' and 0/1 for convenience.
    ''' </summary>
    Private Function ReadIniBoolean(key As String, Optional defaultValue As Boolean = False) As Boolean
        Dim strValue As String = ReadIniValue(key, defaultValue.ToString())
        Dim result As Boolean
        If Boolean.TryParse(strValue, result) Then
            Return result
        End If
        ' Tolerate 0/1 in case the user hand-edited the INI.
        Dim intValue As Integer
        If Integer.TryParse(strValue, intValue) Then
            Return intValue <> 0
        End If
        Return defaultValue
    End Function

#End Region

#Region "Recent Files"

    ''' <summary>
    ''' Reads the Recent Files list from the INI. Filters out any paths that
    ''' no longer exist on disk, so the menu never offers a dead link.
    ''' </summary>
    Public Function LoadRecentFiles() As List(Of String)
        Dim files As New List(Of String)
        For i As Integer = 1 To MaxRecentFiles
            Dim path As String = ReadIniValue(RecentFileKeyPrefix & i, "")
            If Not String.IsNullOrWhiteSpace(path) AndAlso File.Exists(path) Then
                files.Add(path)
            End If
        Next
        Return files
    End Function

    ''' <summary>
    ''' Writes the Recent Files list to the INI (most-recent-first). Unused
    ''' slots are written as empty strings so old entries get cleared.
    ''' </summary>
    Public Sub SaveRecentFiles(paths As List(Of String))
        For i As Integer = 1 To MaxRecentFiles
            Dim value As String = ""
            If paths IsNot Nothing AndAlso i - 1 < paths.Count Then
                value = paths(i - 1)
            End If
            WriteIniValue(RecentFileKeyPrefix & i, value)
        Next
    End Sub

#End Region

#Region "Position Save/Restore"

    ''' <summary>
    ''' Saves a form's position to the INI file
    ''' </summary>
    Public Sub SavePosition(form As Form, formName As String)
        Debug.WriteLine("SavePosition: " & form.Name.ToString)

        Try
            WriteIniValue($"{formName}Left", form.Location.X.ToString())
            WriteIniValue($"{formName}Top", form.Location.Y.ToString())
            WriteIniValue($"{formName}Width", form.Width.ToString())
            WriteIniValue($"{formName}Height", form.Height.ToString())
        Catch ex As Exception
            Debug.WriteLine($"Error saving position for {formName}: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Restores a form's position from the INI file
    ''' </summary>
    Public Sub RestorePosition(form As Form, formName As String)
        Try
            Dim left As Integer = ReadIniInteger($"{formName}Left", Integer.MinValue)
            Dim top As Integer = ReadIniInteger($"{formName}Top", Integer.MinValue)

            ' Check if we have saved values
            If left = Integer.MinValue OrElse top = Integer.MinValue Then
                Return
            End If

            Dim savedLocation As New Point(left, top)

            ' Verify the position is on a visible screen
            If IsFormAccessibleAtLocation(savedLocation, form.Size) Then
                form.StartPosition = FormStartPosition.Manual
                form.Location = savedLocation
            End If

        Catch ex As Exception
            Debug.WriteLine($"Error restoring position for {formName}: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Saves the main form's position and state to the INI file
    ''' </summary>
    Public Sub SaveMainFormPosition(form As Form)
        Try
            ' Treat Minimized as Normal so re-opening doesn't come back minimized
            ' (with no taskbar entry to find it).
            Dim stateToSave As FormWindowState = form.WindowState
            If stateToSave = FormWindowState.Minimized Then
                stateToSave = FormWindowState.Normal
            End If
            WriteIniValue("MainWindowState", CInt(stateToSave).ToString())

            ' When the window is currently maximized or minimized, form.Bounds
            ' reflects the maximized/minimized state — useless for remembering
            ' where the user actually wants it. Use RestoreBounds instead, which
            ' tracks the last "Normal" position+size.
            Dim bounds As Rectangle =
                If(form.WindowState = FormWindowState.Normal, form.Bounds, form.RestoreBounds)

            WriteIniValue("MainLeft", bounds.Left.ToString())
            WriteIniValue("MainTop", bounds.Top.ToString())
            WriteIniValue("MainWidth", bounds.Width.ToString())
            WriteIniValue("MainHeight", bounds.Height.ToString())

            Debug.WriteLine($"Saved main form: state={stateToSave}, bounds={bounds}, ini={GetIniFilePath()}")
        Catch ex As Exception
            Debug.WriteLine($"Error saving main form position: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Restores the main form's position and state from the INI file
    ''' </summary>
    Public Sub RestoreMainFormPosition(form As Form)
        Try
            Dim left As Integer = ReadIniInteger("MainLeft", Integer.MinValue)
            Dim top As Integer = ReadIniInteger("MainTop", Integer.MinValue)
            Dim width As Integer = ReadIniInteger("MainWidth", Integer.MinValue)
            Dim height As Integer = ReadIniInteger("MainHeight", Integer.MinValue)
            Dim windowState As Integer = ReadIniInteger("MainWindowState", CInt(FormWindowState.Normal))

            ' Need at least Left/Top to do anything.
            If left = Integer.MinValue OrElse top = Integer.MinValue Then
                Debug.WriteLine($"Restore main form: no saved position in INI ({GetIniFilePath()}), using defaults.")
                Return
            End If

            Dim savedLocation As New Point(left, top)
            Dim savedSize As New Size(
                If(width > 0, width, form.Width),
                If(height > 0, height, form.Height)
            )
            Dim savedBounds As New Rectangle(savedLocation, savedSize)

            ' Reject if the saved bounds don't overlap any visible screen.
            ' (Common when the test rig has fewer / differently-arranged
            ' monitors than the system where the position was saved.)
            If Not IsBoundsVisibleOnAnyScreen(savedBounds) Then
                Debug.WriteLine($"Restore main form: saved bounds {savedBounds} are not on any visible screen, using defaults.")
                Return
            End If

            form.StartPosition = FormStartPosition.Manual
            form.Location = savedLocation
            form.Size = savedSize

            ' Apply WindowState AFTER Location/Size so that, if we're restoring
            ' Maximized, the form has already seeded its RestoreBounds with the
            ' saved Normal-state position. Refuse to restore Minimized (clamp
            ' to Normal) so the app doesn't reappear minimized with no taskbar
            ' icon to find it.
            Dim stateToApply As FormWindowState = FormWindowState.Normal
            If [Enum].IsDefined(GetType(FormWindowState), windowState) Then
                stateToApply = CType(windowState, FormWindowState)
                If stateToApply = FormWindowState.Minimized Then
                    stateToApply = FormWindowState.Normal
                End If
            End If
            form.WindowState = stateToApply

            Debug.WriteLine($"Restored main form: state={stateToApply}, bounds={savedBounds}, ini={GetIniFilePath()}")
        Catch ex As Exception
            Debug.WriteLine($"Error restoring main form position: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' True if any part of the form's bounds overlaps a visible screen's
    ''' working area. Stricter than the title-bar-only check used for tool
    ''' palettes — for the main form we want most of it on-screen.
    ''' </summary>
    Private Function IsBoundsVisibleOnAnyScreen(bounds As Rectangle) As Boolean
        For Each screen As Screen In Screen.AllScreens
            Dim intersection As Rectangle = Rectangle.Intersect(screen.WorkingArea, bounds)
            ' Require at least a 200x150 visible chunk so the user can grab
            ' the title bar and at least see/resize the window.
            If intersection.Width >= 200 AndAlso intersection.Height >= 150 Then
                Return True
            End If
        Next
        Return False
    End Function

    ''' <summary>
    ''' Checks if a form would be accessible (title bar visible) at the given location
    ''' </summary>
    Private Function IsFormAccessibleAtLocation(location As Point, formSize As Size) As Boolean
        Dim titleBarRect As New Rectangle(location.X, location.Y, formSize.Width, 30)

        For Each screen As Screen In Screen.AllScreens
            If screen.WorkingArea.IntersectsWith(titleBarRect) Then
                Return True
            End If
        Next

        Return False
    End Function

#End Region

#Region "Edge Snapping"

    ' Snap algorithm overview:
    ' Each tool form overrides WndProc and, on WM_MOVING, passes the proposed
    ' window location to GetSnappedPosition. That checks the moving window's
    ' edges against (1) every screen's working-area edges, then (2) the edges
    ' of every other registered tool palette. Any edge pair closer than
    ' SnapDistance pixels pulls the window flush to that edge (with the
    ' Side/Bottom border offsets compensating for the invisible window
    ' borders), and the adjusted RECT is written back to the move message.

    ''' <summary>
    ''' Calculates snapped position for a form being moved.
    ''' Holding Ctrl during the drag bypasses snapping entirely so the user
    ''' can free-move a tool window without it getting "stuck" to edges or
    ''' other palettes.
    ''' </summary>
    Public Function GetSnappedPosition(movingForm As Form, proposedLocation As Point) As Point
        ' Ctrl held → free move, no snapping.
        If (Control.ModifierKeys And Keys.Control) = Keys.Control Then
            Return proposedLocation
        End If

        Dim result As Point = proposedLocation
        Dim formRect As New Rectangle(proposedLocation, movingForm.Size)

        ' Snap to screen edges first
        result = SnapToScreenEdges(formRect, result, movingForm.Size)

        ' Update formRect with screen snapping applied
        formRect = New Rectangle(result, movingForm.Size)

        ' Snap to other tool forms (fMain is intentionally excluded — only
        ' forms registered via ConfigureAsToolPalette are in GetToolForms()).
        result = SnapToOtherForms(movingForm, formRect, result)

        Return result
    End Function

    ''' <summary>
    ''' Snaps the form's edges to any screen working-area edge within SnapDistance
    ''' </summary>
    Private Function SnapToScreenEdges(formRect As Rectangle, currentResult As Point, formSize As Size) As Point
        Dim result As Point = currentResult

        For Each screen As Screen In Screen.AllScreens
            Dim workArea As Rectangle = screen.WorkingArea

            ' Snap left edge to screen left
            If Math.Abs(formRect.Left - workArea.Left) < SnapDistance Then
                result.X = workArea.Left
            End If

            ' Snap right edge to screen right
            If Math.Abs(formRect.Right - workArea.Right) < SnapDistance Then
                result.X = workArea.Right - formSize.Width
            End If

            ' Snap top edge to screen top
            If Math.Abs(formRect.Top - workArea.Top) < SnapDistance Then
                result.Y = workArea.Top
            End If

            ' Snap bottom edge to screen bottom
            If Math.Abs(formRect.Bottom - workArea.Bottom) < SnapDistance Then
                result.Y = workArea.Bottom - formSize.Height
            End If
        Next

        Return result
    End Function

    ''' <summary>
    ''' Snaps the moving form's edges to the edges of other visible tool palettes,
    ''' including side-by-side docking and left/top/right edge alignment
    ''' </summary>
    Private Function SnapToOtherForms(movingForm As Form, formRect As Rectangle, currentResult As Point) As Point
        Dim result As Point = currentResult

        For Each otherForm As Form In GetToolForms()
            ' Skip self
            If otherForm Is movingForm Then Continue For

            ' Skip if not visible
            If Not otherForm.Visible Then Continue For

            Dim otherRect As Rectangle = otherForm.Bounds

            ' Horizontal snapping (when vertically overlapping)
            If formRect.Bottom > otherRect.Top AndAlso formRect.Top < otherRect.Bottom Then
                ' Snap our left edge to their right edge
                If Math.Abs(formRect.Left - otherRect.Right) < SnapDistance Then
                    result.X = otherRect.Right - SideBorderOffset
                End If

                ' Snap our right edge to their left edge
                If Math.Abs(formRect.Right - otherRect.Left) < SnapDistance Then
                    result.X = otherRect.Left - movingForm.Width + SideBorderOffset
                End If
            End If

            ' Vertical snapping (when horizontally overlapping)
            If formRect.Right > otherRect.Left AndAlso formRect.Left < otherRect.Right Then
                ' Snap our top edge to their bottom edge
                If Math.Abs(formRect.Top - otherRect.Bottom) < SnapDistance Then
                    ' Other form's bottom has invisible border, so subtract it
                    result.Y = otherRect.Bottom - BottomBorderOffset
                End If

                ' Snap our bottom edge to their top edge
                If Math.Abs(formRect.Bottom - otherRect.Top) < SnapDistance Then
                    ' Our bottom has invisible border, their top doesn't (title bar)
                    result.Y = otherRect.Top - movingForm.Height + BottomBorderOffset
                End If
            End If

            ' Edge alignment (snap to same left or top edge)
            If Math.Abs(formRect.Left - otherRect.Left) < SnapDistance Then
                result.X = otherRect.Left
            End If

            If Math.Abs(formRect.Top - otherRect.Top) < SnapDistance Then
                result.Y = otherRect.Top
            End If

            ' Right edge alignment
            If Math.Abs(formRect.Right - otherRect.Right) < SnapDistance Then
                result.X = otherRect.Right - movingForm.Width
            End If
        Next

        Return result
    End Function

    ''' <summary>
    ''' True if the form is one of the five known tool-palette form types
    ''' </summary>
    Private Function IsToolForm(form As Form) As Boolean
        Return TypeOf form Is fTileSet OrElse
               TypeOf form Is fCellTypes OrElse
               TypeOf form Is fTileSelect OrElse
               TypeOf form Is fTileGroups OrElse
               TypeOf form Is fMiniMap
    End Function

#End Region

#Region "Arrange Windows"

    ''' <summary>
    ''' Returns True when at least one of the five tool palettes has a saved
    ''' position in the INI file. A False result means a first run, so the
    ''' smart layout can pick sensible defaults.
    ''' </summary>
    Public Function HasAnySavedToolPositions() As Boolean
        Dim keys = {KeyTileSet, KeyCellTypes, KeyTileSelect, KeyTileGroups, KeyMiniMap}
        For Each k In keys
            If ReadIniInteger(k & "Left", Integer.MinValue) <> Integer.MinValue Then
                Return True
            End If
        Next
        Return False
    End Function

    ''' <summary>
    ''' Returns the open form instance of the given type, or Nothing if not open
    ''' </summary>
    Private Function FindOpenToolForm(formType As Type) As Form
        For Each f As Form In Application.OpenForms
            If formType.IsInstanceOfType(f) Then Return f
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' Picks a default placement for the five tool palettes based on screen
    ''' count and primary screen width. Called once on first run; positions
    ''' are then saved to the INI so subsequent runs restore them per-form.
    '''
    ''' Layout for a wide single screen:
    '''   Top row (left → right): TileSelect | TileSet | CellTypes
    '''   Second row:             MiniMap    |   TileGroups (spans TS+CT)
    '''
    ''' Layout for multi-monitor: all five tools on the secondary screen
    ''' using the same arrangement (primary stays free for fMain).
    '''
    ''' Layout for a narrow single screen: cascade from top-left so every
    ''' window is at least visible.
    ''' </summary>
    Public Sub ArrangeToolWindowsSmart()
        Dim tileSet = FindOpenToolForm(GetType(fTileSet))
        Dim cellTypes = FindOpenToolForm(GetType(fCellTypes))
        Dim tileSelect = FindOpenToolForm(GetType(fTileSelect))
        Dim tileGroups = FindOpenToolForm(GetType(fTileGroups))
        Dim miniMap = FindOpenToolForm(GetType(fMiniMap))

        ' Pick the screen to lay out on. If the user has a second monitor,
        ' use it (keeps the primary clear for fMain). Otherwise primary.
        Dim targetScreen As Screen = If(Screen.AllScreens.Length > 1,
                                         Screen.AllScreens(1),
                                         Screen.PrimaryScreen)
        Dim workArea As Rectangle = targetScreen.WorkingArea

        ' Wide enough to do the proper right-side layout. Threshold chosen so
        ' TileSelect (479) + TileSet (248) + CellTypes (217) = 944 px of tools
        ' fits on the right while still leaving ~700+ px for fMain on the left.
        Dim wideEnough As Boolean = workArea.Width >= 1700

        If wideEnough Then
            LayoutWideScreen(workArea, tileSet, cellTypes, tileSelect, tileGroups, miniMap)
        Else
            LayoutCascade(workArea, tileSet, cellTypes, tileSelect, tileGroups, miniMap)
        End If

        ' Persist the chosen positions so this only runs once.
        SaveToolPosition(tileSet, KeyTileSet)
        SaveToolPosition(cellTypes, KeyCellTypes)
        SaveToolPosition(tileSelect, KeyTileSelect)
        SaveToolPosition(tileGroups, KeyTileGroups)
        SaveToolPosition(miniMap, KeyMiniMap)
    End Sub

    ''' <summary>
    ''' Saves a tool form's position under the given INI key, skipping Nothing
    ''' </summary>
    Private Sub SaveToolPosition(form As Form, key As String)
        If form Is Nothing Then Return
        SavePosition(form, key)
    End Sub

    ''' <summary>
    ''' Right-side dock for a wide screen. Matches the arrangement the user
    ''' confirmed works for them (CellTypes at the rightmost edge, TileSet
    ''' immediately to its left, TileSelect left of those, MiniMap below
    ''' TileSelect, TileGroups under TileSet/CellTypes).
    ''' </summary>
    Private Sub LayoutWideScreen(workArea As Rectangle,
                                 tileSet As Form, cellTypes As Form,
                                 tileSelect As Form, tileGroups As Form,
                                 miniMap As Form)
        ' Anchor: top-right corner of the working area.
        Dim right As Integer = workArea.Right
        Dim top As Integer = workArea.Top

        ' Row 1 — top edge, right-aligned cluster.
        If cellTypes IsNot Nothing Then
            cellTypes.Location = New Point(right - cellTypes.Width, top)
        End If

        If tileSet IsNot Nothing Then
            Dim ctW As Integer = If(cellTypes IsNot Nothing, cellTypes.Width, 217)
            tileSet.Location = New Point(right - ctW - tileSet.Width, top)
        End If

        If tileSelect IsNot Nothing Then
            Dim ctW As Integer = If(cellTypes IsNot Nothing, cellTypes.Width, 217)
            Dim tsW As Integer = If(tileSet IsNot Nothing, tileSet.Width, 248)
            tileSelect.Location = New Point(right - ctW - tsW - tileSelect.Width, top)
        End If

        ' Row 2 — directly under the row-1 forms.
        ' TileSet + CellTypes are 445 tall; TileSelect is 245 tall. We line
        ' the second row under TileSelect for MiniMap and under TileSet/CT
        ' for TileGroups.
        If miniMap IsNot Nothing AndAlso tileSelect IsNot Nothing Then
            miniMap.Location = New Point(tileSelect.Left, top + tileSelect.Height)
        ElseIf miniMap IsNot Nothing Then
            miniMap.Location = New Point(right - miniMap.Width - 500, top + 245)
        End If

        If tileGroups IsNot Nothing AndAlso tileSet IsNot Nothing Then
            tileGroups.Location = New Point(right - tileGroups.Width, top + tileSet.Height)
        ElseIf tileGroups IsNot Nothing Then
            tileGroups.Location = New Point(right - tileGroups.Width, top + 445)
        End If
    End Sub

    ''' <summary>
    ''' Fallback for narrow screens — cascade from top-left so at least
    ''' every window is visible and the user can rearrange manually.
    ''' </summary>
    Private Sub LayoutCascade(workArea As Rectangle,
                              tileSet As Form, cellTypes As Form,
                              tileSelect As Form, tileGroups As Form,
                              miniMap As Form)
        Const cascadeStep As Integer = 32
        Dim x As Integer = workArea.Left + 16
        Dim y As Integer = workArea.Top + 16

        For Each f As Form In {tileSet, cellTypes, tileSelect, tileGroups, miniMap}
            If f IsNot Nothing Then
                f.Location = New Point(x, y)
                x += cascadeStep
                y += cascadeStep
            End If
        Next
    End Sub

    ''' <summary>
    ''' Legacy entry point — kept for compatibility but now just calls the
    ''' smart layout. Was never wired into the app, so this is effectively a
    ''' rename.
    ''' </summary>
    Public Sub ArrangeToolWindows()
        ArrangeToolWindowsSmart()
    End Sub

#End Region

End Module