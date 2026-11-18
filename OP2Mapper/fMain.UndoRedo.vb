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

' fMain.UndoRedo.vb
' Partial class of fMain (split out of fMain.vb).
' Undo/redo system - command stacks, execution wrapper, dirty-flag and title refresh.
Partial Public Class fMain

#Region "Undo/Redo System"



    ''' <summary>
    ''' Maximum number of undo steps kept. Oldest steps are dropped beyond this.
    ''' </summary>
    Private Const MaxUndoSteps As Integer = 500

    ''' <summary>
    ''' Commands collected during the current drag stroke (Nothing when no
    ''' stroke is open). Committed as one undo step by EndEditStroke.
    ''' </summary>
    Private currentStroke As List(Of Command) = Nothing

    ''' <summary>
    ''' The undo-stack top at the time the map was last saved/loaded (Nothing =
    ''' empty stack). Undo/Redo compare against this so returning to the saved
    ''' state clears the "*". Set to NoSavePoint when no history state matches
    ''' the file on disk (e.g. a freshly created, never-saved map).
    ''' </summary>
    Private savePoint As Object = Nothing
    Private Shared ReadOnly NoSavePoint As New Object()

    ''' <summary>
    ''' Executes a command, marks the map dirty and pushes the command onto the undo
    ''' stack (clearing the redo stack). During undo/redo replay it only executes.
    ''' While a drag stroke is open the command is collected into the stroke instead.
    ''' </summary>
    Private Sub ExecuteCommand(command As Command)
        ' If we're in the middle of an undo/redo operation, don't add to the stack
        If isUndoRedo Then
            command.Execute()
            SetMapDirty(True)
            ScheduleMiniMapRefresh()
            ScheduleEnclosedHighlightRefresh()
            Return
        End If

        ' Execute first: if it throws, nothing is recorded and the dirty flag
        ' isn't touched for a change that didn't happen.
        command.Execute()

        ' Any command mutates the map, so the document is now dirty.
        SetMapDirty(True)
        ' And the mini-map thumbnail needs to refresh — but debounced so a
        ' drag-edit doesn't re-render the full map at every step. Same for the
        ' enclosed-areas highlight, if it's on.
        ScheduleMiniMapRefresh()
        ScheduleEnclosedHighlightRefresh()

        If currentStroke IsNot Nothing Then
            ' Part of a drag stroke — committed as one step in EndEditStroke.
            currentStroke.Add(command)
            Return
        End If

        PushUndo(command)

        Debug.WriteLine($"Command executed: {command.Description}")
    End Sub

    ''' <summary>
    ''' Pushes a new step onto the undo stack, clears redo, and trims the oldest
    ''' steps beyond MaxUndoSteps.
    ''' </summary>
    Private Sub PushUndo(command As Command)
        undoStack.Push(command)
        redoStack.Clear()

        If undoStack.Count > MaxUndoSteps Then
            ' Stack(Of T).ToArray is top-first; keep the newest MaxUndoSteps.
            Dim newestFirst() As Command = undoStack.ToArray()
            undoStack.Clear()
            For i As Integer = MaxUndoSteps - 1 To 0 Step -1
                undoStack.Push(newestFirst(i))
            Next
        End If

        UpdateUndoRedoState()
    End Sub

    ''' <summary>
    ''' Starts collecting edits into a single undo step (call on drag start).
    ''' </summary>
    Private Sub BeginEditStroke(Optional description As String = Nothing)
        EndEditStroke()
        currentStroke = New List(Of Command)()
        currentStrokeDescription = description
    End Sub

    ''' <summary>
    ''' Undo-menu text for the open stroke when it holds more than one command
    ''' (e.g. "Terrain Brush: Sand"). Nothing = the generic "Paint (N tiles)".
    ''' </summary>
    Private currentStrokeDescription As String = Nothing

    ''' <summary>
    ''' Commits the open drag stroke (if any) as one undo step.
    ''' </summary>
    Private Sub EndEditStroke()
        If currentStroke Is Nothing Then Return
        Dim stroke As List(Of Command) = currentStroke
        currentStroke = Nothing

        If stroke.Count = 0 Then Return
        If stroke.Count = 1 Then
            PushUndo(stroke(0))
        Else
            PushUndo(New CompositeCommand(stroke,
                     If(currentStrokeDescription, $"Paint ({stroke.Count} tiles)")))
        End If
    End Sub

    ''' <summary>
    ''' Ends an edit drag: clears the drag state and commits its undo step.
    ''' </summary>
    Private Sub EndEditDrag()
        isEditDragging = False
        lastEditTileX = -1
        lastEditTileY = -1
        EndEditStroke()
        EndTerrainStroke()
    End Sub

    ''' <summary>
    ''' Records the current history position as matching the file on disk.
    ''' </summary>
    Private Sub MarkSavePoint()
        EndEditStroke()
        savePoint = If(undoStack.Count > 0, undoStack.Peek(), Nothing)
        SetMapDirty(False)
    End Sub

    ''' <summary>
    ''' Marks the map as unsaved with no history state matching disk — used for
    ''' a newly created map, which has never been written.
    ''' </summary>
    Friend Sub MarkNewUnsavedMap()
        savePoint = NoSavePoint
        SetMapDirty(True)
    End Sub

    ''' <summary>
    ''' Sets the dirty flag from whether the current history position is the save point.
    ''' </summary>
    Private Sub RefreshDirtyFromSavePoint()
        Dim top As Object = If(undoStack.Count > 0, undoStack.Peek(), Nothing)
        SetMapDirty(top IsNot savePoint)
    End Sub

    ''' <summary>
    ''' Updates the dirty flag and refreshes the window title to show / hide
    ''' the leading asterisk.
    ''' </summary>
    Private Sub SetMapDirty(dirty As Boolean)
        If mapIsDirty = dirty Then Return
        mapIsDirty = dirty
        RefreshWindowTitle()
    End Sub

    ''' <summary>
    ''' Renders the window title from current state. Shows "* Name - file"
    ''' when the map has unsaved changes.
    ''' </summary>
    Private Sub RefreshWindowTitle()
        If currentMap Is Nothing OrElse String.IsNullOrEmpty(currentMapFile) Then
            Text = GlobalVariables.ApplicationName
        Else
            Dim prefix As String = If(mapIsDirty, "* ", "")
            Text = prefix & GlobalVariables.ApplicationName & " - " & currentMapFile
        End If
    End Sub

    ''' <summary>
    ''' If the map has unsaved changes, prompts Save / Discard / Cancel.
    ''' Returns False if the caller should abort the pending action.
    ''' </summary>
    Friend Function ConfirmDiscardUnsavedChanges() As Boolean
        If Not mapIsDirty OrElse currentMap Is Nothing Then
            Return True
        End If

        Dim result As DialogResult = MessageBox.Show(
            "The current map has unsaved changes. Save before continuing?",
            "Unsaved Changes",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning)

        Select Case result
            Case DialogResult.Yes
                SaveMap_Click(Nothing, EventArgs.Empty)
                ' If the user cancelled the save dialog, the map is still dirty — abort.
                Return Not mapIsDirty
            Case DialogResult.No
                Return True
            Case Else
                Return False
        End Select
    End Function

    ''' <summary>
    ''' Undoes the most recent command, moves it to the redo stack and refreshes the
    ''' affected tiles (or the whole panel for multi-tile commands).
    ''' </summary>
    Private Sub Undo()
        ' Commit any stroke still open (e.g. Ctrl+Z pressed mid-drag).
        EndEditStroke()

        If undoStack.Count = 0 Then
            Return
        End If

        ' Get the command to undo
        Dim command As Command = undoStack.Pop()

        ' Set flag to prevent adding undo operations to the stack. Reset in a
        ' Finally so a throwing command can't leave the flag stuck True, which
        ' would silently stop all later edits from being pushed to the stack.
        isUndoRedo = True
        Try
            ' Undo the command
            command.Undo()
        Finally
            isUndoRedo = False
        End Try

        ' Add to redo stack
        redoStack.Push(command)

        ' Dirty unless this lands back on the saved state.
        RefreshDirtyFromSavePoint()
        ScheduleMiniMapRefresh()
        ScheduleEnclosedHighlightRefresh()

        ' Update UI
        UpdateUndoRedoState()

        Debug.WriteLine($"Undo: {command.Description}")

        ' Refresh the display for the affected tile
        If TypeOf command Is ChangeCellTypeCommand Then
            Dim cellTypeCmd = DirectCast(command, ChangeCellTypeCommand)
            UpdateSingleTile(cellTypeCmd.X, cellTypeCmd.Y)
            UpdatePropertyControls()
        ElseIf TypeOf command Is ChangeTileMappingCommand Then
            Dim tileCmd = DirectCast(command, ChangeTileMappingCommand)
            UpdateSingleTile(tileCmd.X, tileCmd.Y)
            UpdatePropertyControls()
        Else
            ' Multi-tile commands (PlaceTileGroup, PasteTileMappings) — repaint
            ' the whole panel since we don't know the affected region here.
            UpdatePropertyControls()
            pnlMap.Invalidate()
        End If
    End Sub

    ''' <summary>
    ''' Re-executes the most recently undone command, moves it back to the undo stack
    ''' and refreshes the affected tiles (or the whole panel for multi-tile commands).
    ''' </summary>
    Private Sub Redo()
        EndEditStroke()

        If redoStack.Count = 0 Then
            Return
        End If

        ' Get the command to redo
        Dim command As Command = redoStack.Pop()

        ' Set flag to prevent adding redo operations to the stack. Reset in a
        ' Finally so a throwing command can't leave the flag stuck True, which
        ' would silently stop all later edits from being pushed to the stack.
        isUndoRedo = True
        Try
            ' Execute the command again
            command.Execute()
        Finally
            isUndoRedo = False
        End Try

        ' Add back to undo stack
        undoStack.Push(command)

        ' Dirty unless this lands back on the saved state.
        RefreshDirtyFromSavePoint()
        ScheduleMiniMapRefresh()
        ScheduleEnclosedHighlightRefresh()

        ' Update UI
        UpdateUndoRedoState()

        Debug.WriteLine($"Redo: {command.Description}")

        ' Refresh the display for the affected tile
        If TypeOf command Is ChangeCellTypeCommand Then
            Dim cellTypeCmd = DirectCast(command, ChangeCellTypeCommand)
            UpdateSingleTile(cellTypeCmd.X, cellTypeCmd.Y)
            UpdatePropertyControls()
        ElseIf TypeOf command Is ChangeTileMappingCommand Then
            Dim tileCmd = DirectCast(command, ChangeTileMappingCommand)
            UpdateSingleTile(tileCmd.X, tileCmd.Y)
            UpdatePropertyControls()
        Else
            ' Multi-tile commands repaint the whole panel — we don't know the
            ' affected region here.
            UpdatePropertyControls()
            pnlMap.Invalidate()
        End If
    End Sub

    ''' <summary>
    ''' Updates the Undo/Redo menu items' enabled state and text to show the
    ''' description of the next command on each stack.
    ''' </summary>
    Private Sub UpdateUndoRedoState()
        ' Update UI elements to reflect undo/redo state
        Dim mnuEdit As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuEdit", False)(0), ToolStripMenuItem)

        Dim mnuUndo As ToolStripMenuItem = DirectCast(mnuEdit.DropDownItems.Find("mnuUndo", False)(0), ToolStripMenuItem)
        Dim mnuRedo As ToolStripMenuItem = DirectCast(mnuEdit.DropDownItems.Find("mnuRedo", False)(0), ToolStripMenuItem)

        ' Update undo menu item
        If undoStack.Count > 0 Then
            Dim lastCommand As Command = undoStack.Peek()
            mnuUndo.Text = $"Undo {lastCommand.Description}"
            mnuUndo.Enabled = True
        Else
            mnuUndo.Text = "Undo"
            mnuUndo.Enabled = False
        End If

        ' Update redo menu item
        If redoStack.Count > 0 Then
            Dim nextCommand As Command = redoStack.Peek()
            mnuRedo.Text = $"Redo {nextCommand.Description}"
            mnuRedo.Enabled = True
        Else
            mnuRedo.Text = "Redo"
            mnuRedo.Enabled = False
        End If
    End Sub

    ''' <summary>
    ''' Clears both undo and redo stacks. Call when loading a new map so history
    ''' from the previous map can't be replayed.
    ''' </summary>
    Private Sub ClearUndoRedoStacks()
        currentStroke = Nothing
        undoStack.Clear()
        redoStack.Clear()
        ' Empty history now matches the file on disk.
        savePoint = Nothing
        UpdateUndoRedoState()
    End Sub



    ''' <summary>
    ''' Central keyboard shortcut dispatcher (ESC cancels modes, Ctrl+C/V copy/paste,
    ''' Ctrl+Z/Y undo/redo, view toggles, Ctrl+O/S open/save). Returns True when handled.
    ''' </summary>
    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        '===================================================
        '== MAP-DEPENDENT SHORTCUTS                       ==
        '===================================================
        If currentMap IsNot Nothing Then

            ' Draw Cliff: Enter places, Backspace removes a point, Esc clears.
            If HandleCliffKey(keyData) Then Return True

            '===================================================
            '== TILE GROUP OPERATION                          ==
            '===================================================

            ' ESC key to cancel tile group placement mode
            If keyData = Keys.Escape AndAlso TileGroupPlacementMode Then
                ExitTileGroupPlacementMode()
                Debug.WriteLine("Tile group placement canceled with ESC key")
                pnlMap.Refresh()
                Return True
            End If

            '===================================================
            '== COPY/PASTE OPERATIONS                         ==
            '===================================================

            ' ESC key to cancel copy mode during selection stages (0 or 1)
            If keyData = Keys.Escape AndAlso
                GlobalVariables.IsCopyMode AndAlso
                GlobalVariables.CopyStage < 2 Then

                ExitCopyMode()
                Debug.WriteLine("Copy mode canceled with ESC key")
                Return True
            End If

            ' ESC key to cancel paste mode
            If keyData = Keys.Escape AndAlso isPasteMode Then
                ExitPasteMode()
                Debug.WriteLine("Paste mode canceled with ESC key")
                ' Force immediate redraw to remove the white selection box
                pnlMap.Refresh()
                Return True
            End If

            ' Only process edit-related shortcuts if edit mode is enabled
            If EditModeEnabled Then
                ' Ctrl+C for Copy
                If keyData = (Keys.Control Or Keys.C) Then
                    ' Only if copy menu item is enabled
                    Dim mnuEdit As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuEdit", False)(0), ToolStripMenuItem)
                    Dim mnuCopy As ToolStripMenuItem = DirectCast(mnuEdit.DropDownItems.Find("mnuCopy", False)(0), ToolStripMenuItem)

                    If mnuCopy IsNot Nothing AndAlso mnuCopy.Enabled Then
                        Debug.WriteLine("Ctrl+C pressed - Starting copy operation")
                        ' Same path as Edit → Copy, so the render-mode check and
                        ' leaving paste/tile-group mode apply to the shortcut too.
                        Copy_Click(Nothing, EventArgs.Empty)
                        Return True
                    End If
                End If

                ' Ctrl+V for Paste
                If keyData = (Keys.Control Or Keys.V) Then
                    ' Only if paste menu item is enabled
                    Dim mnuEdit As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuEdit", False)(0), ToolStripMenuItem)
                    Dim mnuPaste As ToolStripMenuItem = DirectCast(mnuEdit.DropDownItems.Find("mnuPaste", False)(0), ToolStripMenuItem)

                    If mnuPaste IsNot Nothing AndAlso mnuPaste.Enabled Then
                        Debug.WriteLine("Ctrl+V pressed - Executing paste operation")
                        Paste_Click(Nothing, EventArgs.Empty)
                        Return True
                    End If
                End If

                ' Ctrl+B to enter tile-group placement mode using whatever's
                ' currently selected in the Tile Groups window. Silently no-op
                ' if no tile group is loaded (vs the button which pops a dialog).
                If keyData = (Keys.Control Or Keys.B) Then
                    If GlobalVariables.TileGroupTileMappings IsNot Nothing AndAlso
                       GlobalVariables.TileGroupWidth > 0 AndAlso
                       GlobalVariables.TileGroupHeight > 0 Then
                        Debug.WriteLine("Ctrl+B pressed - Initiating tile-group placement")
                        InitializeTileGroupPlacementMode()
                    Else
                        Debug.WriteLine("Ctrl+B pressed but no tile group is loaded — ignoring")
                    End If
                    Return True
                End If
            End If

            '===================================================
            '== UNDO/REDO OPERATIONS                         ==
            '===================================================

            ' Ctrl+Z for Undo
            If keyData = (Keys.Control Or Keys.Z) Then
                Undo()
                Return True
            End If

            ' Ctrl+Y for Redo
            If keyData = (Keys.Control Or Keys.Y) Then
                Redo()
                Return True
            End If

            '===================================================
            '== VIEW OPERATIONS                              ==
            '===================================================

            ' Ctrl+G for Toggle Grid
            If keyData = (Keys.Control Or Keys.G) Then
                ToggleGrid()
                Return True
            End If

            ' Ctrl+L for Toggle Large Grid
            If keyData = (Keys.Control Or Keys.L) Then
                ToggleLargeGrid()
                Return True
            End If

            ' Ctrl+D for Toggle Coordinates
            If keyData = (Keys.Control Or Keys.D) Then
                ToggleCoordinates()
                Return True
            End If

            ' Ctrl+R for Reset Zoom
            If keyData = (Keys.Control Or Keys.R) Then
                ResetZoom_Click(Nothing, EventArgs.Empty)
                Return True
            End If
        End If

        '===================================================
        '== GLOBAL OPERATIONS (WORK WITHOUT MAP)          ==
        '===================================================

        ' Ctrl+O for Open Map
        If keyData = (Keys.Control Or Keys.O) Then
            OpenMap_Click(Nothing, EventArgs.Empty)
            Return True
        End If

        ' Ctrl+S for Save Map (if map is loaded)
        If keyData = (Keys.Control Or Keys.S) AndAlso currentMap IsNot Nothing Then
            SaveMap_Click(Nothing, EventArgs.Empty)
            Return True
        End If

        ' Let the base class handle the rest
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function



#End Region

End Class
