# Fixtures

Fixture drawings for smoke-harness verification of the Chamber19 AutoCAD MCP plugin.

## Files

| File | Purpose |
|------|---------|
| `sample.dxf` | Main fixture drawing; open this in AutoCAD before running the smoke test for full coverage |
| `xref.dxf` | External-reference target; `sample.dxf` attaches it via the relative path `./xref.dxf` |

> **Format note:** These files are in **DXF R2010** (text-based exchange format). AutoCAD opens
> DXF files directly via **File → Open** — no rename or conversion required. The MCP plugin
> does not distinguish between DXF and DWG; it operates on whatever drawing is currently active.

---

## Expected results when AutoCAD has `sample.dxf` open

### `chamber19_list_layers`

6 layers (layer `0` and `Defpoints` are always present; the four user layers are below):

| Name | Color index | Frozen | Locked |
|------|-------------|--------|--------|
| `0` | 7 | false | false |
| `Defpoints` | 7 | false | false |
| `PLINES` | 2 | false | false |
| `NOTES` | 3 | false | false |
| `DIMS` | 4 | **true** | false |
| `DETAIL` | 5 | false | **true** |

### `chamber19_list_blocks`

2 user-defined blocks (anonymous insertion wrappers, xref, and layout blocks are excluded):

| Name | `isDynamic` | Notes |
|------|-------------|-------|
| `BLOCK_A` | false | Has attributes TAG1, TAG2 |
| `BLOCK_B` | false | No attributes |

### `chamber19_get_block_attributes`

- `BLOCK_A` → `[{"tag":"TAG1","value":"FIXTURE_TAG1"},{"tag":"TAG2","value":"FIXTURE_TAG2"}]`
- `BLOCK_B` → `[]` (no attributes)

### `chamber19_list_xrefs`

1 xref:

| Name | Path | `isLoaded` | `isAttached` |
|------|------|------------|--------------|
| `XREF_TARGET` | `./xref.dxf` | false (not resolved at open time without host file) | true |

> `isLoaded` may be `false` until AutoCAD resolves the relative xref path. This is expected
> when the file is opened from a directory that does not contain `xref.dxf` next to it.

### `chamber19_list_layouts`

2 layouts sorted by `tabOrder`:

| Name | `tabOrder` | `isCurrent` |
|------|------------|-------------|
| `Model` | 0 | true (when opened; may differ if another layout was last active) |
| `Layout1` | 1 | false |

### `chamber19_count_entities_by_layer`

Counts are for **model space** (TILEMODE=1, the default when the file opens):

| Layer | Expected count | Entity types |
|-------|----------------|--------------|
| `PLINES` | 6 | 3 closed LWPOLYLINE + 3 open LWPOLYLINE |
| `NOTES` | 4 | 2 TEXT + 2 MTEXT |

### Polyline geometry (layer `PLINES`)

| # | Closed | Shape | Approximate size |
|---|--------|-------|-----------------|
| 1 | Yes | Triangle | 40 × 30 units |
| 2 | Yes | Square | 10 × 10 units |
| 3 | Yes | Rectangle | 80 × 40 units |
| 4 | No | L-shape | 50 × 50 units |
| 5 | No | Zigzag (5 vertices) | 80 × 30 units |
| 6 | No | Step (4 vertices) | 60 × 20 units |

### Text entities (layer `NOTES`)

| Type | Content |
|------|---------|
| `TEXT` (DBText) | `FIXTURE NOTE 1` at (0, 100) |
| `TEXT` (DBText) | `FIXTURE NOTE 2` at (0, 110) |
| `MTEXT` | `FIXTURE MTEXT 1` at (0, 120) |
| `MTEXT` | `FIXTURE MTEXT 2` at (0, 135) |

---

## Expected results when AutoCAD has `xref.dxf` open

| Tool | Expected |
|------|----------|
| `chamber19_list_layers` | 2 layers: `0`, `XREF_CONTENT` (color 6) |
| `chamber19_count_entities_by_layer XREF_CONTENT` | 2 entities (1 LWPOLYLINE + 1 TEXT) |

---

## Smoke test

See [`../tools/smoke-test.ps1`](../tools/smoke-test.ps1) for the automated HTTP-level smoke
harness. The smoke test verifies port discovery, bearer auth, and MCP protocol responses
without requiring specific drawing content to be open. Open `sample.dxf` in AutoCAD first to
exercise the drawing-read tools as well.
