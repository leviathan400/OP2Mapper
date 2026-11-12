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

' fMain.MapAnalysis.vb
' Partial class of fMain (split out of fMain.vb).
' Map analysis tools - detection and reporting of enclosed (unreachable) areas.
Partial Public Class fMain

#Region "Map Analysis — Enclosed Areas"

    ''' <summary>
    ''' Cell types that count as "passable" for connectivity analysis — i.e.
    ''' the types a unit can move across. In OP2, tubes are passable
    ''' (vehicles can drive over them). Walls, cliffs, vents, and the
    ''' pad/impassable types block movement.
    ''' </summary>
    Private Function IsPassableCellType(c As CellType) As Boolean
        Select Case c
            Case CellType.FastPassible1, CellType.FastPassible2,
                 CellType.MediumPassible1, CellType.MediumPassible2,
                 CellType.SlowPassible1, CellType.SlowPassible2,
                 CellType.DozedArea, CellType.Rubble,
                 CellType.Tube0, CellType.Tube1, CellType.Tube2,
                 CellType.Tube3, CellType.Tube4, CellType.Tube5
                Return True
            Case Else
                Return False
        End Select
    End Function

    ''' <summary>
    ''' Returns every connected-component of passable tiles in the map.
    ''' Uses 8-connected neighborhood (so a region is only flagged as
    ''' "separate" if it's truly sealed off in all eight directions).
    '''
    ''' Only the full 512×256 "world" map size wraps horizontally in OP2 —
    ''' all smaller maps have hard edges on all four sides. The bottom row
    ''' is always treated as impassable by convention.
    ''' </summary>
    Private Function FindConnectedPassableComponents() As List(Of List(Of Point))
        Dim components As New List(Of List(Of Point))
        If currentMap Is Nothing Then Return components

        Dim w As Integer = currentMap.WidthInTiles()
        Dim h As Integer = currentMap.HeightInTiles()
        Dim maxY As Integer = h - 2  ' skip the special impassable bottom row

        ' Only the full-size world map (512×256) wraps left-to-right; every
        ' other map size has hard edges on all 4 sides.
        Dim wrapX As Boolean = (w = 512 AndAlso h = 256)

        Dim visited(w - 1, h - 1) As Boolean

        For y As Integer = 0 To maxY
            For x As Integer = 0 To w - 1
                If visited(x, y) Then Continue For
                visited(x, y) = True
                If Not IsPassableCellType(currentMap.GetCellType(x, y)) Then Continue For

                ' BFS from (x, y) collecting every connected passable tile.
                Dim component As New List(Of Point)
                Dim queue As New Queue(Of Point)
                queue.Enqueue(New Point(x, y))

                While queue.Count > 0
                    Dim p As Point = queue.Dequeue()
                    component.Add(p)

                    For dx As Integer = -1 To 1
                        For dy As Integer = -1 To 1
                            If dx = 0 AndAlso dy = 0 Then Continue For

                            Dim nx As Integer = p.X + dx
                            Dim ny As Integer = p.Y + dy

                            If wrapX Then
                                ' Toroidal X (only on 512×256 world maps).
                                nx = ((nx Mod w) + w) Mod w
                            Else
                                ' Hard edges left/right.
                                If nx < 0 OrElse nx >= w Then Continue For
                            End If

                            ' Y never wraps and is clamped to the playable rows.
                            If ny < 0 OrElse ny > maxY Then Continue For
                            If visited(nx, ny) Then Continue For
                            visited(nx, ny) = True
                            If IsPassableCellType(currentMap.GetCellType(nx, ny)) Then
                                queue.Enqueue(New Point(nx, ny))
                            End If
                        Next
                    Next
                End While

                components.Add(component)
            Next
        Next

        Return components
    End Function

    ''' <summary>
    ''' Menu handler for "Edit → Find &amp; Fix Enclosed Areas...". Detects
    ''' passable regions disconnected from the main play area and offers to
    ''' convert them to Impassable 1. The whole operation is a single undo
    ''' step via ChangeCellTypesBulkCommand.
    ''' </summary>
    Private Sub FindEnclosedAreas_Click(sender As Object, e As EventArgs)
        If currentMap Is Nothing Then
            MessageBox.Show("No map loaded.", "Find Enclosed Areas",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim components As List(Of List(Of Point)) = FindConnectedPassableComponents()

        If components.Count = 0 Then
            MessageBox.Show("No passable tiles found in this map.",
                            "Find Enclosed Areas",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If components.Count = 1 Then
            MessageBox.Show("No enclosed areas found." & vbCrLf & vbCrLf &
                            $"The map has a single connected passable region ({components(0).Count} tiles).",
                            "Find Enclosed Areas",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Sort components largest-first. The largest is taken as the main
        ' play area; everything else is considered enclosed.
        components.Sort(Function(a, b) b.Count.CompareTo(a.Count))
        Dim mainArea As List(Of Point) = components(0)
        Dim enclosed As New List(Of List(Of Point))
        For i As Integer = 1 To components.Count - 1
            enclosed.Add(components(i))
        Next

        Dim totalEnclosedTiles As Integer = 0
        For Each comp In enclosed
            totalEnclosedTiles += comp.Count
        Next

        Dim sizesList As String = String.Join(", ",
            enclosed.Select(Function(c) c.Count.ToString() & " tiles"))

        ' Target cell type for the fix. Was temporarily LavaWall during
        ' detection-algorithm verification (made changes visually distinct
        ' from pre-existing I1 tiles); restored to Impassible1 now that the
        ' algorithm is trusted.
        Const fixCellType As CellType = CellType.Impassible1
        Const fixCellLabel As String = "Impassable 1 (I1)"

        Dim prompt As String =
            $"Found {enclosed.Count} enclosed area(s) — sizes: {sizesList}." & vbCrLf & vbCrLf &
            $"Main play area: {mainArea.Count} tiles." & vbCrLf & vbCrLf &
            $"Convert all {totalEnclosedTiles} enclosed tile(s) to {fixCellLabel}?" & vbCrLf & vbCrLf &
            "Tiles already at the target cell type are not changed." & vbCrLf &
            "This action can be undone with Ctrl+Z."

        If MessageBox.Show(prompt, "Find & Fix Enclosed Areas",
                           MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then
            Return
        End If

        ' Build the change list. Only collect tiles whose current cell type
        ' would actually change — saves a tiny bit of undo memory and means
        ' Undo restores the prior types accurately.
        Dim changes As New List(Of CellTypeChange)
        For Each comp In enclosed
            For Each tile In comp
                Dim oldType As CellType = currentMap.GetCellType(tile.X, tile.Y)
                If oldType <> fixCellType Then
                    changes.Add(New CellTypeChange With {
                        .X = tile.X,
                        .Y = tile.Y,
                        .OldType = oldType,
                        .NewType = fixCellType
                    })
                End If
            Next
        Next

        If changes.Count = 0 Then
            MessageBox.Show($"All enclosed tiles are already {fixCellLabel} — nothing to change.",
                            "Find & Fix Enclosed Areas",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim cmd As New ChangeCellTypesBulkCommand(
            currentMap, changes,
            $"Fix {enclosed.Count} enclosed area(s) ({changes.Count} tiles)")
        ExecuteCommand(cmd)

        ' Multi-tile change — full repaint and mini-map refresh.
        pnlMap.Invalidate()
        ScheduleMiniMapRefresh()

        Dim summary As String =
            "Enclosed area fix complete." & vbCrLf & vbCrLf &
            $"Areas edited: {enclosed.Count}" & vbCrLf &
            $"Tiles changed: {changes.Count}"
        MessageBox.Show(summary, "Find & Fix Enclosed Areas",
                        MessageBoxButtons.OK, MessageBoxIcon.Information)

        ' Once the fix has run there's nothing left to highlight. Turn the
        ' overlay off (unticks the menu item) so the user has a clear visual
        ' signal that the operation completed.
        ClearEnclosedHighlight()
    End Sub

    ''' <summary>
    ''' Toggle handler for "Edit → Highlight Enclosed Areas". Snapshots the
    ''' current enclosed-area tiles into <see cref="_highlightedEnclosedTiles"/>
    ''' and triggers a repaint. MapPanel_Paint draws a semi-transparent
    ''' magenta fill over each listed tile while the list is non-Nothing.
    ''' </summary>
    Private Sub HighlightEnclosedAreas_CheckedChanged(sender As Object, e As EventArgs)
        Dim item As ToolStripMenuItem = TryCast(sender, ToolStripMenuItem)
        If item Is Nothing Then Return

        If item.Checked Then
            If currentMap Is Nothing Then
                ' Nothing to highlight — bail and untick.
                item.Checked = False
                Return
            End If

            Dim enclosedCount As Integer = RecomputeEnclosedHighlight()
            If enclosedCount = 0 Then
                ' Map has no inaccessible passable regions — don't leave a
                ' checked-but-invisible toggle. Setting Checked=False fires
                ' this handler recursively (Else branch below) which clears
                ' the list and invalidates, so we can just show the message
                ' and bail.
                item.Checked = False
                MessageBox.Show("No enclosed areas found to highlight.",
                                "Highlight Enclosed Areas",
                                MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
        Else
            _highlightedEnclosedTiles = Nothing
        End If

        pnlMap.Invalidate()
    End Sub

    ''' <summary>
    ''' Drops the highlight overlay and unticks the menu item. Called when
    ''' the map being analysed is about to change (load / close) — the tile
    ''' coordinates in the cached list belong to the OLD map and would be
    ''' meaningless / confusing on the new one.
    ''' </summary>
    Private Sub ClearEnclosedHighlight()
        _highlightedEnclosedTiles = Nothing
        If mnuHighlightEnclosed IsNot Nothing AndAlso mnuHighlightEnclosed.Checked Then
            mnuHighlightEnclosed.Checked = False ' fires CheckedChanged → also invalidates
        ElseIf pnlMap IsNot Nothing Then
            pnlMap.Invalidate()
        End If
    End Sub

    ''' <summary>
    ''' Rebuilds the enclosed-tile list used by the highlight overlay.
    ''' Returns the number of enclosed areas found (i.e. passable components
    ''' beyond the largest one). Callers use the count to decide whether to
    ''' keep the overlay on or message the user.
    ''' </summary>
    Private Function RecomputeEnclosedHighlight() As Integer
        If currentMap Is Nothing Then
            _highlightedEnclosedTiles = Nothing
            Return 0
        End If

        Dim components As List(Of List(Of Point)) = FindConnectedPassableComponents()
        If components.Count <= 1 Then
            _highlightedEnclosedTiles = New List(Of Point)
            Return 0
        End If

        components.Sort(Function(a, b) b.Count.CompareTo(a.Count))
        Dim tiles As New List(Of Point)
        For i As Integer = 1 To components.Count - 1
            tiles.AddRange(components(i))
        Next
        _highlightedEnclosedTiles = tiles
        Return components.Count - 1
    End Function

#End Region

End Class
