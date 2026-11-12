Imports OP2UtilityDotNet.OP2Map

' Abstract Command class for the undo/redo system
'
' Contract: Execute() applies the change to the map; Undo() restores the exact
' prior state. Both must be safely re-runnable, since redo simply calls
' Execute() again.
Public MustInherit Class Command
    ''' <summary>
    ''' Applies the change to the map. Called for the initial action and again on redo.
    ''' </summary>
    Public MustOverride Sub Execute()
    ''' <summary>
    ''' Reverts the change, restoring the state captured before Execute.
    ''' </summary>
    Public MustOverride Sub Undo()
    ''' <summary>
    ''' Human-readable description for undo/redo UI (menus, history list).
    ''' </summary>
    Public MustOverride ReadOnly Property Description As String
End Class

' Command for changing a tile's cell type
Public Class ChangeCellTypeCommand
    Inherits Command

    Private ReadOnly map As Map
    Private ReadOnly _x As Integer
    Private ReadOnly _y As Integer
    Private ReadOnly oldCellType As CellType
    Private ReadOnly newCellType As CellType

    ''' <summary>
    ''' Captures both the old and new cell type for one tile; the caller supplies the old value.
    ''' </summary>
    Public Sub New(map As Map, x As Integer, y As Integer, oldCellType As CellType, newCellType As CellType)
        Me.map = map
        Me._x = x
        Me._y = y
        Me.oldCellType = oldCellType
        Me.newCellType = newCellType
    End Sub

    ' Exposed so callers (e.g. fMain's Undo/Redo) can refresh the affected tile
    ' without reaching in via reflection.
    ''' <summary>
    ''' X tile coordinate of the affected tile.
    ''' </summary>
    Public ReadOnly Property X As Integer
        Get
            Return _x
        End Get
    End Property

    ''' <summary>
    ''' Y tile coordinate of the affected tile.
    ''' </summary>
    Public ReadOnly Property Y As Integer
        Get
            Return _y
        End Get
    End Property

    ''' <summary>
    ''' Execute: sets the tile's cell type to the new value.
    ''' </summary>
    Public Overrides Sub Execute()
        map.SetCellType(newCellType, _x, _y)
    End Sub

    ''' <summary>
    ''' Undo: restores the tile's previous cell type.
    ''' </summary>
    Public Overrides Sub Undo()
        map.SetCellType(oldCellType, _x, _y)
    End Sub

    ''' <summary>
    ''' Description of the change for undo/redo UI.
    ''' </summary>
    Public Overrides ReadOnly Property Description As String
        Get
            Return $"Change cell type at ({_x},{_y}) from {oldCellType} to {newCellType}"
        End Get
    End Property
End Class

' Command for changing a tile's mapping
Public Class ChangeTileMappingCommand
    Inherits Command

    Private ReadOnly map As Map
    Private ReadOnly _x As Integer
    Private ReadOnly _y As Integer
    Private ReadOnly oldMappingIndex As Integer
    Private ReadOnly newMappingIndex As Integer

    ''' <summary>
    ''' Captures both the old and new mapping index for one tile; the caller supplies the old value.
    ''' </summary>
    Public Sub New(map As Map, x As Integer, y As Integer, oldMappingIndex As Integer, newMappingIndex As Integer)
        Me.map = map
        Me._x = x
        Me._y = y
        Me.oldMappingIndex = oldMappingIndex
        Me.newMappingIndex = newMappingIndex
    End Sub

    ''' <summary>
    ''' X tile coordinate of the affected tile.
    ''' </summary>
    Public ReadOnly Property X As Integer
        Get
            Return _x
        End Get
    End Property

    ''' <summary>
    ''' Y tile coordinate of the affected tile.
    ''' </summary>
    Public ReadOnly Property Y As Integer
        Get
            Return _y
        End Get
    End Property

    ''' <summary>
    ''' Execute: sets the tile's mapping index to the new value.
    ''' </summary>
    Public Overrides Sub Execute()
        map.SetTileMappingIndex(_x, _y, newMappingIndex)
    End Sub

    ''' <summary>
    ''' Undo: restores the tile's previous mapping index.
    ''' </summary>
    Public Overrides Sub Undo()
        map.SetTileMappingIndex(_x, _y, oldMappingIndex)
    End Sub

    ''' <summary>
    ''' Description of the change for undo/redo UI.
    ''' </summary>
    Public Overrides ReadOnly Property Description As String
        Get
            Return $"Change tile at ({_x},{_y}) from mapping {oldMappingIndex} to {newMappingIndex}"
        End Get
    End Property
End Class

''' <summary>
''' Command for placing a tile group with undo/redo support
''' </summary>
Public Class PlaceTileGroupCommand
    Inherits Command

    Private ReadOnly map As Map
    Private ReadOnly startX As Integer
    Private ReadOnly startY As Integer
    Private ReadOnly newMappings As Integer(,)
    Private ReadOnly oldMappings As Integer(,)
    Private ReadOnly width As Integer
    Private ReadOnly height As Integer
    Private ReadOnly tileGroupName As String

    ''' <summary>
    ''' Copies the new mappings and snapshots the current map mappings in the
    ''' target region so Undo can restore them.
    ''' </summary>
    Public Sub New(map As Map, startX As Integer, startY As Integer,
                   tileMappings As Integer(,), width As Integer, height As Integer,
                   Optional groupName As String = "")
        Me.map = map
        Me.startX = startX
        Me.startY = startY
        Me.width = width
        Me.height = height
        Me.tileGroupName = If(String.IsNullOrEmpty(groupName), "", groupName)

        ' Store the new mappings
        Me.newMappings = New Integer(width - 1, height - 1) {}
        Array.Copy(tileMappings, Me.newMappings, tileMappings.Length)

        ' Store the old mappings for undo
        Me.oldMappings = New Integer(width - 1, height - 1) {}
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                Me.oldMappings(x, y) = map.GetTileMappingIndex(startX + x, startY + y)
            Next
        Next
    End Sub

    ''' <summary>
    ''' Description of the placement (group name and size) for undo/redo UI.
    ''' </summary>
    Public Overrides ReadOnly Property Description As String
        Get
            If String.IsNullOrEmpty(tileGroupName) Then
                Return $"Place Tile Group ({width}×{height} tiles)"
            Else
                Return $"Place Tile Group '{tileGroupName}' ({width}×{height} tiles)"
            End If
        End Get
    End Property

    ''' <summary>
    ''' Execute: writes the tile group's mappings into the target region.
    ''' </summary>
    Public Overrides Sub Execute()
        ' Apply the new tile mappings
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                map.SetTileMappingIndex(startX + x, startY + y, newMappings(x, y))
            Next
        Next
    End Sub

    ''' <summary>
    ''' Undo: restores the mappings the region held before placement.
    ''' </summary>
    Public Overrides Sub Undo()
        ' Restore the old tile mappings
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                map.SetTileMappingIndex(startX + x, startY + y, oldMappings(x, y))
            Next
        Next
    End Sub
End Class

''' <summary>
''' One cell-type change in a bulk operation.
''' </summary>
Public Structure CellTypeChange
    Public X As Integer
    Public Y As Integer
    Public OldType As CellType
    Public NewType As CellType
End Structure

''' <summary>
''' Command that applies many cell-type changes at once as a single undo step.
''' Used by "Find &amp; Fix Enclosed Areas" and similar bulk operations where a
''' tile-by-tile undo stack would be unusable.
''' </summary>
Public Class ChangeCellTypesBulkCommand
    Inherits Command

    Private ReadOnly map As Map
    Private ReadOnly changes As List(Of CellTypeChange)
    Private ReadOnly _description As String

    ''' <summary>
    ''' Takes the full change list (each entry carries its own old/new types) and a UI description.
    ''' </summary>
    Public Sub New(map As Map, changes As List(Of CellTypeChange), description As String)
        Me.map = map
        Me.changes = changes
        Me._description = description
    End Sub

    ''' <summary>
    ''' Execute: applies every change's new cell type.
    ''' </summary>
    Public Overrides Sub Execute()
        For Each c In changes
            map.SetCellType(c.NewType, c.X, c.Y)
        Next
    End Sub

    ''' <summary>
    ''' Undo: restores every change's old cell type.
    ''' </summary>
    Public Overrides Sub Undo()
        For Each c In changes
            map.SetCellType(c.OldType, c.X, c.Y)
        Next
    End Sub

    ''' <summary>
    ''' Description supplied by the bulk operation for undo/redo UI.
    ''' </summary>
    Public Overrides ReadOnly Property Description As String
        Get
            Return _description
        End Get
    End Property
End Class
