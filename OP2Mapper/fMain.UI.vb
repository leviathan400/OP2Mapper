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

' fMain.UI.vb
' Partial class of fMain (split out of fMain.vb).
' UI refresh helpers - status bar, labels and other controls that mirror editor state.
Partial Public Class fMain

#Region "UI Update Methods"


    ''' <summary>
    ''' Refreshes the tile property labels (tileset, indices, coordinates, cell type)
    ''' and the tile preview images for the currently selected tile.
    ''' </summary>
    Private Sub UpdatePropertyControls()
        If currentMap Is Nothing OrElse currentTileX < 0 OrElse currentTileY < 0 Then
            Return
        End If

        Try
            ' Get the tile data using our TileData class
            Dim tileData As TileData = TileData.FromMap(currentMap, currentTileX, currentTileY)

            ' Get the well BMP filename
            Dim wellBmpFilename As String = "Unknown"
            If tileData.TilesetIndex >= 0 AndAlso tileData.TilesetIndex < currentMap.tilesetSources.Count Then
                wellBmpFilename = currentMap.tilesetSources(tileData.TilesetIndex).tilesetFilename
            End If

            ' Update the tileset labels
            lblTileset.Text = $"Tileset: {wellBmpFilename}"
            lblTilesetImage.Text = $"Image Index: {tileData.ImageIndex}"
            lblMappingIndex.Text = $"Mapping Index: {tileData.TileMappingIndex}"

            ' Update coordinates and cell type labels
            lblCoordinates.Text = "Coordinates: " & FormatTileCoords(currentTileX, currentTileY)
            lblCellType.Text = $"Cell Type: {tileData.CellType}"

            ' Update the tile preview images
            UpdateTilePreview(tileData)

        Catch ex As Exception
            Debug.WriteLine($"Error in UpdatePropertyControls: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Updates the small (1:1) and large (3x, nearest-neighbor) tile preview images for
    ''' the given tile, falling back to a placeholder if the tile image is unavailable.
    ''' </summary>
    Private Sub UpdateTilePreview(tileData As TileData)
        ' Clear any previous images
        If selectedTileImage IsNot Nothing Then
            picTilePreview.Image = Nothing
            selectedTileImage.Dispose()
            selectedTileImage = Nothing
        End If

        picTilePreview.Image = Nothing

        If mapRenderer IsNot Nothing Then
            ' Get the raw tile image directly, bypassing render mode effects
            Using originalTile As Bitmap = mapRenderer.GetRawTileImage(tileData.Coordinates.X, tileData.Coordinates.Y)
                If originalTile IsNot Nothing Then
                    ' Display the original size tile
                    picTilePreview.Image = New Bitmap(originalTile)

                    ' Create the zoomed preview
                    selectedTileImage = New Bitmap(96, 96)

                    Using g As Graphics = Graphics.FromImage(selectedTileImage)
                        g.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
                        g.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half
                        g.DrawImage(originalTile, New Rectangle(0, 0, 96, 96))
                    End Using

                    picTilePreviewBig.Image = selectedTileImage
                    Return
                End If
            End Using
        End If

        ' If we get here, create placeholder images
        CreatePlaceholderTilePreview(tileData)
    End Sub

    ''' <summary>
    ''' Draws red-crossed placeholder previews (with the missing tileset name and image
    ''' index) when the real tile image can't be rendered.
    ''' </summary>
    Private Sub CreatePlaceholderTilePreview(tileData As TileData)
        ' Get tileset name if available
        Dim tilesetName As String = "Unknown"
        If tileData.TilesetIndex >= 0 AndAlso tileData.TilesetIndex < currentMap.tilesetSources.Count Then
            tilesetName = currentMap.tilesetSources(tileData.TilesetIndex).tilesetFilename
        End If

        ' Original size placeholder
        Dim originalPlaceholder As New Bitmap(32, 32)
        Using g As Graphics = Graphics.FromImage(originalPlaceholder)
            g.Clear(System.Drawing.Color.LightGray)
            g.DrawRectangle(Pens.Red, 0, 0, 31, 31)
            g.DrawLine(Pens.Red, 0, 0, 31, 31)
            g.DrawLine(Pens.Red, 0, 31, 31, 0)
        End Using
        picTilePreview.Image = originalPlaceholder

        ' Enlarged placeholder
        selectedTileImage = New Bitmap(96, 96)
        Using g As Graphics = Graphics.FromImage(selectedTileImage)
            g.Clear(System.Drawing.Color.LightGray)
            g.DrawRectangle(Pens.Red, 0, 0, 95, 95)
            g.DrawLine(Pens.Red, 0, 0, 95, 95)
            g.DrawLine(Pens.Red, 0, 95, 95, 0)

            ' Draw some text explaining the issue
            Using font As New Font("Arial", 8)
                g.DrawString($"Missing: {tilesetName}", font, Brushes.Black, 5, 5)
                g.DrawString($"Index: {tileData.ImageIndex}", font, Brushes.Black, 5, 20)
            End Using
        End Using
        picTilePreviewBig.Image = selectedTileImage
    End Sub

    ''' <summary>
    ''' Handles the main form resize event to adjust the map display
    ''' </summary>
    Private Sub fMain_Resize(sender As Object, e As EventArgs) Handles Me.Resize
        ' Only process if we have a map loaded
        If currentMap Is Nothing Then
            Return
        End If

        ' Calculate if we need to adjust the map offset to keep it centered
        Dim mapWidthPixels As Integer = CInt(currentMap.WidthInTiles()) * 32 * zoomLevel
        Dim mapHeightPixels As Integer = CInt(currentMap.HeightInTiles()) * 32 * zoomLevel

        ' If the map is smaller than the viewport, center it
        If mapWidthPixels < pnlMap.ClientSize.Width Then
            offsetX = (pnlMap.ClientSize.Width - mapWidthPixels) \ 2
        End If

        If mapHeightPixels < pnlMap.ClientSize.Height Then
            offsetY = (pnlMap.ClientSize.Height - mapHeightPixels) \ 2
        End If

        ' Ensure the map doesn't have large empty spaces when resizing
        ' If map is larger than the view but offset creates empty space, adjust offset
        If mapWidthPixels > pnlMap.ClientSize.Width Then
            If offsetX > 0 Then
                offsetX = 0
            ElseIf offsetX < pnlMap.ClientSize.Width - mapWidthPixels Then
                offsetX = pnlMap.ClientSize.Width - mapWidthPixels
            End If
        End If

        If mapHeightPixels > pnlMap.ClientSize.Height Then
            If offsetY > 0 Then
                offsetY = 0
            ElseIf offsetY < pnlMap.ClientSize.Height - mapHeightPixels Then
                offsetY = pnlMap.ClientSize.Height - mapHeightPixels
            End If
        End If

        ' Refresh the map display
        pnlMap.Invalidate()
    End Sub

    ''' <summary>
    ''' Converts screen coordinates to map tile coordinates
    ''' </summary>
    Private Function ScreenToMapCoordinates(screenX As Integer, screenY As Integer) As Point
        ' Convert screen coordinates to map coordinates
        Dim mapX As Integer = CInt(Math.Floor((screenX - offsetX) / (32 * zoomLevel)))
        Dim mapY As Integer = CInt(Math.Floor((screenY - offsetY) / (32 * zoomLevel)))

        Return New Point(mapX, mapY)
    End Function

    ''' <summary>
    ''' Converts map tile coordinates to screen coordinates
    ''' </summary>
    Private Function MapToScreenCoordinates(mapX As Integer, mapY As Integer) As Rectangle
        ' Convert map coordinates to screen coordinates
        Dim screenX As Integer = CInt(mapX * 32 * zoomLevel) + offsetX
        Dim screenY As Integer = CInt(mapY * 32 * zoomLevel) + offsetY
        Dim tileSize As Integer = CInt(32 * zoomLevel)

        Return New Rectangle(screenX, screenY, tileSize, tileSize)
    End Function

    ''' <summary>
    ''' Formats tile coordinates for display. Internal coords are 0-based;
    ''' the UI shows them 1-based.
    ''' </summary>
    Private Shared Function FormatTileCoords(tileX As Integer, tileY As Integer) As String
        Return $"{tileX + 1}, {tileY + 1}"
    End Function

#End Region

End Class
