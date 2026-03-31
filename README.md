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
- jq (optional, for content validation script)

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

- Start two clients against one server (use separate terminals):

	dotnet run --project src/Grim.Core/Grim.Core.csproj -- --client alpha --account account_alpha
	dotnet run --project src/Grim.Core/Grim.Core.csproj -- --client beta --account account_beta

The client and server now emit periodic network console logs (handshake/login and snapshot summaries) so you can verify replication in real time.

Client 3D debug controls:

- `W/A/S/D`: pan camera on XZ plane
- `Q/E`: move camera target up/down
- `Right Mouse Drag`: orbit camera
- `Mouse Wheel`: zoom in/out
- `Space`: re-center camera on replicated entities

## Current Scope

- Bootable MonoGame window and render loop.
- Bootable headless server with session lifecycle and continuous snapshot replication.
- Versioned shared protocol models.
- Shared binary message codec with length-prefixed framing.
- Handshake/login plus movement intent loop between client and server.
- Content-first modding surface through JSON files.

## Next Milestones

1. Replace synthetic client movement with real input-driven intent messages.
2. Introduce validated plugin loading for trusted server gameplay mods.
3. Add ECS-lite gameplay systems and combat pipeline.
4. Add persistence for account and character session state.