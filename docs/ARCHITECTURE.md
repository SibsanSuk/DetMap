# DetMap Architecture

DetMap is designed for games that outgrow the usual "inspect objects one by one in the editor" workflow.

The core goal is not only determinism.

The larger goal is to make large simulation state understandable, searchable, editable, and maintainable for years.

## Positioning

DetMap should be read as an **inspectable simulation database**.

It is not primarily:

- an ECS replacement
- an ORM
- a Unity `GameObject` hierarchy replacement

It is primarily:

- a deterministic state container
- a data model for large-scale simulation
- a debugging surface for developers
- a stable foundation for external tools such as browser-based inspectors

## Why Table-Centric

When a game has hundreds or thousands of units, deposits, jobs, requests, and events, humans stop reasoning about state through scene objects.

They switch to tables.

Tables are easier to:

- scan
- search
- filter
- sort
- diff
- validate
- edit in bulk

For that reason, DetMap treats `Table` as the primary surface for sparse gameplay state.

If a developer needs to understand why a worker is stuck, the answer should be visible in a row, not hidden inside a runtime object graph.

## Core Model

DetMap uses a small set of concepts with different jobs.

### `Database`

`DetSpatialDatabase` is the top-level simulation state.

It owns:

- dimensions
- global values
- layers
- tables
- stores
- tick progression

### `Layer`

`Layer` is for dense per-cell data.

Use a layer when the question is:

- "what value does this cell have?"
- "what is true across most or all cells?"

Examples:

- height
- fertility
- biome
- terrain cost
- walkable
- resource type per tile
- resource amount per tile

### `Table`

`Table` is for sparse row-based state keyed by row id.

Use a table when the question is:

- "what records exist?"
- "what state does this object have?"
- "what should a developer be able to inspect directly?"

Examples:

- workers
- buildings
- resource deposits
- jobs
- reservations
- leaderboard entries

Tables are the main debugging and operations surface.

If humans need to inspect it, search it, or edit it, it probably belongs in a table column.

### `Store`

`Store` is for sidecar data keyed by row id that exists to support a subsystem.

Use a store when the data is:

- runtime-owned
- derived or cache-like
- not the best primary debugging surface

Example:

- `DetPathStore`

### `Index`

Some structures exist to make queries fast rather than to be the main place humans read state.

`DetCellIndex` fits this role well.

It is a spatial index over row ids.

It answers:

- which row ids are in this cell?
- how many row ids are in this cell?

It should not be the only place that important gameplay truth is stored.

## Source of Truth Rules

These rules keep the architecture understandable.

### 1. Important gameplay state should be visible in tables

If developers will ask questions like these, the answer should usually be in columns:

- where is this worker?
- where is it going?
- what is it doing?
- who owns this object?
- how much resource remains?
- why is this job blocked?

Typical visible columns:

- `cellX`
- `cellY`
- `destinationX`
- `destinationY`
- `state`
- `ownerId`
- `amount`
- `priority`

### 2. Indexes should support the source of truth, not replace it

`DetCellIndex` may mirror position data for fast spatial query.

That is useful.

But if position matters to developers, position should still be readable from table columns.

### 3. Stores should hold subsystem state, not business meaning

`current path` is a good store candidate.

`job`, `hp`, `resourceTypeId`, and `amount` are not.

Those belong in columns because they are normal gameplay attributes.

### 4. Dense map facts belong in layers

If the data is about the map itself rather than about a sparse object, use a layer.

Examples:

- tile height
- water
- fertility
- temperature
- tile resource type

## Systems

DetMap is easiest to reason about when systems are behavior and the database is state.

That means:

- systems read tables, layers, and stores
- systems compute decisions
- systems write results back into tables, layers, and stores
- systems avoid hidden long-lived state when possible

This is similar to service-oriented thinking:

- the data store is central
- systems are workers over shared state
- a row can be inspected without attaching a debugger to a specific runtime object

The goal is not literal microservices.

The goal is the same operational clarity:

- explicit state
- explicit writes
- explicit ownership

## Tooling Direction

DetMap is intended to support tools that operate on the same state model developers use in code.

The first important tool is a snapshot viewer.

That means a simple HTML or browser-based tool that can load a saved snapshot file and inspect the database offline.

This should come before any realtime web server integration.

That tool should treat tables and layers as first-class surfaces.

Later, a realtime inspector can reuse the same model, but it should be treated as a second phase after the core data model is stable.

### Inspector goals

Developers should be able to:

- list all tables and layers
- inspect schema
- search rows by id or text
- filter rows by values
- sort rows by columns
- inspect all rows changed this tick
- inspect all row ids at a given cell
- edit values safely
- export or diff snapshots

For the first phase, "edit values safely" can be deferred.

Read-only inspection of saved snapshots already solves a large part of the debugging and operations problem.

### Live editing rules

Live editing is useful, but it should not break determinism or corrupt runtime state.

It is not the first priority.

The first milestone is snapshot-first inspection.

Recommended rules:

- read from a stable per-tick snapshot
- queue writes as explicit command batches
- apply edits at a safe tick boundary
- record who changed what and when

This keeps the runtime predictable and makes tooling trustworthy.

In practice, this means tooling should stage a `DetDbCommandList` rather than mutating live state directly.

For generic tools, the database should also expose schema metadata directly.

That means a tool should be able to ask the runtime:

- which layers exist
- which tables exist
- which columns each table contains
- which stores exist

without hard-coding gameplay types up front.

## Practical Modeling Heuristics

When choosing where data should live, use these questions.

### Put it in a `Layer` when:

- it is conceptually attached to cells
- it exists for most or all cells
- it is natural to ask for it by coordinate

### Put it in a `Table` when:

- it represents a sparse record or object
- it has lifecycle such as create, remove, claim, complete, deplete
- developers will inspect or edit it directly

### Put it in a `Store` when:

- it is subsystem-specific
- it is derived or cache-like
- it is useful for execution but not ideal as the main human-facing representation

### Put it in an index like `DetCellIndex` when:

- spatial lookup speed matters
- multiple records may occupy the same cell
- the structure exists to accelerate query

## Example

### Workers

Use a table for worker state:

- `name`
- `state`
- `cellX`
- `cellY`
- `destinationX`
- `destinationY`
- `jobId`
- `hp`

Use a cell index as a spatial index over worker row ids.

Use a path store for current path data.

This gives both:

- good runtime query performance
- good human inspectability

### Resource deposits

Use a table when deposits are sparse objects:

- `resourceTypeId`
- `amount`
- `cellX`
- `cellY`
- `depleted`

Use a layer instead when the resource is truly tile-native and dense across the map:

- `resourceType`
- `resourceAmount`

## Design Consequences

This architecture leads to a few strong preferences.

- Public API should make `Table` and `Layer` obvious.
- Important state should not be hidden only in helper structures.
- Tooling should read the same model gameplay code writes.
- Runtime helpers such as stores and indexes should stay explicit, but secondary.
- Naming should favor concepts developers can guess correctly under pressure.

## Summary

DetMap is designed so that large game state remains operable by humans.

The core idea is simple:

- `Layer` for dense map data
- `Table` for sparse inspectable records
- `Store` for sidecar subsystem state
- indexes such as `DetCellIndex` for fast query
- systems as explicit logic over shared state

That model supports both determinism and the more important long-term goal:

large simulations that developers can still understand and control.
