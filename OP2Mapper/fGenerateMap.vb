Imports System.Drawing
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Op2MapLib

' fGenerateMap.vb
' File -> Generate New Map: settings for op2maplib's map generator. Built in
' code (no designer file).
'
' Pick a size, a template (which sets the terrain mix, roughness and
' volcanoes), a seed and the other settings, optionally add terrain features,
' then Generate. The map is built straight into the main view as a new,
' unsaved map (fMain.ShowGeneratedMap); generating again replaces it as long
' as it hasn't been edited. Roll picks a new seed and generates. The settings
' are remembered in Mapper3.ini, [GenerateMap] section.
'
' Generation runs on a background thread: a large map with cliffs can take
' several seconds. The window stays open so seeds and settings can be tried
' one after another.
Public Class fGenerateMap
    Inherits Form

    Private Const IniSection As String = "GenerateMap"

    Private ReadOnly mainForm As fMain

    ''' <summary>Sizes with a blank map template (width, height).</summary>
    Private Shared ReadOnly MapSizes As Size() = {
        New Size(64, 64), New Size(64, 128), New Size(64, 256), New Size(128, 64), New Size(128, 128),
        New Size(128, 256), New Size(256, 64), New Size(256, 128), New Size(256, 256), New Size(512, 256)}

    ''' <summary>Generator templates: display name and library name.</summary>
    Private Shared ReadOnly Templates As String(,) = {
        {"Plains", "plains"}, {"Canyon", "canyon"}, {"Desert", "desert"},
        {"Islands", "islands"}, {"Ice", "ice"}, {"New Terra", "newterra"}}

    ''' <summary>
    ''' Each template's terrain mix (ground id, share), as set by op2maplib's
    ''' applyTemplate (src/mapgen/field.cpp). The library has no getter for the
    ''' mix, so it's mirrored here to show it on the sliders.
    ''' </summary>
    Private Shared ReadOnly TemplateMix As New Dictionary(Of String, Dictionary(Of String, Double)) From {
        {"plains", New Dictionary(Of String, Double) From {{"mud", 0.45}, {"roc", 0.25}, {"san", 0.15}, {"veg", 0.15}}},
        {"canyon", New Dictionary(Of String, Double) From {{"roc", 0.55}, {"mud", 0.25}, {"san", 0.1}, {"veg", 0.1}}},
        {"desert", New Dictionary(Of String, Double) From {{"san", 0.6}, {"mud", 0.3}, {"roc", 0.08}, {"veg", 0.02}}},
        {"islands", New Dictionary(Of String, Double) From {{"mud", 0.35}, {"roc", 0.35}, {"san", 0.2}, {"veg", 0.1}}},
        {"ice", New Dictionary(Of String, Double) From {{"sno", 0.45}, {"roc", 0.35}, {"mud", 0.2}}},
        {"newterra", New Dictionary(Of String, Double) From {{"mud", 0.68}, {"san", 0.2}, {"roc", 0.12}}}}

    ''' <summary>Terrain mix sliders: label and the profile ground id they set.</summary>
    Private Shared ReadOnly MixGrounds As String(,) = {
        {"Mud", "mud"}, {"Rock", "roc"}, {"Sand", "san"}, {"Vegetation", "veg"}, {"Snow", "sno"}}

    ''' <summary>Feature positions: display name and the library's sector code (German compass: o = east).</summary>
    Private Shared ReadOnly Sectors As String(,) = {
        {"Random", ""}, {"North-west", "nw"}, {"North", "n"}, {"North-east", "no"}, {"West", "w"}, {"Centre", "c"},
        {"East", "o"}, {"South-west", "sw"}, {"South", "s"}, {"South-east", "so"}}

    ''' <summary>One terrain feature as the user set it up.</summary>
    Private Class FeatureEntry
        Public Kind As MapFeature
        Public Where As Integer   ' index into Sectors
        Public [To] As Integer    ' index into Sectors (valley and choke)
        Public Size As Integer = 50   ' 0..100
        Public Lava As Boolean    ' valley only

        Public Overrides Function ToString() As String
            Dim text As String = $"{Kind} - {Sectors(Where, 0)}"
            If Kind = MapFeature.Valley OrElse Kind = MapFeature.Choke Then
                text &= " to " & If([To] = 0, "opposite", Sectors([To], 0))
            End If
            text &= $", size {Size} %"
            If Kind = MapFeature.Valley AndAlso Lava Then text &= ", lava"
            Return text
        End Function
    End Class

    ' --- Controls ---------------------------------------------------------
    Private ReadOnly cmbSize As New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly cmbTemplate As New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly txtSeed As New TextBox()
    Private ReadOnly btnRoll As New Button() With {.Text = "Roll"}
    Private ReadOnly mixBars As New List(Of TrackBar)
    Private ReadOnly barRoughness As TrackBar = NewBar(0, 100)
    Private ReadOnly barMinBiome As TrackBar = NewBar(1, 30)
    Private ReadOnly cmbSymmetry As New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly barCliffs As TrackBar = NewBar(0, 50)
    Private ReadOnly barDetails As TrackBar = NewBar(0, 100)
    Private ReadOnly numSpawns As New NumericUpDown() With {.Minimum = 0, .Maximum = 8}
    Private ReadOnly numVolcanoes As New NumericUpDown() With {.Minimum = 0, .Maximum = 8}
    Private ReadOnly lstFeatures As New ListBox() With {.IntegralHeight = False}
    Private ReadOnly cmbWhere As New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly cmbTo As New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly barFeatureSize As TrackBar = NewBar(0, 100)
    Private ReadOnly chkLava As New CheckBox() With {.Text = "Lava floor", .AutoSize = True}
    Private ReadOnly btnRemoveFeature As New Button() With {.Text = "Remove Feature"}
    Private ReadOnly lblStatus As New Label() With {.AutoSize = False}
    Private ReadOnly btnGenerate As New Button() With {.Text = "Generate"}
    Private ReadOnly btnClose As New Button() With {.Text = "Close"}

    ''' <summary>Label next to each slider showing its value, keyed by the slider.</summary>
    Private ReadOnly valueLabels As New Dictionary(Of TrackBar, Label)
    ''' <summary>How each slider's value is shown.</summary>
    Private ReadOnly valueFormats As New Dictionary(Of TrackBar, Func(Of Integer, String))

    ''' <summary>True while code sets the controls, so their events don't react.</summary>
    Private loading As Boolean = False
    Private generating As Boolean = False
    Private ReadOnly rng As New Random()

    Public Sub New(owner As fMain)
        mainForm = owner
        Name = "fGenerateMap"
        Text = "Generate New Map"
        Icon = My.Resources.IconLarge
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = True
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        AutoScaleMode = AutoScaleMode.Font
        Font = New Font("Segoe UI", 9.0F)
        BuildLayout()
        LoadSettings()
    End Sub

#Region "Layout"

    Private Const LabelWidth As Integer = 120
    Private Const FieldWidth As Integer = 250
    Private Const Pad As Integer = 10

    ''' <summary>A slider without ticks, sized for a settings row.</summary>
    Private Shared Function NewBar(min As Integer, max As Integer) As TrackBar
        Return New TrackBar() With {.Minimum = min, .Maximum = max, .TickStyle = TickStyle.None,
                                    .AutoSize = False, .Height = 26, .SmallChange = 1, .LargeChange = 5}
    End Function

    ''' <summary>Lays the window out top to bottom.</summary>
    Private Sub BuildLayout()
        Dim y As Integer = Pad

        For Each s As Size In MapSizes
            cmbSize.Items.Add($"{s.Width} x {s.Height}")
        Next
        AddRow("Size:", cmbSize, y)

        For i As Integer = 0 To Templates.GetLength(0) - 1
            cmbTemplate.Items.Add(Templates(i, 0))
        Next
        AddRow("Template:", cmbTemplate, y)
        AddHandler cmbTemplate.SelectedIndexChanged, AddressOf Template_Changed

        ' Seed: text box plus Roll (new seed and generate).
        AddLabel("Seed:", y)
        txtSeed.SetBounds(Pad + LabelWidth, y, FieldWidth - 70, 23)
        btnRoll.SetBounds(Pad + LabelWidth + FieldWidth - 64, y - 1, 64, 25)
        Controls.Add(txtSeed)
        Controls.Add(btnRoll)
        AddHandler btnRoll.Click, AddressOf Roll_Click
        y += 30

        AddHeading("Terrain mix", y)
        For i As Integer = 0 To MixGrounds.GetLength(0) - 1
            Dim bar As TrackBar = NewBar(0, 100)
            mixBars.Add(bar)
            AddBar(MixGrounds(i, 0) & ":", bar, y, Function(v) $"{v} %")
        Next
        AddBar("Roughness:", barRoughness, y, Function(v) $"{v} %")
        AddBar("Min. biome size:", barMinBiome, y,
               Function(v) (v / 10.0).ToString("0.0", CultureInfo.CurrentCulture) & " %")

        AddHeading("Layout", y)
        cmbSymmetry.Items.AddRange({"None", "Mirror left / right", "Mirror top / bottom", "Rotated 180°", "Four-way (4 players)"})
        AddRow("Symmetry:", cmbSymmetry, y)
        AddBar("Cliffs:", barCliffs, y, Function(v) $"{v} %")
        AddBar("Rocks and craters:", barDetails, y, Function(v) $"{v} %")
        numSpawns.Width = 60
        AddRow("Spawn points:", numSpawns, y, keepWidth:=True)
        numVolcanoes.Width = 60
        AddRow("Volcanoes:", numVolcanoes, y, keepWidth:=True)

        ' Features: a list, buttons to add each kind, and the selected one's settings.
        AddHeading("Features", y)
        Dim bx As Integer = Pad
        For Each kind As MapFeature In {MapFeature.Valley, MapFeature.Mountain, MapFeature.Plateau,
                                        MapFeature.Clearing, MapFeature.Choke}
            Dim b As New Button() With {.Text = kind.ToString(), .Tag = kind}
            b.SetBounds(bx, y, 70, 25)
            AddHandler b.Click, AddressOf AddFeature_Click
            Controls.Add(b)
            bx += 75
        Next
        y += 30
        lstFeatures.SetBounds(Pad, y, LabelWidth + FieldWidth, 70)
        Controls.Add(lstFeatures)
        AddHandler lstFeatures.SelectedIndexChanged, AddressOf Feature_Selected
        y += 76
        For i As Integer = 0 To Sectors.GetLength(0) - 1
            cmbWhere.Items.Add(Sectors(i, 0))
            cmbTo.Items.Add(If(i = 0, "Opposite", Sectors(i, 0)))
        Next
        AddRow("Where:", cmbWhere, y)
        AddRow("To:", cmbTo, y)
        AddBar("Size:", barFeatureSize, y, Function(v) $"{v} %")
        chkLava.Location = New Point(Pad + LabelWidth, y)
        Controls.Add(chkLava)
        btnRemoveFeature.SetBounds(Pad + LabelWidth + FieldWidth - 120, y - 2, 120, 25)
        Controls.Add(btnRemoveFeature)
        y += 32
        AddHandler cmbWhere.SelectedIndexChanged, AddressOf FeatureSetting_Changed
        AddHandler cmbTo.SelectedIndexChanged, AddressOf FeatureSetting_Changed
        AddHandler barFeatureSize.ValueChanged, AddressOf FeatureSetting_Changed
        AddHandler chkLava.CheckedChanged, AddressOf FeatureSetting_Changed
        AddHandler btnRemoveFeature.Click, AddressOf RemoveFeature_Click

        ' Result of the last generate, then the buttons.
        lblStatus.SetBounds(Pad, y, LabelWidth + FieldWidth, 50)
        lblStatus.ForeColor = SystemColors.GrayText
        lblStatus.Text = "The map is built straight into the main view as a new, unsaved map."
        Controls.Add(lblStatus)
        y += 56
        btnClose.SetBounds(Pad + LabelWidth + FieldWidth - 90, y, 90, 28)
        btnGenerate.SetBounds(btnClose.Left - 96, y, 90, 28)
        Controls.Add(btnGenerate)
        Controls.Add(btnClose)
        AddHandler btnGenerate.Click, AddressOf Generate_Click
        AddHandler btnClose.Click, Sub(s, e) Close()
        AcceptButton = btnGenerate
        CancelButton = btnClose
        y += 28 + Pad

        ClientSize = New Size(2 * Pad + LabelWidth + FieldWidth, y)
        UpdateFeatureEditor()
    End Sub

    Private Sub AddLabel(text As String, y As Integer)
        Dim l As New Label() With {.Text = text, .AutoSize = False, .TextAlign = ContentAlignment.MiddleLeft}
        l.SetBounds(Pad, y, LabelWidth, 23)
        Controls.Add(l)
    End Sub

    Private Sub AddHeading(text As String, ByRef y As Integer)
        y += 4
        Dim l As New Label() With {.Text = text, .AutoSize = True, .Font = New Font(Font, FontStyle.Bold),
                                   .ForeColor = SystemColors.HotTrack}
        l.Location = New Point(Pad, y)
        Controls.Add(l)
        y += 22
    End Sub

    Private Sub AddRow(text As String, control As Control, ByRef y As Integer, Optional keepWidth As Boolean = False)
        AddLabel(text, y)
        control.Location = New Point(Pad + LabelWidth, y)
        If Not keepWidth Then control.Width = FieldWidth
        Controls.Add(control)
        y += 29
    End Sub

    ''' <summary>Adds a slider row with its value shown on the right.</summary>
    Private Sub AddBar(text As String, bar As TrackBar, ByRef y As Integer, format As Func(Of Integer, String))
        AddLabel(text, y)
        bar.SetBounds(Pad + LabelWidth - 4, y, FieldWidth - 50, 26)
        Dim value As New Label() With {.AutoSize = False, .TextAlign = ContentAlignment.MiddleRight}
        value.SetBounds(Pad + LabelWidth + FieldWidth - 54, y, 54, 23)
        valueLabels(bar) = value
        valueFormats(bar) = format
        AddHandler bar.ValueChanged, Sub(s, e) value.Text = format(bar.Value)
        value.Text = format(bar.Value)
        Controls.Add(bar)
        Controls.Add(value)
        y += 27
    End Sub

#End Region

#Region "Settings"

    ''' <summary>Library name of the selected template.</summary>
    Private ReadOnly Property TemplateName As String
        Get
            Return Templates(Math.Max(0, cmbTemplate.SelectedIndex), 1)
        End Get
    End Property

    ''' <summary>
    ''' A template was picked: set the mix, roughness and volcanoes to its
    ''' values. Everything else is left as it is.
    ''' </summary>
    Private Sub Template_Changed(sender As Object, e As EventArgs)
        If loading Then Return
        ApplyTemplateDefaults()
    End Sub

    Private Sub ApplyTemplateDefaults()
        Dim mix As Dictionary(Of String, Double) = TemplateMix(TemplateName)
        For i As Integer = 0 To mixBars.Count - 1
            Dim share As Double = 0
            mix.TryGetValue(MixGrounds(i, 1), share)
            mixBars(i).Value = CInt(Math.Round(share * 100))
        Next
        Using s As New MapGenSettings(TemplateName)
            barRoughness.Value = Clamp(CInt(Math.Round(s.Get("roughness") * 100)), barRoughness)
            numVolcanoes.Value = Math.Min(numVolcanoes.Maximum, CDec(s.Get("volcanoes")))
        End Using
    End Sub

    Private Shared Function Clamp(v As Integer, bar As TrackBar) As Integer
        Return Math.Max(bar.Minimum, Math.Min(bar.Maximum, v))
    End Function

    ''' <summary>Restores the last settings from Mapper3.ini (defaults: 128 x 128 plains).</summary>
    Private Sub LoadSettings()
        loading = True
        Try
            cmbSize.SelectedIndex = Math.Max(0, Array.IndexOf(MapSizes.Select(Function(s) $"{s.Width}x{s.Height}").ToArray(),
                                                              Read("Size", "128x128")))
            Dim t As Integer = 0
            For i As Integer = 0 To Templates.GetLength(0) - 1
                If Templates(i, 1) = Read("Template", "plains") Then t = i
            Next
            cmbTemplate.SelectedIndex = t
            ApplyTemplateDefaults()
            ' Saved values override the template's.
            For i As Integer = 0 To mixBars.Count - 1
                mixBars(i).Value = Clamp(ReadInt("Mix_" & MixGrounds(i, 1), mixBars(i).Value), mixBars(i))
            Next
            barRoughness.Value = Clamp(ReadInt("Roughness", barRoughness.Value), barRoughness)
            numVolcanoes.Value = Math.Max(0, Math.Min(numVolcanoes.Maximum, ReadInt("Volcanoes", CInt(numVolcanoes.Value))))
            txtSeed.Text = Read("Seed", "1234")
            barMinBiome.Value = Clamp(ReadInt("MinBiome", 6), barMinBiome)
            cmbSymmetry.SelectedIndex = Math.Max(0, Math.Min(cmbSymmetry.Items.Count - 1, ReadInt("Symmetry", 0)))
            barCliffs.Value = Clamp(ReadInt("Cliffs", 0), barCliffs)
            barDetails.Value = Clamp(ReadInt("Details", 50), barDetails)
            numSpawns.Value = Math.Max(0, Math.Min(numSpawns.Maximum, ReadInt("Spawns", 2)))
        Finally
            loading = False
        End Try
    End Sub

    ''' <summary>Saves the settings (not the features) to Mapper3.ini.</summary>
    Private Sub SaveSettings()
        Dim size As Size = MapSizes(Math.Max(0, cmbSize.SelectedIndex))
        Write("Size", $"{size.Width}x{size.Height}")
        Write("Template", TemplateName)
        Write("Seed", txtSeed.Text.Trim())
        For i As Integer = 0 To mixBars.Count - 1
            Write("Mix_" & MixGrounds(i, 1), mixBars(i).Value.ToString())
        Next
        Write("Roughness", barRoughness.Value.ToString())
        Write("MinBiome", barMinBiome.Value.ToString())
        Write("Symmetry", cmbSymmetry.SelectedIndex.ToString())
        Write("Cliffs", barCliffs.Value.ToString())
        Write("Details", barDetails.Value.ToString())
        Write("Spawns", numSpawns.Value.ToString())
        Write("Volcanoes", numVolcanoes.Value.ToString())
    End Sub

    Private Shared Function Read(key As String, defaultValue As String) As String
        Return ToolPaletteHelper.ReadIniSetting(IniSection, key, defaultValue)
    End Function

    Private Shared Function ReadInt(key As String, defaultValue As Integer) As Integer
        Dim v As Integer
        Return If(Integer.TryParse(Read(key, ""), v), v, defaultValue)
    End Function

    Private Shared Sub Write(key As String, value As String)
        ToolPaletteHelper.WriteIniSetting(IniSection, key, value)
    End Sub

#End Region

#Region "Features"

    Private Sub AddFeature_Click(sender As Object, e As EventArgs)
        Dim f As New FeatureEntry() With {.Kind = CType(DirectCast(sender, Button).Tag, MapFeature)}
        lstFeatures.Items.Add(f)
        lstFeatures.SelectedItem = f
    End Sub

    Private Sub RemoveFeature_Click(sender As Object, e As EventArgs)
        Dim i As Integer = lstFeatures.SelectedIndex
        If i < 0 Then Return
        lstFeatures.Items.RemoveAt(i)
        If lstFeatures.Items.Count > 0 Then lstFeatures.SelectedIndex = Math.Min(i, lstFeatures.Items.Count - 1)
        UpdateFeatureEditor()
    End Sub

    Private Sub Feature_Selected(sender As Object, e As EventArgs)
        UpdateFeatureEditor()
    End Sub

    ''' <summary>Shows the selected feature's settings; To and Lava only where they apply.</summary>
    Private Sub UpdateFeatureEditor()
        Dim f As FeatureEntry = TryCast(lstFeatures.SelectedItem, FeatureEntry)
        loading = True
        Try
            Dim has As Boolean = f IsNot Nothing
            cmbWhere.Enabled = has
            barFeatureSize.Enabled = has
            btnRemoveFeature.Enabled = has
            cmbTo.Enabled = has AndAlso (f.Kind = MapFeature.Valley OrElse f.Kind = MapFeature.Choke)
            chkLava.Enabled = has AndAlso f.Kind = MapFeature.Valley
            If has Then
                cmbWhere.SelectedIndex = f.Where
                cmbTo.SelectedIndex = f.To
                barFeatureSize.Value = f.Size
                chkLava.Checked = f.Lava
            Else
                cmbWhere.SelectedIndex = -1
                cmbTo.SelectedIndex = -1
                chkLava.Checked = False
            End If
        Finally
            loading = False
        End Try
    End Sub

    ''' <summary>The feature editor changed: store it on the selected feature.</summary>
    Private Sub FeatureSetting_Changed(sender As Object, e As EventArgs)
        If loading Then Return
        Dim i As Integer = lstFeatures.SelectedIndex
        Dim f As FeatureEntry = TryCast(lstFeatures.SelectedItem, FeatureEntry)
        If f Is Nothing Then Return
        f.Where = Math.Max(0, cmbWhere.SelectedIndex)
        f.To = Math.Max(0, cmbTo.SelectedIndex)
        f.Size = barFeatureSize.Value
        f.Lava = chkLava.Checked
        ' Re-insert so the list shows the new text.
        loading = True
        Try
            lstFeatures.Items(i) = f
            lstFeatures.SelectedIndex = i
        Finally
            loading = False
        End Try
    End Sub

#End Region

#Region "Generate"

    ''' <summary>Everything the generator needs, read from the controls on the UI thread.</summary>
    Private Class GenerateRequest
        Public Template As String
        Public Width As Integer
        Public Height As Integer
        Public Seed As ULong
        Public Mix As New Dictionary(Of String, Double)
        Public Roughness As Double
        Public MinBiome As Double
        Public Symmetry As MapSymmetry
        Public Cliffs As Double
        Public Details As Double
        Public Spawns As Integer
        Public Volcanoes As Integer
        Public Features As New List(Of FeatureEntry)
    End Class

    Private Sub Roll_Click(sender As Object, e As EventArgs)
        If generating Then Return
        txtSeed.Text = rng.Next(1, Integer.MaxValue).ToString()
        Generate_Click(sender, e)
    End Sub

    Private Async Sub Generate_Click(sender As Object, e As EventArgs)
        If generating Then Return
        Dim seed As ULong
        If Not ULong.TryParse(txtSeed.Text.Trim(), seed) Then
            MessageBox.Show("The seed must be a whole number (0 or more).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtSeed.Focus()
            Return
        End If
        Dim profile As BlendProfile = mainForm.GetTerrainProfile()
        If profile Is Nothing Then
            MessageBox.Show("op2maplib couldn't be loaded: " & mainForm.TerrainProfileError, Text,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        ' Ask about unsaved changes before the (possibly long) generate, not after.
        If Not mainForm.IsUntouchedGeneratedMap() AndAlso Not mainForm.ConfirmDiscardUnsavedChanges() Then Return

        Dim req As GenerateRequest = BuildRequest(seed)
        SaveSettings()

        generating = True
        SetBusy(True)
        lblStatus.Text = $"Generating {req.Width} x {req.Height} (seed {req.Seed})..."
        Dim sw As Stopwatch = Stopwatch.StartNew()
        Try
            Dim result As GeneratedMap = Await Task.Run(Function() RunGenerator(profile, req))
            sw.Stop()
            If IsDisposed Then Return
            Dim fileName As String = $"Generated_{req.Template}_{req.Seed}_{req.Width}x{req.Height}.map"
            If mainForm.ShowGeneratedMap(result, fileName, alreadyConfirmed:=True) Then
                lblStatus.Text = DescribeResult(profile, result, sw.Elapsed.TotalSeconds)
                AppLog.Write("Generate", $"Generated {fileName} in {sw.Elapsed.TotalSeconds:0.0} s")
            Else
                lblStatus.Text = "Not shown: the map in the main view was kept."
            End If
        Catch ex As Exception
            AppLog.WriteError("Generate", "Map generation failed", ex)
            If Not IsDisposed Then
                lblStatus.Text = "Generation failed."
                MessageBox.Show("The map couldn't be generated: " & ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Finally
            generating = False
            If Not IsDisposed Then SetBusy(False)
        End Try
    End Sub

    Private Function BuildRequest(seed As ULong) As GenerateRequest
        Dim size As Size = MapSizes(Math.Max(0, cmbSize.SelectedIndex))
        Dim req As New GenerateRequest() With {
            .Template = TemplateName, .Width = size.Width, .Height = size.Height, .Seed = seed,
            .Roughness = barRoughness.Value / 100.0, .MinBiome = barMinBiome.Value / 1000.0,
            .Symmetry = CType(Math.Max(0, cmbSymmetry.SelectedIndex), MapSymmetry),
            .Cliffs = barCliffs.Value / 100.0, .Details = barDetails.Value / 100.0,
            .Spawns = CInt(numSpawns.Value), .Volcanoes = CInt(numVolcanoes.Value)}
        For i As Integer = 0 To mixBars.Count - 1
            If mixBars(i).Value > 0 Then req.Mix(MixGrounds(i, 1)) = mixBars(i).Value / 100.0
        Next
        For Each f As FeatureEntry In lstFeatures.Items
            req.Features.Add(f)
        Next
        Return req
    End Function

    ''' <summary>Runs the generator (background thread; touches no controls).</summary>
    Private Shared Function RunGenerator(profile As BlendProfile, req As GenerateRequest) As GeneratedMap
        Using s As New MapGenSettings(req.Template)
            s.Set("width", req.Width)
            s.Set("height", req.Height)
            s.Seed = req.Seed
            ' All sliders at 0 keeps the template's own mix.
            If req.Mix.Count > 0 Then s.SetMix(req.Mix)
            s.Set("roughness", req.Roughness)
            s.Set("min_biome", req.MinBiome)
            s.Set("symmetry", CDbl(req.Symmetry))
            s.Set("cliffs", req.Cliffs)
            s.Set("details", req.Details)
            s.Set("spawns", req.Spawns)
            s.Set("volcanoes", req.Volcanoes)
            For Each f As FeatureEntry In req.Features
                s.AddFeature(f.Kind, Sectors(f.Where, 1), Sectors(f.To, 1), f.Size / 100.0, f.Lava)
            Next
            Return s.Generate(profile)
        End Using
    End Function

    ''' <summary>One-line summary like "95 % passable · Mud 45 %, Rock 25 % · 2 spawn points (connected)".</summary>
    Private Shared Function DescribeResult(profile As BlendProfile, r As GeneratedMap, seconds As Double) As String
        Dim parts As New List(Of String) From {$"{r.PassableShare * 100:0} % passable"}
        Dim grounds As New List(Of String)
        For g As Integer = 0 To r.GroundShares.Length - 1
            If r.GroundShares(g) >= 0.005 Then grounds.Add($"{profile.GroundName(g)} {r.GroundShares(g) * 100:0} %")
        Next
        If grounds.Count > 0 Then parts.Add(String.Join(", ", grounds))
        If r.PlateauShare > 0 Then parts.Add($"cliffs {r.PlateauShare * 100:0.0} %")
        If r.Spawns.Length > 0 Then
            parts.Add($"{r.Spawns.Length} spawn point{If(r.Spawns.Length = 1, "", "s")}" &
                      If(r.Spawns.Length > 1, If(r.Connected, " (connected)", " (NOT connected)"), ""))
        End If
        If r.Volcanoes.Length > 0 Then parts.Add($"{r.Volcanoes.Length} volcano{If(r.Volcanoes.Length = 1, "", "es")}")
        Return String.Join(" · ", parts) & If(seconds < 1, $" · {seconds * 1000:0} ms", $" · {seconds:0.0} s")
    End Function

    ''' <summary>Disables the inputs while a map is being generated.</summary>
    Private Sub SetBusy(busy As Boolean)
        For Each c As Control In Controls
            If c IsNot lblStatus AndAlso c IsNot btnClose Then c.Enabled = Not busy
        Next
        UseWaitCursor = busy
        If Not busy Then UpdateFeatureEditor()
    End Sub

    ''' <summary>Can't close mid-generate: the result would have nowhere to go.</summary>
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If generating AndAlso e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            Return
        End If
        If Not generating Then SaveSettings()
        MyBase.OnFormClosing(e)
    End Sub

#End Region

End Class
