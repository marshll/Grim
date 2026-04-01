using Grim.Client;
using Grim.Shared;
using Xunit;

namespace Grim.Tests;

public sealed class ClientRuntimeContextTests
{
    [Fact]
    public void AdvanceFrame_RemoteEntity_InterpolatesBetweenSnapshots()
    {
        var context = new ClientRuntimeContext();
        var owner = Guid.NewGuid();
        var entityId = new EntityId(Guid.NewGuid());

        context.UpdateSnapshot(new WorldSnapshot(
            1,
            [new EntitySnapshot(entityId, owner, new Vector3Snapshot(0f, 0f, 0f), 0f)]));

        context.UpdateSnapshot(new WorldSnapshot(
            2,
            [new EntitySnapshot(entityId, owner, new Vector3Snapshot(10f, 0f, 0f), 0f)]));

        context.AdvanceFrame(0.05f);
        var mid = context.GetSnapshotView();
        var midEntity = Assert.Single(mid.Entities);

        Assert.InRange(midEntity.Position.X, 4.5f, 5.5f);

        context.AdvanceFrame(0.05f);
        var end = context.GetSnapshotView();
        var endEntity = Assert.Single(end.Entities);

        Assert.InRange(endEntity.Position.X, 9.9f, 10.1f);
    }

    [Fact]
    public void AdvanceFrame_LocalEntity_PredictsThenReconcilesToServerTruth()
    {
        var context = new ClientRuntimeContext();
        var localSessionId = Guid.NewGuid();
        var entityId = new EntityId(Guid.NewGuid());

        context.SetLocalSessionId(localSessionId);
        context.UpdateSnapshot(new WorldSnapshot(
            1,
            [new EntitySnapshot(entityId, localSessionId, new Vector3Snapshot(0f, 0f, 0f), 0f)]));

        context.SetMovementIntent(1f, 0f, 0f);
        context.AdvanceFrame(0.10f);

        var predicted = context.GetSnapshotView();
        var predictedEntity = Assert.Single(predicted.Entities);
        Assert.True(predictedEntity.Position.X > 0.30f);

        context.UpdateSnapshot(new WorldSnapshot(
            2,
            [new EntitySnapshot(entityId, localSessionId, new Vector3Snapshot(0f, 0f, 0f), 0f)]));

        context.AdvanceFrame(0.10f);
        context.AdvanceFrame(0.10f);

        var reconciled = context.GetSnapshotView();
        var reconciledEntity = Assert.Single(reconciled.Entities);

        Assert.InRange(reconciledEntity.Position.X, -0.01f, 0.01f);
    }
}
