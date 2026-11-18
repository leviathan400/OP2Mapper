Imports System.Drawing
Imports System.Windows.Forms
Imports Op2MapLib

' fTerrain.vb
' Tool 6: the Terrain palette. Pick a ground (mud, rock, sand, ...) and a brush
' size, then left-drag on the map: op2maplib picks the tiles so borders between
' grounds use the game's transition tiles. Built in code (no designer file).
Public Class fTerrain
    Inherits Form

    ''' <summary>INI key for the brush size (Mapper3.ini, [Window] section).</summary>
    Private Const SettingBrushSize As String = "TerrainBrushSize"
    ''' <summary>Smallest brush: 3 tiles (a single tile can't show a blend). A saved 1 becomes 3.</summary>
    Private Const MinBrushSize As Integer = 3
    Private Const MaxBrushSize As Integer = 25

    Private mainForm As fMain
    ''' <summary>One toggle button per paintable ground; Tag holds the ground index.</summary>
    Private ReadOnly groundButtons As New List(Of CheckBox)
    Private ReadOnly numBrushSize As New NumericUpDown()
    Private ReadOnly btnCleanUp As New Button()
    Private ReadOnly lblHelp As New Label()

    ' Cliffs
    Private Const SettingCliffSide As String = "TerrainCliffSide"
    Private Const SettingCliffHeight As String = "TerrainCliffHeight"
    Private ReadOnly btnCliff As New CheckBox()
    Private ReadOnly cmbCliffSide As New ComboBox()
    Private ReadOnly numCliffHeight As New NumericUpDown()
    Private ReadOnly btnPlaceCliff As New Button()
    ''' <summary>True while SyncFromState sets the buttons, so their events don't re-arm the tool.</summary>
    Private syncing As Boolean = False

    Public Sub New()
        Me.Name = "fTerrain"
        Me.Text = "Terrain Brush"
        Me.StartPosition = FormStartPosition.Manual
        Me.AutoScaleMode = AutoScaleMode.Font
        ' Border, buttons, snapping and position saving come from ToolPalette.vb
        ' (ConfigureAsToolPalette in Load), as for every tool window.
        Me.ClientSize = New Size(264, 100)   ' height set once the controls are laid out
    End Sub

#Region "Window Snapping"

    Private Const WM_MOVING As Integer = &H216

    ''' <summary>
    ''' Snaps the window to screen edges and other tool palettes while it is
    ''' dragged, as the other palettes do (see ToolPaletteHelper.GetSnappedPosition).
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

    ''' <summary>Win32 RECT (screen coordinates) from the WM_MOVING lParam.</summary>
    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure RECT
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

#End Region

    ''' <summary>
    ''' Builds the palette from the op2maplib profile: one button per ground that
    ''' can be painted, the brush size, and Clean Up Transitions.
    ''' </summary>
    Private Sub fTerrain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ToolPaletteHelper.ConfigureAsToolPalette(Me)
        mainForm = TryCast(Application.OpenForms("fMain"), fMain)

        Const margin As Integer = 8
        Dim y As Integer = margin
        Dim width As Integer = ClientSize.Width - 2 * margin

        Dim profile As BlendProfile = If(mainForm IsNot Nothing, mainForm.GetTerrainProfile(), Nothing)
        If profile Is Nothing Then
            ' op2maplib missing or failed to load: say so instead of showing dead buttons.
            Dim lblError As New Label With {
                .Text = "The terrain brush is unavailable: " &
                        If(mainForm IsNot Nothing, mainForm.TerrainProfileError, "no main window") ,
                .Location = New Point(margin, y),
                .Size = New Size(width, 80),
                .ForeColor = Color.DarkRed
            }
            Controls.Add(lblError)
            y += lblError.Height + margin
        Else
            ' Ground buttons, two per row. Grounds with nothing to fill them
            ' (in the Outpost 2 profile: snow and lava) are left out.
            Dim colWidth As Integer = (width - margin) \ 2
            Dim col As Integer = 0
            For g As Integer = 0 To profile.GroundCount - 1
                If profile.GroundFillCount(g) = 0 Then Continue For
                Dim btn As New CheckBox With {
                    .Appearance = Appearance.Button,
                    .Text = profile.GroundName(g),
                    .Tag = g,
                    .TextImageRelation = TextImageRelation.ImageBeforeText,
                    .Image = MakeSwatch(GroundColour(profile.GroundId(g))),
                    .ImageAlign = ContentAlignment.MiddleLeft,
                    .Size = New Size(colWidth, 30),
                    .Location = New Point(margin + col * (colWidth + margin), y)
                }
                AddHandler btn.CheckedChanged, AddressOf GroundButton_CheckedChanged
                groundButtons.Add(btn)
                Controls.Add(btn)
                col += 1
                If col = 2 Then
                    col = 0
                    y += btn.Height + 4
                End If
            Next
            If col <> 0 Then y += 34
            y += margin \ 2

            ' Brush size
            Dim lblSize As New Label With {
                .Text = "Brush size (tiles):",
                .AutoSize = True,
                .Location = New Point(margin, y + 3)
            }
            Controls.Add(lblSize)
            ' Odd sizes only (3, 5, 7, ...), so the brush is centred on the tile under the mouse.
            numBrushSize.Minimum = MinBrushSize
            numBrushSize.Maximum = MaxBrushSize
            numBrushSize.Increment = 2
            numBrushSize.Value = MakeOdd(LoadBrushSize())
            numBrushSize.Size = New Size(56, 22)
            numBrushSize.Location = New Point(margin + width - numBrushSize.Width, y)
            GlobalVariables.TerrainBrushSize = CInt(numBrushSize.Value)
            AddHandler numBrushSize.ValueChanged, AddressOf BrushSize_ValueChanged
            Controls.Add(numBrushSize)
            y += numBrushSize.Height + margin

            ' Clean up transitions (whole map)
            btnCleanUp.Text = "Clean Up Transitions..."
            btnCleanUp.Size = New Size(width, 28)
            btnCleanUp.Location = New Point(margin, y)
            AddHandler btnCleanUp.Click, Sub(s, ev) If mainForm IsNot Nothing Then mainForm.TerrainCleanUpTransitions()
            Controls.Add(btnCleanUp)
            y += btnCleanUp.Height + margin

            ' --- Cliffs ---
            Dim sep As New Label With {
                .BorderStyle = BorderStyle.Fixed3D,
                .Location = New Point(margin, y + 2),
                .Size = New Size(width, 2)
            }
            Controls.Add(sep)
            y += margin

            btnCliff.Appearance = Appearance.Button
            btnCliff.Text = "Draw Cliff"
            btnCliff.TextAlign = ContentAlignment.MiddleCenter
            btnCliff.Size = New Size(width, 30)
            btnCliff.Location = New Point(margin, y)
            AddHandler btnCliff.CheckedChanged, AddressOf CliffButton_CheckedChanged
            Controls.Add(btnCliff)
            y += btnCliff.Height + 6

            Dim lblSide As New Label With {.Text = "High side:", .AutoSize = True, .Location = New Point(margin, y + 4)}
            Controls.Add(lblSide)
            cmbCliffSide.DropDownStyle = ComboBoxStyle.DropDownList
            cmbCliffSide.Items.AddRange(New Object() {"Left of the line", "Right of the line", "North", "South"})
            cmbCliffSide.Size = New Size(130, 22)
            cmbCliffSide.Location = New Point(margin + width - cmbCliffSide.Width, y)
            GlobalVariables.CliffHighSide = LoadSetting(SettingCliffSide, 0, 0, 3)
            cmbCliffSide.SelectedIndex = GlobalVariables.CliffHighSide
            AddHandler cmbCliffSide.SelectedIndexChanged, AddressOf CliffSettings_Changed
            Controls.Add(cmbCliffSide)
            y += cmbCliffSide.Height + 6

            Dim lblHeight As New Label With {.Text = "Wall height (tiles):", .AutoSize = True, .Location = New Point(margin, y + 3)}
            Controls.Add(lblHeight)
            numCliffHeight.Minimum = 3
            numCliffHeight.Maximum = 6
            numCliffHeight.Size = New Size(56, 22)
            numCliffHeight.Location = New Point(margin + width - numCliffHeight.Width, y)
            GlobalVariables.CliffWallHeight = LoadSetting(SettingCliffHeight, 3, 3, 6)
            numCliffHeight.Value = GlobalVariables.CliffWallHeight
            AddHandler numCliffHeight.ValueChanged, AddressOf CliffSettings_Changed
            Controls.Add(numCliffHeight)
            y += numCliffHeight.Height + 6

            btnPlaceCliff.Text = "Place Cliff (Enter)"
            btnPlaceCliff.Size = New Size(width, 28)
            btnPlaceCliff.Location = New Point(margin, y)
            AddHandler btnPlaceCliff.Click, Sub(s, ev) If mainForm IsNot Nothing Then mainForm.PlaceCliff()
            Controls.Add(btnPlaceCliff)
            y += btnPlaceCliff.Height + margin
        End If

        lblHelp.Text = "Pick a ground, then left-drag on the map. Borders blend with the game's transition tiles. " &
                       "Ctrl + mouse wheel changes the size. Clear Tool stops the brush." & Environment.NewLine &
                       "Draw Cliff: click points on the map (they snap to cliff slopes); Enter places the cliff. " &
                       "Backspace removes the last point, Esc clears them. Click the first point again for a closed cliff."
        lblHelp.ForeColor = SystemColors.GrayText
        lblHelp.Location = New Point(margin, y)
        ' Tall enough for the wrapped text, so nothing is cut off.
        Dim helpHeight As Integer = TextRenderer.MeasureText(lblHelp.Text, lblHelp.Font, New Size(width, 0),
                                                             TextFormatFlags.WordBreak).Height
        lblHelp.Size = New Size(width, helpHeight + 4)
        Controls.Add(lblHelp)
        y += lblHelp.Height + margin

        ClientSize = New Size(ClientSize.Width, y)

        ' Restore after sizing, so a saved position is checked against the real size.
        ToolPaletteHelper.RestorePosition(Me, "Terrain")
        SyncFromState()
    End Sub

    Private Sub fTerrain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        ToolPaletteHelper.SavePosition(Me, "Terrain")
        ToolPaletteHelper.UnregisterToolForm(Me)
    End Sub

    ''' <summary>
    ''' A ground button was toggled: arm the brush with that ground (and pop the
    ''' other buttons out), or disarm it when the active one is clicked again.
    ''' </summary>
    Private Sub GroundButton_CheckedChanged(sender As Object, e As EventArgs)
        If syncing OrElse mainForm Is Nothing Then Return
        Dim btn = DirectCast(sender, CheckBox)
        If btn.Checked Then
            mainForm.ArmTerrainTool(CInt(btn.Tag), btn.Text)
        ElseIf GlobalVariables.TerrainGround = CInt(btn.Tag) Then
            mainForm.DisarmTerrainTool()
        End If
        SyncFromState()
    End Sub

    ''' <summary>Draw Cliff toggled: turn the cliff tool on or off.</summary>
    Private Sub CliffButton_CheckedChanged(sender As Object, e As EventArgs)
        If syncing OrElse mainForm Is Nothing Then Return
        If btnCliff.Checked Then
            mainForm.ArmCliffTool()
        Else
            mainForm.DisarmCliffTool()
        End If
        SyncFromState()
    End Sub

    ''' <summary>High side or wall height changed: store it (and remember it in Mapper3.ini).</summary>
    Private Sub CliffSettings_Changed(sender As Object, e As EventArgs)
        GlobalVariables.CliffHighSide = Math.Max(0, cmbCliffSide.SelectedIndex)
        GlobalVariables.CliffWallHeight = CInt(numCliffHeight.Value)
        ToolPaletteHelper.WriteToolSetting(SettingCliffSide, GlobalVariables.CliffHighSide.ToString())
        ToolPaletteHelper.WriteToolSetting(SettingCliffHeight, GlobalVariables.CliffWallHeight.ToString())
        If mainForm IsNot Nothing Then mainForm.CliffSettingsChanged()
    End Sub

    Private Shared Function LoadSetting(key As String, defaultValue As Integer, min As Integer, max As Integer) As Integer
        Dim v As Integer
        If Not Integer.TryParse(ToolPaletteHelper.ReadToolSetting(key, defaultValue.ToString()), v) Then v = defaultValue
        Return Math.Max(min, Math.Min(max, v))
    End Function

    Private Sub BrushSize_ValueChanged(sender As Object, e As EventArgs)
        ' A typed-in even number is rounded up to the next odd one.
        Dim v As Integer = MakeOdd(CInt(numBrushSize.Value))
        If v <> CInt(numBrushSize.Value) Then
            numBrushSize.Value = v   ' fires this handler again with the odd value
            Return
        End If
        GlobalVariables.TerrainBrushSize = v
        ToolPaletteHelper.WriteToolSetting(SettingBrushSize, GlobalVariables.TerrainBrushSize.ToString())
        If mainForm IsNot Nothing Then mainForm.TerrainBrushSizeChanged()
    End Sub

    ''' <summary>
    ''' Changes the brush size by a step (Ctrl + mouse wheel on the map).
    ''' </summary>
    Public Sub StepBrushSize(delta As Integer)
        Dim v As Decimal = numBrushSize.Value + delta
        numBrushSize.Value = Math.Max(numBrushSize.Minimum, Math.Min(numBrushSize.Maximum, v))
    End Sub

    ''' <summary>
    ''' Shows which ground is active. Called by fMain whenever the tool changes,
    ''' including when another tool (tile, cell type, copy, ...) takes over.
    ''' </summary>
    Public Sub SyncFromState()
        Dim active As Boolean = mainForm IsNot Nothing AndAlso mainForm.IsTerrainToolActive()
        syncing = True
        Try
            For Each btn In groundButtons
                btn.Checked = active AndAlso CInt(btn.Tag) = GlobalVariables.TerrainGround
            Next
            btnCliff.Checked = mainForm IsNot Nothing AndAlso mainForm.IsCliffToolActive()
        Finally
            syncing = False
        End Try
    End Sub

    ''' <summary>Clamps a size to the allowed range and rounds an even size up to odd.</summary>
    Private Shared Function MakeOdd(v As Integer) As Integer
        v = Math.Max(MinBrushSize, Math.Min(MaxBrushSize, v))
        If v Mod 2 = 0 Then v = Math.Min(MaxBrushSize, v + 1)
        Return v
    End Function

    Private Shared Function LoadBrushSize() As Integer
        Dim v As Integer
        If Integer.TryParse(ToolPaletteHelper.ReadToolSetting(SettingBrushSize, "5"), v) Then Return v
        Return 5
    End Function

    ''' <summary>Swatch colour for a ground id, roughly matching its tiles.</summary>
    Friend Shared Function GroundColour(groundId As String) As Color
        Select Case groundId
            Case "mud" : Return Color.FromArgb(150, 120, 85)
            Case "roc" : Return Color.FromArgb(105, 105, 110)
            Case "san" : Return Color.FromArgb(214, 140, 50)
            Case "veg" : Return Color.FromArgb(85, 135, 60)
            Case "sno" : Return Color.FromArgb(235, 235, 240)
            Case "lav" : Return Color.FromArgb(200, 60, 30)
            Case Else : Return Color.Gray
        End Select
    End Function

    Private Shared Function MakeSwatch(c As Color) As Bitmap
        Dim bmp As New Bitmap(14, 14)
        Using g = Graphics.FromImage(bmp)
            Using b As New SolidBrush(c)
                g.FillRectangle(b, 1, 1, 12, 12)
            End Using
            g.DrawRectangle(Pens.Black, 1, 1, 11, 11)
        End Using
        Return bmp
    End Function

End Class
