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

' fMain.Export.vb
' Partial class of fMain (split out of fMain.vb).
' Image export - full map, scaled, grid-overlay variants and the multi-mode overview composite.
Partial Public Class fMain

#Region "Image Export Methods"

    ''' <summary>
    ''' Which (if any) overlay an export should force on, regardless of the
    ''' editor's current grid/coordinate toggles.
    ''' </summary>
    Private Enum ExportGridMode
        None
        SmallGrid
        LargeGrid
    End Enum

    ''' <summary>
    ''' Largest export bitmap we'll attempt, in pixels. A 32bpp GDI+ bitmap of
    ''' this size is ~1 GB; anything bigger reliably fails with an unhelpful
    ''' "Parameter is not valid" / OutOfMemory, so we refuse up front instead.
    ''' </summary>
    Private Const MaxExportPixels As Long = 16384L * 16384L

    ''' <summary>
    ''' Renders the full map at the given scale with a forced overlay set (the
    ''' editor's own grid/coordinate toggles are saved and restored). Throws
    ''' with a clear message if the image would be too large. Caller owns the
    ''' returned bitmap.
    ''' </summary>
    Private Function RenderExportBitmap(scale As Double, gridMode As ExportGridMode) As Bitmap
        Dim pixelWidth As Long = CLng(Math.Round(currentMap.WidthInTiles() * 32 * scale))
        Dim pixelHeight As Long = CLng(Math.Round(currentMap.HeightInTiles() * 32 * scale))
        If pixelWidth * pixelHeight > MaxExportPixels Then
            Throw New InvalidOperationException(
                $"The image would be {pixelWidth:N0} x {pixelHeight:N0} pixels, which is too large to create. " &
                "Use a smaller export size (e.g. 12.5%) for this map.")
        End If

        ' The export always picks a deterministic overlay set, regardless of
        ' what the user has toggled in the editor. Save the on-screen flags,
        ' force the chosen combination for the render, then restore.
        Dim savedShowGrid As Boolean = mapRenderer.ShowGrid
        Dim savedShowLargeGrid As Boolean = mapRenderer.ShowLargeGrid
        Dim savedShowCoordinates As Boolean = mapRenderer.ShowCoordinates

        Try
            mapRenderer.ShowGrid = (gridMode = ExportGridMode.SmallGrid)
            mapRenderer.ShowLargeGrid = (gridMode = ExportGridMode.LargeGrid)
            mapRenderer.ShowCoordinates = False

            Dim bmp As Bitmap
            Try
                bmp = mapRenderer.RenderFullMap(CSng(scale))
            Catch ex As Exception When TypeOf ex Is OutOfMemoryException OrElse TypeOf ex Is ArgumentException
                ' GDI+ reports a failed large allocation as either of these.
                Throw New InvalidOperationException(
                    $"Not enough memory to create a {pixelWidth:N0} x {pixelHeight:N0} pixel image. " &
                    "Try a smaller export size.", ex)
            End Try

            ' RenderFullMap returns Nothing when its internal map reference is null.
            If bmp Is Nothing Then
                Throw New InvalidOperationException("Map renderer produced no output for export.")
            End If
            Return bmp
        Finally
            mapRenderer.ShowGrid = savedShowGrid
            mapRenderer.ShowLargeGrid = savedShowLargeGrid
            mapRenderer.ShowCoordinates = savedShowCoordinates
        End Try
    End Function

    ''' <summary>
    ''' Saves a bitmap as a quality-95 JPEG. Writes to a temp file first and
    ''' only then replaces the target, so a failed save never destroys an
    ''' existing export.
    ''' </summary>
    Private Sub SaveJpegSafely(bmp As Bitmap, targetPath As String)
        Dim tempPath As String = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetPath)),
            Path.GetFileNameWithoutExtension(targetPath) & ".saving-" & Guid.NewGuid().ToString("N").Substring(0, 8) & ".jpg")
        Try
            Dim jpegEncoder As ImageCodecInfo = GetEncoder(ImageFormat.Jpeg)
            Using encoderParameters As New EncoderParameters(1)
                encoderParameters.Param(0) = New EncoderParameter(Encoder.Quality, 95L)
                bmp.Save(tempPath, jpegEncoder, encoderParameters)
            End Using

            If File.Exists(targetPath) Then
                File.Replace(tempPath, targetPath, Nothing)
            Else
                File.Move(tempPath, targetPath)
            End If
        Finally
            Try
                If File.Exists(tempPath) Then File.Delete(tempPath)
            Catch
                ' Best effort - a leftover temp file is harmless.
            End Try
        End Try
    End Sub

    ''' <summary>
    ''' Renders the full map at the given scale and saves it as a high-quality JPEG next
    ''' to the map file, with an optional filename suffix and forced grid overlay.
    ''' </summary>
    Private Sub ExportImageCustomSize(scale As Double, Optional filenameSuffix As String = "",
                                      Optional gridMode As ExportGridMode = ExportGridMode.None)
        If currentMap Is Nothing OrElse mapRenderer Is Nothing Then
            Return
        End If

        Try
            ' Generate filename in the same directory as the map file
            Dim exportFilename As String
            If String.IsNullOrEmpty(filenameSuffix) Then
                exportFilename = Path.Combine(currentMapPath, Path.GetFileNameWithoutExtension(currentMapFile) & "_" & CurrentRenderMode.ToString() & ".jpg")
            Else
                exportFilename = Path.Combine(currentMapPath, Path.GetFileNameWithoutExtension(currentMapFile) & "_" & CurrentRenderMode.ToString() & "_" & filenameSuffix & ".jpg")
            End If

            ' Render first, then save via a temp file: the previous export is
            ' only replaced once the new one has been written successfully.
            Using exportBitmap As Bitmap = RenderExportBitmap(scale, gridMode)
                SaveJpegSafely(exportBitmap, exportFilename)
            End Using

            ' Calculate scale percentage for message
            Dim scalePercent As Integer = CInt(scale * 100)

            ' Show success message
            If scale = 1.0 Then
                MessageBox.Show("Map image exported successfully to: " & exportFilename, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Else
                MessageBox.Show("Map image exported successfully at " & scalePercent & "% size to: " & exportFilename, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
            MessageBox.Show("Error exporting image: " & ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Debug.WriteLine($"Error exporting image: {ex}")
        End Try
    End Sub

    ''' <summary>
    ''' Creates and saves a comprehensive overview image with multiple render modes
    ''' </summary>
    Private Sub ExportOverviewImage()
        If currentMap Is Nothing OrElse mapRenderer Is Nothing Then
            MessageBox.Show("Please load a map first.", "No Map Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Declared outside the Try so the Finally below can dispose it on the
        ' exception path. Previously only the happy path closed it — any throw
        ' between Show and the explicit Close left the progress dialog stuck
        ' on screen.
        Dim progressForm As Form = Nothing
        Dim modeImages As New List(Of Bitmap)()
        Dim originalRendererMode As MapRenderer.MapRenderMode = mapRenderer.RenderMode

        Try
            ' Create a progress form
            progressForm = New Form()
            progressForm.Text = "Generating Overview..."
            progressForm.StartPosition = FormStartPosition.CenterParent
            progressForm.Size = New Size(300, 100)
            progressForm.FormBorderStyle = FormBorderStyle.FixedDialog
            progressForm.ControlBox = False

            Dim lblStatus As New Label()
            lblStatus.Text = "Creating overview image..."
            lblStatus.Dock = DockStyle.Fill
            progressForm.Controls.Add(lblStatus)

            progressForm.Show(Me)
            Application.DoEvents()

            ' Define scale for small images
            Dim scale As Double = 0.125 ' 12.5% size

            ' Generate output filename for the overview image
            Dim overviewFilename As String = Path.Combine(currentMapPath, Path.GetFileNameWithoutExtension(currentMapFile) & "_overview.jpg")

            ' Array of render modes to include
            Dim renderModes As RenderMode() = {
                RenderMode.GameMap,
                RenderMode.CellTypeOverlay,
                RenderMode.CellTypeOverlayAll,
                RenderMode.CellTypeBasic,
                RenderMode.CellTypeAll
            }

            ' Render each mode straight to memory. This used to export each one
            ' as <map>_<mode>_small.jpg via ExportImageCustomSize (overwriting
            ' the user's own small exports and showing an "Export Complete"
            ' box per mode) and then read the files back. Only the renderer's
            ' mode is switched; the editor's CurrentRenderMode and tool state
            ' are untouched, and the Finally restores the renderer.
            For Each mode In renderModes
                lblStatus.Text = $"Rendering {mode}..."
                Application.DoEvents()

                mapRenderer.RenderMode = CType(mode, MapRenderer.MapRenderMode)
                modeImages.Add(RenderExportBitmap(scale, ExportGridMode.None))
            Next

            ' Now create the composite overview image
            lblStatus.Text = "Creating overview image..."
            Application.DoEvents()

            ' Get dimensions of the first image to calculate layout
            Dim smallImageWidth As Integer = modeImages(0).Width
            Dim smallImageHeight As Integer = modeImages(0).Height

            ' Define padding
            Const padding As Integer = 6

            ' Calculate total image dimensions (3x2 grid with padding)
            Dim totalWidth As Integer = smallImageWidth * 3 + padding * 4
            Dim totalHeight As Integer = smallImageHeight * 2 + padding * 3

            ' Create the composite image
            Using compositeImage As New Bitmap(totalWidth, totalHeight)
                Using g As Graphics = Graphics.FromImage(compositeImage)
                    ' Fill background with white
                    g.Clear(System.Drawing.Color.White)

                    ' Calculate positions for each image in the grid
                    Dim positions As Point() = {
                        New Point(padding + smallImageWidth + padding, padding),                                ' Top-middle (GameMap)
                        New Point(padding + smallImageWidth + padding + smallImageWidth + padding, padding),    ' Top-right (CellTypeOverlay)
                        New Point(padding, padding + smallImageHeight + padding),                               ' Bottom-left (CellTypeOverlayAll)
                        New Point(padding + smallImageWidth + padding, padding + smallImageHeight + padding),   ' Bottom-middle (CellTypeBasic)
                        New Point(padding + smallImageWidth + padding + smallImageWidth + padding, padding + smallImageHeight + padding) ' Bottom-right (CellTypeAll)
                    }

                    ' Place the images in the grid
                    For i As Integer = 0 To modeImages.Count - 1
                        ' Draw border around the image
                        g.FillRectangle(Brushes.White, positions(i).X - 1, positions(i).Y - 1, smallImageWidth + 2, smallImageHeight + 2)
                        ' Draw the image
                        g.DrawImage(modeImages(i), positions(i))
                    Next

                    ' Create the info panel for the top-left cell
                    Using infoPanel As New Bitmap(smallImageWidth, smallImageHeight)
                        Using infoG As Graphics = Graphics.FromImage(infoPanel)
                            ' Set up for info panel drawing
                            infoG.Clear(System.Drawing.Color.FromArgb(240, 240, 240))  ' Light gray background
                            infoG.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

                            ' Draw border
                            Using borderPen As New Pen(System.Drawing.Color.DarkGray, 1)
                                infoG.DrawRectangle(borderPen, 0, 0, smallImageWidth - 1, smallImageHeight - 1)
                            End Using

                            ' Draw info text 
                            Using titleFont As New Font("Arial", 16, FontStyle.Bold)
                                Using normalFont As New Font("Arial", 12)
                                    ' Draw title
                                    infoG.DrawString("Map Overview", titleFont, Brushes.Black, 10, 10)

                                    ' Draw map info
                                    Dim y As Integer = 36
                                    infoG.DrawString("Map: " & Path.GetFileName(currentMapFile), normalFont, Brushes.Black, 10, y)
                                    y += 20
                                    infoG.DrawString("Size: " & currentMap.WidthInTiles() & " x " & currentMap.HeightInTiles(), normalFont, Brushes.Black, 10, y)
                                    y += 20
                                    infoG.DrawString("Tiles: " & currentMap.TileCount().ToString("#,##0"), normalFont, Brushes.Black, 10, y)

                                    y += 36

                                    infoG.DrawString("Render Modes:", normalFont, Brushes.Black, 10, y)
                                    y += 20

                                    ' Draw render mode list
                                    Dim modeDescriptions As String() = {
                                        "1: Game Map (top-middle)",
                                        "2: CellType Overlay (top-right)",
                                        "3: CellType Overlay All (bottom-left)",
                                        "4: CellType Basic (bottom-middle)",
                                        "5: CellType All (bottom-right)"
                                    }

                                    For Each desc In modeDescriptions
                                        infoG.DrawString(desc, normalFont, Brushes.Black, 10, y)
                                        y += 20
                                    Next

                                    ' Add generation date at bottom
                                    infoG.DrawString("Generated: " & DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), normalFont, Brushes.Black, 10, smallImageHeight - 30)
                                End Using
                            End Using
                        End Using

                        ' Draw border around the info panel
                        g.FillRectangle(Brushes.White, padding - 1, padding - 1, smallImageWidth + 2, smallImageHeight + 2)
                        ' Add the info panel to the composite image
                        g.DrawImage(infoPanel, padding, padding)
                    End Using
                End Using

                ' Save as JPEG with high quality, via a temp file so a failure
                ' keeps any previous overview intact.
                SaveJpegSafely(compositeImage, overviewFilename)
            End Using

            ' Close progress form
            progressForm.Close()

            ' Show success message
            MessageBox.Show("Overview image exported successfully to: " & overviewFilename, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' Open the image with the default viewer
            Process.Start(overviewFilename)

        Catch ex As Exception
            MessageBox.Show("Error exporting overview image: " & ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Debug.WriteLine($"Export Overview error: {ex}")
        Finally
            ' Always free the per-mode bitmaps and put the renderer back in the
            ' mode the editor is showing, success or failure.
            For Each img In modeImages
                img.Dispose()
            Next
            mapRenderer.RenderMode = originalRendererMode
            pnlMap.Invalidate()

            ' Safety net for the exception path. On the happy path the form
            ' was already Closed (and modeless forms auto-dispose on Close),
            ' so IsDisposed is true here and we do nothing.
            If progressForm IsNot Nothing AndAlso Not progressForm.IsDisposed Then
                progressForm.Close()
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Returns the GDI+ encoder for the given image format. Throws if the
    ''' system has no encoder for that format (vanishingly unlikely on Windows
    ''' for JPEG/PNG, but better than a NullReferenceException deep in
    ''' Bitmap.Save).
    ''' </summary>
    Private Function GetEncoder(format As ImageFormat) As ImageCodecInfo
        For Each codec As ImageCodecInfo In ImageCodecInfo.GetImageEncoders()
            If codec.FormatID = format.Guid Then
                Return codec
            End If
        Next

        Throw New InvalidOperationException(
            $"No image encoder is registered for format {format} on this system.")
    End Function

#End Region

End Class
