Imports OP2UtilityDotNet.OP2Map
Imports System.Drawing


''' <summary>
''' Represents tile information that can be shared across multiple mapping tools
''' </summary>
Public Class TileData
    ' Core properties

    ''' <summary>
    ''' The tile's cell type (passability/terrain classification).
    ''' </summary>
    Public Property CellType As CellType
    ''' <summary>
    ''' Index into the map's tile mapping table for this tile.
    ''' </summary>
    Public Property TileMappingIndex As UInteger
    ''' <summary>
    ''' Whether lava can flow onto this tile.
    ''' </summary>
    Public Property LavaPossible As Boolean
    ''' <summary>
    ''' The tile's (x, y) position on the map, in tile coordinates.
    ''' </summary>
    Public Property Coordinates As Point
    ''' <summary>
    ''' Index of the tileset this tile's image comes from.
    ''' </summary>
    Public Property TilesetIndex As Integer
    ''' <summary>
    ''' Index of the image within the tileset.
    ''' </summary>
    Public Property ImageIndex As Integer

    ' Constructors

    ''' <summary>
    ''' Creates an empty TileData with default values.
    ''' </summary>
    Public Sub New()
        ' Default constructor
    End Sub

    ''' <summary>
    ''' Creates a TileData at the given tile coordinates.
    ''' </summary>
    Public Sub New(x As Integer, y As Integer)
        Me.Coordinates = New Point(x, y)
    End Sub

    ' Static factory methods

    ''' <summary>
    ''' Creates a TileData object from map coordinates
    ''' </summary>
    Public Shared Function FromMap(map As Map, x As Integer, y As Integer) As TileData
        If map Is Nothing Then Return Nothing

        Dim tile As New TileData(x, y)
        tile.CellType = map.GetCellType(x, y)
        tile.TileMappingIndex = CUInt(map.GetTileMappingIndex(x, y))
        tile.LavaPossible = map.GetLavaPossible(x, y)
        tile.TilesetIndex = map.GetTilesetIndex(x, y)
        tile.ImageIndex = map.GetImageIndex(x, y)

        Return tile
    End Function

    ''' <summary>
    ''' Applies the properties of this TileData to a map at the specified coordinates
    ''' </summary>
    Public Sub ApplyToMap(map As Map, Optional x As Integer = -1, Optional y As Integer = -1)
        If map Is Nothing Then Return

        ' Use provided coordinates or default to the stored coordinates
        Dim targetX As Integer = If(x >= 0, x, Coordinates.X)
        Dim targetY As Integer = If(y >= 0, y, Coordinates.Y)

        ' Apply properties to the map
        map.SetCellType(CellType, targetX, targetY)
        map.SetTileMappingIndex(targetX, targetY, TileMappingIndex)
        map.SetLavaPossible(LavaPossible, targetX, targetY)
    End Sub

    ''' <summary>
    ''' Creates a clone of this TileData object
    ''' </summary>
    Public Function Clone() As TileData
        Dim cloneTile As New TileData(Coordinates.X, Coordinates.Y)
        cloneTile.CellType = CellType
        cloneTile.TileMappingIndex = TileMappingIndex
        cloneTile.LavaPossible = LavaPossible
        cloneTile.TilesetIndex = TilesetIndex
        cloneTile.ImageIndex = ImageIndex
        Return cloneTile
    End Function

    ''' <summary>
    ''' Returns a string representation of this tile data
    ''' </summary>
    Public Overrides Function ToString() As String
        Return $"Tile({Coordinates.X},{Coordinates.Y}) - Type:{CellType}, Image:{TilesetIndex}/{ImageIndex}"
    End Function

End Class