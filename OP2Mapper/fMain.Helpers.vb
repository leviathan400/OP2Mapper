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

' fMain.Helpers.vb
' Partial class of fMain (split out of fMain.vb).
' Small shared helper methods used across the main form.
Partial Public Class fMain

#Region "Helper Methods"

    ''' <summary>
    ''' Safely finds a control by name with recursive search
    ''' </summary>
    Private Function FindControl(controlName As String) As Control
        Dim foundControls As Control() = Controls.Find(controlName, True)
        If foundControls IsNot Nothing AndAlso foundControls.Length > 0 Then
            Return foundControls(0)
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Provides diagnostic information about tileset loading
    ''' </summary>
    Private Sub DiagnoseTilesetLoading()
        Debug.WriteLine("=========== TILESET LOADING DIAGNOSTICS ===========")

        ' Check if OP2Path is set correctly
        Debug.WriteLine($"OP2Path: {My.Settings.OP2Path}")
        Debug.WriteLine($"OP2Path exists: {Directory.Exists(My.Settings.OP2Path)}")

        ' Check tileset directory paths
        Dim tilesetPath As String = Path.Combine(My.Settings.OP2Path, "OPU\base\tilesets")
        Debug.WriteLine($"Primary tileset path: {tilesetPath}")
        Debug.WriteLine($"Primary tileset path exists: {Directory.Exists(tilesetPath)}")

        ' List alternate paths
        Dim alternatePaths = New String() {
        Path.Combine(My.Settings.OP2Path, "base\tilesets"),
        Path.Combine(My.Settings.OP2Path, "tilesets")
    }

        For Each path In alternatePaths
            Debug.WriteLine($"Alternative path: {path}")
            Debug.WriteLine($"Alternative path exists: {Directory.Exists(path)}")
        Next

        ' List all map tilesets needed
        If currentMap IsNot Nothing Then
            Debug.WriteLine("Map requires these tilesets:")
            For Each tilesetSource In currentMap.tilesetSources
                If Not tilesetSource.IsEmpty() Then
                    Debug.WriteLine($"- {tilesetSource.tilesetFilename}")
                End If
            Next
        End If

        ' List loaded tilesets
        If mapRenderer IsNot Nothing Then
            Debug.WriteLine("Loaded tilesets:")
            Dim tilesets = mapRenderer.ListLoadedTilesets()
            For Each kvp In tilesets
                Debug.WriteLine($"- {kvp.Key}: {kvp.Value} tiles")
            Next
        End If

        Debug.WriteLine("=========== END DIAGNOSTICS ===========")
    End Sub

    ''' <summary>
    ''' Synchronizes render mode between GlobalVariables and MapRenderer
    ''' </summary>
    Private Property CurrentRenderMode As RenderMode
        Get
            Return GlobalVariables.CurrentRenderMode
        End Get
        Set(value As RenderMode)
            ' Update global variable
            GlobalVariables.CurrentRenderMode = value

            ' Synchronize with MapRenderer if available
            If mapRenderer IsNot Nothing Then
                mapRenderer.RenderMode = CType(value, MapRenderer.MapRenderMode)
            End If
        End Set
    End Property

    ''' <summary>
    ''' Enable or disable edit mode for the application
    ''' </summary>
    Private Property EditModeEnabled As Boolean
        Get
            Return GlobalVariables.EditModeEnabled
        End Get
        Set(value As Boolean)
            GlobalVariables.EditModeEnabled = value
        End Set
    End Property


#End Region

End Class
