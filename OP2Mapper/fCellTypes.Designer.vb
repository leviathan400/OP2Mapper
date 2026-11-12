<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class fCellTypes
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.cmbCellTypeList = New System.Windows.Forms.ListBox()
        Me.SuspendLayout()
        '
        'cmbCellTypeList
        '
        Me.cmbCellTypeList.FormattingEnabled = True
        Me.cmbCellTypeList.Location = New System.Drawing.Point(12, 12)
        Me.cmbCellTypeList.Name = "cmbCellTypeList"
        Me.cmbCellTypeList.Size = New System.Drawing.Size(192, 420)
        Me.cmbCellTypeList.TabIndex = 0
        '
        'fCellTypes
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(217, 445)
        Me.ControlBox = False
        Me.Controls.Add(Me.cmbCellTypeList)
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "fCellTypes"
        Me.Text = "fCellTypes"
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents cmbCellTypeList As ListBox
End Class
