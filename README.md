# Grim

MMO-first C# game foundation with a MonoGame client host, a headless server skeleton, shared protocol contracts, and mod-friendly content directories.

## Repository Layout

- src/Grim.Core: MonoGame desktop host.
- src/Grim.Client: game composition layer.
- src/Grim.Engine: reusable engine primitives.
- src/Grim.Shared: protocol and shared contracts.
- src/Grim.Server: headless server and world tick loop.
- tests/Grim.Tests: starter unit tests.
- content: data-driven mod content.
- docs: architecture, networking, content, and modding docs.

## Prerequisites

- .NET SDK 8.0+
- On Linux for DesktopGL runtime: SDL2 and OpenAL packages
- Blender or fbx2gltf (optional, only for FBX import conversion)

## Build

1. Install .NET SDK.
2. Run restore and build:

	 dotnet restore Grim.sln
	 dotnet build Grim.sln

## Run

- Start server:

	dotnet run --project src/Grim.Server/Grim.Server.csproj

- Start client:

	dotnet run --project src/Grim.Core/Grim.Core.csproj

- Start client with explicit identity/endpoint:

	dotnet run --project src/Grim.Core/Grim.Core.csproj -- --client alpha --account account_alpha --host 127.0.0.1 --port 7777

- Start client in editor mode:

	dotnet run --project src/Grim.Core/Grim.Core.csproj -- --editor

- Start two clients against one server (use separate terminals):

	dotnet run --project src/Grim.Core/Grim.Core.csproj -- --client alpha --account account_alpha
	dotnet run --project src/Grim.Core/Grim.Core.csproj -- --client beta --account account_beta

The client and server now emit periodic network console logs (handshake/login and snapshot summaries) so you can verify replication in real time.

First model slice:

- `content/zones/start_zone.json` now supports optional `modelId` on static objects.
- `modelId: "obelisk_v1"` now resolves through `content/models/registry.json` and loads a runtime glTF asset.
- `content/models/obelisk_v1/obelisk_v1.gltf` is the first real asset-backed model with texture.
- Static objects without `modelId` still use cube fallback rendering.
- If model loading fails, `obelisk_v1` still falls back to the built-in obelisk debug mesh.

Client 3D debug controls:

- `Arrow Keys`: move local player (network intent)
- Third-person camera automatically follows and pivots around the local player
- `Right Mouse Drag`: orbit camera
- `Mouse Wheel`: zoom in/out
- `Space`: snap camera behind player movement direction

Editor controls (current foundation):

- Start with `--editor`
- Editor mode is active immediately when started with `--editor`
- `W/A/S/D`: fly editor camera on X/Z plane
- `Q/E`: move editor camera down/up
- `Right Mouse Drag`: look around in editor mode
- `F`: focus camera on selected object
- `Tab`: select next object
- Mesh palette panel shows all registered models on screen
- Click a mesh entry to make it the active placement model
- `[` / `]`: cycle active model palette entry
- `Insert`: place the current palette model on the ground under the camera aim
- `LMB` on gizmo axis: drag selected object on X/Y/Z axis
- `I/J/K/L`: move selected object on X/Z plane
- `U/O`: move selected object on Y axis
- `Z/X`: rotate selected object (yaw)
- `C/V`: decrease/increase selected object scale
- `Shift`: faster move speed while transforming
- `Ctrl+Z`: undo last editor command
- `Ctrl+Y` or `Ctrl+Shift+Z`: redo editor command
- `F5`: save current editor overrides to `content/zones/start_zone.json`

Placeholder level-blockout workflow:

- Generate starter meshes directly in .NET:

	dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models scaffold

- Optional: generate only selected shapes:

	dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models scaffold --shapes ground_tile_v1,wall_v1

- The scaffold currently creates `ground_tile_v1`, `rock_v1`, `wall_v1`, and `pillar_v1` and upserts them into `content/models/registry.json`.
- Use the editor palette (`[` / `]`) to switch between registered models before placing objects.

FBX import pipeline (FBX -> glTF):

- Recommended structure:

	content/models/_imports/<asset_name>/<asset_name>.fbx      (source)
	content/models/<asset_name>/<asset_name>.gltf              (runtime target)

- Add import jobs to `content/models/import-manifest.json`:

	{
	  "imports": [
	    {
	      "id": "pillar_v1",
	      "sourceFbx": "content/models/_imports/pillar_v1/pillar_v1.fbx",
	      "targetDir": "pillar_v1",
	      "outputFile": "pillar_v1.gltf",
	      "scale": 1.0
	    }
	  ]
	}

- Import all jobs and update registry automatically:

	dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models import

- Import one model by ID:

	dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models import --id pillar_v1

- Legacy direct conversion is still available:

	dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models convert content/models/my_mesh/my_mesh.fbx content/models/my_mesh/my_mesh.gltf

- Place the `modelId` on static objects in `content/zones/start_zone.json`.
- Run .NET validation (cross-platform):

	dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- content validate

- Legacy shell validation is still available temporarily:

	./tools/validate-content.sh

## Current Scope

- Bootable MonoGame window and render loop.
- Bootable headless server with session lifecycle and continuous snapshot replication.
- Versioned shared protocol models.
- Shared binary message codec with length-prefixed framing.
- Handshake/login plus input-driven movement intent loop between client and server.
- Model-aware static entity replication (`EntitySnapshot.ModelId`) with client fallback rendering.
- Content-first modding surface through JSON files.

## Next Milestones

1. Introduce validated plugin loading for trusted server gameplay mods.
2. Add ECS-lite gameplay systems and combat pipeline.
3. Add persistence for account and character session state.