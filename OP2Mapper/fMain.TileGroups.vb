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

' fMain.TileGroups.vb
' Partial class of fMain (split out of fMain.vb).
' Tile group placement - placement mode and preview of multi-tile groups on the map.
Partial Public Class fMain

#Region "Tile Groups"

    ''' <summary>
    ''' Initializes tile group placement mode with preview
    ''' </summary>
    Public Sub InitializeTileGroupPlacementMode()
        Debug.WriteLine("Initializing tile group placement mode")

        ' Exit any other modes first
        If isPasteMode Then
            ExitPasteMode()
        End If

        If GlobalVariables.IsCopyMode Then
            ExitCopyMode()
        End If

        ' Set tile group placement mode flag
        GlobalVariables.TileGroupPlacementMode = True

        ' Create preview image for the tile group
        CreateTileGroupPreviewImage()

        ' Update UI
        lblCurrentTool.Text = "Current Tool: Place Tile Group"
        lblCurrentTool.ForeColor = System.Drawing.Color.DarkMagenta
        lblCurrentPlacement.Text = $"Click location to place {GlobalVariables.TileGroupWidth}x{GlobalVariables.TileGroupHeight} tile group"
        lblCurrentPlacement.ForeColor = System.Drawing.Color.Magenta

        ' Update tool display with a tile group icon
        If picSelectedTool IsNot Nothing Then
            Using groupIndicator As New Bitmap(32, 32)
                Using g As Graphics = Graphics.FromImage(groupIndicator)
                    g.Clear(System.Drawing.Color.Lavender)
                    g.DrawRectangle(New Pen(System.Drawing.Color.DarkMagenta, 2), 2, 2, 28, 28)

                    Using font As New Font("Arial", 7, FontStyle.Bold)
                        Dim text As String = $"{GlobalVariables.TileGroupWidth}x{GlobalVariables.TileGroupHeight}"
                        Dim size = g.MeasureString(text, font)

                        If size.Width < 28 Then
                            g.DrawString(text, font, Brushes.DarkMagenta,
                                       (32 - size.Width) / 2,
                                       (32 - size.Height) / 2)
                        End If
                    End Using
                End Using

                If picSelectedTool.Image IsNot Nothing Then
                    picSelectedTool.Image.Dispose()
                End If
                picSelectedTool.Image = New Bitmap(groupIndicator)
                picSelectedTool.BackColor = System.Drawing.Color.Lavender
            End Using
        End If

        ' Update cursor
        pnlMap.Cursor = Cursors.Cross

        Debug.WriteLine("Tile group placement mode initialized")
    End Sub

    ''' <summary>
    ''' Semi-transparent preview bitmap of the tile group, drawn at the cursor during
    ''' placement mode. Owned here; disposed/rebuilt by the methods below.
    ''' </summary>
    Private tileGroupPreviewImage As Bitmap = Nothing

    ''' <summary>
    ''' Builds the tile group preview image: each tile drawn at 70% opacity with a
    ''' magenta border around the whole group.
    ''' </summary>
    Private Sub CreateTileGroupPreviewImage()
        Debug.WriteLine("Creating tile group preview image")

        ' Dispose any existing preview
        If tileGroupPreviewImage IsNot Nothing Then
            tileGroupPreviewImage.Dispose()
            tileGroupPreviewImage = Nothing
        End If

        If mapRenderer Is Nothing Then
            Debug.WriteLine("MapRenderer is null")
            Return
        End If

        Try
            Dim width As Integer = GlobalVariables.TileGroupWidth * 32
            Dim height As Integer = GlobalVariables.TileGroupHeight * 32

            tileGroupPreviewImage = New Bitmap(width, height)

            Using g As Graphics = Graphics.FromImage(tileGroupPreviewImage)
                g.Clear(System.Drawing.Color.Transparent)

                ' Draw each tile with semi-transparency
                For y As Integer = 0 To GlobalVariables.TileGroupHeight - 1
                    For x As Integer = 0 To GlobalVariables.TileGroupWidth - 1
                        Dim mappingIndex As Integer = GlobalVariables.TileGroupTileMappings(x, y)

                        If mappingIndex < 0 OrElse mappingIndex >= currentMap.tileMappings.Count Then
                            Continue For
                        End If

                        Dim tilesetIndex As Integer = currentMap.tileMappings(mappingIndex).tilesetIndex
                        Dim imageIndex As Integer = currentMap.tileMappings(mappingIndex).tileGraphicIndex

                        If tilesetIndex < 0 OrElse imageIndex < 0 Then
                            Continue For
                        End If

                        Dim tileImage As Bitmap = Nothing
                        Try
                            tileImage = mapRenderer.GetTileImageByIndices(tilesetIndex, imageIndex)
                            If tileImage Is Nothing Then
                                Continue For
                            End If

                            ' Draw with semi-transparency
                            Dim cm As New Imaging.ColorMatrix()
                            cm.Matrix33 = 0.7F ' 70% opacity

                            Using ia As New Imaging.ImageAttributes()
                                ia.SetColorMatrix(cm)
                                g.DrawImage(tileImage,
                                          New Rectangle(x * 32, y * 32, 32, 32),
                                          0, 0, tileImage.Width, tileImage.Height,
                                          GraphicsUnit.Pixel, ia)
                            End Using
                        Finally
                            If tileImage IsNot Nothing Then
                                tileImage.Dispose()
                            End If
                        End Try
                    Next
                Next

                ' Add border
                g.DrawRectangle(New Pen(System.Drawing.Color.Magenta, 2), 0, 0, width - 1, height - 1)
            End Using

        Catch ex As Exception
            Debug.WriteLine($"Error creating tile group preview: {ex.Message}")
            If tileGroupPreviewImage IsNot Nothing Then
                tileGroupPreviewImage.Dispose()
                tileGroupPreviewImage = Nothing
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Exits tile group placement mode
    ''' </summary>
    Private Sub ExitTileGroupPlacementMode()
        Debug.WriteLine("Exiting tile group placement mode")

        GlobalVariables.TileGroupPlacementMode = False
        GlobalVariables.TileGroupPreviewX = -1
        GlobalVariables.TileGroupPreviewY = -1

        If tileGroupPreviewImage IsNot Nothing Then
            tileGroupPreviewImage.Dispose()
            tileGroupPreviewImage = Nothing
        End If

        currentTileX = -1
        currentTileY = -1

        ResetToolUI(True)

        pnlMap.Cursor = Cursors.Default
        pnlMap.Invalidate()
        pnlMap.Refresh()
    End Sub

    ''' <summary>
    ''' Executes the tile group placement
    ''' </summary>
    Private Sub ExecuteTileGroupPlacement(startX As Integer, startY As Integer)
        Debug.WriteLine($"Executing tile group placement at {FormatTileCoords(startX, startY)}")

        If Not GlobalVariables.TileGroupPlacementMode OrElse GlobalVariables.TileGroupTileMappings Is Nothing Then
            Return
        End If

        ' Check bounds
        If startX + GlobalVariables.TileGroupWidth > currentMap.WidthInTiles() OrElse
           startY + GlobalVariables.TileGroupHeight > currentMap.HeightInTiles() Then
            MessageBox.Show("Cannot place tile group at this location. It would extend beyond the map boundary.",
                           "Placement Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Create command for undo/redo
        Dim placeCommand As New PlaceTileGroupCommand(currentMap, startX, startY,
                                             GlobalVariables.TileGroupTileMappings,
                                             GlobalVariables.TileGroupWidth,
                                             GlobalVariables.TileGroupHeight,
                                             GlobalVariables.TileGroupFileName)

        ExecuteCommand(placeCommand)

        ' Repaint just the placed region.
        InvalidateTileRegion(startX, startY,
                             GlobalVariables.TileGroupWidth, GlobalVariables.TileGroupHeight)

        ExitTileGroupPlacementMode()

        Debug.WriteLine("Tile group placement completed")
    End Sub

#End Region

End Class
