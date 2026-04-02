using Grim.Client;
using Xunit;

namespace Grim.Tests;

public sealed class ZoneEditorPersistenceTests
{
    [Fact]
    public void SaveStaticObjectOverrides_UpdatesMatchingIndices()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "grim-editor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var zonePath = Path.Combine(tempDir, "start_zone.json");
        File.WriteAllText(
            zonePath,
            """
            {
              "id": "start_zone",
              "name": "Valley",
              "recommendedLevelMin": 1,
              "recommendedLevelMax": 5,
              "spawnPoints": [
                { "x": 0, "y": 0, "z": 0 }
              ],
              "staticObjects": [
                { "x": 1, "y": 0, "z": 1, "yawRadians": 0.1, "modelId": "obelisk_v1" },
                { "x": 2, "y": 0, "z": 2, "yawRadians": 0.2, "modelId": "obelisk_v1" }
              ]
            }
            """);

        var persistence = new ZoneEditorPersistence(zonePath);
        var result = persistence.SaveStaticObjectOverrides(
            [
                new ZoneStaticObjectOverride(1, new ZonePosition(7f, 1f, -3f), 1.57f)
            ]);

        Assert.True(result.Success);
        Assert.Equal(1, result.AppliedCount);

        var output = File.ReadAllText(zonePath);
        Assert.Contains("\"x\": 7", output, StringComparison.Ordinal);
        Assert.Contains("\"y\": 1", output, StringComparison.Ordinal);
        Assert.Contains("\"z\": -3", output, StringComparison.Ordinal);
        Assert.Contains("\"scale\": 1", output, StringComparison.Ordinal);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void SaveStaticObjectOverrides_UpdatesScaleAndModelWhenRequested()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "grim-editor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var zonePath = Path.Combine(tempDir, "start_zone.json");
        File.WriteAllText(
            zonePath,
            """
            {
              "id": "start_zone",
              "name": "Valley",
              "recommendedLevelMin": 1,
              "recommendedLevelMax": 5,
              "spawnPoints": [
                { "x": 0, "y": 0, "z": 0 }
              ],
              "staticObjects": [
                { "x": 1, "y": 0, "z": 1, "yawRadians": 0.1, "modelId": "obelisk_v1" }
              ]
            }
            """);

        var persistence = new ZoneEditorPersistence(zonePath);
        var result = persistence.SaveStaticObjectOverrides(
            [
                new ZoneStaticObjectOverride(
                    0,
                    new ZonePosition(1f, 0f, 1f),
                    0.1f,
                    2.5f,
                    "pillar_v2",
                    true)
            ]);

        Assert.True(result.Success);
        Assert.Equal(1, result.AppliedCount);

        var output = File.ReadAllText(zonePath);
        Assert.Contains("\"modelId\": \"pillar_v2\"", output, StringComparison.Ordinal);
        Assert.Contains("\"scale\": 2.5", output, StringComparison.Ordinal);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void SaveStaticObjectEdits_CreatesAndDeletesStaticObjects()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "grim-editor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var zonePath = Path.Combine(tempDir, "start_zone.json");
        File.WriteAllText(
            zonePath,
            """
            {
              "id": "start_zone",
              "name": "Valley",
              "recommendedLevelMin": 1,
              "recommendedLevelMax": 5,
              "spawnPoints": [
                { "x": 0, "y": 0, "z": 0 }
              ],
              "staticObjects": [
                { "x": 1, "y": 0, "z": 1, "yawRadians": 0.1, "modelId": "obelisk_v1" },
                { "x": 2, "y": 0, "z": 2, "yawRadians": 0.2, "modelId": null }
              ]
            }
            """);

        var persistence = new ZoneEditorPersistence(zonePath);
        var result = persistence.SaveStaticObjectEdits(
            [],
            [
                new ZoneStaticObjectDraft(new ZonePosition(9f, 0f, -4f), 0.75f, 1.8f, "pillar_v2")
            ],
            [0]);

        Assert.True(result.Success);
        Assert.Equal(2, result.AppliedCount);

        var output = File.ReadAllText(zonePath);
        Assert.DoesNotContain("\"x\": 1", output, StringComparison.Ordinal);
        Assert.Contains("\"x\": 9", output, StringComparison.Ordinal);
        Assert.Contains("\"modelId\": \"pillar_v2\"", output, StringComparison.Ordinal);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void SaveStaticObjectOverrides_ReturnsFailureForUnknownIndex()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "grim-editor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var zonePath = Path.Combine(tempDir, "start_zone.json");
        File.WriteAllText(
            zonePath,
            """
            {
              "id": "start_zone",
              "name": "Valley",
              "recommendedLevelMin": 1,
              "recommendedLevelMax": 5,
              "spawnPoints": [
                { "x": 0, "y": 0, "z": 0 }
              ],
              "staticObjects": [
                { "x": 1, "y": 0, "z": 1, "yawRadians": 0.1, "modelId": "obelisk_v1" }
              ]
            }
            """);

        var persistence = new ZoneEditorPersistence(zonePath);
        var result = persistence.SaveStaticObjectOverrides(
            [
                new ZoneStaticObjectOverride(99, new ZonePosition(7f, 1f, -3f), 1.57f)
            ]);

        Assert.False(result.Success);
        Assert.Equal(0, result.AppliedCount);

        Directory.Delete(tempDir, true);
    }
}
