# DetMap Schema Guide

This document defines the target data schema for `.dmap`.

The goal is to keep the system easy to learn.

Users should not need a different mental model for each kind of data.

The preferred model is:

- metadata describes the document
- definitions live in tables
- runtime state lives in tables
- dense per-cell facts live in layers
- sidecar subsystem state lives in stores

## Learning Model

A user should be able to understand a `.dmap` file by answering five questions.

1. What document is this
2. What kinds of things exist
3. What instances are placed or active
4. What does each cell say
5. What runtime caches support systems

Those five questions map to these concepts:

- `DocumentMetadata`
- `Definition Tables`
- `Runtime Tables`
- `Layers`
- `Stores`

This is the recommended learning path.

## Top-Level Structure

The top-level structure of a `.dmap` document should be read like this:

- `DocumentMetadata`
- `Definitions`
- `Tables`
- `Layers`
- `Stores`
- `Globals`

These names are conceptual groups.

They do not all need to be serialized as separate binary sections on day one.

The important part is that users can understand the file in these terms.

## Keep Definitions as Tables

Definitions should be modeled as tables whenever possible.

This is the simplest approach for users because they only need to learn one main inspection surface:

- row
- column
- id
- reference

Good:

- `SpatialDefinitions`
- `ResourceDefinitions`
- `AgentDefinitions`

Avoid introducing a second custom format if a table can represent the data clearly.

## Derived Columns Are Allowed

Readable "reading model" columns are allowed when they help users understand a table without opening several related tables.

Examples:

- `storageSummary`
- `layoutPreview`
- `connectorSummary`
- `taskLabel`

This is useful when the source-of-truth schema is normalized for systems, but users still need a table that reads clearly at a glance.

### Rule

Derived columns are not source-of-truth data.

They are generated from source tables or source columns.

Systems should write the source data only.

Inspectors and editors should treat derived columns as read-only unless an edit is translated back into source-table edits and then regenerated.

### Naming

Use names that clearly read as presentation or summary data.

Prefer suffixes such as:

- `Summary`
- `Preview`
- `Label`
- `Display`

Avoid names that sound like primary state, such as:

- `storage`
- `capacity`
- `amount`

### Example

Good pattern:

- source table: `SpatialStorageCapacities`
- readable column: `SpatialDefinitions.storageSummary`

In this model:

- `SpatialStorageCapacities` is the truth used by systems
- `storageSummary` is a deterministic summary used to help humans read the table quickly

### Optional Metadata Later

If DetMap later adds richer schema metadata, derived columns should be able to declare:

- `isDerived`
- `source`
- `editable`

## Recommended Meaning of Each Group

### `DocumentMetadata`

`DocumentMetadata` is file-level descriptive information.

Examples:

- title
- author
- description
- tags
- revision
- source
- createdAt
- schemaVersion

This can be modeled as:

- a dedicated top-level metadata object

or, if you want maximum uniformity:

- a single-row table such as `DocumentMetadata`

For ease of learning, both are acceptable.

If DetMap later gains metadata at many scopes, `DocumentMetadata` is the safer public term.

### `Definitions`

`Definitions` are immutable reference tables shared by many rows in runtime tables.

Definitions answer questions like:

- what kinds exist
- what shape does this thing have
- what connectors does it expose
- how can it rotate

Definitions should not contain live per-instance state.

### `Tables`

`Tables` contain runtime rows and relational state.

Examples:

- placed objects
- agents
- jobs
- reservations
- inventories
- requests

### `Layers`

`Layers` contain dense per-cell facts.

Examples:

- height
- terrain type
- water
- roads
- fertility
- temperature

### `Stores`

`Stores` contain subsystem-owned sidecar data.

Examples:

- path payloads
- variable-sized temporary search data
- other cache-like structures

### `Globals`

`Globals` contain document-wide scalar values.

Examples:

- current tick
- weather phase
- economy multiplier

## Generic Naming for Multi-Cell Spatial Objects

Do not make the schema depend on game words such as `Building` unless the project is intentionally domain-specific.

For broad use, prefer:

- `SpatialDefinitions`
- `Placements`
- `OccupancyLayer`
- `ConnectionPoints`

This works for:

- buildings
- machines
- facilities
- infrastructure nodes
- military emplacements
- academic layout objects
- research-site structures

## Recommended First Schema

If the goal is to stay easy to learn, start with this minimal schema.

### `DocumentMetadata`

Fields:

- `title`
- `description`
- `author`
- `tags`
- `revision`
- `schemaVersion`

### `SpatialDefinitions`

Each row describes one placeable spatial type.

Columns:

- `definitionId`
- `name`
- `category`
- `footprintMaskText`
- `connectionMaskText`
- `anchorX`
- `anchorY`
- `rotationMode`

Optional later:

- `displayColor`
- `notes`
- `definitionVersion`

### `Placements`

Each row describes one placed instance on the grid.

Columns:

- `placementId`
- `definitionId`
- `cellX`
- `cellY`
- `rotation`
- `state`
- `ownerId`

This table is the main runtime record for placed spatial objects.

### `Agents`

Optional runtime table for moving rows.

Columns:

- `agentId`
- `cellX`
- `cellY`
- `destinationX`
- `destinationY`
- `state`
- `definitionId`

### `ResourceDefinitions`

Optional reference table for resource kinds.

Columns:

- `resourceDefinitionId`
- `name`
- `category`
- `unitLabel`

### `ResourceNodes`

Optional runtime table for spatial resource instances.

Columns:

- `resourceNodeId`
- `resourceDefinitionId`
- `cellX`
- `cellY`
- `amount`
- `state`

## Why Text Masks Are Acceptable

Text-based masks are good definition data.

They are:

- readable
- diffable
- easy to author
- easy to inspect in a browser tool

Examples:

- `footprintMaskText`
- `connectionMaskText`

These should be treated as source definition data.

At load time, they can be compiled into:

- footprint offsets
- connection point offsets
- rotated variants
- bounds

That compiled form does not need to be the first thing users learn.

## Runtime Rules

Keep these rules simple:

1. Definitions are immutable.
2. Runtime tables reference definitions by id.
3. Layers describe dense map facts.
4. Stores describe subsystem sidecar state.
5. Indexes accelerate query and should not replace source-of-truth tables.
6. Derived columns improve readability but must remain generated, not authoritative.

## Authoring and Save Files

A `.dmap` file does not need to represent only an empty or first-tick world.

A document may represent:

- an early empty map
- a developed city
- a mid-simulation scenario
- a benchmark setup
- a research layout

The file should be understood as a document snapshot, not as a special "first step" format.

## Recommendation

If simplicity is the priority, use this rule:

- treat definitions as tables
- keep metadata clearly named
- keep runtime rows in tables
- keep dense space in layers
- keep caches in stores
- use derived columns only as read-only reading surfaces

This gives DetMap a schema that is easy to teach, easy to inspect, and broad enough for games and non-game simulation work.
