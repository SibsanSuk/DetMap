# DetMap Naming Guide

This document defines the target public API naming for DetMap.

It is intentionally opinionated.

- one concept uses one word
- one category uses one suffix
- one action uses one verb
- public API favors clarity over shorthand

This guide does not preserve legacy naming. Old names are not part of the target design.

## Design Principles

DetMap naming should follow `KISS` and support `SOLID`.

### KISS

- Prefer the simplest name that is still precise.
- Do not keep two words for the same concept.
- Do not require users to learn project-specific jargon when plain words work.
- If a user cannot guess what something is from the name, the name is wrong.

### SOLID

Naming is not architecture by itself, but good naming should reinforce good design.

- `SRP`: a type name should describe one responsibility.
- `OCP`: new APIs should extend an existing naming family instead of inventing a new pattern.
- `LSP`: related APIs should behave consistently enough that their names remain trustworthy.
- `ISP`: interface names should describe the smallest useful capability, not a vague umbrella.
- `DIP`: public abstractions should be named by role and behavior, not storage detail.

## Core Vocabulary

DetMap uses these words as canonical public concepts.

### `Map`

`Map` is reserved for the top-level simulation object.

Examples:

- `DetMap`

Do not use `Map` for grid-backed helper types like entity occupancy, tags, or flow data.

### `Grid`

`Grid` means the spatial registry of layers and map dimensions.

Examples:

- `DetGrid`

### `Layer`

`Layer` means data attached to grid cells.

Canonical public layer types:

- `DetValueLayer<T>`
- `DetBitLayer`
- `DetEntityLayer`
- `DetTagLayer`
- `DetFlowLayer`

All grid-backed public types should use the `Layer` family. Do not introduce public `Map`, `Field`, or `Structure` names for grid-backed data.

### `Table`

`Table` means row-oriented entity storage keyed by entity id.

Examples:

- `DetTable`

### `Column`

`Column` means schema data belonging to a `Table`.

Canonical public column types:

- `DetColumn<T>`
- `DetStringColumn`

Public methods should use:

- `CreateColumn`
- `GetColumn`
- `CreateStringColumn`
- `GetStringColumn`

Do not use `Col` in public API.

### `Store`

`Store` means sidecar data keyed by entity id but not part of the table schema.

Examples:

- `DetPathStore`

Use a `Column` when the data is a normal entity attribute.

Use a `Store` when the data is subsystem-owned state that is better modeled outside the row schema.

Examples:

- `hp`, `job`, `posX`, `posY` -> `Column`
- `current path` -> `Store`

### `Definition`

`Definition` means immutable configuration or design-time data.

More specifically, a `Definition` is an immutable design-time description shared by many runtime instances.

Examples:

- `BuildingDefinition`

`Definition` is not per-instance runtime state.

Do not use `Def` in public API.

### `Query`

`Query` means a read-only search or selection operation.

Examples:

- `QueryEngine`
- `RectQuery`
- `RadiusQuery`

## Public API Rules

### 1. Public names use full words

Use full words unless the abbreviation is universally standard.

Allowed:

- `Id`

Avoid:

- `Col`
- `Def`
- `Info`
- `Mgr`
- `Struct`

### 2. One family, one suffix

Use one suffix per concept:

- grid-backed data -> `Layer`
- row storage -> `Table`
- row field -> `Column`
- sidecar entity data -> `Store`
- immutable config -> `Definition`
- read-only searching -> `Query`

If a new type does not fit one of these families, its role is probably still unclear.

### 3. Creation and retrieval always use verbs

Factories use `CreateX`.

Retrieval uses `GetX`.

Do not use bare noun accessors.

Good:

- `CreateEntityLayer`
- `GetEntityLayer`
- `CreateColumn`
- `GetColumn`

Avoid:

- `Structure<T>`
- `Layer<T>()` as a getter name
- `Table()` as a getter name

### 4. Checks use fixed words

Use one word per type of question:

- `Exists` for identity or row existence
- `Has` for a property, tag, flag, or capability
- `Contains` for membership or spatial inclusion

Examples:

- `table.Exists(id)`
- `tagLayer.HasTag(x, y, "market")`
- `dirtyRegion.Contains(x, y)`

Do not treat `Exists`, `Has`, and `Contains` as interchangeable.

### 5. Enumeration names say what they enumerate

Examples:

- `GetAliveIds`
- `GetEntitiesAt`
- `GetTagsAt`

Avoid short names that hide the returned concept.

Examples to avoid:

- `GetAlive`
- `GetAll`
- `Items`

### 6. Names should describe role, not implementation

Prefer names that describe what the type is for.

Good:

- `DetEntityLayer`
- `DetPathStore`
- `BuildingDefinition`

Avoid:

- `Structure`
- `LinkedEntityMap`
- `RawPathSlots`

Implementation details may still appear in private code when useful.

## Canonical Public API Shape

### Grid

Public grid creation methods:

- `CreateValueLayer<T>(name, type, defaultValue = default)`
- `CreateBitLayer(name)`
- `CreateEntityLayer(name)`
- `CreateTagLayer(name)`
- `CreateFlowLayer(name)`

Public grid retrieval methods:

- `GetValueLayer<T>(name)`
- `GetBitLayer(name)`
- `GetEntityLayer(name)`
- `GetTagLayer(name)`
- `GetFlowLayer(name)`

`Grid` should not expose public retrieval via `Structure<T>()` or other noun-only accessors.

### Table

Public table methods:

- `Insert`
- `Delete`
- `Exists`
- `GetAliveIds`
- `CreateColumn`
- `GetColumn`
- `CreateStringColumn`
- `GetStringColumn`

### Query delegates

Per-cell boolean delegates should use one shared name:

- `CellPredicate`

Do not introduce synonyms like `CellFilter` for the same concept.

## Canonical Renames

These names define the target direction.

| Reject | Use |
| --- | --- |
| `DetLayer<T>` | `DetValueLayer<T>` |
| `DetEntityMap` | `DetEntityLayer` |
| `DetTagMap` | `DetTagLayer` |
| `DetFlowField` | `DetFlowLayer` |
| `DetCol<T>` | `DetColumn<T>` |
| `DetStringCol` | `DetStringColumn` |
| `CreateCol` | `CreateColumn` |
| `GetCol` | `GetColumn` |
| `GetAlive` | `GetAliveIds` |
| `Structure<T>()` | `GetX` retrieval methods |
| `BuildingDef` | `BuildingDefinition` |
| `BuildingId` | `BuildingTypeId` or `TileValue` |
| `CellFilter` | `CellPredicate` |
| `LayerType` | remove |

## Enum Direction

`DetLayerKind` should classify public API families, not mix internal representation and domain language.

Canonical direction:

- `ValueByte`
- `ValueInt`
- `ValueFix64`
- `Bit`
- `Entity`
- `Tag`
- `Flow`

## Before / After

### Grid-backed data

Reject:

```csharp
var units = map.Grid.CreateEntityMap("units");
var unitMap = map.Grid.Structure<DetEntityMap>("units");
```

Use:

```csharp
var units = map.Grid.CreateEntityLayer("units");
var unitLayer = map.Grid.GetEntityLayer("units");
```

### Dense value layers

Reject:

```csharp
var height = map.Grid.CreateLayer("height", DetType.Fix64);
var heightLayer = map.Grid.Layer<Fix64>("height");
```

Use:

```csharp
var height = map.Grid.CreateValueLayer("height", DetType.Fix64);
var heightLayer = map.Grid.GetValueLayer<Fix64>("height");
```

### Table schema

Reject:

```csharp
var workers = map.CreateTable("workers");
var hpCol = workers.CreateCol("hp", DetType.Int);
var jobCol = workers.GetCol<byte>("job");
```

Use:

```csharp
var workers = map.CreateTable("workers");
DetColumn<int> hpColumn = workers.CreateColumn("hp", DetType.Int);
DetColumn<byte> jobColumn = workers.GetColumn<byte>("job");
```

### Definitions

Reject:

```csharp
var house = new BuildingDef("house", 2, 2, Fix64.FromInt(1));
```

Use:

```csharp
var house = new BuildingDefinition("house", 2, 2, 1);
```

### Column vs store

Use a `Column` for normal entity attributes:

```csharp
var hpColumn = workers.CreateColumn("hp", DetType.Int);
var posXColumn = workers.CreateColumn("posX", DetType.Int);
```

Use a `Store` for subsystem-owned sidecar state:

```csharp
var workerPaths = map.CreatePathStore("workerPaths");
```

The name should tell the user whether the data belongs to the entity record or to a supporting subsystem.

## Review Checklist

When adding a new public API, check:

1. Is this a `Map`, `Grid`, `Layer`, `Table`, `Column`, `Store`, `Definition`, or `Query`?
2. Is the chosen word already the canonical word for that concept?
3. Can a new user guess how to create and retrieve it?
4. Can a new user tell whether the data lives on the grid, in a row schema, or in sidecar state?
5. Does the name describe behavior and responsibility rather than storage detail?

If the answer to any of `2` through `5` is "no", rename it before merging.

## Non-Goals

This guide does not require private symbols to use the full public vocabulary.

Short private names are acceptable when:

- they are private
- their meaning is obvious in local context
- they do not leak into public API, public docs, or examples

The main target is a public API that is simple, predictable, and internally consistent.
