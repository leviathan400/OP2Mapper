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
    ''' Executes a command, marks the map dirty and pushes the command onto the undo
    ''' stack (clearing the redo stack). During undo/redo replay it only executes.
    ''' </summary>
    Private Sub ExecuteCommand(command As Command)
        ' Any command mutates the map, so the document is now dirty.
        SetMapDirty(True)
        ' And the mini-map thumbnail needs to refresh — but debounced so a
        ' drag-edit doesn't re-render the full map at every step.
        ScheduleMiniMapRefresh()

        ' If we're in the middle of an undo/redo operation, don't add to the stack
        If isUndoRedo Then
            command.Execute()
            Return
        End If

        ' Execute the command
        command.Execute()

        ' Add to undo stack
        undoStack.Push(command)

        ' Clear redo stack when a new command is executed
        redoStack.Clear()

        ' Update UI to reflect undo/redo state
        UpdateUndoRedoState()

        Debug.WriteLine($"Command executed: {command.Description}")
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
    Private Function ConfirmDiscardUnsavedChanges() As Boolean
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

        ' Undoing a command moves us away from disk state.
        SetMapDirty(True)
        ScheduleMiniMapRefresh()

        ' Add to redo stack
        redoStack.Push(command)

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

        ' Redoing a command moves us away from disk state.
        SetMapDirty(True)
        ScheduleMiniMapRefresh()

        ' Add back to undo stack
        undoStack.Push(command)

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
        undoStack.Clear()
        redoStack.Clear()
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
                        InitializeCopyMode()

                        ' Ensure UI is updated
                        picSelectedTool.Show()
                        picSelectedTool.Refresh()
                        UpdateToolAndPlacementLabels()
                        Application.DoEvents()  ' Force immediate UI update

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
