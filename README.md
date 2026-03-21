# DetMap

A deterministic, inspectable in-memory spatial database for 2D simulation worlds built on C# / Unity.

DetMap is built for projects where scene hierarchies stop being a useful debugging tool: city-builders, RTS games, logistics sims, military sims, and other data-heavy worlds with hundreds or thousands of rows of state.

Simulation math uses **Fix64** fixed-point arithmetic, and database values are restricted to deterministic scalar types (`byte`, `int`, `Fix64`), so the same input produces the same state across platforms.

---

## Table of Contents

- [Why DetMap](#why-detmap)
- [Modeling Data](#modeling-data)
- [Architecture](#architecture)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Layers](#layers)
  - [DetValueLayer\<T\> — dense value grid](#detvaluelayert--dense-value-grid)
  - [DetBitLayer — packed booleans](#detbitlayer--packed-booleans)
  - [DetCellIndex — spatial row index](#detcellindex--spatial-row-index)
  - [DetTagLayer — multi-tag per cell](#dettaglayer--multi-tag-per-cell)
  - [DetFlowLayer — direction + cost grid](#detflowlayer--direction--cost-grid)
- [Tables](#tables)
  - [DetTable — column-oriented row store](#dettable--column-oriented-row-store)
- [DetPathStore — path store](#detpathstore--path-store)
- [Pathfinding](#pathfinding)
- [Building System](#building-system)
- [Query Engine](#query-engine)
- [Command Batches](#command-batches)
- [Save / Load (DetSnapshot)](#save--load-detsnapshot)
- [Determinism Reference](#determinism-reference)
- [Unity Integration](#unity-integration)
- [API Quick Reference](#api-quick-reference)

---

## Why DetMap

- **Inspectable by default.** State lives in tables and layers instead of being hidden across large GameObject graphs.
- **Deterministic by design.** `Fix64`, stable row IDs, deterministic ordering, and binary snapshots make replay and lockstep practical.
- **Built for tooling.** Schema metadata and `DetSnapshot` let you save a frame and inspect it outside the running game.
- **Designed for large state.** Dense per-cell data goes into layers; sparse gameplay records go into tables.

If your main pain is “the game has too much state to reason about in the editor,” DetMap is aimed at that problem first.

---

## Modeling Data

Use this rule of thumb:

- `Layer`: data attached to cells. Examples: `height`, `terrainType`, `walkable`, `fertility`.
- `Table`: sparse rows you want humans and systems to inspect. Examples: `units`, `buildings`, `resourceStacks`, `jobs`.
- `CellIndex`: spatial lookup for table rows. Example: “which unit rows are in cell `(10,5)`?”
- `PathStore`: derived movement payload keyed by row id. Example: the current A* path for a worker.
- `DetSnapshot`: save/load boundary for debugging, replay, and offline inspection.

Typical modeling pattern:

- Keep business truth in `Table` columns and `Layer` values.
- Add `CellIndex` when you need fast row-by-cell lookup.
- Use `PathStore` for runtime path payload, not as your main business record.

---

## Architecture

```text
DetSpatialDatabase           ← top-level state database (tick, globals, tables)
  └── DetGrid                ← holds all layers, exposes InBounds
        ├── DetValueLayer<T>    ← dense value grid  (byte / int / Fix64)
        ├── DetBitLayer      ← bit-packed boolean grid
        ├── DetCellIndex   ← flat-array linked-list spatial row index
        ├── DetTagLayer      ← sparse multi-tag per cell
        └── DetFlowLayer     ← direction (byte) + cost (Fix64) per cell

DetTable                     ← column-oriented row store
  ├── DetColumn<T>              ← typed value column (byte / int / Fix64)
  └── DetStringColumn           ← string column

DetPathStore                 ← named path store (rowId → DetPath), serialized by DetSnapshot

DetPathfinder                ← A* with Chebyshev heuristic + cell-index tie-breaking
QueryEngine                  ← rect, radius, flood-fill queries (zero allocation via caller buffer)
BuildingPlacer               ← place / remove / canPlace footprint on grid
DetSnapshot                  ← binary save/load with embedded schema
```

Architecture note: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
Snapshot viewer note: [docs/SNAPSHOT_VIEWER.md](docs/SNAPSHOT_VIEWER.md)

**Dependency**: [DetMath](DetMath/DetMathREADME.md) — Q2.2 fixed-point library compiled as `DetMath.dll` (`netstandard2.0`).

---

## Installation

### As a DLL (Unity)

1. Copy `DetMath/bin/Release/netstandard2.0/DetMath.dll` into your Unity project's `Assets/Plugins/` folder.
2. Build `src/DetMap` targeting `netstandard2.1`:

   ```sh
   dotnet build src/DetMap -c Release
   ```

3. Copy `src/DetMap/bin/Release/netstandard2.1/DetMap.dll` into `Assets/Plugins/`.

### As a Project Reference (.NET)

```xml
<ProjectReference Include="path/to/DetMap/src/DetMap/DetMap.csproj" />
<Reference Include="DetMath">
  <HintPath>path/to/DetMath/bin/Release/netstandard2.0/DetMath.dll</HintPath>
</Reference>
```

---

## Quick Start

```csharp
using DetMath;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Tables;

// 1. Create the database
var db = new DetSpatialDatabase(64, 64);

// 2. Create dense cell data
var height   = db.Grid.CreateFix64Layer("height");
var walkable = db.Grid.CreateBitLayer("walkable");
walkable.SetAll(true);

// 3. Create sparse row data
var units      = db.CreateTable("units");
var unitName   = units.CreateStringColumn("name");
var unitHp     = units.CreateIntColumn("hp");
var unitCells  = db.Grid.CreateCellIndex("unitsByCell");

// 4. Add one row
int rowId = units.CreateRow();
unitName.Set(rowId, "Alice");
unitHp.Set(rowId, 100);
unitCells.Place(rowId, 5, 5);
height.Set(5, 5, Fix64.FromInt(2));

// 5. Save a snapshot for replay or inspection
byte[] snapshot = db.ToBytes();

// 6. Load it back
var restored = DetSpatialDatabase.FromBytes(snapshot);
int hp = restored.GetTable("units").GetIntColumn("hp").Get(rowId);
bool canWalk = restored.Grid.GetBitLayer("walkable").Get(5, 5);
```

What this shows:

- `Layer` stores per-cell world data.
- `Table` stores inspectable row state.
- `CellIndex` answers spatial lookup questions fast.
- `DetSnapshot` is the portable save/inspect boundary.

The rest of this README expands each piece in detail.

---

## Layers

All layers are registered by name on the grid. Each has a `DirtyRect` that tracks which cells changed since the last `ClearDirty()` call — useful for incremental rendering.

### DetValueLayer\<T\> — dense value grid

Backed by a flat array `T[width * height]`. Supported element types: `byte`, `int`, `Fix64`.

```csharp
// Preferred typed factories
DetValueLayer<byte>  flags  = map.Grid.CreateByteLayer("flags");
DetValueLayer<int>   ids    = map.Grid.CreateIntLayer("ids");
DetValueLayer<Fix64> height = map.Grid.CreateFix64Layer("height");

// Read / Write
flags.Set(3, 4, 42);
byte v = flags.Get(3, 4);         // 42

height.Set(1, 2, Fix64.FromInt(77));
Fix64 h = height.Get(1, 2);      // 77

// Fill entire layer
flags.Fill((byte)0);

// Dirty tracking
DirtyRect dirty = flags.Dirty;    // bounding box of all Set() calls
flags.ClearDirty();

// Raw span access (for bulk reads — read-only pattern recommended)
Span<Fix64> raw = height.AsSpan();

// Retrieve from grid by name
DetValueLayer<int> ids2 = map.Grid.GetIntLayer("ids");

// Low-level generic API is still available when a DetType token is useful
DetValueLayer<int> building2 = map.Grid.CreateValueLayer("building", DetType.Int);
```

**Allowed types only.** Trying to pass a custom type that is not `byte`, `int`, or `Fix64` will fail to compile because `DetType<T>` has an `internal` constructor:

```csharp
// compile error — DetType<float> cannot be constructed
map.Grid.CreateValueLayer("speed", new DetType<float>());
```

---

### DetBitLayer — packed booleans

Stores one bit per cell using `ulong[]` words. 64× more memory-efficient than `DetValueLayer<byte>` for boolean data.

```csharp
DetBitLayer walkable = map.Grid.CreateBitLayer("walkable");

walkable.SetAll(true);
walkable.Set(5, 5, false);        // mark cell as blocked

bool canWalk = walkable.Get(3, 3); // true

// Bulk logical operations (writes into a pre-allocated result layer)
var result = map.Grid.CreateBitLayer("temp");
DetBitLayer.And(walkable, anotherLayer, result);
DetBitLayer.Or (walkable, anotherLayer, result);
DetBitLayer.Xor(walkable, anotherLayer, result);

// Retrieve by name
DetBitLayer w = map.Grid.GetBitLayer("walkable");
```

---

### DetCellIndex — spatial row index

Zero-allocation flat-array linked list. Maps row IDs to grid cells. Supports multiple rows per cell. Row IDs are assigned by `DetTable.CreateRow()`.

```csharp
DetCellIndex units = map.Grid.CreateCellIndex("units");

units.Place(rowId: 0, x: 5, y: 5);
units.Place(rowId: 1, x: 5, y: 5);  // two rows on same cell

int count = units.CountAt(5, 5);      // 2

units.MoveTo(0, 6, 5);                // move row 0 to (6,5)
units.Remove(1);                      // remove row 1

// Iterate row ids at a cell (zero allocation — struct enumerator)
foreach (int id in units.GetRowIdsAt(6, 5))
{
    // process row id
}

// Retrieve by name
DetCellIndex u = map.Grid.GetCellIndex("units");
```

---

### DetTagLayer — multi-tag per cell

Sparse dictionary — only cells with at least one tag consume memory. Each cell can hold any number of string tags.

```csharp
DetTagLayer services = map.Grid.CreateTagLayer("services");

services.AddTag(8, 9, "market");
services.AddTag(8, 9, "water");

bool hasMarket = services.HasTag(8, 9, "market");     // true
bool hasBoth   = services.HasAllTags(8, 9, new[] { "market", "water" }); // true
int  count     = services.CountAt(8, 9);              // 2

IReadOnlyList<string> tags = services.GetTags(8, 9);  // ["market", "water"]

services.RemoveTag(8, 9, "water");

// Retrieve by name
DetTagLayer svc = map.Grid.GetTagLayer("services");
```

---

### DetFlowLayer — direction + cost grid

Stores a direction byte (`0`=N, `1`=E, `2`=S, `3`=W, `4`=NE, `5`=SE, `6`=SW, `7`=NW, `255`=blocked) and a Fix64 cost per cell. Used for group pathfinding — bake once, all units follow.

```csharp
DetFlowLayer flow = map.Grid.CreateFlowLayer("group_flow");

flow.Set(x: 3, y: 4, direction: 1, cost: Fix64.FromInt(10)); // East, cost 10

byte   dir  = flow.Get(3, 4);              // 1  (East)
Fix64  cost = flow.GetCost(3, 4);

bool blocked = flow.Get(0, 0) == DetFlowLayer.Blocked; // default = blocked

flow.Reset(); // fill all cells back to Blocked / InfiniteCost

// Retrieve by name
DetFlowLayer ff = map.Grid.GetFlowLayer("group_flow");
```

---

## Tables

Tables are column-oriented record stores. Row IDs are recycled deterministically using a LIFO free list.

### DetTable — column-oriented row store

```csharp
DetTable chars = map.CreateTable("characters");

// Add columns before creating rows
DetStringColumn nameCol = chars.CreateStringColumn("name");
DetColumn<byte>  jobCol = chars.CreateByteColumn("job");
DetColumn<int>   hpCol  = chars.CreateIntColumn("hp");
DetColumn<Fix64> xpCol  = chars.CreateFix64Column("xp");

// Create row — returns next available ID (recycles deleted IDs, LIFO)
int id = chars.CreateRow();             // 0
nameCol.Set(id, "Alice");
jobCol.Set(id, 1);
hpCol.Set(id, 100);
xpCol.Set(id, Fix64.FromInt(0));

int id2 = chars.CreateRow();            // 1
nameCol.Set(id2, "Bob");

// Delete row — frees ID for reuse
chars.DeleteRow(id);                  // id 0 goes into free list

int recycled = chars.CreateRow();       // 0  (recycled — LIFO)

// Query
bool alive = chars.RowExists(0);      // true (recycled)
int  hw    = chars.HighWater;       // 2 (max ID ever assigned + 1)

// Iterate all existing rows in deterministic order (0..HighWater)
foreach (int i in chars.GetRowIds())
{
    string? name = nameCol.Get(i);
    int     hp   = hpCol.Get(i);
}

// Retrieve table from map
DetTable t = map.GetTable("characters");

// Retrieve column from table
DetColumn<int>  hp2   = t.GetIntColumn("hp");
DetStringColumn name2 = t.GetStringColumn("name");
```

---

## Schema / Inspection

`DetSpatialDatabase` exposes schema metadata so a browser inspector or admin tool can discover the runtime structure without hard-coding gameplay types up front.

```csharp
var schema = map.GetSchema();

foreach (var layer in schema.Layers)
    Debug.Log($"{layer.Name} : {layer.Kind}");

foreach (var table in schema.Tables)
{
    Debug.Log($"Table: {table.Name}");
    foreach (var column in table.Columns)
        Debug.Log($"  {column.Name} : {column.Kind}");
}
```

This surface is intended for generic tools, debugging views, and snapshot-based inspectors.

The first practical tool can be a plain HTML page that loads a saved `DetSnapshot` file and renders tables, columns, and layers without any realtime connection to the running game.

---

## Command Batches

`DetCommandBatch` is the staging surface for lockstep input, web edits, and deferred changes that should only be applied at a safe boundary.

```csharp
var workers = map.GetTable("workers");
int rowId = workers.PeekNextRowId();

var batch = new DetCommandBatch();
batch.CreateRow("workers", rowId);
batch.SetString("workers", "name", rowId, "Somchai");
batch.SetInt("workers", "hp", rowId, 100);
batch.PlaceRow("units", rowId, 10, 5);

map.Apply(batch);
```

This pattern is useful because:

- row creation stays deterministic
- writes are explicit and ordered
- future tools can stage edits without mutating live state immediately
- the same batch can become the basis for lockstep replay later

The first command surface intentionally focuses on database truth:

- globals
- table rows
- table columns
- dense layer cells
- cell indexes

`DetPathStore` is not part of the first command surface because path data is usually derived runtime state, not player intent.

If the first inspector is snapshot-only, you do not need command batches yet for that tool.

They are included now because they belong to the core deterministic model and will matter once staged editing is introduced.

---

## DetPathStore — path store

Named DB-level structure that maps `rowId → DetPath`. Lives on `DetSpatialDatabase` alongside tables and indexes — serialized automatically by `DetSnapshot`.

```csharp
// Create — registered on DetSpatialDatabase, saved by DetSnapshot
DetPathStore paths = map.CreatePathStore("unitPaths");

var pf = new DetPathfinder(64, 64);
paths.Set(0, pf.FindPath(0, 0, 20, 20, walkable));
paths.Set(1, pf.FindPath(1, 0, 20, 20, walkable));

// Get by ref — modify in-place without copying
ref DetPath p = ref paths.Get(0);

if (p.IsValid && !p.IsComplete)
{
    p.Advance();
    var (nx, ny) = p.Current(mapWidth: 64);
    units.MoveTo(0, nx, ny);
}

// Peek next step without advancing
var (px, py) = p.Peek(64);

paths.Clear(0); // reset path for row 0

// Retrieve from map by name
DetPathStore ps = map.GetPathStore("unitPaths");
```

**Architecture position:**

```text
DetSpatialDatabase
  ├── DetGrid      → spatial layers
  ├── Tables       → row data (hp, name, job)
  └── PathStores   → row paths  ← here
```

---

## Pathfinding

`DetPathfinder` implements A* with:

- **Chebyshev heuristic** (8-directional movement)
- **Cell-index tie-breaking** — nodes with equal f-score are expanded in deterministic order
- **`Fix64` costs** throughout — no float

```csharp
var pf = new DetPathfinder(width: 64, height: 64);

// Basic path
DetPath path = pf.FindPath(startX: 2, startY: 5, goalX: 30, goalY: 30, walkable);

if (path.IsValid)
{
    Console.WriteLine($"Path has {path.Length} steps");
}

// With unit density avoidance (units counted as extra cost)
DetValueLayer<byte> unitCount = map.Grid.GetByteLayer("unit_count"); // optional
DetPath path2 = pf.FindPath(2, 5, 30, 30, walkable, unitCount, maxSearchNodes: 4096);

// Default maxSearchNodes = 2048. Returns default(DetPath) if goal unreachable.
bool unreachable = !path.IsValid;
```

**Straight move cost** = 10, **Diagonal** ≈ 14 (Fix64 raw 1414). Both are `Fix64` values.

---

## Building System

```csharp
using DetMap.Building;

// Define building footprint
var house  = new BuildingDefinition("house",  w: 2, h: 2, buildingTypeId: 1);
var market = new BuildingDefinition("market", w: 3, h: 2, buildingTypeId: 2);

// L-shaped footprint
var lDef = new BuildingDefinition("Ltower", 4, 4, 3,
    mask: BuildingDefinition.CreateLShapeMask(4, 4));

DetValueLayer<int> buildingLayer = map.Grid.GetIntLayer("building");
DetBitLayer        walkable      = map.Grid.GetBitLayer("walkable");

// Check placement validity before placing
bool ok = BuildingPlacer.CanPlace(map.Grid, ox: 10, oy: 5, house, buildingLayer, walkable);

// Place (writes building ID into buildingLayer, marks cells non-walkable)
if (ok)
    BuildingPlacer.Place(map.Grid, 10, 5, house, buildingLayer, walkable);

// Remove (clears building ID, restores walkability for solid cells)
BuildingPlacer.Remove(map.Grid, 10, 5, house, buildingLayer, walkable);

// CanPlace with extra condition (e.g. must be on flat terrain)
DetValueLayer<Fix64> height = map.Grid.GetFix64Layer("height");
bool canBuild = BuildingPlacer.CanPlace(map.Grid, 10, 5, house, buildingLayer, walkable,
    extraCheck: (grid, x, y) => height.Get(x, y).RawValue == 0);
```

---

## Query Engine

All queries write results into a caller-supplied `CellHit[]` buffer — zero allocation during gameplay.

```csharp
using DetMap.Query;

var buffer = new CellHit[64];

// Rectangle query — cells where predicate returns true
int n = QueryEngine.RectQuery(
    map.Grid,
    minX: 5, minY: 5, maxX: 15, maxY: 15,
    predicate: (grid, x, y) =>
    {
        var svc = grid.GetTagLayer("services");
        return svc.HasTag(x, y, "market");
    },
    resultBuffer: buffer);

for (int i = 0; i < n; i++)
    Console.WriteLine($"market cell: ({buffer[i].X}, {buffer[i].Y})");

// Radius query (circle, integer radius)
int m = QueryEngine.RadiusQuery(
    map.Grid,
    cx: 10, cy: 10, radius: 5,
    predicate: (grid, x, y) => grid.GetBitLayer("walkable").Get(x, y),
    resultBuffer: buffer);

// Flood fill — spread from start while predicate is true (4-directional)
int k = QueryEngine.FloodFill(
    map.Grid,
    startX: 3, startY: 3,
    canSpread: (grid, x, y) => grid.GetBitLayer("walkable").Get(x, y),
    resultBuffer: buffer);
```

---

## Save / Load (DetSnapshot)

`DetSnapshot.Serialize` writes a self-contained binary file. The schema (layer names, types, table columns, global keys) is embedded in the file header — no external config needed to load.

This makes `DetSnapshot` a good boundary format for an offline inspector: the game saves a snapshot, and a separate HTML tool reads that file and renders the database.

```csharp
// Save
byte[] save = map.ToBytes();
File.WriteAllBytes("save.dmap", save);

// Load
byte[] data = File.ReadAllBytes("save.dmap");
DetSpatialDatabase loaded = DetSpatialDatabase.FromBytes(data);

// Direct API
byte[] bytes = DetSnapshot.Serialize(map);
DetSpatialDatabase restored = DetSnapshot.Deserialize(bytes);

// Access restored state
ulong tick = loaded.Tick;
Fix64 gold = loaded.GetGlobal("treasury");

DetValueLayer<int> ids  = loaded.Grid.GetIntLayer("building");
DetBitLayer        walk = loaded.Grid.GetBitLayer("walkable");
DetCellIndex       u    = loaded.Grid.GetCellIndex("units");
DetTagLayer        svc  = loaded.Grid.GetTagLayer("services");
DetFlowLayer       ff   = loaded.Grid.GetFlowLayer("group_flow");

DetTable  chars = loaded.GetTable("characters");
string?   name  = chars.GetStringColumn("name").Get(0);
int       hp    = chars.GetIntColumn("hp").Get(0);
Fix64     xp    = chars.GetFix64Column("xp").Get(0);
```

### Binary Format

```text
[4 bytes]  magic: 'D','M','A','P'
[2 bytes]  version: 2
── SCHEMA ─────────────────────────────────────
[4]  grid width
[4]  grid height
[4]  layer count
     per layer: [1] kind  [str] name
[4]  table count
     per table: [str] name  [4] colCount
       per col: [1] kind  [str] name
[4]  global count  (keys in ordinal-sorted order)
     per global: [str] key
[4]  pathstore count
     per pathstore: [str] name
── DATA ────────────────────────────────────────
[8]  tick (ulong)
     layer data × N  (raw bytes, schema order)
     global Fix64.RawValue × G  (schema order)
     per table: [4] highWater  [4] freeCount  freeList[]
                alive col data  user col data × colCount
     pathstore data × P
```

**What is saved:** layers, globals, tables, path stores, tick.

---

## Determinism Reference

| Source of non-determinism | How DetMap avoids it |
| --- | --- |
| `float` / `double` arithmetic | All game math uses `Fix64` (Q2.2, `long` arithmetic) |
| `Dictionary` iteration order | Schema section uses sorted keys for globals; layer/table order follows insertion order, which is deterministic for sequential setup code |
| Uninitialized memory | `DetCellIndex.EnsureCapacity` fills new slots to `-1`; all arrays zero-initialized by default |
| Hash-dependent ordering | No hash-keyed iteration in hot path |
| Free list ordering | LIFO `Stack<int>` — saved and restored in exact stack order |
| A* tie-breaking | `DetMinHeap` breaks equal-priority nodes by cell index (ascending) |
| Diagonal movement cost | `Fix64.FromRaw(1414)` — exact integer, no sqrt |

**Rule**: Never pass `float` or `double` into any `Fix64` API. Use `Fix64.FromInt()` or `Fix64.FromRaw()`.

```csharp
// CORRECT
Fix64 cost = Fix64.FromInt(10);
Fix64 half = Fix64.FromRaw(50);   // scale=100, so raw 50 = 0.50

// WRONG — will not compile because DetType<float> has no public constructor
// map.Grid.CreateValueLayer("speed", new DetType<float>());
```

---

## Unity Integration

DetMap targets `netstandard2.1` and uses only APIs available in Unity (Mono / IL2CPP):

| API | Available |
| --- | --- |
| `ArrayPool<T>` | Yes (netstandard2.1) |
| `Span<T>` / `MemoryMarshal` | Yes |
| `BinaryWriter` / `BinaryReader` | Yes |
| `Stack<T>.Clear()` | Yes |
| C# 8 switch expressions | Yes (`LangVersion=latest`) |

**No `float` / `double` in game logic** — safe for IL2CPP cross-platform builds.

Typical Unity setup pattern:

```csharp
// MonoBehaviour bootstrap — runs once
void Awake()
{
    _map = new DetSpatialDatabase(64, 64);

    _buildingLayer = _map.Grid.CreateIntLayer("building");
    _walkable      = _map.Grid.CreateBitLayer("walkable");
    _units         = _map.Grid.CreateCellIndex("units");

    _walkable.SetAll(true);

    _table   = _map.CreateTable("units");
    _nameCol = _table.CreateStringColumn("name");
    _hpCol   = _table.CreateIntColumn("hp");

    _pathfinder = new DetPathfinder(64, 64);
    _paths      = _map.CreatePathStore("unitPaths");
}

// Per-tick update — call from FixedUpdate or a lockstep loop
void SimulateTick()
{
    _map.AdvanceTick();

    foreach (int id in _table.GetRowIds())
    {
        ref DetPath p = ref _paths.Get(id);
        if (!p.IsValid || p.IsComplete) continue;

        p.Advance();
        var (nx, ny) = p.Current(64);
        _units.MoveTo(id, nx, ny);
    }

    // Dirty rects tell the renderer exactly which cells changed
    DirtyRect changed = _buildingLayer.Dirty;
    if (changed.IsDirty)
    {
        RefreshTiles(changed);
        _buildingLayer.ClearDirty();
    }
}
```

### Random Walkers Example

The example below creates 10 workers. Each worker keeps walking to a random destination, and only after reaching that destination does it pick a new one.

```csharp
using System.Collections.Generic;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Pathfinding;
using DetMap.Tables;
using UnityEngine;

public sealed class RandomWalkerExample : MonoBehaviour
{
    [SerializeField] private GameObject workerPrefab;
    [SerializeField] private int mapWidth = 32;
    [SerializeField] private int mapHeight = 32;
    [SerializeField] private int workerCount = 10;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private uint randomSeed = 123456789;

    private DetSpatialDatabase _map = null!;
    private DetBitLayer _walkable = null!;
    private DetCellIndex _units = null!;
    private DetTable _workers = null!;
    private DetStringColumn _nameColumn = null!;
    private DetColumn<int> _posXColumn = null!;
    private DetColumn<int> _posYColumn = null!;
    private DetPathStore _paths = null!;
    private DetPathfinder _pathfinder = null!;

    private readonly Dictionary<int, Transform> _views = new();
    private uint _rngState;

    private void Awake()
    {
        _rngState = randomSeed;

        _map = new DetSpatialDatabase(mapWidth, mapHeight);
        _walkable = _map.Grid.CreateBitLayer("walkable");
        _walkable.SetAll(true);

        _units = _map.Grid.CreateCellIndex("units");

        _workers = _map.CreateTable("workers");
        _nameColumn = _workers.CreateStringColumn("name");
        _posXColumn = _workers.CreateIntColumn("posX");
        _posYColumn = _workers.CreateIntColumn("posY");

        _paths = _map.CreatePathStore("workerPaths");
        _pathfinder = new DetPathfinder(mapWidth, mapHeight);
    }

    private void Start()
    {
        for (int i = 0; i < workerCount; i++)
        {
            var (x, y) = FindRandomWalkableCell();
            int id = SpawnWorker($"Worker_{i}", x, y);
            AssignRandomDestination(id);
        }

        SyncViews();
    }

    private void FixedUpdate()
    {
        SimulateTick();
        SyncViews();
    }

    private int SpawnWorker(string workerName, int x, int y)
    {
        int id = _workers.CreateRow();

        _nameColumn.Set(id, workerName);
        _posXColumn.Set(id, x);
        _posYColumn.Set(id, y);
        _units.Place(id, x, y);

        if (workerPrefab != null)
        {
            var view = Instantiate(workerPrefab, GridToWorld(x, y), Quaternion.identity, transform);
            view.name = workerName;
            _views[id] = view.transform;
        }

        return id;
    }

    private void SimulateTick()
    {
        foreach (int id in _workers.GetRowIds())
        {
            ref DetPath path = ref _paths.Get(id);

            if (!path.IsValid)
            {
                AssignRandomDestination(id);
                continue;
            }

            if (!path.IsComplete)
            {
                path.Advance();
                var (nx, ny) = path.Current(mapWidth);

                _units.MoveTo(id, nx, ny);
                _posXColumn.Set(id, nx);
                _posYColumn.Set(id, ny);
            }

            if (path.IsComplete)
                AssignRandomDestination(id);
        }

        _map.AdvanceTick();
    }

    private void AssignRandomDestination(int workerId)
    {
        int startX = _posXColumn.Get(workerId);
        int startY = _posYColumn.Get(workerId);

        for (int attempt = 0; attempt < 64; attempt++)
        {
            int goalX = NextInt(mapWidth);
            int goalY = NextInt(mapHeight);

            if (!_walkable.Get(goalX, goalY))
                continue;

            if (goalX == startX && goalY == startY)
                continue;

            DetPath path = _pathfinder.FindPath(startX, startY, goalX, goalY, _walkable);
            if (!path.IsValid)
                continue;

            _paths.Set(workerId, path);
            return;
        }

        _paths.Clear(workerId);
    }

    private (int x, int y) FindRandomWalkableCell()
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            int x = NextInt(mapWidth);
            int y = NextInt(mapHeight);
            if (_walkable.Get(x, y))
                return (x, y);
        }

        return (0, 0);
    }

    private void SyncViews()
    {
        foreach (int id in _workers.GetRowIds())
        {
            if (!_views.TryGetValue(id, out Transform view))
                continue;

            int x = _posXColumn.Get(id);
            int y = _posYColumn.Get(id);
            view.position = GridToWorld(x, y);
        }
    }

    private Vector3 GridToWorld(int x, int y)
        => new Vector3(x * cellSize, 0f, y * cellSize);

    private int NextInt(int maxExclusive)
    {
        _rngState ^= _rngState << 13;
        _rngState ^= _rngState >> 17;
        _rngState ^= _rngState << 5;
        return (int)(_rngState % (uint)maxExclusive);
    }
}
```

If you need strict lockstep determinism, keep the RNG state in simulation state rather than only on the `MonoBehaviour`.

---

## API Quick Reference

### DetSpatialDatabase — Root State

```csharp
new DetSpatialDatabase(int width, int height)
map.Grid                                 // DetGrid
map.Tick                                 // ulong
map.AdvanceTick()
map.SetGlobal(string key, Fix64 value)
Fix64 map.GetGlobal(string key)          // returns Zero if missing
map.Apply(DetCommandBatch batch)
map.Globals                              // IReadOnlyDictionary<string, Fix64>
DetTable     map.CreateTable(string name, int capacity = 256)
DetTable     map.GetTable(string name)
map.Tables                               // IReadOnlyDictionary<string, DetTable>
map.TableOrder                           // IReadOnlyList<string>
DetPathStore map.CreatePathStore(string name, int capacity = 256)
DetPathStore map.GetPathStore(string name)
map.PathStores                           // IReadOnlyDictionary<string, DetPathStore>
map.PathStoreOrder                       // IReadOnlyList<string>
DetDatabaseSchema map.GetSchema()
byte[] map.ToBytes()                     // DetSnapshot.Serialize(map)
DetSpatialDatabase.FromBytes(byte[] data) // DetSnapshot.Deserialize(data)
```

### DetGrid

```csharp
grid.Width / grid.Height
grid.InBounds(int x, int y)             // bool
grid.CreateByteLayer(name)              // DetValueLayer<byte>
grid.CreateIntLayer(name)               // DetValueLayer<int>
grid.CreateFix64Layer(name)             // DetValueLayer<Fix64>
grid.CreateBitLayer(name)               // DetBitLayer
grid.CreateValueLayer<T>(name, DetType<T>)  // low-level generic factory
grid.CreateCellIndex(name)            // DetCellIndex
grid.CreateTagLayer(name)               // DetTagLayer
grid.CreateFlowLayer(name)              // DetFlowLayer
grid.GetByteLayer(name)                 // DetValueLayer<byte>
grid.GetIntLayer(name)                  // DetValueLayer<int>
grid.GetFix64Layer(name)                // DetValueLayer<Fix64>
grid.GetValueLayer<T>(name)             // low-level generic getter
grid.GetBitLayer(name)                  // DetBitLayer
grid.GetCellIndex(name)               // DetCellIndex
grid.GetTagLayer(name)                  // DetTagLayer
grid.GetFlowLayer(name)                 // DetFlowLayer
grid.AllLayers                          // IReadOnlyDictionary<string, IDetLayer>
grid.LayerOrder                         // IReadOnlyList<string>
grid.GetLayerSchemas()                  // IReadOnlyList<DetLayerSchema>
```

### DetValueLayer\<T\>

```csharp
layer.Get(int x, int y) -> T
layer.Set(int x, int y, T value)
layer.Fill(T value)
layer.AsSpan() -> Span<T>
layer.Dirty -> DirtyRect
layer.ClearDirty()
```

### DetBitLayer

```csharp
layer.Get(int x, int y) -> bool
layer.Set(int x, int y, bool value)
layer.SetAll(bool value)
DetBitLayer.And/Or/Xor(a, b, result)
```

### DetCellIndex

```csharp
map.Place(int rowId, int x, int y)
map.Remove(int rowId)
map.MoveTo(int rowId, int newX, int newY)
map.CountAt(int x, int y) -> int
RowIdEnumerator map.GetRowIdsAt(int x, int y)
```

### DetTagLayer

```csharp
map.AddTag(int x, int y, string tag)
map.RemoveTag(int x, int y, string tag)
map.HasTag(int x, int y, string tag) -> bool
map.HasAllTags(int x, int y, IEnumerable<string> tags) -> bool
map.CountAt(int x, int y) -> int
IReadOnlyList<string> map.GetTags(int x, int y)
```

### DetFlowLayer

```csharp
field.Get(int x, int y) -> byte          // direction
field.GetCost(int x, int y) -> Fix64
field.Set(int x, int y, byte dir, Fix64 cost)
field.Reset()
DetFlowLayer.Blocked = 255
```

### DetTable

```csharp
int  table.CreateRow()
void table.DeleteRow(int id)
bool table.RowExists(int id)
DetColumn<byte> table.CreateByteColumn(string name)
DetColumn<int>  table.CreateIntColumn(string name)
DetColumn<Fix64> table.CreateFix64Column(string name)
DetColumn<T>    table.CreateColumn<T>(string name, DetType<T> type)  // low-level generic factory
DetStringColumn table.CreateStringColumn(string name)
DetColumn<byte> table.GetByteColumn(string name)
DetColumn<int>  table.GetIntColumn(string name)
DetColumn<Fix64> table.GetFix64Column(string name)
DetColumn<T>    table.GetColumn<T>(string name)                      // low-level generic getter
DetStringColumn table.GetStringColumn(string name)
IEnumerable<int> table.GetRowIds()        // 0..HighWater, existing rows only
int table.HighWater
DetTableSchema table.GetSchema()
int table.PeekNextRowId()
```

### DetPathStore

```csharp
store.Set(int rowId, DetPath path)
ref DetPath store.Get(int rowId)      // ref — modify in-place
store.Clear(int rowId)
```

### DetCommandBatch

```csharp
new DetCommandBatch()
batch.SetGlobal(string key, Fix64 value)
batch.CreateRow(string tableName, int expectedRowId)
batch.DeleteRow(string tableName, int rowId)
batch.SetByte(string tableName, string columnName, int rowId, byte value)
batch.SetInt(string tableName, string columnName, int rowId, int value)
batch.SetFix64(string tableName, string columnName, int rowId, Fix64 value)
batch.SetString(string tableName, string columnName, int rowId, string? value)
batch.SetBitCell(string layerName, int x, int y, bool value)
batch.SetByteCell(string layerName, int x, int y, byte value)
batch.SetIntCell(string layerName, int x, int y, int value)
batch.SetFix64Cell(string layerName, int x, int y, Fix64 value)
batch.PlaceRow(string indexName, int rowId, int x, int y)
batch.MoveRow(string indexName, int rowId, int x, int y)
batch.RemoveRow(string indexName, int rowId)
batch.ApplyTo(DetSpatialDatabase database)
batch.Clear()
batch.Count
```

### DetPathfinder / DetPath

```csharp
new DetPathfinder(int width, int height)
DetPath pf.FindPath(int sx, int sy, int gx, int gy,
    DetBitLayer walkable,
    DetValueLayer<byte>? unitCount = null,
    int maxSearchNodes = 2048)

path.IsValid       // bool
path.IsComplete    // bool
path.Length        // int
path.Advance()
(int x, int y) path.Current(int width)
(int x, int y) path.Peek(int width)    // next step without advancing
```

### QueryEngine

```csharp
int QueryEngine.RectQuery(grid, minX, minY, maxX, maxY, predicate, CellHit[] buffer)
int QueryEngine.RadiusQuery(grid, cx, cy, radius, predicate, CellHit[] buffer)
int QueryEngine.FloodFill(grid, startX, startY, canSpread, CellHit[] buffer)
// returns number of matching cells written into buffer
```

### DetSnapshot

```csharp
byte[] DetSnapshot.Serialize(DetSpatialDatabase database)
DetSpatialDatabase DetSnapshot.Deserialize(byte[] data)   // throws InvalidDataException on bad magic/version
// Also accessible via: map.ToBytes() / DetSpatialDatabase.FromBytes(data)
```
