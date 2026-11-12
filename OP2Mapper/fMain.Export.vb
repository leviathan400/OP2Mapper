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

            ' Check if file already exists
            If File.Exists(exportFilename) Then
                Debug.WriteLine($"File already exists, deleting: {exportFilename}")
                File.Delete(exportFilename)
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

                ' Use MapRenderer to generate the map at specified scale.
                ' RenderFullMap returns Nothing when its internal map reference
                ' is null — throw a clear error so the outer Catch shows it.
                Dim exportBitmap As Bitmap = mapRenderer.RenderFullMap(CSng(scale))
                If exportBitmap Is Nothing Then
                    Throw New InvalidOperationException("Map renderer produced no output for export.")
                End If
                Using exportBitmap
                    ' Save as JPEG with high quality
                    Dim jpegEncoder As ImageCodecInfo = GetEncoder(ImageFormat.Jpeg)
                    Using encoderParameters As New EncoderParameters(1)
                        encoderParameters.Param(0) = New EncoderParameter(Encoder.Quality, 95L)
                        exportBitmap.Save(exportFilename, jpegEncoder, encoderParameters)
                    End Using
                End Using
            Finally
                mapRenderer.ShowGrid = savedShowGrid
                mapRenderer.ShowLargeGrid = savedShowLargeGrid
                mapRenderer.ShowCoordinates = savedShowCoordinates
            End Try

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
        If currentMap Is Nothing Then
            MessageBox.Show("Please load a map first.", "No Map Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Declared outside the Try so the Finally below can dispose it on the
        ' exception path. Previously only the happy path closed it — any throw
        ' between Show and the explicit Close left the progress dialog stuck
        ' on screen.
        Dim progressForm As Form = Nothing

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

            ' Remember original render mode
            Dim originalRenderMode As RenderMode = CurrentRenderMode

            ' Define scale for small images
            Dim scale As Double = 0.125 ' 12.5% size

            ' Generate output filename for the overview image
            Dim overviewFilename As String = Path.Combine(currentMapPath, Path.GetFileNameWithoutExtension(currentMapFile) & "_overview.jpg")

            ' Check if overview file already exists and delete it if it does
            If File.Exists(overviewFilename) Then
                Debug.WriteLine($"Overview file already exists, deleting: {overviewFilename}")
                File.Delete(overviewFilename)
            End If

            ' Array of render modes to include
            Dim renderModes As RenderMode() = {
                RenderMode.GameMap,
                RenderMode.CellTypeOverlay,
                RenderMode.CellTypeOverlayAll,
                RenderMode.CellTypeBasic,
                RenderMode.CellTypeAll
            }

            ' Generate filenames for each render mode image
            Dim modeFilenames As New List(Of String)()

            ' Generate each render mode image
            For Each mode In renderModes
                lblStatus.Text = $"Rendering {mode}..."
                Application.DoEvents()

                ' Switch to this render mode
                CurrentRenderMode = mode
                mapRenderer.RenderMode = CType(mode, MapRenderer.MapRenderMode)

                ' Generate filename suffix based on render mode
                Dim modeSuffix As String = "_" & mode.ToString() & "_small"

                ' Export this render mode to disk
                ExportImageCustomSize(scale, "small")

                ' Store the filename that was generated
                Dim modeFilename As String = Path.Combine(currentMapPath, Path.GetFileNameWithoutExtension(currentMapFile) & "_" & mode.ToString() & "_small.jpg")
                modeFilenames.Add(modeFilename)
            Next

            ' Now create the composite overview image
            lblStatus.Text = "Creating overview image..."
            Application.DoEvents()

            ' Load the individual mode images
            Dim modeImages As New List(Of Bitmap)()
            For Each filename In modeFilenames
                If File.Exists(filename) Then
                    modeImages.Add(New Bitmap(filename))
                Else
                    Throw New FileNotFoundException($"Cannot find render mode image: {filename}")
                End If
            Next

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
                            infoG.DrawRectangle(New Pen(System.Drawing.Color.DarkGray, 1), 0, 0, smallImageWidth - 1, smallImageHeight - 1)

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

                ' Save as JPEG with high quality
                Dim jpegEncoder As ImageCodecInfo = GetEncoder(ImageFormat.Jpeg)
                Using encoderParameters As New EncoderParameters(1)
                    encoderParameters.Param(0) = New EncoderParameter(Encoder.Quality, 95L)
                    compositeImage.Save(overviewFilename, jpegEncoder, encoderParameters)
                End Using
            End Using

            ' Dispose all loaded images
            For Each img In modeImages
                img.Dispose()
            Next

            ' Restore original render mode
            CurrentRenderMode = originalRenderMode
            mapRenderer.RenderMode = CType(originalRenderMode, MapRenderer.MapRenderMode)
            pnlMap.Invalidate()

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
