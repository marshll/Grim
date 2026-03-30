# Architecture

## Principles

- Server authoritative simulation for movement, combat, and game rules.
- Client prediction can be added later, with reconciliation against server snapshots.
- Content data is externalized under the content directory for modding.
- Shared protocol contracts live in Grim.Shared to prevent drift.

## Layers

- Grim.Core: MonoGame host and render loop.
- Grim.Client: game-specific client composition and scene setup.
- Grim.Engine: reusable primitives such as scene and transform.
- Grim.Server: headless tick loop and network listener.
- Grim.Shared: protocol and snapshot contracts.
- Grim.Tests: protocol and validation tests.

## Modding Baseline

- Tier 1: content mods using JSON files in content.
- Tier 2: trusted server plugins through explicit interfaces.
- Untrusted executable scripts are intentionally out of scope for the first base.