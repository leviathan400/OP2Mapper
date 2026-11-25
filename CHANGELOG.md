# Changelog

All notable changes to OP2Mapper3 are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

Current release: 0.7.2 (build 0155).

### Added
- **Generate New Map (File menu, Ctrl+Shift+N).** Builds a whole map from a seed with op2maplib's map generator.
  - **Settings:** size, a template (Plains, Canyon, Desert, Islands, Ice or New Terra), the terrain mix, roughness, minimum biome size, symmetry (mirrored, rotated or four-way), cliffs, rocks and craters, spawn points and volcanoes.
  - **Features:** add valleys (optionally with a lava floor), mountains, plateaus, clearings and chokes, each with a position and size.
  - **Generate** builds the map straight into the main view as a new, unsaved map, with lava-possible areas set around volcanoes. **Roll** picks a new seed and generates again. A generated map that hasn't been edited is replaced without asking.
  - Spawn points and volcanoes are marked on the map as a guide. Outpost 2 maps don't store them, so they disappear when another map is opened.
  - The settings are remembered in `Mapper3.ini`.
- **OP2Launcher is bundled.** **File → Launch OP2 into DLL Picker** installs `op2launcher.exe` and `Launcher.dll` into the Outpost 2 `OPU` folder the first time it's needed, so there's nothing to copy by hand. If a different copy is already installed, you're asked before it's replaced.
- **Log file.** `OP2Mapper.log` next to the program records starts and closes, crashes, update checks and the updater, OP2Launcher installs, folder changes, and maps opened and saved. It's kept under 1 MB, with the previous part in `OP2Mapper.old.log`.
- **Terrain brush (new Terrain Brush tool window).** Pick a ground (Mud, Rock, Sand or Vegetation) and left-drag on the map. The edges where it meets other ground are filled in with the game's own transition tiles, so mud, rock and sand blend smoothly.
  - **Brush size:** 3 to 25 tiles, set in the window or with Ctrl + mouse wheel on the map. An outline shows the brush under the mouse.
  - **Clean Up Transitions...** re-picks the transitions across the whole map. It says how many tiles will change before doing anything.
  - **Undo:** each drag is one undo step, covering tiles and cell types. Cell types follow the new tiles, except the bottom row stays Impassable.
  - **Cliffs, walls, tubes and other structures** are never changed unless you paint directly over them.
  - **How it works:** the blending is done by op2maplib, a new shared library.
- **Draw Cliff (in the Terrain Brush window).** Turn on Draw Cliff, choose the high side (left or right of the line, north or south) and a wall height of 3 to 6, then click points on the map.
  - Points snap to the slopes cliffs can be built in, and a yellow line previews the cliff.
  - **Enter** or **Place Cliff** builds it from the game's mud or sand cliff tiles, with their cliff cell types. Rock and other grounds use mud cliffs.
  - **Backspace** removes the last point, **Esc** clears them, and clicking the first point again closes the cliff into a loop.
  - Each cliff is one undo step. Painting terrain next to a cliff leaves it in place.
- **Always-on-top control on every tool window.** Right-click a tool window's title bar (or press Alt+Space) for two options:
  - **Always On Top (This Window)** sets just that window.
  - **All Tool Windows Always On Top** sets all of them and matches View → Tool Windows Always On Top.

  Both are remembered in `Mapper3.ini`.
- **Tile Selection: Large Window and zoom.** The Tile Selection title-bar menu adds:
  - **Large Window,** a bigger window (about 30×26 tiles at 100%) for copying and pasting large areas.
  - **Zoom** levels of 100%, 75%, 50% and 25%, plus **Zoom to Fit Window**, to see more of a copy at once.

  Ctrl + mouse wheel steps through the zoom levels, and the current zoom shows in the window title. Both settings are remembered.

  Each new copy opens at the largest zoom that shows it whole, so a small copy is shown at 100%. If even 25% is too small, your current zoom is kept.
- **Tile Selection: Show Cell Types.** A toggle on both Tile Selection menus, the title-bar menu and the right-click menu. It shades the copy with its cell types, in the same colours as the CellTypeOverlay view. It is off by default and remembered.
- **Drag to select a copy, with the area shown on the map.** After Copy (Ctrl+C), you can drag from one corner to the other and release to copy. Clicking each corner still works too.
  - While you choose, a green rectangle with its size (for example "8x6") follows the mouse.
  - Once copied, the area is outlined in blue for a couple of seconds.
  - Dragging past the map's edge selects up to the edge.
- **Copy and paste cell types.** A paste now brings the copied area's cell types along with its tiles, so terrain behaviour such as cliffs, walls and passability moves with it.
  - Turn this off with **Edit → Paste Includes Cell Types**, which is on by default and remembered. When it's off, the target area keeps its own cell types.
  - The setting can be changed after copying, and it applies to the next paste.
  - Undo restores both the tiles and the cell types.
  - The map's bottom row always keeps its cell type, because it must stay Impassable.
- **`/op2dir` command-line option.** Start the mapper with `/op2dir "C:\Games\Outpost 2"` to set the Outpost 2 folder, and the first-run folder prompt is skipped. The folder is saved like one chosen in Settings. A map file can still be given as well.
  - If Settings is cancelled at startup without a folder set, the mapper now explains why and closes, instead of opening with no tilesets.

### Fixed

#### Data safety
- **Save no longer writes straight over the original file.** The map is written to a temp file, read back to check it, and only then swapped in, so a failed save leaves the existing file untouched.
- **New Map is marked unsaved.** Closing the app or opening another map now asks to save a newly created map.
- **New Map can no longer overwrite the open map.** If the blank template fails to load, nothing else happens. The unsaved-changes check runs again when you click OK, because the New Map window doesn't block the main window.
- **Blank map templates no longer appear in Recent Files.**
- **Exports never delete the previous image first.** Every image export is written to a temp file and replaces the old one only once it has been saved.

- **A map that fails to open leaves the current map untouched.** The new map and its renderer are fully built before anything is swapped in.
- **Settings checks typed paths before saving.** An Outpost 2 folder that doesn't exist is refused, and one that doesn't look like an install needs confirming. Missing working or tile-group folders are offered for creation.

#### Error reporting
- **A missing tileset folder is now explained.** You get a message listing the folders that were searched, instead of an unexplained map of placeholder tiles.
- **Blank map template problems are reported when New Map opens.** Missing or truncated templates are re-extracted. When the program folder isn't writable, for example under Program Files, they go to `%LOCALAPPDATA%\OP2Mapper\BlankMaps`.

#### Startup
- **A missing or blocked `updater.exe` no longer closes the app.** The app exits for an update only if the updater actually started.
- **The update check no longer freezes startup.** It runs in the background, so a slow or offline connection can't hold up the splash screen. If the check finishes before the main window opens, an update installs as before. If it finishes later, you're asked first and offered the chance to save.
- **Starting the mapper again while it's open now brings the open window forward.** A map given on the command line (for example by double-clicking a `.map` file) opens there, after the unsaved-changes check. Before, the second start quietly closed, which looked like a crash.
- **The splash screen shows the real version and build,** for example "Version 0.7.1 (build 0155)", instead of "0.07".

#### Tool windows
- **Tool windows remember their positions between runs, on any monitor.** Only Tile Set's position was ever saved. The other four were closed at exit without saving, so they came back piled up at the top-left. Positions are now saved when a window is moved and again when the app closes. Any window without a usable saved position, such as one last on a monitor that's no longer connected, gets the default layout. The others stay where they were. The main window also saves its position when moved, resized, maximized or restored, not only on exit.
- **Tool windows no longer pile up on a new install.** On screens narrower than 1,700 px they used to be stacked at the top-left corner. Now they're laid out in columns from the right edge without overlapping, and overlap as little as possible on very small screens. They open on the main window's screen, or another monitor if there is one.

#### Editing
- **Painting no longer stays stuck on after the bottom-row warning.** The drag also ends when the mouse moves with no button held.
- **Turning edit mode off now also ends paste and tile-group placement,** so a click can't change the map while edit mode is off.
- **Tile groups are checked before placement.** Groups that use tile mapping numbers the current map doesn't have are refused with an explanation.
- **Tile groups with missing header fields now load.** Only the `tiles` data is required, and missing optional fields such as `notes`, `bmp` and `name` show as N/A. Previously any missing field rejected the whole group. Groups whose rows have different lengths are now refused.
- **Cancelling a copy (Esc / Clear Tool) keeps the existing clipboard,** and Paste stays enabled.
- **Opening Cell Types no longer arms the first type.** Previously Fast Passable 1 was armed, so the first click on the map painted it.
- **Clicking the already-selected cell type works again,** after switching to a tile.
- **Clicks in the empty strip right of the tile grid are ignored,** instead of selecting the next row's first tile.
- **Ctrl+C behaves like Edit → Copy.** It respects the render-mode restriction and leaves paste or tile-group mode first, so the next click starts the copy.
- **The enclosed-area highlight updates after edits, undo and redo.** It no longer shows the areas as they were when it was turned on.

#### Undo / redo
- **A whole drag stroke is now one undo step,** instead of one step per tile.
- **Undo history is capped at 500 steps.**
- **Returning to the last saved state clears the `*`.** Undoing or redoing back to that state marks the map as saved again.

#### Export
- **Too-large exports get a clear message.** If the image would be too big at the chosen size, the message suggests a smaller size, instead of a cryptic GDI+ error.
- **Export Overview no longer touches `_small.jpg` files.** It renders each view in memory instead of overwriting the user's exports, shows one completion message instead of five, and always restores the render mode, even on failure.

#### Map view and mini map
- **No more hairline seams at 75%, 125% and 10% zoom.** Repainted areas now start exactly on a screen pixel, so they line up with the rest of the map instead of sitting up to half a pixel off.
- **The mini map's yellow box matches the main view exactly.** It used to be rounded out to whole tiles and could be a tile too big on each side.
- **Clicking the mini map jumps to the tile you clicked.** Clicks in the right or bottom half of a tile used to jump to the next tile over.

#### Right-click menus
- **The menu opens once on a click and not after a pan.** This applies to the map and the Tile Select window. Tile Select's click-versus-drag check also works now.

#### Resources and performance
- **Fixed bitmap leaks in previews.** The tile-group and paste previews leaked one bitmap per tile on every refresh.
- **Fixed smaller leaks:**
  - the tile property previews, which also no longer free an image while it's still shown
  - the selected tile image
  - pens in the tile-group and Tile Select previews
  - the old resource manager when settings change
- **Large copies show a paste preview.** The preview is drawn at a smaller tile size instead of silently failing. The Tile Select window does the same, where large copies used to leave it blank. Its BMP export is still full size.
- **Map repaint renders far less.** It covers only the area being redrawn plus a one-tile margin, clipped to the map. Previously it used a buffer that grew as you zoomed out and redrew most of the map each frame.
- **The cell-type text overlay is much cheaper.** Each label is drawn once and reused. When zoomed out below a readable size, the overlay shows colours instead of text.

### Changed
- **Folder settings are stored in `Mapper3.ini`** next to the program, in a `[Paths]` section, instead of the Windows user profile. Each copy of OP2Mapper keeps its own settings and can be moved as a folder. Existing folder choices are carried over automatically on first start. The default map folder chosen in Settings is now also kept between runs; it used to be reset to the Outpost 2 folder at every start.
- **Tidier sidebar, with a list of the map's tilesets.** The main window's sidebar is now grouped into titled sections: Map, Tilesets, Tile Under Cursor, View and Editing. The new Tilesets section lists every tileset BMP the loaded map uses, with its tile count. The section title gives the total and flags any tileset that couldn't be found. Long text ends in "…" and shows in full on hover, and the sidebar scrolls if the window is too short.
- A paint drag stops at the locked bottom row instead of continuing past it.
- At low zoom, the "Cell Type Overlay All" mode shows the colour overlay instead of unreadable labels.
