Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports Op2MapLib
Imports OP2UtilityDotNet.OP2Map

' fMain.Generate.vb
' Partial class of fMain: File -> Generate New Map, built on op2maplib's map
' generator.
'
' The Generate New Map window (fGenerateMap) collects the settings and runs
' the generator. Each result is built straight into the main view: the blank
' template of the same size is loaded (its tile mapping table is the
' generator's standard table, so mapping indices copy across unchanged), then
' every cell's tile, cell type and lava-possible flag is written. The map is
' new and unsaved, like one from File -> New Map.
'
' Generating again replaces the previous result without asking, as long as it
' hasn't been edited (nothing in the undo history). Any other map with unsaved
' changes gets the usual Save / Discard / Cancel prompt first.
Partial Public Class fMain

#Region "Generate New Map"

    ''' <summary>The Generate New Map window, while open.</summary>
    Private generateMapForm As fGenerateMap

    ''' <summary>The map object last filled by the generator (Nothing once another map is loaded).</summary>
    Private generatedMap As Map
    ''' <summary>Spawn points and volcano centres of the generated map, drawn as markers.</summary>
    Private generatedSpawns As Point() = {}
    Private generatedVolcanoes As Point() = {}

    ''' <summary>Colour of the spawn and volcano markers.</summary>
    Private Shared ReadOnly SpawnMarkerColour As Color = Color.White
    Private Shared ReadOnly VolcanoMarkerColour As Color = Color.OrangeRed

    ''' <summary>File -> Generate New Map: opens the window (or brings it forward).</summary>
    Private Sub GenerateMap_Click(sender As Object, e As EventArgs)
        If GetTerrainProfile() Is Nothing Then
            MessageBox.Show("The map generator needs op2maplib, which couldn't be loaded:" & Environment.NewLine &
                            TerrainProfileError, "Generate New Map", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If generateMapForm Is Nothing OrElse generateMapForm.IsDisposed Then
            generateMapForm = New fGenerateMap(Me)
            generateMapForm.Show(Me)
        Else
            If generateMapForm.WindowState = FormWindowState.Minimized Then generateMapForm.WindowState = FormWindowState.Normal
            generateMapForm.Activate()
        End If
    End Sub

    ''' <summary>
    ''' True when the open map is the last generated one and hasn't been edited,
    ''' so generating again may replace it without asking.
    ''' </summary>
    Friend Function IsUntouchedGeneratedMap() As Boolean
        Return currentMap IsNot Nothing AndAlso currentMap Is generatedMap AndAlso
               undoStack.Count = 0 AndAlso redoStack.Count = 0
    End Function

    ''' <summary>
    ''' Shows a generated map in the main view as a new, unsaved map called
    ''' fileName (in the default map folder). Returns False if nothing changed:
    ''' the user cancelled the unsaved-changes prompt, or the template couldn't
    ''' be loaded (the reason has been shown). Pass alreadyConfirmed when the
    ''' caller has just asked about unsaved changes, so the user isn't asked twice.
    ''' </summary>
    Friend Function ShowGeneratedMap(gen As GeneratedMap, fileName As String,
                                     Optional alreadyConfirmed As Boolean = False) As Boolean
        Dim replacingOwn As Boolean = IsUntouchedGeneratedMap()
        If Not replacingOwn AndAlso Not alreadyConfirmed AndAlso Not ConfirmDiscardUnsavedChanges() Then Return False

        ' Keep the view when replacing a generated map of the same size, so
        ' trying seeds doesn't jump the view back each time.
        Dim keepView As Boolean = replacingOwn AndAlso
            currentMap.WidthInTiles() = gen.Width AndAlso currentMap.HeightInTiles() = gen.Height
        Dim oldZoom As Single = zoomLevel, oldX As Integer = offsetX, oldY As Integer = offsetY

        Dim extractError As String = GlobalVariables.EnsureBlankMapsExtracted()
        Dim template As String = GlobalVariables.GetBlankMapFileName(gen.Width, gen.Height)
        If String.IsNullOrEmpty(template) Then
            MessageBox.Show($"There's no blank map template for {gen.Width} x {gen.Height}." &
                            If(extractError IsNot Nothing, Environment.NewLine & extractError, ""),
                            "Generate New Map", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End If
        If Not LoadMapFile(Path.Combine(GlobalVariables.BlankMapsPath, template), includeInRecentFiles:=False) Then
            Return False
        End If

        ' Copy the result in. The template's mapping table is the profile's
        ' standard table, which the generator's tile numbers refer to.
        Dim w As Integer = gen.Width
        For y As Integer = 0 To gen.Height - 1
            For x As Integer = 0 To w - 1
                Dim i As Integer = y * w + x
                currentMap.SetTileMappingIndex(x, y, gen.Tiles(i))
                currentMap.SetCellType(CType(gen.CellTypes(i), CellType), x, y)
                currentMap.SetLavaPossible(False, x, y)
            Next
        Next
        For Each zone As Point() In gen.LavaZones
            For Each p As Point In zone
                currentMap.SetLavaPossible(True, p.X, p.Y)
            Next
        Next
        ' The bottom row isn't shown in game and must stay Impassable.
        Dim bottomRow As Integer = gen.Height - 1
        For x As Integer = 0 To w - 1
            currentMap.SetCellType(CellType.Impassible1, x, bottomRow)
        Next

        generatedMap = currentMap
        generatedSpawns = gen.Spawns
        generatedVolcanoes = gen.Volcanoes

        SetMapFileName(fileName, AppSettings.WorkingPath)
        MarkNewUnsavedMap()

        If keepView Then
            zoomLevel = oldZoom
            offsetX = oldX
            offsetY = oldY
        Else
            FitGeneratedMapToView()
        End If
        ClampOffsets()
        lblZoomLevel.Text = $"Zoom Level: {Math.Round(zoomLevel * 100)}%"
        RefreshMiniMap()
        pnlMap.Invalidate()
        Return True
    End Function

    ''' <summary>
    ''' Picks the largest of the usual zoom steps that shows the whole map, and
    ''' centres it, so a new generated map is seen at once.
    ''' </summary>
    Private Sub FitGeneratedMapToView()
        Dim steps As Single() = {1.0F, 0.75F, 0.5F, 0.25F, 0.1F}
        Dim mapW As Integer = CInt(currentMap.WidthInTiles()) * 32
        Dim mapH As Integer = CInt(currentMap.HeightInTiles()) * 32
        zoomLevel = steps(steps.Length - 1)
        For Each s As Single In steps
            If mapW * s <= pnlMap.ClientSize.Width AndAlso mapH * s <= pnlMap.ClientSize.Height Then
                zoomLevel = s
                Exit For
            End If
        Next
        offsetX = CInt((pnlMap.ClientSize.Width - mapW * zoomLevel) / 2)
        offsetY = CInt((pnlMap.ClientSize.Height - mapH * zoomLevel) / 2)
    End Sub

    ''' <summary>
    ''' Draws the generated map's spawn points (white circles, numbered) and
    ''' volcanoes (red circles). Outpost 2 maps don't store these, so they are
    ''' only a guide for the mission and disappear when another map is opened.
    ''' </summary>
    Private Sub DrawGeneratedMarkers(g As Graphics)
        If currentMap Is Nothing OrElse currentMap IsNot generatedMap Then Return
        If generatedSpawns.Length = 0 AndAlso generatedVolcanoes.Length = 0 Then Return

        Dim radius As Single = Math.Max(10.0F, 3 * 32 * zoomLevel)
        Dim oldMode = g.SmoothingMode
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        Using shadow As New Pen(Color.Black, 4), spawnPen As New Pen(SpawnMarkerColour, 2),
              volcanoPen As New Pen(VolcanoMarkerColour, 2),
              font As New Font("Segoe UI", 9, FontStyle.Bold), textBrush As New SolidBrush(SpawnMarkerColour)
            For i As Integer = 0 To generatedSpawns.Length - 1
                Dim c As PointF = TileCentre(generatedSpawns(i))
                g.DrawEllipse(shadow, c.X - radius, c.Y - radius, radius * 2, radius * 2)
                g.DrawEllipse(spawnPen, c.X - radius, c.Y - radius, radius * 2, radius * 2)
                Dim label As String = "S" & (i + 1).ToString()
                Dim size As SizeF = g.MeasureString(label, font)
                g.DrawString(label, font, Brushes.Black, c.X - size.Width / 2 + 1, c.Y - size.Height / 2 + 1)
                g.DrawString(label, font, textBrush, c.X - size.Width / 2, c.Y - size.Height / 2)
            Next
            For Each v As Point In generatedVolcanoes
                Dim c As PointF = TileCentre(v)
                g.DrawEllipse(shadow, c.X - radius, c.Y - radius, radius * 2, radius * 2)
                g.DrawEllipse(volcanoPen, c.X - radius, c.Y - radius, radius * 2, radius * 2)
            Next
        End Using
        g.SmoothingMode = oldMode
    End Sub

#End Region

End Class
