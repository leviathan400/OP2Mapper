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

' fMain.Editing.vb
' Partial class of fMain (split out of fMain.vb).
' Editing tools - edit mode toggle, tile/cell-type painting entry points and tool state.
Partial Public Class fMain

#Region "Editing Tools"

    ''' <summary>
    ''' Button handler that toggles edit mode on or off.
    ''' </summary>
    Private Sub EditMode_Click(sender As Object, e As EventArgs)
        ToggleEditMode()
    End Sub


    ''' <summary>
    ''' Toggles edit mode on or off, handles UI updates and form management
    ''' </summary>
    Private Sub ToggleEditMode()
        If EditModeEnabled = False Then
            ' Enable edit mode
            EditModeEnabled = True

            ' Reset tool selection state
            GlobalVariables.ActiveCellType = CellType.FastPassible1
            GlobalVariables.CellTypeToolActive = False
            GlobalVariables.ActiveTileMappingIndex = -1
            GlobalVariables.SelectedTileImage = Nothing
            GlobalVariables.SelectedTilesetName = ""
            GlobalVariables.SelectedTileIndex = -1
            GlobalVariables.SelectedTilesetIndex = -1

            ' Reset copy/paste state
            GlobalVariables.IsCopyMode = False
            GlobalVariables.CopyStage = 0

            ' Reset tile group
            GlobalVariables.TileGroupPlacementMode = False

            ' Enable copy menu item
            Dim mnuEdit As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuEdit", False)(0), ToolStripMenuItem)
            Dim mnuCopy As ToolStripMenuItem = DirectCast(mnuEdit.DropDownItems.Find("mnuCopy", False)(0), ToolStripMenuItem)
            If mnuCopy IsNot Nothing Then
                mnuCopy.Enabled = True
            End If

            ' Only enable paste if we have copied data
            Dim mnuPaste As ToolStripMenuItem = DirectCast(mnuEdit.DropDownItems.Find("mnuPaste", False)(0), ToolStripMenuItem)
            If mnuPaste IsNot Nothing Then
                mnuPaste.Enabled = (GlobalVariables.CopiedTileMappings IsNot Nothing)
            End If

            ' --------------------------
            ' Create and show tool forms
            ' --------------------------

            Me.tileSetForm = New fTileSet()        ' Tool 1
            Me.tileSetForm.Show(Me)                  ' Tileset, select tiles from bmp well's


            cellTypesForm = New fCellTypes()    ' Tool 2
            cellTypesForm.Show()                ' Cell type selection

            tileSelectForm = New fTileSelect()  ' Tool 3
            tileSelectForm.Show()               ' Copy/Paste viewport

            tileGroupsForm = New fTileGroups()  ' Tool 4
            tileGroupsForm.Show()               ' Tile Groups

            miniMapForm = New fMiniMap()        ' Tool 5
            miniMapForm.Show()                  ' Mini Map
            AddHandler miniMapForm.JumpToTile, AddressOf MiniMap_JumpToTile
            ' On resize-end, re-render at the new size so the thumbnail is
            ' rendered at the optimal pixels-per-tile (no upscaling blur).
            AddHandler miniMapForm.ResizeEnd, Sub(s, ev) RefreshMiniMap()
            ' Render an initial thumbnail + viewport rect for the current map.
            RefreshMiniMap()

            ' --------------------------

            ' First-run smart layout: if the INI has no saved positions for any
            ' tool palette, pick sensible defaults based on screen count/size
            ' and save them. Subsequent runs restore per-form via RestorePosition
            ' (already wired in each form's Load).
            If Not ToolPaletteHelper.HasAnySavedToolPositions() Then
                ToolPaletteHelper.ArrangeToolWindowsSmart()
            End If

            picSelectedTool.Show()
            Debug.WriteLine("Edit Mode Enabled")

        Else
            ' If we're in copy mode, exit it when disabling edit mode
            If GlobalVariables.IsCopyMode Then
                ExitCopyMode()
                Debug.WriteLine("Copy mode exited during edit mode toggle")
            End If

            ' Disable edit mode
            EditModeEnabled = False
            isPasteMode = False

            ' Disable edit mode - close the forms
            If tileSetForm IsNot Nothing Then tileSetForm.Close()       ' Tool 1
            If cellTypesForm IsNot Nothing Then cellTypesForm.Close()   ' Tool 2
            If tileSelectForm IsNot Nothing Then tileSelectForm.Close() ' Tool 3
            If tileGroupsForm IsNot Nothing Then tileGroupsForm.Close() ' Tool 4
            If miniMapForm IsNot Nothing Then miniMapForm.Close()       ' Tool 5

            '' Create a list to store forms that need to be closed
            'Dim formsToClose As New List(Of Form)
            '    ' Find forms to close without modifying the collection yet
            '    For Each ToolForm As Form In Application.OpenForms
            '        If TypeOf ToolForm Is fCellTypes OrElse TypeOf ToolForm Is fTileSet OrElse TypeOf ToolForm Is fTileSelect _
            '                            OrElse TypeOf ToolForm Is fTileGroups OrElse TypeOf ToolForm Is fMiniMap Then
            '            formsToClose.Add(ToolForm)
            '        End If
            '    Next

            '    ' Now close the forms
            '    For Each form As Form In formsToClose
            '        form.Close()
            '    Next

            ' Clear the selected tool image
            If picSelectedTool IsNot Nothing Then
                    If picSelectedTool.Image IsNot Nothing Then
                        picSelectedTool.Image.Dispose()
                        picSelectedTool.Image = Nothing
                    End If
                    picSelectedTool.BackColor = System.Drawing.Color.Black
                End If

                ' Disable copy/paste menu items
                Dim mnuEdit As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuEdit", False)(0), ToolStripMenuItem)
                Dim mnuCopy As ToolStripMenuItem = DirectCast(mnuEdit.DropDownItems.Find("mnuCopy", False)(0), ToolStripMenuItem)
                If mnuCopy IsNot Nothing Then
                    mnuCopy.Enabled = False
                End If

                Dim mnuPaste As ToolStripMenuItem = DirectCast(mnuEdit.DropDownItems.Find("mnuPaste", False)(0), ToolStripMenuItem)
                If mnuPaste IsNot Nothing Then
                    mnuPaste.Enabled = False
                End If

                ' Reset copy/paste state
                GlobalVariables.IsCopyMode = False
                GlobalVariables.CopyStage = 0

                picSelectedTool.Hide()
                Debug.WriteLine("Edit Mode Disabled")
            End If

            ' Update the button text and UI
            UpdateEditModeButtonText()
        UpdateToolAndPlacementLabels()
    End Sub

    ''' <summary>
    ''' Updates the edit mode button text/color and the status bar to match the
    ''' current edit mode state.
    ''' </summary>
    Private Sub UpdateEditModeButtonText()
        ' Update the button text based on the current state
        If GlobalVariables.EditModeEnabled Then
            btnEditMode.Text = "Disable Edit Mode"
            btnEditMode.BackColor = System.Drawing.Color.LightBlue
        Else
            btnEditMode.Text = "Enable Edit Mode"
            btnEditMode.BackColor = SystemColors.Control
        End If

        ' Update status strip to show the current edit mode
        UpdateStatusForEditMode()
    End Sub

    ''' <summary>
    ''' Refreshes the current-tool / current-placement labels and the tool preview box
    ''' from global state. Copy mode takes precedence, then tile placement, then cell type.
    ''' </summary>
    Private Sub UpdateToolAndPlacementLabels()
        ' If edit mode is not enabled, clear the labels
        If Not EditModeEnabled Then
            lblCurrentTool.Text = "No Tool Selected"
            lblCurrentPlacement.Text = "No Placement Selected"
            lblCurrentTool.ForeColor = System.Drawing.SystemColors.GrayText
            lblCurrentPlacement.ForeColor = System.Drawing.SystemColors.GrayText
            Return
        End If

        ' Handle copy mode display first - this takes precedence
        If GlobalVariables.IsCopyMode Then
            lblCurrentTool.Text = "Current Tool: Tile Copy/Paste"
            lblCurrentTool.ForeColor = System.Drawing.Color.DarkGreen

            Select Case GlobalVariables.CopyStage
                Case 0
                    lblCurrentPlacement.Text = "Selecting start copy tile"
                    lblCurrentPlacement.ForeColor = System.Drawing.Color.Green
                Case 1
                    lblCurrentPlacement.Text = "Select end copy tile"
                    lblCurrentPlacement.ForeColor = System.Drawing.Color.DarkGreen
                Case 2
                    lblCurrentPlacement.Text = $"Region Copied: {GlobalVariables.CopiedRegionWidth}x{GlobalVariables.CopiedRegionHeight} tiles"
                    lblCurrentPlacement.ForeColor = System.Drawing.Color.DarkBlue
            End Select

            Debug.WriteLine("UpdateLabels: In copy mode - preserving tool display")
            Return ' Exit now - don't continue to code that might change the tool display
        End If

        ' Normal tool display code for when not in copy mode
        Dim tileToolActive As Boolean = (GlobalVariables.ActiveTileMappingIndex >= 0 AndAlso
                      GlobalVariables.SelectedTileImage IsNot Nothing)
        Dim cellTypeToolActive As Boolean = GlobalVariables.CellTypeToolActive

        ' If no tool is active, show default state
        If Not tileToolActive AndAlso Not cellTypeToolActive Then
            lblCurrentTool.Text = "No Tool Selected"
            lblCurrentPlacement.Text = "No Placement Selected"
            lblCurrentTool.ForeColor = System.Drawing.SystemColors.GrayText
            lblCurrentPlacement.ForeColor = System.Drawing.SystemColors.GrayText

            ' Reset the tool display box
            If picSelectedTool IsNot Nothing Then
                If picSelectedTool.Image IsNot Nothing Then
                    picSelectedTool.Image.Dispose()
                    picSelectedTool.Image = Nothing
                End If
                picSelectedTool.BackColor = System.Drawing.Color.Black
            End If

            Return
        End If

        ' Handle tile tool display
        If tileToolActive Then
            lblCurrentTool.Text = "Current Tool: Tile Placement"
            lblCurrentTool.ForeColor = System.Drawing.Color.Blue

            ' Get tileset name if available
            Dim tilesetName As String = GlobalVariables.SelectedTilesetName
            If String.IsNullOrEmpty(tilesetName) Then
                tilesetName = "Unknown"
            End If

            'lblCurrentPlacement.Text = $"Placing: {tilesetName} (Index: {GlobalVariables.SelectedTileIndex})"
            'lblCurrentPlacement.Text = $"Placing: {tilesetName} (Index: {GlobalVariables.SelectedTileIndex}, Mapping: {GlobalVariables.ActiveTileMappingIndex})"
            lblCurrentPlacement.Text = $"Placing: Tile: {GlobalVariables.ActiveTileMappingIndex}"

            lblCurrentPlacement.ForeColor = System.Drawing.Color.DarkBlue

            ' THIS IS THE FIX: Update the picSelectedTool with the selected tile image
            If picSelectedTool IsNot Nothing Then
                ' Clear any previous image
                If picSelectedTool.Image IsNot Nothing Then
                    picSelectedTool.Image.Dispose()
                    picSelectedTool.Image = Nothing
                End If

                ' Set the new image if available
                If GlobalVariables.SelectedTileImage IsNot Nothing Then
                    ' Create a copy of the image (to avoid cross-thread issues)
                    picSelectedTool.Image = New Bitmap(GlobalVariables.SelectedTileImage)
                    picSelectedTool.BackColor = System.Drawing.Color.Transparent
                    picSelectedTool.SizeMode = PictureBoxSizeMode.Normal
                    Debug.WriteLine("Updated picSelectedTool with tile image")
                End If
            End If

            Return
        End If

        ' Handle cell type tool display
        If cellTypeToolActive Then
            lblCurrentTool.Text = "Current Tool: Cell Type"
            lblCurrentTool.ForeColor = System.Drawing.Color.DarkRed

            lblCurrentPlacement.Text = $"Placing: {GlobalVariables.ActiveCellType}"
            lblCurrentPlacement.ForeColor = GetCellTypeColor(GlobalVariables.ActiveCellType)

            ' Clear any tile image and show a colored box for cell type
            If picSelectedTool IsNot Nothing Then
                If picSelectedTool.Image IsNot Nothing Then
                    picSelectedTool.Image.Dispose()
                    picSelectedTool.Image = Nothing
                End If

                ' Set background color to match the cell type
                picSelectedTool.BackColor = GetCellTypeColor(GlobalVariables.ActiveCellType)
            End If

            Return
        End If
    End Sub


    '''<summary>
    ''' Resets all tool-related UI elements and internal state to their default state
    ''' </summary>
    ''' <param name="keepCopyData">If True, keeps copied data available for paste operations</param>
    Private Sub ResetToolUI(Optional keepCopyData As Boolean = True)
        Debug.WriteLine("Resetting all tool UI elements")

        ' Reset label text and appearance
        lblCurrentTool.Text = "No Tool Selected"
        lblCurrentPlacement.Text = "No Placement Selected"
        lblCurrentTool.ForeColor = System.Drawing.SystemColors.GrayText
        lblCurrentPlacement.ForeColor = System.Drawing.SystemColors.GrayText

        ' Reset the tool display box
        If picSelectedTool IsNot Nothing Then
            If picSelectedTool.Image IsNot Nothing Then
                picSelectedTool.Image.Dispose()
                picSelectedTool.Image = Nothing
            End If
            picSelectedTool.BackColor = System.Drawing.Color.Black
        End If

        ' IMPORTANT: Reset the active tool state to prevent actions when no tool is shown as selected
        GlobalVariables.ActiveCellType = CellType.FastPassible1
        GlobalVariables.CellTypeToolActive = False
        GlobalVariables.ActiveTileMappingIndex = -1

        ' If selected tile image exists, dispose it
        If GlobalVariables.SelectedTileImage IsNot Nothing Then
            GlobalVariables.SelectedTileImage.Dispose()
            GlobalVariables.SelectedTileImage = Nothing
        End If

        ' Reset other placement variables
        GlobalVariables.SelectedTilesetName = ""
        GlobalVariables.SelectedTileIndex = -1
        GlobalVariables.SelectedTilesetIndex = -1

        ' If we want to discard copy data too, clear the arrays
        If Not keepCopyData Then
            GlobalVariables.CopiedTileMappings = Nothing
            GlobalVariables.CopiedRegionWidth = 0
            GlobalVariables.CopiedRegionHeight = 0

            ' Disable paste menu item 
            Try
                Dim mnuPaste As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuEdit", False)(0), ToolStripMenuItem).DropDownItems.Find("mnuPaste", False)(0)
                If mnuPaste IsNot Nothing Then
                    mnuPaste.Enabled = False
                End If
            Catch ex As Exception
                Debug.WriteLine("Error updating paste menu item: " & ex.Message)
            End Try
        End If

        ' Reset copy mode flags but preserve actual copied data if requested
        GlobalVariables.IsCopyMode = False
        GlobalVariables.CopyStage = 0

        ' Clear selection in the TileSet form
        ClearTileSetFormSelection()
    End Sub

    ''' <summary>
    ''' Clears the tile selection in the TileSet form if it is open. Uses reflection
    ''' to reset its private selectedTileIndex field rather than a public API.
    ''' </summary>
    Private Sub ClearTileSetFormSelection()
        Try
            ' Find the TileSet form if it's open
            For Each form As Form In Application.OpenForms
                If TypeOf form Is fTileSet Then
                    ' Use reflection to clear selection
                    Dim fieldInfo = form.GetType().GetField("selectedTileIndex",
                                Reflection.BindingFlags.NonPublic Or
                                Reflection.BindingFlags.Instance)
                    If fieldInfo IsNot Nothing Then
                        ' Set selectedTileIndex to -1
                        fieldInfo.SetValue(form, -1)

                        ' Invalidate the panel to update display
                        Dim pnlTileSets = TryCast(form.Controls.Find("pnlTileSets", True).FirstOrDefault(), Panel)
                        If pnlTileSets IsNot Nothing Then
                            pnlTileSets.Invalidate()
                        End If
                    End If
                    Exit For
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine($"Error clearing TileSet selection: {ex.Message}")
        End Try
    End Sub



    ''' <summary>
    ''' Returns the display color used for a cell type, grouped by category
    ''' (passable speeds, walls, tubes, cliffs, etc.).
    ''' </summary>
    Private Function GetCellTypeColor(cellType As CellType) As System.Drawing.Color
        ' Return different colors based on cell type category
        Select Case cellType
            Case CellType.FastPassible1, CellType.FastPassible2
                Return System.Drawing.Color.Green
            Case CellType.MediumPassible1, CellType.MediumPassible2
                Return System.Drawing.Color.Olive
            Case CellType.SlowPassible1, CellType.SlowPassible2
                Return System.Drawing.Color.DarkOrange
            Case CellType.Impassible1, CellType.Impassible2
                Return System.Drawing.Color.Red
            Case CellType.DozedArea, CellType.Rubble
                Return System.Drawing.Color.SaddleBrown
            Case CellType.NormalWall, CellType.MicrobeWall, CellType.LavaWall
                Return System.Drawing.Color.DarkBlue
            Case CellType.Tube0, CellType.Tube1, CellType.Tube2, CellType.Tube3, CellType.Tube4, CellType.Tube5
                Return System.Drawing.Color.Purple
            Case CellType.NorthCliffs, CellType.CliffsHighSide, CellType.CliffsLowSide
                Return System.Drawing.Color.Brown
            Case CellType.VentsAndFumaroles
                Return System.Drawing.Color.Firebrick
            Case Else ' zPad12-zPad20
                Return System.Drawing.Color.DarkGray
        End Select
    End Function

#End Region

End Class
