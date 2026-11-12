<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class fTileSet
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
        Me.pnlTileSets = New System.Windows.Forms.Panel()
        Me.cmbWellSelector = New System.Windows.Forms.ComboBox()
        Me.SuspendLayout()
        '
        'pnlTileSets
        '
        Me.pnlTileSets.Location = New System.Drawing.Point(12, 39)
        Me.pnlTileSets.Name = "pnlTileSets"
        Me.pnlTileSets.Size = New System.Drawing.Size(224, 348)
        Me.pnlTileSets.TabIndex = 0
        '
        'cmbWellSelector
        '
        Me.cmbWellSelector.FormattingEnabled = True
        Me.cmbWellSelector.Location = New System.Drawing.Point(12, 12)
        Me.cmbWellSelector.Name = "cmbWellSelector"
        Me.cmbWellSelector.Size = New System.Drawing.Size(224, 21)
        Me.cmbWellSelector.TabIndex = 1
        '
        'fTileSet
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(248, 445)
        Me.ControlBox = False
        Me.Controls.Add(Me.cmbWellSelector)
        Me.Controls.Add(Me.pnlTileSets)
        Me.MaximizeBox = False
        Me.Name = "fTileSet"
        Me.Text = "fTileSet"
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents pnlTileSets As Panel
    Friend WithEvents cmbWellSelector As ComboBox
End Class
