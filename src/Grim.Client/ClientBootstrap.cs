using Grim.Engine;
using Grim.Shared;

namespace Grim.Client;

public sealed class ClientBootstrap : IGameModule
{
    private readonly ClientRuntimeContext _runtime = new();
    private readonly NetworkBootstrapClient _networkClient = new();
    private bool _networkStarted;

    public ClientRuntimeContext Runtime => _runtime;

    public void Initialize()
    {
        var player = new GameObject("LocalPlayer");
        _runtime.MainScene.Add(player);
        _runtime.LastStatus = $"Initialized with protocol v{ProtocolVersion.Current}";
        _runtime.NetworkStatus = "Bootstrapping network";

        _ = Task.Run(async () =>
        {
            try
            {
                await _networkClient.ConnectAndRunAsync(
                    "127.0.0.1",
                    7777,
                    "dev_account",
                    status => _runtime.NetworkStatus = status,
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
        _runtime.LastStatus = $"Running | Scene Objects: {_runtime.MainScene.Objects.Count} | dt={deltaTime.TotalMilliseconds:F2}ms | Net: {netState}";
    }

    public void Draw()
    {
    }
}
