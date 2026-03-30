using Grim.Shared;
using Xunit;

namespace Grim.Tests;

public sealed class ProtocolVersionTests
{
    [Fact]
    public void CurrentProtocolVersion_IsPositive()
    {
        Assert.True(ProtocolVersion.Current > 0);
    }

    [Fact]
    public void HandshakeResponse_ContainsExpectedVersion()
    {
        var response = new HandshakeResponse(true, "ok", ProtocolVersion.Current);
        Assert.Equal(ProtocolVersion.Current, response.ServerProtocolVersion);
    }
}