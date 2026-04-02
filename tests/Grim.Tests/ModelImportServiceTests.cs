using System.Text.Json;
using Grim.Tools.Models;
using Xunit;

namespace Grim.Tests;

public sealed class ModelImportServiceTests
{
    [Fact]
    public void Import_UpsertsRegistryEntry_ForSelectedId()
    {
        var repoRoot = CreateTempRepo();
        try
        {
            WriteFile(repoRoot, "content/models/import-manifest.json", """
            {
              "imports": [
                {
                  "id": "pillar_v1",
                  "sourceFbx": "content/models/_imports/pillar_v1/pillar_v1.fbx",
                  "targetDir": "pillar_v1",
                  "outputFile": "pillar_v1.gltf",
                  "scale": 1.5
                }
              ]
            }
            """);

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

            WriteFile(repoRoot, "content/models/_imports/pillar_v1/pillar_v1.fbx", "fake fbx");

            var importer = new ModelImportService(new FakeConverter());
            var result = importer.Import(repoRoot, "pillar_v1");

            Assert.True(result.Success);
            Assert.Equal(1, result.ImportedCount);

            var registryJson = File.ReadAllText(Path.Combine(repoRoot, "content", "models", "registry.json"));
            var registry = JsonSerializer.Deserialize<RegistryDoc>(registryJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(registry);
            Assert.Contains(registry!.Models, item => item.Id == "pillar_v1" && item.GltfPath == "pillar_v1/pillar_v1.gltf");
        }
        finally
        {
            Directory.Delete(repoRoot, true);
        }
    }

    [Fact]
    public void Import_FailsWhenSourceFbxMissing()
    {
        var repoRoot = CreateTempRepo();
        try
        {
            WriteFile(repoRoot, "content/models/import-manifest.json", """
            {
              "imports": [
                {
                  "id": "pillar_v1",
                  "sourceFbx": "content/models/_imports/pillar_v1/pillar_v1.fbx",
                  "targetDir": "pillar_v1"
                }
              ]
            }
            """);

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

            var importer = new ModelImportService(new FakeConverter());
            var result = importer.Import(repoRoot, "pillar_v1");

            Assert.False(result.Success);
            Assert.Contains("Input file not found", result.Message, StringComparison.Ordinal);
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

    private sealed class FakeConverter : IModelConverter
    {
        public ConvertResult Convert(string inputFbxPath, string outputGltfPath)
        {
            if (!File.Exists(inputFbxPath))
            {
                return ConvertResult.Failed($"Input file not found: {inputFbxPath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputGltfPath)!);
            File.WriteAllText(outputGltfPath, "{}");
            return ConvertResult.Ok($"Converted: {inputFbxPath} -> {outputGltfPath}");
        }
    }

    private sealed record RegistryDoc(List<RegistryEntry> Models);

    private sealed record RegistryEntry(string Id, string GltfPath, float Scale);
}
