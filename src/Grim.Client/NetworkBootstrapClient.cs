using System.Net.Sockets;
using Grim.Shared;

namespace Grim.Client;

public sealed class NetworkBootstrapClient
{
    public async Task<string> ConnectAndBootstrapAsync(
        string host,
        int port,
        string accountName,
        CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port, cancellationToken);

        using var stream = tcpClient.GetStream();

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

        var snapshotFrame = await NetworkCodec.ReadMessageAsync(stream, cancellationToken)
            ?? throw new InvalidDataException("No world snapshot from server");

        if (!string.Equals(snapshotFrame.MessageType, "world_snapshot", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected frame: {snapshotFrame.MessageType}");
        }

        var snapshotMessage = NetworkCodec.DeserializePayload<WorldSnapshotMessage>(snapshotFrame);
        return $"Connected | Session {loginResponse.SessionId} | Entities: {snapshotMessage.Snapshot.Entities.Count}";
    }
}