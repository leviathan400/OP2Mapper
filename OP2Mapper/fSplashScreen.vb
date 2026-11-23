Imports System.Media
Imports System.Threading.Tasks

' fSplashScreen.vb - Startup splash screen. Verifies the required library
' DLLs exist, runs the update check, then opens the main editor form.
Public NotInheritable Class fSplash

    'TODO: This form can easily be set as the splash screen for the application by going to the "Application" tab
    '  of the Project Designer ("Properties" under the "Project" menu).

    ''' <summary>
    ''' Form load event handler - shows app info, checks required DLLs and
    ''' updates, then starts the timer that opens the main form
    ''' </summary>
    Private Sub SplashScreen1_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        'Set up the dialog text at runtime according to the application's assembly information.  

        'TODO: Customize the application's assembly information in the "Application" pane of the project 
        '  properties dialog (under the "Project" menu).

        'Application title
        If My.Application.Info.Title <> "" Then
            ApplicationTitle.Text = My.Application.Info.Title
        Else
            'If the application title is missing, use the application name, without the extension
            ApplicationTitle.Text = System.IO.Path.GetFileNameWithoutExtension(My.Application.Info.AssemblyName)
        End If

        ' Version and build from GlobalVariables, e.g. "Version 0.7.0 (build 0152)". The
        ' designer's "Version {0}.{1:00}" template showed 0.7.0 as "0.07", with no build.
        Version.Text = $"Version {GlobalVariables.ApplicationVersion} (build {GlobalVariables.ApplicationBuild})"

        'Copyright info
        Copyright.Text = My.Application.Info.Copyright

        Debug.WriteLine("--- " & ApplicationName & " Started ---")

        ' First ensure required DLLs exist
        If CheckRequiredLibraries("OP2UtilityDotNet.dll", "OP2MapJsonTools.dll", "Newtonsoft.Json.dll") = True Then
            'Debug.WriteLine("Library Files Present")
            'MsgBox("Library Files Present")
        Else
            Debug.WriteLine("Library Files Missing")
            AppLog.Write("Start", "Required libraries missing (OP2UtilityDotNet, OP2MapJsonTools or Newtonsoft.Json) - exiting")
            MsgBox("Library Files Missing. Exiting OP2Mapper")
            End
        End If

        ShowLibrariesVersionInfo()

        Dim player As New SoundPlayer(My.Resources.earthwrk)
        player.Play()

        ' Start the update check in the background. It used to run right here
        ' on the UI thread, freezing the splash until the server answered or
        ' timed out. The result is acted on in TimerStartup_Tick.
        updateCheck = mUpdateChecker.BeginCheckForUpdates()

        TimerStartup.Enabled = True
        TimerStartup.Interval = 600
    End Sub

    ''' <summary>
    ''' Startup timer tick event handler - hides the splash and shows the main form
    ''' </summary>
    Private Sub TimerStartup_Tick(sender As Object, e As EventArgs) Handles TimerStartup.Tick
        TimerStartup.Enabled = False

        ' The usual case: the check finished while the splash was showing.
        ' Nothing is open yet, so a newer build is installed straight away:
        ' the updater is launched and we exit so it can replace our .exe.
        ' If the updater can't start, carry on with the current version.
        If updateCheck IsNot Nothing AndAlso updateCheck.IsCompleted AndAlso
           updateCheck.Result IsNot Nothing Then
            If mUpdateChecker.LaunchUpdater() Then
                Debug.WriteLine("Update launching — exiting app for replacement")
                AppLog.Write("Update", "Exiting so the updater can replace OP2Mapper")
                End
            End If
        End If

        ' Load the main form
        Me.Hide()

        'fTileGroups.Show()

        fMain.Show()
        fMain.Focus()

        ' Slow network: don't hold up startup. Let the check finish in the
        ' background and, if it finds a newer build, offer it in the main
        ' window (back on the UI thread) rather than closing it unasked.
        If updateCheck IsNot Nothing AndAlso Not updateCheck.IsCompleted Then
            Dim mainForm As fMain = fMain
            updateCheck.ContinueWith(
                Sub(t)
                    If t.Result IsNot Nothing Then mUpdateChecker.OfferUpdate(t.Result, mainForm)
                End Sub,
                TaskScheduler.FromCurrentSynchronizationContext())
        End If

        Me.Close()
    End Sub

    ''' <summary>
    ''' Background update check started in Load. Its result is a newer server
    ''' build number, or Nothing when there's no update or the server can't be
    ''' reached.
    ''' </summary>
    Private updateCheck As Task(Of String)

End Class
