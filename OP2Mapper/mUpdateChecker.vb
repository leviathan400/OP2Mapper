Imports System.Net

' mUpdateChecker.vb
' Reusable update checker module
' Drop this into any project and call CheckForUpdates() on startup
'
' Setup:
'   1. Add this module to your project
'   2. Set the constants below for your app
'   3. Create a version file on your server (e.g. myapp.htm) containing just the version number like "0031"
'   4. Call mUpdateChecker.CheckForUpdates() on form load
'   5. Ensure updater.exe is in the same folder as your app

Module mUpdateChecker

    ' ===== CONFIGURE THESE FOR EACH PROJECT =====
    Private Const ExeFileName As String = "OP2Mapper.exe"                                   ' Your app's exe name
    Private Const VersionURL As String = "https://update.outpostuniverse.org/OP2Mapper.htm" ' URL to version file
    ' ============================================

    ''' <summary>
    ''' Checks for updates and returns True if the app should continue loading, False if updating/exiting
    ''' </summary>
    Public Function CheckForUpdates(Optional autoUpdate As Boolean = True, Optional showPrompt As Boolean = False) As Boolean
        Dim serverVersion As String = GetServerVersion()

        ' If we couldn't reach the server, just continue
        If serverVersion = "ERROR" Then
            Return True
        End If

        ' The server must return a plain numeric build number (e.g. "0131").
        ' Anything else (HTML error page, captive portal, CDN error body) is
        ' not a version — treat it like an unreachable server, never as "newer".
        Dim serverBuild As Integer
        Dim localBuild As Integer
        If Not Integer.TryParse(serverVersion, serverBuild) OrElse
           Not Integer.TryParse(GlobalVariables.ApplicationBuild, localBuild) Then
            Debug.WriteLine("Update check: server response is not a build number - ignoring: " & serverVersion)
            Return True
        End If

        ' Compare versions - if server version is newer
        If serverBuild > localBuild Then
            ' Newer version available
            If autoUpdate Then
                LaunchUpdater()
                Return False ' App should exit
            ElseIf showPrompt Then
                Dim result = MessageBox.Show(
                    $"A new version ({serverVersion}) is available. You have version {GlobalVariables.ApplicationBuild}." & vbCrLf & vbCrLf & "Would you like to update now?",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question)

                If result = DialogResult.Yes Then
                    LaunchUpdater()
                    Return False ' App should exit
                End If
            End If
        End If

        Return True ' No update needed, continue loading
    End Function

    ''' <summary>
    ''' Gets the version string from the server
    ''' </summary>
    Private Function GetServerVersion() As String
        Try
            ' 5-second timeout — default WebClient timeout is ~100s, which would
            ' stall startup if the version-check server is slow or unreachable.
            Using client As New TimedWebClient With {.TimeoutMs = 5000}
                Dim response As String = client.DownloadString(VersionURL)
                Debug.WriteLine("*** Server Version: " & response.Trim())
                Return response.Trim()
            End Using
        Catch ex As Exception
            Return "ERROR"
        End Try
    End Function

    ' WebClient with a configurable request timeout. (WebClient itself doesn't
    ' expose Timeout — it has to be set on the underlying WebRequest.)
    Private Class TimedWebClient
        Inherits WebClient

        ''' <summary>
        ''' Request timeout in milliseconds (defaults to WebClient's usual ~100s)
        ''' </summary>
        Public Property TimeoutMs As Integer = 100000

        ''' <summary>
        ''' Applies TimeoutMs to the underlying WebRequest
        ''' </summary>
        Protected Overrides Function GetWebRequest(uri As Uri) As WebRequest
            Dim req As WebRequest = MyBase.GetWebRequest(uri)
            If req IsNot Nothing Then req.Timeout = TimeoutMs
            Return req
        End Function
    End Class

    ''' <summary>
    ''' Launches the updater and signals the app to exit
    ''' </summary>
    Private Sub LaunchUpdater()
        Dim updaterPath As String = IO.Path.Combine(Application.StartupPath, "updater.exe")

        ' Check up-front so the user gets a clear "missing file" message instead
        ' of a generic Win32Exception buried in the Catch below.
        If Not IO.File.Exists(updaterPath) Then
            MessageBox.Show(
                "Cannot install update: updater.exe was not found next to the application." & vbCrLf & vbCrLf &
                "Expected at: " & updaterPath,
                "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Try
            Dim startInfo As New ProcessStartInfo() With {
                .FileName = updaterPath,
                .Arguments = ExeFileName
            }
            Process.Start(startInfo)
        Catch ex As Exception
            MessageBox.Show("Failed to launch updater: " & ex.Message, "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Returns the current build version
    ''' </summary>
    Public Function GetCurrentVersion() As String
        Return GlobalVariables.ApplicationBuild
    End Function

End Module