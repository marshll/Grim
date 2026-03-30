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

## Current Scope

- Bootable MonoGame window and render loop.
- Bootable headless server with tick loop and binary framed handshake/login flow.
- Versioned shared protocol models.
- Shared binary message codec with length-prefixed framing.
- Initial snapshot bootstrap from server to client.
- Content-first modding surface through JSON files.

## Next Milestones

1. Add continuous replication snapshots and movement intent messages.
2. Introduce validated plugin loading for trusted server gameplay mods.
3. Add ECS-lite gameplay systems and combat pipeline.
4. Add persistence for account and character session state.