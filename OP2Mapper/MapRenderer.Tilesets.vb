Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports OP2UtilityDotNet
Imports OP2UtilityDotNet.OP2Map
Imports OP2UtilityDotNet.Bitmap
Imports OP2UtilityDotNet.Sprite

' MapRenderer.Tilesets.vb
' Partial class of MapRenderer (split out of MapRenderer.vb).
' Tileset loading, tile image caching and tile image access (raw and by-index lookups).
Partial Public Class MapRenderer

    ' Tileset handling methods

    ''' <summary>
    ''' Loads all tilesets needed for the current map
    ''' </summary>
    Public Sub LoadMapTilesets()
        If _map Is Nothing Then Return

        ' Clean up any existing tileset images
        ClearTilesets()

        ' Default tileset paths
        Dim tilesetPath As String = Path.Combine(My.Settings.OP2Path, "OPU\base\tilesets")
        Dim alternatePaths() As String = {"base\tilesets", "tilesets"}

        ' Check if the tileset path exists
        If Not Directory.Exists(tilesetPath) Then
            ' Try alternate paths
            Dim found As Boolean = False
            For Each altPath In alternatePaths
                Dim fullAltPath As String = Path.Combine(My.Settings.OP2Path, altPath)
                If Directory.Exists(fullAltPath) Then
                    tilesetPath = fullAltPath
                    found = True
                    Exit For
                End If
            Next

            If Not found Then
                Debug.WriteLine("Could not find tileset directory in any expected location")
                Return
            End If
        End If

        ' Load all tilesets used by this map
        For Each tilesetSource In _map.tilesetSources
            If Not tilesetSource.IsEmpty() Then
                Dim tilesetFilename As String = tilesetSource.tilesetFilename

                ' Ensure the filename has a .bmp extension
                If Not tilesetFilename.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) Then
                    tilesetFilename += ".bmp"
                End If

                ' Full path to the tileset file
                Dim fullPath As String = Path.Combine(tilesetPath, tilesetFilename)

                ' Load the tileset if it exists
                If File.Exists(fullPath) Then
                    Try
                        Using fileStream As New FileStream(fullPath, FileMode.Open, FileAccess.Read)
                            Dim tilesetBitmap As BitmapFile = TilesetLoader.ReadTileset(fileStream)
                            ExtractTilesFromTileset(tilesetSource.tilesetFilename, tilesetBitmap)
                        End Using
                    Catch ex As Exception
                        Debug.WriteLine($"Error loading tileset {tilesetFilename}: {ex.Message}")
                        _tilesetImages(tilesetSource.tilesetFilename) = New List(Of Bitmap)()
                    End Try
                Else
                    Debug.WriteLine($"Tileset file not found: {fullPath}")
                    _tilesetImages(tilesetSource.tilesetFilename) = New List(Of Bitmap)()
                End If
            End If
        Next
    End Sub

    ''' <summary>
    ''' Extract tiles from a tileset bitmap
    ''' </summary>
    Private Sub ExtractTilesFromTileset(tilesetName As String, tilesetBitmap As BitmapFile)
        ' Get dimensions
        Dim tileWidth As Integer = _tileSize
        Dim tileHeight As Integer = _tileSize
        Dim totalHeight As Integer = Math.Abs(tilesetBitmap.imageHeader.height)
        Dim width As Integer = tilesetBitmap.imageHeader.width

        ' Verify the tileset has the expected width
        If width <> tileWidth Then
            Debug.WriteLine($"WARNING: Tileset {tilesetName} has unexpected width: {width} (expected {tileWidth})")
        End If

        ' Calculate number of tiles
        Dim totalTiles As Integer = totalHeight \ tileHeight

        ' Create a list to hold the individual tile bitmaps
        Dim tileBitmaps As New List(Of Bitmap)

        ' Convert the bitmap file to a System.Drawing.Bitmap
        Dim fullBitmap As Bitmap = ConvertToBitmap(tilesetBitmap)

        ' Extract each tile (stacked vertically)
        For i As Integer = 0 To totalTiles - 1
            ' Create a new bitmap for this tile
            Dim tileBitmap As New Bitmap(tileWidth, tileHeight)

            ' Calculate the Y position of this tile in the source bitmap
            Dim sourceY As Integer = i * tileHeight

            ' Copy the tile pixels to the new bitmap
            Using g As Graphics = Graphics.FromImage(tileBitmap)
                g.DrawImage(fullBitmap,
                        New Rectangle(0, 0, tileWidth, tileHeight),
                        New Rectangle(0, sourceY, tileWidth, tileHeight),
                        GraphicsUnit.Pixel)
            End Using

            ' Add to our collection
            tileBitmaps.Add(tileBitmap)
        Next

        ' Store in our dictionary
        _tilesetImages(tilesetName) = tileBitmaps

        ' Dispose the full bitmap
        fullBitmap.Dispose()
    End Sub

    ''' <summary>
    ''' Converts a BitmapFile to a NEW System.Drawing.Bitmap with proper color handling.
    ''' The caller owns the returned Bitmap and must dispose it.
    ''' </summary>
    Private Function ConvertToBitmap(bitmapFile As BitmapFile) As Bitmap
        Dim width As Integer = bitmapFile.imageHeader.width
        Dim height As Integer = Math.Abs(bitmapFile.imageHeader.height)
        Dim bitmap As New Bitmap(width, height, PixelFormat.Format32bppArgb)

        ' Build the palette as packed ARGB integers.
        ' Important: OP2 stores colors in BGR format, so we swap Red and Blue.
        Dim palette(bitmapFile.palette.Length - 1) As Integer

        ' Check if index 0 should be treated as transparent
        Dim isIndex0Transparent As Boolean = False
        If bitmapFile.palette.Length > 0 Then
            ' In many OP2 tilesets, index 0 is white (255,255,255) which indicates transparency
            Dim p0 As OP2UtilityDotNet.Bitmap.Color = bitmapFile.palette(0)
            isIndex0Transparent = (p0.red > 250 AndAlso p0.green > 250 AndAlso p0.blue > 250)
            'Debug.WriteLine($"Index 0 values: R={p0.red}, G={p0.green}, B={p0.blue}, isTransparent={isIndex0Transparent}")
        End If

        For i As Integer = 0 To bitmapFile.palette.Length - 1
            ' Create color with Red and Blue swapped
            palette(i) = System.Drawing.Color.FromArgb(
        bitmapFile.palette(i).blue,   ' Red channel gets blue value
        bitmapFile.palette(i).green,  ' Green stays the same
        bitmapFile.palette(i).red).ToArgb()    ' Blue channel gets red value
        Next

        ' Transparent index 0 is rendered as opaque light gray (matches the
        ' previous per-pixel behaviour).
        Dim grayArgb As Integer = System.Drawing.Color.FromArgb(192, 192, 192).ToArgb()

        Dim isTopDown As Boolean = bitmapFile.imageHeader.height < 0

        ' Write pixels straight into the bitmap buffer via LockBits instead of
        ' per-pixel SetPixel. SetPixel locks/unlocks the bitmap on every call,
        ' so over hundreds of 32x32 tiles per tileset it dominated map-load
        ' time; one locked row-buffer copy per scanline is orders of magnitude
        ' faster. Pixels whose palette index is out of range are left as the
        ' default transparent black, exactly as the old code skipped them.
        Dim data As BitmapData = bitmap.LockBits(
            New Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb)

        Try
            Dim stride As Integer = data.Stride
            Dim rowBuffer(stride - 1) As Byte

            For y As Integer = 0 To height - 1
                ' Adjust source row based on bitmap orientation (bottom-up vs top-down)
                Dim adjustedY As Integer = If(isTopDown, y, height - 1 - y)

                ' Clear the row so unset (out-of-range) pixels stay transparent
                Array.Clear(rowBuffer, 0, stride)

                For x As Integer = 0 To width - 1
                    Dim paletteIndex As Integer = bitmapFile.GetPixelPaletteIndex(x, adjustedY)
                    If paletteIndex >= 0 AndAlso paletteIndex < palette.Length Then
                        Dim argb As Integer
                        If paletteIndex = 0 AndAlso isIndex0Transparent Then
                            argb = grayArgb
                        Else
                            argb = palette(paletteIndex)
                        End If

                        ' Format32bppArgb is laid out as B,G,R,A in memory (little-endian)
                        Dim offset As Integer = x * 4
                        rowBuffer(offset) = CByte(argb And &HFF)              ' Blue
                        rowBuffer(offset + 1) = CByte((argb >> 8) And &HFF)   ' Green
                        rowBuffer(offset + 2) = CByte((argb >> 16) And &HFF)  ' Red
                        rowBuffer(offset + 3) = CByte((argb >> 24) And &HFF)  ' Alpha
                    End If
                Next

                System.Runtime.InteropServices.Marshal.Copy(rowBuffer, 0, System.IntPtr.Add(data.Scan0, y * stride), stride)
            Next
        Finally
            bitmap.UnlockBits(data)
        End Try

        Return bitmap
    End Function

    ''' <summary>
    ''' Gets the image for a specific tile, using caching for performance.
    ''' Returns a SHARED cached instance (owned by _tilesetImages or, for the
    ''' fallback, _fallbackTileCache) — the caller must NOT dispose it.
    ''' </summary>
    Private Function GetTileImage(x As Integer, y As Integer) As Bitmap
        ' Get the mapping index for this tile
        Dim mappingIndex As UInteger = _map.GetTileMappingIndex(x, y)

        ' Check if we've already loaded this tile
        If _tileCache.ContainsKey(mappingIndex) Then
            Return _tileCache(mappingIndex)
        End If

        ' Get the tileset and image indexes
        Dim tilesetIndex As Integer = _map.GetTilesetIndex(x, y)
        Dim imageIndex As Integer = _map.GetImageIndex(x, y)

        ' Check if we have valid indices
        If tilesetIndex >= 0 AndAlso tilesetIndex < _map.tilesetSources.Count Then
            Dim tilesetName As String = _map.tilesetSources(tilesetIndex).tilesetFilename

            ' Check if we have this tileset loaded
            If _tilesetImages.ContainsKey(tilesetName) Then
                Dim tileList As List(Of Bitmap) = _tilesetImages(tilesetName)

                ' Check if the image index is valid
                If imageIndex >= 0 AndAlso imageIndex < tileList.Count Then
                    ' Add to cache and return
                    _tileCache(mappingIndex) = tileList(imageIndex)
                    Return _tileCache(mappingIndex)
                End If
            End If
        End If

        ' If we get here, we couldn't find the image - return a fallback
        Return GetFallbackTileImage(x, y)
    End Function

    ''' <summary>
    ''' Creates (or reuses) a fallback image for tiles we can't load.
    ''' Returns a SHARED instance owned by _fallbackTileCache — the caller must
    ''' NOT dispose it (ClearCache disposes these).
    ''' </summary>
    Private Function GetFallbackTileImage(x As Integer, y As Integer) As Bitmap
        ' Get the cell type to color the fallback
        Dim cellType As CellType = _map.GetCellType(x, y)
        Dim cellTypeKey As String = cellType.ToString()

        ' Check if we already have a fallback for this cell type
        If _fallbackTileCache.ContainsKey(cellTypeKey) Then
            Return _fallbackTileCache(cellTypeKey)
        End If

        ' Create a new fallback
        Dim fallbackTile As New Bitmap(_tileSize, _tileSize)
        Using g As Graphics = Graphics.FromImage(fallbackTile)
            ' Fill with cell type color
            g.FillRectangle(New SolidBrush(GetCellTypeColor(cellType)), 0, 0, _tileSize, _tileSize)

            ' Add a border
            g.DrawRectangle(Pens.DarkGray, 0, 0, _tileSize - 1, _tileSize - 1)

            ' Add an "X" pattern to indicate it's a fallback
            g.DrawLine(Pens.DarkGray, 0, 0, _tileSize - 1, _tileSize - 1)
            g.DrawLine(Pens.DarkGray, 0, _tileSize - 1, _tileSize - 1, 0)
        End Using

        ' Add to fallback cache
        _fallbackTileCache(cellTypeKey) = fallbackTile

        Return fallbackTile
    End Function

    ' Resource management methods

    ''' <summary>
    ''' Clears all tile caches
    ''' </summary>
    Public Sub ClearCache()
        ' _tileCache stores REFERENCES to bitmaps owned by _tilesetImages —
        ' the cache is just a lookup shortcut, not the owner. Disposing here
        ' would leave _tilesetImages holding disposed bitmaps, breaking the
        ' next render. Just drop the lookup entries.
        _tileCache.Clear()

        ' _fallbackTileCache, on the other hand, owns its bitmaps — they're
        ' created fresh in GetFallbackTileImage — so they DO need disposing.
        For Each bitmap In _fallbackTileCache.Values
            bitmap.Dispose()
        Next
        _fallbackTileCache.Clear()
    End Sub

    ''' <summary>
    ''' Clears all loaded tilesets
    ''' </summary>
    Public Sub ClearTilesets()
        ' Dispose all tileset bitmaps
        For Each tileList In _tilesetImages.Values
            For Each bitmap In tileList
                bitmap.Dispose()
            Next
        Next
        _tilesetImages.Clear()
    End Sub

    ''' <summary>
    ''' Disposes all resources
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        ClearCache()
        ClearTilesets()
    End Sub

    ' Get tileset loading status
    ''' <summary>
    ''' Returns (loaded tilesets, total tilesets) — a tileset counts as loaded if it has at least one tile image.
    ''' </summary>
    Public Function GetTilesetLoadingStatus() As Tuple(Of Integer, Integer)
        ' Return (loaded tilesets, total tilesets)
        Dim loadedTilesets As Integer = 0
        Dim totalTilesets As Integer = _tilesetImages.Count

        For Each kvp In _tilesetImages
            If kvp.Value.Count > 0 Then
                loadedTilesets += 1
            End If
        Next

        Return New Tuple(Of Integer, Integer)(loadedTilesets, totalTilesets)
    End Function

    ' List all loaded tilesets and their tile counts
    ''' <summary>
    ''' Returns a dictionary of loaded tileset names to their tile counts.
    ''' </summary>
    Public Function ListLoadedTilesets() As Dictionary(Of String, Integer)
        Dim result As New Dictionary(Of String, Integer)

        For Each kvp In _tilesetImages
            result(kvp.Key) = kvp.Value.Count
        Next

        Return result
    End Function

    ''' <summary>
    ''' Preloads and caches frequently used tile images to improve rendering performance
    ''' </summary>
    Public Sub PreloadCommonTiles()
        If _map Is Nothing Then Return

        ' Collect statistics on most common tiles
        Dim tileStats As New Dictionary(Of UInteger, Integer)()

        ' Sample a subset of tiles to find common ones
        Dim sampleStride As Integer = Math.Max(1, CInt(_map.WidthInTiles() * _map.HeightInTiles() / 500))

        For y As Integer = 0 To CInt(_map.HeightInTiles()) - 1 Step sampleStride
            For x As Integer = 0 To CInt(_map.WidthInTiles()) - 1 Step sampleStride
                Dim mappingIndex As UInteger = _map.GetTileMappingIndex(x, y)

                If tileStats.ContainsKey(mappingIndex) Then
                    tileStats(mappingIndex) += 1
                Else
                    tileStats(mappingIndex) = 1
                End If
            Next
        Next

        ' Sort by frequency
        Dim sortedIndices = From entry In tileStats
                            Order By entry.Value Descending
                            Select entry.Key

        ' Preload the most common tiles (up to 50)
        Dim count As Integer = 0
        For Each mappingIndex In sortedIndices
            If count >= 50 Then Exit For

            ' Force the tile to be loaded into cache
            For y As Integer = 0 To CInt(_map.HeightInTiles()) - 1
                For x As Integer = 0 To CInt(_map.WidthInTiles()) - 1
                    If _map.GetTileMappingIndex(x, y) = mappingIndex Then
                        ' This will cache the tile image
                        GetTileImage(x, y)
                        count += 1
                        Exit For
                    End If
                Next
                If count >= 50 Then Exit For
            Next
        Next

        Debug.WriteLine($"Preloaded {count} common tiles into cache")
    End Sub

    ''' <summary>
    ''' Gets the raw tile image from its tileset without applying any render mode effects.
    ''' Returns a NEW Bitmap COPY — the caller OWNS it and must dispose it.
    ''' Returns Nothing if the coordinates are out of range.
    ''' </summary>
    Public Function GetRawTileImage(x As Integer, y As Integer) As Bitmap
        If _map Is Nothing OrElse x < 0 OrElse y < 0 OrElse
           x >= _map.WidthInTiles() OrElse y >= _map.HeightInTiles() Then
            Return Nothing
        End If

        ' Get the tileset and image indexes
        Dim tilesetIndex As Integer = _map.GetTilesetIndex(x, y)
        Dim imageIndex As Integer = _map.GetImageIndex(x, y)

        ' Check if we have valid indices
        If tilesetIndex >= 0 AndAlso tilesetIndex < _map.tilesetSources.Count Then
            Dim tilesetName As String = _map.tilesetSources(tilesetIndex).tilesetFilename

            ' Check if we have this tileset loaded
            If _tilesetImages.ContainsKey(tilesetName) Then
                Dim tileList As List(Of Bitmap) = _tilesetImages(tilesetName)

                ' Check if the image index is valid
                If imageIndex >= 0 AndAlso imageIndex < tileList.Count Then
                    ' Return a copy of the bitmap to avoid modification
                    Return New Bitmap(tileList(imageIndex))
                End If
            End If
        End If

        ' Couldn't find the image - return a COPY of the cached fallback.
        ' Callers of this public method own (and dispose) the returned bitmap;
        ' handing out the shared _fallbackTileCache instance would let them
        ' dispose a bitmap the render path still draws from.
        Return New Bitmap(GetFallbackTileImage(x, y))
    End Function

    ''' <summary>
    ''' Gets a tile image by tileset index and image index.
    ''' Returns a NEW Bitmap COPY — the caller OWNS it and must dispose it.
    ''' </summary>
    ''' <param name="tilesetIndex">Index of the tileset</param>
    ''' <param name="imageIndex">Index of the image within the tileset</param>
    ''' <returns>New Bitmap copy of the requested tile or Nothing if not found</returns>
    Public Function GetTileImageByIndices(tilesetIndex As Integer, imageIndex As Integer) As Bitmap
        Try
            ' Validate indices
            If tilesetIndex < 0 OrElse tilesetIndex >= _map.tilesetSources.Count Then
                Return Nothing
            End If

            ' Get the tileset name
            Dim tilesetName As String = _map.tilesetSources(tilesetIndex).tilesetFilename

            ' Check if we have this tileset loaded
            If Not _tilesetImages.ContainsKey(tilesetName) Then
                Return Nothing
            End If

            ' Get the tileset images
            Dim tileImages As List(Of Bitmap) = _tilesetImages(tilesetName)

            ' Check if the image index is valid
            If imageIndex < 0 OrElse imageIndex >= tileImages.Count Then
                Return Nothing
            End If

            ' Return a copy of the image to avoid modification of the original
            Return New Bitmap(tileImages(imageIndex))
        Catch ex As Exception
            Debug.WriteLine($"Error in GetTileImageByIndices: {ex.Message}")
            Return Nothing
        End Try
    End Function

End Class
