# DetSnapshot Viewer Contract

This document defines the first-phase contract for an offline DetMap snapshot viewer.

The goal is simple:

- load a `DetSnapshot` file
- inspect the saved database in a browser
- do it without a running game process
- do it without a realtime web server

## Scope

The viewer is an offline inspection tool.

Typical flow:

1. the game writes a `.dmap` snapshot file
2. the user opens an HTML page
3. the page loads the snapshot file from disk
4. the page parses the snapshot
5. the page renders database state for inspection

This first phase is intentionally read-only.

## Non-Goals

These are not required for phase 1:

- live connection to a running Unity process
- realtime sync
- editing live simulation state
- command submission back into the game
- lockstep replay controls
- write-back to `.dmap`

Those can come later after the core data model and snapshot contract are stable.

## Input Contract

The viewer input is a single `DetSnapshot` binary file.

The file format is defined by [DetSnapshot.cs](/Users/sibsan/GitHub/DetMap/src/DetMap/Serialization/DetSnapshot.cs).

The viewer must validate:

- magic = `DMAP`
- supported version

If validation fails, the viewer should stop and show a clear error.

## Parsed Output Model

The viewer should normalize the binary snapshot into a browser-friendly model.

Suggested shape:

```ts
type ViewerSnapshot = {
  version: number;
  tick: bigint;
  width: number;
  height: number;
  globals: ViewerGlobal[];
  layers: ViewerLayer[];
  tables: ViewerTable[];
  stores: ViewerStore[];
};

type ViewerGlobal = {
  key: string;
  rawValue: bigint;
  displayValue: string;
};

type ViewerLayer = {
  name: string;
  kind: string;
  data: unknown;
};

type ViewerTable = {
  name: string;
  highWater: number;
  freeRowIds: number[];
  columns: ViewerColumn[];
  rows: ViewerRow[];
};

type ViewerColumn = {
  name: string;
  kind: string;
  isDerived: boolean;
  source: string;
  isEditable: boolean;
};

type ViewerRow = {
  rowId: number;
  exists: boolean;
  values: Record<string, unknown>;
};

type ViewerStore = {
  name: string;
  kind: string;
  data: unknown;
};
```

The exact JavaScript structure is flexible, but the viewer should expose the same information content.

## Required Data Support

The first viewer must understand all public snapshot data families.

### Globals

Show:

- key
- raw `Fix64` value
- formatted decimal value

### Tables

The viewer must parse:

- table name
- column schema
- `highWater`
- free-list contents
- row existence
- all column values

The viewer should build rows from `0..highWater-1`.

Each row should expose:

- `rowId`
- `exists`
- values for every column

This is important because deleted rows are often useful when debugging allocator behavior or recycled row ids.

### Layers

The viewer must parse:

- `DetValueLayer<byte>`
- `DetValueLayer<int>`
- `DetValueLayer<Fix64>`
- `DetBitLayer`
- `DetCellIndex`
- `DetTagLayer`
- `DetFlowLayer`

Suggested rendering:

- value layers: inspect by coordinate, optionally heatmap later
- bit layers: inspect by coordinate, optionally compact bitmap view
- cell index: show count per cell and row ids at a selected cell
- tag layer: show tags per populated cell
- flow layer: show direction and cost per cell

### Stores

The first viewer only needs store support for:

- `DetPathStore`

Show:

- store name
- slot count
- per row id: path length, current step
- optional step list on row expansion

## Required Viewer Screens

Phase 1 should support these views.

### 1. Snapshot Summary

Show:

- file name
- snapshot version
- tick
- grid size
- table count
- layer count
- store count

### 2. Globals View

Show globals in a simple key/value table.

### 3. Tables View

For each table:

- list columns and kinds
- show derived/read-only/source metadata when present
- list rows
- allow filtering by row id
- allow text search over visible values
- allow hiding deleted rows

The default row order should be ascending `rowId`.

### 4. Layers View

For each layer:

- show name and kind
- allow coordinate inspection
- show raw values at a selected cell

The first version does not need advanced map rendering.

A coordinate-driven inspector is enough.

### 5. Stores View

For each store:

- show store name
- show supported kind
- list entries by row id

## Precision Rules

`Fix64` values must not be converted through `float` or `double` and then treated as truth.

The viewer should preserve:

- raw integer value
- formatted human-readable value

Recommended display:

- keep `rawValue`
- also show decimal text for convenience

If JavaScript numeric precision is a concern, use `BigInt` for raw values.

## Row Semantics

The viewer must distinguish:

- row slot exists
- row slot does not exist

This comes from the table alive/existence column in the snapshot payload.

The viewer should not silently drop deleted rows unless the user explicitly hides them.

## Error Handling

The viewer should fail clearly for:

- wrong magic
- unsupported version
- truncated file
- unknown layer kind
- unknown column kind

Preferred behavior:

- show the error section in the UI
- include offset or parsing stage when possible

## Versioning

The viewer should be version-aware.

That means:

- read version early
- route to a version-specific parser if needed
- keep parsing logic isolated per snapshot version

Do not mix multiple snapshot versions into one loose parser with hidden assumptions.

## Phase 1 Deliverable

A phase-1 viewer is successful if it can:

- open a `.dmap` file
- parse `DetSnapshot` versions 2 and 3
- render globals
- render tables and rows
- render layer metadata and coordinate inspection
- render path store contents
- do all of this offline in a browser

That alone already provides major value for debugging and operating large simulation state.

## Later Phases

Possible later phases:

- richer grid visualizations
- table diff between two snapshots
- snapshot compare by tick
- export to JSON for analysis
- staged edits via `DetDbCommandList`
- realtime integration through a local web server

These should stay secondary until the offline snapshot viewer is solid.
