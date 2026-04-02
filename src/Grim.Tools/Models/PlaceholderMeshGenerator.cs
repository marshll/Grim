using System.Text.Json;

namespace Grim.Tools.Models;

public sealed class PlaceholderMeshGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly PlaceholderShapeDefinition[] Definitions =
    [
        new PlaceholderShapeDefinition("ground_tile_v1", () => CreatePlaneMesh(4f, 4f)),
        new PlaceholderShapeDefinition("rock_v1", () => CreateBoxMesh(1.8f, 1.2f, 1.5f)),
        new PlaceholderShapeDefinition("wall_v1", () => CreateBoxMesh(4f, 3f, 0.5f)),
        new PlaceholderShapeDefinition("pillar_v1", () => CreateBoxMesh(0.8f, 5f, 0.8f))
    ];

    public ScaffoldResult Generate(string repoRoot, string? shapesFilter = null)
    {
        var registryPath = Path.Combine(repoRoot, "content", "models", "registry.json");
        if (!File.Exists(registryPath))
        {
            return ScaffoldResult.Failed($"Registry file not found: {registryPath}");
        }

        RegistryManifest registry;
        try
        {
            registry = JsonSerializer.Deserialize<RegistryManifest>(File.ReadAllText(registryPath), JsonOptions) ?? new RegistryManifest([]);
        }
        catch (Exception ex)
        {
            return ScaffoldResult.Failed($"Failed to parse registry: {ex.Message}");
        }

        var selectedDefinitions = SelectDefinitions(shapesFilter);
        if (selectedDefinitions.Count == 0)
        {
            return ScaffoldResult.Failed("No placeholder shapes selected. Use --shapes all or a comma-separated list of ids.");
        }

        var models = registry.Models.ToList();
        foreach (var definition in selectedDefinitions)
        {
            var mesh = definition.Build();
            var outputDirectory = Path.Combine(repoRoot, "content", "models", definition.Id);
            Directory.CreateDirectory(outputDirectory);

            WriteMeshFiles(outputDirectory, definition.Id, mesh);

            var gltfPath = $"{definition.Id}/{definition.Id}.gltf";
            models.RemoveAll(item => string.Equals(item.Id, definition.Id, StringComparison.Ordinal));
            models.Add(new RegistryEntry(definition.Id, gltfPath, 1f));
        }

        var updatedRegistry = new RegistryManifest(models.OrderBy(item => item.Id, StringComparer.Ordinal).ToList());
        var tempPath = $"{registryPath}.tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(updatedRegistry, JsonOptions));
            File.Move(tempPath, registryPath, true);
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
                // Best effort cleanup.
            }

            return ScaffoldResult.Failed($"Failed to update registry: {ex.Message}");
        }

        var generatedIds = string.Join(", ", selectedDefinitions.Select(item => item.Id));
        return ScaffoldResult.Ok($"Scaffold completed ({selectedDefinitions.Count} item(s)): {generatedIds}", selectedDefinitions.Count);
    }

    private static IReadOnlyList<PlaceholderShapeDefinition> SelectDefinitions(string? shapesFilter)
    {
        if (string.IsNullOrWhiteSpace(shapesFilter) || string.Equals(shapesFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            return Definitions;
        }

        var selectedIds = shapesFilter
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Definitions
            .Where(item => selectedIds.Contains(item.Id))
            .ToArray();
    }

    private static void WriteMeshFiles(string outputDirectory, string modelId, PlaceholderMesh mesh)
    {
        var binPath = Path.Combine(outputDirectory, $"{modelId}.bin");
        var gltfPath = Path.Combine(outputDirectory, $"{modelId}.gltf");

        var positionsByteLength = mesh.Positions.Length * 12;
        var texCoordsByteLength = mesh.TexCoords.Length * 8;
        var indicesByteLength = mesh.Indices.Length * 2;
        var totalByteLength = positionsByteLength + texCoordsByteLength + indicesByteLength;

        using (var stream = File.Create(binPath))
        using (var writer = new BinaryWriter(stream))
        {
            foreach (var position in mesh.Positions)
            {
                writer.Write(position.X);
                writer.Write(position.Y);
                writer.Write(position.Z);
            }

            foreach (var texCoord in mesh.TexCoords)
            {
                writer.Write(texCoord.X);
                writer.Write(texCoord.Y);
            }

            foreach (var index in mesh.Indices)
            {
                writer.Write(index);
            }
        }

        var minX = mesh.Positions.Min(item => item.X);
        var minY = mesh.Positions.Min(item => item.Y);
        var minZ = mesh.Positions.Min(item => item.Z);
        var maxX = mesh.Positions.Max(item => item.X);
        var maxY = mesh.Positions.Max(item => item.Y);
        var maxZ = mesh.Positions.Max(item => item.Z);

        var document = new
        {
            asset = new
            {
                version = "2.0",
                generator = "Grim.Tools models scaffold"
            },
            scene = 0,
            scenes = new[]
            {
                new
                {
                    nodes = new[] { 0 }
                }
            },
            nodes = new[]
            {
                new
                {
                    mesh = 0
                }
            },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new
                            {
                                POSITION = 0,
                                TEXCOORD_0 = 1
                            },
                            indices = 2
                        }
                    }
                }
            },
            buffers = new[]
            {
                new
                {
                    uri = $"{modelId}.bin",
                    byteLength = totalByteLength
                }
            },
            bufferViews = new object[]
            {
                new
                {
                    buffer = 0,
                    byteOffset = 0,
                    byteLength = positionsByteLength,
                    target = 34962
                },
                new
                {
                    buffer = 0,
                    byteOffset = positionsByteLength,
                    byteLength = texCoordsByteLength,
                    target = 34962
                },
                new
                {
                    buffer = 0,
                    byteOffset = positionsByteLength + texCoordsByteLength,
                    byteLength = indicesByteLength,
                    target = 34963
                }
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    componentType = 5126,
                    count = mesh.Positions.Length,
                    type = "VEC3",
                    min = new[] { minX, minY, minZ },
                    max = new[] { maxX, maxY, maxZ }
                },
                new
                {
                    bufferView = 1,
                    componentType = 5126,
                    count = mesh.TexCoords.Length,
                    type = "VEC2"
                },
                new
                {
                    bufferView = 2,
                    componentType = 5123,
                    count = mesh.Indices.Length,
                    type = "SCALAR"
                }
            }
        };

        File.WriteAllText(gltfPath, JsonSerializer.Serialize(document, JsonOptions));
    }

    private static PlaceholderMesh CreatePlaneMesh(float width, float depth)
    {
        var halfWidth = width * 0.5f;
        var halfDepth = depth * 0.5f;

        return new PlaceholderMesh(
            [
                new Float3(-halfWidth, 0f, -halfDepth),
                new Float3(halfWidth, 0f, -halfDepth),
                new Float3(halfWidth, 0f, halfDepth),
                new Float3(-halfWidth, 0f, halfDepth)
            ],
            [
                new Float2(0f, 0f),
                new Float2(1f, 0f),
                new Float2(1f, 1f),
                new Float2(0f, 1f)
            ],
            [0, 1, 2, 0, 2, 3]);
    }

    private static PlaceholderMesh CreateBoxMesh(float width, float height, float depth)
    {
        var halfWidth = width * 0.5f;
        var halfDepth = depth * 0.5f;
        var positions = new List<Float3>(24);
        var texCoords = new List<Float2>(24);
        var indices = new List<ushort>(36);

        AddFace(
            positions,
            texCoords,
            indices,
            new Float3(-halfWidth, 0f, halfDepth),
            new Float3(halfWidth, 0f, halfDepth),
            new Float3(halfWidth, height, halfDepth),
            new Float3(-halfWidth, height, halfDepth));

        AddFace(
            positions,
            texCoords,
            indices,
            new Float3(halfWidth, 0f, -halfDepth),
            new Float3(-halfWidth, 0f, -halfDepth),
            new Float3(-halfWidth, height, -halfDepth),
            new Float3(halfWidth, height, -halfDepth));

        AddFace(
            positions,
            texCoords,
            indices,
            new Float3(-halfWidth, 0f, -halfDepth),
            new Float3(-halfWidth, 0f, halfDepth),
            new Float3(-halfWidth, height, halfDepth),
            new Float3(-halfWidth, height, -halfDepth));

        AddFace(
            positions,
            texCoords,
            indices,
            new Float3(halfWidth, 0f, halfDepth),
            new Float3(halfWidth, 0f, -halfDepth),
            new Float3(halfWidth, height, -halfDepth),
            new Float3(halfWidth, height, halfDepth));

        AddFace(
            positions,
            texCoords,
            indices,
            new Float3(-halfWidth, height, halfDepth),
            new Float3(halfWidth, height, halfDepth),
            new Float3(halfWidth, height, -halfDepth),
            new Float3(-halfWidth, height, -halfDepth));

        AddFace(
            positions,
            texCoords,
            indices,
            new Float3(-halfWidth, 0f, -halfDepth),
            new Float3(halfWidth, 0f, -halfDepth),
            new Float3(halfWidth, 0f, halfDepth),
            new Float3(-halfWidth, 0f, halfDepth));

        return new PlaceholderMesh(positions.ToArray(), texCoords.ToArray(), indices.ToArray());
    }

    private static void AddFace(List<Float3> positions, List<Float2> texCoords, List<ushort> indices, Float3 a, Float3 b, Float3 c, Float3 d)
    {
        var baseIndex = checked((ushort)positions.Count);
        positions.Add(a);
        positions.Add(b);
        positions.Add(c);
        positions.Add(d);

        texCoords.Add(new Float2(0f, 1f));
        texCoords.Add(new Float2(1f, 1f));
        texCoords.Add(new Float2(1f, 0f));
        texCoords.Add(new Float2(0f, 0f));

        indices.Add(baseIndex);
        indices.Add((ushort)(baseIndex + 1));
        indices.Add((ushort)(baseIndex + 2));
        indices.Add(baseIndex);
        indices.Add((ushort)(baseIndex + 2));
        indices.Add((ushort)(baseIndex + 3));
    }

    private sealed record PlaceholderShapeDefinition(string Id, Func<PlaceholderMesh> Build);

    private sealed record PlaceholderMesh(Float3[] Positions, Float2[] TexCoords, ushort[] Indices);

    private sealed record Float3(float X, float Y, float Z);

    private sealed record Float2(float X, float Y);

    private sealed record RegistryManifest(List<RegistryEntry> Models);

    private sealed record RegistryEntry(string Id, string GltfPath, float Scale);
}

public readonly record struct ScaffoldResult(bool Success, string Message, int GeneratedCount)
{
    public static ScaffoldResult Ok(string message, int generatedCount)
    {
        return new ScaffoldResult(true, message, generatedCount);
    }

    public static ScaffoldResult Failed(string message)
    {
        return new ScaffoldResult(false, message, 0);
    }
}