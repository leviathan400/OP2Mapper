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

' fMain.Menus.vb
' Partial class of fMain (split out of fMain.vb).
' File / Edit menu command handlers and the View menu display toggles (grids, coordinates, zoom, single-tile mode).
Partial Public Class fMain

#Region "Menu Command Handlers"

    ''' <summary>
    ''' Handles File > Settings - shows the settings dialog.
    ''' </summary>
    Private Sub Settings_Click(sender As Object, e As EventArgs)
        ShowSettingsDialog()
    End Sub

    ''' <summary>
    ''' Handles File > Launch Outpost 2 - starts the game executable.
    ''' </summary>
    Private Sub LaunchOutpost2_Click(sender As Object, e As EventArgs)
        OP2Launcher.LaunchOutpost2()
    End Sub

    ''' <summary>
    ''' Handles File > Launch OP2 into DLL Picker - boots the game into the
    ''' in-game "Launch a DLL" menu via op2launcher.exe --picker.
    ''' </summary>
    Private Sub LaunchDllPicker_Click(sender As Object, e As EventArgs)
        OP2Launcher.LaunchDllPicker()
    End Sub

    ''' <summary>
    ''' Handles File > Open Outpost 2 Folder - opens the game folder in Explorer.
    ''' </summary>
    Private Sub OpenOutpost2Folder_Click(sender As Object, e As EventArgs)
        OP2Launcher.OpenOutpost2Folder()
    End Sub

    ''' <summary>
    ''' Handles File > Exit - closes the application (unsaved-changes prompt runs in fMain_Closing).
    ''' </summary>
    Private Sub Exit_Click(sender As Object, e As EventArgs)
        Close()
    End Sub

    ''' <summary>
    ''' Handles Render > GameMap - switches to the normal in-game tile rendering.
    ''' </summary>
    Private Sub RenderModeGameMap_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return
        CurrentRenderMode = RenderMode.GameMap
        pnlMap.Invalidate()
    End Sub

    ''' <summary>
    ''' Handles Render > CellTypeOverlay - tiles with a cell type colour overlay.
    ''' </summary>
    Private Sub RenderModeCellTypeOverlay_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return
        CurrentRenderMode = RenderMode.CellTypeOverlay
        pnlMap.Invalidate()
    End Sub

    ''' <summary>
    ''' Handles Render > CellTypeOverlayAll - tiles with a colour overlay for every cell type.
    ''' </summary>
    Private Sub RenderModeCellTypeOverlayAll_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return
        CurrentRenderMode = RenderMode.CellTypeOverlayAll
        pnlMap.Invalidate()
    End Sub

    '=================================================================
    ' SPECIAL CASE RENDER MODES
    ' CellTypeBasic and CellTypeAll are restricted render modes
    ' that only allow cell type editing, not tile placement or copy/paste
    '=================================================================

    ''' <summary>
    ''' Handles Render > CellTypeBasic - restricted cell-type-only view; resets any
    ''' tile placement tools since only cell type editing is allowed in this mode.
    ''' </summary>
    Private Sub RenderModeCellTypeBasic_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return

        ' Check if switching to restricted render mode and reset tools if needed
        If GlobalVariables.CurrentRenderMode <> RenderMode.CellTypeBasic Then
            ResetToolsForRestrictedMode()
        End If

        GlobalVariables.CurrentRenderMode = RenderMode.CellTypeBasic

        If mapRenderer IsNot Nothing Then
            mapRenderer.RenderMode = CType(GlobalVariables.CurrentRenderMode, MapRenderer.MapRenderMode)
        End If

        pnlMap.Invalidate()
    End Sub

    ''' <summary>
    ''' Handles Render > CellTypeAll - restricted cell-type-only view (all types); resets
    ''' any tile placement tools since only cell type editing is allowed in this mode.
    ''' </summary>
    Private Sub RenderModeCellTypeAll_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return

        ' Check if switching to restricted render mode and reset tools if needed
        If GlobalVariables.CurrentRenderMode <> RenderMode.CellTypeAll Then
            ResetToolsForRestrictedMode()
        End If

        GlobalVariables.CurrentRenderMode = RenderMode.CellTypeAll

        If mapRenderer IsNot Nothing Then
            mapRenderer.RenderMode = CType(GlobalVariables.CurrentRenderMode, MapRenderer.MapRenderMode)
        End If

        pnlMap.Invalidate()
    End Sub

    ''' <summary>
    ''' Handles Image > Screenshot (F12) - saves a JPG of the current map panel view.
    ''' </summary>
    Private Sub Screenshot_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return
        CaptureMapScreenshot()
    End Sub

    ''' <summary>
    ''' Saves a JPG of whatever the user is currently looking at in pnlMap
    ''' (current zoom, pan position, grid/coord overlays, selection rect —
    ''' everything the panel is painting). Goes to My.Settings.WorkingPath
    ''' with a timestamped filename so multiple shots don't overwrite.
    ''' Triggered by the Image → Screenshot menu item and the F12 hotkey.
    ''' </summary>
    Private Sub CaptureMapScreenshot()
        If currentMap Is Nothing OrElse mapRenderer Is Nothing Then Return
        If pnlMap.ClientSize.Width <= 0 OrElse pnlMap.ClientSize.Height <= 0 Then Return

        ' Pick destination folder.
        Dim folder As String = My.Settings.WorkingPath
        If String.IsNullOrEmpty(folder) OrElse Not Directory.Exists(folder) Then
            folder = Application.StartupPath
        End If

        ' Build filename: <mapname>_screenshot_<timestamp>.jpg
        Dim baseName As String =
            If(String.IsNullOrEmpty(currentMapFile),
               "screenshot",
               Path.GetFileNameWithoutExtension(currentMapFile))
        Dim timestamp As String = DateTime.Now.ToString("yyyyMMdd_HHmmss")
        Dim filePath As String = Path.Combine(folder, $"{baseName}_screenshot_{timestamp}.jpg")

        Try
            Using bmp As New Bitmap(pnlMap.ClientSize.Width, pnlMap.ClientSize.Height)
                ' DrawToBitmap triggers the control's Paint with the bitmap's
                ' Graphics — captures tiles + overlays + selection exactly as
                ' shown on screen.
                pnlMap.DrawToBitmap(bmp, New Rectangle(0, 0, bmp.Width, bmp.Height))

                Dim jpegEncoder As ImageCodecInfo = GetEncoder(ImageFormat.Jpeg)
                Using encoderParameters As New EncoderParameters(1)
                    encoderParameters.Param(0) = New EncoderParameter(Encoder.Quality, 90L)
                    bmp.Save(filePath, jpegEncoder, encoderParameters)
                End Using
            End Using

            Debug.WriteLine($"Screenshot saved: {filePath}")
            MessageBox.Show($"Screenshot saved to:" & vbCrLf & filePath,
                            "Screenshot",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            Debug.WriteLine($"Screenshot error: {ex}")
            MessageBox.Show($"Error saving screenshot: {ex.Message}",
                            "Screenshot Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Handles Image > Export Image > Full Size - exports the map image at 100% scale.
    ''' </summary>
    Private Sub ExportImageFullSize_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return
        ExportImageCustomSize(1.0)
    End Sub

    ''' <summary>
    ''' Handles Image > Export Image > 1/8th Size - exports the map image at 12.5% scale.
    ''' </summary>
    Private Sub ExportImageEighthSize_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return
        ExportImageCustomSize(0.125, "small")
    End Sub

    ''' <summary>
    ''' Handles Image > Export Image > 1/16th Size - exports the map image at 6.25% scale.
    ''' </summary>
    Private Sub ExportImageSixteenthSize_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return
        ExportImageCustomSize(0.0625, "mini")
    End Sub

    ''' <summary>
    ''' Handles Image > Export Image > Full Size (Showing Grid) - full-scale export with the small grid drawn.
    ''' </summary>
    Private Sub ExportImageFullSizeWithGrid_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return
        ExportImageCustomSize(1.0, "grid", gridMode:=ExportGridMode.SmallGrid)
    End Sub

    ''' <summary>
    ''' Handles Image > Export Image > Full Size (Showing Large Grid) - full-scale export with the large grid drawn.
    ''' </summary>
    Private Sub ExportImageFullSizeWithLargeGrid_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then Return
        ExportImageCustomSize(1.0, "largegrid", gridMode:=ExportGridMode.LargeGrid)
    End Sub

    ''' <summary>
    ''' Handles Image > Export Overview - exports the small overview image.
    ''' </summary>
    Private Sub ExportImageMenu_Click(sender As Object, e As EventArgs)
        ExportOverviewImage()
    End Sub

    ''' <summary>
    ''' Handles Edit > Undo (Ctrl+Z) - reverts the most recent edit command.
    ''' </summary>
    Private Sub Undo_Click(sender As Object, e As EventArgs)
        Undo()
    End Sub

    ''' <summary>
    ''' Handles Edit > Redo (Ctrl+Y) - re-applies the most recently undone command.
    ''' </summary>
    Private Sub Redo_Click(sender As Object, e As EventArgs)
        Redo()
    End Sub

#End Region

#Region "View Menu and Display Toggles"



    ''' <summary>
    ''' Handles View > Tool Windows Always On Top - persists the setting and applies it
    ''' to any tool palettes that are already open.
    ''' </summary>
    Private Sub ToolsTopMost_CheckedChanged(sender As Object, e As EventArgs)
        Dim item As ToolStripMenuItem = TryCast(sender, ToolStripMenuItem)
        If item Is Nothing Then Return

        ToolPaletteHelper.SetToolWindowsAlwaysOnTop(item.Checked)

        ' Apply to any tool palettes that are currently open so the change
        ' takes effect immediately instead of next-edit-mode-toggle.
        ToolPaletteHelper.ApplyTopMostToAllToolForms(item.Checked)
    End Sub

    ' Event handlers (keeping these for the menu click events)

    ''' <summary>
    ''' Handles View > Show Grid (Ctrl+G) - syncs the menu check state and toggles the grid.
    ''' </summary>
    Private Sub ToggleGrid_Click(sender As Object, e As EventArgs)
        ' Update menu item checked state
        Dim menuItem As ToolStripMenuItem = DirectCast(sender, ToolStripMenuItem)
        menuItem.Checked = Not GlobalVariables.ShowGrid

        ' Call the actual toggle function
        ToggleGrid()
    End Sub

    ''' <summary>
    ''' Handles View > Show Large Grid (Ctrl+L) - syncs the menu check state and toggles the large grid.
    ''' </summary>
    Private Sub ToggleLargeGrid_Click(sender As Object, e As EventArgs)
        ' Update menu item checked state
        Dim menuItem As ToolStripMenuItem = DirectCast(sender, ToolStripMenuItem)
        menuItem.Checked = Not GlobalVariables.ShowLargeGrid

        ' Call the actual toggle function
        ToggleLargeGrid()
    End Sub

    ''' <summary>
    ''' Handles View > Show Coordinates (Ctrl+D) - syncs the menu check state and toggles coordinates.
    ''' </summary>
    Private Sub ToggleCoordinates_Click(sender As Object, e As EventArgs)
        ' Update menu item checked state
        Dim menuItem As ToolStripMenuItem = DirectCast(sender, ToolStripMenuItem)
        menuItem.Checked = Not GlobalVariables.ShowCoordinates

        ' Call the actual toggle function
        ToggleCoordinates()
    End Sub

    ' Toggles

    ''' <summary>
    ''' Flips the grid display setting, updates the renderer and refreshes the menus.
    ''' </summary>
    Private Sub ToggleGrid()
        ' Toggle grid display
        GlobalVariables.ShowGrid = Not GlobalVariables.ShowGrid

        ' Update renderer
        If mapRenderer IsNot Nothing Then
            mapRenderer.ShowGrid = GlobalVariables.ShowGrid
            pnlMap.Invalidate()
        End If

        ' Update menu items to match current state
        UpdateViewMenuItems()
    End Sub

    ''' <summary>
    ''' Flips the large grid display setting, updates the renderer and refreshes the menus.
    ''' </summary>
    Private Sub ToggleLargeGrid()
        ' Toggle large grid display
        GlobalVariables.ShowLargeGrid = Not GlobalVariables.ShowLargeGrid

        ' Update renderer
        If mapRenderer IsNot Nothing Then
            mapRenderer.ShowLargeGrid = GlobalVariables.ShowLargeGrid
            pnlMap.Invalidate()
        End If

        ' Update menu items to match current state
        UpdateViewMenuItems()
    End Sub

    ''' <summary>
    ''' Flips the coordinates display setting, updates the renderer and refreshes the menus.
    ''' </summary>
    Private Sub ToggleCoordinates()
        ' Toggle coordinates display
        GlobalVariables.ShowCoordinates = Not GlobalVariables.ShowCoordinates

        ' Update renderer
        If mapRenderer IsNot Nothing Then
            mapRenderer.ShowCoordinates = GlobalVariables.ShowCoordinates
            pnlMap.Invalidate()
        End If

        ' Update menu items to match current state
        UpdateViewMenuItems()
    End Sub

    ''' <summary>
    ''' Resets all view and tool state back to defaults - grid/coordinate toggles off,
    ''' tool selection cleared, clipboard emptied, previews disposed. Called on map load.
    ''' </summary>
    Private Sub ResetViewSettings()
        ' Reset the global view settings
        GlobalVariables.ShowGrid = False
        GlobalVariables.ShowLargeGrid = False
        GlobalVariables.ShowCoordinates = False

        ' Reset tool selection state
        GlobalVariables.ActiveCellType = CellType.FastPassible1
        GlobalVariables.CellTypeToolActive = False
        GlobalVariables.ActiveTileMappingIndex = -1

        ' Clear the selected tile image
        If GlobalVariables.SelectedTileImage IsNot Nothing Then
            GlobalVariables.SelectedTileImage.Dispose()
            GlobalVariables.SelectedTileImage = Nothing
        End If

        ' Reset tile selection information
        GlobalVariables.SelectedTilesetName = ""
        GlobalVariables.SelectedTileIndex = -1
        GlobalVariables.SelectedTilesetIndex = -1

        ' Clear clipboard data
        ClearClipboardData()

        ' Clear the tool preview image
        If picSelectedTool IsNot Nothing Then
            If picSelectedTool.Image IsNot Nothing Then
                picSelectedTool.Image.Dispose()
                picSelectedTool.Image = Nothing
            End If
            picSelectedTool.BackColor = System.Drawing.Color.Black
        End If

        ' Clear the tile preview picture boxes
        If picTilePreview IsNot Nothing Then
            If picTilePreview.Image IsNot Nothing Then
                picTilePreview.Image.Dispose()
                picTilePreview.Image = Nothing
            End If
        End If

        If picTilePreviewBig IsNot Nothing Then
            If picTilePreviewBig.Image IsNot Nothing Then
                picTilePreviewBig.Image.Dispose()
                picTilePreviewBig.Image = Nothing
            End If
        End If

        ' Clear the selected tool image
        If picSelectedTool IsNot Nothing Then
            If picSelectedTool.Image IsNot Nothing Then
                picSelectedTool.Image.Dispose()
                picSelectedTool.Image = Nothing
            End If
        End If


        ' Reset tile information labels to N/A state
        lblTileset.Text = "Tileset: N/A"
        lblTilesetImage.Text = "Image Index: N/A"
        lblMappingIndex.Text = "Mapping Index: N/A"
        lblCoordinates.Text = "Coordinates: N/A"
        lblCellType.Text = "Cell Type: N/A"

        ' Reset current tile selection
        currentTileX = -1
        currentTileY = -1

        ' Update the tool and placement labels
        UpdateToolAndPlacementLabels()

        ' Apply settings to the renderer
        If mapRenderer IsNot Nothing Then
            mapRenderer.ShowGrid = GlobalVariables.ShowGrid
            mapRenderer.ShowLargeGrid = GlobalVariables.ShowLargeGrid
            mapRenderer.ShowCoordinates = GlobalVariables.ShowCoordinates
        End If

        ' Update the menu items to match the new settings
        Try
            UpdateViewMenuItems()
        Catch ex As Exception
            ' Log error but continue - don't let menu updating failure prevent map loading
            Debug.WriteLine("Error updating view menu items: " & ex.Message)
        End Try

        ' Force a redraw of the map panel
        pnlMap.Invalidate()
    End Sub





    ''' <summary>
    ''' Syncs the checked state of the View menu and map context menu items with the
    ''' current global settings. Safe to call before the menus exist.
    ''' </summary>
    Private Sub UpdateViewMenuItems()
        Try
            ' Make sure the MainMenuStrip exists and is initialized
            If MainMenuStrip Is Nothing OrElse MainMenuStrip.Items.Count = 0 Then
                Debug.WriteLine("MainMenuStrip not initialized - skipping menu update")
                Return
            End If

            ' Find view menu with error checking
            Dim viewMenuItems = MainMenuStrip.Items.Find("mnuView", False)
            If viewMenuItems.Length = 0 Then
                Debug.WriteLine("View menu not found - skipping menu update")
                Return
            End If

            Dim viewMenu As ToolStripMenuItem = DirectCast(viewMenuItems(0), ToolStripMenuItem)

            ' Update grid menu item
            Dim gridItems = viewMenu.DropDownItems.Find("mnuGrid", False)
            If gridItems.Length > 0 Then
                DirectCast(gridItems(0), ToolStripMenuItem).Checked = GlobalVariables.ShowGrid
            End If

            ' Update large grid menu item
            Dim largeGridItems = viewMenu.DropDownItems.Find("mnuLargeGrid", False)
            If largeGridItems.Length > 0 Then
                DirectCast(largeGridItems(0), ToolStripMenuItem).Checked = GlobalVariables.ShowLargeGrid
            End If

            ' Update coordinates menu item
            Dim coordItems = viewMenu.DropDownItems.Find("mnuShowCoords", False)
            If coordItems.Length > 0 Then
                DirectCast(coordItems(0), ToolStripMenuItem).Checked = GlobalVariables.ShowCoordinates
            End If

            ' Update zoom menu item
            Dim zoomItems = viewMenu.DropDownItems.Find("mnuEnableZoom", False)
            If zoomItems.Length > 0 Then
                DirectCast(zoomItems(0), ToolStripMenuItem).Checked = GlobalVariables.ZoomEnabled
            End If

            ' Update context menu items if they exist
            If mapContextMenu IsNot Nothing Then
                Dim contextGridItems = mapContextMenu.Items.Find("mnuContextGrid", False)
                If contextGridItems.Length > 0 Then
                    DirectCast(contextGridItems(0), ToolStripMenuItem).Checked = GlobalVariables.ShowGrid
                End If

                Dim contextLargeGridItems = mapContextMenu.Items.Find("mnuContextLargeGrid", False)
                If contextLargeGridItems.Length > 0 Then
                    DirectCast(contextLargeGridItems(0), ToolStripMenuItem).Checked = GlobalVariables.ShowLargeGrid
                End If

                Dim contextCoordItems = mapContextMenu.Items.Find("mnuContextCoords", False)
                If contextCoordItems.Length > 0 Then
                    DirectCast(contextCoordItems(0), ToolStripMenuItem).Checked = GlobalVariables.ShowCoordinates
                End If

                Dim contextZoomItems = mapContextMenu.Items.Find("mnuContextEnableZoom", False)
                If contextZoomItems.Length > 0 Then
                    DirectCast(contextZoomItems(0), ToolStripMenuItem).Checked = GlobalVariables.ZoomEnabled
                End If
            End If

        Catch ex As Exception
            ' Catch any unexpected errors to prevent loading failure
            Debug.WriteLine("Error in UpdateViewMenuItems: " & ex.Message & Environment.NewLine & ex.StackTrace)
        End Try
    End Sub



    ''' <summary>
    ''' Handles View > Configure Grid Size - shows a small dialog for setting the large
    ''' grid size and coordinate spacing, then applies the values to the renderer.
    ''' </summary>
    Private Sub ConfigureGridSize_Click(sender As Object, e As EventArgs)
        ' Show a dialog to configure grid and coordinate spacing
        Using dialog As New Form()
            dialog.Text = "Configure Grid Settings"
            dialog.Size = New Size(300, 200)
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog
            dialog.StartPosition = FormStartPosition.CenterParent
            dialog.MaximizeBox = False
            dialog.MinimizeBox = False

            ' Large grid size control
            Dim lblLargeGrid As New Label() With {
                .Text = "Large Grid Size (in tiles):",
                .Location = New Point(20, 20),
                .Size = New Size(150, 20)
            }

            Dim numLargeGrid As New NumericUpDown() With {
                .Location = New Point(180, 20),
                .Size = New Size(80, 20),
                .Minimum = 8,
                .Maximum = 64,
                .Value = If(mapRenderer IsNot Nothing, mapRenderer.LargeGridSize, 32),
                .Increment = 8
            }

            ' Coordinate spacing control
            Dim lblCoordSpacing As New Label() With {
                .Text = "Coordinate Spacing (in tiles):",
                .Location = New Point(20, 50),
                .Size = New Size(150, 20)
            }

            Dim numCoordSpacing As New NumericUpDown() With {
                .Location = New Point(180, 50),
                .Size = New Size(80, 20),
                .Minimum = 8,
                .Maximum = 64,
                .Value = If(mapRenderer IsNot Nothing, mapRenderer.CoordinateSpacing, 32),
                .Increment = 8
            }

            ' Buttons
            Dim btnOK As New Button() With {
                .Text = "OK",
                .DialogResult = DialogResult.OK,
                .Location = New Point(100, 120),
                .Size = New Size(80, 30)
            }

            Dim btnCancel As New Button() With {
                .Text = "Cancel",
                .DialogResult = DialogResult.Cancel,
                .Location = New Point(190, 120),
                .Size = New Size(80, 30)
            }

            ' Add controls to form
            dialog.Controls.Add(lblLargeGrid)
            dialog.Controls.Add(numLargeGrid)
            dialog.Controls.Add(lblCoordSpacing)
            dialog.Controls.Add(numCoordSpacing)
            dialog.Controls.Add(btnOK)
            dialog.Controls.Add(btnCancel)

            dialog.AcceptButton = btnOK
            dialog.CancelButton = btnCancel

            ' Show dialog and process result
            If dialog.ShowDialog() = DialogResult.OK Then
                ' Update global variables
                GlobalVariables.LargeGridSize = CInt(numLargeGrid.Value)
                GlobalVariables.CoordinateSpacing = CInt(numCoordSpacing.Value)

                ' Update renderer
                If mapRenderer IsNot Nothing Then
                    mapRenderer.LargeGridSize = GlobalVariables.LargeGridSize
                    mapRenderer.CoordinateSpacing = GlobalVariables.CoordinateSpacing
                    pnlMap.Invalidate()
                End If
            End If
        End Using
    End Sub

    ''' <summary>
    ''' Handles Help > About - shows the about dialog with version/build details.
    ''' </summary>
    Private Sub About_Click(sender As Object, e As EventArgs)
        Using AboutForm As New fAbout()
            AboutForm.ApplicationTitle = GlobalVariables.ApplicationName
            AboutForm.ApplicationVersion = GlobalVariables.ApplicationVersion
            AboutForm.ApplicationBuild = GlobalVariables.ApplicationBuild
            AboutForm.ApplicationDescription = GlobalVariables.ApplicationDescription
            AboutForm.ApplicationCopyright = "Outpost Universe © 2025-2026"
            AboutForm.ApplicationWebsite = GlobalVariables.ApplicationWebsite
            AboutForm.ShowDialog(Me)
        End Using
    End Sub

    ''' <summary>
    ''' Handles pnlMap.MouseClick - intentionally empty; the context menu is shown from
    ''' MouseUp instead, so handling it here too would double-trigger it.
    ''' </summary>
    Private Sub pnlMap_MouseClick(sender As Object, e As MouseEventArgs) Handles pnlMap.MouseClick
        ' We'll do nothing here since we're handling context menu in MouseUp
        ' This prevents double triggering of the context menu

    End Sub

    ''' <summary>
    ''' Handles View > Enable Zoom (Ctrl+E) - syncs the menu check state and toggles zoom.
    ''' </summary>
    Private Sub ToggleZoomEnabled_Click(sender As Object, e As EventArgs)
        ' Update menu item checked state
        Dim menuItem As ToolStripMenuItem = DirectCast(sender, ToolStripMenuItem)
        menuItem.Checked = Not GlobalVariables.ZoomEnabled

        ' Call the actual toggle function
        ToggleZoomEnabled()
    End Sub

    ''' <summary>
    ''' Flips the zoom-enabled state. When disabling, zoom is forced back to 100% and
    ''' the zoom label shows a locked indicator. Callable from menu or context menu.
    ''' </summary>
    Private Sub ToggleZoomEnabled()
        ' Toggle zoom enabled state
        GlobalVariables.ZoomEnabled = Not GlobalVariables.ZoomEnabled

        ' If zoom is disabled and current zoom is not 100%, reset to 100%
        If Not GlobalVariables.ZoomEnabled AndAlso Math.Abs(zoomLevel - 1.0F) > 0.01F Then
            ' Reset zoom to 100%
            zoomLevel = 1.0F

            ' Clamp the new offset so the map can't be panned/zoomed completely off-screen.
            ClampOffsets()

            ' Refresh display
            pnlMap.Invalidate()
        End If

        ' Update zoom level label to show locked state if disabled
        If Not GlobalVariables.ZoomEnabled Then
            lblZoomLevel.Text = "Zoom Level: 100% (Locked)"
            lblZoomLevel.ForeColor = System.Drawing.Color.Gray
        Else
            lblZoomLevel.Text = $"Zoom Level: {Math.Round(zoomLevel * 100)}%"
            lblZoomLevel.ForeColor = System.Drawing.SystemColors.ControlText
        End If

        ' Find and update any menu items that control zoom
        Dim viewMenu As ToolStripMenuItem = DirectCast(MainMenuStrip.Items.Find("mnuView", True).FirstOrDefault(), ToolStripMenuItem)
        If viewMenu IsNot Nothing Then
            Dim zoomMenuItem As ToolStripMenuItem = DirectCast(viewMenu.DropDownItems.Find("mnuEnableZoom", False).FirstOrDefault(), ToolStripMenuItem)
            If zoomMenuItem IsNot Nothing Then
                zoomMenuItem.Checked = GlobalVariables.ZoomEnabled
            End If
        End If
    End Sub

    ''' <summary>
    ''' Handles Edit > Single Tile Edit Mode - switches between single-click edits and
    ''' drag-to-paint editing, and updates the status strip indicator.
    ''' </summary>
    Private Sub ToggleSingleTileMode_Click(sender As Object, e As EventArgs)
        ' Toggle single tile edit mode
        GlobalVariables.SingleTileEditMode = DirectCast(sender, ToolStripMenuItem).Checked

        ' Output debug info about mode
        Debug.WriteLine("Single Tile Edit Mode: " & GlobalVariables.SingleTileEditMode)

        ' Update status strip to show the current edit mode
        UpdateStatusForEditMode()
    End Sub

    ''' <summary>
    ''' Updates the status strip map name to show the current edit mode
    ''' ([Single Tile] / [Drag Edit]) or just the map name when not editing.
    ''' </summary>
    Private Sub UpdateStatusForEditMode()
        ' Add a note about edit mode to the status strip
        If GlobalVariables.EditModeEnabled Then
            If GlobalVariables.SingleTileEditMode Then
                lblToolStripMapName.Text = currentMapFile & " [Single Tile]"
            Else
                lblToolStripMapName.Text = currentMapFile & " [Drag Edit]"
            End If
        Else
            ' Just show the map name when not in edit mode
            lblToolStripMapName.Text = currentMapFile
        End If
    End Sub


#End Region

End Class
