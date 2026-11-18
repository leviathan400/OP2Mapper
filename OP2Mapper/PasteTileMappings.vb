Imports OP2UtilityDotNet.OP2Map

''' <summary>
''' Command for pasting a region of tiles
''' </summary>
Public Class PasteTileMappingsCommand
    Inherits Command

    Private ReadOnly map As Map
    Private ReadOnly startX As Integer
    Private ReadOnly startY As Integer
    Private ReadOnly newMappings(,) As Integer
    Private ReadOnly width As Integer
    Private ReadOnly height As Integer
    Private ReadOnly originalMappings(,) As Integer

    ''' <summary>
    ''' Cell types to paste, or Nothing to paste tiles only (the target keeps
    ''' its own cell types).
    ''' </summary>
    Private ReadOnly newCellTypes(,) As CellType
    ''' <summary>Cell types the region held before the paste (Nothing when newCellTypes is Nothing).</summary>
    Private ReadOnly originalCellTypes(,) As CellType

    ''' <summary>
    ''' Create a new paste command. Snapshots the current mappings (and cell
    ''' types, when pasting them) in the target region so Undo can restore them.
    ''' </summary>
    ''' <param name="newCellTypes">
    ''' Cell types to paste alongside the tiles, or Nothing for tiles only.
    ''' The map's bottom row is never given a pasted cell type: it isn't shown
    ''' in game and must stay Impassable, so it keeps whatever it already has.
    ''' </param>
    Public Sub New(map As Map, startX As Integer, startY As Integer,
                  newMappings(,) As Integer, width As Integer, height As Integer,
                  Optional newCellTypes(,) As CellType = Nothing)
        Me.map = map
        Me.startX = startX
        Me.startY = startY
        Me.newMappings = newMappings
        Me.width = width
        Me.height = height
        Me.newCellTypes = newCellTypes

        ' Store the original mappings (and cell types) for undo
        ReDim originalMappings(width - 1, height - 1)
        If newCellTypes IsNot Nothing Then ReDim originalCellTypes(width - 1, height - 1)
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                Dim mapX As Integer = startX + x
                Dim mapY As Integer = startY + y
                originalMappings(x, y) = map.GetTileMappingIndex(mapX, mapY)
                If newCellTypes IsNot Nothing Then
                    originalCellTypes(x, y) = map.GetCellType(mapX, mapY)
                End If
            Next
        Next
    End Sub

    ''' <summary>
    ''' True when this paste writes cell types as well as tiles.
    ''' </summary>
    Public ReadOnly Property IncludesCellTypes As Boolean
        Get
            Return newCellTypes IsNot Nothing
        End Get
    End Property

    ''' <summary>
    ''' Execute: writes the pasted mappings (and cell types) into the target region.
    ''' </summary>
    Public Overrides Sub Execute()
        Dim lockedRow As Integer = CInt(map.HeightInTiles()) - 1

        ' Apply the new mappings
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                Dim mapX As Integer = startX + x
                Dim mapY As Integer = startY + y
                map.SetTileMappingIndex(mapX, mapY, newMappings(x, y))
                If newCellTypes IsNot Nothing AndAlso mapY <> lockedRow Then
                    map.SetCellType(newCellTypes(x, y), mapX, mapY)
                End If
            Next
        Next
    End Sub

    ''' <summary>
    ''' Undo: restores the mappings (and cell types) the region held before the paste.
    ''' </summary>
    Public Overrides Sub Undo()
        ' Restore the original mappings
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                Dim mapX As Integer = startX + x
                Dim mapY As Integer = startY + y
                map.SetTileMappingIndex(mapX, mapY, originalMappings(x, y))
                If originalCellTypes IsNot Nothing Then
                    map.SetCellType(originalCellTypes(x, y), mapX, mapY)
                End If
            Next
        Next
    End Sub

    ''' <summary>
    ''' Description of the paste (region size, and whether cell types were
    ''' included) for undo/redo UI.
    ''' </summary>
    Public Overrides ReadOnly Property Description As String
        Get
            Return If(IncludesCellTypes,
                      $"Paste {width}x{height} Tiles + Cell Types",
                      $"Paste {width}x{height} Tiles")
        End Get
    End Property
End Class