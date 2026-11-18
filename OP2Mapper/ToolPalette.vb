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

    ' Per-window override, stored as AlwaysOnTop_<form class name>. Falls back
    ' to ToolWindowsAlwaysOnTop when absent.
    Private Const KeyToolWindowTopMostPrefix As String = "AlwaysOnTop_"
    Private ReadOnly ToolFormTypeNames As String() =
        {"fTileSet", "fCellTypes", "fTileSelect", "fTileGroups", "fMiniMap"}

    ' Title-bar menu hooks, kept here so they aren't garbage collected.
    Private ReadOnly SystemMenus As New Dictionary(Of Form, ToolPaletteSystemMenu)

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
        DetachSystemMenu(form)
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
        ' TopMost is user-controllable per window (title-bar menu) or for all
        ' palettes (title-bar menu / View → Tool Windows Always On Top).
        ' Stored in Mapper3.ini; defaults to True so first-run matches the
        ' previous hardcoded behavior.
        form.TopMost = GetToolWindowAlwaysOnTop(form)
        form.ShowInTaskbar = False
        form.Icon = My.Resources.lists
        form.Tag = "ToolPalette"
        RegisterToolForm(form)
        AttachSystemMenu(form)

        ' Save the position as soon as the user finishes moving the window
        ' (ResizeEnd fires at the end of a move as well as a resize), so it
        ' survives even if the app exits without the palette being closed.
        AddHandler form.ResizeEnd, AddressOf ToolForm_ResizeEnd
    End Sub

    ''' <summary>
    ''' Saves a palette's position after the user moves or resizes it.
    ''' </summary>
    Private Sub ToolForm_ResizeEnd(sender As Object, e As EventArgs)
        Dim form As Form = TryCast(sender, Form)
        If form Is Nothing Then Return
        Dim key As String = GetToolPositionKey(form)
        If key IsNot Nothing Then SavePosition(form, key)
    End Sub

    ''' <summary>
    ''' INI key prefix under which a palette's position is stored, or Nothing
    ''' for a form that isn't one of the five palettes. Must match the names
    ''' each form passes to RestorePosition / SavePosition.
    ''' </summary>
    Private Function GetToolPositionKey(form As Form) As String
        If TypeOf form Is fTileSet Then Return KeyTileSet
        If TypeOf form Is fCellTypes Then Return KeyCellTypes
        If TypeOf form Is fTileSelect Then Return KeyTileSelect
        If TypeOf form Is fTileGroups Then Return KeyTileGroups
        If TypeOf form Is fMiniMap Then Return KeyMiniMap
        Return Nothing
    End Function

    ''' <summary>
    ''' Saves the position of every open palette. Called from fMain when the
    ''' app closes, because unowned palettes get no FormClosing at exit.
    ''' </summary>
    Public Sub SaveAllToolPositions()
        For Each f As Form In GetToolForms().ToList()
            If f Is Nothing OrElse f.IsDisposed OrElse Not f.Visible Then Continue For
            ' Minimized windows report a bogus off-screen location; skip them
            ' so the last real position stays saved.
            If f.WindowState = FormWindowState.Minimized Then Continue For
            Dim key As String = GetToolPositionKey(f)
            If key IsNot Nothing Then SavePosition(f, key)
        Next
    End Sub

    ''' <summary>
    ''' True when the INI holds a position for this key that is still on a
    ''' connected screen, i.e. RestorePosition will have placed the form.
    ''' </summary>
    Private Function HasUsableSavedPosition(key As String, form As Form) As Boolean
        Dim left As Integer = ReadIniInteger($"{key}Left", Integer.MinValue)
        Dim top As Integer = ReadIniInteger($"{key}Top", Integer.MinValue)
        If left = Integer.MinValue OrElse top = Integer.MinValue Then Return False
        Return IsFormAccessibleAtLocation(New Point(left, top), form.Size)
    End Function

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
    ''' Reads a palette's own preference (e.g. Tile Selection's zoom) from the
    ''' [Window] section of Mapper3.ini, or defaultValue when it isn't set.
    ''' </summary>
    Public Function ReadToolSetting(key As String, defaultValue As String) As String
        Return ReadIniValue(key, defaultValue)
    End Function

    ''' <summary>
    ''' Saves a palette's own preference to the [Window] section of Mapper3.ini.
    ''' </summary>
    Public Sub WriteToolSetting(key As String, value As String)
        WriteIniValue(key, value)
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

    ''' <summary>
    ''' Reads one palette's always-on-top preference. Falls back to the
    ''' all-windows preference when the palette has no setting of its own.
    ''' </summary>
    Public Function GetToolWindowAlwaysOnTop(form As Form) As Boolean
        Return ReadIniBoolean(KeyToolWindowTopMostPrefix & form.GetType().Name,
                              defaultValue:=GetToolWindowsAlwaysOnTop())
    End Function

    ''' <summary>
    ''' Sets and persists the always-on-top state of a single palette.
    ''' </summary>
    Public Sub SetToolWindowAlwaysOnTop(form As Form, value As Boolean)
        WriteIniValue(KeyToolWindowTopMostPrefix & form.GetType().Name, value.ToString())
        form.TopMost = value
    End Sub

    ''' <summary>
    ''' Sets and persists the always-on-top state of every palette, open or
    ''' not, overriding any per-window choices, and keeps fMain's View menu
    ''' item in step.
    ''' </summary>
    Public Sub SetAllToolWindowsAlwaysOnTop(value As Boolean)
        SetToolWindowsAlwaysOnTop(value)
        For Each typeName As String In ToolFormTypeNames
            WriteIniValue(KeyToolWindowTopMostPrefix & typeName, value.ToString())
        Next
        ApplyTopMostToAllToolForms(value)
        SyncMainMenuTopMostItem(value)
    End Sub

    ''' <summary>
    ''' True when every open palette is currently always on top.
    ''' </summary>
    Public Function AllOpenToolFormsTopMost() As Boolean
        For Each f As Form In GetToolForms()
            If f IsNot Nothing AndAlso Not f.IsDisposed AndAlso Not f.TopMost Then Return False
        Next
        Return True
    End Function

    ''' <summary>
    ''' Updates the check mark on fMain's View → Tool Windows Always On Top.
    ''' Setting it to its current value raises no CheckedChanged, so this
    ''' can't loop back into SetAllToolWindowsAlwaysOnTop.
    ''' </summary>
    Private Sub SyncMainMenuTopMostItem(value As Boolean)
        Dim mainForm As Form = Application.OpenForms("fMain")
        If mainForm Is Nothing Then Return
        For Each strip As MenuStrip In mainForm.Controls.OfType(Of MenuStrip)()
            For Each found As ToolStripItem In strip.Items.Find("mnuToolsTopMost", True)
                Dim menuItem As ToolStripMenuItem = TryCast(found, ToolStripMenuItem)
                If menuItem IsNot Nothing Then menuItem.Checked = value
            Next
        Next
    End Sub

    ''' <summary>
    ''' Gives a palette its title-bar right-click menu (always-on-top items
    ''' plus any window-specific ones).
    ''' </summary>
    Private Sub AttachSystemMenu(form As Form)
        If SystemMenus.ContainsKey(form) Then Return
        SystemMenus(form) = New ToolPaletteSystemMenu(form)
    End Sub

    ''' <summary>
    ''' Unhooks a palette's title-bar menu handler when the palette closes,
    ''' so the form can be garbage collected.
    ''' </summary>
    Private Sub DetachSystemMenu(form As Form)
        Dim menu As ToolPaletteSystemMenu = Nothing
        If SystemMenus.TryGetValue(form, menu) Then
            menu.Detach()
            SystemMenus.Remove(form)
        End If
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
    '''
    ''' With onlyUnplaced = True, palettes that already have a usable saved
    ''' position keep it; only the rest are moved to the default layout. The
    ''' layout still uses every open palette's size, so the gaps it leaves
    ''' for restored palettes are the same as on a first run.
    ''' </summary>
    Public Sub ArrangeToolWindowsSmart(Optional onlyUnplaced As Boolean = False)
        ' Remember where the already-placed palettes are, so they can be put
        ' back after the layout below moves everything.
        Dim keepLocations As New Dictionary(Of Form, Point)
        If onlyUnplaced Then
            For Each f As Form In GetToolForms()
                Dim key As String = GetToolPositionKey(f)
                If key IsNot Nothing AndAlso HasUsableSavedPosition(key, f) Then
                    keepLocations(f) = f.Location
                End If
            Next
            ' Every open palette was restored: nothing to arrange.
            If keepLocations.Count = GetToolForms().Count Then Return
        End If

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

        ' Put the restored palettes back, then persist the newly chosen
        ' positions so this only happens once per palette.
        For Each kvp In keepLocations
            kvp.Key.Location = kvp.Value
        Next
        For Each f As Form In {tileSet, cellTypes, tileSelect, tileGroups, miniMap}
            If f Is Nothing OrElse keepLocations.ContainsKey(f) Then Continue For
            SavePosition(f, GetToolPositionKey(f))
        Next
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

''' <summary>
''' The menu shown when the user right-clicks a tool palette's title bar (or
''' presses Alt+Space while it's focused):
'''   - "Always On Top (This Window)" toggles just this palette
'''   - "All Tool Windows Always On Top" toggles every palette
''' followed by any window-specific items from a palette that implements
''' IToolPaletteMenuExtras (e.g. Tile Selection's size and zoom options).
'''
''' This is our own menu, not the Windows system menu. Most palettes have no
''' close button, and Windows gives such windows no system menu, so items
''' added to it never showed there; and the system menu would also offer
''' Close, which the palettes shouldn't have. Subclasses the form's window
''' with a NativeWindow so the five palette forms need no code of their own.
''' </summary>
Friend Class ToolPaletteSystemMenu
    Inherits NativeWindow

    Private Const WM_NCRBUTTONDOWN As Integer = &HA4
    Private Const WM_NCRBUTTONUP As Integer = &HA5
    Private Const WM_SYSCOMMAND As Integer = &H112
    Private Const HTCAPTION As Integer = 2
    Private Const SC_KEYMENU As Integer = &HF100

    ''' <summary>The tool palette whose title bar this menu belongs to.</summary>
    Private ReadOnly palette As Form
    ''' <summary>The menu, built on first use and reused after that.</summary>
    Private menu As ContextMenuStrip = Nothing
    ''' <summary>The two always-on-top items, re-checked each time the menu opens.</summary>
    Private thisWindowItem As ToolStripMenuItem
    Private allWindowsItem As ToolStripMenuItem
    ''' <summary>The palette's own items, with their IToolPaletteMenuExtras index in Tag.</summary>
    Private ReadOnly extraMenuItems As New List(Of ToolStripMenuItem)

    ''' <summary>
    ''' Hooks the palette's window. Safe to call before the window handle
    ''' exists; the hook is applied when it's created.
    ''' </summary>
    Public Sub New(form As Form)
        palette = form
        ' Changing some form properties (e.g. ShowInTaskbar) recreates the
        ' window handle, which drops the subclass.
        AddHandler palette.HandleCreated, AddressOf Palette_HandleCreated
        If palette.IsHandleCreated Then Attach()
    End Sub

    ''' <summary>
    ''' Re-hooks the palette after its window handle is (re)created.
    ''' </summary>
    Private Sub Palette_HandleCreated(sender As Object, e As EventArgs)
        Attach()
    End Sub

    ''' <summary>
    ''' Subclasses the palette's current window handle.
    ''' </summary>
    Private Sub Attach()
        ' Drop any hook on an old handle before taking the new one.
        If Handle <> IntPtr.Zero Then ReleaseHandle()
        AssignHandle(palette.Handle)
    End Sub

    ''' <summary>
    ''' Stops listening for handle changes, removes the window hook and frees
    ''' the menu.
    ''' </summary>
    Public Sub Detach()
        RemoveHandler palette.HandleCreated, AddressOf Palette_HandleCreated
        If Handle <> IntPtr.Zero Then ReleaseHandle()
        If menu IsNot Nothing Then
            menu.Dispose()
            menu = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Builds the menu: the two always-on-top items, then the palette's own
    ''' items (if any) after a separator.
    ''' </summary>
    Private Sub BuildMenu()
        menu = New ContextMenuStrip()

        thisWindowItem = New ToolStripMenuItem("Always On Top (This Window)")
        AddHandler thisWindowItem.Click,
            Sub() ToolPaletteHelper.SetToolWindowAlwaysOnTop(palette, Not palette.TopMost)
        allWindowsItem = New ToolStripMenuItem("All Tool Windows Always On Top")
        AddHandler allWindowsItem.Click,
            Sub() ToolPaletteHelper.SetAllToolWindowsAlwaysOnTop(Not ToolPaletteHelper.AllOpenToolFormsTopMost())
        menu.Items.Add(thisWindowItem)
        menu.Items.Add(allWindowsItem)

        Dim extras As IToolPaletteMenuExtras = TryCast(palette, IToolPaletteMenuExtras)
        Dim texts As IList(Of String) = If(extras IsNot Nothing, extras.GetExtraMenuItems(), Nothing)
        If texts IsNot Nothing AndAlso texts.Count > 0 Then
            menu.Items.Add(New ToolStripSeparator())
            For i As Integer = 0 To texts.Count - 1
                If texts(i) = "-" Then
                    menu.Items.Add(New ToolStripSeparator())
                    Continue For
                End If
                Dim item As New ToolStripMenuItem(texts(i)) With {.Tag = i}
                AddHandler item.Click,
                    Sub(sender As Object, e As EventArgs)
                        extras.OnExtraMenuItem(CInt(DirectCast(sender, ToolStripItem).Tag))
                    End Sub
                menu.Items.Add(item)
                extraMenuItems.Add(item)
            Next
        End If

        ' Refresh the check marks each time the menu opens, so they reflect
        ' changes made from other palettes or the View menu.
        AddHandler menu.Opening,
            Sub()
                thisWindowItem.Checked = palette.TopMost
                allWindowsItem.Checked = ToolPaletteHelper.AllOpenToolFormsTopMost()
                If extras IsNot Nothing Then
                    For Each item In extraMenuItems
                        item.Checked = extras.IsExtraMenuItemChecked(CInt(item.Tag))
                    Next
                End If
            End Sub
    End Sub

    ''' <summary>
    ''' Opens the menu at the given screen position.
    ''' </summary>
    Private Sub ShowMenu(screenPos As Point)
        If menu Is Nothing Then BuildMenu()
        menu.Show(screenPos)
    End Sub

    ''' <summary>
    ''' Sees the palette's messages before the form does. A right-click on the
    ''' title bar, or Alt+Space, opens our menu instead of the Windows one;
    ''' everything else is passed on to the form unchanged.
    ''' </summary>
    Protected Overrides Sub WndProc(ByRef m As Message)
        Select Case m.Msg
            Case WM_NCRBUTTONDOWN
                ' Swallow the press on the title bar so Windows doesn't start
                ' its own title-bar menu handling.
                If m.WParam.ToInt32() = HTCAPTION Then Return

            Case WM_NCRBUTTONUP
                ' The release on the title bar: show our menu at the cursor.
                ' Not passing it on also stops Windows opening its system menu
                ' (which, where it exists, includes Close).
                If m.WParam.ToInt32() = HTCAPTION Then
                    ShowMenu(Cursor.Position)
                    Return
                End If

            Case WM_SYSCOMMAND
                ' Alt+Space arrives as SC_KEYMENU with a space in lParam. Show
                ' our menu under the title bar's left end instead.
                ' (The low four bits of wParam are used by Windows internally.)
                If (m.WParam.ToInt32() And &HFFF0) = SC_KEYMENU AndAlso m.LParam.ToInt32() = AscW(" "c) Then
                    ShowMenu(palette.PointToScreen(Point.Empty))
                    Return
                End If
        End Select

        MyBase.WndProc(m)
    End Sub

End Class

''' <summary>
''' Implemented by a tool palette that wants its own items in its title-bar
''' menu, below the always-on-top items. Items are identified by their index
''' in the list GetExtraMenuItems returns.
''' </summary>
Public Interface IToolPaletteMenuExtras

    ''' <summary>
    ''' The item texts, in menu order. "-" adds a separator. Read once each
    ''' time the window handle is created, so the list should not change.
    ''' </summary>
    Function GetExtraMenuItems() As IList(Of String)

    ''' <summary>Whether the item at this index shows a check mark.</summary>
    Function IsExtraMenuItemChecked(index As Integer) As Boolean

    ''' <summary>Called when the user picks the item at this index.</summary>
    Sub OnExtraMenuItem(index As Integer)

End Interface
