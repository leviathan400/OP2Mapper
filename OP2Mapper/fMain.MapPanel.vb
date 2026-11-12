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

' fMain.MapPanel.vb
' Partial class of fMain (split out of fMain.vb).
' Map panel input and paint events - mouse painting, panning, zooming and rendering into pnlMap.
Partial Public Class fMain

#Region "Map Panel Events"



    ''' <summary>
    ''' Handles mouse down on the map panel. Left click dispatches by mode (paste,
    ''' tile group placement, copy selection, tile/cell edit, or plain tile select);
    ''' right click starts a pan drag.
    ''' </summary>
    Private Sub MapPanel_MouseDown(sender As Object, e As MouseEventArgs)
        If currentMap Is Nothing Then
            Return
        End If

        If e.Button = MouseButtons.Left Then
            ' Use the coordinate translation function to get map coordinates
            Dim mapCoords As Point = ScreenToMapCoordinates(e.X, e.Y)

            ' Check if coordinates are valid
            If mapCoords.X >= 0 AndAlso mapCoords.Y >= 0 AndAlso
       mapCoords.X < currentMap.WidthInTiles() AndAlso mapCoords.Y < currentMap.HeightInTiles() Then

                ' Handle paste mode clicks
                If isPasteMode Then
                    ' Execute the paste operation
                    ExecutePasteOperation(mapCoords.X, mapCoords.Y)
                    Return
                End If

                ' Handle tile group placement clicks
                If TileGroupPlacementMode Then
                    ExecuteTileGroupPlacement(mapCoords.X, mapCoords.Y)
                    Return
                End If

                ' Check if we're in edit mode
                If GlobalVariables.EditModeEnabled Then
                    ' Handle copy mode clicks
                    If GlobalVariables.IsCopyMode Then
                        If GlobalVariables.CopyStage = 0 Then
                            ' First click - set start position
                            GlobalVariables.CopyStartX = mapCoords.X
                            GlobalVariables.CopyStartY = mapCoords.Y
                            GlobalVariables.CopyStage = 1

                            ' Update visual indicator
                            UpdateToolDisplay(System.Drawing.Color.DarkGreen, "Copy start position selected")

                            ' Log the selection
                            Dim tileData As TileData = TileData.FromMap(currentMap, mapCoords.X, mapCoords.Y)
                            Debug.WriteLine($"Tile Copy start tile selected: ({mapCoords.X},{mapCoords.Y}) - " &
                            $"Tileset: {tileData.TilesetIndex}, Image: {tileData.ImageIndex}")

                            UpdateToolAndPlacementLabels()
                            Return
                        ElseIf GlobalVariables.CopyStage = 1 Then
                            ' Second click - set end position and complete copy
                            GlobalVariables.CopyEndX = mapCoords.X
                            GlobalVariables.CopyEndY = mapCoords.Y
                            GlobalVariables.CopyStage = 2

                            ' Update visual indicator
                            UpdateToolDisplay(System.Drawing.Color.DarkBlue, "Copy end position selected")

                            ' Log the selection
                            Dim tileData As TileData = TileData.FromMap(currentMap, mapCoords.X, mapCoords.Y)
                            Debug.WriteLine($"Tile Copy end tile selected: ({mapCoords.X},{mapCoords.Y}) - " &
                            $"Tileset: {tileData.TilesetIndex}, Image: {tileData.ImageIndex}")

                            ' Perform the actual copy
                            CompleteCopyOperation()

                            UpdateToolAndPlacementLabels()
                            Return
                        End If
                    End If

                    ' Determine if any valid tool is selected
                    Dim toolSelected As Boolean = (GlobalVariables.ActiveTileMappingIndex >= 0 AndAlso GlobalVariables.SelectedTileImage IsNot Nothing) OrElse
                              GlobalVariables.CellTypeToolActive

                    ' Only process editing actions if a tool is selected
                    If toolSelected Then
                        ' Record this as the starting point for dragging edit operations
                        isEditDragging = True
                        lastEditTileX = mapCoords.X
                        lastEditTileY = mapCoords.Y

                        ' Determine which tool to use based on what's selected
                        If GlobalVariables.ActiveTileMappingIndex >= 0 AndAlso GlobalVariables.SelectedTileImage IsNot Nothing Then
                            ' Tile placement mode
                            HandleTilePlacement(mapCoords.X, mapCoords.Y)
                        Else
                            ' Cell type editing mode
                            HandleEditModeClick(mapCoords.X, mapCoords.Y)
                        End If
                    Else
                        Debug.WriteLine($"No tool selected - ignoring click at ({mapCoords.X}, {mapCoords.Y})")
                    End If
                End If

                ' Remember the old selection position so we can erase its
                ' selection-box border.
                Dim previousSelectionX As Integer = currentTileX
                Dim previousSelectionY As Integer = currentTileY

                ' Set as current tile for display in property panel
                currentTileX = mapCoords.X
                currentTileY = mapCoords.Y

                ' Update property controls
                UpdatePropertyControls()

                ' Refresh just the old + new selection box positions.
                InvalidateTileRect(previousSelectionX, previousSelectionY)
                InvalidateTileRect(currentTileX, currentTileY)
            End If
        ElseIf e.Button = MouseButtons.Right Then
            ' Start dragging for panning the view
            isDragging = True
            isRightDragging = True
            rightClickStartPosition = e.Location
            lastMouseX = e.X
            lastMouseY = e.Y
            pnlMap.Cursor = Cursors.Hand
        End If
    End Sub


    ''' <summary>
    ''' Executes the actual paste operation at the specified position
    ''' </summary>
    Private Sub ExecutePasteOperation(startX As Integer, startY As Integer)
        Debug.WriteLine($"Executing paste at {startX}, {startY}")

        ' Make sure we're in paste mode and have data to paste
        If Not isPasteMode OrElse GlobalVariables.CopiedTileMappings Is Nothing Then
            Return
        End If

        ' Check if the paste region fits on the map
        If startX + GlobalVariables.CopiedRegionWidth > currentMap.WidthInTiles() OrElse
       startY + GlobalVariables.CopiedRegionHeight > currentMap.HeightInTiles() Then
            MessageBox.Show("Cannot paste at this location. The paste region would extend beyond the map boundary.",
                      "Paste Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Create a command to represent the entire paste operation
        Dim pasteCommand As New PasteTileMappingsCommand(currentMap, startX, startY,
                                                  GlobalVariables.CopiedTileMappings,
                                                  GlobalVariables.CopiedRegionWidth,
                                                  GlobalVariables.CopiedRegionHeight)

        ' Execute the command (this adds it to the undo stack)
        ExecuteCommand(pasteCommand)

        ' Repaint just the pasted region.
        InvalidateTileRegion(startX, startY,
                             GlobalVariables.CopiedRegionWidth, GlobalVariables.CopiedRegionHeight)

        ' Exit paste mode
        ExitPasteMode()

        ' Show confirmation
        Debug.WriteLine("Paste operation completed successfully")
    End Sub

    ''' <summary>
    ''' Handles mouse move on the map panel - updates the coordinate readout, moves the
    ''' paste/tile-group previews, paints while drag-editing, and pans during a right-drag.
    ''' </summary>
    Private Sub MapPanel_MouseMove(sender As Object, e As MouseEventArgs)
        If currentMap Is Nothing Then
            Return
        End If

        ' Use the coordinate translation function to get map coordinates
        Dim mapCoords As Point = ScreenToMapCoordinates(e.X, e.Y)

        ' Check if coordinates are valid
        If mapCoords.X >= 0 AndAlso mapCoords.Y >= 0 AndAlso
       mapCoords.X < currentMap.WidthInTiles() AndAlso mapCoords.Y < currentMap.HeightInTiles() Then
            ' Update status bar with valid coordinates
            lblToolStripCoordinates.Text = "Coordinates: " & FormatTileCoords(mapCoords.X, mapCoords.Y)

            ' Ensure we are in edit mode
            If EditModeEnabled = True Then

                ' Handle paste preview movement
                If isPasteMode Then
                    ' Check if the preview position has changed
                    If mapCoords.X <> pastePreviewX OrElse mapCoords.Y <> pastePreviewY Then
                        ' Remember where the preview was so we can erase it.
                        Dim oldPreviewX As Integer = pastePreviewX
                        Dim oldPreviewY As Integer = pastePreviewY

                        ' Update the paste preview position
                        pastePreviewX = mapCoords.X
                        pastePreviewY = mapCoords.Y

                        ' Update the label to show where we're pasting
                        lblCurrentPlacement.Text =
                            $"Click to paste {GlobalVariables.CopiedRegionWidth}x{GlobalVariables.CopiedRegionHeight} tiles at {FormatTileCoords(pastePreviewX, pastePreviewY)}"

                        ' Repaint old + new preview regions only.
                        InvalidateTileRegion(oldPreviewX, oldPreviewY,
                                             GlobalVariables.CopiedRegionWidth, GlobalVariables.CopiedRegionHeight)
                        InvalidateTileRegion(pastePreviewX, pastePreviewY,
                                             GlobalVariables.CopiedRegionWidth, GlobalVariables.CopiedRegionHeight)
                    End If
                    Return
                End If

                ' Handle tile group placement preview
                If TileGroupPlacementMode Then
                    If mapCoords.X <> GlobalVariables.TileGroupPreviewX OrElse mapCoords.Y <> GlobalVariables.TileGroupPreviewY Then
                        ' Remember where the preview was so we can erase it.
                        Dim oldGroupX As Integer = GlobalVariables.TileGroupPreviewX
                        Dim oldGroupY As Integer = GlobalVariables.TileGroupPreviewY

                        GlobalVariables.TileGroupPreviewX = mapCoords.X
                        GlobalVariables.TileGroupPreviewY = mapCoords.Y

                        lblCurrentPlacement.Text =
                            $"Click to place {GlobalVariables.TileGroupWidth}x{GlobalVariables.TileGroupHeight} tile group at {FormatTileCoords(GlobalVariables.TileGroupPreviewX, GlobalVariables.TileGroupPreviewY)}"

                        ' Repaint old + new preview regions only.
                        InvalidateTileRegion(oldGroupX, oldGroupY,
                                             GlobalVariables.TileGroupWidth, GlobalVariables.TileGroupHeight)
                        InvalidateTileRegion(GlobalVariables.TileGroupPreviewX, GlobalVariables.TileGroupPreviewY,
                                             GlobalVariables.TileGroupWidth, GlobalVariables.TileGroupHeight)
                    End If
                    Return
                End If

            End If

            ' Handle edit dragging if enabled and left mouse button is down
            If isEditDragging AndAlso GlobalVariables.EditModeEnabled AndAlso Not GlobalVariables.SingleTileEditMode Then
                ' Only process if the tile has changed from the last one we edited
                If mapCoords.X <> lastEditTileX OrElse mapCoords.Y <> lastEditTileY Then
                    ' Remember the old selection box position so we can repaint it.
                    Dim previousSelectionX As Integer = currentTileX
                    Dim previousSelectionY As Integer = currentTileY

                    ' Determine which tool to use based on what's selected
                    If GlobalVariables.ActiveTileMappingIndex >= 0 AndAlso GlobalVariables.SelectedTileImage IsNot Nothing Then
                        ' Tile placement mode
                        HandleTilePlacement(mapCoords.X, mapCoords.Y)
                    Else
                        ' Cell type editing mode
                        HandleEditModeClick(mapCoords.X, mapCoords.Y)
                    End If

                    ' Update the last edited position
                    lastEditTileX = mapCoords.X
                    lastEditTileY = mapCoords.Y

                    ' Move the selection box to follow the current edit position
                    currentTileX = mapCoords.X
                    currentTileY = mapCoords.Y

                    ' Update property controls to reflect the newly selected tile
                    UpdatePropertyControls()

                    ' The new tile was already invalidated by HandleTilePlacement /
                    ' HandleEditModeClick (via UpdateSingleTile). We just need to
                    ' redraw the previous selection-box position to erase its border.
                    InvalidateTileRect(previousSelectionX, previousSelectionY)
                End If
            End If
        End If

        ' Handle dragging (panning)
        If isDragging Then
            offsetX += (e.X - lastMouseX)
            offsetY += (e.Y - lastMouseY)
            ClampOffsets()
            pnlMap.Invalidate()
            SyncMiniMapViewport()
        End If

        lastMouseX = e.X
        lastMouseY = e.Y
    End Sub

    ''' <summary>
    ''' Handles mouse up on the map panel - ends edit/pan drags. For right button, shows
    ''' the context menu only if the cursor moved less than 5 px (i.e. it wasn't a pan).
    ''' </summary>
    Private Sub MapPanel_MouseUp(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            ' End edit dragging
            isEditDragging = False
            lastEditTileX = -1
            lastEditTileY = -1
        ElseIf e.Button = MouseButtons.Right Then
            ' Calculate drag distance more precisely
            Dim dragDistance As Double = Math.Sqrt(
        Math.Pow(e.X - rightClickStartPosition.X, 2) +
        Math.Pow(e.Y - rightClickStartPosition.Y, 2))

            ' Stop dragging
            isDragging = False

            ' Store whether this was a drag or not for future reference
            Dim wasDrag As Boolean = (dragDistance >= 5)
            isRightDragging = False  ' Reset the right drag flag

            ' Reset cursor
            pnlMap.Cursor = Cursors.Default

            ' Only show context menu if it wasn't a drag and we have a map loaded
            If Not wasDrag AndAlso currentMap IsNot Nothing Then
                ' Show context menu
                mapContextMenu.Show(pnlMap, e.Location)
            End If

            ' Reset the start position
            rightClickStartPosition = Point.Empty
        End If
    End Sub

    ''' <summary>
    ''' Paints the map panel: renders only the dirty clip region via MapRenderer, then
    ''' draws the overlays (enclosed-area highlight, paste preview, tile group preview,
    ''' selection rectangle) on top.
    ''' </summary>
    Private Sub MapPanel_Paint(sender As Object, e As PaintEventArgs)
        If currentMap Is Nothing OrElse mapRenderer Is Nothing Then
            Return
        End If

        ' Define the buffer size in pixels based on zoom level
        Const BaseTileSize As Integer = 32  ' Standard tile size
        Dim scaledBufferInTiles As Integer = CInt(Math.Ceiling(10 / zoomLevel))  ' More buffer at lower zoom
        Dim bufferSize As Integer = scaledBufferInTiles * BaseTileSize

        ' Render only the area WinForms says needs repainting. e.ClipRectangle
        ' is the union of every Invalidate(...) call since the last paint. By
        ' narrowing the renderer's viewport to the clip rect (plus a buffer
        ' for tiles that straddle the edge), Invalidate(smallRect) actually
        ' saves work — previously we computed a full-panel viewport every
        ' paint regardless. e.ClipRectangle is in screen pixels; convert to
        ' map-pixel (unscaled) coordinates for MapRenderer.
        Dim clip As Rectangle = e.ClipRectangle
        Dim viewportRect As New Rectangle(
            CInt((clip.X - offsetX) / zoomLevel) - bufferSize,
            CInt((clip.Y - offsetY) / zoomLevel) - bufferSize,
            CInt(clip.Width / zoomLevel) + (bufferSize * 2),
            CInt(clip.Height / zoomLevel) + (bufferSize * 2))

        ' Render the visible part of the map using the MapRenderer
        Using mapImage As Bitmap = mapRenderer.RenderMap(viewportRect, zoomLevel)
            ' Screen position of the bitmap's top-left = viewport top-left in
            ' map space, transformed back to screen space (× zoom + offset).
            Dim drawX As Integer = CInt(viewportRect.X * zoomLevel) + offsetX
            Dim drawY As Integer = CInt(viewportRect.Y * zoomLevel) + offsetY

            ' Draw the rendered map at the adjusted position
            e.Graphics.DrawImage(mapImage, drawX, drawY)

            ' Overlays below are drawn directly on e.Graphics — GDI+ clips
            ' each call to e.ClipRectangle automatically, so they only paint
            ' over the dirty region even when our viewport was narrow.

            ' Enclosed-areas highlight (toggled via Edit → Highlight Enclosed
            ' Areas). Magenta fill over each tile the Find & Fix tool would
            ' convert — risk-free visual preview before committing.
            If _highlightedEnclosedTiles IsNot Nothing AndAlso _highlightedEnclosedTiles.Count > 0 Then
                Dim scaledTile As Integer = CInt(32 * zoomLevel)
                Dim panelW As Integer = pnlMap.ClientSize.Width
                Dim panelH As Integer = pnlMap.ClientSize.Height
                Using highlightBrush As New SolidBrush(System.Drawing.Color.FromArgb(120, 255, 0, 255))
                    For Each tile In _highlightedEnclosedTiles
                        Dim sx As Integer = CInt(tile.X * 32 * zoomLevel) + offsetX
                        Dim sy As Integer = CInt(tile.Y * 32 * zoomLevel) + offsetY
                        ' Skip off-screen tiles to avoid wasting GDI calls.
                        If sx + scaledTile <= 0 OrElse sy + scaledTile <= 0 OrElse
                           sx >= panelW OrElse sy >= panelH Then Continue For
                        e.Graphics.FillRectangle(highlightBrush, sx, sy, scaledTile, scaledTile)
                    Next
                End Using
            End If

            ' Draw paste preview if in paste mode and have a valid preview position
            If isPasteMode AndAlso pastePreviewImage IsNot Nothing AndAlso
                pastePreviewX >= 0 AndAlso pastePreviewY >= 0 Then

                ' Calculate the screen position for the paste preview
                Dim screenRect As Rectangle = MapToScreenCoordinates(pastePreviewX, pastePreviewY)

                ' Draw the semi-transparent preview
                e.Graphics.DrawImage(pastePreviewImage, screenRect.X, screenRect.Y,
                               GlobalVariables.CopiedRegionWidth * 32 * zoomLevel,
                               GlobalVariables.CopiedRegionHeight * 32 * zoomLevel)
            End If

            ' Draw tile group preview if in placement mode
            If TileGroupPlacementMode AndAlso tileGroupPreviewImage IsNot Nothing AndAlso
                GlobalVariables.TileGroupPreviewX >= 0 AndAlso GlobalVariables.TileGroupPreviewY >= 0 Then

                Dim screenRect As Rectangle = MapToScreenCoordinates(GlobalVariables.TileGroupPreviewX,
                                                         GlobalVariables.TileGroupPreviewY)

                e.Graphics.DrawImage(tileGroupPreviewImage, screenRect.X, screenRect.Y,
                        GlobalVariables.TileGroupWidth * 32 * zoomLevel,
                        GlobalVariables.TileGroupHeight * 32 * zoomLevel)
            End If

            ' Draw selection rectangle if a tile is selected
            If currentTileX >= 0 AndAlso currentTileY >= 0 AndAlso
                currentTileX < currentMap.WidthInTiles() AndAlso currentTileY < currentMap.HeightInTiles() Then

                ' Use MapToScreenCoordinates to get the selection rectangle
                Dim selectionRect As Rectangle = MapToScreenCoordinates(currentTileX, currentTileY)

                ' Using-wrap so the Pen doesn't leak on every paint.
                Using selectionPen As New Pen(System.Drawing.Color.White, 2)
                    e.Graphics.DrawRectangle(selectionPen, selectionRect)
                End Using
            End If
        End Using
    End Sub




    ''' <summary>
    ''' Applies the active cell type to the given tile via an undoable command.
    ''' Switches to an overlay render mode first so the change is visible, and blocks
    ''' edits to the (normally locked) bottom row.
    ''' </summary>
    Private Sub HandleEditModeClick(mapX As Integer, mapY As Integer)
        ' Check if trying to edit the last row — this row isn't rendered in-game
        ' and is conventionally all-Impassable. Locked by default, but the user
        ' can flip My.Settings.AllowEditLastRow to override (some maps need it).
        If mapY = GlobalVariables.MapLastRowIndex AndAlso Not My.Settings.AllowEditLastRow Then
            MessageBox.Show(
                "Cant edit bottom row. This row is not visible in game and all cell types should be Impassable.",
                "Cannot Edit Last Row", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Switch to overlay mode for better editing visibility (for cell types) if needed
        ' BUT only if we're not already in one of the CellType-specific modes
        If CurrentRenderMode <> RenderMode.CellTypeOverlayAll AndAlso
            CurrentRenderMode <> RenderMode.CellTypeBasic AndAlso
            CurrentRenderMode <> RenderMode.CellTypeAll Then
            CurrentRenderMode = RenderMode.CellTypeOverlayAll
            pnlMap.Invalidate()
        End If

        ' Save previous cell type
        Dim previousCellType As CellType = currentMap.GetCellType(mapX, mapY)

        ' Only create a command if the cell type is actually changing
        If previousCellType <> GlobalVariables.ActiveCellType Then
            ' Create and execute the command
            Dim command As New ChangeCellTypeCommand(currentMap, mapX, mapY, previousCellType, GlobalVariables.ActiveCellType)
            ExecuteCommand(command)

            ' Update the tile display
            UpdateSingleTile(mapX, mapY)

            'Debug.WriteLine("Cell Type Updated - " & GlobalVariables.ActiveCellType.ToString & " - " & mapX & "," & mapY)
        End If
    End Sub





    ''' <summary>
    ''' Places the active tile mapping at the given tile via an undoable command.
    ''' No-op if the tile already uses that mapping.
    ''' </summary>
    Private Sub HandleTilePlacement(mapX As Integer, mapY As Integer)
        ' Check if we have a valid tile mapping selected
        If GlobalVariables.ActiveTileMappingIndex < 0 OrElse
       GlobalVariables.ActiveTileMappingIndex >= currentMap.tileMappings.Count Then
            Return
        End If

        ' Get the current mapping index at this position
        Dim currentMappingIndex As Integer = currentMap.GetTileMappingIndex(mapX, mapY)

        ' Don't do anything if the tile is already the same
        If currentMappingIndex = GlobalVariables.ActiveTileMappingIndex Then
            Return
        End If

        ' Create and execute a command to change the tile
        Dim command As New ChangeTileMappingCommand(currentMap, mapX, mapY, currentMappingIndex, GlobalVariables.ActiveTileMappingIndex)
        ExecuteCommand(command)

        ' Update the tile display
        UpdateSingleTile(mapX, mapY)

        Debug.WriteLine($"Placed tile mapping {GlobalVariables.ActiveTileMappingIndex} at position ({mapX},{mapY})")
    End Sub



    ''' <summary>
    ''' Updates a single tile on the map without regenerating the full map image
    ''' </summary>
    Private Sub UpdateSingleTile(x As Integer, y As Integer)
        InvalidateTileRect(x, y)

        ' Also update the tile preview if this is the currently selected tile
        If x = currentTileX AndAlso y = currentTileY Then
            UpdatePropertyControls()
        End If
    End Sub

    ''' <summary>
    ''' Invalidates the screen rectangle for a single tile, inflated by a small
    ''' margin so selection borders / grid lines are repainted too. Skips the
    ''' call when the tile is off-screen.
    ''' </summary>
    Private Sub InvalidateTileRect(tileX As Integer, tileY As Integer)
        If mapRenderer Is Nothing Then Return
        Dim r As Rectangle = MapToScreenCoordinates(tileX, tileY)
        r.Inflate(2, 2)
        If r.IntersectsWith(New Rectangle(0, 0, pnlMap.Width, pnlMap.Height)) Then
            pnlMap.Invalidate(r)
        End If
    End Sub

    ''' <summary>
    ''' Invalidates the screen rectangle covering a tile-aligned region.
    ''' Off-screen regions are skipped.
    ''' </summary>
    Private Sub InvalidateTileRegion(tileX As Integer, tileY As Integer,
                                     widthInTiles As Integer, heightInTiles As Integer)
        If mapRenderer Is Nothing OrElse widthInTiles <= 0 OrElse heightInTiles <= 0 Then Return
        Dim topLeft As Rectangle = MapToScreenCoordinates(tileX, tileY)
        Dim bottomRight As Rectangle = MapToScreenCoordinates(tileX + widthInTiles - 1, tileY + heightInTiles - 1)
        Dim r As New Rectangle(
            topLeft.X, topLeft.Y,
            bottomRight.Right - topLeft.X,
            bottomRight.Bottom - topLeft.Y)
        r.Inflate(2, 2)
        If r.IntersectsWith(New Rectangle(0, 0, pnlMap.Width, pnlMap.Height)) Then
            pnlMap.Invalidate(r)
        End If
    End Sub



    ''' <summary>
    ''' Handles mouse wheel on the map panel - steps through the fixed zoom levels,
    ''' keeping the map point under the cursor stationary while zooming.
    ''' </summary>
    Private Sub MapPanel_MouseWheel(sender As Object, e As MouseEventArgs)
        ' Only handle zoom if it's enabled
        If Not GlobalVariables.ZoomEnabled Then
            Return
        End If

        ' Store old zoom level for positioning calculations
        Dim oldZoom As Single = zoomLevel

        ' Store mouse position relative to map before zoom
        Dim mouseMapX As Single = (e.X - offsetX) / oldZoom
        Dim mouseMapY As Single = (e.Y - offsetY) / oldZoom

        ' Fixed zoom levels (in decimal form)
        Dim zoomLevels As Single() = {0.1F, 0.25F, 0.5F, 0.75F, 1.0F, 1.25F, 1.5F}

        ' Find current index in the zoom levels array
        Dim currentIndex As Integer = -1
        For i As Integer = 0 To zoomLevels.Length - 1
            ' Use a small epsilon for floating point comparison
            If Math.Abs(zoomLevel - zoomLevels(i)) < 0.01F Then
                currentIndex = i
                Exit For
            End If
        Next

        ' If current zoom is not in our defined levels, find closest level
        If currentIndex = -1 Then
            Dim minDifference As Single = Single.MaxValue
            For i As Integer = 0 To zoomLevels.Length - 1
                Dim difference As Single = Math.Abs(zoomLevel - zoomLevels(i))
                If difference < minDifference Then
                    minDifference = difference
                    currentIndex = i
                End If
            Next
        End If

        ' Adjust index based on scroll direction
        Dim newIndex As Integer = currentIndex
        If e.Delta > 0 Then
            ' Zoom in - move to next higher zoom level
            newIndex = Math.Min(zoomLevels.Length - 1, currentIndex + 1)
        Else
            ' Zoom out - move to next lower zoom level
            newIndex = Math.Max(0, currentIndex - 1)
        End If

        ' Set the new zoom level
        zoomLevel = zoomLevels(newIndex)

        ' Adjust offset to zoom toward the mouse position
        offsetX = CInt(e.X - mouseMapX * zoomLevel)
        offsetY = CInt(e.Y - mouseMapY * zoomLevel)

        ' Clamp the new offset so the map can't be panned/zoomed completely off-screen.
        ClampOffsets()

        ' Update zoom level label - show as percentage
        lblZoomLevel.Text = $"Zoom Level: {Math.Round(zoomLevel * 100)}%"

        ' Refresh display
        pnlMap.Invalidate()
        SyncMiniMapViewport()
    End Sub


    ''' <summary>
    ''' Handles View > Reset Zoom (Ctrl+R) and the Reset Zoom button - resets the view.
    ''' </summary>
    Private Sub ResetZoom_Click(sender As Object, e As EventArgs)
        ResetView()
    End Sub

    'Private Sub EnsureProperRendering()
    '    ' Enable double buffering for the map panel to prevent flickering
    '    pnlMap.GetType().GetProperty("DoubleBuffered",
    '    Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic) _
    '    .SetValue(pnlMap, True)

    '    ' Set other rendering optimizations
    '    pnlMap.SetStyle(ControlStyles.OptimizedDoubleBuffer, True)
    '    pnlMap.SetStyle(ControlStyles.AllPaintingInWmPaint, True)
    '    pnlMap.SetStyle(ControlStyles.UserPaint, True)
    'End Sub

    ''' <summary>
    ''' Returns the rectangle of map tile coordinates currently visible in
    ''' pnlMap. Used by the mini-map's viewport overlay.
    ''' </summary>
    Private Function GetVisibleTileRect() As Rectangle
        If currentMap Is Nothing Then Return Rectangle.Empty

        Dim leftTile As Integer = CInt(Math.Floor(-offsetX / (32 * zoomLevel)))
        Dim topTile As Integer = CInt(Math.Floor(-offsetY / (32 * zoomLevel)))
        Dim rightTile As Integer = CInt(Math.Ceiling((pnlMap.ClientSize.Width - offsetX) / (32 * zoomLevel)))
        Dim bottomTile As Integer = CInt(Math.Ceiling((pnlMap.ClientSize.Height - offsetY) / (32 * zoomLevel)))
        Return New Rectangle(leftTile, topTile, rightTile - leftTile, bottomTile - topTile)
    End Function

    ''' <summary>
    ''' Pans pnlMap so the given tile is centred in the viewport. Called when
    ''' the user clicks/drags on the mini-map.
    ''' </summary>
    Private Sub PanToCenterTile(tileX As Integer, tileY As Integer)
        If currentMap Is Nothing Then Return

        Dim tileCenterMapPxX As Single = (tileX + 0.5F) * 32
        Dim tileCenterMapPxY As Single = (tileY + 0.5F) * 32

        offsetX = CInt(pnlMap.ClientSize.Width / 2 - tileCenterMapPxX * zoomLevel)
        offsetY = CInt(pnlMap.ClientSize.Height / 2 - tileCenterMapPxY * zoomLevel)
        ClampOffsets()
        pnlMap.Invalidate()
        SyncMiniMapViewport()
    End Sub

    ''' <summary>
    ''' Re-renders the mini-map thumbnail immediately. Use for events where
    ''' rate isn't a concern (map load, render-mode change). For frequent
    ''' edits, prefer ScheduleMiniMapRefresh which debounces.
    ''' </summary>
    Private Sub RefreshMiniMap()
        If miniMapForm Is Nothing OrElse miniMapForm.IsDisposed Then Return
        miniMapForm.RefreshThumbnail(currentMap, mapRenderer)
        miniMapForm.UpdateViewport(GetVisibleTileRect())
    End Sub

    ''' <summary>
    ''' Schedules a debounced thumbnail refresh. Multiple calls within ~300 ms
    ''' coalesce into a single re-render. Use this for per-command edits so a
    ''' drag-edit doesn't re-render the thumbnail at every step.
    ''' </summary>
    Private Sub ScheduleMiniMapRefresh()
        If miniMapForm Is Nothing OrElse miniMapForm.IsDisposed Then Return
        If miniMapRefreshTimer Is Nothing Then
            miniMapRefreshTimer = New Timer With {.Interval = 300}
            AddHandler miniMapRefreshTimer.Tick, Sub(s, ev)
                                                     miniMapRefreshTimer.Stop()
                                                     RefreshMiniMap()
                                                 End Sub
        End If
        miniMapRefreshTimer.Stop()
        miniMapRefreshTimer.Start()
    End Sub

    ''' <summary>
    ''' Cheap update — just pushes the new viewport rect to the mini-map.
    ''' Call after pan/zoom; no thumbnail re-render needed.
    ''' </summary>
    Private Sub SyncMiniMapViewport()
        If miniMapForm Is Nothing OrElse miniMapForm.IsDisposed Then Return
        miniMapForm.UpdateViewport(GetVisibleTileRect())
    End Sub

    ''' <summary>
    ''' Handler for fMiniMap.JumpToTile — pans the main view to the clicked tile.
    ''' </summary>
    Private Sub MiniMap_JumpToTile(tileX As Integer, tileY As Integer)
        PanToCenterTile(tileX, tileY)
    End Sub

    ''' <summary>
    ''' Clamps offsetX/offsetY so the user can't pan/zoom the map off-screen.
    ''' Allows up to ~64 px of black around the map when the map is larger
    ''' than the panel; for smaller maps the whole map stays on-screen.
    ''' </summary>
    Private Sub ClampOffsets()
        If currentMap Is Nothing OrElse pnlMap Is Nothing Then Return

        Const margin As Integer = 64
        Dim scaledW As Integer = CInt(currentMap.WidthInTiles() * 32 * zoomLevel)
        Dim scaledH As Integer = CInt(currentMap.HeightInTiles() * 32 * zoomLevel)
        Dim pw As Integer = pnlMap.ClientSize.Width
        Dim ph As Integer = pnlMap.ClientSize.Height

        If scaledW <= pw Then
            offsetX = Math.Max(0, Math.Min(pw - scaledW, offsetX))
        Else
            offsetX = Math.Max(pw - scaledW - margin, Math.Min(margin, offsetX))
        End If

        If scaledH <= ph Then
            offsetY = Math.Max(0, Math.Min(ph - scaledH, offsetY))
        Else
            offsetY = Math.Max(ph - scaledH - margin, Math.Min(margin, offsetY))
        End If
    End Sub

    ''' <summary>
    ''' Resets zoom to 100% and centres the map in the panel when it is smaller than
    ''' the panel (otherwise snaps to the top-left).
    ''' </summary>
    Private Sub ResetView()
        If currentMap Is Nothing Then
            Return
        End If

        ' Reset zoom to 100%
        zoomLevel = 1.0F

        ' Center the map in the panel if it's smaller than the panel
        Dim mapWidthPixels As Integer = CInt(currentMap.WidthInTiles()) * 32
        Dim mapHeightPixels As Integer = CInt(currentMap.HeightInTiles()) * 32

        If mapWidthPixels < pnlMap.ClientSize.Width Then
            offsetX = (pnlMap.ClientSize.Width - mapWidthPixels) \ 2
        Else
            offsetX = 0
        End If

        If mapHeightPixels < pnlMap.ClientSize.Height Then
            offsetY = (pnlMap.ClientSize.Height - mapHeightPixels) \ 2
        Else
            offsetY = 0
        End If

        ' Update zoom level label
        lblZoomLevel.Text = "Zoom Level: 100%"

        ' Refresh display
        pnlMap.Invalidate()
        SyncMiniMapViewport()
    End Sub

#End Region

End Class
