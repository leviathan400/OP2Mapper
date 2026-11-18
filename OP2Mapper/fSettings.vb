Imports System.IO

' OP2Mapper
' fSettings.vb - Settings dialog for the OP2 game path, default save
' directory, and Tilegroups folder (persisted via My.Settings).

Public Class fSettings
    ''' <summary>
    ''' Form load event handler - loads the current My.Settings values into the fields
    ''' </summary>
    Private Sub fSettings_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Same icon as the main window / application.
        Me.Icon = My.Resources.IconLarge
        Me.Text = "Settings"
        lblVersion.Text = "Version " & GlobalVariables.ApplicationVersion & " (Build " & GlobalVariables.ApplicationBuild & ")"

        panelBanner.BackgroundImage = My.Resources.banner03_620x100

        ' Load the current values from My.Settings into the text boxes
        txtOP2Path.Text = My.Settings.OP2Path
        txtDefaultSaveDirectory.Text = My.Settings.WorkingPath
        txtTilegroups.Text = My.Settings.Tilegroups
    End Sub

    ''' <summary>
    ''' Browse button click event handler - picks and validates the Outpost 2 game directory
    ''' </summary>
    Private Sub btnBrowseOP2Path_Click(sender As Object, e As EventArgs) Handles btnBrowseOP2Path.Click
        ' Open a folder dialog to select the Outpost 2 game directory
        Using folderDialog As New FolderBrowserDialog()
            folderDialog.Description = "Select Outpost 2 Game Directory"
            folderDialog.ShowNewFolderButton = False

            ' Set initial directory with proper validation
            If Not String.IsNullOrWhiteSpace(txtOP2Path.Text) AndAlso
               Directory.Exists(txtOP2Path.Text) Then
                folderDialog.SelectedPath = txtOP2Path.Text
            Else
                ' Default to Program Files if the path is invalid
                folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            End If

            ' Keep showing the dialog until user selects a valid directory or cancels
            Dim dialogResult As DialogResult
            Dim validDirectorySelected As Boolean = False

            Do
                dialogResult = folderDialog.ShowDialog()

                If dialogResult = DialogResult.OK Then
                    Dim selectedPath As String = folderDialog.SelectedPath

                    ' Validate the Outpost 2 directory using our validation function
                    If IsValidOP2Directory(selectedPath) Then
                        ' Valid Outpost 2 v1.4.1 directory found
                        txtOP2Path.Text = selectedPath
                        validDirectorySelected = True
                    Else
                        ' Display an error message with specific validation requirements
                        MessageBox.Show(
                            "The selected directory is not a valid Outpost 2 v1.4.1 installation." & vbCrLf & vbCrLf &
                            "Please ensure:" & vbCrLf &
                            "- Outpost2.exe exists in the main directory" & vbCrLf &
                            "- OPULauncher.exe exists in the /OPU subfolder",
                            "Invalid Outpost 2 Directory",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)
                    End If
                Else
                    ' User canceled the dialog
                    validDirectorySelected = True  ' Exit the loop
                End If
            Loop Until validDirectorySelected
        End Using
    End Sub

    ''' <summary>
    ''' Browse button click event handler - picks the default map save directory
    ''' </summary>
    Private Sub btnBrowseDefaultSaveDirectory_Click(sender As Object, e As EventArgs) Handles btnBrowseDefaultSaveDirectory.Click
        ' Open a folder dialog to select the default save directory
        Using folderDialog As New FolderBrowserDialog()
            folderDialog.Description = "Select Default Save Directory"
            folderDialog.ShowNewFolderButton = True

            ' Set initial directory with proper validation
            If Not String.IsNullOrWhiteSpace(txtDefaultSaveDirectory.Text) AndAlso
               Directory.Exists(txtDefaultSaveDirectory.Text) Then
                folderDialog.SelectedPath = txtDefaultSaveDirectory.Text
            Else
                ' Default to Documents folder if the path is invalid
                folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            End If

            If folderDialog.ShowDialog() = DialogResult.OK Then
                txtDefaultSaveDirectory.Text = folderDialog.SelectedPath
            End If
        End Using
    End Sub

    ''' <summary>
    ''' Browse button click event handler - picks the Tilegroups directory
    ''' </summary>
    Private Sub btnBrowseTilegroups_Click(sender As Object, e As EventArgs) Handles btnBrowseTilegroups.Click
        Using folderDialog As New FolderBrowserDialog()
            folderDialog.Description = "Select Tile Groups Directory"
            folderDialog.ShowNewFolderButton = True

            If Not String.IsNullOrWhiteSpace(txtTilegroups.Text) AndAlso
               Directory.Exists(txtTilegroups.Text) Then
                folderDialog.SelectedPath = txtTilegroups.Text
            Else
                folderDialog.SelectedPath = Application.StartupPath
            End If

            If folderDialog.ShowDialog() = DialogResult.OK Then
                txtTilegroups.Text = folderDialog.SelectedPath
            End If
        End Using
    End Sub

    ''' <summary>
    ''' Save button click event handler - persists the values to My.Settings and closes
    ''' </summary>
    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        ' The text boxes are editable, so typed paths get the same scrutiny the
        ' Browse buttons apply. A bad OP2 path otherwise saved silently and
        ' later showed up as a map full of placeholder tiles.
        Dim op2Path As String = txtOP2Path.Text.Trim()
        Dim workingPath As String = txtDefaultSaveDirectory.Text.Trim()
        Dim tilegroupsPath As String = txtTilegroups.Text.Trim()

        If String.IsNullOrEmpty(op2Path) OrElse Not Directory.Exists(op2Path) Then
            MessageBox.Show(
                "The Outpost 2 directory doesn't exist:" & vbCrLf & vbCrLf &
                If(String.IsNullOrEmpty(op2Path), "(empty)", op2Path),
                "Invalid Outpost 2 Directory", MessageBoxButtons.OK, MessageBoxIcon.Error)
            txtOP2Path.Focus()
            Return
        End If

        If Not IsValidOP2Directory(op2Path) Then
            ' The folder exists but doesn't look like a standard install —
            ' allow it (layouts vary), but make sure it's deliberate.
            Dim answer As DialogResult = MessageBox.Show(
                "This folder doesn't look like an Outpost 2 v1.4.1 installation " &
                "(expected Outpost2.exe, and OPU\OPULauncher.exe)." & vbCrLf & vbCrLf &
                "Tilesets may not load. Save it anyway?",
                "Outpost 2 Directory", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            If answer <> DialogResult.Yes Then
                txtOP2Path.Focus()
                Return
            End If
        End If

        If Not EnsureFolderOrAsk(workingPath, "default save directory", txtDefaultSaveDirectory) Then Return
        If Not EnsureFolderOrAsk(tilegroupsPath, "tile groups directory", txtTilegroups) Then Return

        ' Store the values directly to My.Settings
        My.Settings.OP2Path = op2Path
        My.Settings.WorkingPath = workingPath
        My.Settings.Tilegroups = tilegroupsPath

        ' Save the settings
        My.Settings.Save()

        ' Close the form
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    ''' <summary>
    ''' Cancel button click event handler - closes the dialog without saving
    ''' </summary>
    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        ' Just close the form without saving
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    ''' <summary>
    ''' Returns True if the folder exists, or is empty (unset), or the user agreed
    ''' to create it and creation succeeded. Otherwise explains why, focuses the
    ''' field and returns False.
    ''' </summary>
    Private Function EnsureFolderOrAsk(folder As String, description As String, field As TextBox) As Boolean
        If String.IsNullOrEmpty(folder) OrElse Directory.Exists(folder) Then Return True

        Dim answer As DialogResult = MessageBox.Show(
            $"The {description} doesn't exist:" & vbCrLf & vbCrLf & folder & vbCrLf & vbCrLf & "Create it?",
            "Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If answer = DialogResult.Yes Then
            Try
                Directory.CreateDirectory(folder)
                Return True
            Catch ex As Exception
                MessageBox.Show($"Could not create the {description}:" & vbCrLf & vbCrLf & ex.Message,
                                "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If

        field.Focus()
        Return False
    End Function

    ''' <summary>
    ''' Validates whether a path is a valid Outpost 2 1.4.1 installation directory
    ''' </summary>
    ''' <param name="directoryPath">The directory to validate</param>
    ''' <returns>True if valid, False otherwise</returns>
    Private Function IsValidOP2Directory(directoryPath As String) As Boolean
        ' Check if the directory exists
        If Not Directory.Exists(directoryPath) Then
            Return False
        End If

        ' Check for Outpost2.exe in the main directory
        Dim outpost2ExePath As String = Path.Combine(directoryPath, "Outpost2.exe")
        If Not File.Exists(outpost2ExePath) Then
            Return False
        End If

        ' Check for OPULauncher.exe in the OPU subfolder
        Dim opuSubfolderPath As String = Path.Combine(directoryPath, "OPU")
        Dim opuLauncherPath As String = Path.Combine(opuSubfolderPath, "OPULauncher.exe")

        ' Check if both the OPU subfolder exists and contains OPULauncher.exe
        Return Directory.Exists(opuSubfolderPath) AndAlso File.Exists(opuLauncherPath)
    End Function

End Class