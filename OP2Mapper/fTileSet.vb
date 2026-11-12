Imports System.IO
Imports System.Drawing
Imports System.Windows.Forms
Imports OP2UtilityDotNet
Imports OP2UtilityDotNet.OP2Map
Imports OP2UtilityDotNet.Bitmap
Imports OP2UtilityDotNet.Sprite

' fTileSet.vb - Tool 1: "Tile Set" palette window.
' Displays the tiles of the selected tileset (well) in a scrollable grid and
' lets the user pick the active tile for placement on the map.
Public Class fTileSet

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

    ' Keep track of the currently loaded tileset bitmap
    Private currentTilesetBitmap As Bitmap = Nothing

    ' Reference to main form
    Private mainForm As fMain = Nothing

    ' Selected tile info
    Private selectedTileIndex As Integer = -1
    Private currentTilesetIndex As Integer = -1
    Private tilesWide As Integer = 0
    Private tilesHigh As Integer = 0

    ' Constants for tile display
    Private Const TileSize As Integer = 32 ' Standard tile size in pixels
    Private Const TilesPerRow As Integer = 6 ' Number of tiles to display per row
    Private Const TilePadding As Integer = 1 ' Space between tiles

    ' Variables for panel interaction
    Private isDragging As Boolean = False
    Private startScrollPos As Point

    ''' <summary>
    ''' Constructor - configures the tile display panel and wires up its events
    ''' </summary>
    Public Sub New()
        InitializeComponent()

        ' Set up panel for double buffering to reduce flicker
        pnlTileSets.GetType().GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(pnlTileSets, True)

        ' Configure panel properties
        pnlTileSets.AutoScroll = True
        pnlTileSets.BorderStyle = BorderStyle.FixedSingle
        pnlTileSets.BackColor = System.Drawing.Color.DarkGray

        ' Add event handlers
        AddHandler pnlTileSets.Paint, AddressOf PnlTileSets_Paint
        AddHandler pnlTileSets.MouseDown, AddressOf PnlTileSets_MouseDown
        AddHandler pnlTileSets.MouseMove, AddressOf PnlTileSets_MouseMove
        AddHandler pnlTileSets.MouseUp, AddressOf PnlTileSets_MouseUp
        AddHandler cmbWellSelector.SelectedIndexChanged, AddressOf CmbWellSelector_SelectedIndexChanged
    End Sub

    ''' <summary>
    ''' Form load event handler - restores position and loads the initial tileset
    ''' </summary>
    Private Sub fTileSet_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ToolPaletteHelper.ConfigureAsToolPalette(Me)
        ToolPaletteHelper.RestorePosition(Me, "Tileset")

        ' Get reference to main form
        mainForm = TryCast(Application.OpenForms("fMain"), fMain)

        ' If we have a map loaded, populate the well selector
        If mainForm IsNot Nothing AndAlso mainForm.CurrentLoadedMap IsNot Nothing Then
            Debug.WriteLine("Populating tileset selector...")
            PopulateWellSelector()

            ' Set form properties (TopMost is set by ConfigureAsToolPalette
            ' per the View → Tool Windows Always On Top setting)
            Me.FormBorderStyle = FormBorderStyle.FixedToolWindow
            Me.Text = "Tile Set"
            Me.Height = 484
            Me.Icon = My.Resources.lists

            pnlTileSets.Height = 395
            cmbWellSelector.DropDownStyle = ComboBoxStyle.DropDownList

            ' Force load the selected tileset if one is selected
            If cmbWellSelector.SelectedItem IsNot Nothing Then
                LoadSelectedWell()
            End If
        Else
            Debug.WriteLine("No map loaded or main form not found")
        End If
    End Sub

    ''' <summary>
    ''' Form closing event handler - saves position and releases the tileset bitmap
    ''' </summary>
    Private Sub fTileSet_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        ToolPaletteHelper.SavePosition(Me, "Tileset")
        ToolPaletteHelper.UnregisterToolForm(Me)

        If currentTilesetBitmap IsNot Nothing Then      ' Clean up resources
            currentTilesetBitmap.Dispose()
            currentTilesetBitmap = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Fills the well selector combo with the tilesets used by the loaded map
    ''' </summary>
    Private Sub PopulateWellSelector()
        ' Clear existing items
        cmbWellSelector.Items.Clear()

        Try
            ' Get tilesets from the map
            If mainForm.CurrentLoadedMap IsNot Nothing Then
                For Each tilesetSource In mainForm.CurrentLoadedMap.tilesetSources
                    If Not tilesetSource.IsEmpty() Then
                        cmbWellSelector.Items.Add(tilesetSource.tilesetFilename)
                    End If
                Next

                ' Select the first item if available
                If cmbWellSelector.Items.Count > 0 Then
                    cmbWellSelector.SelectedIndex = 0
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine($"Error populating well selector: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Well selector change event handler - loads the newly selected tileset
    ''' </summary>
    Private Sub CmbWellSelector_SelectedIndexChanged(sender As Object, e As EventArgs)
        ' Load the selected well file
        LoadSelectedWell()
    End Sub


    ''' <summary>
    ''' Loads the tileset selected in the well combo into currentTilesetBitmap,
    ''' trying the resource manager first and then known disk locations
    ''' </summary>
    Private Sub LoadSelectedWell()
        If cmbWellSelector.SelectedItem Is Nothing Then
            Return
        End If

        Try
            ' Get the selected well filename
            Dim wellFilename As String = cmbWellSelector.SelectedItem.ToString()
            'Debug.WriteLine($"Loading tileset: {wellFilename}")

            ' Find the index of this tileset in the map
            currentTilesetIndex = -1
            For i As Integer = 0 To mainForm.CurrentLoadedMap.tilesetSources.Count - 1
                If mainForm.CurrentLoadedMap.tilesetSources(i).tilesetFilename = wellFilename Then
                    currentTilesetIndex = i
                    Exit For
                End If
            Next

            ' Clean up any previous tileset bitmap
            If currentTilesetBitmap IsNot Nothing Then
                currentTilesetBitmap.Dispose()
                currentTilesetBitmap = Nothing
            End If

            ' Make sure the filename has .bmp extension
            If Not wellFilename.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) Then
                wellFilename += ".bmp"
            End If

            ' Try loading the tileset from various locations
            Dim loaded As Boolean = False

            ' Method 1: Try ResourceManager
            Using stream As Stream = mainForm.MapResourceManager.GetResourceStream(wellFilename)
                If stream IsNot Nothing Then
                    Dim bitmapFile As OP2UtilityDotNet.Bitmap.BitmapFile = TilesetLoader.ReadTileset(stream)
                    currentTilesetBitmap = CreateTilesetBitmap(bitmapFile)
                    tilesWide = bitmapFile.imageHeader.width \ TileSize
                    tilesHigh = Math.Abs(bitmapFile.imageHeader.height) \ TileSize
                    loaded = True
                    Debug.WriteLine($"Loaded tileset (" & wellFilename.ToString & ") via ResourceManager: {tilesWide}×{tilesHigh} tiles")
                End If
            End Using

            ' Method 2: Try alternate paths
            If Not loaded Then
                Dim alternatePaths As String() = {
                Path.Combine(My.Settings.OP2Path, "OPU\base\tilesets", wellFilename),
                Path.Combine(My.Settings.OP2Path, "base\tilesets", wellFilename),
                Path.Combine(My.Settings.OP2Path, "tilesets", wellFilename)
            }

                For Each path In alternatePaths
                    If File.Exists(path) Then
                        Debug.WriteLine($"Loading tileset from: {path}")
                        Dim result = LoadOP2Tileset(path)
                        currentTilesetBitmap = result.Item1
                        tilesWide = result.Item2
                        tilesHigh = result.Item3
                        loaded = True
                        Debug.WriteLine($"Loaded tileset from alternate path: {tilesWide}×{tilesHigh} tiles")
                        Exit For
                    End If
                Next
            End If

            ' Update panel size
            If currentTilesetBitmap IsNot Nothing Then
                Dim panelWidth As Integer = TilesPerRow * (TileSize + TilePadding)
                Dim totalTiles As Integer = tilesWide * tilesHigh
                Dim rowsNeeded As Integer = (totalTiles + TilesPerRow - 1) \ TilesPerRow
                Dim panelHeight As Integer = rowsNeeded * (TileSize + TilePadding)
                pnlTileSets.AutoScrollMinSize = New Size(panelWidth, panelHeight)

                'Debug.WriteLine($"Set panel size: {panelWidth}×{panelHeight} for {totalTiles} tiles")
            Else
                Debug.WriteLine("Failed to load tileset bitmap")
            End If

            ' Refresh display
            pnlTileSets.Invalidate()

        Catch ex As Exception
            Debug.WriteLine($"Error loading well file: {ex.Message}")
            MessageBox.Show($"Error loading tileset: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Loads an OP2 tileset file with proper color handling
    ''' </summary>
    Private Function LoadOP2Tileset(filePath As String) As Tuple(Of Bitmap, Integer, Integer)
        Try
            Using fileStream As New FileStream(filePath, FileMode.Open, FileAccess.Read)
                ' Load the tileset using OP2UtilityDotNet's specialized loader
                Dim bitmapFile As OP2UtilityDotNet.Bitmap.BitmapFile = TilesetLoader.ReadTileset(fileStream)

                ' Create bitmap with proper color handling
                Dim bitmap As Bitmap = CreateTilesetBitmap(bitmapFile)

                ' Calculate dimensions
                Dim tWidth As Integer = bitmapFile.imageHeader.width \ TileSize
                Dim tHeight As Integer = Math.Abs(bitmapFile.imageHeader.height) \ TileSize

                Return New Tuple(Of Bitmap, Integer, Integer)(bitmap, tWidth, tHeight)
            End Using
        Catch ex As Exception
            Debug.WriteLine($"Error in LoadOP2Tileset: {ex.Message}")
            Throw
        End Try
    End Function

    ''' <summary>
    ''' Creates a fallback checkerboard bitmap when the real tileset can't be loaded
    ''' </summary>
    Private Function CreateFallbackTileset(filename As String) As Bitmap
        Dim width As Integer = TileSize * 6
        Dim height As Integer = TileSize * 10
        Dim bitmap As New Bitmap(width, height)

        Using g As Graphics = Graphics.FromImage(bitmap)
            ' Fill background
            g.Clear(System.Drawing.Color.LightGray)

            ' Draw checkerboard pattern
            Using brush1 As New SolidBrush(System.Drawing.Color.DarkGray)
                Using brush2 As New SolidBrush(System.Drawing.Color.Gray)
                    For y As Integer = 0 To height - 1 Step TileSize
                        For x As Integer = 0 To width - 1 Step TileSize
                            Dim isEven As Boolean = ((x \ TileSize) + (y \ TileSize)) Mod 2 = 0
                            g.FillRectangle(If(isEven, brush1, brush2), x, y, TileSize, TileSize)
                        Next
                    Next
                End Using
            End Using

            ' Draw text
            Using font As New Font("Arial", 10)
                Dim text As String = $"Missing: {filename}"
                g.DrawString(text, font, Brushes.Black, 10, 10)
            End Using
        End Using

        Return bitmap
    End Function


    ''' <summary>
    ''' Creates a System.Drawing.Bitmap from a BitmapFile with proper OP2 color handling
    ''' </summary>
    Private Function CreateTilesetBitmap(bitmapFile As OP2UtilityDotNet.Bitmap.BitmapFile) As Bitmap
        ' Create a bitmap for display
        Dim width As Integer = bitmapFile.imageHeader.width
        Dim height As Integer = Math.Abs(bitmapFile.imageHeader.height)
        Dim bitmap As New Bitmap(width, height, Imaging.PixelFormat.Format32bppArgb)

        ' Important: Build the palette as packed ARGB integers with RED and BLUE
        ' SWAPPED — OP2 stores colors in BGR format in the palette.
        Dim palette(bitmapFile.palette.Length - 1) As Integer
        For i As Integer = 0 To bitmapFile.palette.Length - 1
            ' Create color with Red and Blue swapped to properly interpret OP2's format
            palette(i) = System.Drawing.Color.FromArgb(
            bitmapFile.palette(i).blue,   ' Red channel gets blue value
            bitmapFile.palette(i).green,  ' Green stays the same
            bitmapFile.palette(i).red).ToArgb()    ' Blue channel gets red value
        Next

        ' Index 0 is rendered as opaque light gray (transparency placeholder)
        Dim grayArgb As Integer = System.Drawing.Color.FromArgb(192, 192, 192).ToArgb()

        ' Determine if the bitmap uses top-down or bottom-up orientation
        Dim isTopDown As Boolean = bitmapFile.imageHeader.height < 0

        ' Write pixels via LockBits rather than per-pixel SetPixel — the whole
        ' tileset BMP is converted on every well switch, and SetPixel's
        ' per-call lock/unlock made that noticeably slow on tall tilesets.
        Dim data As Imaging.BitmapData = bitmap.LockBits(
            New Rectangle(0, 0, width, height),
            Imaging.ImageLockMode.WriteOnly,
            Imaging.PixelFormat.Format32bppArgb)

        Try
            Dim stride As Integer = data.Stride
            Dim rowBuffer(stride - 1) As Byte

            For y As Integer = 0 To height - 1
                ' Adjust source row based on bitmap orientation
                Dim adjustedY As Integer = If(isTopDown, y, height - 1 - y)

                ' Clear the row so unset (out-of-range) pixels stay transparent
                Array.Clear(rowBuffer, 0, stride)

                For x As Integer = 0 To width - 1
                    Dim paletteIndex As Integer = bitmapFile.GetPixelPaletteIndex(x, adjustedY)
                    If paletteIndex >= 0 AndAlso paletteIndex < palette.Length Then
                        Dim argb As Integer = If(paletteIndex = 0, grayArgb, palette(paletteIndex))

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
    ''' Paint event handler - draws the visible portion of the tileset grid
    ''' and highlights the selected tile
    ''' </summary>
    Private Sub PnlTileSets_Paint(sender As Object, e As PaintEventArgs)
        Static lastScrollX As Integer = -1
        Static lastScrollY As Integer = -1

        ' If no tileset is loaded, display a message
        If currentTilesetBitmap Is Nothing Then
            e.Graphics.Clear(System.Drawing.Color.LightGray)
            Using font As New Font("Arial", 10)
                e.Graphics.DrawString("No tileset loaded", font, Brushes.Black, 10, 10)
            End Using
            Return
        End If

        ' Check if we're just scrolling without other changes
        Dim currentScrollX As Integer = Math.Abs(pnlTileSets.AutoScrollPosition.X)
        Dim currentScrollY As Integer = Math.Abs(pnlTileSets.AutoScrollPosition.Y)

        ' Skip debug message during simple scrolling
        If lastScrollX <> currentScrollX Or lastScrollY <> currentScrollY Then
            ' Only log when there's a significant change or first draw
            If lastScrollX = -1 Or lastScrollY = -1 Then
                Debug.WriteLine($"Drawing tileset with {tilesWide * tilesHigh} tiles ({tilesWide}×{tilesHigh})")
            End If

            ' Update last scroll position
            lastScrollX = currentScrollX
            lastScrollY = currentScrollY
        End If

        ' Draw the tileset in a grid format (TilesPerRow tiles per row)
        Dim totalTiles As Integer = tilesWide * tilesHigh

        ' Fill background
        e.Graphics.Clear(System.Drawing.Color.DarkGray)

        ' Calculate visible range based on scroll position
        Dim visibleAreaX As Integer = Math.Abs(pnlTileSets.AutoScrollPosition.X)
        Dim visibleAreaY As Integer = Math.Abs(pnlTileSets.AutoScrollPosition.Y)
        Dim visibleWidth As Integer = pnlTileSets.ClientSize.Width
        Dim visibleHeight As Integer = pnlTileSets.ClientSize.Height

        ' Calculate visible tiles range in the grid
        Dim startCol As Integer = Math.Max(0, visibleAreaX \ (TileSize + TilePadding))
        Dim startRow As Integer = Math.Max(0, visibleAreaY \ (TileSize + TilePadding))
        Dim endCol As Integer = Math.Min(TilesPerRow - 1, (visibleAreaX + visibleWidth) \ (TileSize + TilePadding))

        ' Calculate how many rows we need to display all tiles
        Dim totalRows As Integer = (totalTiles + TilesPerRow - 1) \ TilesPerRow
        Dim endRow As Integer = Math.Min(totalRows - 1, (visibleAreaY + visibleHeight) \ (TileSize + TilePadding))

        ' Optimize by only drawing visible tiles
        For row As Integer = startRow To endRow
            For col As Integer = startCol To endCol
                ' Calculate grid index in our display
                Dim gridIndex As Integer = row * TilesPerRow + col

                ' Skip if we're beyond the total number of tiles
                If gridIndex >= totalTiles Then Continue For

                ' Get the actual tile index from the original tileset
                Dim tileIndex As Integer = gridIndex

                ' Calculate the position in the original tileset
                Dim tilesetX As Integer = tileIndex Mod tilesWide
                Dim tilesetY As Integer = tileIndex \ tilesWide

                ' Calculate source rectangle in the original tileset
                Dim srcX As Integer = tilesetX * TileSize
                Dim srcY As Integer = tilesetY * TileSize

                ' Check if source rectangle is valid
                If srcX + TileSize > currentTilesetBitmap.Width OrElse
               srcY + TileSize > currentTilesetBitmap.Height Then
                    Continue For
                End If

                ' Calculate destination rectangle in our grid
                Dim destX As Integer = col * (TileSize + TilePadding) - visibleAreaX
                Dim destY As Integer = row * (TileSize + TilePadding) - visibleAreaY

                ' Define rectangles
                Dim srcRect As New Rectangle(srcX, srcY, TileSize, TileSize)
                Dim destRect As New Rectangle(destX, destY, TileSize, TileSize)

                ' Draw the tile
                e.Graphics.DrawImage(currentTilesetBitmap, destRect, srcRect, GraphicsUnit.Pixel)

                ' Draw a border around the tile
                e.Graphics.DrawRectangle(Pens.Gray, destRect)

                ' Highlight the selected tile
                If tileIndex = selectedTileIndex Then
                    Using selectionPen As New Pen(System.Drawing.Color.Red, 2)
                        e.Graphics.DrawRectangle(selectionPen, destRect)
                    End Using
                End If
            Next
        Next
    End Sub


    ''' <summary>
    ''' Mouse down event handler - left-click selects a tile, right-click starts panning
    ''' </summary>
    Private Sub PnlTileSets_MouseDown(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            ' Calculate which tile was clicked in the grid
            Dim adjustedX As Integer = e.X + Math.Abs(pnlTileSets.AutoScrollPosition.X)
            Dim adjustedY As Integer = e.Y + Math.Abs(pnlTileSets.AutoScrollPosition.Y)

            Dim gridCol As Integer = adjustedX \ (TileSize + TilePadding)
            Dim gridRow As Integer = adjustedY \ (TileSize + TilePadding)

            ' Calculate the grid index
            Dim gridIndex As Integer = gridRow * TilesPerRow + gridCol

            ' Calculate total tiles displayed in the grid
            Dim totalTiles As Integer = tilesWide * tilesHigh

            ' Check if this is a valid grid position
            If gridIndex >= 0 AndAlso gridIndex < totalTiles Then
                ' Calculate the position in the original tileset
                ' We need to convert from grid position to tileset position

                ' When displaying tiles in a grid with TilesPerRow columns,
                ' we need to map back to the original tile's position in the tileset
                Dim tilesInCurrentRow As Integer = Math.Min(TilesPerRow, tilesWide - (gridRow * TilesPerRow))

                ' Calculate which tile in the source tileset this corresponds to
                Dim tileIndex As Integer = gridIndex

                ' Make sure we're within bounds
                If tileIndex < totalTiles Then
                    ' Log the click for debugging
                    Debug.WriteLine($"Clicked grid position ({gridCol},{gridRow}), grid index {gridIndex}, mapping to tile {tileIndex}")

                    ' Set as the active tile for mapping
                    SetActiveTile(tileIndex)
                End If
            End If
        ElseIf e.Button = MouseButtons.Right Then
            ' Start dragging for panning the view
            isDragging = True
            startScrollPos = e.Location
            pnlTileSets.Cursor = Cursors.Hand
        End If
    End Sub

    ''' <summary>
    ''' Makes the clicked tile the active placement tile: finds its tile mapping,
    ''' updates the GlobalVariables selection state, and refreshes the highlight
    ''' </summary>
    Private Sub SetActiveTile(tileIndex As Integer)
        If currentTilesetIndex < 0 OrElse mainForm Is Nothing Then
            Return
        End If

        Try
            ' Check if we're in copy mode - don't change selection during copy
            If GlobalVariables.IsCopyMode Then
                Debug.WriteLine("fTileSet: In copy mode - not updating selection")
                Return
            End If

            ' Check if we're in a restricted render mode
            If GlobalVariables.CurrentRenderMode = GlobalVariables.RenderMode.CellTypeBasic OrElse
           GlobalVariables.CurrentRenderMode = GlobalVariables.RenderMode.CellTypeAll Then
                MessageBox.Show($"Cannot place tiles while in {GlobalVariables.CurrentRenderMode} Render Mode.",
                          "Operation Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' Find matching tile mapping
            Dim foundMappingIndex As Integer = -1

            ' Search through mappings for a match
            For i As Integer = 0 To mainForm.CurrentLoadedMap.tileMappings.Count - 1
                If mainForm.CurrentLoadedMap.tileMappings(i).tilesetIndex = currentTilesetIndex AndAlso
   mainForm.CurrentLoadedMap.tileMappings(i).tileGraphicIndex = tileIndex Then
                    foundMappingIndex = i
                    Exit For
                End If
            Next

            ' Store the old selected tile index for invalidation
            Dim previousSelectedTileIndex As Integer = selectedTileIndex

            ' Update the selected tile index
            selectedTileIndex = tileIndex

            If foundMappingIndex >= 0 Then
                ' Deselect the cell-type tool when selecting a tile
                ' This is crucial - it tells the system we're now using tile placement, not cell type
                GlobalVariables.ActiveCellType = CellType.FastPassible1
                GlobalVariables.CellTypeToolActive = False

                ' We found a mapping - set it as active in the global state
                GlobalVariables.ActiveTileMappingIndex = foundMappingIndex
                Debug.WriteLine($"Set active tile mapping: {foundMappingIndex} (Tileset: {currentTilesetIndex}, Tile: {tileIndex})")

                ' Get the well filename (without extension)
                Dim wellFilename As String = cmbWellSelector.SelectedItem.ToString()
                If wellFilename.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) Then
                    wellFilename = wellFilename.Substring(0, wellFilename.Length - 4)
                End If

                ' Update the placement info in GlobalVariables
                GlobalVariables.SelectedTilesetName = wellFilename
                GlobalVariables.SelectedTileIndex = tileIndex
                GlobalVariables.SelectedTilesetIndex = currentTilesetIndex

                ' Extract the tile image for sending to the main form
                Try
                    ' Create a small bitmap of just this tile for preview
                    If currentTilesetBitmap IsNot Nothing Then
                        ' Calculate the source rectangle in the tileset
                        Dim tileX As Integer = tileIndex Mod tilesWide
                        Dim tileY As Integer = tileIndex \ tilesWide

                        ' The tile's position in pixels within the tileset
                        Dim srcX As Integer = tileX * TileSize
                        Dim srcY As Integer = tileY * TileSize

                        ' Create a bitmap just for this tile
                        Dim tileImage As New Bitmap(TileSize, TileSize)
                        Using g As Graphics = Graphics.FromImage(tileImage)
                            ' Draw just this tile from the tileset
                            g.DrawImage(currentTilesetBitmap,
                                New Rectangle(0, 0, TileSize, TileSize),
                                New Rectangle(srcX, srcY, TileSize, TileSize),
                                GraphicsUnit.Pixel)
                        End Using

                        ' Store the tile image in the global variable for the main form to access
                        If GlobalVariables.SelectedTileImage IsNot Nothing Then
                            GlobalVariables.SelectedTileImage.Dispose()
                        End If
                        GlobalVariables.SelectedTileImage = tileImage
                    End If
                Catch tileEx As Exception
                    Debug.WriteLine($"Error creating tile preview: {tileEx.Message}")
                End Try

                ' Update the main form's UI to show the current tile selection
                UpdateMainFormLabels()

                ' Invalidate only the affected areas
                If previousSelectedTileIndex >= 0 Then
                    ' Calculate the previous tile's position
                    Dim prevX As Integer = previousSelectedTileIndex Mod tilesWide
                    Dim prevY As Integer = previousSelectedTileIndex \ tilesWide

                    ' Calculate the display position in the grid
                    Dim gridIndex As Integer = (prevY * tilesWide + prevX)
                    Dim gridRow As Integer = gridIndex \ TilesPerRow
                    Dim gridCol As Integer = gridIndex Mod TilesPerRow

                    ' Calculate the previous tile's rectangle in panel coordinates
                    Dim prevRect As New Rectangle(
                gridCol * (TileSize + TilePadding),
                gridRow * (TileSize + TilePadding),
                TileSize + 4, TileSize + 4)

                    ' Account for scrolling
                    prevRect.Offset(-Math.Abs(pnlTileSets.AutoScrollPosition.X),
                   -Math.Abs(pnlTileSets.AutoScrollPosition.Y))

                    ' Invalidate previous selection
                    pnlTileSets.Invalidate(prevRect)
                End If

                ' Calculate the new selection's position in the grid
                Dim newX As Integer = tileIndex Mod tilesWide
                Dim newY As Integer = tileIndex \ tilesWide

                ' Calculate the grid display position
                Dim newGridIndex As Integer = (newY * tilesWide + newX)
                Dim newGridRow As Integer = newGridIndex \ TilesPerRow
                Dim newGridCol As Integer = newGridIndex Mod TilesPerRow

                ' Calculate new tile's rectangle in panel coordinates
                Dim newRect As New Rectangle(
            newGridCol * (TileSize + TilePadding),
            newGridRow * (TileSize + TilePadding),
            TileSize + 4, TileSize + 4)

                ' Account for scrolling
                newRect.Offset(-Math.Abs(pnlTileSets.AutoScrollPosition.X),
              -Math.Abs(pnlTileSets.AutoScrollPosition.Y))

                ' Invalidate new selection
                pnlTileSets.Invalidate(newRect)

                Debug.WriteLine($"Selected tile {tileIndex} at position ({newX},{newY}), grid pos ({newGridCol},{newGridRow})")
            Else
                Debug.WriteLine($"Could not find mapping for Tileset {currentTilesetIndex}, Tile {tileIndex}")

                ' Still update the UI for selection even if no mapping is found
                pnlTileSets.Invalidate()
            End If
        Catch ex As Exception
            Debug.WriteLine($"Error setting active tile: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Updates the main form's tool/placement labels via reflection
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
            Catch ex As Exception
                Debug.WriteLine("Error updating main form labels: " & ex.Message)
            End Try
        End If
    End Sub


    ''' <summary>
    ''' Mouse move event handler - pans the tileset view while right-dragging
    ''' </summary>
    Private Sub PnlTileSets_MouseMove(sender As Object, e As MouseEventArgs)
        If isDragging AndAlso e.Button = MouseButtons.Right Then
            ' Calculate the scroll delta
            Dim deltaX As Integer = startScrollPos.X - e.X
            Dim deltaY As Integer = startScrollPos.Y - e.Y

            ' Apply the scroll (note: AutoScrollPosition returns negative values)
            Dim newX As Integer = Math.Abs(pnlTileSets.AutoScrollPosition.X) + deltaX
            Dim newY As Integer = Math.Abs(pnlTileSets.AutoScrollPosition.Y) + deltaY

            pnlTileSets.AutoScrollPosition = New Point(newX, newY)

            startScrollPos = e.Location
        End If
    End Sub

    ''' <summary>
    ''' Mouse up event handler - ends a right-drag panning operation
    ''' </summary>
    Private Sub PnlTileSets_MouseUp(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Right Then
            isDragging = False
            pnlTileSets.Cursor = Cursors.Default
        End If
    End Sub

    ''' <summary>
    ''' Positions this form to the left of the specified reference form
    ''' </summary>
    Public Sub PositionWindowNextTo(referenceForm As Form)
        If referenceForm Is Nothing Then Return

        ' Ensure the reference form is visible
        If Not referenceForm.Visible Then Return

        ' Get the screen working area to ensure we stay within bounds
        Dim workingArea As Rectangle = Screen.FromControl(referenceForm).WorkingArea

        ' Position to the left of the reference form
        Dim newX As Integer = referenceForm.Location.X - Me.Width - 1
        Dim newY As Integer = referenceForm.Location.Y

        ' Make sure we don't go off-screen to the left
        If newX < workingArea.Left Then
            newX = workingArea.Left
        End If

        ' Set the new position
        Me.Location = New Point(newX, newY)

        ' Make sure we're not too high or low on the screen
        If Me.Bottom > workingArea.Bottom Then
            Me.Top = workingArea.Bottom - Me.Height
        End If

        If Me.Top < workingArea.Top Then
            Me.Top = workingArea.Top
        End If

        'Debug.WriteLine($"Positioned fTileSet at {Me.Location} to the left of {referenceForm.Name}")
    End Sub

End Class