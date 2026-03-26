# DetMap Naming Guide

This document defines the target public API naming for DetMap.

DetMap is a deterministic spatial database.

The naming should help developers think in terms of:

- database state
- 2D grid data
- searchable records
- explicit indexes and stores

It should not force users to think in terms of engine internals.

## Design Principles

DetMap naming follows two simple rules.

### 1. Prefer database words for inspectable state

If humans will browse it in a table, search it, edit it, or diff it, use database vocabulary.

Examples:

- `Database`
- `Table`
- `Column`
- `Row`
- `Index`
- `Store`
- `Snapshot`
- `DbCommandList`

### 2. Use spatial words only where space actually matters

Use spatial vocabulary for grid-native concepts.

Examples:

- `Grid`
- `Cell`
- `Layer`
- `Region`
- `Bounds`

## Canonical Public Vocabulary

### `Database`

`Database` is the top-level runtime state container.

Canonical type:

- `DetSpatialDatabase`

Use `Database` for the root state object, not `Map`.

`DetMap` remains the project name and assembly name.

### `Grid`

`Grid` is the registry of cell-based data and dimensions.

Canonical type:

- `DetGrid`

### `Layer`

`Layer` means dense cell-attached data.

Canonical types:

- `DetValueLayer<T>`
- `DetBitLayer`
- `DetTagLayer`
- `DetFlowLayer`

Use `Layer` when the value is conceptually attached to cells and likely exists for most or all cells.

### `Index`

`Index` means a structure that accelerates query and should not be mistaken for the main human-facing source of truth.

Canonical type:

- `DetCellIndex`

Use `Index` when the structure primarily answers lookup questions such as:

- which row ids are in this cell
- how many row ids are in this cell

Do not use `Layer` for an index.

### `Table`

`Table` means sparse row-based state keyed by row id.

Canonical type:

- `DetTable`

Tables are the primary inspection surface for gameplay state.

### `Column`

`Column` means schema data that belongs to a table.

Canonical types:

- `DetColumn<T>`
- `DetStringColumn`

Public methods:

- `CreateByteColumn`
- `CreateIntColumn`
- `CreateFix64Column`
- `CreateStringColumn`
- `GetByteColumn`
- `GetIntColumn`
- `GetFix64Column`
- `GetStringColumn`

Low-level generic API:

- `CreateColumn`
- `GetColumn`

Do not use `Col` in public API.

### `Row`

`Row` is the public term for one record in a table.

Use row-centric verbs:

- `CreateRow`
- `DeleteRow`
- `RowExists`
- `GetRowIds`

Do not use:

- `Insert`
- `Delete`
- `Exists`

These names are too storage-centric or too ambiguous in a DB-first API.

### `Store`

`Store` means sidecar data keyed by row id but not modeled as normal schema columns.

Canonical type:

- `DetPathStore`

Use a store when the data is subsystem-owned, variable-sized, or cache-like.

Examples:

- `current path` -> `Store`
- `hp` -> `Column`
- `destinationX` -> `Column`

### `Snapshot`

`Snapshot` means a serialized point-in-time image of the database.

Canonical type:

- `DetSnapshot`

### `DbCommandList`

`DbCommandList` means an ordered set of deferred database mutations that can be applied at a safe simulation boundary.

Canonical type:

- `DetDbCommandList`

### `Definition`

`Definition` means immutable design-time data shared by many runtime instances.

Canonical type:

- `SpatialDefinition`

Do not use `Def` in public API.

## Public API Shape

### Root Object

Good:

- `new DetSpatialDatabase(width, height)`
- `DetSpatialDatabase.FromBytes(data)`
- `DetDbCommandApplier.ApplyFrame(database, commandList)`

Avoid:

- `new DetMap(...)` as the public root type name

### Grid

Good:

- `CreateByteLayer`
- `CreateIntLayer`
- `CreateFix64Layer`
- `CreateBitLayer`
- `CreateValueLayer`
- `CreateCellIndex`
- `CreateTagLayer`
- `CreateFlowLayer`
- `GetByteLayer`
- `GetIntLayer`
- `GetFix64Layer`
- `GetValueLayer`
- `GetBitLayer`
- `GetCellIndex`
- `GetTagLayer`
- `GetFlowLayer`

Avoid:

- `CreateBooleanLayer`
- `CreateEntityLayer`
- noun-only getters

### Cell Index

Good:

- `Place(rowId, x, y)`
- `MoveTo(rowId, x, y)`
- `Remove(rowId)`
- `CountAt(x, y)`
- `GetRowIdsAt(x, y)`

Avoid:

- `Add(entityId, x, y)`
- `Move(entityId, x, y)`
- `GetEntitiesAt`

The index is indexing rows, not gameplay entities as a special category.

### Table

Good:

- `CreateRow()`
- `DeleteRow(id)`
- `RowExists(id)`
- `GetRowIds()`
- `PeekNextRowId()`

Avoid:

- `Insert()`
- `Delete(id)`
- `Exists(id)`
- `GetAliveIds()`

`GetRowIds()` is clearer because the table is not necessarily storing "alive" game entities. It may also store jobs, events, requests, or leaderboard rows.

### Snapshot and Commands

Good:

- `DetSnapshot.Serialize(database)`
- `DetSnapshot.Deserialize(data)`
- `DetDbCommandList`

Avoid:

- bare `Snapshot` as the public type name
- delegate-based mutation queues for lockstep edits

## Checks and Verbs

Use one verb per kind of action.

- creation: `Create`
- retrieval: `Get`
- deletion: `Delete`
- placement into a cell index: `Place`
- movement within a cell index: `MoveTo`

Use one word per kind of question.

- `RowExists` for table row identity
- `Has` for tags, flags, or properties
- `Contains` for membership or region inclusion

## Terms To Avoid

Avoid these in the public API unless the domain truly requires them:

- `Entity`
- `Spawn`
- `Character`
- `Unit`
- `Col`
- `Def`
- `Map` for the root state object

These words either skew too game-specific, too abbreviated, or too implementation-driven.

## Summary

DetMap public naming should read like a spatial database:

- `DetSpatialDatabase`
- `DetGrid`
- `DetTable`
- `DetColumn<T>`
- `DetValueLayer<T>`
- `DetBitLayer`
- `DetCellIndex`
- `DetPathStore`
- `DetSnapshot`
- `DetDbCommandList`

The main rule is simple:

- inspectable truth uses database words
- dense cell data uses layer words
- query helpers use index words
