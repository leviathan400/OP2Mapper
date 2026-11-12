Imports OP2UtilityDotNet.OP2Map

' fMiniMap - Tool 5
'
' Mini map window. Shows a photo-real thumbnail of the whole map (always in
' GameMap render mode, regardless of the editor's current mode) with a
' viewport-rectangle overlay marking what fMain is currently showing.
' Click or drag inside the thumbnail to jump fMain to that location.

Public Class fMiniMap

    ''' <summary>
    ''' Raised when the user clicks (or drags) inside the thumbnail. Tile
    ''' coordinates are in 0-based map space. fMain pans to centre this tile.
    ''' </summary>
    Public Event JumpToTile(tileX As Integer, tileY As Integer)

    ' Cached thumbnail bitmap (rendered by RefreshThumbnail).
    Private _thumbnail As Bitmap = Nothing
    ' Where the thumbnail is drawn inside the client area (lets us letterbox
    ' for non-square maps without distorting the image).
    Private _thumbBounds As Rectangle = Rectangle.Empty
    ' Visible tile rect in fMain's panel — drawn as a yellow overlay.
    Private _viewportTileRect As Rectangle = Rectangle.Empty
    ' Cached map dimensions so click→tile conversion doesn't need the Map.
    Private _mapWidthInTiles As Integer = 0
    Private _mapHeightInTiles As Integer = 0
    ' Drag state for click-and-drag scrub.
    Private _isDragging As Boolean = False

#Region "Public API"

    ''' <summary>
    ''' Re-renders the thumbnail at a scale that fits the current client area
    ''' while preserving aspect ratio. Always uses GameMap mode with overlays
    ''' off, regardless of the renderer's current state — state is saved and
    ''' restored.
    ''' </summary>
    Public Sub RefreshThumbnail(map As Map, renderer As MapRenderer)
        If map Is Nothing OrElse renderer Is Nothing Then
            ClearThumbnail()
            Return
        End If

        _mapWidthInTiles = CInt(map.WidthInTiles())
        _mapHeightInTiles = CInt(map.HeightInTiles())

        Dim mapPxW As Integer = _mapWidthInTiles * 32
        Dim mapPxH As Integer = _mapHeightInTiles * 32
        Dim cw As Integer = ClientSize.Width
        Dim ch As Integer = ClientSize.Height
        If cw <= 0 OrElse ch <= 0 OrElse mapPxW <= 0 OrElse mapPxH <= 0 Then Return

        ' Fit-to-panel scale (what we'll display at). Preserves aspect ratio.
        Dim displayScale As Single = Math.Min(cw / CSng(mapPxW), ch / CSng(mapPxH))
        If displayScale <= 0 Then Return

        ' Pick the SMALLEST integer pixels-per-tile that's at least as large as
        ' the display scale. Rendering at integer ppt means every tile draws
        ' at integer pixel boundaries inside MapRenderer — no sub-pixel rounding
        ' gaps that produce thin black lines at low scales. The form's OnPaint
        ' then DrawImage-scales this slightly-oversized bitmap down to fit.
        Dim ppt As Integer = Math.Max(1, CInt(Math.Ceiling(displayScale * 32)))
        Dim renderScale As Single = ppt / 32.0F

        ' Mini-map is always the photo-real GameMap view — save the renderer's
        ' current state, force GameMap + no overlays, render, then restore.
        Dim savedMode = renderer.RenderMode
        Dim savedShowGrid = renderer.ShowGrid
        Dim savedShowLargeGrid = renderer.ShowLargeGrid
        Dim savedShowCoordinates = renderer.ShowCoordinates

        Try
            renderer.RenderMode = MapRenderer.MapRenderMode.GameMap
            renderer.ShowGrid = False
            renderer.ShowLargeGrid = False
            renderer.ShowCoordinates = False

            Dim newThumb As Bitmap = renderer.RenderFullMap(renderScale)
            If newThumb IsNot Nothing Then
                If _thumbnail IsNot Nothing Then _thumbnail.Dispose()
                _thumbnail = newThumb

                ' Compute the on-screen rectangle (the form may be bigger or
                ' smaller than the rendered bitmap — OnPaint will scale).
                Dim displayW As Integer = CInt(mapPxW * displayScale)
                Dim displayH As Integer = CInt(mapPxH * displayScale)
                _thumbBounds = New Rectangle(
                    (cw - displayW) \ 2,
                    (ch - displayH) \ 2,
                    displayW,
                    displayH)
            End If
        Finally
            renderer.RenderMode = savedMode
            renderer.ShowGrid = savedShowGrid
            renderer.ShowLargeGrid = savedShowLargeGrid
            renderer.ShowCoordinates = savedShowCoordinates
        End Try

        Invalidate()
    End Sub

    ''' <summary>
    ''' Updates the viewport rectangle overlay (in map-tile coordinates).
    ''' Cheap — no re-render, just stores and invalidates.
    ''' </summary>
    Public Sub UpdateViewport(visibleTileRect As Rectangle)
        If _viewportTileRect = visibleTileRect Then Return
        _viewportTileRect = visibleTileRect
        Invalidate()
    End Sub

    ''' <summary>
    ''' Drops the cached thumbnail (e.g. after Close Map).
    ''' </summary>
    Public Sub ClearThumbnail()
        If _thumbnail IsNot Nothing Then
            _thumbnail.Dispose()
            _thumbnail = Nothing
        End If
        _viewportTileRect = Rectangle.Empty
        _thumbBounds = Rectangle.Empty
        _mapWidthInTiles = 0
        _mapHeightInTiles = 0
        Invalidate()
    End Sub

#End Region

#Region "Painting"

    ''' <summary>
    ''' Paints the cached thumbnail (scaled to fit) and the yellow viewport overlay.
    ''' </summary>
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g = e.Graphics
        g.Clear(System.Drawing.Color.Black)
        If _thumbnail Is Nothing OrElse _thumbBounds.IsEmpty Then Return

        ' Source bitmap is rendered at integer pixels-per-tile, then scaled
        ' here to fit. NearestNeighbor keeps tile edges sharp instead of
        ' smearing them with bilinear filtering.
        g.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
        g.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half
        g.DrawImage(_thumbnail, _thumbBounds)

        ' Viewport overlay — yellow rectangle marking what fMain is showing.
        If _viewportTileRect.Width > 0 AndAlso _viewportTileRect.Height > 0 AndAlso
           _mapWidthInTiles > 0 AndAlso _mapHeightInTiles > 0 Then

            Dim sx As Single = _thumbBounds.Width / CSng(_mapWidthInTiles)
            Dim sy As Single = _thumbBounds.Height / CSng(_mapHeightInTiles)

            Dim rx As Integer = _thumbBounds.X + CInt(_viewportTileRect.X * sx)
            Dim ry As Integer = _thumbBounds.Y + CInt(_viewportTileRect.Y * sy)
            Dim rw As Integer = Math.Max(1, CInt(_viewportTileRect.Width * sx))
            Dim rh As Integer = Math.Max(1, CInt(_viewportTileRect.Height * sy))

            Using pen As New Pen(System.Drawing.Color.Yellow, 2)
                g.DrawRectangle(pen, rx, ry, rw, rh)
            End Using
        End If
    End Sub

    ''' <summary>
    ''' Re-fits the existing thumbnail to the new client size (no re-render).
    ''' </summary>
    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        ' Re-fit the existing thumbnail to the new client size by recomputing
        ' _thumbBounds. (Full re-render at the new resolution is the caller's
        ' job via RefreshThumbnail — we don't have a Map reference here.)
        If _thumbnail Is Nothing Then Return

        Dim cw = ClientSize.Width
        Dim ch = ClientSize.Height
        If cw <= 0 OrElse ch <= 0 Then Return

        Dim scale As Single = Math.Min(cw / CSng(_thumbnail.Width), ch / CSng(_thumbnail.Height))
        Dim newW = CInt(_thumbnail.Width * scale)
        Dim newH = CInt(_thumbnail.Height * scale)
        _thumbBounds = New Rectangle((cw - newW) \ 2, (ch - newH) \ 2, newW, newH)
        Invalidate()
    End Sub

#End Region

#Region "Click / Drag"

    ''' <summary>
    ''' Converts a mouse position to clamped tile coordinates and raises JumpToTile.
    ''' </summary>
    Private Sub HandleClickOrDrag(e As MouseEventArgs)
        If _thumbnail Is Nothing OrElse _thumbBounds.IsEmpty OrElse
           _mapWidthInTiles <= 0 OrElse _mapHeightInTiles <= 0 Then Return

        ' Convert click position to tile coords (clamped to map bounds so a
        ' drag that exits the thumbnail still produces a valid tile).
        Dim relX As Single = (e.X - _thumbBounds.X) / CSng(_thumbBounds.Width)
        Dim relY As Single = (e.Y - _thumbBounds.Y) / CSng(_thumbBounds.Height)
        Dim tx As Integer = CInt(relX * _mapWidthInTiles)
        Dim ty As Integer = CInt(relY * _mapHeightInTiles)
        tx = Math.Max(0, Math.Min(_mapWidthInTiles - 1, tx))
        ty = Math.Max(0, Math.Min(_mapHeightInTiles - 1, ty))

        RaiseEvent JumpToTile(tx, ty)
    End Sub

    ''' <summary>
    ''' Left button starts a drag-scrub and jumps to the clicked tile.
    ''' </summary>
    Private Sub fMiniMap_MouseDown(sender As Object, e As MouseEventArgs) Handles Me.MouseDown
        If e.Button = MouseButtons.Left Then
            _isDragging = True
            HandleClickOrDrag(e)
        End If
    End Sub

    ''' <summary>
    ''' While dragging, keeps jumping to the tile under the cursor.
    ''' </summary>
    Private Sub fMiniMap_MouseMove(sender As Object, e As MouseEventArgs) Handles Me.MouseMove
        If _isDragging Then HandleClickOrDrag(e)
    End Sub

    ''' <summary>
    ''' Releasing the left button ends the drag-scrub.
    ''' </summary>
    Private Sub fMiniMap_MouseUp(sender As Object, e As MouseEventArgs) Handles Me.MouseUp
        If e.Button = MouseButtons.Left Then _isDragging = False
    End Sub

#End Region

#Region "Window Snapping"

    Private Const WM_MOVING As Integer = &H216

    ''' <summary>
    ''' Intercepts WM_MOVING to snap the window edges via ToolPaletteHelper.
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

#Region "Lifecycle"

    ''' <summary>
    ''' Configures the window as a tool palette and restores its saved position.
    ''' </summary>
    Private Sub fMiniMap_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Reduce flicker — repainting happens every pan/zoom/edit.
        Me.DoubleBuffered = True
        Me.Text = "Mini Map"
        ToolPaletteHelper.ConfigureAsToolPalette(Me)
        ToolPaletteHelper.RestorePosition(Me, "MiniMap")
    End Sub

    ''' <summary>
    ''' Saves the window position and releases the cached thumbnail.
    ''' </summary>
    Private Sub fMiniMap_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        ToolPaletteHelper.SavePosition(Me, "MiniMap")
        ToolPaletteHelper.UnregisterToolForm(Me)
        ClearThumbnail()
    End Sub

#End Region

End Class
