# DetMap

A deterministic in-memory spatial database for city-builder and RTS games built on C# / Unity.

Every read and write goes through **Fix64** fixed-point arithmetic (no `float`, no `double` in game logic), so simulation state is **bit-identical across all platforms** — Windows, macOS, Linux, iOS, Android, WebGL.

---

## Table of Contents

- [Architecture](#architecture)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Layers](#layers)
  - [DetValueLayer\<T\> — dense value grid](#detvaluelayert--dense-value-grid)
  - [DetBitLayer — packed booleans](#detbitlayer--packed-booleans)
  - [DetEntityLayer — spatial entity index](#detentitylayer--spatial-entity-index)
  - [DetTagLayer — multi-tag per cell](#dettaglayer--multi-tag-per-cell)
  - [DetFlowLayer — direction + cost grid](#detflowlayer--direction--cost-grid)
- [Tables](#tables)
  - [DetTable — column-oriented entity store](#dettable--column-oriented-entity-store)
- [DetPathStore — path store](#detpathstore--path-store)
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
        ├── DetValueLayer<T>    ← dense value grid  (byte / int / Fix64)
        ├── DetBitLayer      ← bit-packed boolean grid
        ├── DetEntityLayer   ← flat-array linked-list spatial entity index
        ├── DetTagLayer      ← sparse multi-tag per cell
        └── DetFlowLayer     ← direction (byte) + cost (Fix64) per cell

DetTable                     ← column-oriented entity store
  ├── DetColumn<T>              ← typed value column (byte / int / Fix64)
  └── DetStringColumn           ← string column

DetPathStore                 ← named path store (entityId → DetPath), serialized by Snapshot

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
var building  = map.Grid.CreateValueLayer("building",  DetType.Int);
var height    = map.Grid.CreateValueLayer("height",    DetType.Fix64);
var walkable  = map.Grid.CreateBitLayer("walkable");
var units     = map.Grid.CreateEntityLayer("units");
var services  = map.Grid.CreateTagLayer("services");

walkable.SetAll(true);

// 3. Set global state
map.SetGlobal("treasury",   Fix64.FromInt(1000));
map.SetGlobal("population", Fix64.FromInt(0));

// 4. Create entity table
var chars   = map.CreateTable("characters");
var nameCol = chars.CreateStringColumn("name");
var jobCol  = chars.CreateColumn("job", DetType.Byte);
var hpCol   = chars.CreateColumn("hp", DetType.Int);

// 5. Place a building
var houseDef = new BuildingDefinition("house", 2, 2, 1);
BuildingPlacer.Place(map.Grid, 10, 10, houseDef, building, walkable);

// 6. Spawn entities
int id = chars.Insert();
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
var pathStore = map.CreatePathStore("unitPaths");
pathStore.Set(id, path);
ref DetPath p = ref pathStore.Get(id);
if (p.IsValid && !p.IsComplete) { p.Advance(); var (nx, ny) = p.Current(64); }

// 10. Save / Load
byte[] save = map.ToBytes();
var loaded  = DetMap.Core.DetMap.FromBytes(save);
```

---

## Layers

All layers are registered by name on the grid. Each has a `DirtyRect` that tracks which cells changed since the last `ClearDirty()` call — useful for incremental rendering.

### DetValueLayer\<T\> — dense value grid

Backed by a flat array `T[width * height]`. Supported element types: `byte`, `int`, `Fix64`.

```csharp
// Creation — DetType token enforces compile-time determinism
DetValueLayer<byte>  flags  = map.Grid.CreateValueLayer("flags",  DetType.Byte);
DetValueLayer<int>   ids    = map.Grid.CreateValueLayer("ids",    DetType.Int);
DetValueLayer<Fix64> height = map.Grid.CreateValueLayer("height", DetType.Fix64);

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
DetValueLayer<int> ids2 = map.Grid.GetValueLayer<int>("ids");
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

### DetEntityLayer — spatial entity index

Zero-allocation flat-array linked list. Maps entity IDs to grid cells. Supports multiple entities per cell. Entity IDs are assigned by `DetTable.Insert()`.

```csharp
DetEntityLayer units = map.Grid.CreateEntityLayer("units");

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
DetEntityLayer u = map.Grid.GetEntityLayer("units");
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

Tables are column-oriented entity stores — think of each row as an entity, each column as a component. IDs are recycled deterministically using a LIFO free list.

### DetTable — column-oriented entity store

```csharp
DetTable chars = map.CreateTable("characters");

// Add columns before spawning entities
DetStringColumn nameCol = chars.CreateStringColumn("name");
DetColumn<byte> jobCol  = chars.CreateColumn("job", DetType.Byte);
DetColumn<int>  hpCol   = chars.CreateColumn("hp", DetType.Int);
DetColumn<Fix64> xpCol  = chars.CreateColumn("xp", DetType.Fix64);

// Spawn entity — returns next available ID (recycles despawned IDs, LIFO)
int id = chars.Insert();             // 0
nameCol.Set(id, "Alice");
jobCol.Set(id, 1);
hpCol.Set(id, 100);
xpCol.Set(id, Fix64.FromInt(0));

int id2 = chars.Insert();            // 1
nameCol.Set(id2, "Bob");

// Despawn — frees ID for reuse
chars.Delete(id);                  // id 0 goes into free list

int recycled = chars.Insert();       // 0  (recycled — LIFO)

// Query
bool alive = chars.Exists(0);      // true (recycled)
int  hw    = chars.HighWater;       // 2 (max ID ever assigned + 1)

// Iterate all alive entities in deterministic order (0..HighWater)
foreach (int i in chars.GetAliveIds())
{
    string? name = nameCol.Get(i);
    int     hp   = hpCol.Get(i);
}

// Retrieve table from map
DetTable t = map.GetTable("characters");

// Retrieve column from table
DetColumn<int>  hp2   = t.GetColumn<int>("hp");
DetStringColumn name2 = t.GetStringColumn("name");
```

---

---

## DetPathStore — path store

Named DB-level structure that maps `entityId → DetPath`. Lives on `DetMap` alongside tables — serialized automatically by `Snapshot`.

```csharp
// Create — registered on DetMap, saved by Snapshot
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
    units.Move(0, nx, ny);
}

// Peek next step without advancing
var (px, py) = p.Peek(64);

paths.Clear(0); // reset path for entity 0

// Retrieve from map by name
DetPathStore ps = map.GetPathStore("unitPaths");
```

**Architecture position:**

```text
DetMap
  ├── DetGrid      → spatial layers
  ├── Tables       → entity attributes (hp, name, job)
  └── PathStores   → entity paths  ← here
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
DetValueLayer<byte> unitCount = map.Grid.GetValueLayer<byte>("unit_count"); // optional
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

DetValueLayer<int> buildingLayer = map.Grid.GetValueLayer<int>("building");
DetBitLayer   walkable      = map.Grid.GetBitLayer("walkable");

// Check placement validity before placing
bool ok = BuildingPlacer.CanPlace(map.Grid, ox: 10, oy: 5, house, buildingLayer, walkable);

// Place (writes building ID into buildingLayer, marks cells non-walkable)
if (ok)
    BuildingPlacer.Place(map.Grid, 10, 5, house, buildingLayer, walkable);

// Remove (clears building ID, restores walkability for solid cells)
BuildingPlacer.Remove(map.Grid, 10, 5, house, buildingLayer, walkable);

// CanPlace with extra condition (e.g. must be on flat terrain)
DetValueLayer<Fix64> height = map.Grid.GetValueLayer<Fix64>("height");
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

DetValueLayer<int>    ids  = loaded.Grid.GetValueLayer<int>("building");
DetBitLayer      walk = loaded.Grid.GetBitLayer("walkable");
DetEntityLayer     u    = loaded.Grid.GetEntityLayer("units");
DetTagLayer        svc  = loaded.Grid.GetTagLayer("services");
DetFlowLayer     ff   = loaded.Grid.GetFlowLayer("group_flow");

DetTable  chars = loaded.GetTable("characters");
string?   name  = chars.GetStringColumn("name").Get(0);
int       hp    = chars.GetColumn<int>("hp").Get(0);
Fix64     xp    = chars.GetColumn<Fix64>("xp").Get(0);
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

**What is saved:** layers, globals, tables, path stores, tick.

---

## Determinism Reference

| Source of non-determinism | How DetMap avoids it |
| --- | --- |
| `float` / `double` arithmetic | All game math uses `Fix64` (Q2.2, `long` arithmetic) |
| `Dictionary` iteration order | Schema section uses sorted keys for globals; layer/table order follows insertion order, which is deterministic for sequential setup code |
| Uninitialized memory | `DetEntityLayer.EnsureCapacity` fills new slots to `-1`; all arrays zero-initialized by default |
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
    _map = new DetMap.Core.DetMap(64, 64);

    _buildingLayer = _map.Grid.CreateValueLayer("building", DetType.Int);
    _walkable      = _map.Grid.CreateBitLayer("walkable");
    _units         = _map.Grid.CreateEntityLayer("units");

    _walkable.SetAll(true);

    _table   = _map.CreateTable("units");
    _nameCol = _table.CreateStringColumn("name");
    _hpCol   = _table.CreateColumn("hp", DetType.Int);

    _pathfinder = new DetPathfinder(64, 64);
    _paths      = _map.CreatePathStore("unitPaths");
}

// Per-tick update — call from FixedUpdate or a lockstep loop
void SimulateTick()
{
    _map.AdvanceTick();

    foreach (int id in _table.GetAliveIds())
    {
        ref DetPath p = ref _paths.Get(id);
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
DetTable     map.CreateTable(string name, int capacity = 256)
DetTable     map.GetTable(string name)
map.Tables                               // IReadOnlyDictionary<string, DetTable>
DetPathStore map.CreatePathStore(string name, int capacity = 256)
DetPathStore map.GetPathStore(string name)
map.PathStores                           // IReadOnlyDictionary<string, DetPathStore>
byte[] map.ToBytes()                     // Snapshot.Serialize(map)
DetMap.FromBytes(byte[] data)            // Snapshot.Deserialize(data)
```

### DetGrid

```csharp
grid.Width / grid.Height
grid.InBounds(int x, int y)             // bool
grid.CreateValueLayer<T>(name, DetType<T>)  // DetValueLayer<T>
grid.CreateBitLayer(name)               // DetBitLayer
grid.CreateEntityLayer(name)            // DetEntityLayer
grid.CreateTagLayer(name)               // DetTagLayer
grid.CreateFlowLayer(name)              // DetFlowLayer
grid.GetValueLayer<T>(name)             // DetValueLayer<T>
grid.GetBitLayer(name)                  // DetBitLayer
grid.GetEntityLayer(name)               // DetEntityLayer
grid.GetTagLayer(name)                  // DetTagLayer
grid.GetFlowLayer(name)                 // DetFlowLayer
grid.AllLayers                          // IReadOnlyDictionary<string, IDetLayer>
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

### DetEntityLayer

```csharp
map.Add(int entityId, int x, int y)
map.Remove(int entityId)
map.Move(int entityId, int newX, int newY)
map.CountAt(int x, int y) -> int
EntityEnumerator map.GetEntitiesAt(int x, int y)
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
int  table.Insert()
void table.Delete(int id)
bool table.Exists(int id)
DetColumn<T>    table.CreateColumn<T>(string name, DetType<T> type)  // T = byte | int | Fix64
DetStringColumn table.CreateStringColumn(string name)
DetColumn<T>    table.GetColumn<T>(string name)
DetStringColumn table.GetStringColumn(string name)
IEnumerable<int> table.GetAliveIds()        // 0..HighWater, alive only
int table.HighWater
```

### DetPathStore

```csharp
store.Set(int entityId, DetPath path)
ref DetPath store.Get(int entityId)      // ref — modify in-place
store.Clear(int entityId)
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

### Snapshot

```csharp
byte[] Snapshot.Serialize(DetMap map)
DetMap Snapshot.Deserialize(byte[] data)   // throws InvalidDataException on bad magic/version
// Also accessible via: map.ToBytes() / DetMap.FromBytes(data)
```
