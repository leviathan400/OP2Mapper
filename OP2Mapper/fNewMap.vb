Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports OP2UtilityDotNet.OP2Map


' OP2Mapper
' fNewMap.vb - "New Map" dialog. Lets the user pick a map size, name and
' starting biome, then creates the map from a blank template and fills it
' with random terrain tiles for the chosen biome.

Public Class fNewMap
    ' Class to hold map size information
    Private Structure MapSizeInfo
        Public Width As Integer
        Public Height As Integer
    End Structure

    ' Dictionary to map combo box indices to actual map dimensions
    Private mapSizes As New Dictionary(Of Integer, MapSizeInfo)
    ' Flag to prevent unnecessary file name updates
    Private updatingFileName As Boolean = False
    ' Keep track of the selected biome panel
    Private selectedBiomePanel As Panel = Nothing
    ' Keep track of the selected biome filename
    Private selectedBiomeFilename As String = "blank_4x4.bmp"

    ''' <summary>
    ''' Form load event handler - sets defaults and loads the biome thumbnails
    ''' </summary>
    Private Sub fNewMap_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Set default title
        Me.Text = "New Map"
        Me.Icon = My.Resources.lists

        ' Make sure the BlankMaps folder is populated before we let the user
        ' pick a size — by the time they click OK, GetBlankMapFileName needs
        ' to find the matching template on disk.
        GlobalVariables.EnsureBlankMapsExtracted()

        ' Set up map size dictionary
        InitializeMapSizes()

        ' Set default selection in the ComboBox to 128x128
        cboMapSize.SelectedIndex = 4 ' Index for 128x128

        ' Set default map name, filename, and tileset
        txtMapName.Text = "New Map"
        txtFileName.Text = "New_Map.map"
        txtTileset.Text = "well00"

        ' Load the biome images
        LoadBiomeImages()

        ' Select the blank biome by default
        SelectBiome(pnlBlank, "blank_4x4.bmp")

        ' Focus on the map name field
        txtMapName.SelectAll()
        txtMapName.Focus()
    End Sub

    ''' <summary>
    ''' Populates the map-size dropdown and the index-to-dimensions dictionary,
    ''' matching the available blank map templates
    ''' </summary>
    Private Sub InitializeMapSizes()
        ' Blank Maps (10)
        ' blank_64x64.map
        ' blank_64x128.map
        ' blank_64x256.map
        ' blank_128x64.map
        ' blank_128x128.map
        ' blank_128x256.map
        ' blank_256x64.map
        ' blank_256x128.map
        ' blank_256x256.map
        ' blank_512x256.map

        ' Add to dropdown - Width x Height
        cboMapSize.Items.Add("64 x 64")
        cboMapSize.Items.Add("64 x 128")
        cboMapSize.Items.Add("64 x 256")
        cboMapSize.Items.Add("128 x 64")
        cboMapSize.Items.Add("128 x 128")
        cboMapSize.Items.Add("128 x 256")
        cboMapSize.Items.Add("256 x 64")
        cboMapSize.Items.Add("256 x 128")
        cboMapSize.Items.Add("256 x 256")
        cboMapSize.Items.Add("512 x 256")

        ' Define all available map sizes - Width x Height
        mapSizes.Add(0, New MapSizeInfo With {.Width = 64, .Height = 64})
        mapSizes.Add(1, New MapSizeInfo With {.Width = 64, .Height = 128})
        mapSizes.Add(2, New MapSizeInfo With {.Width = 64, .Height = 256})
        mapSizes.Add(3, New MapSizeInfo With {.Width = 128, .Height = 64})
        mapSizes.Add(4, New MapSizeInfo With {.Width = 128, .Height = 128})
        mapSizes.Add(5, New MapSizeInfo With {.Width = 128, .Height = 256})
        mapSizes.Add(6, New MapSizeInfo With {.Width = 256, .Height = 64})
        mapSizes.Add(7, New MapSizeInfo With {.Width = 256, .Height = 128})
        mapSizes.Add(8, New MapSizeInfo With {.Width = 256, .Height = 256})
        mapSizes.Add(9, New MapSizeInfo With {.Width = 512, .Height = 256})
    End Sub

    ''' <summary>
    ''' Loads the biome thumbnail images from embedded resources onto the panels
    ''' </summary>
    Private Sub LoadBiomeImages()
        Try
            ' Biome thumbnails are embedded as project resources (see
            ' Resources.resx). No disk lookup needed; the build embeds them
            ' into the .exe.
            SetBiomeImageOnPanel(My.Resources.blank_4x4, pnlBlank, "blank_4x4.bmp")
            SetBiomeImageOnPanel(My.Resources.TerrainType_0, pnlTerrain0, "TerrainType_0.bmp")
            SetBiomeImageOnPanel(My.Resources.TerrainType_1, pnlTerrain1, "TerrainType_1.bmp")
            SetBiomeImageOnPanel(My.Resources.TerrainType_2, pnlTerrain2, "TerrainType_2.bmp")
        Catch ex As Exception
            Debug.WriteLine($"Error loading biome images: {ex.Message}")
            CreateDummyBiomeImages()
        End Try
    End Sub

    ''' <summary>
    ''' Paints the given source image onto the panel's background, stretched
    ''' to fit. Don't dispose the source — for My.Resources.* values the
    ''' ResourceManager caches the bitmap and disposing it would break later
    ''' accesses.
    ''' </summary>
    Private Sub SetBiomeImageOnPanel(srcImg As Image, panel As Panel, dummyLabel As String)
        Try
            If srcImg Is Nothing Then
                CreateDummyBiomeImage(panel, dummyLabel)
                Return
            End If

            Dim panelImage As New Bitmap(panel.Width, panel.Height)
            Using g As Graphics = Graphics.FromImage(panelImage)
                g.DrawImage(srcImg, 0, 0, panel.Width, panel.Height)
            End Using
            panel.BackgroundImage = panelImage
        Catch ex As Exception
            Debug.WriteLine($"Error painting biome image {dummyLabel}: {ex.Message}")
            CreateDummyBiomeImage(panel, dummyLabel)
        End Try
    End Sub

    ''' <summary>
    ''' Creates placeholder images for all biome panels when resources fail to load
    ''' </summary>
    Private Sub CreateDummyBiomeImages()
        ' Create dummy images for all panels
        CreateDummyBiomeImage(pnlBlank, "blank_4x4.bmp")
        CreateDummyBiomeImage(pnlTerrain0, "TerrainType_0.bmp")
        CreateDummyBiomeImage(pnlTerrain1, "TerrainType_1.bmp")
        CreateDummyBiomeImage(pnlTerrain2, "TerrainType_2.bmp")
    End Sub

    ''' <summary>
    ''' Creates a color-coded placeholder image (with filename text) for one biome panel
    ''' </summary>
    Private Sub CreateDummyBiomeImage(panel As Panel, filename As String)
        ' Create a dummy image with the panel's size
        Dim dummyImage As New Bitmap(panel.Width, panel.Height)

        ' Create a graphics object from the new bitmap
        Using g As Graphics = Graphics.FromImage(dummyImage)
            ' Fill with a color based on the filename
            Dim fillColor As Color = Color.White

            If filename.Contains("blank") Then
                fillColor = Color.Blue
            ElseIf filename.Contains("0") Then
                fillColor = Color.Brown
            ElseIf filename.Contains("1") Then
                fillColor = Color.Orange
            ElseIf filename.Contains("2") Then
                fillColor = Color.DarkGray
            End If

            ' Fill with the color
            g.Clear(fillColor)

            ' Draw a border
            g.DrawRectangle(Pens.Black, 0, 0, panel.Width - 1, panel.Height - 1)

            ' Add the filename text
            Using font As New Font("Arial", 8)
                Dim textBrush As New SolidBrush(Color.Black)
                Dim textRect As New RectangleF(5, 5, panel.Width - 10, panel.Height - 10)

                g.DrawString(filename, font, textBrush, textRect)
            End Using
        End Using

        ' Set the panel's background image
        panel.BackgroundImage = dummyImage
    End Sub

    ''' <summary>
    ''' Map name change event handler - auto-updates the filename field
    ''' </summary>
    Private Sub txtMapName_TextChanged(sender As Object, e As EventArgs) Handles txtMapName.TextChanged
        ' Only update if we're not already in the middle of an update to prevent infinite loops
        If Not updatingFileName Then
            updatingFileName = True
            txtFileName.Text = GenerateFilenameFromMapName(txtMapName.Text)
            updatingFileName = False
        End If
    End Sub

    ''' <summary>
    ''' Builds a safe .map filename from the map name plus the selected dimensions
    ''' </summary>
    Private Function GenerateFilenameFromMapName(mapName As String) As String
        ' Convert to lowercase
        Dim result As String = mapName.Trim()

        ' Replace spaces with underscores
        result = result.Replace(" ", "_")

        ' Remove invalid filename characters
        For Each c As Char In System.IO.Path.GetInvalidFileNameChars()
            result = result.Replace(c.ToString(), "")
        Next

        ' If we have a map size, append it
        If cboMapSize.SelectedIndex >= 0 AndAlso mapSizes.ContainsKey(cboMapSize.SelectedIndex) Then
            Dim width = mapSizes(cboMapSize.SelectedIndex).Width
            Dim height = mapSizes(cboMapSize.SelectedIndex).Height
            result = $"{result}_{width}x{height}"
        End If

        ' Make sure it ends with .map
        If Not result.EndsWith(".map", StringComparison.OrdinalIgnoreCase) Then
            result &= ".map"
        End If

        Return result
    End Function

    ' Public properties to access form data

    ''' <summary>
    ''' The map name entered by the user (trimmed)
    ''' </summary>
    Public ReadOnly Property MapName As String
        Get
            Return txtMapName.Text.Trim()
        End Get
    End Property

    ''' <summary>
    ''' The .map filename entered (or auto-generated) for the new map
    ''' </summary>
    Public ReadOnly Property FileName As String
        Get
            Return txtFileName.Text.Trim()
        End Get
    End Property

    ''' <summary>
    ''' The tileset (well) name entered by the user
    ''' </summary>
    Public ReadOnly Property Tileset As String
        Get
            Return txtTileset.Text.Trim()
        End Get
    End Property

    ''' <summary>
    ''' Free-form notes entered for the new map
    ''' </summary>
    Public ReadOnly Property MapNotes As String
        Get
            Return txtNotes.Text.Trim()
        End Get
    End Property

    ''' <summary>
    ''' Selected map width in tiles (defaults to 128 if no valid selection)
    ''' </summary>
    Public ReadOnly Property MapWidth As Integer
        Get
            If cboMapSize.SelectedIndex >= 0 AndAlso mapSizes.ContainsKey(cboMapSize.SelectedIndex) Then
                Return mapSizes(cboMapSize.SelectedIndex).Width
            End If
            Return 128 ' Default if something goes wrong
        End Get
    End Property

    ''' <summary>
    ''' Selected map height in tiles (defaults to 128 if no valid selection)
    ''' </summary>
    Public ReadOnly Property MapHeight As Integer
        Get
            If cboMapSize.SelectedIndex >= 0 AndAlso mapSizes.ContainsKey(cboMapSize.SelectedIndex) Then
                Return mapSizes(cboMapSize.SelectedIndex).Height
            End If
            Return 128 ' Default if something goes wrong
        End Get
    End Property

    ''' <summary>
    ''' Filename of the currently selected biome thumbnail (e.g. "blank_4x4.bmp")
    ''' </summary>
    Public ReadOnly Property SelectedBiome As String
        Get
            Return selectedBiomeFilename
        End Get
    End Property

    ''' <summary>
    ''' OK button click event handler - validates input then creates the new map
    ''' </summary>
    Private Sub btnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click
        ' Validate map name
        If String.IsNullOrWhiteSpace(MapName) Then
            MessageBox.Show("Please enter a map name.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtMapName.Focus()
            DialogResult = DialogResult.None
            Return
        End If

        ' Validate file name
        If String.IsNullOrWhiteSpace(FileName) Then
            MessageBox.Show("Please enter a file name.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtFileName.Focus()
            DialogResult = DialogResult.None
            Return
        End If

        ' Make sure the filename ends with .map
        If Not txtFileName.Text.Trim().EndsWith(".map", StringComparison.OrdinalIgnoreCase) Then
            txtFileName.Text = txtFileName.Text.Trim() & ".map"
        End If

        ' Validate tileset
        If String.IsNullOrWhiteSpace(Tileset) Then
            MessageBox.Show("Please enter a tileset name.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtTileset.Focus()
            DialogResult = DialogResult.None
            Return
        End If

        ' Make sure a map size is selected
        If cboMapSize.SelectedIndex < 0 Then
            MessageBox.Show("Please select a map size.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            cboMapSize.Focus()
            DialogResult = DialogResult.None
            Return
        End If

        ' Log the selected values
        'Debug.WriteLine($"New Map Details:")
        'Debug.WriteLine($"  Map Name: {MapName}")
        'Debug.WriteLine($"  File Name: {FileName}")
        'Debug.WriteLine($"  Tileset: {Tileset}")
        'Debug.WriteLine($"  Dimensions: {MapWidth} x {MapHeight}")
        'Debug.WriteLine($"  Selected Biome: {SelectedBiome}")
        'Debug.WriteLine($"  Notes: {MapNotes}")

        ' All validation passed
        DialogResult = DialogResult.OK

        CreateNewMap()

        ' Dismiss the New Map dialog and return focus to the main editor.
        ' (fNewMap is shown modeless, so DialogResult.OK alone wouldn't close it.)
        Me.Close()
        If fMain IsNot Nothing Then fMain.Activate()
    End Sub

    ''' <summary>
    ''' Cancel button click event handler - closes the dialog without creating a map
    ''' </summary>
    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        ' Log the cancellation
        Debug.WriteLine("New map creation canceled by user")

        ' Set the dialog result to Cancel (though this is already set via the DialogResult property)
        DialogResult = DialogResult.Cancel

        ' Close the form
        Me.Close()
    End Sub

    ''' <summary>
    ''' Map size change event handler - regenerates the filename with the new dimensions
    ''' </summary>
    Private Sub cboMapSize_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboMapSize.SelectedIndexChanged
        ' Only update if we have a map name and we're not already updating
        If Not String.IsNullOrEmpty(txtMapName.Text) AndAlso Not updatingFileName Then
            updatingFileName = True
            txtFileName.Text = GenerateFilenameFromMapName(txtMapName.Text)
            updatingFileName = False
        End If
    End Sub

    ' Biome panel click event handlers

    ''' <summary>
    ''' Biome panel click event handler - selects the Blank biome
    ''' </summary>
    Private Sub pnlBlank_Click(sender As Object, e As EventArgs) Handles pnlBlank.Click
        SelectBiome(pnlBlank, "blank_4x4.bmp")
    End Sub

    ''' <summary>
    ''' Biome panel click event handler - selects Terrain Type 0 (brown mud)
    ''' </summary>
    Private Sub pnlTerrain0_Click(sender As Object, e As EventArgs) Handles pnlTerrain0.Click
        SelectBiome(pnlTerrain0, "TerrainType_0.bmp")
    End Sub

    ''' <summary>
    ''' Biome panel click event handler - selects Terrain Type 1 (orange sand)
    ''' </summary>
    Private Sub pnlTerrain1_Click(sender As Object, e As EventArgs) Handles pnlTerrain1.Click
        SelectBiome(pnlTerrain1, "TerrainType_1.bmp")
    End Sub

    ''' <summary>
    ''' Biome panel click event handler - selects Terrain Type 2 (gray rock)
    ''' </summary>
    Private Sub pnlTerrain2_Click(sender As Object, e As EventArgs) Handles pnlTerrain2.Click
        SelectBiome(pnlTerrain2, "TerrainType_2.bmp")
    End Sub

    ' Label click event handlers (to make the labels also selectable)

    ''' <summary>
    ''' Biome label click event handler - selects the Blank biome
    ''' </summary>
    Private Sub lblBlank_Click(sender As Object, e As EventArgs) Handles lblBlank.Click
        SelectBiome(pnlBlank, "blank_4x4.bmp")
    End Sub

    ''' <summary>
    ''' Biome label click event handler - selects Terrain Type 0 (brown mud)
    ''' </summary>
    Private Sub lblTerrain0_Click(sender As Object, e As EventArgs) Handles lblTerrain0.Click
        SelectBiome(pnlTerrain0, "TerrainType_0.bmp")
    End Sub

    ''' <summary>
    ''' Biome label click event handler - selects Terrain Type 1 (orange sand)
    ''' </summary>
    Private Sub lblTerrain1_Click(sender As Object, e As EventArgs) Handles lblTerrain1.Click
        SelectBiome(pnlTerrain1, "TerrainType_1.bmp")
    End Sub

    ''' <summary>
    ''' Biome label click event handler - selects Terrain Type 2 (gray rock)
    ''' </summary>
    Private Sub lblTerrain2_Click(sender As Object, e As EventArgs) Handles lblTerrain2.Click
        SelectBiome(pnlTerrain2, "TerrainType_2.bmp")
    End Sub

    ''' <summary>
    ''' Marks the given biome panel as selected and remembers its biome filename
    ''' </summary>
    Private Sub SelectBiome(panel As Panel, filename As String)
        ' Reset borders on all panels
        pnlBlank.BorderStyle = BorderStyle.FixedSingle
        pnlTerrain0.BorderStyle = BorderStyle.FixedSingle
        pnlTerrain1.BorderStyle = BorderStyle.FixedSingle
        pnlTerrain2.BorderStyle = BorderStyle.FixedSingle

        ' Highlight the selected panel
        panel.BorderStyle = BorderStyle.Fixed3D

        ' Store the selected panel and filename
        selectedBiomePanel = panel
        selectedBiomeFilename = filename

        Debug.WriteLine($"Selected biome: {filename}")
    End Sub

    ''' <summary>
    ''' Loads the matching blank map template into the editor, then initializes
    ''' it with the selected biome's terrain
    ''' </summary>
    Private Sub CreateNewMap()
        'Create new map

        Dim blankMapFileName As String = GetBlankMapFileName(MapWidth, MapHeight)
        ' Check If we found a matching template
        If String.IsNullOrEmpty(blankMapFileName) Then
            Dim errorMsg As String = $"ERROR: No blank map template found for dimensions {MapWidth}x{MapHeight}"
            Debug.WriteLine(errorMsg)
            MessageBox.Show($"Could not find blank map template: blank_{MapWidth}x{MapHeight}.map", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim blankMapPath As String = Path.Combine(BlankMapsPath, blankMapFileName)
        Debug.WriteLine("Using blank map template: " & blankMapFileName)


        ' Load the blank map
        'fMain.currentMap = Map.ReadMap(blankMapPath)

        ' Load the blank map using the LoadMapFile function
        fMain.LoadMapFile(blankMapPath)

        InitializeNewMap()

    End Sub


#Region "Initialize New Map"

    ' Now we have loaded a blank map, lets set the starting tiles based on the biome selected
    '
    ' Using the list of random tiles is for the biome does not currently produce great results for orange sand - TerrainType_1.
    ' We may need to look at the tile adjacency database to produce better results.
    ' Also looking at the list of tiles, its not a great list.. there are more tiles that should be included.


    ''' <summary>
    ''' Fills a newly created map with terrain tiles based on the selected biome.
    ''' </summary>
    ''' <remarks>
    ''' This method handles the initialization of terrain after a new map has been created:
    ''' 
    ''' 1. BIOME SELECTION:
    '''    - If "Blank" biome is selected: No changes (keeps default tile 0)
    '''    - If "TerrainType_0" (brown mud): Uses tiles 1-32
    '''    - If "TerrainType_1" (orange sand): Uses tiles 1206-1221
    '''    - If "TerrainType_2" (gray rock): Uses tiles 439-454
    ''' 
    ''' 2. TERRAIN GENERATION:
    '''    - Fills the entire map with randomly selected tiles from the appropriate tile list
    '''    - Each position on the map gets a random tile from the biome's tile pool
    ''' 
    ''' 3. DIRECT MAP MANIPULATION:
    '''    - Sets tile values directly since this is initial map creation
    '''    - No need for undo/redo as this is the starting state of the map
    ''' 
    ''' 4. INTERFACE UPDATES:
    '''    - Updates the map display to show the new terrain
    '''    - Sets proper map filename in UI elements
    '''    - Updates window title
    ''' 
    ''' The method handles exceptions and provides debug logging throughout the process.
    ''' </remarks>
    Private Sub InitializeNewMap()
        ' If no map is loaded or blank biome is selected, nothing to do
        If fMain.CurrentLoadedMap Is Nothing Then
            Debug.WriteLine("ERROR: No map loaded in fMain")
            Return
        End If

        'If selectedBiomeFilename.Contains("blank") Then
        '    Debug.WriteLine("Blank biome selected - keeping default map with tile 0")
        '    'Return
        'End If

        ' Set up the appropriate tile ID list based on the selected biome
        Dim tileIdList As New List(Of Integer)()

        If selectedBiomeFilename.Contains("blank") Then
            ' Check if "Blank" biome is selected - nothing to do as blank maps already have tile 0
            Debug.WriteLine("Blank biome selected - keeping default map with tile 0")

        ElseIf selectedBiomeFilename.Contains("TerrainType_0") Then
            ' Brown mud terrain
            Debug.WriteLine("Initializing with Terrain Type 0 (brown mud)")
            tileIdList.AddRange({1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
                            19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32})

        ElseIf selectedBiomeFilename.Contains("TerrainType_1") Then
            ' Orange sand terrain
            Debug.WriteLine("Initializing with Terrain Type 1 (orange sand)")
            tileIdList.AddRange({1206, 1207, 1208, 1209, 1210, 1211, 1212, 1213, 1214,
                            1215, 1216, 1217, 1218, 1219, 1220, 1221})

        ElseIf selectedBiomeFilename.Contains("TerrainType_2") Then
            ' Gray rock terrain
            Debug.WriteLine("Initializing with Terrain Type 2 (gray rock)")
            tileIdList.AddRange({439, 440, 441, 442, 443, 444, 445, 446, 447,
                            448, 449, 450, 451, 452, 453, 454})

        Else
            ' Unrecognized biome, use default (blank)
            Debug.WriteLine("WARNING: Unrecognized biome selected - keeping default map")
            Return
        End If

        If selectedBiomeFilename.Contains("blank") = False Then
            'Debug.WriteLine("Non Blank Selected")

            ' Create a random number generator
            Dim random As New Random()

            ' Initialize map with random tiles from the selected tile list
            Try
                Debug.WriteLine($"Filling map ({MapWidth}x{MapHeight}) with random tiles from list of {tileIdList.Count} tiles")

                ' Fill the entire map with random tiles from the list
                For y As Integer = 0 To fMain.CurrentLoadedMap.HeightInTiles - 1
                    For x As Integer = 0 To fMain.CurrentLoadedMap.WidthInTiles - 1
                        ' Get a random tile ID from the list
                        Dim randomTileId As Integer = tileIdList(random.Next(0, tileIdList.Count))

                        ' Set the tile directly 
                        fMain.CurrentLoadedMap.SetTileMappingIndex(x, y, randomTileId)
                    Next
                Next

                Debug.WriteLine($"Successfully filled map with random terrain tiles")

            Catch ex As Exception
                Debug.WriteLine($"ERROR initializing map terrain: {ex.Message}")
                MessageBox.Show($"Error initializing map terrain: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If

        Try
            ' Make bottom row impassible (special row)
            'Debug.WriteLine("Setting bottom row tiles to Impassible1")

            ' Get the bottom row index (last row)
            Dim bottomRow As Integer = fMain.CurrentLoadedMap.HeightInTiles - 1

            ' Set all tiles in the bottom row to Impassible1
            For x As Integer = 0 To fMain.CurrentLoadedMap.WidthInTiles - 1
                fMain.CurrentLoadedMap.SetCellType(CellType.Impassible1, x, bottomRow)
            Next

            Debug.WriteLine($"Bottom row (index {bottomRow}) set to Impassible1")
        Catch ex As Exception

        End Try

        ' Update the map display
        fMain.RedrawMap()

        ' Associate the new map with the user's working directory so it doesn't
        ' look like it lives in the BlankMaps templates folder. Save Map's
        ' dialog will default to this path too.
        fMain.SetMapFileName(FileName, My.Settings.WorkingPath)

        Debug.WriteLine($"Map initialization complete: {MapName} ({FileName})")
    End Sub

#End Region


End Class