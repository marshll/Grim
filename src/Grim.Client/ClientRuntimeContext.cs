using Grim.Engine;
using Grim.Shared;

namespace Grim.Client;

public sealed class ClientRuntimeContext
{
    private readonly object _sync = new();
    private WorldSnapshot? _latestSnapshot;
    private Guid? _localSessionId;

    public Scene MainScene { get; } = new();
    public string LastStatus { get; set; } = "Client bootstrapped";
    public string NetworkStatus { get; set; } = "Disconnected";

    public void UpdateSnapshot(WorldSnapshot snapshot)
    {
        lock (_sync)
        {
            _latestSnapshot = snapshot;
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
            if (_latestSnapshot is null)
            {
                return new SnapshotView(0, [], _localSessionId);
            }

            return new SnapshotView(_latestSnapshot.Tick, _latestSnapshot.Entities.ToArray(), _localSessionId);
        }
    }
}

public readonly record struct SnapshotView(long Tick, IReadOnlyList<EntitySnapshot> Entities, Guid? LocalSessionId);