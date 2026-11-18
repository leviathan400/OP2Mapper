Imports System.IO
Imports System.Diagnostics
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Text
Imports OP2UtilityDotNet
Imports OP2UtilityDotNet.OP2Map
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports OP2MapJsonTools

' fTileSelect.vb - Tool 3: "Tile Selection" palette window.
' Shows a preview of the currently copied tile region and lets the user
' export it as a reusable tile group (BMP + JSON). Its title-bar menu adds a
' Large Window mode and preview zoom levels (also Ctrl + mouse wheel).
Public Class fTileSelect
    Implements IToolPaletteMenuExtras

#Region "Window Size and Zoom"

    ''' <summary>Outer window size in normal mode (the long-standing size).</summary>
    Private Shared ReadOnly NormalWindowSize As New Size(498, 484)
    ''' <summary>
    ''' Outer window size in Large Window mode: roughly 30x26 tiles at 100%.
    ''' Clamped to the screen's working area when applied.
    ''' </summary>
    Private Shared ReadOnly LargeWindowSize As New Size(1000, 880)

    ''' <summary>Zoom levels offered in the menu and stepped through by Ctrl + wheel.</summary>
    Private Shared ReadOnly ZoomLevels As Single() = {1.0F, 0.75F, 0.5F, 0.25F}

    ''' <summary>Whether the window is in Large Window mode.</summary>
    Private largeWindow As Boolean = False
    ''' <summary>Preview zoom (1 = 32 px tiles). Ignored while zoomToFit is on.</summary>
    Private previewZoom As Single = 1.0F
    ''' <summary>When True, tiles are sized so the whole copy fits the window.</summary>
    Private zoomToFit As Boolean = False
    ''' <summary>
    ''' The copy (CopiedTileMappings array) the zoom was last auto-picked for.
    ''' Every copy creates a new array, so a different reference means a new
    ''' copy has arrived. See PickZoomForNewCopy.
    ''' </summary>
    Private autoZoomedCopy As Integer(,) = Nothing
    ''' <summary>
    ''' When True, the copied cell types are drawn as a coloured overlay on the
    ''' preview (the same colours as the map's CellTypeOverlay view).
    ''' </summary>
    Private showCellTypes As Boolean = False

    ' INI keys for the preferences above (Mapper3.ini, [Window] section).
    Private Const SettingLarge As String = "TileSelectLarge"
    Private Const SettingZoom As String = "TileSelectZoom"
    Private Const SettingShowCellTypes As String = "TileSelectShowCellTypes"

    ''' <summary>Opacity of the cell-type overlay; matches the map's CellTypeOverlay view.</summary>
    Private Const CellTypeOverlayAlpha As Integer = 115

    ' Title-bar menu items, by index (see IToolPaletteMenuExtras).
    Private Const MenuLargeWindow As Integer = 0
    ' Index 1 is a separator.
    Private Const MenuZoomFirst As Integer = 2   ' 2..5 = ZoomLevels(0..3)
    Private Const MenuZoomFit As Integer = 6
    ' Index 7 is a separator.
    Private Const MenuShowCellTypes As Integer = 8

    ''' <summary>Text of the Show Cell Types item (title-bar and right-click menus).</summary>
    Private Const ShowCellTypesText As String = "Show Cell Types"

    ''' <summary>Right-click menu's Show Cell Types item, kept to refresh its check mark.</summary>
    Private showCellTypesMenuItem As ToolStripMenuItem

    ''' <summary>Title-bar menu item texts; see the Menu* constants.</summary>
    Public Function GetExtraMenuItems() As IList(Of String) Implements IToolPaletteMenuExtras.GetExtraMenuItems
        Return {"Large Window", "-",
                "Zoom 100%", "Zoom 75%", "Zoom 50%", "Zoom 25%",
                "Zoom to Fit Window", "-",
                ShowCellTypesText}
    End Function

    ''' <summary>Check marks: the size mode, the active zoom choice, and Show Cell Types.</summary>
    Public Function IsExtraMenuItemChecked(index As Integer) As Boolean Implements IToolPaletteMenuExtras.IsExtraMenuItemChecked
        If index = MenuLargeWindow Then Return largeWindow
        If index = MenuZoomFit Then Return zoomToFit
        If index = MenuShowCellTypes Then Return showCellTypes
        Dim zoomIndex As Integer = index - MenuZoomFirst
        If zoomIndex >= 0 AndAlso zoomIndex < ZoomLevels.Length Then
            Return Not zoomToFit AndAlso Math.Abs(previewZoom - ZoomLevels(zoomIndex)) < 0.001F
        End If
        Return False
    End Function

    ''' <summary>Applies a title-bar menu choice.</summary>
    Public Sub OnExtraMenuItem(index As Integer) Implements IToolPaletteMenuExtras.OnExtraMenuItem
        If index = MenuLargeWindow Then
            SetLargeWindow(Not largeWindow)
        ElseIf index = MenuZoomFit Then
            SetZoom(previewZoom, fitToWindow:=True)
        ElseIf index = MenuShowCellTypes Then
            SetShowCellTypes(Not showCellTypes)
        Else
            Dim zoomIndex As Integer = index - MenuZoomFirst
            If zoomIndex >= 0 AndAlso zoomIndex < ZoomLevels.Length Then
                SetZoom(ZoomLevels(zoomIndex), fitToWindow:=False)
            End If
        End If
    End Sub

    ''' <summary>
    ''' Reads the saved size mode and zoom from the INI (called from Load).
    ''' </summary>
    Private Sub LoadViewSettings()
        largeWindow = String.Equals(ToolPaletteHelper.ReadToolSetting(SettingLarge, "False"), "True",
                                    StringComparison.OrdinalIgnoreCase)
        showCellTypes = String.Equals(ToolPaletteHelper.ReadToolSetting(SettingShowCellTypes, "False"), "True",
                                      StringComparison.OrdinalIgnoreCase)
        ' The right-click menu is built in the constructor, before this runs.
        If showCellTypesMenuItem IsNot Nothing Then showCellTypesMenuItem.Checked = showCellTypes

        Dim zoomText As String = ToolPaletteHelper.ReadToolSetting(SettingZoom, "100")
        If String.Equals(zoomText, "Fit", StringComparison.OrdinalIgnoreCase) Then
            zoomToFit = True
        Else
            Dim percent As Integer
            If Integer.TryParse(zoomText, percent) AndAlso
               ZoomLevels.Any(Function(z) Math.Abs(z * 100 - percent) < 0.5F) Then
                previewZoom = percent / 100.0F
            End If
        End If
    End Sub

    ''' <summary>
    ''' Switches between the normal and large window size, keeps the window on
    ''' its screen, and re-renders the preview (Zoom to Fit depends on size).
    ''' </summary>
    Private Sub SetLargeWindow(large As Boolean)
        largeWindow = large
        ToolPaletteHelper.WriteToolSetting(SettingLarge, large.ToString())
        ApplyWindowSize()
        ToolPaletteHelper.SavePosition(Me, "TileSelect")
        UpdateCopiedTilesPreview()
    End Sub

    ''' <summary>
    ''' Turns the cell-type overlay on the preview on or off, saves the choice,
    ''' and re-renders. Used by both the title-bar and right-click menus.
    ''' </summary>
    Private Sub SetShowCellTypes(show As Boolean)
        showCellTypes = show
        ToolPaletteHelper.WriteToolSetting(SettingShowCellTypes, show.ToString())
        If showCellTypesMenuItem IsNot Nothing Then showCellTypesMenuItem.Checked = show

        ' Re-render at the same scroll position (the size doesn't change).
        Dim scroll As New Point(-pnlTilePasteViewport.AutoScrollPosition.X, -pnlTilePasteViewport.AutoScrollPosition.Y)
        UpdateCopiedTilesPreview()
        pnlTilePasteViewport.AutoScrollPosition = scroll
    End Sub

    ''' <summary>
    ''' Sets the preview zoom (or Zoom to Fit), saves it, and re-renders while
    ''' keeping the same part of the copy centred in the view.
    ''' </summary>
    Private Sub SetZoom(zoom As Single, fitToWindow As Boolean)
        ' Remember which part of the copy is centred, as a fraction of its size.
        Dim centreX As Single = 0.5F, centreY As Single = 0.5F
        If copiedTilesPreview IsNot Nothing Then
            Dim view As Size = pnlTilePasteViewport.ClientSize
            centreX = (-pnlTilePasteViewport.AutoScrollPosition.X + view.Width / 2.0F) / copiedTilesPreview.Width
            centreY = (-pnlTilePasteViewport.AutoScrollPosition.Y + view.Height / 2.0F) / copiedTilesPreview.Height
        End If

        previewZoom = zoom
        zoomToFit = fitToWindow
        ToolPaletteHelper.WriteToolSetting(SettingZoom,
            If(fitToWindow, "Fit", CInt(zoom * 100).ToString()))
        UpdateCopiedTilesPreview()

        ' Scroll so the same point is centred again at the new size.
        If copiedTilesPreview IsNot Nothing Then
            Dim view As Size = pnlTilePasteViewport.ClientSize
            pnlTilePasteViewport.AutoScrollPosition = New Point(
                Math.Max(0, CInt(centreX * copiedTilesPreview.Width - view.Width / 2.0F)),
                Math.Max(0, CInt(centreY * copiedTilesPreview.Height - view.Height / 2.0F)))
            pnlTilePasteViewport.Invalidate()
        End If
    End Sub

    ''' <summary>
    ''' Chooses the zoom for a newly copied region: the largest zoom level
    ''' (100%, 75%, 50%, 25%) at which the whole copy fits the window, so a
    ''' small copy is shown at full size. If it doesn't fit even at 25%, the
    ''' current zoom is kept and the copy scrolls. The choice isn't saved: the
    ''' INI keeps the zoom the user last picked from the menu.
    ''' </summary>
    Private Sub PickZoomForNewCopy(widthInTiles As Integer, heightInTiles As Integer)
        ' Measure inside the 1 px border, ignoring scrollbars (as Zoom to Fit
        ' does): a copy that fits won't have any.
        Dim viewW As Integer = pnlTilePasteViewport.Width - 2
        Dim viewH As Integer = pnlTilePasteViewport.Height - 2

        For Each zoom As Single In ZoomLevels   ' largest first
            Dim tileSize As Integer = Math.Max(1, CInt(32 * zoom))
            If widthInTiles * tileSize <= viewW AndAlso heightInTiles * tileSize <= viewH Then
                previewZoom = zoom
                zoomToFit = False
                pnlTilePasteViewport.AutoScrollPosition = Point.Empty
                Return
            End If
        Next
    End Sub

    ''' <summary>
    ''' Sizes the window for the current mode, clamped to (and kept inside)
    ''' its screen's working area, and fills it with the preview panel.
    ''' </summary>
    Private Sub ApplyWindowSize()
        Dim target As Size = If(largeWindow, LargeWindowSize, NormalWindowSize)
        Dim work As Rectangle = Screen.FromControl(Me).WorkingArea
        Me.Size = New Size(Math.Min(target.Width, work.Width), Math.Min(target.Height, work.Height))

        ' Growing can push the window past the screen edge; pull it back in.
        Dim x As Integer = Math.Max(work.Left, Math.Min(Left, work.Right - Width))
        Dim y As Integer = Math.Max(work.Top, Math.Min(Top, work.Bottom - Height))
        Me.Location = New Point(x, y)

        pnlTilePasteViewport.SetBounds(1, 1, ClientSize.Width - 2, ClientSize.Height - 2)
    End Sub

    ''' <summary>
    ''' Pixels per tile for the preview: from the zoom level, or for Zoom to
    ''' Fit the largest size (up to 32) at which the whole copy fits the view.
    ''' </summary>
    Private Function GetPreviewTileSize(widthInTiles As Integer, heightInTiles As Integer) As Integer
        If zoomToFit Then
            ' Measure the panel inside its 1 px border but ignoring any
            ' scrollbars: once the copy fits, the scrollbars go away.
            Dim viewW As Integer = pnlTilePasteViewport.Width - 2
            Dim viewH As Integer = pnlTilePasteViewport.Height - 2
            Return Math.Max(1, Math.Min(32, Math.Min(viewW \ widthInTiles, viewH \ heightInTiles)))
        End If
        Return Math.Max(1, CInt(32 * previewZoom))
    End Function

    ''' <summary>
    ''' Ctrl + mouse wheel steps through the zoom levels. A plain wheel scrolls
    ''' the preview (Shift + wheel scrolls sideways). Hooked on both the form
    ''' and the panel, since either may receive wheel messages.
    ''' </summary>
    Private Sub Preview_MouseWheel(sender As Object, e As MouseEventArgs)
        Dim handledArgs As HandledMouseEventArgs = TryCast(e, HandledMouseEventArgs)

        If (Control.ModifierKeys And Keys.Control) = Keys.Control Then
            ' Find the current level (Zoom to Fit counts from 100%) and step:
            ' wheel up = zoom in (toward 100%), wheel down = zoom out.
            Dim current As Integer = If(zoomToFit, 0, Array.FindIndex(ZoomLevels, Function(z) Math.Abs(z - previewZoom) < 0.001F))
            If current < 0 Then current = 0
            Dim nextIndex As Integer = Math.Max(0, Math.Min(ZoomLevels.Length - 1, current + If(e.Delta > 0, -1, 1)))
            SetZoom(ZoomLevels(nextIndex), fitToWindow:=False)
            If handledArgs IsNot Nothing Then handledArgs.Handled = True
            Return
        End If

        ' The panel scrolls itself; only the form's own wheel events need it done here.
        If sender Is pnlTilePasteViewport Then Return
        Dim pos As New Point(-pnlTilePasteViewport.AutoScrollPosition.X, -pnlTilePasteViewport.AutoScrollPosition.Y)
        If (Control.ModifierKeys And Keys.Shift) = Keys.Shift Then
            pos.X -= e.Delta
        Else
            pos.Y -= e.Delta
        End If
        pnlTilePasteViewport.AutoScrollPosition = pos
        pnlTilePasteViewport.Invalidate()
    End Sub

#End Region

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

    ''' <summary>
    ''' Win32 RECT (screen coordinates) as passed in the WM_MOVING lParam;
    ''' read, adjusted for snapping, then written back.
    ''' </summary>
    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure RECT
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

#End Region

    ''' <summary>
    ''' Reference to main form
    ''' </summary>
    Private mainForm As fMain = Nothing

    ''' <summary>
    ''' Current bitmap of copied tiles
    ''' </summary>
    Private copiedTilesPreview As Bitmap = Nothing

    ''' <summary>
    ''' Variables for panel interaction
    ''' </summary>
    Private isDragging As Boolean = False
    Private startScrollPos As Point
    ' Where the right button went down. startScrollPos moves with every pan
    ' step, so it can't be used to tell a click from a drag.
    Private rightDownPos As Point
    ' Set on right-button-up to cancel the context menu WinForms opens next.
    Private suppressNextContextMenu As Boolean = False

    ''' <summary>
    ''' Last known copied region dimensions
    ''' </summary>
    Private lastKnownWidth As Integer = 0
    Private lastKnownHeight As Integer = 0

    ''' <summary>
    ''' Constructor
    ''' </summary>
    Public Sub New()
        InitializeComponent()

        ' Set up panel for double buffering to reduce flicker
        pnlTilePasteViewport.GetType().GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(pnlTilePasteViewport, True)

        ' Configure panel properties
        pnlTilePasteViewport.AutoScroll = True
        pnlTilePasteViewport.BorderStyle = BorderStyle.FixedSingle
        pnlTilePasteViewport.BackColor = System.Drawing.Color.DarkGray

        ' Add event handlers
        AddHandler pnlTilePasteViewport.Paint, AddressOf PnlTilePasteViewport_Paint
        AddHandler pnlTilePasteViewport.MouseDown, AddressOf PnlTilePasteViewport_MouseDown
        AddHandler pnlTilePasteViewport.MouseMove, AddressOf PnlTilePasteViewport_MouseMove
        AddHandler pnlTilePasteViewport.MouseUp, AddressOf PnlTilePasteViewport_MouseUp
        AddHandler pnlTilePasteViewport.MouseWheel, AddressOf Preview_MouseWheel
        AddHandler Me.MouseWheel, AddressOf Preview_MouseWheel

        ' Initialize context menu
        InitializeContextMenu()
    End Sub

    ''' <summary>
    ''' Form load event handler
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Event arguments</param>
    Private Sub fTileSelect_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ToolPaletteHelper.ConfigureAsToolPalette(Me)
        ToolPaletteHelper.RestorePosition(Me, "TileSelect")

        ' Get reference to main form
        mainForm = TryCast(Application.OpenForms("fMain"), fMain)

        ' Set form properties (TopMost is set by ConfigureAsToolPalette per
        ' the View → Tool Windows Always On Top setting)
        Me.FormBorderStyle = FormBorderStyle.FixedToolWindow
        Me.Text = "Tile Selection"
        Me.Icon = My.Resources.lists

        ' Size the window (normal or Large Window mode) and restore the zoom.
        LoadViewSettings()
        ApplyWindowSize()

        ' Initialize the copied tiles preview if we already have data
        UpdateCopiedTilesPreview()
    End Sub

    ''' <summary>
    ''' Form closing event handler
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Form closing event arguments</param>
    Private Sub fTileSelect_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        ToolPaletteHelper.SavePosition(Me, "TileSelect")
        ToolPaletteHelper.UnregisterToolForm(Me)

        If copiedTilesPreview IsNot Nothing Then        ' Clean up resources
            copiedTilesPreview.Dispose()
            copiedTilesPreview = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Initializes the context menu for the tile selection panel
    ''' </summary>
    Private Sub InitializeContextMenu()
        ' Create a new context menu
        Dim contextMenu As New ContextMenuStrip()

        ' Add Export Tile Group menu item
        Dim exportTileGroupMenuItem As New ToolStripMenuItem("Export Tile Group")
        AddHandler exportTileGroupMenuItem.Click, AddressOf ExportTileGroup_Click
        contextMenu.Items.Add(exportTileGroupMenuItem)

        ' Show Cell Types toggle (also on the title-bar menu)
        contextMenu.Items.Add(New ToolStripSeparator())
        showCellTypesMenuItem = New ToolStripMenuItem(ShowCellTypesText) With {.Checked = showCellTypes}
        AddHandler showCellTypesMenuItem.Click, Sub(s, args) SetShowCellTypes(Not showCellTypes)
        contextMenu.Items.Add(showCellTypesMenuItem)

        ' Assign the context menu to the panel. WinForms opens it on the
        ' WM_CONTEXTMENU after right-button-up; the Opening handler cancels it
        ' after a pan or when there's nothing to export.
        pnlTilePasteViewport.ContextMenuStrip = contextMenu
        AddHandler contextMenu.Opening, AddressOf PasteViewportContextMenu_Opening
    End Sub

    ''' <summary>
    ''' Cancels the viewport context menu right after a pan, or when no tiles are copied.
    ''' </summary>
    Private Sub PasteViewportContextMenu_Opening(sender As Object, e As System.ComponentModel.CancelEventArgs)
        If suppressNextContextMenu OrElse copiedTilesPreview Is Nothing Then
            e.Cancel = True
        End If
        suppressNextContextMenu = False
    End Sub

    ''' <summary>
    ''' Updates the display with the current copied tiles
    ''' </summary>
    Public Sub UpdateCopiedTilesPreview()
        Try
            ' Clean up previous bitmap if it exists
            If copiedTilesPreview IsNot Nothing Then
                copiedTilesPreview.Dispose()
                copiedTilesPreview = Nothing
            End If

            ' Check if we have copied tiles
            If GlobalVariables.CopiedTileMappings Is Nothing OrElse
               GlobalVariables.CopiedRegionWidth <= 0 OrElse
               GlobalVariables.CopiedRegionHeight <= 0 Then

                ' Update form title to indicate no copied tiles
                Me.Text = "Tile Selection - No Copied Tiles"
                pnlTilePasteViewport.Invalidate()
                Return
            End If

            ' Save dimensions for comparison later
            lastKnownWidth = GlobalVariables.CopiedRegionWidth
            lastKnownHeight = GlobalVariables.CopiedRegionHeight

            ' A new copy (made now, or before this window was opened): pick a
            ' zoom that shows it whole. Later refreshes of the same copy keep
            ' whatever zoom the user has chosen since.
            If GlobalVariables.CopiedTileMappings IsNot autoZoomedCopy Then
                autoZoomedCopy = GlobalVariables.CopiedTileMappings
                PickZoomForNewCopy(lastKnownWidth, lastKnownHeight)
            End If

            ' Update form title with dimensions and the zoom setting
            Me.Text = $"Tile Selection - {lastKnownWidth}×{lastKnownHeight} Tiles " &
                      If(zoomToFit, "(Fit)", $"({CInt(previewZoom * 100)}%)")

            ' Get reference to map and renderer
            If mainForm Is Nothing OrElse mainForm.CurrentLoadedMap Is Nothing OrElse mainForm.CurrentMapRenderer Is Nothing Then
                Debug.WriteLine("Cannot update copied tiles preview - missing required references")
                pnlTilePasteViewport.Invalidate()
                Return
            End If

            ' Create a bitmap to show the copied tiles. Tiles are drawn at the
            ' standard 32 px, but a large copy at that size needs a huge bitmap
            ' (e.g. 256x256 tiles = 8192x8192 px, 256 MB) whose allocation
            ' failed and left the window blank. Shrink the tile size so the
            ' preview stays within MaxPreviewPixels on each side.
            ' The size also follows the zoom level / Zoom to Fit setting.
            Const MaxPreviewPixels As Integer = 4096
            Dim largestSide As Integer = Math.Max(GlobalVariables.CopiedRegionWidth, GlobalVariables.CopiedRegionHeight)
            Dim tileSize As Integer = Math.Max(1, Math.Min(
                GetPreviewTileSize(GlobalVariables.CopiedRegionWidth, GlobalVariables.CopiedRegionHeight),
                MaxPreviewPixels \ largestSide))
            Dim width As Integer = GlobalVariables.CopiedRegionWidth * tileSize
            Dim height As Integer = GlobalVariables.CopiedRegionHeight * tileSize

            copiedTilesPreview = New Bitmap(width, height)

            ' Get references we need for rendering
            Dim map = mainForm.CurrentLoadedMap
            Dim renderer = mainForm.CurrentMapRenderer

            ' Render each copied tile
            Using g As Graphics = Graphics.FromImage(copiedTilesPreview)
                ' Draw a checkered background to indicate transparency
                Using checkerBrush As New SolidBrush(System.Drawing.Color.LightGray)
                    g.FillRectangle(checkerBrush, 0, 0, width, height)
                End Using

                ' Draw each tile
                For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                    For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                        Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)

                        ' Skip invalid mapping indices
                        If mappingIndex < 0 OrElse mappingIndex >= map.tileMappings.Count Then
                            Continue For
                        End If

                        ' Get the tileset index and image index for this mapping
                        Dim tilesetIndex As Integer = map.tileMappings(mappingIndex).tilesetIndex
                        Dim imageIndex As Integer = map.tileMappings(mappingIndex).tileGraphicIndex

                        ' Skip invalid indices
                        If tilesetIndex < 0 OrElse imageIndex < 0 Then
                            Continue For
                        End If

                        ' Try to get the tile image from the renderer
                        Dim tileImage As Bitmap = Nothing
                        Try
                            tileImage = renderer.GetTileImageByIndices(tilesetIndex, imageIndex)
                            If tileImage Is Nothing Then
                                Continue For
                            End If

                            ' Draw the tile, scaled down if the preview is shrunk
                            g.DrawImage(tileImage, x * tileSize, y * tileSize, tileSize, tileSize)

                            ' Draw grid line around the tile, unless the tiles
                            ' are so small that the lines would hide them
                            If tileSize >= 8 Then
                                g.DrawRectangle(Pens.Gray, x * tileSize, y * tileSize, tileSize, tileSize)
                            End If

                        Catch ex As Exception
                            Debug.WriteLine($"Error drawing copied tile at {x},{y}: {ex.Message}")
                        Finally
                            ' Ensure we dispose the tile image
                            If tileImage IsNot Nothing Then
                                tileImage.Dispose()
                            End If
                        End Try
                    Next
                Next

                ' Cell-type overlay, in the map's CellTypeOverlay colours. Drawn
                ' after all tiles so the grid lines stay visible through it.
                Dim cellTypes As CellType(,) = GlobalVariables.CopiedCellTypes
                If showCellTypes AndAlso cellTypes IsNot Nothing AndAlso
                   cellTypes.GetLength(0) >= GlobalVariables.CopiedRegionWidth AndAlso
                   cellTypes.GetLength(1) >= GlobalVariables.CopiedRegionHeight Then
                    ' One brush per colour, not per tile.
                    Dim cellBrushes As New Dictionary(Of System.Drawing.Color, SolidBrush)
                    Try
                        For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                            For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                                Dim colour As System.Drawing.Color = System.Drawing.Color.FromArgb(
                                    CellTypeOverlayAlpha, renderer.GetOverlayColorForCellType(cellTypes(x, y)))
                                Dim brush As SolidBrush = Nothing
                                If Not cellBrushes.TryGetValue(colour, brush) Then
                                    brush = New SolidBrush(colour)
                                    cellBrushes(colour) = brush
                                End If
                                g.FillRectangle(brush, x * tileSize, y * tileSize, tileSize, tileSize)
                            Next
                        Next
                    Finally
                        For Each b As SolidBrush In cellBrushes.Values
                            b.Dispose()
                        Next
                    End Try
                End If

                ' Draw border around the entire region
                Using borderPen As New Pen(System.Drawing.Color.DarkBlue, 2)
                    g.DrawRectangle(borderPen, 0, 0, width - 1, height - 1)
                End Using
            End Using

            ' Set panel size to match the bitmap
            pnlTilePasteViewport.AutoScrollMinSize = New Size(width, height)

            ' Refresh display
            pnlTilePasteViewport.Invalidate()

            Debug.WriteLine($"Updated copied tiles preview: {GlobalVariables.CopiedRegionWidth}×{GlobalVariables.CopiedRegionHeight} tiles")

        Catch ex As Exception
            Debug.WriteLine($"Error updating copied tiles preview: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Check if copied region has changed and update if needed
    ''' </summary>
    Public Sub CheckForCopiedRegionChanges()
        ' If dimensions have changed, update the preview
        If GlobalVariables.CopiedRegionWidth <> lastKnownWidth OrElse
           GlobalVariables.CopiedRegionHeight <> lastKnownHeight OrElse
           (GlobalVariables.CopiedTileMappings Is Nothing AndAlso copiedTilesPreview IsNot Nothing) OrElse
           (GlobalVariables.CopiedTileMappings IsNot Nothing AndAlso copiedTilesPreview Is Nothing) Then

            UpdateCopiedTilesPreview()
        End If
    End Sub

    ''' <summary>
    ''' Paint event handler for the tile paste viewport panel
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Paint event arguments</param>
    Private Sub PnlTilePasteViewport_Paint(sender As Object, e As PaintEventArgs)
        ' If no preview exists, show a message
        If copiedTilesPreview Is Nothing Then
            e.Graphics.Clear(System.Drawing.Color.LightGray)
            Using font As New Font("Arial", 10)
                e.Graphics.DrawString("No tiles copied", font, Brushes.Black, 10, 10)
                e.Graphics.DrawString("Use Copy (Ctrl+C) to copy tiles", font, Brushes.Black, 10, 30)
            End Using
            Return
        End If

        ' Draw the copied tiles preview at exactly its pixel size (the
        ' x/y-only overload would rescale it by the bitmap's DPI)
        e.Graphics.DrawImage(copiedTilesPreview,
                            -Math.Abs(pnlTilePasteViewport.AutoScrollPosition.X),
                            -Math.Abs(pnlTilePasteViewport.AutoScrollPosition.Y),
                            copiedTilesPreview.Width, copiedTilesPreview.Height)
    End Sub

    ''' <summary>
    ''' Mouse down event handler for the tile paste viewport panel
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Mouse event arguments</param>
    Private Sub PnlTilePasteViewport_MouseDown(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Right Then
            'Debug.WriteLine("PnlTilePasteViewport_MouseDown")

            ' Start dragging for panning the view
            isDragging = True
            startScrollPos = e.Location
            rightDownPos = e.Location
            pnlTilePasteViewport.Cursor = Cursors.Hand
        End If
    End Sub

    ''' <summary>
    ''' Mouse move event handler for the tile paste viewport panel
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Mouse event arguments</param>
    Private Sub PnlTilePasteViewport_MouseMove(sender As Object, e As MouseEventArgs)
        If isDragging AndAlso e.Button = MouseButtons.Right Then
            ' Calculate the scroll delta
            Dim deltaX As Integer = startScrollPos.X - e.X
            Dim deltaY As Integer = startScrollPos.Y - e.Y

            ' Apply the scroll (note: AutoScrollPosition returns negative values)
            Dim newX As Integer = Math.Abs(pnlTilePasteViewport.AutoScrollPosition.X) + deltaX
            Dim newY As Integer = Math.Abs(pnlTilePasteViewport.AutoScrollPosition.Y) + deltaY

            pnlTilePasteViewport.AutoScrollPosition = New Point(newX, newY)

            startScrollPos = e.Location
        End If
    End Sub

    ''' <summary>
    ''' Mouse up event handler for the tile paste viewport panel
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Mouse event arguments</param>
    Private Sub PnlTilePasteViewport_MouseUp(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Right Then
            isDragging = False
            pnlTilePasteViewport.Cursor = Cursors.Default

            ' Measure from where the button went down (not startScrollPos,
            ' which tracks each pan step). WinForms shows the menu itself;
            ' just tell the Opening handler whether this was a pan.
            Dim dragDistance As Double = Math.Sqrt(Math.Pow(e.X - rightDownPos.X, 2) + Math.Pow(e.Y - rightDownPos.Y, 2))
            suppressNextContextMenu = (dragDistance >= 5)
        End If
    End Sub

    ''' <summary>
    ''' Positions this form relative to other editor forms
    ''' </summary>
    ''' <param name="referenceForm">The form to position relative to</param>
    Public Sub PositionWindowRelativeToOthers(referenceForm As Form)
        If referenceForm Is Nothing Then Return

        ' Get the screen working area to ensure we stay within bounds
        Dim workingArea As Rectangle = Screen.FromControl(referenceForm).WorkingArea

        ' Find other editor forms
        Dim tileSetForm As Form = Nothing
        For Each form As Form In Application.OpenForms
            If TypeOf form Is fTileSet Then
                tileSetForm = form
                Exit For
            End If
        Next

        If tileSetForm IsNot Nothing Then
            ' Position below the TileSet form
            Dim newX As Integer = tileSetForm.Location.X
            Dim newY As Integer = tileSetForm.Location.Y + tileSetForm.Height + 1

            ' Ensure we stay on screen
            If newY + Me.Height > workingArea.Bottom Then
                ' Not enough room below - try to position to the right instead
                newX = tileSetForm.Location.X + tileSetForm.Width + 1
                newY = tileSetForm.Location.Y

                ' If still not enough room, position at bottom of screen
                If newX + Me.Width > workingArea.Right Then
                    newX = workingArea.Right - Me.Width
                    newY = workingArea.Bottom - Me.Height
                End If
            End If

            Me.Location = New Point(newX, newY)
        Else
            ' No tile set form - position to the right of the reference form
            Dim newX As Integer = referenceForm.Location.X + referenceForm.Width + 1
            Dim newY As Integer = referenceForm.Location.Y

            ' Ensure we stay on screen
            If newX + Me.Width > workingArea.Right Then
                newX = workingArea.Right - Me.Width
            End If

            If newY + Me.Height > workingArea.Bottom Then
                newY = workingArea.Bottom - Me.Height
            End If

            Me.Location = New Point(newX, newY)
        End If
    End Sub

    ''' <summary>
    ''' Handles the Export Tile Group menu item click
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Event arguments</param>
    Private Sub ExportTileGroup_Click(sender As Object, e As EventArgs)
        ' Check if we have any tiles to export
        If copiedTilesPreview Is Nothing Then
            MessageBox.Show("No tiles to export. Please copy a tile group first.", "Export Tiles", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Ask the user for a name for this tile group
        Dim tileName As String = InputBox("Enter a name for this tile group:", "Export Tile Group", $"TileGroup_{GlobalVariables.CopiedRegionWidth}x{GlobalVariables.CopiedRegionHeight}")

        ' If user cancels or enters an empty name, abort
        If String.IsNullOrEmpty(tileName) Then
            Return
        End If

        ' Sanitize the filename (remove invalid characters)
        tileName = String.Join("_", tileName.Split(Path.GetInvalidFileNameChars()))

        ' Tile-group exports always land in <Tilegroups>\Exported\. Keeping
        ' user exports in their own subfolder means the Tile Groups window
        ' shows them under a dedicated "Exported" category alongside any
        ' curated/shared sets the user has dropped in by hand.
        If String.IsNullOrEmpty(My.Settings.Tilegroups) Then
            My.Settings.Tilegroups = Path.Combine(Application.StartupPath, "Tilegroups")
        End If
        Dim exportFolder As String = Path.Combine(My.Settings.Tilegroups, "Exported")
        If Not Directory.Exists(exportFolder) Then
            Directory.CreateDirectory(exportFolder)
        End If

        ' Export the BMP + JSON together so the Tile Groups window can use both
        ExportTileGroupToBmp(tileName)
        ExportTileGroupToJson(tileName, JsonExportFormat.PerRowPadded)

        ' Refresh the Tile Groups window (if open) so the export shows up in
        ' the list immediately. We auto-select the "Exported" folder; the user
        ' can pick the specific tile group themselves.
        Dim tg As fTileGroups = TryCast(Application.OpenForms("fTileGroups"), fTileGroups)
        If tg IsNot Nothing Then
            tg.RefreshFromDisk("Exported")
        End If

        'Debug.WriteLine($"Tile group {tileName} export complete")
    End Sub

    ''' <summary>
    ''' Exports a tile group to a JSON file
    ''' </summary>
    ''' <param name="tileGroupName">Name of the tile group</param>
    ''' <param name="format">Format to use for export</param>
    Private Sub ExportTileGroupToJson(tileGroupName As String, format As JsonExportFormat)
        ' Should this function become part of OP2MapJsonToolsLibrary ??

        Try
            ' Make sure we have valid copied tile data
            If GlobalVariables.CopiedTileMappings Is Nothing Then
                Debug.WriteLine("No tile data to export")
                Return
            End If

            ' Create full path for the exported file (goes in <Tilegroups>\Exported\)
            Dim filePath As String = Path.Combine(My.Settings.Tilegroups, "Exported", $"{tileGroupName}.json")

            ' Get reference to the map
            Dim map = mainForm.CurrentLoadedMap
            If map Is Nothing Then
                Debug.WriteLine("No map loaded, cannot export tile data")
                Return
            End If

            ' Create the JSON object
            Dim json As New JObject()

            ' Identify the tileset used for this tile group
            Dim tilesetName As String = "well00" 'well00' is the default value and default tileset prefix

            ' Find the first valid tile to determine the tileset
            Dim foundValidTile As Boolean = False
            For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                    If foundValidTile Then Continue For

                    Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)

                    ' Skip invalid mapping indices
                    If mappingIndex >= 0 AndAlso mappingIndex < map.tileMappings.Count Then
                        ' Get the tileset index for this mapping
                        Dim tilesetIndex As Integer = map.tileMappings(mappingIndex).tilesetIndex

                        ' Skip invalid tileset indices
                        If tilesetIndex >= 0 AndAlso tilesetIndex < map.tilesetSources.Count Then
                            ' Get the tileset filename
                            Dim tilesetFilename As String = map.tilesetSources(tilesetIndex).tilesetFilename
                            If Not String.IsNullOrEmpty(tilesetFilename) AndAlso tilesetFilename.Length >= 6 Then
                                ' Extract the 6-character prefix
                                tilesetName = tilesetFilename.Substring(0, 6)
                                foundValidTile = True
                            End If
                        End If
                    End If
                Next
                If foundValidTile Then Exit For
            Next

            ' Add header information
            Dim header As New JObject()
            header.Add("width", GlobalVariables.CopiedRegionWidth)
            header.Add("height", GlobalVariables.CopiedRegionHeight)
            header.Add("tileset", tilesetName)
            header.Add("name", tileGroupName)
            header.Add("bmp", tileGroupName & ".bmp")
            header.Add("notes", "Exported from OP2Mapper")

            '' Add format information ??
            'If format = JsonExportFormat.Original Then
            '    header.Add("format", "original")
            'Else
            '    header.Add("format", "perrow")
            'End If

            json.Add("header", header)

            ' Add tile mapping data based on selected format
            If format = JsonExportFormat.Original Then
                'Get rid of this format..

                ' Original flat array format
                Dim tilesArray As New JArray()
                For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                    For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                        Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)
                        tilesArray.Add(mappingIndex)
                    Next
                Next
                json.Add("tiles", tilesArray)

                ' Write with standard formatting
                File.WriteAllText(filePath, JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8)

            Else ' PerRow or PerRowPadded
                ' Use array of arrays format
                Dim tilesArray As New JArray()
                For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                    Dim rowArray As New JArray()
                    For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                        Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)
                        rowArray.Add(mappingIndex)
                    Next
                    tilesArray.Add(rowArray)
                Next
                json.Add("tiles", tilesArray)

                ' Write to file based on format
                If format = JsonExportFormat.PerRow Then
                    'Get rid of this format..

                    File.WriteAllText(filePath, JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8)

                    ' Process the file to format rows on single lines with proper commas
                    Dim fileContent As String = File.ReadAllText(filePath)
                    Dim processedContent As String = FormatJsonArrays(fileContent, False)
                    File.WriteAllText(filePath, processedContent)

                Else    ' PerRowPadded
                    ' Format to keep. Make things more simple, One uniform format.

                    File.WriteAllText(filePath, JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8)

                    ' Process the file to format rows with padded numbers and proper commas
                    Dim fileContent As String = File.ReadAllText(filePath)
                    Dim processedContent As String = FormatJsonArrays(fileContent, True)
                    File.WriteAllText(filePath, processedContent)
                End If
            End If

            Debug.WriteLine("Tile group exported to JSON: " & filePath)

        Catch ex As Exception
            MessageBox.Show($"Error exporting tile group to JSON: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Debug.WriteLine($"Error in ExportTileGroupToJson: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Exports a tile group to a BMP image file
    ''' </summary>
    ''' <param name="tileName">Name of the tile group</param>
    Private Sub ExportTileGroupToBmp(tileName As String)
        Try
            ' Make sure we have a valid copiedTilesPreview
            If copiedTilesPreview Is Nothing Then
                Debug.WriteLine("No tile preview to export")
                Return
            End If

            ' Create full path for the exported file (goes in <Tilegroups>\Exported\)
            Dim filePath As String = Path.Combine(My.Settings.Tilegroups, "Exported", $"{tileName}.bmp")

            ' Render a full-size (32 px per tile) copy without the grid lines
            ' and border. Sized from the copied region, not the on-screen
            ' preview, which is drawn smaller for large copies.
            Using exportBitmap As New Bitmap(GlobalVariables.CopiedRegionWidth * 32,
                                             GlobalVariables.CopiedRegionHeight * 32)

                ' Get references we need for rendering
                Dim map = mainForm.CurrentLoadedMap
                Dim renderer = mainForm.CurrentMapRenderer

                ' Re-render each copied tile without grid lines
                Using g As Graphics = Graphics.FromImage(exportBitmap)
                    ' Fill with transparent background
                    g.Clear(System.Drawing.Color.Transparent)

                    ' Draw each tile
                    For y As Integer = 0 To GlobalVariables.CopiedRegionHeight - 1
                        For x As Integer = 0 To GlobalVariables.CopiedRegionWidth - 1
                            Dim mappingIndex As Integer = GlobalVariables.CopiedTileMappings(x, y)

                            ' Skip invalid mapping indices
                            If mappingIndex < 0 OrElse mappingIndex >= map.tileMappings.Count Then
                                Continue For
                            End If

                            ' Get the tileset index and image index for this mapping
                            Dim tilesetIndex As Integer = map.tileMappings(mappingIndex).tilesetIndex
                            Dim imageIndex As Integer = map.tileMappings(mappingIndex).tileGraphicIndex

                            ' Skip invalid indices
                            If tilesetIndex < 0 OrElse imageIndex < 0 Then
                                Continue For
                            End If

                            ' Try to get the tile image from the renderer
                            Dim tileImage As Bitmap = Nothing
                            Try
                                tileImage = renderer.GetTileImageByIndices(tilesetIndex, imageIndex)
                                If tileImage Is Nothing Then
                                    Continue For
                                End If

                                ' Draw the tile without any grid or borders
                                g.DrawImage(tileImage, x * 32, y * 32)

                            Catch ex As Exception
                                Debug.WriteLine($"Error drawing export tile at {x},{y}: {ex.Message}")
                            Finally
                                ' Ensure we dispose the tile image
                                If tileImage IsNot Nothing Then
                                    tileImage.Dispose()
                                End If
                            End Try
                        Next
                    Next
                End Using

                ' Save the image as BMP
                exportBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp)
            End Using ' exportBitmap - disposed even if rendering or saving fails

            ' Optionally open the folder containing the exported file
            If MessageBox.Show("Do you want to open the folder containing the exported file?", "Open Folder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                Process.Start("explorer.exe", $"/select,{filePath}")
            End If

            Debug.WriteLine("Tile group exported to BMP: " & filePath)

        Catch ex As Exception
            MessageBox.Show($"Error exporting tile group: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Debug.WriteLine($"Error in ExportTileGroupToBmp: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Formats JSON array content with proper commas between elements
    ''' </summary>
    ''' <param name="jsonContent">The JSON content to format</param>
    ''' <param name="padNumbers">Whether to pad numbers for alignment</param>
    ''' <returns>Formatted JSON string with proper commas</returns>
    Private Function FormatJsonArrays(jsonContent As String, padNumbers As Boolean) As String
        Dim lines As String() = jsonContent.Split(New String() {Environment.NewLine}, StringSplitOptions.None)
        Dim result As New List(Of String)

        Dim inTilesSection As Boolean = False
        Dim inArrayRow As Boolean = False
        Dim currentRow As String = ""

        For i As Integer = 0 To lines.Length - 1
            Dim line As String = lines(i).TrimEnd()

            ' Check if we're entering the tiles section
            If line.Contains("""tiles""") Then
                inTilesSection = True
                result.Add(line)
                Continue For
            End If

            ' Check if we're at the end of the tiles section
            If inTilesSection AndAlso line = "}" Then
                inTilesSection = False
                result.Add(line)
                Continue For
            End If

            ' Handle lines within the tiles section
            If inTilesSection Then
                ' Start of an array row
                If line.TrimStart().StartsWith("[") AndAlso Not line.TrimEnd().EndsWith("]") Then
                    inArrayRow = True
                    currentRow = line.TrimEnd()

                    ' End of an array row
                ElseIf inArrayRow AndAlso (line.TrimEnd().EndsWith("],") OrElse line.TrimEnd().EndsWith("]")) Then
                    currentRow += " " + line.Trim()

                    ' Apply padding if requested
                    If padNumbers Then
                        currentRow = PadNumbersInRow(currentRow)
                    End If

                    result.Add(currentRow)
                    inArrayRow = False
                    currentRow = ""

                    ' Middle of an array row
                ElseIf inArrayRow Then
                    currentRow += " " + line.Trim()

                    ' Regular line in the tiles section (not part of an array row)
                Else
                    result.Add(line)
                End If

                ' Lines outside the tiles section
            Else
                result.Add(line)
            End If
        Next

        Return String.Join(Environment.NewLine, result)
    End Function

    ''' <summary>
    ''' Pads numbers in a JSON array row for alignment while ensuring commas are preserved
    ''' </summary>
    ''' <param name="rowLine">The row line to pad</param>
    ''' <returns>A string with properly padded numbers and commas</returns>
    Private Function PadNumbersInRow(rowLine As String) As String
        ' Extract the beginning bracket part
        Dim startBracketIndex As Integer = rowLine.IndexOf("[")
        Dim prefix As String = rowLine.Substring(0, startBracketIndex + 1)

        ' Extract the end bracket part
        Dim endBracketIndex As Integer = rowLine.LastIndexOf("]")
        Dim suffix As String = rowLine.Substring(endBracketIndex)

        ' Extract the content between brackets
        Dim content As String = rowLine.Substring(startBracketIndex + 1, endBracketIndex - startBracketIndex - 1).Trim()

        ' Split by commas to get individual numbers
        Dim numberParts As String() = content.Split(","c)

        ' Trim and collect all numbers
        Dim trimmedNumbers As New List(Of String)
        For Each part As String In numberParts
            trimmedNumbers.Add(part.Trim())
        Next

        ' Find the maximum width needed for padding
        Dim maxWidth As Integer = 4 ' Default minimum width
        For Each numStr As String In trimmedNumbers
            If numStr.Length > maxWidth Then
                maxWidth = numStr.Length
            End If
        Next

        ' Pad each number to the maximum width
        Dim paddedNumbers As New List(Of String)
        For Each numStr As String In trimmedNumbers
            paddedNumbers.Add(numStr.PadLeft(maxWidth))
        Next

        ' Reconstruct the row with padded numbers and proper commas
        Return prefix & " " & String.Join(", ", paddedNumbers) & " " & suffix
    End Function

    ''' <summary>
    ''' Helper function for inline assignment in While loops
    ''' </summary>
    Private Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
        target = value
        Return value
    End Function
End Class