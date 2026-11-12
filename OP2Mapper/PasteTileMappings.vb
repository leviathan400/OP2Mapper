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
    ''' Create a new paste command. Snapshots the current mappings in the target
    ''' region so Undo can restore them.
    ''' </summary>
    Public Sub New(map As Map, startX As Integer, startY As Integer,
                  newMappings(,) As Integer, width As Integer, height As Integer)
        Me.map = map
        Me.startX = startX
        Me.startY = startY
        Me.newMappings = newMappings
        Me.width = width
        Me.height = height

        ' Store the original mappings for undo
        ReDim originalMappings(width - 1, height - 1)
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                Dim mapX As Integer = startX + x
                Dim mapY As Integer = startY + y
                originalMappings(x, y) = map.GetTileMappingIndex(mapX, mapY)
            Next
        Next
    End Sub

    ''' <summary>
    ''' Execute: writes the pasted mappings into the target region.
    ''' </summary>
    Public Overrides Sub Execute()
        ' Apply the new mappings
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                Dim mapX As Integer = startX + x
                Dim mapY As Integer = startY + y
                map.SetTileMappingIndex(mapX, mapY, newMappings(x, y))
            Next
        Next
    End Sub

    ''' <summary>
    ''' Undo: restores the mappings the region held before the paste.
    ''' </summary>
    Public Overrides Sub Undo()
        ' Restore the original mappings
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                Dim mapX As Integer = startX + x
                Dim mapY As Integer = startY + y
                map.SetTileMappingIndex(mapX, mapY, originalMappings(x, y))
            Next
        Next
    End Sub

    ''' <summary>
    ''' Description of the paste (region size) for undo/redo UI.
    ''' </summary>
    Public Overrides ReadOnly Property Description As String
        Get
            Return $"Paste {width}x{height} Tiles"
        End Get
    End Property
End Class