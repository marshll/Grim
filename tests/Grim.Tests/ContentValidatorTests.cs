using Grim.Tools.ContentValidation;
using Xunit;

namespace Grim.Tests;

public sealed class ContentValidatorTests
{
    [Fact]
    public void Validate_PassesForMinimalValidContent()
    {
        var repoRoot = CreateTempRepo();
        try
        {
            WriteFile(repoRoot, "content/zones/start_zone.json", """
            {
              "id": "start_zone",
              "name": "Valley"
            }
            """);

            WriteFile(repoRoot, "content/models/registry.json", """
            {
              "models": [
                { "id": "obelisk_v1", "gltfPath": "obelisk_v1/obelisk_v1.gltf", "scale": 1.0 }
              ]
            }
            """);

            WriteFile(repoRoot, "content/models/import-manifest.json", """
            {
              "imports": []
            }
            """);

            var validator = new ContentValidator();
            var result = validator.Validate(repoRoot);

            Assert.True(result.Success);
            Assert.Empty(result.Errors);
        }
        finally
        {
            Directory.Delete(repoRoot, true);
        }
    }

    [Fact]
    public void Validate_FailsWhenRegistryMissingModelEntries()
    {
        var repoRoot = CreateTempRepo();
        try
        {
            WriteFile(repoRoot, "content/zones/start_zone.json", """
            {
              "id": "start_zone"
            }
            """);

            WriteFile(repoRoot, "content/models/registry.json", """
            {
              "models": []
            }
            """);

            WriteFile(repoRoot, "content/models/import-manifest.json", """
            {
              "imports": []
            }
            """);

            var validator = new ContentValidator();
            var result = validator.Validate(repoRoot);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, item => item.Contains("Registry must include at least one model", StringComparison.Ordinal));
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
        Directory.CreateDirectory(Path.Combine(root, "content"));
        return root;
    }

    private static void WriteFile(string repoRoot, string relativePath, string content)
    {
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
    }
}
