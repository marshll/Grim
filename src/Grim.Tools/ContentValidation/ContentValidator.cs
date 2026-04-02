using System.Text.Json;

namespace Grim.Tools.ContentValidation;

public sealed class ContentValidator
{
    public ValidationResult Validate(string repoRoot)
    {
        var errors = new List<string>();
        var contentDir = Path.Combine(repoRoot, "content");
        if (!Directory.Exists(contentDir))
        {
            errors.Add($"Content directory not found: {contentDir}");
            PrintErrors(errors);
            return ValidationResult.Failed(errors);
        }

        Console.WriteLine($"Validating content files in {contentDir}");

        foreach (var file in Directory.EnumerateFiles(contentDir, "*.json", SearchOption.AllDirectories))
        {
            ValidateJsonFile(file, errors);
        }

        Console.WriteLine("Checking FBX -> glTF conversion freshness");
        foreach (var fbxFile in Directory.EnumerateFiles(contentDir, "*.fbx", SearchOption.AllDirectories))
        {
            var gltfFile = Path.ChangeExtension(fbxFile, ".gltf");
            if (!File.Exists(gltfFile))
            {
                errors.Add($"Missing converted glTF for {fbxFile} (expected {gltfFile})");
                continue;
            }

            if (File.GetLastWriteTimeUtc(fbxFile) > File.GetLastWriteTimeUtc(gltfFile))
            {
                errors.Add($"Stale conversion: {fbxFile} is newer than {gltfFile}");
                continue;
            }

            Console.WriteLine($"OK: {fbxFile} -> {gltfFile}");
        }

        if (errors.Count > 0)
        {
            PrintErrors(errors);
            return ValidationResult.Failed(errors);
        }

        Console.WriteLine("Validation complete");
        return ValidationResult.Ok();
    }

    private static void ValidateJsonFile(string filePath, ICollection<string> errors)
    {
        JsonDocument document;
        try
        {
            using var stream = File.OpenRead(filePath);
            document = JsonDocument.Parse(stream);
        }
        catch (Exception ex)
        {
            errors.Add($"Invalid JSON in {filePath}: {ex.Message}");
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            var normalized = filePath.Replace('\\', '/');

            if (normalized.EndsWith("/content/models/registry.json", StringComparison.OrdinalIgnoreCase))
            {
                ValidateRegistry(filePath, root, errors);
            }
            else if (normalized.EndsWith("/content/models/import-manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                ValidateImportManifest(filePath, root, errors);
            }
            else
            {
                ValidateHasId(filePath, root, errors);
            }

            if (!errors.Any(item => item.Contains(filePath, StringComparison.Ordinal)))
            {
                Console.WriteLine($"OK: {filePath}");
            }
        }
    }

    private static void ValidateHasId(string filePath, JsonElement root, ICollection<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"Expected JSON object in {filePath}");
            return;
        }

        if (!root.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(idElement.GetString()))
        {
            errors.Add($"Missing or invalid 'id' in {filePath}");
        }
    }

    private static void ValidateRegistry(string filePath, JsonElement root, ICollection<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"Expected JSON object in {filePath}");
            return;
        }

        if (!root.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"Missing or invalid 'models' array in {filePath}");
            return;
        }

        if (modelsElement.GetArrayLength() == 0)
        {
            errors.Add($"Registry must include at least one model in {filePath}");
            return;
        }

        foreach (var model in modelsElement.EnumerateArray())
        {
            if (model.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Invalid model entry in {filePath}: expected object");
                continue;
            }

            if (!TryGetNonEmptyString(model, "id") || !TryGetNonEmptyString(model, "gltfPath"))
            {
                errors.Add($"Model entry missing non-empty 'id' or 'gltfPath' in {filePath}");
            }
        }
    }

    private static void ValidateImportManifest(string filePath, JsonElement root, ICollection<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"Expected JSON object in {filePath}");
            return;
        }

        if (!root.TryGetProperty("imports", out var importsElement) || importsElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"Missing or invalid 'imports' array in {filePath}");
            return;
        }

        foreach (var import in importsElement.EnumerateArray())
        {
            if (import.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Invalid import entry in {filePath}: expected object");
                continue;
            }

            var requiredStringsValid = TryGetNonEmptyString(import, "id")
                && TryGetNonEmptyString(import, "sourceFbx")
                && TryGetNonEmptyString(import, "targetDir");
            if (!requiredStringsValid)
            {
                errors.Add($"Import entry missing required string fields in {filePath}");
                continue;
            }

            if (import.TryGetProperty("scale", out var scaleElement) && scaleElement.ValueKind != JsonValueKind.Number)
            {
                errors.Add($"Import entry 'scale' must be a number in {filePath}");
            }
        }
    }

    private static bool TryGetNonEmptyString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.String
               && !string.IsNullOrWhiteSpace(value.GetString());
    }

    private static void PrintErrors(IEnumerable<string> errors)
    {
        Console.Error.WriteLine("Validation failed:");
        foreach (var error in errors)
        {
            Console.Error.WriteLine($"  - {error}");
        }
    }
}

public sealed record ValidationResult(bool Success, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok()
    {
        return new ValidationResult(true, []);
    }

    public static ValidationResult Failed(IReadOnlyList<string> errors)
    {
        return new ValidationResult(false, errors);
    }
}
