Imports System.IO

' OP2Launcher
' Launches Outpost 2 via OPULauncher.exe in <OP2Path>\OPU\.

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
        If String.IsNullOrEmpty(My.Settings.OP2Path) OrElse
           Not Directory.Exists(My.Settings.OP2Path) Then
            MessageBox.Show(
                "Outpost 2 game directory is not set or doesn't exist." & vbCrLf & vbCrLf &
                "Configure it in File → Settings.",
                "Launch Outpost 2",
                MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        Dim launcherPath As String = Path.Combine(My.Settings.OP2Path, "OPU", "OPULauncher.exe")

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
            Return True
        Catch ex As Exception
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
        If String.IsNullOrEmpty(My.Settings.OP2Path) OrElse
           Not Directory.Exists(My.Settings.OP2Path) Then
            MessageBox.Show(
                "Outpost 2 game directory is not set or doesn't exist." & vbCrLf & vbCrLf &
                "Configure it in File → Settings.",
                dialogTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        Dim opuFolder As String = Path.Combine(My.Settings.OP2Path, "OPU")
        Dim launcherPath As String = Path.Combine(opuFolder, "op2launcher.exe")

        If Not File.Exists(launcherPath) Then
            MessageBox.Show(
                "op2launcher.exe was not found at:" & vbCrLf & launcherPath & vbCrLf & vbCrLf &
                "Copy op2launcher.exe and Launcher.dll into the OPU folder (next to OPULauncher.exe).",
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
            Return True
        Catch ex As Exception
            MessageBox.Show($"Failed to launch: {ex.Message}",
                            dialogTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Opens the configured Outpost 2 directory in Windows Explorer.
    ''' Mirrors LaunchOutpost2's error-handling style.
    ''' </summary>
    Public Function OpenOutpost2Folder() As Boolean
        If String.IsNullOrEmpty(My.Settings.OP2Path) OrElse
           Not Directory.Exists(My.Settings.OP2Path) Then
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
            Process.Start("explorer.exe", My.Settings.OP2Path)
            Return True
        Catch ex As Exception
            MessageBox.Show($"Failed to open Outpost 2 folder: {ex.Message}",
                            "Open Outpost 2 Folder",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

End Module
