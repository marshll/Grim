namespace Grim.Shared;

public readonly record struct EntityId(Guid Value);

public sealed record HandshakeRequest(string ClientName, int ProtocolVersion);

public sealed record HandshakeResponse(bool Accepted, string Reason, int ServerProtocolVersion);

public sealed record LoginRequest(string AccountName);

public sealed record LoginResponse(bool Accepted, string Reason, Guid SessionId);

public sealed record MovementIntentMessage(float MoveX, float MoveZ, float YawRadians);

public sealed record Vector3Snapshot(float X, float Y, float Z);

public sealed record EntitySnapshot(EntityId Id, Guid OwnerSessionId, Vector3Snapshot Position, float YawRadians, string? ModelId = null);

public sealed record WorldSnapshot(long Tick, IReadOnlyList<EntitySnapshot> Entities);

public sealed record WorldSnapshotMessage(WorldSnapshot Snapshot);