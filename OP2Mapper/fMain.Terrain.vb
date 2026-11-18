Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports Op2MapLib
Imports OP2UtilityDotNet.OP2Map

' fMain.Terrain.vb
' Partial class of fMain: the terrain brush, built on op2maplib.
'
' The user picks a ground (mud, rock, sand, ...) in the Terrain palette and
' left-drags on the map. Each brush dab paints that ground onto a disc of
' cells, and op2maplib picks tiles so the borders with neighbouring grounds use
' the game's transition tiles. Every dab becomes a TerrainBrushCommand; a whole
' drag is one undo step.
'
' The library's view of the map (a BlendMap) is created fresh at the start of
' each stroke from the current tiles, so edits made any other way (tile
' placement, paste, undo, ...) never leave it out of date.
Partial Public Class fMain

#Region "Terrain Brush"

    ''' <summary>Tool 6: the Terrain palette.</summary>
    Private terrainForm As fTerrain

    ''' <summary>The op2maplib blend profile, loaded on first use and shared.</summary>
    Private Shared terrainProfile As BlendProfile
    ''' <summary>Why the profile couldn't be loaded, if it couldn't (shown by the palette).</summary>
    Private Shared terrainProfileLoadError As String
    Private Shared terrainProfileTried As Boolean = False

    ''' <summary>Blend state for the stroke in progress (Nothing between strokes).</summary>
    Private terrainBlend As BlendMap
    ''' <summary>Random seed for the stroke in progress: one per stroke, so a drag looks consistent.</summary>
    Private terrainSeed As ULong
    Private ReadOnly terrainRandom As New Random()

    ''' <summary>Tile under the mouse while the brush is armed, for the outline (-1 = none).</summary>
    Private terrainHoverX As Integer = -1
    Private terrainHoverY As Integer = -1

    ''' <summary>
    ''' The blend profile (op2maplib\op2.json next to the exe), or
    ''' Nothing if the library or profile couldn't be loaded. The reason is in
    ''' TerrainProfileError.
    ''' </summary>
    Friend Function GetTerrainProfile() As BlendProfile
        If Not terrainProfileTried Then
            terrainProfileTried = True
            Try
                terrainProfile = BlendProfile.LoadDefault()
                Debug.WriteLine($"op2maplib {BlendProfile.LibraryVersion}: {terrainProfile.Name}")
            Catch ex As DllNotFoundException
                terrainProfileLoadError = "op2maplib.dll was not found next to the program (x86\ or x64\ folder)."
            Catch ex As BadImageFormatException
                terrainProfileLoadError = "op2maplib.dll is the wrong build for this process (32/64-bit)."
            Catch ex As FileNotFoundException
                terrainProfileLoadError = "the blend profile op2maplib\op2.json was not found next to the program."
            Catch ex As Exception
                terrainProfileLoadError = ex.Message
            End Try
        End If
        Return terrainProfile
    End Function

    Friend ReadOnly Property TerrainProfileError As String
        Get
            Return If(terrainProfileLoadError, "")
        End Get
    End Property

    ''' <summary>
    ''' True when a left-click on the map should paint terrain: a ground is
    ''' picked in the Terrain palette and no tile or cell type has been picked
    ''' since (picking one of those takes over).
    ''' </summary>
    Friend Function IsTerrainToolActive() As Boolean
        Return GlobalVariables.EditModeEnabled AndAlso
               GlobalVariables.TerrainToolActive AndAlso
               Not GlobalVariables.CliffToolActive AndAlso
               GlobalVariables.TerrainGround >= 0 AndAlso
               GlobalVariables.ActiveTileMappingIndex < 0 AndAlso
               Not GlobalVariables.CellTypeToolActive
    End Function

    ''' <summary>
    ''' Makes the terrain brush the active tool, painting the given ground.
    ''' Called by the Terrain palette. Leaves any other tool or mode first.
    ''' </summary>
    Friend Sub ArmTerrainTool(ground As Integer, groundName As String)
        LeaveOtherToolsForTerrain()
        ' The brush and Draw Cliff are separate tools.
        If GlobalVariables.CliffToolActive Then
            GlobalVariables.CliffToolActive = False
            ClearCliffPoints()
        End If

        GlobalVariables.TerrainGround = ground
        GlobalVariables.TerrainGroundName = groundName
        GlobalVariables.TerrainToolActive = True
        Debug.WriteLine($"Terrain brush armed: {groundName}")

        UpdateToolAndPlacementLabels()
        pnlMap.Cursor = Cursors.Cross
    End Sub

    ''' <summary>
    ''' Leaves paste, tile-group placement and copy, and clears the tile and
    ''' cell-type tools, before the terrain brush or Draw Cliff takes over.
    ''' </summary>
    Private Sub LeaveOtherToolsForTerrain()
        If isPasteMode Then ExitPasteMode()
        If GlobalVariables.TileGroupPlacementMode Then ExitTileGroupPlacementMode()
        If GlobalVariables.IsCopyMode AndAlso GlobalVariables.CopyStage < 2 Then ExitCopyMode()

        ' Clear the tile and cell-type tools (as picking a cell type clears the tile tool).
        If GlobalVariables.SelectedTileImage IsNot Nothing Then GlobalVariables.SelectedTileImage.Dispose()
        GlobalVariables.SelectedTileImage = Nothing
        GlobalVariables.SelectedTilesetName = ""
        GlobalVariables.SelectedTileIndex = -1
        GlobalVariables.SelectedTilesetIndex = -1
        GlobalVariables.ActiveTileMappingIndex = -1
        GlobalVariables.ActiveCellType = CellType.FastPassible1
        GlobalVariables.CellTypeToolActive = False
        ClearTileSetFormSelection()
    End Sub

    ''' <summary>Stops the terrain brush (Clear Tool, or the palette's active button clicked again).</summary>
    Friend Sub DisarmTerrainTool()
        If Not GlobalVariables.TerrainToolActive Then Return
        GlobalVariables.TerrainToolActive = False
        InvalidateTerrainOutline()
        terrainHoverX = -1
        terrainHoverY = -1
        If Not isPasteMode Then pnlMap.Cursor = Cursors.Default
        UpdateToolAndPlacementLabels()
    End Sub

    ''' <summary>
    ''' Stops the brush without updating the tool labels: for Clear Tool, copy,
    ''' paste and tile-group placement, which update the labels themselves.
    ''' </summary>
    Private Sub StopTerrainTool()
        ' Draw Cliff stops too (its unplaced points are dropped).
        If GlobalVariables.CliffToolActive Then
            GlobalVariables.CliffToolActive = False
            ClearCliffPoints()
            If terrainForm IsNot Nothing AndAlso Not terrainForm.IsDisposed Then terrainForm.SyncFromState()
        End If
        If Not GlobalVariables.TerrainToolActive Then Return
        GlobalVariables.TerrainToolActive = False
        InvalidateTerrainOutline()
        terrainHoverX = -1
        terrainHoverY = -1
        If terrainForm IsNot Nothing AndAlso Not terrainForm.IsDisposed Then terrainForm.SyncFromState()
    End Sub

    ''' <summary>The palette changed the brush size: redraw the outline at the new size.</summary>
    Friend Sub TerrainBrushSizeChanged()
        InvalidateTerrainOutline(previousSize:=MaxTerrainOutlineSize)
        UpdateToolAndPlacementLabels()
    End Sub

    ''' <summary>Tells the palette to show the current tool (called from UpdateToolAndPlacementLabels).</summary>
    Private Sub SyncTerrainPalette()
        If terrainForm IsNot Nothing AndAlso Not terrainForm.IsDisposed Then terrainForm.SyncFromState()
        If Not IsTerrainToolActive() AndAlso terrainHoverX >= 0 Then
            ' Another tool took over: drop the outline and the brush cursor.
            InvalidateTerrainOutline()
            terrainHoverX = -1
            terrainHoverY = -1
            If Not isPasteMode Then pnlMap.Cursor = Cursors.Default
        End If
    End Sub

    ''' <summary>
    ''' Creates the library's view of the current map: its tile mapping table
    ''' and every cell's mapping index.
    ''' </summary>
    Private Function CreateTerrainBlendMap() As BlendMap
        Dim profile As BlendProfile = GetTerrainProfile()
        If profile Is Nothing OrElse currentMap Is Nothing Then Return Nothing

        Dim mappings As New List(Of Op2MapLib.TileMapping)(currentMap.tileMappings.Count)
        For i As Integer = 0 To currentMap.tileMappings.Count - 1
            Dim m = currentMap.tileMappings(i)
            Dim ts As Integer = m.tilesetIndex
            Dim name As String = If(ts >= 0 AndAlso ts < currentMap.tilesetSources.Count,
                                    currentMap.tilesetSources(ts).tilesetFilename, "")
            mappings.Add(New Op2MapLib.TileMapping(name, m.tileGraphicIndex))
        Next

        Dim w As Integer = CInt(currentMap.WidthInTiles())
        Dim h As Integer = CInt(currentMap.HeightInTiles())
        Dim cells(w * h - 1) As Integer
        For y As Integer = 0 To h - 1
            For x As Integer = 0 To w - 1
                cells(y * w + x) = CInt(currentMap.GetTileMappingIndex(x, y))
            Next
        Next
        Return New BlendMap(profile, w, h, mappings, cells)
    End Function

    ''' <summary>
    ''' Starts a brush stroke at a tile (left button down). Returns False if the
    ''' brush can't be used now, so the caller does nothing else.
    ''' </summary>
    Private Function BeginTerrainStroke(mapX As Integer, mapY As Integer) As Boolean
        If IsRestrictedRenderMode() Then
            MessageBox.Show($"Cannot paint terrain while in {CurrentRenderMode} Render Mode.",
                            "Operation Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return False
        End If

        EndTerrainStroke()
        Try
            terrainBlend = CreateTerrainBlendMap()
        Catch ex As Exception
            MessageBox.Show("The terrain brush couldn't read this map: " & ex.Message,
                            "Terrain Brush", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            terrainBlend = Nothing
        End Try
        If terrainBlend Is Nothing Then Return False

        terrainSeed = CULng(terrainRandom.Next()) << 16 Xor CULng(terrainRandom.Next())
        isEditDragging = True
        BeginEditStroke($"Terrain Brush: {GlobalVariables.TerrainGroundName}")
        lastEditTileX = mapX
        lastEditTileY = mapY
        TerrainDab(mapX, mapY)
        Return True
    End Function

    ''' <summary>Ends the stroke: frees the blend state. (The undo step is committed by EndEditStroke.)</summary>
    Private Sub EndTerrainStroke()
        If terrainBlend IsNot Nothing Then
            terrainBlend.Dispose()
            terrainBlend = Nothing
        End If
    End Sub

    ''' <summary>The cells of the brush disc centred on a tile, clipped to the map.</summary>
    Private Iterator Function TerrainBrushCells(cx As Integer, cy As Integer) As IEnumerable(Of Point)
        Dim half As Integer = Math.Max(0, GlobalVariables.TerrainBrushSize \ 2)
        Dim limit As Double = (half + 0.5) * (half + 0.5)
        Dim w As Integer = CInt(currentMap.WidthInTiles())
        Dim h As Integer = CInt(currentMap.HeightInTiles())
        Dim lockLastRow As Boolean = Not My.Settings.AllowEditLastRow
        For dy As Integer = -half To half
            For dx As Integer = -half To half
                If dx * dx + dy * dy > limit Then Continue For
                Dim x As Integer = cx + dx, y As Integer = cy + dy
                If x < 0 OrElse y < 0 OrElse x >= w OrElse y >= h Then Continue For
                ' The bottom row isn't shown in game and stays Impassable.
                If lockLastRow AndAlso y = GlobalVariables.MapLastRowIndex Then Continue For
                Yield New Point(x, y)
            Next
        Next
    End Function

    ''' <summary>
    ''' One brush dab: paint the disc, let op2maplib pick tiles, and apply the
    ''' changes as one command (part of the open stroke).
    ''' </summary>
    Private Sub TerrainDab(cx As Integer, cy As Integer)
        If terrainBlend Is Nothing Then Return
        Dim painted As Integer = terrainBlend.Paint(GlobalVariables.TerrainGround, TerrainBrushCells(cx, cy))
        If painted = 0 Then Return
        ApplyBlendChanges(terrainBlend.Resolve(terrainSeed), $"Terrain Brush: {GlobalVariables.TerrainGroundName}")
    End Sub

    ''' <summary>
    ''' Turns op2maplib's changes into a TerrainBrushCommand, executes it
    ''' (through the undo system) and repaints the affected area.
    ''' </summary>
    Private Sub ApplyBlendChanges(changes As BlendChange(), description As String)
        If changes Is Nothing OrElse changes.Length = 0 Then Return

        Dim lockLastRow As Boolean = Not My.Settings.AllowEditLastRow
        Dim list As New List(Of TerrainTileChange)(changes.Length)
        Dim minX As Integer = Integer.MaxValue, minY As Integer = Integer.MaxValue
        Dim maxX As Integer = Integer.MinValue, maxY As Integer = Integer.MinValue
        For Each c In changes
            Dim oldCell As CellType = currentMap.GetCellType(c.X, c.Y)
            ' Use the profile's cell type for the new tile, except on the locked
            ' bottom row, which keeps its (Impassable) cell type.
            Dim newCell As CellType = oldCell
            If c.CellType >= 0 AndAlso c.CellType < 32 AndAlso
               Not (lockLastRow AndAlso c.Y = GlobalVariables.MapLastRowIndex) Then
                newCell = CType(c.CellType, CellType)
            End If
            list.Add(New TerrainTileChange With {
                .X = c.X, .Y = c.Y,
                .OldMapping = c.OldMapping, .NewMapping = c.NewMapping,
                .OldCellType = oldCell, .NewCellType = newCell})
            minX = Math.Min(minX, c.X) : maxX = Math.Max(maxX, c.X)
            minY = Math.Min(minY, c.Y) : maxY = Math.Max(maxY, c.Y)
        Next

        ExecuteCommand(New TerrainBrushCommand(currentMap, list, description))
        InvalidateTileRegion(minX, minY, maxX - minX + 1, maxY - minY + 1)
        If currentTileX >= minX AndAlso currentTileX <= maxX AndAlso
           currentTileY >= minY AndAlso currentTileY <= maxY Then
            UpdatePropertyControls()
        End If
    End Sub

    ''' <summary>
    ''' Re-picks every transition on the map ("Clean Up Transitions"): shows how
    ''' many tiles would change, and applies them as one undo step if confirmed.
    ''' </summary>
    Friend Sub TerrainCleanUpTransitions()
        If currentMap Is Nothing Then
            MessageBox.Show("No map loaded.", "Clean Up Transitions", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Not GlobalVariables.EditModeEnabled Then Return
        If IsRestrictedRenderMode() Then
            MessageBox.Show($"Cannot change tiles while in {CurrentRenderMode} Render Mode.",
                            "Operation Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim changes As BlendChange()
        Try
            Using blend As BlendMap = CreateTerrainBlendMap()
                If blend Is Nothing Then
                    MessageBox.Show("The terrain brush is unavailable: " & TerrainProfileError,
                                    "Clean Up Transitions", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
                changes = blend.ResolveAll(CULng(terrainRandom.Next()))
            End Using
        Catch ex As Exception
            MessageBox.Show("Clean Up Transitions failed: " & ex.Message, "Clean Up Transitions",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End Try

        If changes.Length = 0 Then
            MessageBox.Show("All transitions already fit; nothing to change.", "Clean Up Transitions",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If MessageBox.Show($"Clean Up Transitions will change {changes.Length} tile(s) so the borders between " &
                           "mud, rock and sand use matching transition tiles." & Environment.NewLine &
                           Environment.NewLine & "This can be undone. Continue?",
                           "Clean Up Transitions", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then
            Return
        End If

        EndEditStroke()
        ApplyBlendChanges(changes, $"Clean Up Transitions ({changes.Length} tiles)")
        pnlMap.Invalidate()
    End Sub

#Region "Draw Cliff"

    ''' <summary>Points of the cliff being drawn (cells), in click order.</summary>
    Private ReadOnly cliffPoints As New List(Of Point)
    ''' <summary>Where the next point would go (snapped), for the preview line. X = -1: none.</summary>
    Private cliffCursor As New Point(-1, -1)

    ''' <summary>Preview colours, as in op2-codegen-editor.</summary>
    Private Shared ReadOnly CliffPreviewColour As Color = Color.FromArgb(255, 210, 90)

    ''' <summary>
    ''' True when a left-click on the map adds a cliff point: Draw Cliff is on in
    ''' the Terrain Brush window and no tile or cell type has been picked since.
    ''' </summary>
    Friend Function IsCliffToolActive() As Boolean
        Return GlobalVariables.EditModeEnabled AndAlso
               GlobalVariables.CliffToolActive AndAlso
               GlobalVariables.ActiveTileMappingIndex < 0 AndAlso
               Not GlobalVariables.CellTypeToolActive
    End Function

    ''' <summary>Turns Draw Cliff on (from the Terrain Brush window). Leaves any other tool.</summary>
    Friend Sub ArmCliffTool()
        LeaveOtherToolsForTerrain()
        GlobalVariables.TerrainToolActive = False
        InvalidateTerrainOutline()
        terrainHoverX = -1
        terrainHoverY = -1
        GlobalVariables.CliffToolActive = True
        ClearCliffPoints()
        pnlMap.Cursor = Cursors.Cross
        UpdateToolAndPlacementLabels()
    End Sub

    ''' <summary>Turns Draw Cliff off, dropping any unplaced points.</summary>
    Friend Sub DisarmCliffTool()
        If Not GlobalVariables.CliffToolActive Then Return
        GlobalVariables.CliffToolActive = False
        ClearCliffPoints()
        If Not isPasteMode Then pnlMap.Cursor = Cursors.Default
        UpdateToolAndPlacementLabels()
    End Sub

    ''' <summary>The Terrain Brush window changed the high side or wall height.</summary>
    Friend Sub CliffSettingsChanged()
        UpdateToolAndPlacementLabels()
    End Sub

    Friend ReadOnly Property CliffPointCount As Integer
        Get
            Return cliffPoints.Count
        End Get
    End Property

    ''' <summary>Drops the unplaced points.</summary>
    Friend Sub ClearCliffPoints()
        If cliffPoints.Count = 0 AndAlso cliffCursor.X < 0 Then Return
        cliffPoints.Clear()
        cliffCursor = New Point(-1, -1)
        pnlMap.Invalidate()
        If GlobalVariables.CliffToolActive Then UpdateToolAndPlacementLabels()
    End Sub

    ''' <summary>Removes the last point (Backspace).</summary>
    Private Sub RemoveLastCliffPoint()
        If cliffPoints.Count = 0 Then Return
        cliffPoints.RemoveAt(cliffPoints.Count - 1)
        pnlMap.Invalidate()
        UpdateToolAndPlacementLabels()
    End Sub

    ''' <summary>Where a click at this tile would put the next point: snapped to a cliff slope after the first.</summary>
    Private Function SnapCliffTarget(mapX As Integer, mapY As Integer) As Point
        If cliffPoints.Count = 0 Then Return New Point(mapX, mapY)
        Return BlendProfile.SnapCliffPoint(cliffPoints(cliffPoints.Count - 1), New Point(mapX, mapY))
    End Function

    Private Function InsideMap(p As Point) As Boolean
        Return currentMap IsNot Nothing AndAlso p.X >= 0 AndAlso p.Y >= 0 AndAlso
               p.X < CInt(currentMap.WidthInTiles()) AndAlso p.Y < CInt(currentMap.HeightInTiles())
    End Function

    ''' <summary>
    ''' A left-click with Draw Cliff on: adds a point (snapped to an allowed
    ''' slope). Clicking the first point again closes the loop and places it.
    ''' </summary>
    Private Sub CliffClick(mapX As Integer, mapY As Integer)
        If IsRestrictedRenderMode() Then
            MessageBox.Show($"Cannot draw cliffs while in {CurrentRenderMode} Render Mode.",
                            "Operation Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim p As Point = SnapCliffTarget(mapX, mapY)
        If Not InsideMap(p) Then Return                           ' the snapped point fell off the map
        If cliffPoints.Count > 0 AndAlso p = cliffPoints(cliffPoints.Count - 1) Then Return
        cliffPoints.Add(p)
        pnlMap.Invalidate()
        ' Back at the start: a closed loop (a plateau), placed straight away.
        If cliffPoints.Count > 3 AndAlso p = cliffPoints(0) Then
            PlaceCliff()
            Return
        End If
        UpdateToolAndPlacementLabels()
    End Sub

    ''' <summary>Moves the preview line's loose end to the (snapped) tile under the mouse.</summary>
    Private Sub UpdateCliffCursor(mapX As Integer, mapY As Integer)
        If cliffPoints.Count = 0 Then Return
        Dim p As Point = SnapCliffTarget(mapX, mapY)
        If p = cliffCursor Then Return
        cliffCursor = p
        pnlMap.Invalidate()
    End Sub

    ''' <summary>
    ''' Builds the cliff along the points with op2maplib and applies it as one
    ''' undo step (Enter, Place Cliff, or closing a loop).
    ''' </summary>
    Friend Sub PlaceCliff()
        If currentMap Is Nothing OrElse Not IsCliffToolActive() Then Return
        If cliffPoints.Count < 2 Then
            MessageBox.Show("Click at least two points on the map first.", "Draw Cliff",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim changes As BlendChange()
        Dim stage As Integer
        Try
            Using blend As BlendMap = CreateTerrainBlendMap()
                If blend Is Nothing Then
                    MessageBox.Show("Cliffs are unavailable: " & TerrainProfileError, "Draw Cliff",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
                changes = blend.DrawCliff(cliffPoints, CType(GlobalVariables.CliffHighSide, CliffSide),
                                          GlobalVariables.CliffWallHeight, CULng(terrainRandom.Next()))
                stage = blend.LastCliffStage
            End Using
        Catch ex As Exception
            MessageBox.Show("Draw Cliff failed: " & ex.Message, "Draw Cliff", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End Try

        If changes.Length = 0 Then
            MessageBox.Show("No cliff could be built along these points.", "Draw Cliff",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Debug.WriteLine($"Draw Cliff: {changes.Length} tiles, solver stage {stage}")
        EndEditStroke()
        ApplyBlendChanges(changes, $"Draw Cliff ({changes.Length} tiles)")
        ClearCliffPoints()
        pnlMap.Invalidate()
        statusStrip.Items(0).Text = $"Cliff placed: {changes.Length} tiles" &
                                    If(stage < 0, " (rough joins: no exact fit found)", "")
    End Sub

    ''' <summary>
    ''' Handles Enter (place), Backspace (remove last point) and Esc (clear the
    ''' points, or stop Draw Cliff when there are none). True if handled.
    ''' </summary>
    Private Function HandleCliffKey(keyData As Keys) As Boolean
        If Not IsCliffToolActive() Then Return False
        Select Case keyData
            Case Keys.Enter
                PlaceCliff()
                Return True
            Case Keys.Back
                RemoveLastCliffPoint()
                Return True
            Case Keys.Escape
                If cliffPoints.Count > 0 Then
                    ClearCliffPoints()
                Else
                    DisarmCliffTool()
                    If terrainForm IsNot Nothing AndAlso Not terrainForm.IsDisposed Then terrainForm.SyncFromState()
                End If
                Return True
        End Select
        Return False
    End Function

    ''' <summary>Screen centre of a tile.</summary>
    Private Function TileCentre(p As Point) As PointF
        Dim r As Rectangle = MapToScreenCoordinates(p.X, p.Y)
        Return New PointF(r.X + r.Width / 2.0F, r.Y + r.Height / 2.0F)
    End Function

    ''' <summary>Draws the cliff points, the line between them, and a dashed line to the mouse.</summary>
    Private Sub DrawCliffPreview(g As Graphics)
        If Not IsCliffToolActive() OrElse cliffPoints.Count = 0 Then Return
        Dim tile As Single = 32 * zoomLevel
        Using shadow As New Pen(Color.Black, 5), line As New Pen(CliffPreviewColour, 3)
            For i As Integer = 0 To cliffPoints.Count - 2
                Dim a = TileCentre(cliffPoints(i)), b = TileCentre(cliffPoints(i + 1))
                g.DrawLine(shadow, a, b)
                g.DrawLine(line, a, b)
            Next
        End Using
        If cliffCursor.X >= 0 Then
            Using dash As New Pen(CliffPreviewColour, 2) With {.DashStyle = Drawing2D.DashStyle.Dash}
                g.DrawLine(dash, TileCentre(cliffPoints(cliffPoints.Count - 1)), TileCentre(cliffCursor))
            End Using
        End If
        Using box As New Pen(CliffPreviewColour, 3)
            For i As Integer = 0 To cliffPoints.Count - 1
                Dim c = TileCentre(cliffPoints(i))
                ' 2x2-tile square around the point; the first one is drawn double so a loop's start shows.
                Dim r As New RectangleF(c.X - tile, c.Y - tile, tile * 2, tile * 2)
                g.DrawRectangle(box, r.X, r.Y, r.Width, r.Height)
                If i = 0 AndAlso cliffPoints.Count > 1 Then
                    g.DrawRectangle(box, r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8)
                End If
            Next
        End Using
    End Sub

#End Region

#Region "Brush outline"

    ''' <summary>Largest brush size, used to erase an outline drawn at an older size.</summary>
    Private Const MaxTerrainOutlineSize As Integer = 25

    ''' <summary>Screen rectangle of the brush outline around a tile for a brush size.</summary>
    Private Function TerrainOutlineRect(x As Integer, y As Integer, size As Integer) As Rectangle
        Dim half As Integer = size \ 2
        Dim tl As Rectangle = MapToScreenCoordinates(x - half, y - half)
        Dim br As Rectangle = MapToScreenCoordinates(x + half, y + half)
        Return Rectangle.FromLTRB(tl.Left, tl.Top, br.Right, br.Bottom)
    End Function

    ''' <summary>Repaints where the brush outline is (or was, at previousSize).</summary>
    Private Sub InvalidateTerrainOutline(Optional previousSize As Integer = -1)
        If terrainHoverX < 0 OrElse terrainHoverY < 0 Then Return
        Dim size As Integer = If(previousSize > 0, previousSize, GlobalVariables.TerrainBrushSize)
        Dim r As Rectangle = TerrainOutlineRect(terrainHoverX, terrainHoverY, size)
        r.Inflate(3, 3)
        pnlMap.Invalidate(r)
    End Sub

    ''' <summary>Moves the brush outline to the tile under the mouse.</summary>
    Private Sub UpdateTerrainHover(mapX As Integer, mapY As Integer)
        If mapX = terrainHoverX AndAlso mapY = terrainHoverY Then Return
        InvalidateTerrainOutline()
        terrainHoverX = mapX
        terrainHoverY = mapY
        InvalidateTerrainOutline()
    End Sub

    ''' <summary>Draws the brush outline (a circle the size of the brush) in the map's Paint.</summary>
    Private Sub DrawTerrainOutline(g As Graphics)
        If Not IsTerrainToolActive() OrElse terrainHoverX < 0 Then Return
        Dim r As Rectangle = TerrainOutlineRect(terrainHoverX, terrainHoverY, GlobalVariables.TerrainBrushSize)
        Dim colour As Color = Color.White
        Dim profile = GetTerrainProfile()
        If profile IsNot Nothing Then colour = fTerrain.GroundColour(profile.GroundId(GlobalVariables.TerrainGround))
        Using shadow As New Pen(Color.Black, 3)
            g.DrawEllipse(shadow, r)
        End Using
        Using pen As New Pen(colour, 1.5F)
            g.DrawEllipse(pen, r)
        End Using
    End Sub

#End Region

#End Region

End Class
