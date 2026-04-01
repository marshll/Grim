using Grim.Server;
using Grim.Shared;
using Xunit;

namespace Grim.Tests;

public sealed class SessionRegistryTests
{
    [Fact]
    public void CreateSnapshot_IncludesStaticEntityModelId()
    {
        var registry = new SessionRegistry();
        registry.RegisterStaticEntity(new Vector3Snapshot(6f, 0f, -6f), 0.2f, "obelisk_v1");

        var snapshot = registry.CreateSnapshot(1);

        Assert.Contains(
            snapshot.Entities,
            entity => entity.OwnerSessionId == Guid.Empty && string.Equals(entity.ModelId, "obelisk_v1", StringComparison.Ordinal));
    }
}
