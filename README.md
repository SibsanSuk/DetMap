# DetMap

A deterministic in-memory spatial database for city-builder and RTS games built on C# / Unity.

Every read and write goes through **Fix64** fixed-point arithmetic (no `float`, no `double` in game logic), so simulation state is **bit-identical across all platforms** — Windows, macOS, Linux, iOS, Android, WebGL.

---

## Table of Contents

- [Architecture](#architecture)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Layers](#layers)
  - [DetLayer\<T\> — dense value grid](#detlayert--dense-value-grid)
  - [DetBitLayer — packed booleans](#detbitlayer--packed-booleans)
  - [DetEntityMap — spatial entity index](#detentitymap--spatial-entity-index)
  - [DetTagMap — multi-tag per cell](#dettagmap--multi-tag-per-cell)
  - [DetFlowField — direction + cost grid](#detflowfield--direction--cost-grid)
- [Tables](#tables)
  - [DetTable — column-oriented entity store](#dettable--column-oriented-entity-store)
  - [DetPathCol — path column](#detpathcol--path-column)
- [Pathfinding](#pathfinding)
- [Building System](#building-system)
- [Query Engine](#query-engine)
- [Save / Load (Snapshot)](#save--load-snapshot)
- [Determinism Reference](#determinism-reference)
- [Unity Integration](#unity-integration)
- [API Quick Reference](#api-quick-reference)

---

## Architecture

```text
DetMap                       ← top-level facade (tick, globals, tables)
  └── DetGrid                ← holds all layers, exposes InBounds
        ├── DetLayer<T>      ← dense value grid  (byte / int / Fix64)
        ├── DetBitLayer      ← bit-packed boolean grid
        ├── DetEntityMap     ← flat-array linked-list spatial entity index
        ├── DetTagMap        ← sparse multi-tag per cell
        └── DetFlowField     ← direction (byte) + cost (Fix64) per cell

DetTable                     ← column-oriented entity store
  ├── DetCol<T>              ← typed value column (byte / int / Fix64)
  ├── DetStringCol           ← string column
  └── DetPathCol             ← A* path column (not serialized by Snapshot)

DetPathfinder                ← A* with Chebyshev heuristic + cell-index tie-breaking
QueryEngine                  ← rect, radius, flood-fill queries (zero allocation via caller buffer)
BuildingPlacer               ← place / remove / canPlace footprint on grid
Snapshot                     ← binary save/load with embedded schema
```

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
using DetMap.Building;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Pathfinding;
using DetMap.Tables;

// 1. Create a 64×64 map
var map = new DetMap.Core.DetMap(64, 64);

// 2. Create layers
var building  = map.Grid.CreateLayer("building",  LayerType.Int);
var height    = map.Grid.CreateLayer("height",    LayerType.Fix64);
var walkable  = map.Grid.CreateBitLayer("walkable");
var units     = map.Grid.CreateEntityMap("units");
var services  = map.Grid.CreateTagMap("services");

walkable.SetAll(true);

// 3. Set global state
map.SetGlobal("treasury",   Fix64.FromInt(1000));
map.SetGlobal("population", Fix64.FromInt(0));

// 4. Create entity table
var chars   = map.CreateTable("characters");
var nameCol = chars.AddStringCol("name");
var jobCol  = chars.AddCol<byte>("job");
var hpCol   = chars.AddCol<int>("hp");

// 5. Place a building
var houseDef = new BuildingDef("house", 2, 2, Fix64.FromInt(1));
BuildingPlacer.Place(map.Grid, 10, 10, houseDef, building, walkable);

// 6. Spawn entities
int id = chars.Spawn();
nameCol.Set(id, "Alice");
jobCol.Set(id, 0);
hpCol.Set(id, 100);
units.Add(id, 5, 5);

// 7. Pathfind
var pf = new DetPathfinder(64, 64);
var path = pf.FindPath(5, 5, 30, 30, walkable);

// 8. Simulation tick
map.AdvanceTick();

// 9. Move entity along path
ref DetPath p = ref new DetPathCol(1).Get(0); // or store in a DetPathCol
// p.Advance(); var (nx, ny) = p.Current(64);

// 10. Save / Load
byte[] save = map.ToBytes();
var loaded  = DetMap.Core.DetMap.FromBytes(save);
```

---

## Layers

All layers are registered by name on the grid. Each has a `DirtyRect` that tracks which cells changed since the last `ClearDirty()` call — useful for incremental rendering.

### DetLayer\<T\> — dense value grid

Backed by a flat array `T[width * height]`. Supported element types: `byte`, `int`, `Fix64`.

```csharp
// Creation — LayerType token enforces compile-time determinism
DetLayer<byte>  flags  = map.Grid.CreateLayer("flags",  LayerType.Byte);
DetLayer<int>   ids    = map.Grid.CreateLayer("ids",    LayerType.Int);
DetLayer<Fix64> height = map.Grid.CreateLayer("height", LayerType.Fix64);

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
DetLayer<int> ids2 = map.Grid.Layer<int>("ids");
```

**Allowed types only.** Trying to pass a custom type that is not `byte`, `int`, or `Fix64` will fail to compile because `LayerType<T>` has an `internal` constructor:

```csharp
// compile error — LayerType<float> cannot be constructed
map.Grid.CreateLayer("speed", new LayerType<float>());
```

---

### DetBitLayer — packed booleans

Stores one bit per cell using `ulong[]` words. 64× more memory-efficient than `DetLayer<byte>` for boolean data.

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

// Retrieve by name (use Structure<T> for non-generic layers)
DetBitLayer w = map.Grid.Structure<DetBitLayer>("walkable");
```

---

### DetEntityMap — spatial entity index

Zero-allocation flat-array linked list. Maps entity IDs to grid cells. Supports multiple entities per cell. Entity IDs are assigned by `DetTable.Spawn()`.

```csharp
DetEntityMap units = map.Grid.CreateEntityMap("units");

units.Add(entityId: 0, x: 5, y: 5);
units.Add(entityId: 1, x: 5, y: 5);  // two entities on same cell

int count = units.CountAt(5, 5);      // 2

units.Move(0, 6, 5);                  // move entity 0 to (6,5)
units.Remove(1);                      // remove entity 1

// Iterate entities at a cell (zero allocation — struct enumerator)
foreach (int id in units.GetEntitiesAt(6, 5))
{
    // process entity id
}

// Retrieve by name
DetEntityMap u = map.Grid.Structure<DetEntityMap>("units");
```

---

### DetTagMap — multi-tag per cell

Sparse dictionary — only cells with at least one tag consume memory. Each cell can hold any number of string tags.

```csharp
DetTagMap services = map.Grid.CreateTagMap("services");

services.AddTag(8, 9, "market");
services.AddTag(8, 9, "water");

bool hasMarket = services.HasTag(8, 9, "market");     // true
bool hasBoth   = services.HasAllTags(8, 9, new[] { "market", "water" }); // true
int  count     = services.CountAt(8, 9);              // 2

IReadOnlyList<string> tags = services.GetTags(8, 9);  // ["market", "water"]

services.RemoveTag(8, 9, "water");

// Retrieve by name
DetTagMap svc = map.Grid.Structure<DetTagMap>("services");
```

---

### DetFlowField — direction + cost grid

Stores a direction byte (`0`=N, `1`=E, `2`=S, `3`=W, `4`=NE, `5`=SE, `6`=SW, `7`=NW, `255`=blocked) and a Fix64 cost per cell. Used for group pathfinding — bake once, all units follow.

```csharp
DetFlowField flow = map.Grid.CreateFlowField("group_flow");

flow.Set(x: 3, y: 4, direction: 1, cost: Fix64.FromInt(10)); // East, cost 10

byte   dir  = flow.Get(3, 4);              // 1  (East)
Fix64  cost = flow.GetCost(3, 4);

bool blocked = flow.Get(0, 0) == DetFlowField.Blocked; // default = blocked

flow.Reset(); // fill all cells back to Blocked / InfiniteCost

// Retrieve by name
DetFlowField ff = map.Grid.Structure<DetFlowField>("group_flow");
```

---

## Tables

Tables are column-oriented entity stores — think of each row as an entity, each column as a component. IDs are recycled deterministically using a LIFO free list.

### DetTable — column-oriented entity store

```csharp
DetTable chars = map.CreateTable("characters");

// Add columns before spawning entities
DetStringCol nameCol = chars.AddStringCol("name");
DetCol<byte> jobCol  = chars.AddCol<byte>("job");
DetCol<int>  hpCol   = chars.AddCol<int>("hp");
DetCol<Fix64> xpCol  = chars.AddCol<Fix64>("xp");

// Spawn entity — returns next available ID (recycles despawned IDs, LIFO)
int id = chars.Spawn();             // 0
nameCol.Set(id, "Alice");
jobCol.Set(id, 1);
hpCol.Set(id, 100);
xpCol.Set(id, Fix64.FromInt(0));

int id2 = chars.Spawn();            // 1
nameCol.Set(id2, "Bob");

// Despawn — frees ID for reuse
chars.Despawn(id);                  // id 0 goes into free list

int recycled = chars.Spawn();       // 0  (recycled — LIFO)

// Query
bool alive = chars.IsAlive(0);      // true (recycled)
int  hw    = chars.HighWater;       // 2 (max ID ever assigned + 1)

// Iterate all alive entities in deterministic order (0..HighWater)
foreach (int i in chars.GetAlive())
{
    string? name = nameCol.Get(i);
    int     hp   = hpCol.Get(i);
}

// Retrieve table from map
DetTable t = map.Table("characters");

// Retrieve column from table
DetCol<int>  hp2   = t.GetCol<int>("hp");
DetStringCol name2 = t.GetStringCol("name");
```

---

### DetPathCol — path column

Standalone column for storing one `DetPath` per entity. Not part of `DetTable._cols` and not serialized by `Snapshot` — manage separately.

```csharp
int capacity = 256;
var pathCol = new DetPathCol(capacity);

var pf   = new DetPathfinder(64, 64);
var path = pf.FindPath(2, 5, 30, 30, walkable);
pathCol.Set(entityId, path);

// Get by ref to avoid copying the struct
ref DetPath p = ref pathCol.Get(entityId);

if (p.IsValid && !p.IsComplete)
{
    p.Advance();
    var (nx, ny) = p.Current(mapWidth: 64);
    units.Move(entityId, nx, ny);
}

// Peek next step without advancing
var (px, py) = p.Peek(64);

pathCol.Clear(entityId); // reset path
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
DetLayer<byte> unitCount = map.Grid.Layer<byte>("unit_count"); // optional
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
var house  = new BuildingDef("house",  w: 2, h: 2, buildingId: Fix64.FromInt(1));
var market = new BuildingDef("market", w: 3, h: 2, buildingId: Fix64.FromInt(2));

// L-shaped footprint
var lDef = new BuildingDef("Ltower", 4, 4, Fix64.FromInt(3),
    mask: BuildingDef.MakeLShape(4, 4));

DetLayer<int> buildingLayer = map.Grid.Layer<int>("building");
DetBitLayer   walkable      = map.Grid.Structure<DetBitLayer>("walkable");

// Check placement validity before placing
bool ok = BuildingPlacer.CanPlace(map.Grid, ox: 10, oy: 5, house, buildingLayer, walkable);

// Place (writes building ID into buildingLayer, marks cells non-walkable)
if (ok)
    BuildingPlacer.Place(map.Grid, 10, 5, house, buildingLayer, walkable);

// Remove (clears building ID, restores walkability for solid cells)
BuildingPlacer.Remove(map.Grid, 10, 5, house, buildingLayer, walkable);

// CanPlace with extra condition (e.g. must be on flat terrain)
DetLayer<Fix64> height = map.Grid.Layer<Fix64>("height");
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
        var svc = grid.Structure<DetTagMap>("services");
        return svc.HasTag(x, y, "market");
    },
    resultBuffer: buffer);

for (int i = 0; i < n; i++)
    Console.WriteLine($"market cell: ({buffer[i].X}, {buffer[i].Y})");

// Radius query (circle, integer radius)
int m = QueryEngine.RadiusQuery(
    map.Grid,
    cx: 10, cy: 10, radius: 5,
    predicate: (grid, x, y) => grid.Structure<DetBitLayer>("walkable").Get(x, y),
    resultBuffer: buffer);

// Flood fill — spread from start while predicate is true (4-directional)
int k = QueryEngine.FloodFill(
    map.Grid,
    startX: 3, startY: 3,
    canSpread: (grid, x, y) => grid.Structure<DetBitLayer>("walkable").Get(x, y),
    resultBuffer: buffer);
```

---

## Save / Load (Snapshot)

`Snapshot.Serialize` writes a self-contained binary file. The schema (layer names, types, table columns, global keys) is embedded in the file header — no external config needed to load.

```csharp
// Save
byte[] save = map.ToBytes();
File.WriteAllBytes("save.dmap", save);

// Load
byte[] data = File.ReadAllBytes("save.dmap");
DetMap.Core.DetMap loaded = DetMap.Core.DetMap.FromBytes(data);

// Access restored state
ulong tick = loaded.Tick;
Fix64 gold = loaded.GetGlobal("treasury");

DetLayer<int>    ids  = loaded.Grid.Layer<int>("building");
DetBitLayer      walk = loaded.Grid.Structure<DetBitLayer>("walkable");
DetEntityMap     u    = loaded.Grid.Structure<DetEntityMap>("units");
DetTagMap        svc  = loaded.Grid.Structure<DetTagMap>("services");
DetFlowField     ff   = loaded.Grid.Structure<DetFlowField>("group_flow");

DetTable  chars = loaded.Table("characters");
string?   name  = chars.GetStringCol("name").Get(0);
int       hp    = chars.GetCol<int>("hp").Get(0);
Fix64     xp    = chars.GetCol<Fix64>("xp").Get(0);
```

### Binary Format

```text
[4 bytes]  magic: 'D','M','A','P'
[2 bytes]  version: 1
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
── DATA ────────────────────────────────────────
[8]  tick (ulong)
     layer data × N  (raw bytes, schema order)
     global Fix64.RawValue × G  (schema order)
     per table: [4] highWater  [4] freeCount  freeList[]
                alive col data  user col data × colCount
```

**What is not saved by `Snapshot`:** `DetPathCol` (standalone, user-managed).

---

## Determinism Reference

| Source of non-determinism | How DetMap avoids it |
| --- | --- |
| `float` / `double` arithmetic | All game math uses `Fix64` (Q2.2, `long` arithmetic) |
| `Dictionary` iteration order | Schema section uses sorted keys for globals; layer/table order follows insertion order, which is deterministic for sequential setup code |
| Uninitialized memory | `DetEntityMap.EnsureCapacity` fills new slots to `-1`; all arrays zero-initialized by default |
| Hash-dependent ordering | No hash-keyed iteration in hot path |
| Free list ordering | LIFO `Stack<int>` — saved and restored in exact stack order |
| A* tie-breaking | `DetMinHeap` breaks equal-priority nodes by cell index (ascending) |
| Diagonal movement cost | `Fix64.FromRaw(1414)` — exact integer, no sqrt |

**Rule**: Never pass `float` or `double` into any `Fix64` API. Use `Fix64.FromInt()` or `Fix64.FromRaw()`.

```csharp
// CORRECT
Fix64 cost = Fix64.FromInt(10);
Fix64 half = Fix64.FromRaw(50);   // scale=100, so raw 50 = 0.50

// WRONG — will not compile because LayerType<float> has no public constructor
// map.Grid.CreateLayer("speed", new LayerType<float>());
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
    _map = new DetMap.Core.DetMap(64, 64);

    _buildingLayer = _map.Grid.CreateLayer("building", LayerType.Int);
    _walkable      = _map.Grid.CreateBitLayer("walkable");
    _units         = _map.Grid.CreateEntityMap("units");

    _walkable.SetAll(true);

    _table   = _map.CreateTable("units");
    _nameCol = _table.AddStringCol("name");
    _hpCol   = _table.AddCol<int>("hp");

    _pathfinder = new DetPathfinder(64, 64);
    _pathCols   = new DetPathCol(256);
}

// Per-tick update — call from FixedUpdate or a lockstep loop
void SimulateTick()
{
    _map.AdvanceTick();

    foreach (int id in _table.GetAlive())
    {
        ref DetPath p = ref _pathCols.Get(id);
        if (!p.IsValid || p.IsComplete) continue;

        p.Advance();
        var (nx, ny) = p.Current(64);
        _units.Move(id, nx, ny);
    }

    // Dirty rects tell the renderer exactly which cells changed
    DirtyRect changed = _buildingLayer.Dirty;
    if (!changed.IsEmpty)
    {
        RefreshTiles(changed);
        _buildingLayer.ClearDirty();
    }
}
```

---

## API Quick Reference

### DetMap — Facade

```csharp
new DetMap(int width, int height)
map.Grid                                 // DetGrid
map.Tick                                 // ulong
map.AdvanceTick()
map.SetGlobal(string key, Fix64 value)
Fix64 map.GetGlobal(string key)          // returns Zero if missing
map.Globals                              // IReadOnlyDictionary<string, Fix64>
DetTable map.CreateTable(string name, int capacity = 256)
DetTable map.Table(string name)
map.Tables                               // IReadOnlyDictionary<string, DetTable>
byte[] map.ToBytes()                     // Snapshot.Serialize(map)
DetMap.FromBytes(byte[] data)            // Snapshot.Deserialize(data)
```

### DetGrid

```csharp
grid.Width / grid.Height
grid.InBounds(int x, int y)             // bool
grid.CreateLayer<T>(name, LayerType<T>) // DetLayer<T>
grid.CreateBitLayer(name)               // DetBitLayer
grid.CreateEntityMap(name)              // DetEntityMap
grid.CreateTagMap(name)                 // DetTagMap
grid.CreateFlowField(name)              // DetFlowField
grid.Layer<T>(name)                     // DetLayer<T>
grid.Structure<T>(name)                 // T : class, IDetLayer
grid.AllLayers                          // IReadOnlyDictionary<string, IDetLayer>
```

### DetLayer\<T\>

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

### DetEntityMap

```csharp
map.Add(int entityId, int x, int y)
map.Remove(int entityId)
map.Move(int entityId, int newX, int newY)
map.CountAt(int x, int y) -> int
EntityEnumerator map.GetEntitiesAt(int x, int y)
```

### DetTagMap

```csharp
map.AddTag(int x, int y, string tag)
map.RemoveTag(int x, int y, string tag)
map.HasTag(int x, int y, string tag) -> bool
map.HasAllTags(int x, int y, IEnumerable<string> tags) -> bool
map.CountAt(int x, int y) -> int
IReadOnlyList<string> map.GetTags(int x, int y)
```

### DetFlowField

```csharp
field.Get(int x, int y) -> byte          // direction
field.GetCost(int x, int y) -> Fix64
field.Set(int x, int y, byte dir, Fix64 cost)
field.Reset()
DetFlowField.Blocked = 255
```

### DetTable

```csharp
int  table.Spawn()
void table.Despawn(int id)
bool table.IsAlive(int id)
DetCol<T>    table.AddCol<T>(string name)
DetStringCol table.AddStringCol(string name)
DetCol<T>    table.GetCol<T>(string name)
DetStringCol table.GetStringCol(string name)
IEnumerable<int> table.GetAlive()        // 0..HighWater, alive only
int table.HighWater
```

### DetPathfinder / DetPath

```csharp
new DetPathfinder(int width, int height)
DetPath pf.FindPath(int sx, int sy, int gx, int gy,
    DetBitLayer walkable,
    DetLayer<byte>? unitCount = null,
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

### Snapshot

```csharp
byte[] Snapshot.Serialize(DetMap map)
DetMap Snapshot.Deserialize(byte[] data)   // throws InvalidDataException on bad magic/version
// Also accessible via: map.ToBytes() / DetMap.FromBytes(data)
```
