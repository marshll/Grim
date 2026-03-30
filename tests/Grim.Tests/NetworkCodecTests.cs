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
}