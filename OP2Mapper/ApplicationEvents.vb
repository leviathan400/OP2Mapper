' ApplicationEvents.vb - VB application framework events.
Namespace My
    ' The following events are available for MyApplication:
    ' Startup: Raised when the application starts, before the startup form is created.
    ' Shutdown: Raised after all application forms are closed.  This event is not raised if the application terminates abnormally.
    ' UnhandledException: Raised if the application encounters an unhandled exception.
    ' StartupNextInstance: Raised when launching a single-instance application and the application is already active.
    ' NetworkAvailabilityChanged: Raised when the network connection is connected or disconnected.
    Partial Friend Class MyApplication

        ''' <summary>First thing on startup: note the version and environment in OP2Mapper.log.</summary>
        Private Sub MyApplication_Startup(sender As Object,
                e As Microsoft.VisualBasic.ApplicationServices.StartupEventArgs) Handles Me.Startup
            AppLog.WriteStartup()
        End Sub

        ''' <summary>Clean exit (all forms closed).</summary>
        Private Sub MyApplication_Shutdown(sender As Object, e As EventArgs) Handles Me.Shutdown
            AppLog.Write("Stop", "OP2Mapper closed")
        End Sub

        ''' <summary>
        ''' An exception nothing else caught. Logged so a crash leaves a trace;
        ''' the framework then shows its usual error and closes the app.
        ''' </summary>
        Private Sub MyApplication_UnhandledException(sender As Object,
                e As Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs) Handles Me.UnhandledException
            AppLog.WriteError("Crash", "Unhandled exception", e.Exception)
        End Sub

        ''' <summary>
        ''' OP2Mapper is single-instance: a second launch (double-clicking the
        ''' exe again, or opening a .map with it) exits and this event fires in
        ''' the running copy instead. Bring that copy to the front, and open
        ''' the map if one was given. Without this the second launch quit with
        ''' nothing visible happening, which looked like a crash.
        ''' </summary>
        Private Sub MyApplication_StartupNextInstance(sender As Object,
                e As Microsoft.VisualBasic.ApplicationServices.StartupNextInstanceEventArgs) Handles Me.StartupNextInstance
            AppLog.Write("Start", "Second launch handed to this copy, arguments: " &
                         If(e.CommandLine.Count > 0, String.Join(" ", e.CommandLine), "(none)"))
            e.BringToForeground = True
            Dim main As fMain = TryCast(Me.MainForm, fMain)
            If main IsNot Nothing AndAlso Not main.IsDisposed Then
                main.HandleNextInstance(e.CommandLine)
            End If
        End Sub

    End Class
End Namespace
