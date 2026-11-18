# OP2Mapper

![Screenshot](https://images.outpostuniverse.org/OP2Mapper3.png)

OP2Mapper3 is a modern map editor for **Outpost 2: Divided Destiny**. It opens
and saves the game's `.map` files and lets you edit their tiles and cell types,
with painting tools that understand the game's terrain.

**Current version: 0.7.0.** Downloads are on the [Releases](https://github.com/leviathan400/OP2Mapper/releases) page, and the [changelog](CHANGELOG.md) lists every change.

## Features

### Terrain tools
- **Terrain Brush:** paint mud, rock, sand or vegetation. Where grounds meet,
  the brush fills in the game's own transition tiles so the edges blend.
  Brush size is 3–25 tiles (Ctrl + mouse wheel). **Clean Up Transitions**
  redoes the transitions across the whole map.
- **Draw Cliff:** click points on the map, choose the high side and a wall
  height of 3 to 6, then press Enter. The cliff is built from the game's mud
  or sand cliff tiles, with the right cliff cell types. Click the first point
  again for a closed plateau.
- **Cliffs, walls and tubes are left alone** unless you paint directly over them.

### Editing
- **Tile painting:** click or drag-paint any tile from any loaded tileset.
- **Cell types:** paint all 32 Outpost 2 cell types, which control passability, cliffs, walls and tubes.
- **Copy and paste:** drag to select an area. A preview follows the mouse
  while pasting, and pastes bring the area's cell types along with its tiles.
- **Tile groups:** a library of ready-made multi-tile shapes. Export your own copies to it too.
- **Undo and redo** for every edit. A whole drag or cliff is one step.
- **Find & Fix Enclosed Areas:** find passable areas that units can never
  reach, highlight them, and turn them impassable in one undoable step.

### Viewing
- **Five render modes:** the game's look, or cell-type overlays with colours or labels.
- **Zoom** from 10% to 500% (mouse wheel) and right-drag panning.
- **Grid, large-grid and coordinate overlays.**
- **Mini map** with click-to-jump and a drag-to-scroll viewport.
- **Sidebar** with map details, the tilesets the map uses, and the tile under the mouse.

### Tool windows
Tile Set, Cell Types, Tile Selection, Tile Groups, Mini Map and Terrain Brush.
They snap to each other and to screen edges, remember their positions across
monitors, and can each be set to stay on top.

### Export and launch
- **Image export:** full size, 1/8, 1/16 or a custom scale, with or without grids.
  There's also a combined overview image of several render modes.
- **Screenshot** of the current view (F12).
- **Launch Outpost 2** or open the game folder from the menu.

### Safe by design
- Saves write to a temporary file and check it before replacing the map, so
  a failed save never damages the original. Image exports work the same way.
- You're asked before unsaved changes are lost.
- Updates are checked for in the background at startup.

## Getting started

**Requirements:** Windows, .NET Framework 4.8, and an Outpost 2 installation
(the map's tilesets are loaded from the game folder).

1. Download the latest release and unzip it.
2. Run `OP2Mapper.exe`. The first time, choose your Outpost 2 folder, or start
   it with `OP2Mapper.exe /op2dir "C:\Games\Outpost 2"`.
3. Open a map with **File → Open**, or start one with **File → New**.
4. Click **Enable Edit Mode** to show the tool windows and start editing.

Settings and window positions are saved in `Mapper3.ini` next to the program.

## Keyboard shortcuts

| Keys | Action |
|---|---|
| Ctrl+N / Ctrl+O / Ctrl+S | New / open / save map |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Ctrl+C / Ctrl+V | Copy an area / paste |
| Ctrl+B | Place the selected tile group |
| Ctrl+G / Ctrl+L / Ctrl+D | Toggle grid / large grid / coordinates |
| Ctrl+E / Ctrl+R | Toggle zoom / reset zoom |
| Ctrl + mouse wheel | Terrain brush size |
| Enter / Backspace / Esc | Draw Cliff: place / remove last point / clear |
| F12 | Screenshot |

## Building from source

- **Tools:** Visual Studio 2022 (or the 2022 Build Tools) with the .NET desktop
  development workload. The app is VB.NET WinForms on .NET Framework 4.8.
- **Libraries:** the project references prebuilt DLLs in a folder named `other`
  next to the repository folder (`..\other` from the repository root):
  - `OP2UtilityDotNet.dll` and `OP2MapJsonTools.dll`;
  - `Op2MapLib.Net.dll`, plus op2maplib's native DLLs and profile in
    `other\op2maplib\` (`x86\`, `x64\` and `op2.json`).
- **NuGet:** restore Newtonsoft.Json 13.0.4, then build `OP2Mapper.sln`.

[ARCHITECTURE.md](ARCHITECTURE.md) describes the code, and
[ROADMAP.md](ROADMAP.md) lists ideas for future features.

## Related projects

- [OP2MissionEditor](https://github.com/leviathan400/OP2MissionEditor): a newer map editor from 2019
- [OP2Mapper2](https://github.com/leviathan400/OP2Mapper2): the original OPU map editor from 2005
- [OP2UtilityDotNet](https://github.com/leviathan400/OP2UtilityDotNet): .NET library for working with Outpost 2 files
