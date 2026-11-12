# OP2Mapper — Roadmap

A living document covering current state and planned features.

---

## Part 1: What it has today (v0.4.x)

### Map operations
- **Open / Save / New** `.map` files with 10 embedded blank-map templates (64×64 up to 512×256)
- **Biome-based new map** — Blank, TerrainType 0/1/2 fill the new map with random terrain tiles
- **Save As semantics** — Save dialog defaults to working dir + current filename, `currentMapFile/Path` updated after Save As
- **Unsaved-changes prompt** on close / new / open / re-open (Yes / No / Cancel)
- **Bottom-row protection** — the special impassable last row can't be edited

### Painting & selection
- **Tile painting** — click to place or drag-paint
- **Cell-type painting** — full set of 32 OP2 cell types
- **Tile group placement** — multi-tile shapes; **Ctrl+B** shortcut
- **Copy / paste rectangular regions** with live semi-transparent preview
- **Undo / redo** for every edit type (Command pattern across single-tile, cell-type, paste, and group placement)

### Rendering & navigation
- **5 render modes** — GameMap, CellTypeOverlay (translucent), CellTypeOverlayAll (with text), CellTypeBasic, CellTypeAll
- **Mouse-wheel zoom** 10% – 500%
- **Right-drag pan** with ClampOffsets (128 px overscan margin)
- **Small grid / large grid / coordinate** overlays, individually toggleable (Ctrl+G/L/D), configurable spacing
- **Selection rectangle** at the current tile
- **Dirty-rect repaints** — most edits only repaint affected tile(s), not the whole panel
- **Status-bar coordinates** live-updated on mouse-move (1-indexed)

### Tool palettes (Edit Mode)
- **Tile Set** — pick tiles from any loaded tileset
- **Cell Types** — pick from 32 cell types
- **Tile Selection** — preview of the current clipboard
- **Tile Groups** — folder-based browser with subfolders + `(Default)` for loose JSONs + auto-selecting `Exported\` after export
- **Mini-map** — photo-real GameMap thumbnail with click-to-jump + drag-to-scrub viewport, debounced refresh on edits
- All five palettes have **window snapping** (form-to-form and to screen edges) with **Ctrl-bypass for free move**
- **First-run smart layout** — multi-monitor aware (tools on secondary screen) or right-side dock on single screen
- **Positions persist** between runs via `Mapper3.ini`

### Export
- **JPG image export** — Full / 1/8 / 1/16 / custom scale, all grid-free by default
- **Full Size (Showing Grid)** and **Full Size (Showing Large Grid)** explicit overlay variants
- **Overview export** — multi-mode composite image
- **Tile group export** — writes BMP + JSON pair to `<Tilegroups>\Exported\`, refreshes the Tile Groups window automatically

### Persistence
- **Mapper3.ini** next to .exe — tool window positions, main window position/size/state, recent files
- **user.config** (`My.Settings`) — OP2Path, WorkingPath, Tilegroups path
- **Recent Files** — last 5 opened maps, with dead-link cleanup
- **Embedded resources** — `blankmaps.zip` (extracted to hidden folder on first use), biome BMPs, icons

### Update flow
- **Startup version check** against `update.outpostuniverse.org/OP2Mapper.htm` (5 s timeout)
- **Updater.exe** handoff if newer build is available
- **Graceful fallback** if `updater.exe` missing or server unreachable

### Polish
- **Maximize / minimize / restore** state persists
- **Multi-monitor disconnect** safely handled (off-screen positions get defaults)
- **Window title** shows `* App - mapname.map` when dirty
- **Keyboard shortcuts**: Ctrl+N, O, S, Z, Y, C, V, G, L, D, E, R, B
- **Custom .ico** (HD)

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
| **Brush sizes** (1×1, 3×3, 5×5, 7×7) and **brush shapes** (square, circle, line) | M | No OP2 editor has variable brushes |
| **Flood fill** (paint-bucket on contiguous tiles of same mapping) | M | Classic painting tool, none of the OP2 editors have it |
| **Random fill within selection** — weighted picks from a tile pool (e.g. "85% grass, 10% rocks, 5% bushes") | M | Speeds up natural-looking terrain massively |
| **Smart brush / auto-adjacency** — paint "grass" and the editor picks the right tile variant based on neighbors (matches OP2's 16+ grass tiles to terrain edges automatically) | L | This alone would be huge — basically how Tiled / RPG Maker handle terrain |
| **Magic wand** select (connected tiles of same mapping) | M | Pairs with flood fill |
| **Mirror / rotate** the current selection or clipboard | M | Lets builders make symmetric bases quickly |
| **Selection arithmetic** — Shift = add, Ctrl = subtract, Alt = intersect | M | Standard image-editor feature |

### Tier 3 — Cell-type / pathing tooling

OP2 cell types control vehicle pathing. There's a real game-design opportunity here.

| Feature | Effort | Why |
|---|---|---|
| **Pathing visualizer** — pick a tile, see which other tiles a unit could reach (BFS from selection, colour-coded by reachability) | M | Unique to OP2 maps; nobody's built this |
| **Connected-region highlight** — find isolated areas (no path to main play space) | M | Catches map bugs before playtest |
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
| **Inline screenshots** — F12 to capture current view to clipboard / disk | S | Useful for forum posts |

### Tier 5 — Inspection and diff

| Feature | Effort | Why |
|---|---|---|
| **Tile inspector** — click any tile, see mapping index, tileset, image index, cell type, neighbors | S | Faster than the props panel for power users |
| **Find tile by mapping index** — "highlight every tile that uses mapping 1206" | M | Debugging tool |
| **Find / replace** — swap every mapping X for mapping Y (with preview) | M | Refactoring tile placement |
| **Statistics panel** — count of each cell type, each tileset, each mapping | S | Quick map analysis |
| **Map validator** — flag orphaned tilesets, missing tile mappings, impassable corridors, etc. | M | Builds confidence before publishing a map |
| **Diff mode** — open two `.map` files side-by-side, highlight changed tiles | L | Reviewing other people's maps, or your own across versions |
| **Compare-with-disk** — see what's changed since last save | M | Pre-commit review for map authors |

### Tier 6 — Procedural / scripting

| Feature | Effort | Why |
|---|---|---|
| **Procedural terrain generation** — Perlin noise / cellular automata fills based on a biome preset | L | Generates "interesting" maps automatically |
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
| **"Test in OP2"** menu — saves the map and launches OP2 with it | M | Iteration speed for map authors |
| **OP2MissionEditor link** — open current map in MissionEditor and back | M | Cross-tool integration |
| **Unit / structure placement preview** — overlay where common buildings would fit | M | Composition-aware mapping |
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

- **"Map AI" — describe what you want, the editor generates it.** "Make a 256×128 lava map with two starting positions and a river running south-to-north." Could be local heuristics, doesn't need actual ML. This would be **completely unique** in the OP2 space.
- **Time-of-day rendering preview** — if OP2's renderer has lighting variation, show what the map looks like at different times. Probably needs OP2 source-engine knowledge.
- **Multiplayer-fair-spawn checker** — analyze the map, score how balanced the starting positions are (resources reachable, sight lines, choke points). Important for multiplayer maps.
- **Replay map review** — if OP2 saves replay files, overlay player movement on the map post-game. Long shot; probably needs OP2 modding.

---

## How to use this document

- **Pick a tier** when you have a free evening / weekend.
- **Stay in one tier per release** — keeps each version's identity clear.
- **Tier 2 + Tier 4 + Tier 8** is what would make this the **dominant OP2 tool**. Everything else is bonus.
- **Tier 6 (scripting)** is the long-term moat — once users can program the editor, it becomes irreplaceable.
