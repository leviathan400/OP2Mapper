Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports OP2UtilityDotNet
Imports OP2UtilityDotNet.OP2Map
Imports OP2UtilityDotNet.Bitmap
Imports OP2UtilityDotNet.Sprite


''' <summary>
''' High-performance renderer for Outpost 2 maps with flexible display options
''' </summary>
Public Class MapRenderer
    Implements IDisposable

    ' Define an internal RenderMode enum to avoid exposing GlobalVariables
    Public Enum MapRenderMode
        GameMap            ' 1  Game map view with well BMPs (default)
        CellTypeOverlay    ' 2  Cell types overlaid on game map
        CellTypeOverlayAll ' 3  Cell types with detailed text labels overlaid on game map
        CellTypeBasic      ' 4  Basic cell type coloring
        CellTypeAll        ' 5  Detailed cell type coloring
    End Enum

    ' Grid and Coordinate renders

    ''' <summary>
    ''' Whether to draw the large grid overlay (see RenderLargeGrid).
    ''' </summary>
    Public Property ShowLargeGrid As Boolean = False
    ''' <summary>
    ''' Spacing of the large grid, in tiles.
    ''' </summary>
    Public Property LargeGridSize As Integer = 32
    ''' <summary>
    ''' Whether to draw coordinate labels (see RenderCoordinates).
    ''' </summary>
    Public Property ShowCoordinates As Boolean = False
    ''' <summary>
    ''' Spacing of coordinate labels, in tiles.
    ''' </summary>
    Public Property CoordinateSpacing As Integer = 32

    ' Constants
    Private Const DefaultTileSize As Integer = 32

    ' Properties
    Private _map As Map
    Private _resourceManager As ResourceManager
    Private _tilesetImages As Dictionary(Of String, List(Of Bitmap))
    Private _tileCache As New Dictionary(Of UInteger, Bitmap)
    Private _fallbackTileCache As New Dictionary(Of String, Bitmap)

    ' Rendering options
    Private _renderMode As MapRenderMode = MapRenderMode.GameMap
    Private _showGrid As Boolean = False
    Private _tileSize As Integer = DefaultTileSize

    ' Constructor with render mode option
    ''' <summary>
    ''' Creates a renderer for the given map and loads its tilesets if the map is not Nothing.
    ''' </summary>
    Public Sub New(map As Map, resourceManager As ResourceManager, Optional renderMode As MapRenderMode = MapRenderMode.GameMap)
        _map = map
        _resourceManager = resourceManager
        _renderMode = renderMode
        _tilesetImages = New Dictionary(Of String, List(Of Bitmap))

        ' If map is provided, load tilesets
        If map IsNot Nothing Then
            LoadMapTilesets()
        End If
    End Sub

    ' Properties to control rendering behavior

    ''' <summary>
    ''' The active render mode (game map, cell-type overlays, etc.).
    ''' </summary>
    Public Property RenderMode As MapRenderMode
        Get
            Return _renderMode
        End Get
        Set(value As MapRenderMode)
            _renderMode = value
        End Set
    End Property

    ''' <summary>
    ''' Whether to draw the regular (per-tile) grid overlay.
    ''' </summary>
    Public Property ShowGrid As Boolean
        Get
            Return _showGrid
        End Get
        Set(value As Boolean)
            _showGrid = value
        End Set
    End Property

    ''' <summary>
    ''' Tile size in pixels used for rendering (default 32).
    ''' </summary>
    Public Property TileSize As Integer
        Get
            Return _tileSize
        End Get
        Set(value As Integer)
            _tileSize = value
        End Set
    End Property

    ' Methods for rendering

    ''' <summary>
    ''' Renders the given viewport (in unscaled map-pixel coordinates) to a NEW Bitmap.
    ''' The caller OWNS the returned Bitmap and must dispose it.
    ''' </summary>
    Public Function RenderMap(viewportRect As Rectangle, Optional scale As Single = 1.0F) As Bitmap
        ' Create a bitmap to render the map section to
        Dim scaledWidth As Integer = CInt(viewportRect.Width * scale)
        Dim scaledHeight As Integer = CInt(viewportRect.Height * scale)
        Dim bmp As New Bitmap(scaledWidth, scaledHeight)

        Using g As Graphics = Graphics.FromImage(bmp)
            ' Set quality options
            g.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half

            ' Clear background
            g.Clear(System.Drawing.Color.Black)

            ' Calculate buffer size based on zoom level
            ' Increase buffer when zoomed out, decrease when zoomed in
            Dim baseBufferSize As Integer = 5
            Dim adjustedBufferSize As Integer = CInt(baseBufferSize * (1.0F / Math.Max(scale, 0.1F)))

            ' Ensure minimum buffer size
            adjustedBufferSize = Math.Max(adjustedBufferSize, baseBufferSize)

            ' Calculate visible tiles with dynamic buffer.
            ' viewportRect is in *unscaled* map-pixel coordinates (see how the
            ' per-tile draw at RenderGameMapTiles uses ((x * _tileSize) - viewportRect.X) * scale),
            ' so the tile-index conversion divides by _tileSize only — not by _tileSize * scale.
            Dim startX As Integer = Math.Max(0, CInt(Math.Floor(viewportRect.X / _tileSize)) - adjustedBufferSize)
            Dim startY As Integer = Math.Max(0, CInt(Math.Floor(viewportRect.Y / _tileSize)) - adjustedBufferSize)
            Dim endX As Integer = Math.Min(CInt(_map.WidthInTiles()) - 1, CInt(Math.Ceiling(viewportRect.Right / _tileSize)) + adjustedBufferSize)
            Dim endY As Integer = Math.Min(CInt(_map.HeightInTiles()) - 1, CInt(Math.Ceiling(viewportRect.Bottom / _tileSize)) + adjustedBufferSize)

            ' Render based on selected mode
            Select Case _renderMode
                Case MapRenderMode.GameMap
                    RenderGameMapTiles(g, startX, startY, endX, endY, viewportRect, scale)
                Case MapRenderMode.CellTypeOverlay
                    RenderGameMapTiles(g, startX, startY, endX, endY, viewportRect, scale)
                    RenderCellTypeOverlay(g, startX, startY, endX, endY, viewportRect, scale)
                Case MapRenderMode.CellTypeOverlayAll
                    RenderGameMapTiles(g, startX, startY, endX, endY, viewportRect, scale)
                    RenderCellTypeOverlayWithText(g, startX, startY, endX, endY, viewportRect, scale)
                Case MapRenderMode.CellTypeBasic
                    RenderCellTypeBasic(g, startX, startY, endX, endY, viewportRect, scale)
                Case MapRenderMode.CellTypeAll
                    RenderCellTypeDetailed(g, startX, startY, endX, endY, viewportRect, scale)
            End Select

            ' Draw regular grid if enabled
            If _showGrid Then
                RenderGrid(g, startX, startY, endX, endY, viewportRect, scale)
            End If

            ' Draw large grid if enabled
            If ShowLargeGrid Then
                RenderLargeGrid(g, startX, startY, endX, endY, viewportRect, scale)
            End If

            ' Draw coordinates if enabled
            If ShowCoordinates Then
                RenderCoordinates(g, startX, startY, endX, endY, viewportRect, scale)
            End If
        End Using

        Return bmp
    End Function


    ''' <summary>
    ''' Renders the entire map to a NEW Bitmap (via RenderMap). The caller OWNS the
    ''' returned Bitmap and must dispose it. Returns Nothing if no map is loaded.
    ''' </summary>
    Public Function RenderFullMap(Optional scale As Single = 1.0F) As Bitmap
        If _map Is Nothing Then Return Nothing

        ' Calculate map size
        Dim mapWidth As Integer = CInt(_map.WidthInTiles() * _tileSize)
        Dim mapHeight As Integer = CInt(_map.HeightInTiles() * _tileSize)

        ' Create the viewport rectangle for the full map
        Dim viewport As New Rectangle(0, 0, mapWidth, mapHeight)

        ' Render the full map
        Return RenderMap(viewport, scale)
    End Function

    ''' <summary>
    ''' Renders a single tile to a NEW Bitmap. The caller OWNS the returned Bitmap
    ''' and must dispose it. Returns Nothing if the coordinates are out of range.
    ''' </summary>
    Public Function RenderTile(x As Integer, y As Integer, Optional size As Integer = -1) As Bitmap
        If _map Is Nothing OrElse x < 0 OrElse y < 0 OrElse
           x >= _map.WidthInTiles() OrElse y >= _map.HeightInTiles() Then
            Return Nothing
        End If

        Dim tileSize As Integer = If(size > 0, size, _tileSize)
        Dim bmp As New Bitmap(tileSize, tileSize)

        Using g As Graphics = Graphics.FromImage(bmp)
            ' Clear background
            g.Clear(System.Drawing.Color.Black)

            ' Calculate the rectangle for this tile
            Dim tileRect As New Rectangle(0, 0, tileSize, tileSize)

            ' Get tile data
            Dim tileData As TileData = TileData.FromMap(_map, x, y)

            ' Render based on mode
            Select Case _renderMode
                Case MapRenderMode.GameMap
                    ' Get the game tile image
                    Dim tileImage As Bitmap = GetTileImage(x, y)
                    If tileImage IsNot Nothing Then
                        g.DrawImage(tileImage, tileRect)
                    End If

                Case MapRenderMode.CellTypeBasic
                    ' Fill with cell type color
                    Dim cellType As CellType = _map.GetCellType(x, y)
                    Dim cellColor As System.Drawing.Color = GetCellTypeColor(cellType)
                    g.FillRectangle(New SolidBrush(cellColor), tileRect)

                    ' Add cases for other render modes as needed
            End Select
        End Using

        Return bmp
    End Function

    ' Private rendering helpers

    ''' <summary>
    ''' Draws the tile images for the given tile range onto g (GameMap view).
    ''' </summary>
    Private Sub RenderGameMapTiles(g As Graphics, startX As Integer, startY As Integer, endX As Integer, endY As Integer,
                          viewportRect As Rectangle, scale As Single)
        ' Ensure we're drawing all tiles in the calculated range
        For y As Integer = startY To endY
            For x As Integer = startX To endX
                ' Calculate the position in the output bitmap with more precise positioning
                ' Use single-precision math throughout to avoid rounding errors
                Dim tileX As Single = ((x * _tileSize) - viewportRect.X) * scale
                Dim tileY As Single = ((y * _tileSize) - viewportRect.Y) * scale
                Dim scaledTileSize As Single = _tileSize * scale

                ' Define the destination rectangle with explicit conversion to integers
                Dim destRect As New RectangleF(tileX, tileY, scaledTileSize, scaledTileSize)

                ' Get the tile image
                Dim tileImage As Bitmap = GetTileImage(x, y)

                ' Draw the tile if it's in view and we have an image
                If tileImage IsNot Nothing Then
                    ' Use DrawImageUnscaled for better performance at exact scales
                    If scale = 1.0F Then
                        g.DrawImageUnscaled(tileImage, CInt(tileX), CInt(tileY))
                    Else
                        ' Otherwise draw with scaling
                        g.DrawImage(tileImage, destRect)
                    End If
                End If
            Next
        Next
    End Sub

    ''' <summary>
    ''' Draws a translucent cell-type color over each tile (CellTypeOverlay mode).
    ''' </summary>
    Private Sub RenderCellTypeOverlay(g As Graphics, startX As Integer, startY As Integer, endX As Integer, endY As Integer,
                                     viewportRect As Rectangle, scale As Single)
        ' The previous implementation rendered each tile by creating a fresh
        ' opaque Bitmap, painting it with a SolidBrush, then blitting it through
        ' an ImageAttributes / ColorMatrix with Matrix33 = 0.45 to get 45% alpha.
        ' For an opaque source, that final alpha is 255 * 0.45 ≈ 115, which we
        ' can produce directly with Color.FromArgb(115, ...) and a single
        ' FillRectangle — no per-tile Bitmap, Graphics, or ImageAttributes.
        Const overlayAlpha As Integer = 115

        For y As Integer = startY To endY
            For x As Integer = startX To endX
                Dim tileX As Integer = CInt((x * _tileSize - viewportRect.X) * scale)
                Dim tileY As Integer = CInt((y * _tileSize - viewportRect.Y) * scale)
                Dim scaledTileSize As Integer = CInt(_tileSize * scale)

                Dim cellType As CellType = _map.GetCellType(x, y)
                Dim baseColor As System.Drawing.Color = GetOverlayColorForCellType(cellType)
                Dim translucent As System.Drawing.Color = System.Drawing.Color.FromArgb(overlayAlpha, baseColor)

                Using brush As New SolidBrush(translucent)
                    g.FillRectangle(brush, tileX, tileY, scaledTileSize, scaledTileSize)
                End Using
            Next
        Next
    End Sub

    ''' <summary>
    ''' Draws an outlined cell-type text label on each tile (CellTypeOverlayAll mode).
    ''' </summary>
    Private Sub RenderCellTypeOverlayWithText(g As Graphics, startX As Integer, startY As Integer, endX As Integer, endY As Integer,
                                            viewportRect As Rectangle, scale As Single)
        ' Font size depends only on scale (constant for the whole call), so the
        ' Font can be created once and reused for every tile. Same for the
        ' scaled tile-size constant.
        Dim fontSize As Single = Math.Max(7.0F * scale, 6.0F)
        Dim scaledTileSize As Integer = CInt(_tileSize * scale)

        Using font As New Font("Arial", fontSize, FontStyle.Bold)
            For y As Integer = startY To endY
                For x As Integer = startX To endX
                    Dim tileX As Integer = CInt((x * _tileSize - viewportRect.X) * scale)
                    Dim tileY As Integer = CInt((y * _tileSize - viewportRect.Y) * scale)

                    ' Get cell type and label info
                    Dim cellType As CellType = _map.GetCellType(x, y)
                    Dim labelInfo As Tuple(Of String, System.Drawing.Color) = GetCellTypeLabel(cellType)
                    Dim label As String = labelInfo.Item1
                    Dim textColor As System.Drawing.Color = labelInfo.Item2

                    Dim textSize As SizeF = g.MeasureString(label, font)
                    Dim xPos As Single = tileX + (scaledTileSize - textSize.Width) / 2
                    Dim yPos As Single = tileY + (scaledTileSize - textSize.Height) / 2

                    ' Add dark outline for better readability
                    For offsetX As Integer = -1 To 1
                        For offsetY As Integer = -1 To 1
                            If Not (offsetX = 0 AndAlso offsetY = 0) Then
                                g.DrawString(label, font, Brushes.Black, xPos + offsetX, yPos + offsetY)
                            End If
                        Next
                    Next

                    ' Draw actual text — Using-wrap so the per-tile brush is
                    ' deterministically disposed instead of leaked.
                    Using textBrush As New SolidBrush(textColor)
                        g.DrawString(label, font, textBrush, xPos, yPos)
                    End Using
                Next
            Next
        End Using
    End Sub

    ''' <summary>
    ''' Fills each tile with a flat cell-type color plus border (CellTypeBasic mode).
    ''' </summary>
    Private Sub RenderCellTypeBasic(g As Graphics, startX As Integer, startY As Integer, endX As Integer, endY As Integer,
                                   viewportRect As Rectangle, scale As Single)
        For y As Integer = startY To endY
            For x As Integer = startX To endX
                Dim tileX As Integer = CInt((x * _tileSize - viewportRect.X) * scale)
                Dim tileY As Integer = CInt((y * _tileSize - viewportRect.Y) * scale)
                Dim scaledTileSize As Integer = CInt(_tileSize * scale)

                ' Get cell type color
                Dim cellType As CellType = _map.GetCellType(x, y)
                Dim tileColor As System.Drawing.Color = GetCellTypeColor(cellType)

                ' Draw colored tile
                Using brush As New SolidBrush(tileColor)
                    g.FillRectangle(brush, tileX, tileY, scaledTileSize, scaledTileSize)
                End Using
                g.DrawRectangle(Pens.DarkGray, tileX, tileY, scaledTileSize, scaledTileSize)
            Next
        Next
    End Sub

    ''' <summary>
    ''' Fills each tile with a detailed cell-type color and, when zoomed in enough,
    ''' a short type-name label (CellTypeAll mode).
    ''' </summary>
    Private Sub RenderCellTypeDetailed(g As Graphics, startX As Integer, startY As Integer, endX As Integer, endY As Integer,
                                      viewportRect As Rectangle, scale As Single)
        ' scaledTileSize and the text-Font are both scale-dependent only — hoist
        ' them. drawText decides up-front whether we'll bother with labels at
        ' all (skipped when zoomed out enough that text would be unreadable).
        Dim scaledTileSize As Integer = CInt(_tileSize * scale)
        Dim drawText As Boolean = scale >= 0.5F
        Dim textY As Single = 12 * scale
        Dim textFont As Font = Nothing
        If drawText Then
            textFont = New Font("Arial", 7 * scale)
        End If

        Try
            For y As Integer = startY To endY
                For x As Integer = startX To endX
                    Dim tileX As Integer = CInt((x * _tileSize - viewportRect.X) * scale)
                    Dim tileY As Integer = CInt((y * _tileSize - viewportRect.Y) * scale)

                    Dim cellType As CellType = _map.GetCellType(x, y)
                    Dim tileColor As System.Drawing.Color = GetDetailedColorForCellType(cellType)

                    Using fillBrush As New SolidBrush(tileColor)
                        g.FillRectangle(fillBrush, tileX, tileY, scaledTileSize, scaledTileSize)
                    End Using
                    g.DrawRectangle(Pens.DarkGray, tileX, tileY, scaledTileSize, scaledTileSize)

                    If drawText Then
                        Dim typeName As String = cellType.ToString()
                        If typeName.Length > 6 Then
                            typeName = typeName.Substring(0, 6)
                        End If
                        g.DrawString(typeName, textFont, Brushes.White, tileX + 2, tileY + textY)
                    End If
                Next
            Next
        Finally
            If textFont IsNot Nothing Then textFont.Dispose()
        End Try
    End Sub

    ''' <summary>
    ''' Draws the regular per-tile grid lines over the rendered area.
    ''' </summary>
    Private Sub RenderGrid(g As Graphics, startX As Integer, startY As Integer, endX As Integer, endY As Integer,
                         viewportRect As Rectangle, scale As Single)

        ' Create a thin pen for grid lines
        'Using gridPen As New Pen(System.Drawing.Color.FromArgb(100, 100, 100), 1)  'Dark Gray
        Using gridPen As New Pen(System.Drawing.Color.Black, 1)                     'Black
            ' Draw vertical grid lines
            For x As Integer = startX To endX + 1
                Dim xPos As Integer = CInt((x * _tileSize - viewportRect.X) * scale)
                g.DrawLine(gridPen, xPos, 0, xPos, g.VisibleClipBounds.Height)
            Next

            ' Draw horizontal grid lines
            For y As Integer = startY To endY + 1
                Dim yPos As Integer = CInt((y * _tileSize - viewportRect.Y) * scale)
                g.DrawLine(gridPen, 0, yPos, g.VisibleClipBounds.Width, yPos)
            Next
        End Using
    End Sub




    ''' <summary>
    ''' Optimized rendering for tiles of the same type
    ''' </summary>
    Private Sub RenderTileBatch(g As Graphics, tiles As List(Of Point), tileImage As Bitmap,
                              viewportRect As Rectangle, scale As Single)
        ' Skip if no image or no tiles
        If tileImage Is Nothing OrElse tiles.Count = 0 Then Return

        ' Draw all tiles of the same type at once
        For Each tile In tiles
            Dim x As Integer = tile.X
            Dim y As Integer = tile.Y

            ' Calculate the position in the output bitmap
            Dim tileX As Single = (x * _tileSize * scale) - (viewportRect.X * scale)
            Dim tileY As Single = (y * _tileSize * scale) - (viewportRect.Y * scale)
            Dim scaledTileSize As Single = _tileSize * scale

            ' Draw the tile
            g.DrawImage(tileImage, tileX, tileY, scaledTileSize, scaledTileSize)
        Next
    End Sub


    ''' <summary>
    ''' Renders a large grid at specified spacing, clipped to map boundaries
    ''' </summary>
    Private Sub RenderLargeGrid(g As Graphics, startX As Integer, startY As Integer, endX As Integer, endY As Integer,
                          viewportRect As Rectangle, scale As Single)
        If Not ShowLargeGrid Then Return

        ' Calculate the first grid line positions
        Dim firstGridX As Integer = CInt(Math.Floor(startX / LargeGridSize) * LargeGridSize)
        Dim firstGridY As Integer = CInt(Math.Floor(startY / LargeGridSize) * LargeGridSize)

        ' Calculate map boundaries in screen coordinates
        Dim mapWidth As Integer = CInt(_map.WidthInTiles())
        Dim mapHeight As Integer = CInt(_map.HeightInTiles())

        Dim mapLeft As Single = (0 - viewportRect.X) * scale
        Dim mapTop As Single = (0 - viewportRect.Y) * scale
        Dim mapRight As Single = ((mapWidth * _tileSize) - viewportRect.X) * scale
        Dim mapBottom As Single = ((mapHeight * _tileSize) - viewportRect.Y) * scale

        ' Create a thicker pen for large grid lines
        Using gridPen As New Pen(System.Drawing.Color.FromArgb(180, 60, 60, 180), 2)
            ' Draw vertical grid lines (clipped to map boundaries)
            For x As Integer = firstGridX To endX Step LargeGridSize
                ' Calculate screen position
                Dim xPos As Single = (x * _tileSize - viewportRect.X) * scale

                ' Only draw if within map boundaries
                If xPos >= mapLeft AndAlso xPos <= mapRight Then
                    g.DrawLine(gridPen, xPos, mapTop, xPos, mapBottom)
                End If
            Next

            ' Draw horizontal grid lines (clipped to map boundaries)
            For y As Integer = firstGridY To endY Step LargeGridSize
                ' Calculate screen position
                Dim yPos As Single = (y * _tileSize - viewportRect.Y) * scale

                ' Only draw if within map boundaries
                If yPos >= mapTop AndAlso yPos <= mapBottom Then
                    g.DrawLine(gridPen, mapLeft, yPos, mapRight, yPos)
                End If
            Next
        End Using
    End Sub



    ''' <summary>
    ''' Renders coordinate labels at specified spacing, clipped to map boundaries
    ''' </summary>
    Private Sub RenderCoordinates(g As Graphics, startX As Integer, startY As Integer, endX As Integer, endY As Integer,
                                viewportRect As Rectangle, scale As Single)
        If Not ShowCoordinates Then Return

        ' Calculate map boundaries
        Dim mapWidth As Integer = CInt(_map.WidthInTiles())
        Dim mapHeight As Integer = CInt(_map.HeightInTiles())

        ' Calculate the positions for coordinate labels
        Dim firstLabelX As Integer = CInt(Math.Floor(startX / CoordinateSpacing) * CoordinateSpacing)
        Dim firstLabelY As Integer = CInt(Math.Floor(startY / CoordinateSpacing) * CoordinateSpacing)

        ' Set up font and brush for coordinate text
        Using font As New Font("Arial", Math.Max(8 * scale, 7), FontStyle.Bold)
            Using textBrush As New SolidBrush(System.Drawing.Color.FromArgb(200, 255, 255, 255))
                Using outlineBrush As New SolidBrush(System.Drawing.Color.FromArgb(200, 0, 0, 0))
                    ' Draw coordinate labels at each grid intersection
                    For x As Integer = firstLabelX To endX Step CoordinateSpacing
                        For y As Integer = firstLabelY To endY Step CoordinateSpacing
                            ' Skip if outside map boundaries
                            If x < 0 OrElse y < 0 OrElse x >= mapWidth OrElse y >= mapHeight Then
                                Continue For
                            End If

                            ' Calculate position for the text
                            Dim xPos As Single = (x * _tileSize - viewportRect.X) * scale
                            Dim yPos As Single = (y * _tileSize - viewportRect.Y) * scale

                            ' Create coordinate text
                            Dim coordText As String = x + 1 & ", " & y + 1

                            ' Draw text with outline for better visibility
                            For offsetX As Integer = -1 To 1
                                For offsetY As Integer = -1 To 1
                                    g.DrawString(coordText, font, outlineBrush, xPos + offsetX, yPos + offsetY)
                                Next
                            Next

                            ' Draw the actual text
                            g.DrawString(coordText, font, textBrush, xPos, yPos)
                        Next
                    Next
                End Using
            End Using
        End Using
    End Sub


End Class
