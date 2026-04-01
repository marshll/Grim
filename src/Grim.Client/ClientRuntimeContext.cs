using Grim.Engine;
using Grim.Shared;

namespace Grim.Client;

public sealed class ClientRuntimeContext
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, RuntimeEntityState> _entityStates = new();
    private Guid? _localSessionId;
    private MovementIntentMessage _movementIntent = new(0f, 0f, 0f);
    private long _latestTick;

    private const float MovementSpeedUnitsPerSecond = 4f;
    private const float SnapshotInterpolationSeconds = 0.10f;
    private const float LocalReconciliationSeconds = 0.20f;
    private const float LocalReconciliationThreshold = 0.25f;

    public Scene MainScene { get; } = new();
    public string LastStatus { get; set; } = "Client bootstrapped";
    public string NetworkStatus { get; set; } = "Disconnected";

    public void UpdateSnapshot(WorldSnapshot snapshot)
    {
        lock (_sync)
        {
            _latestTick = snapshot.Tick;
            var seenEntityIds = new HashSet<Guid>();

            foreach (var entity in snapshot.Entities)
            {
                var id = entity.Id.Value;
                seenEntityIds.Add(id);

                if (!_entityStates.TryGetValue(id, out var state))
                {
                    var start = entity.Position;
                    _entityStates[id] = new RuntimeEntityState(
                        entity.Id,
                        entity.OwnerSessionId,
                        start,
                        start,
                        start,
                        entity.YawRadians,
                        entity.ModelId,
                        entity.ZoneStaticIndex,
                        0f,
                        0f,
                        false);
                    continue;
                }

                var isLocal = _localSessionId.HasValue && entity.OwnerSessionId == _localSessionId.Value;

                state.OwnerSessionId = entity.OwnerSessionId;
                state.AuthoritativePosition = entity.Position;
                state.YawRadians = entity.YawRadians;
                state.ModelId = entity.ModelId;
                state.ZoneStaticIndex = entity.ZoneStaticIndex;

                if (isLocal)
                {
                    var dx = state.RenderPosition.X - entity.Position.X;
                    var dy = state.RenderPosition.Y - entity.Position.Y;
                    var dz = state.RenderPosition.Z - entity.Position.Z;
                    var distance = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

                    if (distance > LocalReconciliationThreshold)
                    {
                        state.ReconciliationElapsed = 0f;
                        state.ReconciliationDuration = LocalReconciliationSeconds;
                        state.HasReconciliation = true;
                    }
                    else
                    {
                        state.RenderPosition = entity.Position;
                        state.HasReconciliation = false;
                    }
                }
                else
                {
                    state.InterpolationStartPosition = state.RenderPosition;
                    state.InterpolationElapsed = 0f;
                    state.InterpolationDuration = SnapshotInterpolationSeconds;
                }
            }

            var staleEntityIds = _entityStates.Keys.Where(id => !seenEntityIds.Contains(id)).ToArray();
            foreach (var staleId in staleEntityIds)
            {
                _entityStates.Remove(staleId);
            }
        }
    }

    public void SetLocalSessionId(Guid sessionId)
    {
        lock (_sync)
        {
            _localSessionId = sessionId;
        }
    }

    public SnapshotView GetSnapshotView()
    {
        lock (_sync)
        {
            if (_entityStates.Count == 0)
            {
                return new SnapshotView(0, [], _localSessionId);
            }

            var entities = _entityStates.Values
                .Select(state => new EntitySnapshot(state.Id, state.OwnerSessionId, state.RenderPosition, state.YawRadians, state.ModelId, state.ZoneStaticIndex))
                .ToArray();

            return new SnapshotView(_latestTick, entities, _localSessionId);
        }
    }

    public void AdvanceFrame(float deltaSeconds)
    {
        lock (_sync)
        {
            foreach (var state in _entityStates.Values)
            {
                var isLocal = _localSessionId.HasValue && state.OwnerSessionId == _localSessionId.Value;
                if (isLocal)
                {
                    state.YawRadians = _movementIntent.YawRadians;

                    state.RenderPosition = new Vector3Snapshot(
                        state.RenderPosition.X + (_movementIntent.MoveX * MovementSpeedUnitsPerSecond * deltaSeconds),
                        state.RenderPosition.Y,
                        state.RenderPosition.Z + (_movementIntent.MoveZ * MovementSpeedUnitsPerSecond * deltaSeconds));

                    if (state.HasReconciliation)
                    {
                        state.ReconciliationElapsed += deltaSeconds;
                        var t = Math.Clamp(state.ReconciliationElapsed / state.ReconciliationDuration, 0f, 1f);
                        state.RenderPosition = Lerp(state.RenderPosition, state.AuthoritativePosition, t);
                        if (t >= 1f)
                        {
                            state.HasReconciliation = false;
                        }
                    }

                    continue;
                }

                state.InterpolationElapsed += deltaSeconds;
                var alpha = state.InterpolationDuration <= 0f
                    ? 1f
                    : Math.Clamp(state.InterpolationElapsed / state.InterpolationDuration, 0f, 1f);
                state.RenderPosition = Lerp(state.InterpolationStartPosition, state.AuthoritativePosition, alpha);
            }
        }
    }

    public void SetMovementIntent(float moveX, float moveZ, float yawRadians)
    {
        lock (_sync)
        {
            _movementIntent = new MovementIntentMessage(moveX, moveZ, yawRadians);
        }
    }

    public MovementIntentMessage GetMovementIntent()
    {
        lock (_sync)
        {
            return _movementIntent;
        }
    }

    private static Vector3Snapshot Lerp(Vector3Snapshot a, Vector3Snapshot b, float t)
    {
        return new Vector3Snapshot(
            a.X + ((b.X - a.X) * t),
            a.Y + ((b.Y - a.Y) * t),
            a.Z + ((b.Z - a.Z) * t));
    }
}

public sealed class RuntimeEntityState
{
    public RuntimeEntityState(
        EntityId id,
        Guid ownerSessionId,
        Vector3Snapshot authoritativePosition,
        Vector3Snapshot renderPosition,
        Vector3Snapshot interpolationStartPosition,
        float yawRadians,
        string? modelId,
        int? zoneStaticIndex,
        float interpolationElapsed,
        float interpolationDuration,
        bool hasReconciliation)
    {
        Id = id;
        OwnerSessionId = ownerSessionId;
        AuthoritativePosition = authoritativePosition;
        RenderPosition = renderPosition;
        InterpolationStartPosition = interpolationStartPosition;
        YawRadians = yawRadians;
        ModelId = modelId;
        ZoneStaticIndex = zoneStaticIndex;
        InterpolationElapsed = interpolationElapsed;
        InterpolationDuration = interpolationDuration;
        HasReconciliation = hasReconciliation;
    }

    public EntityId Id { get; }
    public Guid OwnerSessionId { get; set; }
    public Vector3Snapshot AuthoritativePosition { get; set; }
    public Vector3Snapshot RenderPosition { get; set; }
    public Vector3Snapshot InterpolationStartPosition { get; set; }
    public float YawRadians { get; set; }
    public string? ModelId { get; set; }
    public int? ZoneStaticIndex { get; set; }
    public float InterpolationElapsed { get; set; }
    public float InterpolationDuration { get; set; }
    public float ReconciliationElapsed { get; set; }
    public float ReconciliationDuration { get; set; }
    public bool HasReconciliation { get; set; }
}

public readonly record struct SnapshotView(long Tick, IReadOnlyList<EntitySnapshot> Entities, Guid? LocalSessionId);