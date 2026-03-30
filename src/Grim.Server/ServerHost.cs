using System.Net;
using System.Net.Sockets;
using Grim.Shared;

namespace Grim.Server;

public sealed class ServerHost
{
    private readonly TcpListener _listener;
    private readonly WorldLoop _world = new();

    public ServerHost(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        Console.WriteLine($"Grim server listening on {_listener.LocalEndpoint}");

        var worldTask = _world.RunAsync(cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _listener.Stop();
            await worldTask;
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;

        var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        Console.WriteLine($"Client connected: {endpoint}");

        await using var stream = client.GetStream();

        var handshakeFrame = await NetworkCodec.ReadMessageAsync(stream, cancellationToken);
        if (handshakeFrame is null || !string.Equals(handshakeFrame.MessageType, "handshake_request", StringComparison.Ordinal))
        {
            Console.WriteLine($"Invalid handshake frame from {endpoint}");
            return;
        }

        var handshakeRequest = NetworkCodec.DeserializePayload<HandshakeRequest>(handshakeFrame);
        var accepted = handshakeRequest.ProtocolVersion == ProtocolVersion.Current;
        var handshakeResponse = new HandshakeResponse(
            accepted,
            accepted ? "ok" : "Protocol mismatch",
            ProtocolVersion.Current);

        await NetworkCodec.WriteMessageAsync(
            stream,
            "handshake_response",
            handshakeResponse,
            cancellationToken);

        if (!accepted)
        {
            Console.WriteLine($"Handshake rejected for {endpoint}: protocol {handshakeRequest.ProtocolVersion}");
            return;
        }

        var loginFrame = await NetworkCodec.ReadMessageAsync(stream, cancellationToken);
        if (loginFrame is null || !string.Equals(loginFrame.MessageType, "login_request", StringComparison.Ordinal))
        {
            Console.WriteLine($"Missing login request from {endpoint}");
            return;
        }

        var loginRequest = NetworkCodec.DeserializePayload<LoginRequest>(loginFrame);
        var sessionId = Guid.NewGuid();
        var loginResponse = new LoginResponse(true, "ok", sessionId);

        await NetworkCodec.WriteMessageAsync(
            stream,
            "login_response",
            loginResponse,
            cancellationToken);

        var snapshot = new WorldSnapshot(
            _world.Tick,
            [new EntitySnapshot(new EntityId(Guid.NewGuid()), new Vector3Snapshot(0, 0, 0), 0f)]);

        await NetworkCodec.WriteMessageAsync(
            stream,
            "world_snapshot",
            new WorldSnapshotMessage(snapshot),
            cancellationToken);

        Console.WriteLine($"Client bootstrapped: {endpoint} ({loginRequest.AccountName})");
    }
}