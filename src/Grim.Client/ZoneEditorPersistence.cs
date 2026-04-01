using System.Text.Json;

namespace Grim.Client;

public sealed class ZoneEditorPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string ZonePath { get; }

    public ZoneEditorPersistence(string? zonePath = null)
    {
        ZonePath = zonePath ?? ResolveDefaultZonePath();
    }

    public SaveZoneResult SaveStaticObjectOverrides(IReadOnlyList<ZoneStaticObjectOverride> overrides)
    {
        if (!File.Exists(ZonePath))
        {
            return SaveZoneResult.Failed($"Zone file not found: {ZonePath}");
        }

        ZoneDefinition? zone;
        try
        {
            var json = File.ReadAllText(ZonePath);
            zone = JsonSerializer.Deserialize<ZoneDefinition>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            return SaveZoneResult.Failed($"Failed to read zone file: {ex.Message}");
        }

        if (zone is null)
        {
            return SaveZoneResult.Failed("Zone file could not be deserialized.");
        }

        if (zone.StaticObjects is null)
        {
            zone = zone with { StaticObjects = [] };
        }

        var applied = 0;
        foreach (var item in overrides)
        {
            if (item.ZoneStaticIndex < 0 || item.ZoneStaticIndex >= zone.StaticObjects.Count)
            {
                continue;
            }

            var current = zone.StaticObjects[item.ZoneStaticIndex];
            zone.StaticObjects[item.ZoneStaticIndex] = current with
            {
                X = item.Position.X,
                Y = item.Position.Y,
                Z = item.Position.Z,
                YawRadians = item.YawRadians
            };
            applied++;
        }

        if (applied == 0)
        {
            return SaveZoneResult.Failed("No matching static objects to save.");
        }

        var tempPath = $"{ZonePath}.tmp";
        try
        {
            var output = JsonSerializer.Serialize(zone, JsonOptions);
            File.WriteAllText(tempPath, output);
            File.Move(tempPath, ZonePath, true);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }

            return SaveZoneResult.Failed($"Failed to write zone file: {ex.Message}");
        }

        return SaveZoneResult.Ok(applied, ZonePath);
    }

    private static string ResolveDefaultZonePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var readme = Path.Combine(current.FullName, "README.md");
            var zonePath = Path.Combine(current.FullName, "content", "zones", "start_zone.json");
            if (File.Exists(readme) && File.Exists(zonePath))
            {
                return zonePath;
            }

            current = current.Parent;
        }

        return Path.Combine(Environment.CurrentDirectory, "content", "zones", "start_zone.json");
    }
}

public readonly record struct ZoneStaticObjectOverride(int ZoneStaticIndex, ZonePosition Position, float YawRadians);

public readonly record struct ZonePosition(float X, float Y, float Z);

public sealed record SaveZoneResult(bool Success, string Message, int AppliedCount, string ZonePath)
{
    public static SaveZoneResult Ok(int appliedCount, string zonePath)
    {
        return new SaveZoneResult(true, $"Saved {appliedCount} static object changes.", appliedCount, zonePath);
    }

    public static SaveZoneResult Failed(string message)
    {
        return new SaveZoneResult(false, message, 0, string.Empty);
    }
}

public sealed record ZoneDefinition(
    string Id,
    string Name,
    int RecommendedLevelMin,
    int RecommendedLevelMax,
    List<ZoneSpawnPoint> SpawnPoints,
    List<ZoneStaticObject> StaticObjects);

public sealed record ZoneSpawnPoint(float X, float Y, float Z);

public sealed record ZoneStaticObject(float X, float Y, float Z, float YawRadians, string? ModelId = null);
