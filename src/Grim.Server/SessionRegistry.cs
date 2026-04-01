using System.Collections.Concurrent;
using Grim.Shared;

namespace Grim.Server;

public sealed class SessionRegistry
{
    private readonly ConcurrentDictionary<Guid, SessionState> _sessions = new();
    private readonly ConcurrentDictionary<EntityId, StaticEntityState> _staticEntities = new();
    private const float MovementSpeedUnitsPerSecond = 4f;

    public SessionState Register(string accountName, Vector3Snapshot? spawnPosition = null)
    {
        var sessionId = Guid.NewGuid();
        var entityId = new EntityId(Guid.NewGuid());
        var state = new SessionState(
            sessionId,
            entityId,
            accountName,
            spawnPosition ?? new Vector3Snapshot(0, 0, 0),
            new MovementIntentMessage(0, 0, 0));

        _sessions[sessionId] = state;
        return state;
    }

    public void RegisterStaticEntity(Vector3Snapshot position, float yawRadians = 0f)
    {
        var entityId = new EntityId(Guid.NewGuid());
        _staticEntities[entityId] = new StaticEntityState(entityId, position, yawRadians);
    }

    public void Remove(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }

    public bool ApplyMovementIntent(Guid sessionId, MovementIntentMessage intent)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            return false;
        }

        lock (state.Sync)
        {
            state.Intent = intent;
        }

        return true;
    }

    public void AdvanceSession(Guid sessionId, float deltaSeconds)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            return;
        }

        lock (state.Sync)
        {
            var dx = state.Intent.MoveX * MovementSpeedUnitsPerSecond * deltaSeconds;
            var dz = state.Intent.MoveZ * MovementSpeedUnitsPerSecond * deltaSeconds;

            state.Position = new Vector3Snapshot(
                state.Position.X + dx,
                state.Position.Y,
                state.Position.Z + dz);

            state.YawRadians = state.Intent.YawRadians;
        }
    }

    public WorldSnapshot CreateSnapshot(long tick)
    {
        var entities = new List<EntitySnapshot>(_sessions.Count + _staticEntities.Count);

        foreach (var state in _sessions.Values)
        {
            lock (state.Sync)
            {
                entities.Add(new EntitySnapshot(state.EntityId, state.SessionId, state.Position, state.YawRadians));
            }
        }

        foreach (var staticEntity in _staticEntities.Values)
        {
            entities.Add(new EntitySnapshot(staticEntity.EntityId, Guid.Empty, staticEntity.Position, staticEntity.YawRadians));
        }

        return new WorldSnapshot(tick, entities);
    }
}

public sealed record StaticEntityState(EntityId EntityId, Vector3Snapshot Position, float YawRadians);

public sealed class SessionState
{
    public SessionState(
        Guid sessionId,
        EntityId entityId,
        string accountName,
        Vector3Snapshot position,
        MovementIntentMessage intent)
    {
        SessionId = sessionId;
        EntityId = entityId;
        AccountName = accountName;
        Position = position;
        Intent = intent;
    }

    public object Sync { get; } = new();
    public Guid SessionId { get; }
    public EntityId EntityId { get; }
    public string AccountName { get; }
    public Vector3Snapshot Position { get; set; }
    public MovementIntentMessage Intent { get; set; }
    public float YawRadians { get; set; }
}