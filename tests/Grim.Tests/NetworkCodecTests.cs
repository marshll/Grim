using Grim.Shared;
using Xunit;

namespace Grim.Tests;

public sealed class NetworkCodecTests
{
    [Fact]
    public async Task WriteThenReadMessage_RoundTripsPayload()
    {
        await using var stream = new MemoryStream();

        var expected = new LoginRequest("tester");
        await NetworkCodec.WriteMessageAsync(stream, "login_request", expected, CancellationToken.None);

        stream.Position = 0;
        var frame = await NetworkCodec.ReadMessageAsync(stream, CancellationToken.None);

        Assert.NotNull(frame);
        Assert.Equal("login_request", frame!.MessageType);

        var payload = NetworkCodec.DeserializePayload<LoginRequest>(frame);
        Assert.Equal(expected.AccountName, payload.AccountName);
    }

    [Fact]
    public async Task MovementIntent_RoundTripsPayload()
    {
        await using var stream = new MemoryStream();

        var expected = new MovementIntentMessage(1f, -1f, 0.5f);
        await NetworkCodec.WriteMessageAsync(stream, "movement_intent", expected, CancellationToken.None);

        stream.Position = 0;
        var frame = await NetworkCodec.ReadMessageAsync(stream, CancellationToken.None);

        Assert.NotNull(frame);
        Assert.Equal("movement_intent", frame!.MessageType);

        var payload = NetworkCodec.DeserializePayload<MovementIntentMessage>(frame);
        Assert.Equal(expected.MoveX, payload.MoveX);
        Assert.Equal(expected.MoveZ, payload.MoveZ);
        Assert.Equal(expected.YawRadians, payload.YawRadians);
    }
}
