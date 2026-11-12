<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class fTileSelect
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
        Me.pnlTilePasteViewport = New System.Windows.Forms.Panel()
        Me.SuspendLayout()
        '
        'pnlTilePasteViewport
        '
        Me.pnlTilePasteViewport.Location = New System.Drawing.Point(1, 1)
        Me.pnlTilePasteViewport.Name = "pnlTilePasteViewport"
        Me.pnlTilePasteViewport.Size = New System.Drawing.Size(401, 243)
        Me.pnlTilePasteViewport.TabIndex = 1
        '
        'fTileSelect
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(479, 245)
        Me.ControlBox = False
        Me.Controls.Add(Me.pnlTilePasteViewport)
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "fTileSelect"
        Me.Text = "fTileSelect"
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents pnlTilePasteViewport As Panel
End Class
