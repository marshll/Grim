using System.Net;
using System.Net.Sockets;
using Grim.Shared;

namespace Grim.Server;

public sealed class ServerHost
{
    private readonly TcpListener _listener;
    private readonly WorldLoop _world = new();
    private readonly SessionRegistry _sessions = new();

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

        Console.WriteLine($"Handshake accepted for {endpoint}: protocol={handshakeRequest.ProtocolVersion}");

        var loginFrame = await NetworkCodec.ReadMessageAsync(stream, cancellationToken);
        if (loginFrame is null || !string.Equals(loginFrame.MessageType, "login_request", StringComparison.Ordinal))
        {
            Console.WriteLine($"Missing login request from {endpoint}");
            return;
        }

        var loginRequest = NetworkCodec.DeserializePayload<LoginRequest>(loginFrame);
        var session = _sessions.Register(loginRequest.AccountName);
        var loginResponse = new LoginResponse(true, "ok", session.SessionId);

        await NetworkCodec.WriteMessageAsync(
            stream,
            "login_response",
            loginResponse,
            cancellationToken);

        Console.WriteLine($"Client bootstrapped: {endpoint} ({loginRequest.AccountName}) session={session.SessionId}");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = ReceiveClientLoopAsync(session.SessionId, stream, endpoint, linkedCts.Token);
        var replicateTask = ReplicationLoopAsync(session.SessionId, stream, loginRequest.AccountName, linkedCts.Token);

        var completedTask = await Task.WhenAny(receiveTask, replicateTask);
        linkedCts.Cancel();

        try
        {
            await Task.WhenAll(receiveTask, replicateTask);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            _sessions.Remove(session.SessionId);
            Console.WriteLine($"Client disconnected: {endpoint} session={session.SessionId}");
        }
    }

    private async Task ReceiveClientLoopAsync(
        Guid sessionId,
        NetworkStream stream,
        string endpoint,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await NetworkCodec.ReadMessageAsync(stream, cancellationToken);
            if (frame is null)
            {
                return;
            }

            if (string.Equals(frame.MessageType, "movement_intent", StringComparison.Ordinal))
            {
                var intent = NetworkCodec.DeserializePayload<MovementIntentMessage>(frame);
                _sessions.ApplyMovementIntent(sessionId, intent);
                continue;
            }

            Console.WriteLine($"Unknown message from {endpoint}: {frame.MessageType}");
        }
    }

    private async Task ReplicationLoopAsync(
        Guid sessionId,
        NetworkStream stream,
        string accountName,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(100);
        var deltaSeconds = (float)interval.TotalSeconds;
        var snapshotsSent = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            _sessions.AdvanceSession(sessionId, deltaSeconds);

            var snapshot = _sessions.CreateSnapshot(_world.Tick);
            await NetworkCodec.WriteMessageAsync(
                stream,
                "world_snapshot",
                new WorldSnapshotMessage(snapshot),
                cancellationToken);

            snapshotsSent++;
            if (snapshotsSent == 1 || snapshotsSent % 10 == 0)
            {
                Console.WriteLine(
                    $"[SERVER] Snapshot tx: session={sessionId}, account={accountName}, tick={snapshot.Tick}, entities={snapshot.Entities.Count}, count={snapshotsSent}");
            }

            await Task.Delay(interval, cancellationToken);
        }
    }
}