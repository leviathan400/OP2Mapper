<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class fTileGroups
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
        Me.pnlTileGroupViewport = New System.Windows.Forms.Panel()
        Me.cmbTileGroup = New System.Windows.Forms.ComboBox()
        Me.cmbFolder = New System.Windows.Forms.ComboBox()
        Me.LabelFolder = New System.Windows.Forms.Label()
        Me.LabelTileGroupCombo = New System.Windows.Forms.Label()
        Me.lblPath = New System.Windows.Forms.Label()
        Me.lblPlaceShortcut = New System.Windows.Forms.Label()
        Me.LabelCount = New System.Windows.Forms.Label()
        Me.lblTileGroups = New System.Windows.Forms.Label()
        Me.LabelTileSet = New System.Windows.Forms.Label()
        Me.lblTileset = New System.Windows.Forms.Label()
        Me.lblWidth = New System.Windows.Forms.Label()
        Me.lblHeight = New System.Windows.Forms.Label()
        Me.lblTileGroupName = New System.Windows.Forms.Label()
        Me.LabelWidth = New System.Windows.Forms.Label()
        Me.LabelHeight = New System.Windows.Forms.Label()
        Me.LabelName = New System.Windows.Forms.Label()
        Me.LabelNotes = New System.Windows.Forms.Label()
        Me.lblNotes = New System.Windows.Forms.Label()
        Me.btnPlaceTileGroup = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'pnlTileGroupViewport
        '
        Me.pnlTileGroupViewport.Location = New System.Drawing.Point(12, 92)
        Me.pnlTileGroupViewport.Name = "pnlTileGroupViewport"
        Me.pnlTileGroupViewport.Size = New System.Drawing.Size(259, 243)
        Me.pnlTileGroupViewport.TabIndex = 4
        '
        'cmbTileGroup
        '
        Me.cmbTileGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbTileGroup.FormattingEnabled = True
        Me.cmbTileGroup.Location = New System.Drawing.Point(12, 65)
        Me.cmbTileGroup.Name = "cmbTileGroup"
        Me.cmbTileGroup.Size = New System.Drawing.Size(193, 21)
        Me.cmbTileGroup.TabIndex = 3
        '
        'cmbFolder
        '
        Me.cmbFolder.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbFolder.FormattingEnabled = True
        Me.cmbFolder.Location = New System.Drawing.Point(12, 25)
        Me.cmbFolder.Name = "cmbFolder"
        Me.cmbFolder.Size = New System.Drawing.Size(193, 21)
        Me.cmbFolder.TabIndex = 1
        '
        'LabelFolder
        '
        Me.LabelFolder.AutoSize = True
        Me.LabelFolder.Location = New System.Drawing.Point(12, 9)
        Me.LabelFolder.Name = "LabelFolder"
        Me.LabelFolder.Size = New System.Drawing.Size(42, 13)
        Me.LabelFolder.TabIndex = 0
        Me.LabelFolder.Text = "Folder:"
        '
        'LabelTileGroupCombo
        '
        Me.LabelTileGroupCombo.AutoSize = True
        Me.LabelTileGroupCombo.Location = New System.Drawing.Point(12, 49)
        Me.LabelTileGroupCombo.Name = "LabelTileGroupCombo"
        Me.LabelTileGroupCombo.Size = New System.Drawing.Size(62, 13)
        Me.LabelTileGroupCombo.TabIndex = 2
        Me.LabelTileGroupCombo.Text = "Tile Group:"
        '
        'lblPath
        '
        Me.lblPath.AutoEllipsis = True
        Me.lblPath.AutoSize = False
        Me.lblPath.ForeColor = System.Drawing.Color.DimGray
        Me.lblPath.Location = New System.Drawing.Point(12, 339)
        Me.lblPath.Name = "lblPath"
        Me.lblPath.Size = New System.Drawing.Size(420, 32)
        Me.lblPath.TabIndex = 5
        Me.lblPath.Text = ""
        '
        'LabelCount
        '
        Me.LabelCount.AutoSize = True
        Me.LabelCount.Location = New System.Drawing.Point(295, 15)
        Me.LabelCount.Name = "LabelCount"
        Me.LabelCount.Size = New System.Drawing.Size(64, 13)
        Me.LabelCount.TabIndex = 3
        Me.LabelCount.Text = "Tile Groups:"
        Me.LabelCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTileGroups
        '
        Me.lblTileGroups.AutoSize = True
        Me.lblTileGroups.Location = New System.Drawing.Point(365, 15)
        Me.lblTileGroups.Name = "lblTileGroups"
        Me.lblTileGroups.Size = New System.Drawing.Size(25, 13)
        Me.lblTileGroups.TabIndex = 4
        Me.lblTileGroups.Text = "000"
        '
        'LabelTileSet
        '
        Me.LabelTileSet.AutoSize = True
        Me.LabelTileSet.Location = New System.Drawing.Point(316, 45)
        Me.LabelTileSet.Name = "LabelTileSet"
        Me.LabelTileSet.Size = New System.Drawing.Size(43, 13)
        Me.LabelTileSet.TabIndex = 5
        Me.LabelTileSet.Text = "TileSet:"
        Me.LabelTileSet.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblTileset
        '
        Me.lblTileset.AutoSize = True
        Me.lblTileset.Location = New System.Drawing.Point(365, 45)
        Me.lblTileset.Name = "lblTileset"
        Me.lblTileset.Size = New System.Drawing.Size(34, 13)
        Me.lblTileset.TabIndex = 6
        Me.lblTileset.Text = "tileset"
        '
        'lblWidth
        '
        Me.lblWidth.AutoSize = True
        Me.lblWidth.Location = New System.Drawing.Point(365, 105)
        Me.lblWidth.Name = "lblWidth"
        Me.lblWidth.Size = New System.Drawing.Size(13, 13)
        Me.lblWidth.TabIndex = 7
        Me.lblWidth.Text = "0"
        '
        'lblHeight
        '
        Me.lblHeight.AutoSize = True
        Me.lblHeight.Location = New System.Drawing.Point(365, 135)
        Me.lblHeight.Name = "lblHeight"
        Me.lblHeight.Size = New System.Drawing.Size(13, 13)
        Me.lblHeight.TabIndex = 8
        Me.lblHeight.Text = "0"
        '
        'lblTileGroupName
        '
        Me.lblTileGroupName.AutoSize = True
        Me.lblTileGroupName.Location = New System.Drawing.Point(365, 75)
        Me.lblTileGroupName.Name = "lblTileGroupName"
        Me.lblTileGroupName.Size = New System.Drawing.Size(33, 13)
        Me.lblTileGroupName.TabIndex = 9
        Me.lblTileGroupName.Text = "name"
        '
        'LabelWidth
        '
        Me.LabelWidth.AutoSize = True
        Me.LabelWidth.Location = New System.Drawing.Point(321, 105)
        Me.LabelWidth.Name = "LabelWidth"
        Me.LabelWidth.Size = New System.Drawing.Size(38, 13)
        Me.LabelWidth.TabIndex = 10
        Me.LabelWidth.Text = "Width:"
        Me.LabelWidth.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'LabelHeight
        '
        Me.LabelHeight.AutoSize = True
        Me.LabelHeight.Location = New System.Drawing.Point(318, 135)
        Me.LabelHeight.Name = "LabelHeight"
        Me.LabelHeight.Size = New System.Drawing.Size(41, 13)
        Me.LabelHeight.TabIndex = 11
        Me.LabelHeight.Text = "Height:"
        Me.LabelHeight.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'LabelName
        '
        Me.LabelName.AutoSize = True
        Me.LabelName.Location = New System.Drawing.Point(321, 75)
        Me.LabelName.Name = "LabelName"
        Me.LabelName.Size = New System.Drawing.Size(38, 13)
        Me.LabelName.TabIndex = 12
        Me.LabelName.Text = "Name:"
        Me.LabelName.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'LabelNotes
        '
        Me.LabelNotes.AutoSize = True
        Me.LabelNotes.Location = New System.Drawing.Point(321, 165)
        Me.LabelNotes.Name = "LabelNotes"
        Me.LabelNotes.Size = New System.Drawing.Size(38, 13)
        Me.LabelNotes.TabIndex = 13
        Me.LabelNotes.Text = "Notes:"
        Me.LabelNotes.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'lblNotes
        '
        Me.lblNotes.AutoSize = True
        Me.lblNotes.Location = New System.Drawing.Point(365, 165)
        Me.lblNotes.Name = "lblNotes"
        Me.lblNotes.Size = New System.Drawing.Size(27, 13)
        Me.lblNotes.TabIndex = 14
        Me.lblNotes.Text = "N/A"
        '
        'btnPlaceTileGroup
        '
        Me.btnPlaceTileGroup.Location = New System.Drawing.Point(303, 210)
        Me.btnPlaceTileGroup.Name = "btnPlaceTileGroup"
        Me.btnPlaceTileGroup.Size = New System.Drawing.Size(130, 23)
        Me.btnPlaceTileGroup.TabIndex = 15
        Me.btnPlaceTileGroup.Text = "Place Tile Group"
        Me.btnPlaceTileGroup.UseVisualStyleBackColor = True
        '
        'lblPlaceShortcut
        '
        Me.lblPlaceShortcut.AutoSize = True
        Me.lblPlaceShortcut.ForeColor = System.Drawing.Color.DimGray
        Me.lblPlaceShortcut.Location = New System.Drawing.Point(322, 236)
        Me.lblPlaceShortcut.Name = "lblPlaceShortcut"
        Me.lblPlaceShortcut.Size = New System.Drawing.Size(86, 13)
        Me.lblPlaceShortcut.TabIndex = 16
        Me.lblPlaceShortcut.Text = "Ctrl+B to Place"
        '
        'fTileGroups
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(446, 382)
        Me.ControlBox = False
        Me.Controls.Add(Me.lblPlaceShortcut)
        Me.Controls.Add(Me.btnPlaceTileGroup)
        Me.Controls.Add(Me.lblNotes)
        Me.Controls.Add(Me.LabelNotes)
        Me.Controls.Add(Me.LabelName)
        Me.Controls.Add(Me.LabelHeight)
        Me.Controls.Add(Me.LabelWidth)
        Me.Controls.Add(Me.lblTileGroupName)
        Me.Controls.Add(Me.lblHeight)
        Me.Controls.Add(Me.lblWidth)
        Me.Controls.Add(Me.lblTileset)
        Me.Controls.Add(Me.LabelTileSet)
        Me.Controls.Add(Me.lblTileGroups)
        Me.Controls.Add(Me.LabelCount)
        Me.Controls.Add(Me.LabelFolder)
        Me.Controls.Add(Me.LabelTileGroupCombo)
        Me.Controls.Add(Me.lblPath)
        Me.Controls.Add(Me.cmbFolder)
        Me.Controls.Add(Me.cmbTileGroup)
        Me.Controls.Add(Me.pnlTileGroupViewport)
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "fTileGroups"
        Me.Text = "fTileGroups"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents pnlTileGroupViewport As Panel
    Friend WithEvents cmbTileGroup As ComboBox
    Friend WithEvents cmbFolder As ComboBox
    Friend WithEvents LabelCount As Label
    Friend WithEvents lblTileGroups As Label
    Friend WithEvents LabelTileSet As Label
    Friend WithEvents lblTileset As Label
    Friend WithEvents lblWidth As Label
    Friend WithEvents lblHeight As Label
    Friend WithEvents lblTileGroupName As Label
    Friend WithEvents LabelWidth As Label
    Friend WithEvents LabelHeight As Label
    Friend WithEvents LabelName As Label
    Friend WithEvents LabelNotes As Label
    Friend WithEvents lblNotes As Label
    Friend WithEvents btnPlaceTileGroup As Button
    Friend WithEvents LabelFolder As Label
    Friend WithEvents LabelTileGroupCombo As Label
    Friend WithEvents lblPath As Label
    Friend WithEvents lblPlaceShortcut As Label
End Class
