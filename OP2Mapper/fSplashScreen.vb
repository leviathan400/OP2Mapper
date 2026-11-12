Imports System.Media

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

        'Format the version information using the text set into the Version control at design time as the
        '  formatting string.  This allows for effective localization if desired.
        '  Build and revision information could be included by using the following code and changing the 
        '  Version control's designtime text to "Version {0}.{1:00}.{2}.{3}" or something similar.  See
        '  String.Format() in Help for more information.
        '
        '    Version.Text = System.String.Format(Version.Text, My.Application.Info.Version.Major, My.Application.Info.Version.Minor, My.Application.Info.Version.Build, My.Application.Info.Version.Revision)

        Version.Text = System.String.Format(Version.Text, My.Application.Info.Version.Major, My.Application.Info.Version.Minor)

        'Copyright info
        Copyright.Text = My.Application.Info.Copyright

        Debug.WriteLine("--- " & ApplicationName & " Started ---")

        ' First ensure required DLLs exist
        If CheckRequiredLibraries("OP2UtilityDotNet.dll", "OP2MapJsonTools.dll", "Newtonsoft.Json.dll") = True Then
            'Debug.WriteLine("Library Files Present")
            'MsgBox("Library Files Present")
        Else
            Debug.WriteLine("Library Files Missing")
            MsgBox("Library Files Missing. Exiting OP2Mapper")
            End
        End If

        ShowLibrariesVersionInfo()

        Dim player As New SoundPlayer(My.Resources.earthwrk)
        player.Play()

        ' Check for a newer version. If one is available, the updater is
        ' launched and we exit so it can replace our .exe. CheckForUpdates
        ' returns False in that case.
        If Not mUpdateChecker.CheckForUpdates(autoUpdate:=True) Then
            Debug.WriteLine("Update launching — exiting app for replacement")
            End
        End If

        TimerStartup.Enabled = True
        TimerStartup.Interval = 600
    End Sub

    ''' <summary>
    ''' Startup timer tick event handler - hides the splash and shows the main form
    ''' </summary>
    Private Sub TimerStartup_Tick(sender As Object, e As EventArgs) Handles TimerStartup.Tick
        ' Load the main form
        TimerStartup.Enabled = False
        Me.Hide()

        'fTileGroups.Show()

        fMain.Show()
        fMain.Focus()
        Me.Close()
    End Sub

End Class
