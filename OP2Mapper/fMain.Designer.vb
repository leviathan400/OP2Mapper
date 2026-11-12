<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class fMain
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
        Me.pnlProperties = New System.Windows.Forms.Panel()
        Me.lblCurrentPlacement = New System.Windows.Forms.Label()
        Me.picSelectedTool = New System.Windows.Forms.PictureBox()
        Me.lblCurrentTool = New System.Windows.Forms.Label()
        Me.picTilePreview = New System.Windows.Forms.PictureBox()
        Me.picTilePreviewBig = New System.Windows.Forms.PictureBox()
        Me.lblMapImage = New System.Windows.Forms.Label()
        Me.lblTileCount = New System.Windows.Forms.Label()
        Me.lblMapSize = New System.Windows.Forms.Label()
        Me.lblZoomLevel = New System.Windows.Forms.Label()
        Me.lblCellType = New System.Windows.Forms.Label()
        Me.lblCoordinates = New System.Windows.Forms.Label()
        Me.lblMappingIndex = New System.Windows.Forms.Label()
        Me.lblTilesetImage = New System.Windows.Forms.Label()
        Me.lblTileset = New System.Windows.Forms.Label()
        Me.lblMapName = New System.Windows.Forms.Label()
        Me.pnlMap = New System.Windows.Forms.Panel()
        Me.pnlProperties.SuspendLayout()
        CType(Me.picSelectedTool, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.picTilePreview, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.picTilePreviewBig, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'pnlProperties
        '
        Me.pnlProperties.Controls.Add(Me.lblCurrentPlacement)
        Me.pnlProperties.Controls.Add(Me.picSelectedTool)
        Me.pnlProperties.Controls.Add(Me.lblCurrentTool)
        Me.pnlProperties.Controls.Add(Me.picTilePreview)
        Me.pnlProperties.Controls.Add(Me.picTilePreviewBig)
        Me.pnlProperties.Controls.Add(Me.lblMapImage)
        Me.pnlProperties.Controls.Add(Me.lblTileCount)
        Me.pnlProperties.Controls.Add(Me.lblMapSize)
        Me.pnlProperties.Controls.Add(Me.lblZoomLevel)
        Me.pnlProperties.Controls.Add(Me.lblCellType)
        Me.pnlProperties.Controls.Add(Me.lblCoordinates)
        Me.pnlProperties.Controls.Add(Me.lblMappingIndex)
        Me.pnlProperties.Controls.Add(Me.lblTilesetImage)
        Me.pnlProperties.Controls.Add(Me.lblTileset)
        Me.pnlProperties.Controls.Add(Me.lblMapName)
        Me.pnlProperties.Location = New System.Drawing.Point(716, 12)
        Me.pnlProperties.Name = "pnlProperties"
        Me.pnlProperties.Size = New System.Drawing.Size(254, 592)
        Me.pnlProperties.TabIndex = 1
        '
        'lblCurrentPlacement
        '
        Me.lblCurrentPlacement.AutoSize = True
        Me.lblCurrentPlacement.Location = New System.Drawing.Point(23, 496)
        Me.lblCurrentPlacement.Name = "lblCurrentPlacement"
        Me.lblCurrentPlacement.Size = New System.Drawing.Size(101, 13)
        Me.lblCurrentPlacement.TabIndex = 23
        Me.lblCurrentPlacement.Text = "lblCurrentPlacement"
        '
        'picSelectedTool
        '
        Me.picSelectedTool.Location = New System.Drawing.Point(38, 528)
        Me.picSelectedTool.Name = "picSelectedTool"
        Me.picSelectedTool.Size = New System.Drawing.Size(32, 32)
        Me.picSelectedTool.TabIndex = 22
        Me.picSelectedTool.TabStop = False
        '
        'lblCurrentTool
        '
        Me.lblCurrentTool.AutoSize = True
        Me.lblCurrentTool.Location = New System.Drawing.Point(23, 467)
        Me.lblCurrentTool.Name = "lblCurrentTool"
        Me.lblCurrentTool.Size = New System.Drawing.Size(72, 13)
        Me.lblCurrentTool.TabIndex = 21
        Me.lblCurrentTool.Text = "lblCurrentTool"
        '
        'picTilePreview
        '
        Me.picTilePreview.Location = New System.Drawing.Point(59, 273)
        Me.picTilePreview.Name = "picTilePreview"
        Me.picTilePreview.Size = New System.Drawing.Size(32, 32)
        Me.picTilePreview.TabIndex = 20
        Me.picTilePreview.TabStop = False
        '
        'picTilePreviewBig
        '
        Me.picTilePreviewBig.Location = New System.Drawing.Point(132, 273)
        Me.picTilePreviewBig.Name = "picTilePreviewBig"
        Me.picTilePreviewBig.Size = New System.Drawing.Size(96, 96)
        Me.picTilePreviewBig.TabIndex = 19
        Me.picTilePreviewBig.TabStop = False
        '
        'lblMapImage
        '
        Me.lblMapImage.AutoSize = True
        Me.lblMapImage.Location = New System.Drawing.Point(22, 44)
        Me.lblMapImage.Name = "lblMapImage"
        Me.lblMapImage.Size = New System.Drawing.Size(67, 13)
        Me.lblMapImage.TabIndex = 10
        Me.lblMapImage.Text = "lblMapImage"
        '
        'lblTileCount
        '
        Me.lblTileCount.AutoSize = True
        Me.lblTileCount.Location = New System.Drawing.Point(22, 60)
        Me.lblTileCount.Name = "lblTileCount"
        Me.lblTileCount.Size = New System.Drawing.Size(62, 13)
        Me.lblTileCount.TabIndex = 9
        Me.lblTileCount.Text = "lblTileCount"
        '
        'lblMapSize
        '
        Me.lblMapSize.AutoSize = True
        Me.lblMapSize.Location = New System.Drawing.Point(22, 28)
        Me.lblMapSize.Name = "lblMapSize"
        Me.lblMapSize.Size = New System.Drawing.Size(58, 13)
        Me.lblMapSize.TabIndex = 8
        Me.lblMapSize.Text = "lblMapSize"
        '
        'lblZoomLevel
        '
        Me.lblZoomLevel.AutoSize = True
        Me.lblZoomLevel.Location = New System.Drawing.Point(22, 188)
        Me.lblZoomLevel.Name = "lblZoomLevel"
        Me.lblZoomLevel.Size = New System.Drawing.Size(70, 13)
        Me.lblZoomLevel.TabIndex = 7
        Me.lblZoomLevel.Text = "lblZoomLevel"
        '
        'lblCellType
        '
        Me.lblCellType.AutoSize = True
        Me.lblCellType.Location = New System.Drawing.Point(22, 156)
        Me.lblCellType.Name = "lblCellType"
        Me.lblCellType.Size = New System.Drawing.Size(58, 13)
        Me.lblCellType.TabIndex = 5
        Me.lblCellType.Text = "lblCellType"
        '
        'lblCoordinates
        '
        Me.lblCoordinates.AutoSize = True
        Me.lblCoordinates.Location = New System.Drawing.Point(22, 92)
        Me.lblCoordinates.Name = "lblCoordinates"
        Me.lblCoordinates.Size = New System.Drawing.Size(73, 13)
        Me.lblCoordinates.TabIndex = 4
        Me.lblCoordinates.Text = "lblCoordinates"
        '
        'lblMappingIndex
        '
        Me.lblMappingIndex.AutoSize = True
        Me.lblMappingIndex.Location = New System.Drawing.Point(22, 140)
        Me.lblMappingIndex.Name = "lblMappingIndex"
        Me.lblMappingIndex.Size = New System.Drawing.Size(84, 13)
        Me.lblMappingIndex.TabIndex = 3
        Me.lblMappingIndex.Text = "lblMappingIndex"
        '
        'lblTilesetImage
        '
        Me.lblTilesetImage.AutoSize = True
        Me.lblTilesetImage.Location = New System.Drawing.Point(22, 124)
        Me.lblTilesetImage.Name = "lblTilesetImage"
        Me.lblTilesetImage.Size = New System.Drawing.Size(77, 13)
        Me.lblTilesetImage.TabIndex = 2
        Me.lblTilesetImage.Text = "lblTilesetImage"
        '
        'lblTileset
        '
        Me.lblTileset.AutoSize = True
        Me.lblTileset.Location = New System.Drawing.Point(22, 108)
        Me.lblTileset.Name = "lblTileset"
        Me.lblTileset.Size = New System.Drawing.Size(48, 13)
        Me.lblTileset.TabIndex = 1
        Me.lblTileset.Text = "lblTileset"
        '
        'lblMapName
        '
        Me.lblMapName.AutoSize = True
        Me.lblMapName.Location = New System.Drawing.Point(22, 12)
        Me.lblMapName.Name = "lblMapName"
        Me.lblMapName.Size = New System.Drawing.Size(66, 13)
        Me.lblMapName.TabIndex = 0
        Me.lblMapName.Text = "lblMapName"
        '
        'pnlMap
        '
        Me.pnlMap.AllowDrop = True
        Me.pnlMap.Location = New System.Drawing.Point(12, 12)
        Me.pnlMap.Name = "pnlMap"
        Me.pnlMap.Size = New System.Drawing.Size(542, 253)
        Me.pnlMap.TabIndex = 2
        '
        'fMain
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(982, 636)
        Me.Controls.Add(Me.pnlMap)
        Me.Controls.Add(Me.pnlProperties)
        Me.Name = "fMain"
        Me.Text = "OP2Mapper"
        Me.pnlProperties.ResumeLayout(False)
        Me.pnlProperties.PerformLayout()
        CType(Me.picSelectedTool, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.picTilePreview, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.picTilePreviewBig, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)

    End Sub
    Friend WithEvents pnlProperties As Panel
    Friend WithEvents lblCellType As Label
    Friend WithEvents lblCoordinates As Label
    Friend WithEvents lblMappingIndex As Label
    Friend WithEvents lblTilesetImage As Label
    Friend WithEvents lblTileset As Label
    Friend WithEvents lblMapName As Label
    Friend WithEvents lblZoomLevel As Label
    Friend WithEvents lblMapSize As Label
    Friend WithEvents lblTileCount As Label
    Friend WithEvents pnlMap As Panel
    Friend WithEvents lblMapImage As Label
    Friend WithEvents picTilePreviewBig As PictureBox
    Friend WithEvents picTilePreview As PictureBox
    Friend WithEvents picSelectedTool As PictureBox
    Friend WithEvents lblCurrentTool As Label
    Friend WithEvents lblCurrentPlacement As Label
End Class
