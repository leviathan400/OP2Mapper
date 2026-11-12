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

' fMain.Clipboard.vb
' Partial class of fMain (split out of fMain.vb).
' Copy/paste machinery - copy mode, paste mode with live preview, clipboard state and tool clearing.
Partial Public Class fMain

#Region "Copy and Paste"

    ''' <summary>
    ''' Context menu handler: makes the tile under the right-click location the active
    ''' placement tool (sets the global tile selection and updates the preview/labels).
    ''' </summary>
    Private Sub SelectTileUnderCursor_Click(sender As Object, e As EventArgs)
        'Debug.WriteLine("SelectTileUnderCursor_Click triggered")

        ' Only proceed if a map is loaded and we have a valid context menu position
        If currentMap Is Nothing Then
            Debug.WriteLine("No map loaded")
            Return
        End If

        If contextMenuOpenLocation.IsEmpty Then
            Debug.WriteLine("No context menu location available")
            Return
        End If

        ' Convert the context menu open location to map coordinates
        Dim mapCoords As Point = ScreenToMapCoordinates(contextMenuOpenLocation.X, contextMenuOpenLocation.Y)

        ' Check if the coordinates are valid
        If mapCoords.X < 0 OrElse mapCoords.Y < 0 OrElse
       mapCoords.X >= currentMap.WidthInTiles() OrElse mapCoords.Y >= currentMap.HeightInTiles() Then
            Debug.WriteLine($"Invalid map coordinates: {mapCoords.X}, {mapCoords.Y}")
            Return
        End If

        'Debug.WriteLine($"Attempting to select tile at {mapCoords.X}, {mapCoords.Y}")

        Try
            ' Get the tile data at the cursor position
            Dim tileData As TileData = TileData.FromMap(currentMap, mapCoords.X, mapCoords.Y)
            'Debug.WriteLine($"TileData - TilesetIndex: {tileData.TilesetIndex}, ImageIndex: {tileData.ImageIndex}, MappingIndex: {tileData.TileMappingIndex}")

            ' Check if we have a valid tileset and mapping index
            If tileData.TilesetIndex < 0 OrElse tileData.ImageIndex < 0 Then
                Debug.WriteLine("Invalid tile data - no valid tileset or image index")
                MessageBox.Show("No valid tile found at the cursor position.", "No Tile", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' Get the tileset name
            Dim tilesetName As String = ""
            If tileData.TilesetIndex < currentMap.tilesetSources.Count Then
                tilesetName = currentMap.tilesetSources(tileData.TilesetIndex).tilesetFilename
                'Debug.WriteLine($"Found tileset name: {tilesetName}")
            Else
                Debug.WriteLine("Tileset index out of range")
                Return
            End If

            ' Set this directly in the global variables
            GlobalVariables.ActiveTileMappingIndex = tileData.TileMappingIndex
            GlobalVariables.SelectedTilesetName = tilesetName
            GlobalVariables.SelectedTileIndex = tileData.ImageIndex
            GlobalVariables.SelectedTilesetIndex = tileData.TilesetIndex

            Debug.WriteLine($"Set globals - MappingIndex: {GlobalVariables.ActiveTileMappingIndex}, " &
                        $"Tileset: {GlobalVariables.SelectedTilesetName}, " &
                        $"TileIndex: {GlobalVariables.SelectedTileIndex}, " &
                        $"TilesetIndex: {GlobalVariables.SelectedTilesetIndex}")

            ' Extract tile image for preview
            If mapRenderer IsNot Nothing Then
                ' Get the raw tile image directly
                Using tileImage As Bitmap = mapRenderer.GetRawTileImage(mapCoords.X, mapCoords.Y)
                    If tileImage IsNot Nothing Then
                        ' Create a copy for the global variable
                        If GlobalVariables.SelectedTileImage IsNot Nothing Then
                            GlobalVariables.SelectedTileImage.Dispose()
                        End If

                        GlobalVariables.SelectedTileImage = New Bitmap(tileImage)
                        'Debug.WriteLine("Created tile image for preview")

                    Else
                        Debug.WriteLine("Failed to get raw tile image")

                    End If
                End Using
            End If

            ' Deselect the cell-type tool to indicate we're now using tile placement
            GlobalVariables.ActiveCellType = CellType.FastPassible1
            GlobalVariables.CellTypeToolActive = False

            ' Also update the current selection to show the selected tile
            currentTileX = mapCoords.X
            currentTileY = mapCoords.Y

            ' Update property controls to reflect the newly selected tile
            UpdatePropertyControls()

            ' Update tool and placement labels
            UpdateToolAndPlacementLabels()
            'Debug.WriteLine("Updated tool and placement labels")

            ' Redraw the map with the new selection
            pnlMap.Invalidate()

            ' Show a message to confirm the selection
            'MessageBox.Show($"Selected tile from {tilesetName} (Index: {tileData.ImageIndex})",
            '           "Tile Selected", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' Reset the context menu location after using it
            contextMenuOpenLocation = Point.Empty
        Catch ex As Exception
            Debug.WriteLine($"Error in SelectTileUnderCursor_Click: {ex.Message}")
            Debug.WriteLine(ex.StackTrace)
            MessageBox.Show($"Error selecting tile: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Context menu handler for the not-yet-implemented Replace CellTypes feature.
    ''' </summary>
    Private Sub ReplaceCellTypes_Click(sender As Object, e As EventArgs)
        ' This is a placeholder for a future feature
        MessageBox.Show("Replace CellTypes feature is not implemented yet.", "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub




    ''' <summary>
    ''' Menu handler for Edit → Copy. Starts copy mode (region selection) unless the
    ''' current render mode restricts tile operations.
    ''' </summary>
    Private Sub Copy_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return

        ' Check for restricted render mode
        If IsRestrictedRenderMode() Then
            MessageBox.Show($"Cannot copy tiles while in {CurrentRenderMode} Render Mode.",
                      "Operation Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Start copy mode
        InitializeCopyMode()

        ' Ensure the selected tool is visible with the right color
        picSelectedTool.BackColor = System.Drawing.Color.Green
        picSelectedTool.Show()
        picSelectedTool.Refresh()  ' Force immediate redraw

        ' Update UI
        UpdateToolAndPlacementLabels()

        Application.DoEvents()  ' Force immediate UI update
    End Sub

    ''' <summary>
    ''' Menu handler for Edit → Paste. Validates that copied tile data exists, exits any
    ''' in-progress copy mode (keeping the data) and enters paste mode.
    ''' </summary>
    Private Sub Paste_Click(sender As Object, e As EventArgs)
        ' Make sure we have a map loaded
        If currentMap Is Nothing Then
            MessageBox.Show("No map loaded. Please open a map first.", "Cannot Paste", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Check for restricted render mode
        If IsRestrictedRenderMode() Then
            MessageBox.Show($"Cannot paste tiles while in {CurrentRenderMode} Render Mode.",
                      "Operation Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Stop any active confirmation timer - we don't need to wait for it
        If copyConfirmationTimer.Enabled Then
            copyConfirmationTimer.Stop()
            Debug.WriteLine("Stopped active copy confirmation timer for immediate paste")
        End If

        ' If we're in copy mode but want to paste immediately, exit copy mode properly
        ' but preserve the copied data
        If GlobalVariables.IsCopyMode Then
            Debug.WriteLine("Exiting copy mode for immediate paste")
            GlobalVariables.IsCopyMode = False
            GlobalVariables.CopyStage = 0
        End If

        ' Check if we have something to paste
        If GlobalVariables.CopiedTileMappings Is Nothing OrElse
       GlobalVariables.CopiedRegionWidth <= 0 OrElse
       GlobalVariables.CopiedRegionHeight <= 0 Then
            MessageBox.Show("No tiles copied. Please copy tiles first.", "Cannot Paste", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Initialize paste mode
        InitializePasteMode()
    End Sub



    ''' <summary>
    ''' Enters copy mode at stage 0 (waiting for the start tile) and updates the
    ''' tool display, labels and status bar to match.
    ''' </summary>
    Private Sub InitializeCopyMode()
        ' Reset copy mode state
        GlobalVariables.IsCopyMode = True
        GlobalVariables.CopyStage = 0
        GlobalVariables.CopyStartX = -1
        GlobalVariables.CopyStartY = -1
        GlobalVariables.CopyEndX = -1
        GlobalVariables.CopyEndY = -1

        ' Update visual indicator for copy mode
        UpdateToolDisplay(System.Drawing.Color.Green, "Copy mode started")

        ' Make sure to update the tool and placement labels
        lblCurrentTool.Text = "Current Tool: Tile Copy/Paste"
        lblCurrentTool.ForeColor = System.Drawing.Color.DarkGreen
        lblCurrentPlacement.Text = "Selecting start copy tile"
        lblCurrentPlacement.ForeColor = System.Drawing.Color.Green

        ' Apply UI changes immediately
        lblCurrentTool.Refresh()
        lblCurrentPlacement.Refresh()
        picSelectedTool.Show()
        picSelectedTool.Refresh()

        ' Update the status bar
        UpdateStatusForEditMode()
        'statusStrip.Items(0).Text = "Copy Mode: Select start position"
        'statusStrip.Refresh()

        Debug.WriteLine("Starting Tile Copy/Paste")
    End Sub

    ''' <summary>
    ''' Initializes paste mode with appropriate UI updates
    ''' </summary>
    Private Sub InitializePasteMode()
        Debug.WriteLine("Initializing paste mode")

        ' If a confirmation timer is active, stop it
        If copyConfirmationTimer.Enabled Then
            copyConfirmationTimer.Stop()
        End If

        ' Exit copy mode if we're in it (but preserve the copied data)
        If GlobalVariables.IsCopyMode Then
            ' Just reset the flags but keep the data
            GlobalVariables.IsCopyMode = False
            GlobalVariables.CopyStage = 0
        End If

        ' Set paste mode flag
        isPasteMode = True

        ' Clear any previous display state that might interfere
        If picSelectedTool IsNot Nothing AndAlso picSelectedTool.Image IsNot Nothing Then
            picSelectedTool.Image.Dispose()
            picSelectedTool.Image = Nothing
        End If

        ' Create a preview image for the paste region
        CreatePastePreviewImage()

        ' Update the UI
        lblCurrentTool.Text = "Current Tool: Paste Tiles"
        lblCurrentTool.ForeColor = System.Drawing.Color.DarkBlue
        lblCurrentPlacement.Text = $"Click location to paste {GlobalVariables.CopiedRegionWidth}x{GlobalVariables.CopiedRegionHeight} tiles"
        lblCurrentPlacement.ForeColor = System.Drawing.Color.Blue

        ' Update tool display
        If picSelectedTool IsNot Nothing Then
            ' Create a small visualization of the region to paste
            Using regionIndicator As New Bitmap(32, 32)
                Using g As Graphics = Graphics.FromImage(regionIndicator)
                    ' Draw a white box with blue outline to represent paste area
                    g.Clear(System.Drawing.Color.White)
                    g.DrawRectangle(New Pen(System.Drawing.Color.Blue, 2), 2, 2, 28, 28)

                    ' Draw text showing the dimensions if it fits
                    Using font As New Font("Arial", 7, FontStyle.Bold)
                        Dim text As String = $"{GlobalVariables.CopiedRegionWidth}x{GlobalVariables.CopiedRegionHeight}"
                        Dim size = g.MeasureString(text, font)

                        ' Only draw text if it fits in the box
                        If size.Width < 28 Then
                            g.DrawString(text, font, Brushes.Blue,
                               (32 - size.Width) / 2,
                               (32 - size.Height) / 2)
                        End If
                    End Using
                End Using

                ' Set as the selected tool image
                If picSelectedTool.Image IsNot Nothing Then
                    picSelectedTool.Image.Dispose()
                End If
                picSelectedTool.Image = New Bitmap(regionIndicator)
                picSelectedTool.BackColor = System.Drawing.Color.White
            End Using
        End If

        ' Show status message
        UpdateStatusForEditMode()
        statusStrip.Items(0).Text = "Paste Mode: Click to paste tiles"

        ' Update cursor to indicate paste mode
        pnlMap.Cursor = Cursors.Cross
    End Sub

    ''' <summary>
    ''' Creates a preview image for visualizing the paste region
    ''' </summary>
    Private Sub CreatePastePreviewImage()
        Debug.WriteLine("Creating paste preview image")

        ' Dispose any existing preview image
        If pastePreviewImage IsNot Nothing Then
            pastePreviewImage.Dispose()
            pastePreviewImage = Nothing
        End If

        ' We need the map renderer to get tile images
        If mapRenderer Is Nothing Then
            Debug.WriteLine("MapRenderer is null, cannot create preview")
            Return
        End If

        Try
            ' Ensure dimensions are valid
            If GlobalVariables.CopiedRegionWidth <= 0 OrElse GlobalVariables.CopiedRegionHeight <= 0 Then
                Debug.WriteLine($"Invalid region dimensions: {GlobalVariables.CopiedRegionWidth}x{GlobalVariables.CopiedRegionHeight}")
                Return
            End If

            ' Create a composite image of the copied region
            Dim width As Integer = GlobalVariables.CopiedRegionWidth * 32
            Dim height As Integer = GlobalVariables.CopiedRegionHeight * 32

            ' Create the preview image
            pastePreviewImage = New Bitmap(width, height)

            ' Use semi-transparent overlay to show what will be pasted
            Using g As Graphics = Graphics.FromImage(pastePreviewImage)
                ' Set transparency for preview
                g.Clear(System.Drawing.Color.Transparent)

                ' Validate the copied tiles array
                If GlobalVariables.CopiedTileMappings Is Nothing Then
                    Debug.WriteLine("CopiedTileMappings is null")
                    Throw New InvalidOperationException("No tile data to preview")
                End If

                ' Ensure array dimensions match expected size
                If GlobalVariables.CopiedTileMappings.GetLength(0) < GlobalVariables.CopiedRegionWidth OrElse
               GlobalVariables.CopiedTileMappings.GetLength(1) < GlobalVariables.CopiedRegionHeight Then
                    Debug.WriteLine("CopiedTileMappings array dimensions don't match region size")
                    ' Continue anyway, we'll just show what we can
                End If

                ' Hoist the 70%-opacity ColorMatrix + ImageAttributes out of the
                ' per-tile loop. The opacity is the same for every tile so
                ' creating a fresh one per tile was wasteful (and the
                ' ImageAttributes wasn't even Using-wrapped consistently).
                Dim cm As New Imaging.ColorMatrix()
                cm.Matrix33 = 0.7F ' 70% opacity

                Using ia As New Imaging.ImageAttributes()
                    ia.SetColorMatrix(cm)

                    ' Build preview image from each copied tile
                    For y As Integer = 0 To Math.Min(GlobalVariables.CopiedRegionHeight - 1, GlobalVariables.CopiedTileMappings.GetLength(1) - 1)
                        For x As Integer = 0 To Math.Min(GlobalVariables.CopiedRegionWidth - 1, GlobalVariables.CopiedTileMappings.GetLength(0) - 1)
                            ' Get the mapping index for this tile
                            Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)

                            ' Skip invalid mapping indices
                            If mappingIndex < 0 OrElse mappingIndex >= currentMap.tileMappings.Count Then
                                Continue For
                            End If

                            ' Get the tileset index and image index for this mapping
                            Dim tilesetIndex As Integer = currentMap.tileMappings(mappingIndex).tilesetIndex
                            Dim imageIndex As Integer = currentMap.tileMappings(mappingIndex).tileGraphicIndex

                            ' Skip invalid indices
                            If tilesetIndex < 0 OrElse imageIndex < 0 Then
                                Continue For
                            End If

                            ' Try to get the tile image from the renderer.
                            ' NOTE: GetTileImageByIndices returns a REFERENCE to
                            ' a bitmap owned by the renderer's tileset cache —
                            ' do not dispose it, or subsequent renders lose that
                            ' tile and show black/fallback.
                            Try
                                Dim tileImage As Bitmap = mapRenderer.GetTileImageByIndices(tilesetIndex, imageIndex)
                                If tileImage Is Nothing Then
                                    Continue For
                                End If

                                ' Draw the semi-transparent tile
                                g.DrawImage(tileImage,
                                   New Rectangle(x * 32, y * 32, 32, 32),
                                   0, 0, tileImage.Width, tileImage.Height,
                                   GraphicsUnit.Pixel, ia)
                            Catch ex As Exception
                                Debug.WriteLine($"Error drawing tile at {x},{y}: {ex.Message}")
                                ' Just continue to the next tile
                            End Try
                        Next
                    Next
                End Using

                ' Add a border around the entire paste region. Using-wrap so
                ' the Pen doesn't leak every time the preview is rebuilt.
                Using borderPen As New Pen(System.Drawing.Color.White, 2)
                    g.DrawRectangle(borderPen, 0, 0, width - 1, height - 1)
                End Using
            End Using

        Catch ex As Exception
            Debug.WriteLine($"Error creating paste preview: {ex.Message}")

            ' Clean up in case of error
            If pastePreviewImage IsNot Nothing Then
                pastePreviewImage.Dispose()
                pastePreviewImage = Nothing
            End If
        End Try
    End Sub


    ''' <summary>
    ''' Exit paste mode and reset UI
    ''' </summary>
    Private Sub ExitPasteMode()
        Debug.WriteLine("Exiting paste mode")

        ' Reset paste mode flag
        isPasteMode = False

        ' Clean up preview image
        If pastePreviewImage IsNot Nothing Then
            pastePreviewImage.Dispose()
            pastePreviewImage = Nothing
        End If

        ' Reset paste preview position
        pastePreviewX = -1
        pastePreviewY = -1

        ' Reset current tile selection to clear the white box
        currentTileX = -1
        currentTileY = -1

        ' Reset UI
        ResetToolUI(True)  ' Keep copied data for future pastes

        ' Reset tile information labels to N/A state
        lblTileset.Text = "Tileset: N/A"
        lblTilesetImage.Text = "Image Index: N/A"
        lblMappingIndex.Text = "Mapping Index: N/A"
        lblCoordinates.Text = "Coordinates: N/A"
        lblCellType.Text = "Cell Type: N/A"

        ' Clear the tile preview picture boxes
        If picTilePreview.Image IsNot Nothing Then
            picTilePreview.Image.Dispose()
            picTilePreview.Image = Nothing
        End If

        If picTilePreviewBig.Image IsNot Nothing Then
            picTilePreviewBig.Image.Dispose()
            picTilePreviewBig.Image = Nothing
        End If

        ' Reset cursor
        pnlMap.Cursor = Cursors.Default

        ' Reset status
        UpdateStatusForEditMode()

        ' Force a complete redraw of the map panel to remove the selection box
        pnlMap.Invalidate()
        pnlMap.Refresh()
    End Sub



    ''' <summary>
    ''' Finishes a copy: normalizes the start/end coordinates into a rectangle, snapshots
    ''' the tile mapping indices into the clipboard arrays and enables the Paste menu item.
    ''' </summary>
    Private Sub CompleteCopyOperation()
        ' Ensure we have valid coordinates
        If GlobalVariables.CopyStartX < 0 OrElse GlobalVariables.CopyStartY < 0 OrElse
   GlobalVariables.CopyEndX < 0 OrElse GlobalVariables.CopyEndY < 0 Then
            Debug.WriteLine("Invalid copy coordinates")
            Return
        End If

        ' Calculate region dimensions (normalize coordinates)
        Dim startX As Integer = Math.Min(GlobalVariables.CopyStartX, GlobalVariables.CopyEndX)
        Dim endX As Integer = Math.Max(GlobalVariables.CopyStartX, GlobalVariables.CopyEndX)
        Dim startY As Integer = Math.Min(GlobalVariables.CopyStartY, GlobalVariables.CopyEndY)
        Dim endY As Integer = Math.Max(GlobalVariables.CopyStartY, GlobalVariables.CopyEndY)

        GlobalVariables.CopiedRegionWidth = endX - startX + 1
        GlobalVariables.CopiedRegionHeight = endY - startY + 1

        ' Create an array to store the copied mappings
        GlobalVariables.CopiedTileMappings = New Integer(GlobalVariables.CopiedRegionWidth - 1,
                                               GlobalVariables.CopiedRegionHeight - 1) {}

        ' Copy the tile mappings
        For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
            For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                Dim mapX As Integer = startX + x
                Dim mapY As Integer = startY + y

                ' Get the mapping index
                Dim mappingIndex As Integer = currentMap.GetTileMappingIndex(mapX, mapY)
                GlobalVariables.CopiedTileMappings(x, y) = mappingIndex
            Next
        Next

        Debug.WriteLine($"Copied region size: {GlobalVariables.CopiedRegionWidth}x{GlobalVariables.CopiedRegionHeight} tiles")

        ' Enable paste menu item now that we have copied tiles
        Dim mnuPaste As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuEdit", False)(0), ToolStripMenuItem).DropDownItems.Find("mnuPaste", False)(0)
        If mnuPaste IsNot Nothing Then
            mnuPaste.Enabled = True
        End If

        ' Update the UI to show the copied region info
        UpdateToolDisplay(System.Drawing.Color.DarkBlue, "Region Copied")
        UpdateToolAndPlacementLabels()

        '' Update the tile selection form if it's open
        'For Each form As Form In Application.OpenForms
        '    If TypeOf form Is fTileSelect Then
        '        DirectCast(form, fTileSelect).UpdateCopiedTilesPreview()
        '        Exit For
        '    End If
        'Next

        ' Update the tile selection form if it's open
        If tileSelectForm IsNot Nothing AndAlso tileSelectForm.Visible Then
            tileSelectForm.UpdateCopiedTilesPreview()
        End If

        ' Start the timer to reset the UI after 2 seconds
        copyConfirmationTimer.Start()
    End Sub



    ''' <summary>
    ''' Sets the selected-tool picture box to a solid color indicator (used as visual
    ''' feedback for copy/paste mode changes) and forces an immediate repaint.
    ''' </summary>
    Private Sub UpdateToolDisplay(color As System.Drawing.Color, message As String)
        Debug.WriteLine($"UpdateToolDisplay: {message} | Color: {color.Name} | IsCopyMode: {GlobalVariables.IsCopyMode}")

        ' Clear any existing image
        If picSelectedTool.Image IsNot Nothing Then
            picSelectedTool.Image.Dispose()
            picSelectedTool.Image = Nothing
            Debug.WriteLine("  - Cleared existing image")
        End If

        ' Configure the control properties
        picSelectedTool.BorderStyle = BorderStyle.FixedSingle
        picSelectedTool.BackColor = color
        picSelectedTool.SizeMode = PictureBoxSizeMode.Normal
        picSelectedTool.Visible = True

        ' Force immediate visual update
        picSelectedTool.Invalidate()
        picSelectedTool.Update()
        picSelectedTool.Refresh()

        Debug.WriteLine($"  - Tool display configured for {message}")
    End Sub

    ''' <summary>
    ''' Cancels copy mode: resets the copy flags, discards any partial copy data and
    ''' disables the Paste menu item. Use ExitPasteMode/timer paths to keep copied data.
    ''' </summary>
    Private Sub ExitCopyMode()
        Debug.WriteLine("Exiting copy mode")

        ' Clear the tool display first (visual feedback)
        UpdateToolDisplay(System.Drawing.Color.Black, "Copy mode ended")

        statusStrip.Items(0).Text = currentMapFile ' What default text should be shown??

        ' Reset copy mode flags
        GlobalVariables.IsCopyMode = False
        GlobalVariables.CopyStage = 0

        ' Update UI based on current state
        UpdateToolAndPlacementLabels()

        ' If we're explicitly canceling a copy operation, 
        ' we should also clear any partial copy data
        GlobalVariables.CopiedTileMappings = Nothing
        GlobalVariables.CopiedRegionWidth = 0
        GlobalVariables.CopiedRegionHeight = 0

        ' Disable paste menu item since we've canceled the copy
        Try
            Dim mnuPaste As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuEdit", False)(0), ToolStripMenuItem).DropDownItems.Find("mnuPaste", False)(0)
            If mnuPaste IsNot Nothing Then
                mnuPaste.Enabled = False
            End If
        Catch ex As Exception
            Debug.WriteLine("Error updating paste menu item: " & ex.Message)
        End Try
    End Sub


    ''' <summary>
    ''' Copy confirmation timer tick - fires 2 seconds after a copy completes to reset
    ''' the tool display back to its idle state (the copied data itself is kept).
    ''' </summary>
    Private Sub CopyConfirmationTimer_Tick(sender As Object, e As EventArgs)
        ' Stop the timer
        copyConfirmationTimer.Stop()

        ' Explicitly ensure copy mode is exited
        GlobalVariables.IsCopyMode = False
        GlobalVariables.CopyStage = 0

        ' Reset label text and appearance immediately
        lblCurrentTool.Text = "No Tool Selected"
        lblCurrentPlacement.Text = "No Placement Selected"
        lblCurrentTool.ForeColor = System.Drawing.SystemColors.GrayText
        lblCurrentPlacement.ForeColor = System.Drawing.SystemColors.GrayText

        ' Reset the tool display box
        If picSelectedTool IsNot Nothing Then
            If picSelectedTool.Image IsNot Nothing Then
                picSelectedTool.Image.Dispose()
                picSelectedTool.Image = Nothing
            End If
            picSelectedTool.BackColor = System.Drawing.Color.Black
        End If

        ' Reset tool state
        GlobalVariables.ActiveCellType = CellType.FastPassible1
        GlobalVariables.CellTypeToolActive = False
        GlobalVariables.ActiveTileMappingIndex = -1

        ' Clear TileSet selection
        ClearTileSetFormSelection()

        ' Force immediate UI update
        lblCurrentTool.Refresh()
        lblCurrentPlacement.Refresh()
        picSelectedTool.Refresh()

        Debug.WriteLine("Copy confirmation timer elapsed - reset tool display")

        ' Force full form update
        Application.DoEvents()

        ' Invalidate the map to ensure it's redrawn with updated state
        pnlMap.Invalidate()
    End Sub




    ''' <summary>
    ''' Returns True if the current render mode restricts tile placement
    ''' </summary>
    Private Function IsRestrictedRenderMode() As Boolean
        Return (GlobalVariables.CurrentRenderMode = RenderMode.CellTypeBasic Or
                GlobalVariables.CurrentRenderMode = RenderMode.CellTypeAll)
    End Function

    ''' <summary>
    ''' Resets tools when switching to a restricted render mode
    ''' </summary>
    Private Sub ResetToolsForRestrictedMode()
        ' Exit copy/paste modes if active
        If GlobalVariables.IsCopyMode Then
            ExitCopyMode()
        End If

        If isPasteMode Then
            ExitPasteMode()
        End If

        ' Reset tile placement tool
        GlobalVariables.ActiveTileMappingIndex = -1

        ' Clear selected tile image
        If GlobalVariables.SelectedTileImage IsNot Nothing Then
            GlobalVariables.SelectedTileImage.Dispose()
            GlobalVariables.SelectedTileImage = Nothing
        End If

        ' Reset other tile-related variables
        GlobalVariables.SelectedTilesetName = ""
        GlobalVariables.SelectedTileIndex = -1
        GlobalVariables.SelectedTilesetIndex = -1

        ' Update UI
        UpdateToolAndPlacementLabels()
    End Sub

    ''' <summary>
    ''' Clears the current tool selection and updates all related UI elements
    ''' </summary>
    Private Sub ClearCurrentTool()
        ' Only proceed if edit mode is enabled
        If Not GlobalVariables.EditModeEnabled Then
            Return
        End If

        statusStrip.Items(0).Text = currentMapFile ' What should default text be? Need to decide

        ' Reset tool selection to default state
        GlobalVariables.ActiveCellType = CellType.FastPassible1
        GlobalVariables.CellTypeToolActive = False
        GlobalVariables.ActiveTileMappingIndex = -1

        ' Clear selected tile image
        If GlobalVariables.SelectedTileImage IsNot Nothing Then
            GlobalVariables.SelectedTileImage.Dispose()
            GlobalVariables.SelectedTileImage = Nothing
        End If

        ' Reset other placement variables
        GlobalVariables.SelectedTilesetName = ""
        GlobalVariables.SelectedTileIndex = -1
        GlobalVariables.SelectedTilesetIndex = -1

        ' Update UI to reflect cleared tool state
        UpdateToolAndPlacementLabels()

        ' Clear the tool preview image
        If picSelectedTool.Image IsNot Nothing Then
            picSelectedTool.Image.Dispose()
            picSelectedTool.Image = Nothing
        End If
        picSelectedTool.BackColor = System.Drawing.Color.Black

        isPasteMode = False

        ' Clear the selection in the TileSet form
        ClearTileSetFormSelection()

        Debug.WriteLine("Tool selection cleared")

        ' Refresh the map display
        pnlMap.Invalidate()
    End Sub

    ''' <summary>
    ''' Clears the currently selected tool in response to user clicking the Clear Tool button
    ''' </summary>
    Private Sub ClearTool_Click(sender As Object, e As EventArgs)
        ClearCurrentTool()
        Debug.WriteLine("Tool selection cleared by user")

        If GlobalVariables.IsCopyMode AndAlso   ' Also cancel copy
           GlobalVariables.CopyStage < 2 Then
            ExitCopyMode()
        End If
    End Sub

    ''' <summary>
    ''' Menu handler for JSON export. Currently disabled - the implementation is
    ''' commented out pending a decision on the export format.
    ''' </summary>
    Private Sub ExportToJson_Click(sender As Object, e As EventArgs)
        'If currentMap Is Nothing Then
        '    MessageBox.Show("Please load a map first.", "No Map Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information)
        '    Return
        'End If

        'Dim format As JsonExportFormat
        '' JsonExportFormat.Original     - Flat arrays like your C++ implementation
        '' JsonExportFormat.PerRow       - Each row as a separate array on a single line
        '' JsonExportFormat.PerRowPadded - Same as PerRow but with padded numbers for alignment
        'format = JsonExportFormat.PerRowPadded

        'Using saveDialog As New SaveFileDialog()
        '    saveDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        '    saveDialog.Title = "Export Map to JSON"
        '    saveDialog.FileName = Path.GetFileNameWithoutExtension(currentMapFile) & ".json"

        '    If saveDialog.ShowDialog() = DialogResult.OK Then
        '        Try
        '            Cursor = Cursors.WaitCursor

        '            ' Export the map to JSON with the hardcoded format
        '            If Not String.IsNullOrEmpty(currentMapPath) Then
        '                ExportMapToJsonFile(Path.Combine(currentMapPath, currentMapFile), saveDialog.FileName, format)
        '            Else
        '                ExportMapToJsonFile(currentMapFile, saveDialog.FileName, format)
        '            End If

        '            MessageBox.Show("Map exported to JSON successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)

        '            ' Ask if the user wants to open the file
        '            If MessageBox.Show("Do you want to open the exported JSON file?", "Open File", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
        '                Process.Start(saveDialog.FileName)
        '            End If
        '        Catch ex As Exception
        '            MessageBox.Show("Error exporting map to JSON: " & ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        '        Finally
        '            Cursor = Cursors.Default
        '        End Try
        '    End If
        'End Using
    End Sub

    ''' <summary>
    ''' Clears the copied tile data, refreshes the tile selection form preview and
    ''' disables the Paste menu item.
    ''' </summary>
    Private Sub ClearClipboardData()
        ' Clear the copied tile mappings
        GlobalVariables.CopiedTileMappings = Nothing
        GlobalVariables.CopiedRegionWidth = 0
        GlobalVariables.CopiedRegionHeight = 0

        ' Update the tile selection form via the cached reference instead of
        ' walking Application.OpenForms (this function is called from several
        ' code paths and the loop was O(n) on each call).
        If tileSelectForm IsNot Nothing AndAlso Not tileSelectForm.IsDisposed Then
            tileSelectForm.UpdateCopiedTilesPreview()
        End If

        ' Disable paste menu item
        Try
            Dim mnuEdit As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuEdit", False)(0), ToolStripMenuItem)
            Dim mnuPaste As ToolStripMenuItem = DirectCast(mnuEdit.DropDownItems.Find("mnuPaste", False)(0), ToolStripMenuItem)
            If mnuPaste IsNot Nothing Then
                mnuPaste.Enabled = False
            End If
        Catch ex As Exception
            Debug.WriteLine("Error updating paste menu item: " & ex.Message)
        End Try

        Debug.WriteLine("Clipboard data cleared")
    End Sub


#End Region

End Class
