# Architecture Overview

This document describes how OP2Mapper3 is put together. OP2Mapper3 is a Windows desktop map editor for *Outpost 2: Divided Destiny*. It edits the tile graphics and cell types (passability) of `.map` files.

It is meant to give a new contributor, human or AI agent, a fast and accurate mental model of the codebase. Keep it up to date when the structure changes.

## 1. Project Structure

```
op2mapper/
├── OP2Mapper.sln                  # Visual Studio 2022 solution (one project)
├── README.md                      # Short project description and related projects
├── ROADMAP.md                     # Feature inventory and future plans
├── CHANGELOG.md                   # Notable changes per release
├── ARCHITECTURE.md                # This document
└── OP2Mapper/                     # The application (VB.NET, WinForms, .NET Framework 4.8)
    ├── OP2Mapper.vbproj           # Project file; references DLLs in ..\..\other\ (incl. op2maplib)
    ├── App.config, packages.config# Runtime config; NuGet: Newtonsoft.Json 13.0.4
    ├── ApplicationEvents.vb       # My.Application startup/shutdown hooks
    ├── My Project/                # Assembly info, resources, user settings, manifest
    ├── Resources/                 # Embedded images, icons and sounds
    ├── BlankMaps/blankmaps.zip    # Embedded blank .map templates (one per size)
    │
    │   ── Main window (one class split into partial files) ──
    ├── fMain.vb                   # Form setup, fields, menus/context menu wiring
    ├── fMain.Designer.vb          # Designer-generated layout
    ├── fMain.MapIO.vb             # Open / save / close / recent files, LoadMapFile
    ├── fMain.MapPanel.vb          # Map panel mouse input, painting, zoom/pan
    ├── fMain.Editing.vb           # Edit mode on/off, tool window lifecycle
    ├── fMain.UndoRedo.vb          # Command execution, undo/redo stacks, dirty flag,
    │                              #   keyboard shortcuts (ProcessCmdKey)
    ├── fMain.Clipboard.vb         # Copy region / paste mode and previews
    ├── fMain.TileGroups.vb        # Tile-group placement mode
    ├── fMain.Export.vb            # JPEG export and multi-mode overview image
    ├── fMain.MapAnalysis.vb       # Enclosed-area detection and fix (analysis by op2maplib)
    ├── fMain.Terrain.vb           # Terrain brush and Draw Cliff (op2maplib)
    ├── fMain.Generate.vb          # Shows a generated map in the main view; spawn/volcano markers
    ├── fMain.Menus.vb             # Menu item handlers
    ├── fMain.UI.vb                # Labels, tile preview, resize handling
    ├── fMain.Helpers.vb           # Small shared helpers (render-mode sync, lookups)
    │
    │   ── Rendering ──
    ├── MapRenderer.vb             # Viewport → Bitmap renderer, 5 render modes, grids
    ├── MapRenderer.Tilesets.vb    # Tileset BMP loading, palette conversion, tile cache
    ├── MapRenderer.CellTypeStyles.vb # Colours and labels per cell type
    │
    │   ── Editing model ──
    ├── Commands.vb                # Command pattern: per-tile, bulk, tile-group, composite
    ├── PasteTileMappings.vb       # Paste command
    ├── TileData.vb                # Tile info value type
    ├── GlobalVariables.vb         # Shared app state, constants, blank-map extraction
    ├── AppSettings.vb             # Folder settings (OP2Path, WorkingPath, Tilegroups) in Mapper3.ini
    ├── AppLog.vb                  # OP2Mapper.log next to the exe: start/close, crashes, updates, launcher installs, maps
    │
    │   ── Tool palettes and dialogs (shown in Edit Mode or from menus) ──
    ├── fTileSet.vb                # Tile picker (tool 1)
    ├── fCellTypes.vb              # Cell-type picker (tool 2)
    ├── fTileSelect.vb             # Copied-region viewer and tile-group export (tool 3)
    ├── fTileGroups.vb             # Tile-group library browser (tool 4)
    ├── fMiniMap.vb                # Mini-map with click-to-jump (tool 5)
    ├── fTerrain.vb                # Terrain Brush window: grounds, brush size, Draw Cliff (tool 6)
    ├── fNewMap.vb                 # New Map dialog (size, biome, name)
    ├── fGenerateMap.vb            # Generate New Map window (op2maplib map generator)
    ├── fSettings.vb               # Paths: OP2 folder, working folder, tile groups
    ├── fSplashScreen.vb           # Splash and startup update check
    ├── fAbout.vb, fLog.vb, fBrushTool.vb (stub)
    │
    │   ── Support modules ──
    ├── ToolPalette.vb             # Tool-window positions and recent files in Mapper3.ini
    ├── OP2Launcher.vb             # Launch Outpost 2 / open the game folder; installs the bundled op2launcher
    ├── OP2Launcher/               # Bundled op2launcher.exe + Launcher.dll (embedded resources)
    └── mUpdateChecker.vb          # Version check and updater.exe hand-off
```

## 2. High-Level System Diagram

OP2Mapper3 is a single-process, single-window desktop application with no server component.

```
                 ┌──────────────────────────────────────────────┐
  .map file ───► │ OP2UtilityDotNet  Map.ReadMap / Map.Write    │ ◄─── .map file
                 └───────────────┬──────────────────────────────┘
                                 │ Map object (tiles, cell types, tileMappings,
                                 │             tilesetSources)
                                 ▼
 ┌────────────────────────── fMain (main window) ───────────────────────────┐
 │  MapIO ─ loads/saves ─► currentMap ◄─ Commands (Execute/Undo) ◄─ UndoRedo │
 │                            │                      ▲                       │
 │                            ▼                      │ edits from mouse/keys │
 │                       MapRenderer ──► Bitmap ──► MapPanel_Paint           │
 │                            ▲                                              │
 │     Tileset BMPs from <OP2Path>\OPU\base\tilesets (palette-converted,     │
 │     sliced into 32×32 tiles, cached)                                      │
 └──────┬───────────────┬───────────────┬───────────────┬───────────────────┘
        │ GlobalVariables (shared selection/mode state) │
   fTileSet       fCellTypes      fTileSelect / fTileGroups      fMiniMap
   (pick tile)    (pick cell type) (clipboard / tile-group JSON)  (RenderFullMap thumbnail)
```

Main flows:

- **Load:** `LoadMapFile` reads the map with OP2UtilityDotNet. It builds a `MapRenderer`, which loads the map's tilesets, then resets undo, clipboard and view state.
- **Edit:** mouse input on the map panel creates `Command` objects. `ExecuteCommand` applies each one to `currentMap` and records it for undo; everything painted in one drag stroke is grouped into a `CompositeCommand`. The affected tiles are then invalidated and repainted.
- **Render:** `MapPanel_Paint` asks `MapRenderer.RenderMap` for the dirty region only, clipped to the map. It then draws overlays on top: selection, paste preview, tile-group preview, the enclosed-area highlight, the terrain brush outline, the cliff being drawn and a generated map's spawn and volcano markers.
- **Generate:** `fGenerateMap` runs op2maplib's generator in the background, then `ShowGeneratedMap` loads the blank template of that size and copies the result in.
- **Save:** `WriteMapSafely` writes a temp `.map`, reads it back to check it, then atomically replaces the target.
- **Tool palettes:** these are separate top-level forms. They talk to `fMain` through `GlobalVariables` (the active tile, cell type and tile group) and through a few public `fMain` members.

## 3. Core Components

### 3.1. Frontend

Name: OP2Mapper3 desktop UI

Description: The whole application is a WinForms UI with:

- **Main window (`fMain`):**
  - a scrollable, zoomable map panel
  - a property and status panel
  - File / Edit / Render / Image menus
- **Six tool palettes**, opened when Edit Mode is enabled: tiles, cell types, tile selection, tile groups, mini-map and Terrain Brush.
- **Dialogs:** New Map, Generate New Map, Settings and About.

Editing actions:

- paint tiles
- paint cell types
- copy and paste regions
- place saved tile groups
- paint grounds with blended transitions (Terrain Brush)
- draw cliffs
- auto-fix enclosed areas
- generate a whole map from a seed

Render modes, all of which can be exported as JPEG images:

- game view
- cell-type overlay
- labelled overlay
- basic cell-type map
- detailed cell-type map

Technologies: VB.NET, Windows Forms, GDI+ (System.Drawing), .NET Framework 4.8.

Deployment: Windows desktop executable (`OP2Mapper.exe`). It is placed next to the Outpost 2 install or any folder. It is updated in place by `updater.exe`.

### 3.2. Backend Services

There are no backend services. All logic runs in-process. The internal subsystems that play a "service" role are described below.

#### 3.2.1. Map model and editing (Commands / Undo)

Name: Command and undo/redo system

Description:

- `Commands.vb` defines the abstract `Command` (`Execute`, `Undo`, `Description`) and its implementations:
  - single-tile cell type
  - single-tile mapping
  - bulk cell types
  - tile-group placement
  - composite (a drag stroke)
  - paste (`PasteTileMappings.vb`)
- `fMain.UndoRedo.vb` owns the undo and redo stacks, capped at 500 steps.
- It also tracks the save point, so undoing back to the saved state clears the dirty flag.
- All map edits should go through `ExecuteCommand`. The exceptions are New Map's initial terrain fill and Generate New Map, which both create the starting state of a new map.

Technologies: VB.NET; the map object comes from OP2UtilityDotNet.

Deployment: In-process.

#### 3.2.2. Rendering (MapRenderer)

Name: MapRenderer

Description:

- Loads the tileset BMPs listed in the map's `tilesetSources` from `<OP2Path>\OPU\base\tilesets`, with fallbacks to `base\tilesets` and `tilesets`.
- Converts each palette using LockBits and slices it into 32×32 tile bitmaps, which it caches.
- `RenderMap(viewport, scale)` returns a new bitmap of the requested map region in the current render mode, with optional grids and coordinates. The caller owns that bitmap.
- `RenderFullMap` is used for export and the mini-map.
- `GetTileImageByIndices` returns a copy of a cached tile, which the caller must dispose.

Technologies: GDI+.

Deployment: In-process.

#### 3.2.3. Map analysis

Name: Enclosed-area finder

Description: `fMain.MapAnalysis.vb` finds areas that are cut off from the main (largest) passable region. The region analysis is done by op2maplib (`MapRegions`); the file keeps the UI. It can highlight those areas, or convert them to impassable in one bulk, undoable command.

Technologies: VB.NET, op2maplib.

Deployment: In-process.

#### 3.2.4. op2maplib (terrain, cliffs, regions, map generator)

Name: op2maplib (source: [OP2MappingLib](https://github.com/OutpostUniverse/OP2MappingLib))

Description:

- A C++ library with a C API, used through its .NET binding `Op2MapLib.Net.dll`. It loads `x86\op2maplib.dll` or `x64\op2maplib.dll` to match the process, and the Outpost 2 profile `op2maplib\op2.json` (grounds, transition tiles, cliff tiles, standard mapping table).
- `fMain.Terrain.vb` (Terrain Brush and Draw Cliff) builds a `BlendMap` from the current map at the start of each stroke and applies the returned changes as undoable commands.
- `fMain.MapAnalysis.vb` uses `MapRegions` for Find & Fix Enclosed Areas.
- `fGenerateMap.vb` and `fMain.Generate.vb` use `MapGenSettings` / `GeneratedMap`:
  - generation runs on a background thread;
  - the result is copied into a freshly loaded blank template of the same size, whose mapping table is the profile's standard table, so the generated mapping indices apply unchanged;
  - tiles, cell types and lava-possible cells are written, the bottom row is set Impassable, and the map is marked new and unsaved;
  - spawn points and volcanoes are drawn as markers only, because `.map` files don't store them.
- The profile is loaded once on first use (`GetTerrainProfile`). If the library or profile is missing, these features explain why and the rest of the app works.
- Version used: op2maplib 0.3.5. Since 0.3.5 the library also has mountains, gorges, valleys and plateaus, and a "learned" generator modelled on the original maps (`LearnedModel`, network weights in `profiles/op2-net.bin`, 4.5 MB). OP2Mapper doesn't use these yet: `op2-net.bin` isn't shipped, and the learned generator doesn't use the Features list.

Technologies: C++17 (native), P/Invoke (.NET Framework 4.8).

Deployment: Prebuilt DLLs from `..\..\other\`, copied next to the exe by the build.

#### 3.2.5. Update checker

Name: mUpdateChecker

Description:

- At startup the splash screen downloads a build number from the update server and compares it with `ApplicationBuild`.
- If the server's build is newer, it launches `updater.exe` and exits.
- If the updater can't be started, the app keeps running.

Technologies: `System.Net.WebClient` with a 5-second timeout.

Deployment: In-process; needs `updater.exe` next to the exe.

## 4. Data Stores

There is no database. All persistence is in local files.

### 4.1. Outpost 2 map files

Name: `.map` files

Type: Binary Outpost 2 map format, read and written by OP2UtilityDotNet.

Purpose: The document being edited.

Key Schemas/Collections:
- tile grid (mapping index and cell type per tile)
- `tileMappings` (tileset index and graphic index)
- `tilesetSources`
- width and height (OP2 map widths are powers of two)

### 4.2. Blank map templates

Name: `BlankMaps\blank_WxH.map`

Type: `.map` files extracted on first use from the embedded `blankmaps.zip` into a hidden `BlankMaps` folder next to the exe.

Purpose: The starting point for File → New Map and File → Generate New Map.

### 4.3. Tile groups

Name: Tile group library (`AppSettings.Tilegroups`, default `<exe>\Tilegroups`)

Type: JSON files, read and written with Newtonsoft.Json.

Purpose: Reusable rectangular stamps of tile mapping indices, plus a preview BMP.

Key Schemas/Collections:
- a header (`width`, `height`, `tileset`, `name`, `bmp`, `notes`)
- `tiles`, the mapping indices

Mapping indices are only meaningful for maps with a compatible mapping table. Placement checks that every index is in range.

### 4.4. Settings and window layout

Name: User settings and `Mapper3.ini`

Type:
- an INI file next to the exe
- .NET user settings (`My.Settings`), for one remaining option

Purpose:
- `Mapper3.ini` `[Paths]` holds the three folders, read and written through `AppSettings.vb`: `OP2Path` (Outpost 2 folder), `WorkingPath` (default map folder) and `Tilegroups`. Older versions kept them in `My.Settings`; the first read copies any value found there into the INI.
- `Mapper3.ini` `[Window]` holds tool-window positions, the "always on top" flags, the recent-files list and tool settings such as the terrain brush size and cliff options.
- `Mapper3.ini` `[GenerateMap]` holds the last Generate New Map settings (size, template, seed, mix, sliders, spawns, volcanoes); the feature list isn't saved.
- `My.Settings` still holds `AllowEditLastRow`.

### 4.5. Exported images

Name: `<map>_<RenderMode>[_suffix].jpg`, `<map>_overview.jpg`

Type: JPEG (quality 95), written next to the map.

Purpose: Map previews for sharing. They are written through a temp file, so a failed export never deletes the previous image.

## 5. External Integrations / APIs

Service Name: OP2UtilityDotNet
Purpose: Reading and writing `.map` files (`Map`, `CellType`, `ResourceManager`).
Integration Method: Referenced DLL (`..\..\other\OP2UtilityDotNet.dll`); source in [OP2UtilityDotNet](https://github.com/leviathan400/OP2UtilityDotNet), included in the GitHub repository as the submodule `lib/OP2UtilityDotNet` (used by CI).

Service Name: OP2MapJsonTools
Purpose: JSON map tooling; its namespace is imported and its types are referenced for version info. Most JSON map export is currently disabled.
Integration Method: Referenced DLL (`..\..\other\OP2MapJsonTools.dll`).

Service Name: op2maplib
Purpose: Terrain blending, cliffs, enclosed-area analysis and map generation (see 3.2.4).
Integration Method: Referenced DLL (`..\..\other\Op2MapLib.Net.dll`) plus native DLLs and profile in `..\..\other\op2maplib\`; source in [OP2MappingLib](https://github.com/OutpostUniverse/OP2MappingLib), included in the GitHub repository as the submodule `lib/OP2MappingLib` (used by CI).

Service Name: OP2Launcher
Purpose: Starting Outpost 2 into the DLL picker.
Integration Method: `op2launcher.exe` and `Launcher.dll` are embedded resources, installed into `<OP2Path>\OPU` when first needed; an MD5 compare decides whether an installed copy differs, and the user is asked before it's replaced.

Service Name: Newtonsoft.Json
Purpose: Tile-group JSON.
Integration Method: NuGet package (`packages.config`, restored to `..\packages`).

Service Name: Outpost 2 installation
Purpose: Tileset BMPs for rendering; launching the game.
Integration Method: File system under `AppSettings.OP2Path`. The game is launched through `OPU\OPULauncher.exe` or `OPU\op2launcher.exe` (`OP2Launcher.vb`).

Service Name: Update server (`update.outpostuniverse.org`)
Purpose: The latest build number.
Integration Method: HTTPS GET of a plain-text version file at startup (`mUpdateChecker.vb`).

Service Name: updater.exe
Purpose: Downloads the new build and replaces `OP2Mapper.exe`.
Integration Method: Started as a separate process with the exe name as its argument; the editor then exits.

## 6. Deployment & Infrastructure

Cloud Provider: None. The app is a standalone Windows desktop application.

Key Services Used:
- the update web server
- the Outpost 2 install on the user's machine

CI/CD Pipeline:
- GitHub Actions (`.github/workflows/build.yml`) builds Debug and Release on every push and pull request, on `windows-latest`. It builds op2maplib (native x86 and x64 DLLs with the library's C++ tests, plus `Op2MapLib.Net.dll`) and OP2UtilityDotNet from source from the `lib/OP2MappingLib` and `lib/OP2UtilityDotNet` git submodules, copies those and the prebuilt OP2MapJsonTools from `ci/deps` to `..\other`, restores NuGet, and uploads the Release build as an artifact. When a library is updated in `..\other`, also move its submodule pin (or update `ci/deps` for OP2MapJsonTools).
- Release builds for users are made locally with Visual Studio 2022 or MSBuild.
- Debug builds go to `..\..\build\`; Release builds go to `bin\Release\`.
- Builds are published by updating the version file on the update server.

Monitoring & Logging:
- There is no telemetry.
- `OP2Mapper.log` next to the exe (`AppLog.vb`) records starts and closes, crashes, update checks, launcher installs, folder changes, maps opened and saved, and generated maps. It rolls over at 1 MB to `OP2Mapper.old.log`.
- Other diagnostics use `Debug.WriteLine`, which is visible in the Visual Studio Output window.
- User-facing errors appear as message boxes.
- `fLog` is a placeholder log window.

## 7. Security Considerations

Authentication: None. The app is a local, single-user desktop app.

Authorization: None. Access is whatever the user's file-system permissions allow.

Data Encryption: The update check uses HTTPS. Local files are not encrypted.

Key Security Tools/Practices:
- **Process launches** use fixed executable names under known folders (`updater.exe` next to the exe, the OP2 launcher under `OP2Path`).
- **Blank-map extraction** uses each zip entry's file name only, so path traversal from the embedded archive isn't possible.
- **The version response** must parse as an integer. Anything else, such as a captive portal or an error page, is ignored rather than treated as a newer build.
- **Auto-update has no signature check:** `updater.exe` is trusted as-is. Treat the update server as the trust boundary.

## 8. Development & Testing Environment

Local Setup Instructions:

1. Install Visual Studio 2022 (or the 2022 Build Tools) with the .NET desktop workload.
2. Make sure the sibling folder `..\..\other\` exists. It contains `OP2UtilityDotNet.dll`, `OP2MapJsonTools.dll` and `Op2MapLib.Net.dll`, with op2maplib's native DLLs and profile in `other\op2maplib\`.
3. Restore NuGet packages (Newtonsoft.Json 13.0.4 into `..\packages`).
4. Build `OP2Mapper.sln`.
5. On first run, set the Outpost 2 folder in File → Settings. Tilesets are loaded from it.

Testing Frameworks: None. There are no automated tests; changes are verified by hand in the running app.

Code Quality Tools:
- `Option Explicit On`.
- `Option Strict Off`. Implicit-conversion and late-binding warnings are suppressed in the project file (`NoWarn`).
- There is no linter or formatter.

## 9. Future Considerations / Roadmap

See [ROADMAP.md](ROADMAP.md) for the full plan. Architecturally significant items:

**Known architectural debt**
- **Mode state is scattered.** Edit, paste, copy, tile-group placement and drag modes are tracked in separate globals and fields, and each exit path resets a different subset, which caused most mode-related bugs. Moving to a single explicit "active tool" state machine would make this robust.
- **`GlobalVariables` is mutable shared state** used by every form. Tool windows also reach `fMain` and each other through reflection on private members.
- **`Option Strict Off`** hides type errors until run time.
- **Build dependencies sit outside the repository** (`..\..\other\`). A stale copy of `OP2UtilityDotNet.dll` in the project folder doesn't match the referenced version.
- **No DPI awareness:** the manifest's `dpiAware` block is commented out.
- **Some failures only reach `Debug.WriteLine`,** so the user never sees them: tileset folder missing, blank-map extraction failing.

**Planned major changes** (ROADMAP Tier 7)
- multi-document tabs (needs the `GlobalVariables` refactor)
- dark mode
- high-DPI support
- .NET 8 migration
- an incremental VB.NET → C# port
- localization

**Significant upcoming features**
- cell-type and pathing tools
- map inspection and diff
- a "Test in OP2" game integration

## 10. Project Identification

Project Name: OP2Mapper3 (assembly `OP2Mapper`, version 0.7.2, build 0156)

Repository URL: https://github.com/leviathan400/OP2Mapper

Primary Contact/Team: leviathan (Outpost Universe)

Date of Last Update: 2026-11-25

## 11. Glossary / Acronyms

- **OP2**: *Outpost 2: Divided Destiny*, the game whose maps this tool edits.
- **OPU**: Outpost Universe, the community that maintains OP2 and its tools. Also the `OPU` folder inside an OP2 install.
- **Tile mapping**: An entry in a map's `tileMappings` table that pairs a tileset index with a graphic index. Each tile stores a mapping index, not an image directly.
- **Tileset / well BMP**: The game's tile graphics files (`wellXXXX.bmp`), sliced into 32×32 tiles.
- **Cell type**: A tile's passability or terrain class, such as Fast Passable, Impassable, Tube, Wall or Rubble. It is separate from the tile's graphic.
- **Bottom row**: The last map row, which the game doesn't render. By convention it is all Impassable, and editing it is locked unless `AllowEditLastRow` is set.
- **Tile group**: A saved rectangular stamp of mapping indices, stored as JSON, that can be placed onto a map.
- **Edit Mode**: The state in which the tool palettes are open and map edits are allowed.
- **Render mode**: One of the five map views: GameMap, CellTypeOverlay, CellTypeOverlayAll, CellTypeBasic and CellTypeAll.
- **Enclosed area**: A passable region that can't be reached from the main passable area.
- **GDI+**: The Windows 2-D graphics API behind `System.Drawing`.
