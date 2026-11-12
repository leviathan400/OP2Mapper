<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class fNewMap
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
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

    'Control declarations go here
    Private WithEvents lblMapName As System.Windows.Forms.Label
    Private WithEvents txtMapName As System.Windows.Forms.TextBox
    Private WithEvents lblFileName As System.Windows.Forms.Label
    Private WithEvents txtFileName As System.Windows.Forms.TextBox
    Private WithEvents lblTileset As System.Windows.Forms.Label
    Private WithEvents txtTileset As System.Windows.Forms.TextBox
    Private WithEvents lblNotes As System.Windows.Forms.Label
    Private WithEvents txtNotes As System.Windows.Forms.TextBox
    Private WithEvents grpDimensions As System.Windows.Forms.GroupBox
    Private WithEvents btnOK As System.Windows.Forms.Button
    Private WithEvents btnCancel As System.Windows.Forms.Button
    Private WithEvents lblMapSize As System.Windows.Forms.Label
    Private WithEvents cboMapSize As System.Windows.Forms.ComboBox
    Private WithEvents grpBiome As System.Windows.Forms.GroupBox
    Private WithEvents pnlBlank As System.Windows.Forms.Panel
    Private WithEvents pnlTerrain0 As System.Windows.Forms.Panel
    Private WithEvents pnlTerrain1 As System.Windows.Forms.Panel
    Private WithEvents pnlTerrain2 As System.Windows.Forms.Panel
    Private WithEvents lblBlank As System.Windows.Forms.Label
    Private WithEvents lblTerrain0 As System.Windows.Forms.Label
    Private WithEvents lblTerrain1 As System.Windows.Forms.Label
    Private WithEvents lblTerrain2 As System.Windows.Forms.Label

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.lblMapName = New System.Windows.Forms.Label()
        Me.txtMapName = New System.Windows.Forms.TextBox()
        Me.lblFileName = New System.Windows.Forms.Label()
        Me.txtFileName = New System.Windows.Forms.TextBox()
        Me.lblTileset = New System.Windows.Forms.Label()
        Me.txtTileset = New System.Windows.Forms.TextBox()
        Me.lblNotes = New System.Windows.Forms.Label()
        Me.txtNotes = New System.Windows.Forms.TextBox()
        Me.grpDimensions = New System.Windows.Forms.GroupBox()
        Me.cboMapSize = New System.Windows.Forms.ComboBox()
        Me.lblMapSize = New System.Windows.Forms.Label()
        Me.grpBiome = New System.Windows.Forms.GroupBox()
        Me.lblTerrain2 = New System.Windows.Forms.Label()
        Me.lblTerrain1 = New System.Windows.Forms.Label()
        Me.lblTerrain0 = New System.Windows.Forms.Label()
        Me.lblBlank = New System.Windows.Forms.Label()
        Me.pnlTerrain2 = New System.Windows.Forms.Panel()
        Me.pnlTerrain1 = New System.Windows.Forms.Panel()
        Me.pnlTerrain0 = New System.Windows.Forms.Panel()
        Me.pnlBlank = New System.Windows.Forms.Panel()
        Me.btnOK = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.grpDimensions.SuspendLayout()
        Me.grpBiome.SuspendLayout()
        Me.SuspendLayout()
        '
        'lblMapName
        '
        Me.lblMapName.AutoSize = True
        Me.lblMapName.Location = New System.Drawing.Point(20, 25)
        Me.lblMapName.Name = "lblMapName"
        Me.lblMapName.Size = New System.Drawing.Size(62, 13)
        Me.lblMapName.TabIndex = 0
        Me.lblMapName.Text = "Map Name:"
        '
        'txtMapName
        '
        Me.txtMapName.Location = New System.Drawing.Point(120, 22)
        Me.txtMapName.Name = "txtMapName"
        Me.txtMapName.Size = New System.Drawing.Size(420, 20)
        Me.txtMapName.TabIndex = 1
        '
        'lblFileName
        '
        Me.lblFileName.AutoSize = True
        Me.lblFileName.Location = New System.Drawing.Point(20, 60)
        Me.lblFileName.Name = "lblFileName"
        Me.lblFileName.Size = New System.Drawing.Size(57, 13)
        Me.lblFileName.TabIndex = 2
        Me.lblFileName.Text = "File Name:"
        '
        'txtFileName
        '
        Me.txtFileName.Location = New System.Drawing.Point(120, 57)
        Me.txtFileName.Name = "txtFileName"
        Me.txtFileName.Size = New System.Drawing.Size(420, 20)
        Me.txtFileName.TabIndex = 3
        '
        'lblTileset
        '
        Me.lblTileset.AutoSize = True
        Me.lblTileset.Location = New System.Drawing.Point(20, 95)
        Me.lblTileset.Name = "lblTileset"
        Me.lblTileset.Size = New System.Drawing.Size(41, 13)
        Me.lblTileset.TabIndex = 4
        Me.lblTileset.Text = "Tileset:"
        '
        'txtTileset
        '
        Me.txtTileset.Location = New System.Drawing.Point(120, 92)
        Me.txtTileset.Name = "txtTileset"
        Me.txtTileset.Size = New System.Drawing.Size(420, 20)
        Me.txtTileset.TabIndex = 5
        '
        'lblNotes
        '
        Me.lblNotes.AutoSize = True
        Me.lblNotes.Location = New System.Drawing.Point(20, 130)
        Me.lblNotes.Name = "lblNotes"
        Me.lblNotes.Size = New System.Drawing.Size(38, 13)
        Me.lblNotes.TabIndex = 6
        Me.lblNotes.Text = "Notes:"
        '
        'txtNotes
        '
        Me.txtNotes.Location = New System.Drawing.Point(120, 127)
        Me.txtNotes.Multiline = True
        Me.txtNotes.Name = "txtNotes"
        Me.txtNotes.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtNotes.Size = New System.Drawing.Size(420, 70)
        Me.txtNotes.TabIndex = 7
        '
        'grpDimensions
        '
        Me.grpDimensions.Controls.Add(Me.cboMapSize)
        Me.grpDimensions.Controls.Add(Me.lblMapSize)
        Me.grpDimensions.Location = New System.Drawing.Point(20, 210)
        Me.grpDimensions.Name = "grpDimensions"
        Me.grpDimensions.Size = New System.Drawing.Size(520, 70)
        Me.grpDimensions.TabIndex = 8
        Me.grpDimensions.TabStop = False
        Me.grpDimensions.Text = "Map Dimensions"
        '
        'cboMapSize
        '
        Me.cboMapSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboMapSize.FormattingEnabled = True
        Me.cboMapSize.Location = New System.Drawing.Point(100, 27)
        Me.cboMapSize.Name = "cboMapSize"
        Me.cboMapSize.Size = New System.Drawing.Size(150, 21)
        Me.cboMapSize.TabIndex = 1
        '
        'lblMapSize
        '
        Me.lblMapSize.AutoSize = True
        Me.lblMapSize.Location = New System.Drawing.Point(25, 30)
        Me.lblMapSize.Name = "lblMapSize"
        Me.lblMapSize.Size = New System.Drawing.Size(54, 13)
        Me.lblMapSize.TabIndex = 0
        Me.lblMapSize.Text = "Map Size:"
        '
        'grpBiome
        '
        Me.grpBiome.Controls.Add(Me.lblTerrain2)
        Me.grpBiome.Controls.Add(Me.lblTerrain1)
        Me.grpBiome.Controls.Add(Me.lblTerrain0)
        Me.grpBiome.Controls.Add(Me.lblBlank)
        Me.grpBiome.Controls.Add(Me.pnlTerrain2)
        Me.grpBiome.Controls.Add(Me.pnlTerrain1)
        Me.grpBiome.Controls.Add(Me.pnlTerrain0)
        Me.grpBiome.Controls.Add(Me.pnlBlank)
        Me.grpBiome.Location = New System.Drawing.Point(20, 290)
        Me.grpBiome.Name = "grpBiome"
        Me.grpBiome.Size = New System.Drawing.Size(700, 200)
        Me.grpBiome.TabIndex = 9
        Me.grpBiome.TabStop = False
        Me.grpBiome.Text = "Starting Map Biome"
        '
        'lblTerrain2
        '
        Me.lblTerrain2.AutoSize = True
        Me.lblTerrain2.Location = New System.Drawing.Point(564, 170)
        Me.lblTerrain2.Name = "lblTerrain2"
        Me.lblTerrain2.Size = New System.Drawing.Size(49, 13)
        Me.lblTerrain2.TabIndex = 7
        Me.lblTerrain2.Text = "Terrain 2"
        Me.lblTerrain2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'lblTerrain1
        '
        Me.lblTerrain1.AutoSize = True
        Me.lblTerrain1.Location = New System.Drawing.Point(406, 170)
        Me.lblTerrain1.Name = "lblTerrain1"
        Me.lblTerrain1.Size = New System.Drawing.Size(49, 13)
        Me.lblTerrain1.TabIndex = 6
        Me.lblTerrain1.Text = "Terrain 1"
        Me.lblTerrain1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'lblTerrain0
        '
        Me.lblTerrain0.AutoSize = True
        Me.lblTerrain0.Location = New System.Drawing.Point(248, 170)
        Me.lblTerrain0.Name = "lblTerrain0"
        Me.lblTerrain0.Size = New System.Drawing.Size(49, 13)
        Me.lblTerrain0.TabIndex = 5
        Me.lblTerrain0.Text = "Terrain 0"
        Me.lblTerrain0.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'lblBlank
        '
        Me.lblBlank.AutoSize = True
        Me.lblBlank.Location = New System.Drawing.Point(95, 170)
        Me.lblBlank.Name = "lblBlank"
        Me.lblBlank.Size = New System.Drawing.Size(34, 13)
        Me.lblBlank.TabIndex = 4
        Me.lblBlank.Text = "Blank"
        Me.lblBlank.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'pnlTerrain2
        '
        Me.pnlTerrain2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.pnlTerrain2.Location = New System.Drawing.Point(524, 30)
        Me.pnlTerrain2.Name = "pnlTerrain2"
        Me.pnlTerrain2.Size = New System.Drawing.Size(128, 128)
        Me.pnlTerrain2.TabIndex = 3
        '
        'pnlTerrain1
        '
        Me.pnlTerrain1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.pnlTerrain1.Location = New System.Drawing.Point(366, 30)
        Me.pnlTerrain1.Name = "pnlTerrain1"
        Me.pnlTerrain1.Size = New System.Drawing.Size(128, 128)
        Me.pnlTerrain1.TabIndex = 2
        '
        'pnlTerrain0
        '
        Me.pnlTerrain0.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.pnlTerrain0.Location = New System.Drawing.Point(208, 30)
        Me.pnlTerrain0.Name = "pnlTerrain0"
        Me.pnlTerrain0.Size = New System.Drawing.Size(128, 128)
        Me.pnlTerrain0.TabIndex = 1
        '
        'pnlBlank
        '
        Me.pnlBlank.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.pnlBlank.Location = New System.Drawing.Point(50, 30)
        Me.pnlBlank.Name = "pnlBlank"
        Me.pnlBlank.Size = New System.Drawing.Size(128, 128)
        Me.pnlBlank.TabIndex = 0
        '
        'btnOK
        '
        Me.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.btnOK.Location = New System.Drawing.Point(275, 505)
        Me.btnOK.Name = "btnOK"
        Me.btnOK.Size = New System.Drawing.Size(100, 32)
        Me.btnOK.TabIndex = 10
        Me.btnOK.Text = "OK"
        Me.btnOK.UseVisualStyleBackColor = True
        '
        'btnCancel
        '
        Me.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.btnCancel.Location = New System.Drawing.Point(395, 505)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(100, 32)
        Me.btnCancel.TabIndex = 11
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'fNewMap
        '
        Me.AcceptButton = Me.btnOK
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(740, 560)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnOK)
        Me.Controls.Add(Me.grpBiome)
        Me.Controls.Add(Me.grpDimensions)
        Me.Controls.Add(Me.txtNotes)
        Me.Controls.Add(Me.lblNotes)
        Me.Controls.Add(Me.txtTileset)
        Me.Controls.Add(Me.lblTileset)
        Me.Controls.Add(Me.txtFileName)
        Me.Controls.Add(Me.lblFileName)
        Me.Controls.Add(Me.txtMapName)
        Me.Controls.Add(Me.lblMapName)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "fNewMap"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "New Map"
        Me.grpDimensions.ResumeLayout(False)
        Me.grpDimensions.PerformLayout()
        Me.grpBiome.ResumeLayout(False)
        Me.grpBiome.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
End Class