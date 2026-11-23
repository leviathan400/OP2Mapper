Imports System.IO

' OP2Launcher
' Launches Outpost 2 via OPULauncher.exe in <OP2Path>\OPU\, or via
' op2launcher.exe (bundled with OP2Mapper and installed into the OPU folder on
' first use, see EnsureOp2LauncherInstalled) to boot straight into a mission
' DLL or the in-game DLL picker.

Module OP2Launcher

    ''' <summary>
    ''' Starts Outpost 2 via OPULauncher.exe under the configured OP2 path.
    ''' Returns True if the launch was successfully started, False if
    ''' something prevented it (missing path, missing launcher exe, OS
    ''' refusal). Failure cases show the user a MessageBox — callers
    ''' don't need to handle errors themselves.
    ''' </summary>
    Public Function LaunchOutpost2() As Boolean
        ' Verify the configured OP2 directory exists.
        If String.IsNullOrEmpty(AppSettings.OP2Path) OrElse
           Not Directory.Exists(AppSettings.OP2Path) Then
            MessageBox.Show(
                "Outpost 2 game directory is not set or doesn't exist." & vbCrLf & vbCrLf &
                "Configure it in File → Settings.",
                "Launch Outpost 2",
                MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        Dim launcherPath As String = Path.Combine(AppSettings.OP2Path, "OPU", "OPULauncher.exe")

        If Not File.Exists(launcherPath) Then
            MessageBox.Show(
                "OPULauncher.exe was not found at:" & vbCrLf & launcherPath & vbCrLf & vbCrLf &
                "Check that the Outpost 2 directory in Settings points to an OPU install.",
                "Launch Outpost 2",
                MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End If

        Try
            ' Run OPULauncher from its own folder — many launchers assume
            ' the working directory is where their .exe lives.
            Dim startInfo As New ProcessStartInfo() With {
                .FileName = launcherPath,
                .WorkingDirectory = Path.GetDirectoryName(launcherPath)
            }
            Process.Start(startInfo)
            AppLog.Write("Launch", "Started Outpost 2: " & launcherPath)
            Return True
        Catch ex As Exception
            AppLog.WriteError("Launch", "Could not start Outpost 2", ex)
            MessageBox.Show($"Failed to launch Outpost 2: {ex.Message}",
                            "Launch Outpost 2",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Launches Outpost 2 straight into a mission DLL via op2launcher.exe in
    ''' the OPU folder. op2launcher arms the in-game Launcher add-on (one-shot)
    ''' and boots the game directly into the given DLL — no menus. Used for
    ''' "Test in OP2": run a map through an editor mission DLL.
    '''
    ''' missionDll is the DLL name as it lives in the OPU folder (e.g.
    ''' "cEditorMission.dll"), not a full path. Returns True if the launch was
    ''' started; failure cases show the user a MessageBox.
    ''' </summary>
    Public Function LaunchMissionDll(missionDll As String) As Boolean
        If String.IsNullOrWhiteSpace(missionDll) Then
            MessageBox.Show("No mission DLL was specified.",
                            "Launch Mission",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        ' Pass the DLL name quoted, in case it ever contains spaces.
        Return RunOp2Launcher("""" & missionDll & """", "Launch Mission")
    End Function

    ''' <summary>
    ''' Launches Outpost 2 via op2launcher.exe --picker, which boots the game
    ''' into the in-game "Launch a DLL" menu so the user can pick a mission
    ''' DLL there. Returns True if the launch was started; failure cases show
    ''' the user a MessageBox.
    ''' </summary>
    Public Function LaunchDllPicker() As Boolean
        Return RunOp2Launcher("--picker", "Launch DLL Picker")
    End Function

    ''' <summary>
    ''' Shared helper: runs op2launcher.exe from the OPU folder with the given
    ''' arguments. Validates the OP2 path and the presence of op2launcher.exe,
    ''' showing a MessageBox (titled dialogTitle) on any failure. Returns True
    ''' if the process was started.
    ''' </summary>
    Private Function RunOp2Launcher(arguments As String, dialogTitle As String) As Boolean
        ' Verify the configured OP2 directory exists.
        If String.IsNullOrEmpty(AppSettings.OP2Path) OrElse
           Not Directory.Exists(AppSettings.OP2Path) Then
            MessageBox.Show(
                "Outpost 2 game directory is not set or doesn't exist." & vbCrLf & vbCrLf &
                "Configure it in File → Settings.",
                dialogTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        Dim opuFolder As String = Path.Combine(AppSettings.OP2Path, "OPU")
        Dim launcherPath As String = Path.Combine(opuFolder, "op2launcher.exe")

        ' Install (or update) the bundled op2launcher first if needed.
        If Not EnsureOp2LauncherInstalled(dialogTitle) Then Return False

        If Not File.Exists(launcherPath) Then
            MessageBox.Show(
                "op2launcher.exe was not found at:" & vbCrLf & launcherPath,
                dialogTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End If

        Try
            ' op2launcher must run from the OPU folder — it resolves the mission
            ' DLL and outpost2.ini relative to its own location.
            Dim startInfo As New ProcessStartInfo() With {
                .FileName = launcherPath,
                .Arguments = arguments,
                .WorkingDirectory = opuFolder
            }
            Process.Start(startInfo)
            AppLog.Write("Launch", $"Started op2launcher.exe {arguments}")
            Return True
        Catch ex As Exception
            AppLog.WriteError("Launch", "Could not start op2launcher.exe " & arguments, ex)
            MessageBox.Show($"Failed to launch: {ex.Message}",
                            dialogTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

    ''' <summary>Files of the bundled OP2Launcher: (file name in the OPU folder, embedded resource name).</summary>
    Private ReadOnly BundledLauncherFiles As String(,) = {
        {"op2launcher.exe", "OP2Launcher.op2launcher.exe"},
        {"Launcher.dll", "OP2Launcher.Launcher.dll"}
    }

    ''' <summary>
    ''' Makes sure op2launcher.exe and Launcher.dll are in &lt;OP2Path&gt;\OPU,
    ''' installing the copies bundled with OP2Mapper (embedded resources) when
    ''' needed. Called before anything that runs op2launcher: Launch OP2 into
    ''' DLL Picker, and later Test in OP2.
    '''
    '''  - Not installed: both files are copied in.
    '''  - Installed, and op2launcher.exe has the same MD5 as the bundled one:
    '''    nothing to do (a missing Launcher.dll is still copied in).
    '''  - Installed but different: the user is asked whether to replace it. An
    '''    MD5 shows the files differ, not which is newer, so a copy the user
    '''    installed themselves is never replaced silently.
    '''
    ''' op2launcher registers its add-on in outpost2.ini itself the first time
    ''' it runs, so this only copies files. Returns False (after telling the
    ''' user) if the launcher isn't usable, e.g. the OPU folder is missing or
    ''' can't be written to.
    ''' </summary>
    Public Function EnsureOp2LauncherInstalled(dialogTitle As String) As Boolean
        If String.IsNullOrEmpty(AppSettings.OP2Path) OrElse Not Directory.Exists(AppSettings.OP2Path) Then
            MessageBox.Show("Outpost 2 game directory is not set or doesn't exist." & vbCrLf & vbCrLf &
                            "Configure it in File → Settings.",
                            dialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        Dim opuFolder As String = Path.Combine(AppSettings.OP2Path, "OPU")
        If Not Directory.Exists(opuFolder) Then
            MessageBox.Show("The OPU folder was not found:" & vbCrLf & opuFolder & vbCrLf & vbCrLf &
                            "OP2Launcher needs an Outpost 2 install with the OPU update.",
                            dialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        Dim bundled As New List(Of Byte())
        Try
            For i As Integer = 0 To BundledLauncherFiles.GetLength(0) - 1
                bundled.Add(ReadEmbeddedResource(BundledLauncherFiles(i, 1)))
            Next
        Catch ex As Exception
            ' Shouldn't happen in a normal build; fall back to whatever is installed.
            Debug.WriteLine("OP2Launcher: bundled files unavailable: " & ex.Message)
            AppLog.WriteError("Launcher", "Bundled OP2Launcher files unavailable", ex)
            Return File.Exists(Path.Combine(opuFolder, "op2launcher.exe"))
        End Try

        Dim exePath As String = Path.Combine(opuFolder, BundledLauncherFiles(0, 0))
        Dim installNeeded As Boolean
        If Not File.Exists(exePath) Then
            installNeeded = True
            Debug.WriteLine("OP2Launcher: not installed, installing the bundled copy")
            AppLog.Write("Launcher", "op2launcher.exe not installed in " & opuFolder & " - installing the bundled copy")
        ElseIf Md5Of(File.ReadAllBytes(exePath)) = Md5Of(bundled(0)) Then
            installNeeded = False
        Else
            Dim answer = MessageBox.Show(
                "A different version of op2launcher.exe is installed in:" & vbCrLf & opuFolder & vbCrLf & vbCrLf &
                "Replace it with the version bundled with OP2Mapper?" & vbCrLf & vbCrLf &
                "Choose No to keep using the installed one.",
                dialogTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
            AppLog.Write("Launcher", $"A different op2launcher.exe is installed in {opuFolder} - user chose {answer}")
            If answer = DialogResult.Cancel Then Return False
            If answer = DialogResult.No Then Return True
            installNeeded = True
        End If

        Try
            For i As Integer = 0 To BundledLauncherFiles.GetLength(0) - 1
                Dim target As String = Path.Combine(opuFolder, BundledLauncherFiles(i, 0))
                ' Up to date: still put back a missing Launcher.dll.
                If Not installNeeded AndAlso File.Exists(target) Then Continue For
                File.WriteAllBytes(target, bundled(i))
                Debug.WriteLine("OP2Launcher: installed " & target)
                AppLog.Write("Launcher", $"Installed {target} (md5 {Md5Of(bundled(i))})")
            Next
        Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is IOException
            AppLog.WriteError("Launcher", "Could not install OP2Launcher into " & opuFolder, ex)
            MessageBox.Show("OP2Launcher could not be installed into:" & vbCrLf & opuFolder & vbCrLf & vbCrLf &
                            ex.Message & vbCrLf & vbCrLf &
                            "If Outpost 2 is under Program Files, run OP2Mapper as administrator once, " &
                            "or close Outpost 2 if it is running, then try again.",
                            dialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
        Return True
    End Function

    Private Function ReadEmbeddedResource(name As String) As Byte()
        Using s As Stream = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            If s Is Nothing Then Throw New FileNotFoundException("Embedded resource not found: " & name)
            Using ms As New MemoryStream()
                s.CopyTo(ms)
                Return ms.ToArray()
            End Using
        End Using
    End Function

    Private Function Md5Of(data As Byte()) As String
        Using md5 = Security.Cryptography.MD5.Create()
            Return BitConverter.ToString(md5.ComputeHash(data))
        End Using
    End Function

    ''' <summary>
    ''' Opens the configured Outpost 2 directory in Windows Explorer.
    ''' Mirrors LaunchOutpost2's error-handling style.
    ''' </summary>
    Public Function OpenOutpost2Folder() As Boolean
        If String.IsNullOrEmpty(AppSettings.OP2Path) OrElse
           Not Directory.Exists(AppSettings.OP2Path) Then
            MessageBox.Show(
                "Outpost 2 game directory is not set or doesn't exist." & vbCrLf & vbCrLf &
                "Configure it in File → Settings.",
                "Open Outpost 2 Folder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        Try
            ' Explicit explorer.exe + path avoids any ambiguity over how
            ' Process.Start interprets a bare folder argument.
            Process.Start("explorer.exe", AppSettings.OP2Path)
            Return True
        Catch ex As Exception
            MessageBox.Show($"Failed to open Outpost 2 folder: {ex.Message}",
                            "Open Outpost 2 Folder",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

End Module
