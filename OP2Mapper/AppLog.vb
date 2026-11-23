' AppLog.vb
' A plain-text log, OP2Mapper.log next to the exe, for the things worth
' knowing about after the fact: start and close, crashes, update checks and
' the updater, OP2Launcher installs, settings changes, maps opened and saved.
' Not for every click - Debug.WriteLine is still used for that.
'
' Each line: "2026-11-18 23:41:07.123  [Start] message". The file is rolled
' over to OP2Mapper.old.log when it passes 1 MB, so it never grows without
' limit. Writing never throws: if the folder isn't writable (e.g. under
' Program Files) the log is simply skipped.
Imports System.IO
Imports System.Text

Public Module AppLog

    Private Const LogFileName As String = "OP2Mapper.log"
    Private Const OldLogFileName As String = "OP2Mapper.old.log"
    Private Const MaxLogBytes As Long = 1024 * 1024

    ' The update check runs on a background thread, so writes are serialised.
    Private ReadOnly writeLock As New Object()

    ''' <summary>Full path of the log file.</summary>
    Public ReadOnly Property LogFilePath As String
        Get
            Return Path.Combine(Application.StartupPath, LogFileName)
        End Get
    End Property

    ''' <summary>
    ''' Appends one line to the log. category is a short tag such as "Start",
    ''' "Update" or "Launcher"; message is free text (line breaks are flattened).
    ''' </summary>
    Public Sub Write(category As String, message As String)
        Dim line As String = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  [{category}] " &
                             If(message, "").Replace(vbCrLf, " | ").Replace(vbLf, " | ").Replace(vbCr, " | ")
        Debug.WriteLine("LOG " & line)
        SyncLock writeLock
            Try
                Dim logPath As String = LogFilePath
                Dim info As New FileInfo(logPath)
                If info.Exists AndAlso info.Length > MaxLogBytes Then
                    Dim oldPath As String = Path.Combine(Application.StartupPath, OldLogFileName)
                    If File.Exists(oldPath) Then File.Delete(oldPath)
                    File.Move(logPath, oldPath)
                End If
                File.AppendAllText(logPath, line & Environment.NewLine, Encoding.UTF8)
            Catch ex As Exception
                ' Logging must never break the app.
                Debug.WriteLine("AppLog: could not write the log: " & ex.Message)
            End Try
        End SyncLock
    End Sub

    ''' <summary>Logs an exception with its type and message (and where it came from).</summary>
    Public Sub WriteError(category As String, context As String, ex As Exception)
        If ex Is Nothing Then
            Write(category, context)
            Return
        End If
        Dim where As String = ""
        If ex.StackTrace IsNot Nothing Then
            ' The first frame is enough to find it; the full trace would bloat the log.
            where = " at " & ex.StackTrace.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)(0).Trim()
        End If
        Write(category, $"{context}: {ex.GetType().Name}: {ex.Message}{where}")
    End Sub

    ''' <summary>
    ''' The first line written at startup: version, build, process bitness,
    ''' Windows version and the command line.
    ''' </summary>
    Public Sub WriteStartup()
        Dim args() As String = Environment.GetCommandLineArgs()
        Dim argText As String = If(args.Length > 1, String.Join(" ", args, 1, args.Length - 1), "(none)")
        Write("Start", $"OP2Mapper {GlobalVariables.ApplicationVersion} build {GlobalVariables.ApplicationBuild}, " &
                       $"{If(Environment.Is64BitProcess, "64", "32")}-bit, {WindowsVersion()}, " &
                       $"folder {Application.StartupPath}, arguments: {argText}")
    End Sub

    ''' <summary>
    ''' The real Windows version, e.g. "Windows 10 IoT Enterprise LTSC 2021 21H2 (build 19044)".
    ''' Environment.OSVersion says 6.2.9200 for any app without a Windows 10
    ''' manifest entry, so the registry is used instead.
    ''' </summary>
    Private Function WindowsVersion() As String
        Try
            Using key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion")
                If key IsNot Nothing Then
                    Dim name As String = TryCast(key.GetValue("ProductName"), String)
                    Dim release As String = If(TryCast(key.GetValue("DisplayVersion"), String), TryCast(key.GetValue("ReleaseId"), String))
                    Dim build As String = TryCast(key.GetValue("CurrentBuild"), String)
                    If Not String.IsNullOrEmpty(name) Then
                        Return name & If(String.IsNullOrEmpty(release), "", " " & release) &
                               If(String.IsNullOrEmpty(build), "", $" (build {build})")
                    End If
                End If
            End Using
        Catch
            ' Fall back below.
        End Try
        Return Environment.OSVersion.VersionString
    End Function

End Module
