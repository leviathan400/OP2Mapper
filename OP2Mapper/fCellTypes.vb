Imports OP2UtilityDotNet.OP2Map

' fCellTypes.vb - Tool 2: "Cell Types" palette window.
' Lists the Outpost 2 cell types (passability, walls, tubes, etc.) and sets
' the active cell type for painting onto the map.
Public Class fCellTypes

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

    ' Reference to main form
    Private mainForm As fMain = Nothing

    ''' <summary>
    ''' Form load event handler - restores position and populates the cell types list
    ''' </summary>
    Private Sub fCellTypes_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.StartPosition = FormStartPosition.Manual
        ToolPaletteHelper.ConfigureAsToolPalette(Me)
        ToolPaletteHelper.RestorePosition(Me, "CellTypes")

        ' Get reference to main form
        mainForm = TryCast(Application.OpenForms("fMain"), fMain)

        ' Set form properties. Don't override Location here — RestorePosition
        ' (above) handles the saved location, and ArrangeToolWindowsSmart
        ' handles first-run placement. Hardcoding Location here was clobbering
        ' both, which is why this window wouldn't remember its position.
        Me.Text = "Cell Types"
        Me.Height = 484

        cmbCellTypeList.Height = 420

        ' Populate cell types list
        PopulateCellTypesList()
    End Sub

    ''' <summary>
    ''' Form closing event handler - saves position and unregisters the palette
    ''' </summary>
    Private Sub fCellTypes_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        ToolPaletteHelper.SavePosition(Me, "CellTypes")
        ToolPaletteHelper.UnregisterToolForm(Me)
    End Sub

    ''' <summary>
    ''' Fills the list with all cell types in the order matching GlobalVariables.CellTypeMap
    ''' </summary>
    Private Sub PopulateCellTypesList()
        ' Clear existing items
        cmbCellTypeList.Items.Clear()

        ' Add all cell types with descriptive text
        cmbCellTypeList.Items.Add("Fast Passable 1 (F1)")
        cmbCellTypeList.Items.Add("Fast Passable 2 (F2)")
        cmbCellTypeList.Items.Add("Medium Passable 1 (M1)")
        cmbCellTypeList.Items.Add("Medium Passable 2 (M2)")
        cmbCellTypeList.Items.Add("Slow Passable 1 (S1)")
        cmbCellTypeList.Items.Add("Slow Passable 2 (S2)")
        cmbCellTypeList.Items.Add("Impassable 1 (I1)")
        cmbCellTypeList.Items.Add("Impassable 2 (I2)")
        cmbCellTypeList.Items.Add("North Cliffs (NC)")
        cmbCellTypeList.Items.Add("Cliffs - High Side (CHS)")
        cmbCellTypeList.Items.Add("Cliffs - Low Side (CLS)")
        cmbCellTypeList.Items.Add("Vents and Fumaroles (V)")
        cmbCellTypeList.Items.Add("zPad12 (Z12)")
        cmbCellTypeList.Items.Add("zPad13 (Z13)")
        cmbCellTypeList.Items.Add("zPad14 (Z14)")
        cmbCellTypeList.Items.Add("zPad15 (Z15)")
        cmbCellTypeList.Items.Add("zPad16 (Z16)")
        cmbCellTypeList.Items.Add("zPad17 (Z17)")
        cmbCellTypeList.Items.Add("zPad18 (Z18)")
        cmbCellTypeList.Items.Add("zPad19 (Z19)")
        cmbCellTypeList.Items.Add("zPad20 (Z20)")
        cmbCellTypeList.Items.Add("Bulldozed (D)")
        cmbCellTypeList.Items.Add("Rubble (R)")
        cmbCellTypeList.Items.Add("Normal Wall (NW)")
        cmbCellTypeList.Items.Add("Microbe Wall (MW)")
        cmbCellTypeList.Items.Add("Lava Wall (LW)")
        cmbCellTypeList.Items.Add("Tube 0 (T0)")
        cmbCellTypeList.Items.Add("Tube 1 (T1)")
        cmbCellTypeList.Items.Add("Tube 2 (T2)")
        cmbCellTypeList.Items.Add("Tube 3 (T3)")
        cmbCellTypeList.Items.Add("Tube 4 (T4)")
        cmbCellTypeList.Items.Add("Tube 5 (T5)")

        ' Select the first item by default
        If cmbCellTypeList.Items.Count > 0 Then
            cmbCellTypeList.SelectedIndex = 0
        End If
    End Sub

    ''' <summary>
    ''' Selection change event handler - sets the active cell type and clears
    ''' any active tile selection
    ''' </summary>
    Private Sub cmbCellTypeList_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbCellTypeList.SelectedIndexChanged
        ' Use the global dictionary to look up the cell type
        Dim index As Integer = cmbCellTypeList.SelectedIndex

        If GlobalVariables.CellTypeMap.ContainsKey(index) Then
            ' Reset tile placement selection and image since we're now using cell type tool
            GlobalVariables.SelectedTileImage = Nothing
            GlobalVariables.SelectedTilesetName = ""
            GlobalVariables.SelectedTileIndex = -1
            GlobalVariables.SelectedTilesetIndex = -1
            GlobalVariables.ActiveTileMappingIndex = -1

            ' Clear the selection in the TileSet form
            ClearTileSetFormSelection()

            ' Set the active cell type and mark the cell-type tool as the
            ' deliberate selection (the flag, not the value, is what makes the
            ' tool active - so even the default FastPassible1 is paintable)
            GlobalVariables.ActiveCellType = GlobalVariables.CellTypeMap(index)
            GlobalVariables.CellTypeToolActive = True
            Debug.WriteLine("Set ActiveCellType to " & GlobalVariables.ActiveCellType.ToString)

            ' Update main form's UI to reflect the new cell type selection
            UpdateMainFormLabels()
        Else
            Debug.WriteLine("Error: Invalid index " & index)
        End If
    End Sub

    ''' <summary>
    ''' Clears the selection in the TileSet form if it's open
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
    ''' Updates the main form's tool/placement labels via reflection and clears
    ''' the tile preview picture box
    ''' </summary>
    Private Sub UpdateMainFormLabels()
        ' Find the main form instance
        If mainForm Is Nothing Then
            mainForm = TryCast(Application.OpenForms("fMain"), fMain)
        End If

        ' Call the update method if main form is available
        If mainForm IsNot Nothing Then
            ' Use reflection to call the private method on the main form
            Try
                Dim methodInfo = mainForm.GetType().GetMethod("UpdateToolAndPlacementLabels",
                                                            Reflection.BindingFlags.NonPublic Or
                                                            Reflection.BindingFlags.Instance)
                If methodInfo IsNot Nothing Then
                    methodInfo.Invoke(mainForm, Nothing)
                End If

                ' Also clear the tile preview picturebox if possible
                Dim picSelectedTool = TryCast(mainForm.Controls.Find("picSelectedTool", True).FirstOrDefault(), PictureBox)
                If picSelectedTool IsNot Nothing Then
                    picSelectedTool.Image = Nothing
                    picSelectedTool.BackColor = Color.Black
                End If
            Catch ex As Exception
                Debug.WriteLine("Error updating main form labels: " & ex.Message)
            End Try
        End If
    End Sub

End Class