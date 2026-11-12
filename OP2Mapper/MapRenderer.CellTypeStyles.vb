Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports OP2UtilityDotNet
Imports OP2UtilityDotNet.OP2Map
Imports OP2UtilityDotNet.Bitmap
Imports OP2UtilityDotNet.Sprite

' MapRenderer.CellTypeStyles.vb
' Partial class of MapRenderer (split out of MapRenderer.vb).
' Cell type presentation lookups - colors and labels for the cell-type render modes.
Partial Public Class MapRenderer

    ' Color and label helper methods

    ''' <summary>
    ''' Gets the basic color for a cell type
    ''' </summary>
    Private Function GetCellTypeColor(cellType As CellType) As System.Drawing.Color
        Select Case cellType
            Case CellType.FastPassible1, CellType.FastPassible2
                Return System.Drawing.Color.LightGreen
            Case CellType.SlowPassible1, CellType.SlowPassible2
                Return System.Drawing.Color.Yellow
            Case CellType.MediumPassible1, CellType.MediumPassible2
                Return System.Drawing.Color.Olive
            Case CellType.Impassible1, CellType.Impassible2
                Return System.Drawing.Color.DarkRed
            Case CellType.CliffsHighSide, CellType.CliffsLowSide, CellType.NorthCliffs
                Return System.Drawing.Color.Sienna
            Case CellType.VentsAndFumaroles
                Return System.Drawing.Color.Orange
            Case CellType.DozedArea
                Return System.Drawing.Color.LightGray
            Case CellType.Rubble
                Return System.Drawing.Color.DarkGray
            Case CellType.NormalWall
                Return System.Drawing.Color.Gray
            Case CellType.MicrobeWall
                Return System.Drawing.Color.Purple
            Case CellType.LavaWall
                Return System.Drawing.Color.OrangeRed
            Case CellType.Tube0, CellType.Tube1, CellType.Tube2, CellType.Tube3, CellType.Tube4, CellType.Tube5
                Return System.Drawing.Color.Cyan
            Case Else
                Return System.Drawing.Color.White
        End Select
    End Function

    ''' <summary>
    ''' Gets the detailed color for a cell type
    ''' </summary>
    Private Function GetDetailedColorForCellType(cellType As CellType) As System.Drawing.Color
        ' Detailed colors with specific RGB values
        Select Case cellType
            ' Fast Passable - Bright Green
            Case CellType.FastPassible1, CellType.FastPassible2
                Return System.Drawing.Color.FromArgb(0, 255, 0)

            ' Medium Passable - Yellow
            Case CellType.MediumPassible1, CellType.MediumPassible2
                Return System.Drawing.Color.FromArgb(255, 255, 0)

            ' Slow Passable - Orange
            Case CellType.SlowPassible1, CellType.SlowPassible2
                Return System.Drawing.Color.FromArgb(255, 128, 0)

            ' Impassable - Red
            Case CellType.Impassible1, CellType.Impassible2
                Return System.Drawing.Color.FromArgb(255, 0, 0)

            ' Cliffs - Brown
            Case CellType.NorthCliffs, CellType.CliffsHighSide, CellType.CliffsLowSide
                Return System.Drawing.Color.FromArgb(139, 69, 19)

            ' Vents - Purple
            Case CellType.VentsAndFumaroles
                Return System.Drawing.Color.FromArgb(128, 0, 128)

            ' Dozed Areas - Light Gray
            Case CellType.DozedArea
                Return System.Drawing.Color.FromArgb(192, 192, 192)

            ' Rubble - Dark Gray
            Case CellType.Rubble
                Return System.Drawing.Color.FromArgb(64, 64, 64)

            ' Walls - Different shades of gray
            Case CellType.NormalWall
                Return System.Drawing.Color.FromArgb(128, 128, 128)
            Case CellType.MicrobeWall
                Return System.Drawing.Color.FromArgb(128, 0, 255)
            Case CellType.LavaWall
                Return System.Drawing.Color.FromArgb(255, 69, 0)

            ' Tubes - Cyan
            Case CellType.Tube0, CellType.Tube1, CellType.Tube2, CellType.Tube3, CellType.Tube4, CellType.Tube5
                Return System.Drawing.Color.FromArgb(0, 255, 255)

                ' Default/Unknown
            Case Else
                Return System.Drawing.Color.White
        End Select
    End Function

    ''' <summary>
    ''' Gets the overlay color for a cell type
    ''' </summary>
    Private Function GetOverlayColorForCellType(cellType As CellType) As System.Drawing.Color
        Select Case cellType
            ' Impassable - Red
            Case CellType.Impassible1, CellType.Impassible2,
                 CellType.NorthCliffs, CellType.CliffsHighSide, CellType.CliffsLowSide,
                 CellType.Rubble, CellType.NormalWall, CellType.MicrobeWall, CellType.LavaWall
                Return System.Drawing.Color.FromArgb(192, 0, 0)  ' Deep Red

            ' Slow Passable - Orange
            Case CellType.SlowPassible1, CellType.SlowPassible2, CellType.VentsAndFumaroles
                Return System.Drawing.Color.FromArgb(255, 128, 0)  ' Orange

            ' Medium Passable - Yellow
            Case CellType.MediumPassible1, CellType.MediumPassible2
                Return System.Drawing.Color.FromArgb(255, 255, 0)  ' Yellow

            ' Fast Passable - Green
            Case CellType.FastPassible1, CellType.FastPassible2, CellType.DozedArea
                Return System.Drawing.Color.FromArgb(0, 255, 0)  ' Bright Green

            ' Tubes - Cyan
            Case CellType.Tube0, CellType.Tube1, CellType.Tube2, CellType.Tube3, CellType.Tube4, CellType.Tube5
                Return System.Drawing.Color.FromArgb(0, 255, 255)  ' Cyan

                ' Default - White
            Case Else
                Return System.Drawing.Color.White
        End Select
    End Function

    ''' <summary>
    ''' Gets the label and color for a cell type
    ''' </summary>
    Private Function GetCellTypeLabel(cellType As CellType) As Tuple(Of String, System.Drawing.Color)
        ' Returns a tuple with the abbreviated label and color for each cell type
        Select Case cellType
            ' Fast Passable - Bright Green
            Case CellType.FastPassible1
                Return New Tuple(Of String, System.Drawing.Color)("Fast1", System.Drawing.Color.LawnGreen)
            Case CellType.FastPassible2
                Return New Tuple(Of String, System.Drawing.Color)("Fast2", System.Drawing.Color.LawnGreen)

            ' Medium Passable - Yellow
            Case CellType.MediumPassible1
                Return New Tuple(Of String, System.Drawing.Color)("Med1", System.Drawing.Color.Yellow)
            Case CellType.MediumPassible2
                Return New Tuple(Of String, System.Drawing.Color)("Med2", System.Drawing.Color.Yellow)

            ' Slow Passable - Orange
            Case CellType.SlowPassible1
                Return New Tuple(Of String, System.Drawing.Color)("Slow1", System.Drawing.Color.Orange)
            Case CellType.SlowPassible2
                Return New Tuple(Of String, System.Drawing.Color)("Slow2", System.Drawing.Color.Orange)

            ' Impassable - Red
            Case CellType.Impassible1
                Return New Tuple(Of String, System.Drawing.Color)("Imp1", System.Drawing.Color.Red)
            Case CellType.Impassible2
                Return New Tuple(Of String, System.Drawing.Color)("Imp2", System.Drawing.Color.Red)

            ' Cliffs - Red
            Case CellType.NorthCliffs
                Return New Tuple(Of String, System.Drawing.Color)("N Clif", System.Drawing.Color.Red)
            Case CellType.CliffsHighSide
                Return New Tuple(Of String, System.Drawing.Color)("Clif H", System.Drawing.Color.Red)
            Case CellType.CliffsLowSide
                Return New Tuple(Of String, System.Drawing.Color)("Clif L", System.Drawing.Color.Red)

            ' Vents - White
            Case CellType.VentsAndFumaroles
                Return New Tuple(Of String, System.Drawing.Color)("Vent", System.Drawing.Color.White)

            ' Everything else - default labels in white
            Case CellType.DozedArea
                Return New Tuple(Of String, System.Drawing.Color)("Dozed", System.Drawing.Color.LightGreen)
            Case CellType.Rubble
                Return New Tuple(Of String, System.Drawing.Color)("Rubble", System.Drawing.Color.Red)
            Case CellType.NormalWall
                Return New Tuple(Of String, System.Drawing.Color)("Wall", System.Drawing.Color.Red)
            Case CellType.MicrobeWall
                Return New Tuple(Of String, System.Drawing.Color)("MWall", System.Drawing.Color.Red)
            Case CellType.LavaWall
                Return New Tuple(Of String, System.Drawing.Color)("LWall", System.Drawing.Color.Red)
            Case CellType.Tube0
                Return New Tuple(Of String, System.Drawing.Color)("Tube0", System.Drawing.Color.Cyan)
            Case CellType.Tube1
                Return New Tuple(Of String, System.Drawing.Color)("Tube1", System.Drawing.Color.Cyan)
            Case CellType.Tube2
                Return New Tuple(Of String, System.Drawing.Color)("Tube2", System.Drawing.Color.Cyan)
            Case CellType.Tube3
                Return New Tuple(Of String, System.Drawing.Color)("Tube3", System.Drawing.Color.Cyan)
            Case CellType.Tube4
                Return New Tuple(Of String, System.Drawing.Color)("Tube4", System.Drawing.Color.Cyan)
            Case CellType.Tube5
                Return New Tuple(Of String, System.Drawing.Color)("Tube5", System.Drawing.Color.Cyan)
            Case Else
                Return New Tuple(Of String, System.Drawing.Color)(cellType.ToString().Substring(0, Math.Min(5, cellType.ToString().Length)), System.Drawing.Color.White)
        End Select
    End Function

End Class
