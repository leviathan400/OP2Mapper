' AppSettings.vb
' The three folder settings, stored in Mapper3.ini next to the program
' ([Paths] section) instead of My.Settings (user.config deep in the user
' profile). Keeping them in the INI makes a copy of OP2Mapper portable: its
' settings travel with the folder, and every copy has its own.
'
'   [Paths]
'   OP2Path=C:\Games\Outpost 2        Outpost 2 folder (tilesets, launcher)
'   WorkingPath=C:\Games\Outpost 2    default folder for opening and saving maps
'   Tilegroups=C:\...\Tilegroups      tile group library
'
' Older versions kept these in My.Settings. The first time a value is read and
' the INI doesn't have it yet, the My.Settings value is copied over, so
' upgrading keeps the user's folders.
Imports System.Collections.Generic

Public Module AppSettings

    Private Const PathsSection As String = "Paths"

    ''' <summary>Values already read or written this session (the INI is read once per key).</summary>
    Private ReadOnly cache As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>The Outpost 2 folder. Empty when not set yet.</summary>
    Public Property OP2Path As String
        Get
            Return GetPath("OP2Path", Function() My.Settings.OP2Path)
        End Get
        Set(value As String)
            SetPath("OP2Path", value)
        End Set
    End Property

    ''' <summary>Default folder for opening and saving maps. Empty when not set yet.</summary>
    Public Property WorkingPath As String
        Get
            Return GetPath("WorkingPath", Function() My.Settings.WorkingPath)
        End Get
        Set(value As String)
            SetPath("WorkingPath", value)
        End Set
    End Property

    ''' <summary>The tile group library folder. Empty when not set yet.</summary>
    Public Property Tilegroups As String
        Get
            Return GetPath("Tilegroups", Function() My.Settings.Tilegroups)
        End Get
        Set(value As String)
            SetPath("Tilegroups", value)
        End Set
    End Property

    Private Function GetPath(key As String, legacy As Func(Of String)) As String
        Dim value As String = Nothing
        If cache.TryGetValue(key, value) Then Return value

        ' A NUL default can't occur in the file, so it marks "key not in the INI".
        Const missing As String = vbNullChar
        value = ToolPaletteHelper.ReadIniSetting(PathsSection, key, missing)
        If value = missing Then
            ' Not in the INI yet: take the value an older version kept in My.Settings.
            value = ""
            Try
                value = If(legacy(), "")
            Catch ex As Exception
                Debug.WriteLine($"AppSettings: could not read the old {key} setting: {ex.Message}")
            End Try
            If value <> "" Then
                ToolPaletteHelper.WriteIniSetting(PathsSection, key, value)
                Debug.WriteLine($"AppSettings: moved {key} from My.Settings to Mapper3.ini")
                AppLog.Write("Settings", $"Moved {key} from the old user settings to Mapper3.ini: {value}")
            End If
        End If
        cache(key) = value
        Return value
    End Function

    Private Sub SetPath(key As String, value As String)
        value = If(value, "").Trim()
        Dim previous As String = Nothing
        If cache.TryGetValue(key, previous) AndAlso previous = value Then Return   ' unchanged
        AppLog.Write("Settings", $"{key} set to: {If(value = "", "(empty)", value)}")
        cache(key) = value
        ToolPaletteHelper.WriteIniSetting(PathsSection, key, value)
    End Sub

End Module
