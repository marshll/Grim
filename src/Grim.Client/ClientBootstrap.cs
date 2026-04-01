using Grim.Engine;
using Grim.Shared;

namespace Grim.Client;

public sealed class ClientBootstrap : IGameModule
{
    private readonly ClientRuntimeContext _runtime = new();
    private readonly NetworkBootstrapClient _networkClient = new();
    private readonly string _host;
    private readonly int _port;
    private readonly string _accountName;
    private readonly string _clientTag;
    private bool _networkStarted;

    public ClientBootstrap(string host, int port, string accountName, string clientTag)
    {
        _host = host;
        _port = port;
        _accountName = accountName;
        _clientTag = string.IsNullOrWhiteSpace(clientTag) ? "client" : clientTag;
    }

    public ClientRuntimeContext Runtime => _runtime;

    public void Initialize()
    {
        var player = new GameObject("LocalPlayer");
        _runtime.MainScene.Add(player);
        _runtime.LastStatus = $"Initialized with protocol v{ProtocolVersion.Current} | {_clientTag}";
        _runtime.NetworkStatus = $"Bootstrapping network to {_host}:{_port} as {_accountName}";

        _ = Task.Run(async () =>
        {
            try
            {
                await _networkClient.ConnectAndRunAsync(
                    _host,
                    _port,
                    _accountName,
                    _clientTag,
                    status => _runtime.NetworkStatus = status,
                    sessionId => _runtime.SetLocalSessionId(sessionId),
                    snapshot => _runtime.UpdateSnapshot(snapshot),
                    () => _runtime.GetMovementIntent(),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _runtime.NetworkStatus = $"Network bootstrap failed: {ex.Message}";
            }
        });

        _networkStarted = true;
    }

    public void Update(TimeSpan deltaTime)
    {
        var netState = _networkStarted ? _runtime.NetworkStatus : "Not started";
        var snapshotView = _runtime.GetSnapshotView();
        _runtime.LastStatus =
            $"Running | Scene Objects: {_runtime.MainScene.Objects.Count} | Tick: {snapshotView.Tick} | Entities: {snapshotView.Entities.Count} | dt={deltaTime.TotalMilliseconds:F2}ms | Net: {netState}";
    }

    public void Draw()
    {
    }
}
