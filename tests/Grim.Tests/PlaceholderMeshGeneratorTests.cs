using System.Text.Json;
using Grim.Tools.Models;
using Xunit;

namespace Grim.Tests;

public sealed class PlaceholderMeshGeneratorTests
{
    [Fact]
    public void Generate_CreatesPlaceholderModelsAndUpdatesRegistry()
    {
        var repoRoot = CreateTempRepo();
        try
        {
            WriteFile(repoRoot, "content/models/registry.json", """
            {
              "models": [
                {
                  "id": "obelisk_v1",
                  "gltfPath": "obelisk_v1/obelisk_v1.gltf",
                  "scale": 1.25
                }
              ]
            }
            """);

            var generator = new PlaceholderMeshGenerator();
            var result = generator.Generate(repoRoot);

            Assert.True(result.Success);
            Assert.Equal(4, result.GeneratedCount);

            var expectedIds = new[] { "ground_tile_v1", "pillar_v1", "rock_v1", "wall_v1" };
            foreach (var id in expectedIds)
            {
                Assert.True(File.Exists(Path.Combine(repoRoot, "content", "models", id, $"{id}.gltf")));
                Assert.True(File.Exists(Path.Combine(repoRoot, "content", "models", id, $"{id}.bin")));
            }

            var registryJson = File.ReadAllText(Path.Combine(repoRoot, "content", "models", "registry.json"));
            var registry = JsonSerializer.Deserialize<RegistryDoc>(registryJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(registry);
            Assert.Equal(5, registry!.Models.Count);
            Assert.Contains(registry.Models, item => item.Id == "ground_tile_v1" && item.GltfPath == "ground_tile_v1/ground_tile_v1.gltf");
            Assert.Contains(registry.Models, item => item.Id == "wall_v1" && item.GltfPath == "wall_v1/wall_v1.gltf");
        }
        finally
        {
            Directory.Delete(repoRoot, true);
        }
    }

    private static string CreateTempRepo()
    {
        var root = Path.Combine(Path.GetTempPath(), "grim-tools-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "README.md"), "# Test Repo");
        Directory.CreateDirectory(Path.Combine(root, "content", "models"));
        return root;
    }

    private static void WriteFile(string repoRoot, string relativePath, string content)
    {
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private sealed record RegistryDoc(List<RegistryEntry> Models);

    private sealed record RegistryEntry(string Id, string GltfPath, float Scale);
}