using System.Diagnostics;

namespace Grim.Tools.Models;

public interface IModelConverter
{
    ConvertResult Convert(string inputFbxPath, string outputGltfPath);
}

public sealed class ModelConvertService : IModelConverter
{
    public ConvertResult Convert(string inputFbxPath, string outputGltfPath)
    {
        if (!File.Exists(inputFbxPath))
        {
            return ConvertResult.Failed($"Input file not found: {inputFbxPath}");
        }

        var outputDirectory = Path.GetDirectoryName(outputGltfPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return ConvertResult.Failed($"Invalid output path: {outputGltfPath}");
        }

        Directory.CreateDirectory(outputDirectory);

        if (TryRunBlender(inputFbxPath, outputGltfPath, out var blenderError))
        {
            return ConvertResult.Ok($"Converted with Blender: {inputFbxPath} -> {outputGltfPath}");
        }

        if (TryRunProcess("fbx2gltf", $"-i \"{inputFbxPath}\" -o \"{outputGltfPath}\"", out var fbx2GltfError))
        {
            return ConvertResult.Ok($"Converted with fbx2gltf: {inputFbxPath} -> {outputGltfPath}");
        }

        return ConvertResult.Failed($"No converter succeeded. Blender: {blenderError}. fbx2gltf: {fbx2GltfError}");
    }

    private static bool TryRunBlender(string inputFbxPath, string outputGltfPath, out string error)
    {
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"grim_convert_{Guid.NewGuid():N}.py");
        try
        {
            var script = string.Join('\n',
                "import bpy",
                "bpy.ops.wm.read_factory_settings(use_empty=True)",
                $"bpy.ops.import_scene.fbx(filepath=r'{EscapePythonString(inputFbxPath)}')",
                $"bpy.ops.export_scene.gltf(filepath=r'{EscapePythonString(outputGltfPath)}', export_format='GLTF_SEPARATE')");
            File.WriteAllText(tempScriptPath, script);

            return TryRunProcess("blender", $"--background --python \"{tempScriptPath}\"", out error);
        }
        finally
        {
            try
            {
                if (File.Exists(tempScriptPath))
                {
                    File.Delete(tempScriptPath);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private static string EscapePythonString(string path)
    {
        return path.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private static bool TryRunProcess(string fileName, string arguments, out string error)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                error = string.Empty;
                return true;
            }

            var stderr = process.StandardError.ReadToEnd();
            var stdout = process.StandardOutput.ReadToEnd();
            error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

public readonly record struct ConvertResult(bool Success, string Message)
{
    public static ConvertResult Ok(string message)
    {
        return new ConvertResult(true, message);
    }

    public static ConvertResult Failed(string message)
    {
        return new ConvertResult(false, message);
    }
}
