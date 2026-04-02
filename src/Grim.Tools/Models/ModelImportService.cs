using System.Text.Json;

namespace Grim.Tools.Models;

public sealed class ModelImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IModelConverter _converter;

    public ModelImportService(IModelConverter converter)
    {
        _converter = converter;
    }

    public ImportResult Import(string repoRoot, string? modelIdFilter = null)
    {
        var manifestPath = Path.Combine(repoRoot, "content", "models", "import-manifest.json");
        var registryPath = Path.Combine(repoRoot, "content", "models", "registry.json");

        if (!File.Exists(manifestPath))
        {
            return ImportResult.Failed($"Import manifest not found: {manifestPath}");
        }

        if (!File.Exists(registryPath))
        {
            return ImportResult.Failed($"Registry file not found: {registryPath}");
        }

        ImportManifest manifest;
        ModelRegistryManifest registry;
        try
        {
            manifest = JsonSerializer.Deserialize<ImportManifest>(File.ReadAllText(manifestPath), JsonOptions) ?? new ImportManifest([]);
            registry = JsonSerializer.Deserialize<ModelRegistryManifest>(File.ReadAllText(registryPath), JsonOptions) ?? new ModelRegistryManifest([]);
        }
        catch (Exception ex)
        {
            return ImportResult.Failed($"Failed to parse manifest/registry: {ex.Message}");
        }

        var selectedImports = manifest.Imports
            .Where(item => string.IsNullOrWhiteSpace(modelIdFilter) || string.Equals(item.Id, modelIdFilter, StringComparison.Ordinal))
            .ToArray();

        if (selectedImports.Length == 0)
        {
            if (string.IsNullOrWhiteSpace(modelIdFilter))
            {
                return ImportResult.Ok("No import entries found in manifest.", 0);
            }

            return ImportResult.Failed($"No import entry found for id '{modelIdFilter}'.");
        }

        var importedCount = 0;
        var models = registry.Models.ToList();
        foreach (var import in selectedImports)
        {
            if (string.IsNullOrWhiteSpace(import.Id) || string.IsNullOrWhiteSpace(import.SourceFbx) || string.IsNullOrWhiteSpace(import.TargetDir))
            {
                return ImportResult.Failed($"Invalid import entry (missing required fields): {import.Id}");
            }

            var sourcePath = Path.Combine(repoRoot, import.SourceFbx.Replace('/', Path.DirectorySeparatorChar));
            var outputFile = string.IsNullOrWhiteSpace(import.OutputFile) ? $"{import.Id}.gltf" : import.OutputFile;
            var outputPath = Path.Combine(repoRoot, "content", "models", import.TargetDir, outputFile);

            var convertResult = _converter.Convert(sourcePath, outputPath);
            if (!convertResult.Success)
            {
                return ImportResult.Failed($"Import failed for '{import.Id}': {convertResult.Message}");
            }

            var gltfPath = Path.Combine(import.TargetDir, outputFile).Replace('\\', '/');
            models.RemoveAll(model => string.Equals(model.Id, import.Id, StringComparison.Ordinal));
            models.Add(new ModelRegistryEntry(import.Id, gltfPath, import.Scale));
            importedCount++;
        }

        var orderedModels = models.OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
        var updatedRegistry = new ModelRegistryManifest(orderedModels);

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

            return ImportResult.Failed($"Failed to update registry: {ex.Message}");
        }

        return ImportResult.Ok($"Import completed ({importedCount} item(s)).", importedCount);
    }

    public sealed record ImportManifest(List<ImportEntry> Imports);

    public sealed record ImportEntry(
        string Id,
        string SourceFbx,
        string TargetDir,
        string? OutputFile = null,
        float Scale = 1f);

    public sealed record ModelRegistryManifest(List<ModelRegistryEntry> Models);

    public sealed record ModelRegistryEntry(string Id, string GltfPath, float Scale = 1f);
}

public readonly record struct ImportResult(bool Success, string Message, int ImportedCount)
{
    public static ImportResult Ok(string message, int importedCount)
    {
        return new ImportResult(true, message, importedCount);
    }

    public static ImportResult Failed(string message)
    {
        return new ImportResult(false, message, 0);
    }
}
