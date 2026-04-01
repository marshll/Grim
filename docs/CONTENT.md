# Content Pipeline

## Directory Structure

- content/abilities
- content/npcs
- content/items
- content/quests
- content/zones
- content/models

## Format

- JSON source files are the source of truth.
- IDs should be lowercase and stable.
- References should use IDs, not file paths.

## Validation Rules

- Every content record must have an id.
- No duplicate IDs per content type.
- Unknown references should fail validation.

## Model Registry

- `content/models/registry.json` maps stable model IDs to glTF files.
- Static zone objects can set optional `modelId`; the server replicates it in snapshots.
- Client attempts runtime glTF load by `modelId`; if loading fails, renderer falls back to debug mesh.

Example:

```json
{
	"models": [
		{
			"id": "obelisk_v1",
			"gltfPath": "obelisk_v1/obelisk_v1.gltf",
			"scale": 1.25
		}
	]
}
```