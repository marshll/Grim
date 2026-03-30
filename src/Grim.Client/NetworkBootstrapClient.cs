using System.Net.Sockets;
using Grim.Shared;

namespace Grim.Client;

public sealed class NetworkBootstrapClient
{
    public async Task ConnectAndRunAsync(
        string host,
        int port,
        string accountName,
        Action<string> onStatus,
        CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
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

        onStatus($"Connected | Session {loginResponse.SessionId}");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = ReceiveSnapshotsAsync(stream, onStatus, linkedCts.Token);
        var sendTask = SendMovementIntentsAsync(stream, linkedCts.Token);

        await Task.WhenAny(receiveTask, sendTask);
        linkedCts.Cancel();

        try
        {
            await Task.WhenAll(receiveTask, sendTask);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task ReceiveSnapshotsAsync(
        NetworkStream stream,
        Action<string> onStatus,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await NetworkCodec.ReadMessageAsync(stream, cancellationToken)
                ?? throw new IOException("Server closed connection");

            if (!string.Equals(frame.MessageType, "world_snapshot", StringComparison.Ordinal))
            {
                continue;
            }

            var snapshotMessage = NetworkCodec.DeserializePayload<WorldSnapshotMessage>(frame);
            onStatus($"Replicating | Tick {snapshotMessage.Snapshot.Tick} | Entities: {snapshotMessage.Snapshot.Entities.Count}");
        }
    }

    private static async Task SendMovementIntentsAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(100);
        var phase = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var direction = (phase / 40) % 2 == 0 ? 1f : -1f;
            var intent = new MovementIntentMessage(direction, 0f, 0f);

            await NetworkCodec.WriteMessageAsync(
                stream,
                "movement_intent",
                intent,
                cancellationToken);

            phase++;
            await Task.Delay(interval, cancellationToken);
        }
    }
}
