using System.Net.Sockets;
using Grim.Shared;

namespace Grim.Client;

public sealed class NetworkBootstrapClient
{
    public async Task ConnectAndRunAsync(
        string host,
        int port,
        string accountName,
        string clientTag,
        Action<string> onStatus,
        Action<Guid> onSessionStarted,
        Action<WorldSnapshot> onSnapshot,
        Func<MovementIntentMessage> getMovementIntent,
        CancellationToken cancellationToken)
    {
        var tag = BuildTag(clientTag, accountName);
        using var tcpClient = new TcpClient();
        Console.WriteLine($"{tag} Connecting to {host}:{port}");
        await tcpClient.ConnectAsync(host, port, cancellationToken);

        await using var stream = tcpClient.GetStream();

        await NetworkCodec.WriteMessageAsync(
            stream,
            "handshake_request",
            new HandshakeRequest("GrimClient", ProtocolVersion.Current),
            cancellationToken);

        var handshakeFrame = await NetworkCodec.ReadMessageAsync(stream, cancellationToken)
            ?? throw new InvalidDataException("No handshake response from server");

        if (!string.Equals(handshakeFrame.MessageType, "handshake_response", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected frame: {handshakeFrame.MessageType}");
        }

        var handshake = NetworkCodec.DeserializePayload<HandshakeResponse>(handshakeFrame);
        if (!handshake.Accepted)
        {
            throw new InvalidOperationException($"Handshake rejected: {handshake.Reason}");
        }

        Console.WriteLine($"{tag} Handshake accepted (protocol v{handshake.ServerProtocolVersion})");

        await NetworkCodec.WriteMessageAsync(
            stream,
            "login_request",
            new LoginRequest(accountName),
            cancellationToken);

        var loginFrame = await NetworkCodec.ReadMessageAsync(stream, cancellationToken)
            ?? throw new InvalidDataException("No login response from server");

        if (!string.Equals(loginFrame.MessageType, "login_response", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected frame: {loginFrame.MessageType}");
        }

        var loginResponse = NetworkCodec.DeserializePayload<LoginResponse>(loginFrame);
        if (!loginResponse.Accepted)
        {
            throw new InvalidOperationException($"Login rejected: {loginResponse.Reason}");
        }

        Console.WriteLine($"{tag} Login accepted; session={loginResponse.SessionId}");
        onSessionStarted(loginResponse.SessionId);
        onStatus($"Connected | Session {loginResponse.SessionId} | {clientTag}/{accountName}");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = ReceiveSnapshotsAsync(stream, onStatus, onSnapshot, tag, clientTag, accountName, linkedCts.Token);
        var sendTask = SendMovementIntentsAsync(stream, getMovementIntent, linkedCts.Token);

        await Task.WhenAny(receiveTask, sendTask);
        linkedCts.Cancel();

        try
        {
            await Task.WhenAll(receiveTask, sendTask);
        }
        catch (OperationCanceledException)
        {
        }

        Console.WriteLine($"{tag} Connection loop ended");
    }

    private static async Task ReceiveSnapshotsAsync(
        NetworkStream stream,
        Action<string> onStatus,
        Action<WorldSnapshot> onSnapshot,
        string tag,
        string clientTag,
        string accountName,
        CancellationToken cancellationToken)
    {
        var snapshotsReceived = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await NetworkCodec.ReadMessageAsync(stream, cancellationToken)
                ?? throw new IOException("Server closed connection");

            if (!string.Equals(frame.MessageType, "world_snapshot", StringComparison.Ordinal))
            {
                continue;
            }

            var snapshotMessage = NetworkCodec.DeserializePayload<WorldSnapshotMessage>(frame);
            onSnapshot(snapshotMessage.Snapshot);
            snapshotsReceived++;
            if (snapshotsReceived == 1 || snapshotsReceived % 10 == 0)
            {
                Console.WriteLine(
                    $"{tag} Snapshot rx: tick={snapshotMessage.Snapshot.Tick}, entities={snapshotMessage.Snapshot.Entities.Count}, count={snapshotsReceived}");
            }

            onStatus(
                $"Replicating | Tick {snapshotMessage.Snapshot.Tick} | Entities: {snapshotMessage.Snapshot.Entities.Count} | {clientTag}/{accountName}");
        }
    }

    private static async Task SendMovementIntentsAsync(
        NetworkStream stream,
        Func<MovementIntentMessage> getMovementIntent,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(100);

        while (!cancellationToken.IsCancellationRequested)
        {
            var intent = getMovementIntent();

            await NetworkCodec.WriteMessageAsync(
                stream,
                "movement_intent",
                intent,
                cancellationToken);

            await Task.Delay(interval, cancellationToken);
        }
    }

    private static string BuildTag(string clientTag, string accountName)
    {
        var normalizedClientTag = string.IsNullOrWhiteSpace(clientTag) ? "client" : clientTag;
        var normalizedAccount = string.IsNullOrWhiteSpace(accountName) ? "unknown" : accountName;
        return $"[CLIENT {normalizedClientTag}:{normalizedAccount}]";
    }
}
