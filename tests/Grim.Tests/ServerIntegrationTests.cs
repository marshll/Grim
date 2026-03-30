using System.Net;
using System.Net.Sockets;
using Grim.Server;
using Grim.Shared;
using Xunit;

namespace Grim.Tests;

public sealed class ServerIntegrationTests
{
    [Fact]
    public async Task Server_BootstrapAndSnapshotFlow_Works()
    {
        var port = GetAvailablePort();
        var host = new ServerHost(port);

        using var cts = new CancellationTokenSource();
        var serverTask = host.RunAsync(cts.Token);

        await Task.Delay(100);

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = tcpClient.GetStream();

        await NetworkCodec.WriteMessageAsync(
            stream,
            "handshake_request",
            new HandshakeRequest("integration-test", ProtocolVersion.Current),
            CancellationToken.None);

        var handshakeFrame = await NetworkCodec.ReadMessageAsync(stream, CancellationToken.None);
        Assert.NotNull(handshakeFrame);
        Assert.Equal("handshake_response", handshakeFrame!.MessageType);

        var handshake = NetworkCodec.DeserializePayload<HandshakeResponse>(handshakeFrame);
        Assert.True(handshake.Accepted);

        await NetworkCodec.WriteMessageAsync(
            stream,
            "login_request",
            new LoginRequest("integration_account"),
            CancellationToken.None);

        var loginFrame = await NetworkCodec.ReadMessageAsync(stream, CancellationToken.None);
        Assert.NotNull(loginFrame);
        Assert.Equal("login_response", loginFrame!.MessageType);

        var login = NetworkCodec.DeserializePayload<LoginResponse>(loginFrame);
        Assert.True(login.Accepted);
        Assert.NotEqual(Guid.Empty, login.SessionId);

        await NetworkCodec.WriteMessageAsync(
            stream,
            "movement_intent",
            new MovementIntentMessage(1f, 0f, 0f),
            CancellationToken.None);

        var snapshotFrame = await NetworkCodec.ReadMessageAsync(stream, CancellationToken.None);
        Assert.NotNull(snapshotFrame);
        Assert.Equal("world_snapshot", snapshotFrame!.MessageType);

        var snapshot = NetworkCodec.DeserializePayload<WorldSnapshotMessage>(snapshotFrame);
        Assert.NotEmpty(snapshot.Snapshot.Entities);

        cts.Cancel();

        try
        {
            await serverTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
