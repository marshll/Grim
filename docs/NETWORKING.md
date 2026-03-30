# Networking

## Initial Baseline

- Transport: TCP for initial implementation simplicity.
- Default port: 7777.
- Protocol model: versioned message contracts from Grim.Shared.
- Current framing: length-prefixed binary frames carrying JSON payloads.
- Current bootstrap: handshake request/response, login request/response, one world snapshot.

## Authority Model

- Server is the source of truth.
- Client sends intent messages.
- Server sends accepted state snapshots.

## Next Steps

- Add continuous world snapshot replication and client movement intents.
- Add persistent login/session tokens.
- Add snapshot compression and interest management.