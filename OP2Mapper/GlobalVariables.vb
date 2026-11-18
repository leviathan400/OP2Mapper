Imports System.IO
Imports System.IO.Compression
Imports System.Reflection
Imports System.Windows.Forms
Imports OP2UtilityDotNet.OP2Map
Imports Newtonsoft.Json
Imports OP2MapJsonTools

' OP2Mapper
' https://github.com/leviathan400/OP2Mapper
'
' GlobalVariables
' Application-wide shared state: version info, the active editing tool
' (selected tile / cell type / tile group), edit / copy / paste mode flags,
' view options (grid, coordinates, render mode), the cell-type lookup table,
' and the blank-map template paths plus their extraction from the embedded
' BlankMaps.zip. Read and written by fMain and all the tool palettes.

Module GlobalVariables

    ' Application info

    ''' <summary>Build number string shown in the About/title info.</summary>
    Public Const ApplicationBuild As String = "0144"
    ''' <summary>Application version string shown in the About/title info.</summary>
    Public Const ApplicationVersion As String = "0.6.0"
    ''' <summary>Application name used in titles and messages.</summary>
    Public Const ApplicationName As String = "OP2Mapper"      ' OP2Mapper / OP2Mapper3
    ''' <summary>One-line application description.</summary>
    Public Const ApplicationDescription As String = "A powerful map editor for Outpost 2: Divided Destiny."
    ''' <summary>Project homepage URL.</summary>
    Public Const ApplicationWebsite As String = "https://github.com/leviathan400/OP2Mapper"

    ' Shared Bitmap property to store the selected tile image
    ''' <summary>Currently selected tile image for placement; written (and disposed/replaced) by fMain.Clipboard, fMain.Editing and fCellTypes.</summary>
    Public SelectedTileImage As Bitmap = Nothing

    ' Tile edit mode
    ''' <summary>True = clicks edit a single tile; toggled from the fMain menus.</summary>
    Public SingleTileEditMode As Boolean = True

    ' Properties to track selected tile details for the UI
    ''' <summary>Tileset filename of the selected tile; written by fMain.Clipboard/fMain.Editing/fCellTypes when a tile is picked or cleared.</summary>
    Public SelectedTilesetName As String = ""
    ''' <summary>Image index of the selected tile within its tileset (-1 = none); written alongside SelectedTilesetName.</summary>
    Public SelectedTileIndex As Integer = -1
    ''' <summary>Tileset index of the selected tile (-1 = none); written alongside SelectedTilesetName.</summary>
    Public SelectedTilesetIndex As Integer = -1

    ' Last row (special row)
    ''' <summary>Index of the map's last (special) row; set by fMain.MapIO when a map is loaded, reset on close.</summary>
    Public MapLastRowIndex As Integer = -1

    ' Application state
    ''' <summary>True while edit mode is active; toggled by fMain.Editing (mirrored by fMain.Helpers' EditModeEnabled property).</summary>
    Public EditModeEnabled As Boolean = False
    ' Enable or disable zoom
    ''' <summary>Whether mouse zooming is enabled; toggled from the fMain menus.</summary>
    Public ZoomEnabled As Boolean = True

    ' Private backing field for ActiveCellType
    Private _activeCellType As CellType = CellType.FastPassible1

    ''' <summary>
    ''' True when a cell type has been deliberately selected as the active paint
    ''' tool (set by fCellTypes, cleared wherever the tool state is reset).
    ''' ActiveCellType always holds a valid value (FastPassible1 by default), so
    ''' this flag - not the value - distinguishes "painting FastPassible1" from
    ''' "no cell-type tool selected".
    ''' </summary>
    Public CellTypeToolActive As Boolean = False

    ' Copy/Paste state tracking
    ''' <summary>True while a copy-region selection is in progress; written by fMain.Clipboard and fMain.Editing.</summary>
    Public IsCopyMode As Boolean = False
    ''' <summary>First corner X of the copy selection (-1 = unset); written by fMain.Clipboard.</summary>
    Public CopyStartX As Integer = -1
    ''' <summary>First corner Y of the copy selection (-1 = unset); written by fMain.Clipboard.</summary>
    Public CopyStartY As Integer = -1
    ''' <summary>Second corner X of the copy selection (-1 = unset); written by fMain.Clipboard.</summary>
    Public CopyEndX As Integer = -1
    ''' <summary>Second corner Y of the copy selection (-1 = unset); written by fMain.Clipboard.</summary>
    Public CopyEndY As Integer = -1
    ''' <summary>Progress of the copy selection; written by fMain.Clipboard.</summary>
    Public CopyStage As Integer = 0 ' 0=Not started, 1=Start selected, 2=Complete

    ' Storage for copied tile data
    ''' <summary>Mapping indices of the copied region (Nothing = no copy); written by fMain.Clipboard.</summary>
    Public CopiedTileMappings As Integer(,) = Nothing
    ''' <summary>Width in tiles of the copied region; written by fMain.Clipboard.</summary>
    Public CopiedRegionWidth As Integer = 0
    ''' <summary>Height in tiles of the copied region; written by fMain.Clipboard.</summary>
    Public CopiedRegionHeight As Integer = 0
    ''' <summary>
    ''' Cell types of the copied region, captured alongside CopiedTileMappings on
    ''' every copy (Nothing = no copy). Always captured so the paste toggle can be
    ''' changed after copying; written by fMain.Clipboard.
    ''' </summary>
    Public CopiedCellTypes As CellType(,) = Nothing
    ''' <summary>
    ''' Edit → Paste Includes Cell Types. When True (the default) a paste also
    ''' writes the copied cell types; when False only the tiles are pasted and
    ''' the target keeps its own cell types. Saved in Mapper3.ini.
    ''' </summary>
    Public PasteCellTypes As Boolean = True

    ' Tile Group placement state
    ''' <summary>True while a tile group is armed for placement; written by fMain.TileGroups and fMain.Editing.</summary>
    Public TileGroupPlacementMode As Boolean = False
    ''' <summary>Mapping indices of the tile group to place; written by fTileGroups when a group is selected.</summary>
    Public TileGroupTileMappings As Integer(,) = Nothing
    ''' <summary>Width in tiles of the armed tile group; written by fTileGroups.</summary>
    Public TileGroupWidth As Integer = 0
    ''' <summary>Height in tiles of the armed tile group; written by fTileGroups.</summary>
    Public TileGroupHeight As Integer = 0
    ''' <summary>Tile X of the placement preview under the cursor (-1 = none); written by fMain.MapPanel / fMain.TileGroups.</summary>
    Public TileGroupPreviewX As Integer = -1
    ''' <summary>Tile Y of the placement preview under the cursor (-1 = none); written by fMain.MapPanel / fMain.TileGroups.</summary>
    Public TileGroupPreviewY As Integer = -1
    ''' <summary>Filename of the selected tile group; written by fTileGroups.</summary>
    Public TileGroupFileName As String = ""

    ' Location for the extracted blank-map templates. Normally next to the
    ' .exe; if that folder isn't writable (e.g. installed under Program
    ' Files) the templates go to %LOCALAPPDATA%\OP2Mapper\BlankMaps instead.
    ' Resolved on first access (after Application.StartupPath is valid) and
    ' cached.
    Private _blankMapsPath As String = Nothing

    ''' <summary>Folder holding the extracted blank-map templates (beside the .exe, or under LocalAppData if that isn't writable).</summary>
    Public ReadOnly Property BlankMapsPath As String
        Get
            If _blankMapsPath Is Nothing Then
                _blankMapsPath = ResolveBlankMapsPath()
            End If
            Return _blankMapsPath
        End Get
    End Property

    ''' <summary>
    ''' Picks the exe-side BlankMaps folder if it already has templates or can
    ''' be written to; otherwise a per-user folder under LocalAppData.
    ''' </summary>
    Private Function ResolveBlankMapsPath() As String
        Dim exeSide As String = Path.Combine(Application.StartupPath, "BlankMaps")
        If Directory.Exists(exeSide) AndAlso Directory.GetFiles(exeSide, "blank_*.map").Length > 0 Then
            Return exeSide
        End If
        If IsDirectoryWritable(Application.StartupPath) Then
            Return exeSide
        End If
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "OP2Mapper", "BlankMaps")
    End Function

    ''' <summary>
    ''' True if a file can be created in the folder (probes with a temp file).
    ''' </summary>
    Private Function IsDirectoryWritable(folder As String) As Boolean
        Try
            Dim probe As String = Path.Combine(folder, ".write-test-" & Guid.NewGuid().ToString("N"))
            Using fs As New FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)
            End Using
            Return True
        Catch
            Return False
        End Try
    End Function

    ' Property with notification for ActiveCellType
    ''' <summary>Cell type applied when painting cell types; setting it notifies fMain to refresh its labels.</summary>
    Public Property ActiveCellType As CellType
        Get
            Return _activeCellType
        End Get
        Set(value As CellType)
            _activeCellType = value
            NotifyMainFormOfChanges()
        End Set
    End Property

    ' Private backing field for ActiveTileMappingIndex
    Private _activeTileMappingIndex As Integer = -1

    ' Property with notification for ActiveTileMappingIndex
    ''' <summary>Mapping index applied when painting tiles (-1 = none); setting it notifies fMain to refresh its labels.</summary>
    Public Property ActiveTileMappingIndex As Integer
        Get
            Return _activeTileMappingIndex
        End Get
        Set(value As Integer)
            _activeTileMappingIndex = value
            NotifyMainFormOfChanges()
        End Set
    End Property

    ' Tile and map constants
    ''' <summary>Tile size in pixels (OP2 tiles are 32x32).</summary>
    Public Const TileSize As Integer = 32
    ''' <summary>Default map zoom level (100%).</summary>
    Public Const DefaultZoomLevel As Single = 1.0F
    ''' <summary>Minimum allowed zoom level.</summary>
    Public Const MinZoomLevel As Single = 0.1F
    ''' <summary>Maximum allowed zoom level.</summary>
    Public Const MaxZoomLevel As Single = 5.0F

    ' File paths and locations
    ''' <summary>Default tileset folder, relative to the OP2 install path setting; set here, not reassigned elsewhere.</summary>
    Public TilesetPath As String = "OPU\base\tilesets"
    ''' <summary>Fallback tileset folders tried when TilesetPath is missing; set here, not reassigned elsewhere.</summary>
    Public AlternateTilesetPaths() As String = {"base\tilesets", "tilesets"}

    ' Large grid and coordinate renders
    ''' <summary>Whether the large grid overlay is shown; toggled from the fMain View menu.</summary>
    Public ShowLargeGrid As Boolean = False
    ''' <summary>Large grid spacing in tiles; set from the fMain grid-options dialog.</summary>
    Public LargeGridSize As Integer = 32
    ''' <summary>Whether coordinate labels are shown; toggled from the fMain View menu.</summary>
    Public ShowCoordinates As Boolean = False
    ''' <summary>Coordinate label spacing in tiles; set from the fMain grid-options dialog.</summary>
    Public CoordinateSpacing As Integer = 32

    ' Dictionary mapping between cell type indices and enum values.
    ' The order must match the list built in fCellTypes.PopulateCellTypesList:
    ' the list's SelectedIndex is the key looked up here.
    ''' <summary>Maps UI list indices (0-31) to CellType enum values; populated here, read-only thereafter.</summary>
    Public ReadOnly CellTypeMap As New Dictionary(Of Integer, CellType) From {
    {0, CellType.FastPassible1},
    {1, CellType.FastPassible2},
    {2, CellType.MediumPassible1},
    {3, CellType.MediumPassible2},
    {4, CellType.SlowPassible1},
    {5, CellType.SlowPassible2},
    {6, CellType.Impassible1},
    {7, CellType.Impassible2},
    {8, CellType.NorthCliffs},
    {9, CellType.CliffsHighSide},
    {10, CellType.CliffsLowSide},
    {11, CellType.VentsAndFumaroles},
    {12, CellType.zPad12},
    {13, CellType.zPad13},
    {14, CellType.zPad14},
    {15, CellType.zPad15},
    {16, CellType.zPad16},
    {17, CellType.zPad17},
    {18, CellType.zPad18},
    {19, CellType.zPad19},
    {20, CellType.zPad20},
    {21, CellType.DozedArea},
    {22, CellType.Rubble},
    {23, CellType.NormalWall},
    {24, CellType.MicrobeWall},
    {25, CellType.LavaWall},
    {26, CellType.Tube0},
    {27, CellType.Tube1},
    {28, CellType.Tube2},
    {29, CellType.Tube3},
    {30, CellType.Tube4},
    {31, CellType.Tube5}
    }

    ' Render modes
    Public Enum RenderMode
        GameMap            ' 1  Game map view with well BMPs (default)
        CellTypeOverlay    ' 2  Cell types overlaid on game map
        CellTypeOverlayAll ' 3  Cell types with detailed text labels overlaid on game map
        CellTypeBasic      ' 4  Basic cell type coloring
        CellTypeAll        ' 5  Detailed cell type coloring
    End Enum

    ' Current active render mode
    ''' <summary>Render mode the editor is currently displaying; written by fMain (Helpers/Export) when the user switches modes.</summary>
    Public CurrentRenderMode As RenderMode = RenderMode.GameMap

    ' Display settings
    ''' <summary>Whether the per-tile grid overlay is shown; toggled from the fMain View menu.</summary>
    Public ShowGrid As Boolean = False

    ' Export settings
    ''' <summary>Scale used for small image exports (12.5%).</summary>
    Public Const DefaultExportScale As Double = 0.125 ' 12.5% size for small exports
    ''' <summary>Scale used for mini image exports (6.25%).</summary>
    Public Const MiniExportScale As Double = 0.0625  ' 6.25% size for mini exports

    ' Method to notify main form of selection changes
    ''' <summary>
    ''' Tells fMain (if open) to refresh its tool/placement labels after the
    ''' active cell type or tile mapping changes. Uses reflection; failures are logged only.
    ''' </summary>
    Private Sub NotifyMainFormOfChanges()
        ' Find the main form instance
        Dim mainForm = TryCast(Application.OpenForms("fMain"), Form)

        ' Call the update method if main form is available
        If mainForm IsNot Nothing Then
            ' Use reflection to call the private method on the main form
            Try
                Dim methodInfo = mainForm.GetType().GetMethod("UpdateToolAndPlacementLabels",
                                                            Reflection.BindingFlags.NonPublic Or
                                                            Reflection.BindingFlags.Instance)
                If methodInfo IsNot Nothing Then
                    methodInfo.Invoke(mainForm, Nothing)
                End If
            Catch ex As Exception
                Debug.WriteLine("Error notifying main form of changes: " & ex.Message)
            End Try
        End If
    End Sub

    ''' <summary>
    ''' Returns the blank-map template filename exactly matching the given
    ''' dimensions, or an empty string if no template of that size exists.
    ''' </summary>
    Public Function GetBlankMapFileName(width As Integer, height As Integer) As String
        ' Only return an exact match for the dimensions
        Dim exactFileName As String = $"blank_{width}x{height}.map"

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

          If File.Exists(Path.Combine(BlankMapsPath, exactFileName)) Then
              Return exactFileName
          Else
              ' No exact match found - return empty string to indicate error
              Return String.Empty
          End If
    End Function

    ''' <summary>
    ''' Ensures the BlankMaps folder holds every template from the embedded
    ''' blankmaps.zip. Any template that is missing, or whose size doesn't
    ''' match the archive (e.g. an interrupted earlier extraction), is
    ''' (re)written. The archive is small and embedded, so checking every
    ''' entry on each call is cheap.
    '''
    ''' Returns Nothing on success, or a user-readable error message if the
    ''' templates couldn't be written — callers should show it, since New Map
    ''' can't work without them.
    ''' </summary>
    Public Function EnsureBlankMapsExtracted() As String
        Dim folder As String = BlankMapsPath
        Dim wroteAny As Boolean = False

        Try
            If Not Directory.Exists(folder) Then
                Directory.CreateDirectory(folder)
            End If

            Using zipArchive As ZipArchive = OpenEmbeddedBlankMapsZip()
                Dim templateCount As Integer = 0

                For Each entry As ZipArchiveEntry In zipArchive.Entries
                    If Not (entry.Name.EndsWith(".map") AndAlso entry.Name.StartsWith("blank_")) Then Continue For
                    templateCount += 1

                    ' entry.Name (not FullName) keeps every file inside the target folder.
                    Dim targetPath As String = Path.Combine(folder, entry.Name)
                    If File.Exists(targetPath) AndAlso New FileInfo(targetPath).Length = entry.Length Then
                        Continue For
                    End If

                    ' Write to a temp name, then move into place, so a crash
                    ' mid-write never leaves a truncated template behind.
                    Dim tempPath As String = targetPath & ".tmp"
                    Using entryStream As Stream = entry.Open()
                        Using fileStream As New FileStream(tempPath, FileMode.Create, FileAccess.Write)
                            entryStream.CopyTo(fileStream)
                        End Using
                    End Using
                    If File.Exists(targetPath) Then File.Delete(targetPath)
                    File.Move(tempPath, targetPath)
                    wroteAny = True
                Next

                If templateCount = 0 Then
                    Throw New Exception("No blank map files were found in the embedded archive.")
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine($"EnsureBlankMapsExtracted: extraction failed: {ex}")
            Return "Could not prepare the blank map templates needed for New Map." & vbCrLf & vbCrLf &
                   "Folder: " & folder & vbCrLf &
                   "Error: " & ex.Message
        End Try

        ' Hide the folder so the install dir stays tidy. Best-effort — if the
        ' filesystem refuses (read-only, antivirus, etc.) we just skip it; the
        ' folder being visible is harmless, only cosmetic.
        If wroteAny Then
            Try
                Dim existing As FileAttributes = File.GetAttributes(folder)
                If (existing And FileAttributes.Hidden) <> FileAttributes.Hidden Then
                    File.SetAttributes(folder, existing Or FileAttributes.Hidden)
                End If
            Catch ex As Exception
                Debug.WriteLine($"EnsureBlankMapsExtracted: could not set Hidden attribute: {ex.Message}")
            End Try
        End If

        Return Nothing
    End Function

    ''' <summary>
    ''' Opens the embedded blankmaps.zip resource. Caller disposes the archive
    ''' (which also closes the underlying resource stream).
    ''' </summary>
    Private Function OpenEmbeddedBlankMapsZip() As ZipArchive
        Dim asm As Assembly = Assembly.GetExecutingAssembly()

        Dim zipResourceName As String = Nothing
        For Each resourceName As String In asm.GetManifestResourceNames()
            Dim lower As String = resourceName.ToLowerInvariant()
            If lower.EndsWith(".zip") AndAlso (lower.Contains("blank") OrElse lower.Contains("maps")) Then
                zipResourceName = resourceName
                Exit For
            End If
        Next

        If String.IsNullOrEmpty(zipResourceName) Then
            Throw New Exception("Could not find the embedded blankmaps.zip resource.")
        End If

        Dim resourceStream As Stream = asm.GetManifestResourceStream(zipResourceName)
        If resourceStream Is Nothing Then
            Throw New Exception($"Could not load resource stream for: {zipResourceName}")
        End If
        Return New ZipArchive(resourceStream, ZipArchiveMode.Read, leaveOpen:=False)
    End Function


    '''' <summary>
    '''' Show version numbers of the OP2 libraries
    '''' </summary>
    'Public Sub ShowLibraries()
    '    Dim targetAssemblies = {"OP2UtilityDotNet", "OP2MapJsonTools"}
    '    Dim dummy1 = GetType(OP2UtilityDotNet.OP2Map.Map)       ' Force usage so assembly gets loaded
    '    Dim dummy2 = GetType(OP2MapJsonTools.JsonExportFormat)
    '    For Each asm As Assembly In AppDomain.CurrentDomain.GetAssemblies()
    '        Try
    '            Dim name = asm.GetName()
    '            If targetAssemblies.Contains(name.Name) Then
    '                Debug.WriteLine($"{name.Name} - Version: {name.Version}")
    '            End If
    '        Catch ex As Exception
    '            ' Debug.WriteLine($"Error reading assembly info: {ex.Message}")
    '            MsgBox($"Error reading assembly info: {ex.Message}")

    '        End Try
    '    Next
    'End Sub

    ''' <summary>
    ''' Check that required DLLs are present beside the EXE.
    ''' Returns True if everything is present, False otherwise.
    ''' </summary>
    Public Function CheckRequiredLibraries(ParamArray requiredDlls() As String) As Boolean
        Dim exeDir As String = Path.GetDirectoryName(Application.ExecutablePath)

        Dim missing As New List(Of String)

        For Each dllName In requiredDlls
            Dim fullPath = Path.Combine(exeDir, dllName)
            If Not File.Exists(fullPath) Then
                missing.Add(dllName)
            Else
                'Debug.WriteLine("Found Library File: " & dllName)
            End If
        Next

        If missing.Count > 0 Then
            MsgBox(
            "The following required files are missing in the OP2Mapper3 folder:" &
            Environment.NewLine &
            String.Join(Environment.NewLine, missing),
            MsgBoxStyle.Critical,
            "Missing dependencies"
        )
            Return False
        End If

        Return True
    End Function

    ''' <summary>
    ''' Show version numbers of the specified OP2 libraries.
    ''' Call like: ShowLibrariesVersionInfo("OP2UtilityDotNet", "OP2MapJsonTools")
    ''' </summary>
    Public Sub ShowLibrariesVersionInfo()
        Dim targetAssemblies = {"OP2UtilityDotNet", "OP2MapJsonTools", "Newtonsoft.Json"}

        ' Force usage so assemblies get loaded
        Dim dummy1 = GetType(OP2UtilityDotNet.OP2Map.Map)
        Dim dummy2 = GetType(OP2MapJsonTools.JsonExportFormat)
        Dim dummy3 = GetType(Newtonsoft.Json.JsonArrayAttribute)

        For Each asm As Assembly In AppDomain.CurrentDomain.GetAssemblies()
            Try
                Dim name = asm.GetName()
                ' Simple contains check without LINQ
                For Each target In targetAssemblies
                    If String.Equals(name.Name, target, StringComparison.OrdinalIgnoreCase) Then
                        Debug.WriteLine($"{name.Name} - Version: {name.Version}")
                        Exit For
                    End If
                Next
            Catch ex As Exception
                MsgBox($"Error reading assembly info: {ex.Message}")
            End Try
        Next
    End Sub

End Module