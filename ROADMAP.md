# OP2Mapper3 — Roadmap

A living document covering current state and planned features.

---

## Part 1: What it has today (v0.7.1)

### Map operations
- **Open / Save / New** `.map` files with 10 embedded blank-map templates (64×64 up to 512×256)
- **New Map** — blank, or filled with random mud, sand or rock tiles
- **Generate New Map** (Ctrl+Shift+N) — whole maps from a seed via op2maplib: templates (Plains, Canyon, Desert, Islands, Ice, New Terra), terrain mix, roughness, symmetry, cliffs, rocks and craters, spawn points, volcanoes with lava-possible zones, and placed features (valleys, mountains, plateaus, clearings, chokes); Roll tries another seed
- **Safe saves** — written to a temp file, read back, then swapped in; image exports the same way
- **Unsaved-changes prompt** on close / new / open / generate (Yes / No / Cancel)
- **Bottom-row protection** — the special impassable last row can't be edited
- **`/op2dir` command-line option** and a map path argument; a second launch hands the map to the running copy

### Painting & selection
- **Tile painting** — click to place or drag-paint
- **Cell-type painting** — full set of 32 OP2 cell types
- **Terrain Brush** — paint mud, rock, sand or vegetation (3–25 tiles) with the game's transition tiles filled in automatically; Clean Up Transitions for the whole map
- **Draw Cliff** — click points, pick the high side and wall height 3–6; built from the game's mud or sand cliff tiles with cliff cell types
- **Tile group placement** — multi-tile shapes; **Ctrl+B** shortcut
- **Copy / paste** — drag to select, live preview, cell types copied with the tiles (optional)
- **Find & Fix Enclosed Areas** — highlight passable areas units can't reach, make them impassable in one step
- **Undo / redo** for every edit type; a drag, a cliff or a fix is one step; 500-step cap

### Rendering & navigation
- **5 render modes** — GameMap, CellTypeOverlay (translucent), CellTypeOverlayAll (with text), CellTypeBasic, CellTypeAll
- **Mouse-wheel zoom** 10% – 500%, **right-drag pan**
- **Small grid / large grid / coordinate** overlays (Ctrl+G/L/D)
- **Dirty-rect repaints** — edits repaint only the affected tiles
- **Sidebar** — map details, the tilesets the map uses, the tile under the mouse

### Tool palettes (Edit Mode)
- **Tile Set, Cell Types, Tile Selection** (large window, zoom, show cell types), **Tile Groups, Mini-map, Terrain Brush**
- **Window snapping** to each other and to screen edges, **per-window or global always-on-top**
- **First-run layout** places them side by side without overlapping, on another monitor if there is one
- **Positions persist** between runs via `Mapper3.ini`, on any monitor

### Export & launch
- **JPG image export** — Full / 1/8 / 1/16 / custom scale, with or without grids; **overview** composite of several render modes
- **Screenshot** of the current view (F12)
- **Launch Outpost 2**, **Launch OP2 into DLL Picker** (installs the bundled op2launcher when needed), open the game folder

### Persistence & diagnostics
- **Mapper3.ini** next to the exe — `[Paths]` folders, `[Window]` positions and tool settings, `[GenerateMap]` generator settings, recent files
- **OP2Mapper.log** next to the exe — starts, closes, crashes, updates, launcher installs, maps opened, saved and generated
- **Startup update check** in the background; `updater.exe` hand-off when a newer build is available
---

## Part 2: Roadmap — Suggested future features

Effort scale: **S** ≤ 1 day · **M** ~1 week · **L** ~2-4 weeks · **XL** = major project.

### Tier 1 — Quality-of-life wins (close out the open backlog)

| Feature | Effort | Why |
|---|---|---|
| **Auto-save** every N minutes to `.autosave.map` next to current map | S | Crash insurance — the biggest user-facing risk we still have |
| **Backup-before-save** option (`.bak` next to overwritten file) | S | Already on the review list; a half-hour |
| **`ClearClipboardData` walks OpenForms** — cache the fTileSelect reference instead | S | Last open item from the original code review |
| **Copy-confirmation timer verification** — final review item | S | Verify it doesn't clobber active tool selection |
| **Drag-drop a .map onto the main window** to open it | S | Standard UX expectation |
| **Open-in-Explorer button** in Settings for each path | S | One-click "show me where this lives" |
| **Map metadata panel** — name / author / description / version (write to map file or sidecar `.meta.json`) | M | Foundational for sharing |

### Tier 2 — Editor brushes & smart painting

These are where you'd start really differentiating from existing OP2 tools.

| Feature | Effort | Why this is novel |
|---|---|---|
| **Brush sizes** (1×1, 3×3, 5×5, 7×7) and **brush shapes** (square, circle, line) | M | No OP2 editor has variable brushes. *Partly done: the Terrain Brush is 3–25 tiles; tile and cell-type painting are still 1×1.* |
| **Flood fill** (paint-bucket on contiguous tiles of same mapping) | M | Classic painting tool, none of the OP2 editors have it |
| **Random fill within selection** — weighted picks from a tile pool (e.g. "85% grass, 10% rocks, 5% bushes") | M | Speeds up natural-looking terrain massively |
| ~~**Smart brush / auto-adjacency**~~ — **done in 0.7 as the Terrain Brush** (op2maplib blending) | L | — |
| **Magic wand** select (connected tiles of same mapping) | M | Pairs with flood fill |
| **Mirror / rotate** the current selection or clipboard | M | Lets builders make symmetric bases quickly |
| **Selection arithmetic** — Shift = add, Ctrl = subtract, Alt = intersect | M | Standard image-editor feature |

### Tier 3 — Cell-type / pathing tooling

OP2 cell types control vehicle pathing. There's a real game-design opportunity here.

| Feature | Effort | Why |
|---|---|---|
| **Pathing visualizer** — pick a tile, see which other tiles a unit could reach (BFS from selection, colour-coded by reachability) | M | Unique to OP2 maps; nobody's built this |
| ~~**Connected-region highlight**~~ — **done: Find & Fix Enclosed Areas** | M | — |
| **Cell-type heatmap** — overlay showing density of impassable / slow-passable tiles | S | One-shot debug visual |
| **Bulk cell-type "Set all in selection"** — currently you paint one at a time | S | Quick fix for big areas |
| **Cell-type templates** — pre-built patterns ("city base footprint", "lava channel") | M | Speed up common layouts |

### Tier 4 — Sharing, collaboration, library

| Feature | Effort | Why |
|---|---|---|
| **Map browser panel** — visual grid of `.map` files in `WorkingPath`, each with thumbnail (using mini-map renderer) | M | Standard for asset browsers; OP2 has nothing like it |
| **Map preview thumbnails in Recent Files menu** | S | Tiny visual polish |
| **Tile group sharing** — single-click upload to OPU community server | L | Needs server-side support but huge community win |
| **Map upload / download** from in-app community gallery | L | Same, with screenshots |
| ~~**Inline screenshots**~~ — **done: F12** | S | — |

### Tier 5 — Inspection and diff

| Feature | Effort | Why |
|---|---|---|
| **Tile inspector** — click any tile, see mapping index, tileset, image index, cell type, neighbors | S | Faster than the props panel for power users. *Partly done: the sidebar's Tile Under Cursor shows all but the neighbours.* |
| **Find tile by mapping index** — "highlight every tile that uses mapping 1206" | M | Debugging tool |
| **Find / replace** — swap every mapping X for mapping Y (with preview) | M | Refactoring tile placement |
| **Statistics panel** — count of each cell type, each tileset, each mapping | S | Quick map analysis |
| **Map validator** — flag orphaned tilesets, missing tile mappings, impassable corridors, etc. | M | Builds confidence before publishing a map |
| **Diff mode** — open two `.map` files side-by-side, highlight changed tiles | L | Reviewing other people's maps, or your own across versions |
| **Compare-with-disk** — see what's changed since last save | M | Pre-commit review for map authors |

### Tier 6 — Procedural / scripting

| Feature | Effort | Why |
|---|---|---|
| ~~**Procedural terrain generation**~~ — **done in 0.7.1: Generate New Map** (op2maplib map generator) | L | — |
| **Lua scripting console** — run user scripts that walk the map (place tiles, paint cell types, generate features) | L | Power users can do anything; no other OP2 editor has this |
| **Plugin system** — load `.dll` files that extend the menu, add render modes, or add brushes | XL | Long-term extensibility |
| **Macros** — record a sequence of edits, replay anywhere | M | Mini-scripting without code |

### Tier 7 — Modernization

| Feature | Effort | Why |
|---|---|---|
| **Multi-document tabs** — open multiple maps at once | L | Standard editor UX; needs the GlobalVariables refactor from earlier review |
| **Dark mode** — themed colors throughout | M | Modern Windows expectation |
| **High-DPI awareness** — clean rendering on 4K monitors | M | Currently a bit blurry at high DPI |
| **.NET 8 migration** — better GC, JIT, modern runtime | M | .NET Framework 4.8 mainstream support ends 2026 |
| **VB.NET → C# port** | XL | Onboards more contributors; can be incremental |
| **Localization** — strings to resource files for translation | M | Community translations once popular |

### Tier 8 — Game integration

| Feature | Effort | Why |
|---|---|---|
| **"Test in OP2"** menu — saves the map and launches OP2 with it | M | Iteration speed for map authors. The bundled op2launcher install (used by Launch OP2 into DLL Picker) is the first step. |
| **OP2MissionEditor link** — open current map in MissionEditor and back | M | Cross-tool integration |
| **Unit / structure placement preview** — overlay where common buildings would fit | M | Composition-aware mapping. Generate New Map's spawn-point markers are a start. |
| **Resource placement** — show ores, vents, the things mission scripts spawn | M | Currently invisible in the editor |

### Tier 9 — Polish

| Feature | Effort | Why |
|---|---|---|
| **Built-in tutorial** — guided first-use walkthrough | M | Lower barrier to entry for new map makers |
| **Sample maps** — pre-built example maps shipped with the .exe | S | Show what's possible |
| **Sound effects** — already bundled (`earthwrk.wav`, `lab_3.WAV`), behind on/off setting | S | Polish, since the WAVs are there |
| **Animated tile preview** — if OP2 has animated tile frames | M | Need to check if OP2 tilesets support this |
| **Mini-map render modes** — let user pick what the mini-map shows (game / cell-type / etc.) | S | Currently always GameMap |
| **Tooltip hints** — "first time you've used the mini-map" style helpers | S | Helps new users discover features |

---

## Part 3: How I'd order the next 6 months

> **Status (0.7.1, 2026-11-23):** the releases didn't follow this order. Shipped so far: the terrain brush, Draw Cliff, Find & Fix Enclosed Areas (from v0.8 / v0.7 below), map generation (v0.8), F12 screenshots, safe saves, the log file and the bundled launcher. Still open from v0.5–v0.7: auto-save, backup-before-save, drag-drop open, flood fill, magic wand, mirror/rotate, find/replace, statistics and the map validator.

Best-case order if you want each release to land with a coherent theme:

### v0.5 — "Polish & Robustness" (~2 weeks of evening work)
- All Tier 1 items
- Sample map(s) bundled
- Wire up sound effects (toggleable)

This is "shore up the foundation" — fewer flashy features, more "feels solid".

### v0.6 — "Smart Brushes" (~1 month)
- Brush sizes/shapes
- Flood fill
- Random fill with weighted pools
- Magic wand select
- Mirror/rotate selection

This is your **"first OP2 editor with real painting"** release. Big community impact.

### v0.7 — "Inspection" (~2-3 weeks)
- Tile inspector
- Find/replace mapping
- Statistics panel
- Map validator
- Connected-region highlight (pathing visualizer step 1)

Power-user release. Good for serious map authors.

### v0.8 — "Smart Terrain" (~1-2 months — this is the big one)
- Smart brush / auto-adjacency
- Cell-type templates
- Procedural terrain generation (basic)

This is **"OP2Mapper does what no other tool can"**. Single feature with the biggest "wow".

### v0.9 — "Library & Sharing"
- Map browser panel
- Map metadata
- Tile group sharing infrastructure (server-side prep)
- Community upload (if server's ready)

### v1.0 — "Tabs & Modernization"
- Multi-document tabs (requires the GlobalVariables → MapEditorState refactor)
- Dark mode
- Sample tutorial

### Beyond v1.0 — "Power tools"
- Lua scripting
- Plugin system
- .NET 8 migration
- C# port (optional)

---

## Part 4: Big-bet ideas worth thinking about

A few that aren't in the tiers above because they're genuinely novel and worth a separate think:

- **"Map AI" — describe what you want, the editor generates it.** "Make a 256×128 lava map with two starting positions and a river running south-to-north." Could be local heuristics, doesn't need actual ML. This would be **completely unique** in the OP2 space. *Generate New Map already does the heuristic part from settings; a text front-end could fill those settings in.*
- **Time-of-day rendering preview** — if OP2's renderer has lighting variation, show what the map looks like at different times. Probably needs OP2 source-engine knowledge.
- **Multiplayer-fair-spawn checker** — analyze the map, score how balanced the starting positions are (resources reachable, sight lines, choke points). Important for multiplayer maps.
- **Replay map review** — if OP2 saves replay files, overlay player movement on the map post-game. Long shot; probably needs OP2 modding.

---

## How to use this document

- **Pick a tier** when you have a free evening / weekend.
- **Stay in one tier per release** — keeps each version's identity clear.
- **Tier 2 + Tier 4 + Tier 8** is what would make this the **dominant OP2 tool**. Everything else is bonus.
- **Tier 6 (scripting)** is the long-term moat — once users can program the editor, it becomes irreplaceable.
