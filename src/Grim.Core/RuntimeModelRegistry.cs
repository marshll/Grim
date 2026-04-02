using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Grim.Core;

public sealed class RuntimeModelRegistry
{
    private readonly string _registryDirectory;
    private readonly Dictionary<string, RuntimeModelEntry> _entries;
    private readonly Dictionary<string, RuntimeModelAsset> _cache = new(StringComparer.Ordinal);

    private RuntimeModelRegistry(string registryDirectory, Dictionary<string, RuntimeModelEntry> entries)
    {
        _registryDirectory = registryDirectory;
        _entries = entries;
    }

    public static RuntimeModelRegistry? TryLoadFromRepository()
    {
        var repoRoot = FindRepositoryRoot();
        if (repoRoot is null)
        {
            Console.WriteLine("[MODEL] Could not locate repository root; runtime model registry disabled.");
            return null;
        }

        var registryPath = Path.Combine(repoRoot, "content", "models", "registry.json");
        if (!File.Exists(registryPath))
        {
            Console.WriteLine($"[MODEL] Registry file not found at {registryPath}; runtime model registry disabled.");
            return null;
        }

        try
        {
            var json = File.ReadAllText(registryPath);
            var manifest = JsonSerializer.Deserialize<ModelRegistryManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (manifest?.Models is null)
            {
                Console.WriteLine($"[MODEL] Registry file {registryPath} is empty or invalid; runtime model registry disabled.");
                return null;
            }

            var entries = new Dictionary<string, RuntimeModelEntry>(StringComparer.Ordinal);
            foreach (var model in manifest.Models)
            {
                if (string.IsNullOrWhiteSpace(model.Id) || string.IsNullOrWhiteSpace(model.GltfPath))
                {
                    continue;
                }

                entries[model.Id] = new RuntimeModelEntry(model.Id, model.GltfPath, model.Scale <= 0f ? 1f : model.Scale);
            }

            Console.WriteLine($"[MODEL] Loaded registry with {entries.Count} model entries.");
            return new RuntimeModelRegistry(Path.GetDirectoryName(registryPath) ?? string.Empty, entries);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MODEL] Failed to parse model registry: {ex.Message}");
            return null;
        }
    }

    public bool TryGetModel(string modelId, GraphicsDevice graphicsDevice, out RuntimeModelAsset model)
    {
        model = default;

        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        if (_cache.TryGetValue(modelId, out var cached))
        {
            model = cached;
            return true;
        }

        if (!_entries.TryGetValue(modelId, out var entry))
        {
            return false;
        }

        try
        {
            var gltfPath = Path.GetFullPath(Path.Combine(_registryDirectory, entry.GltfPath));
            var loaded = GltfRuntimeLoader.Load(gltfPath, graphicsDevice, entry.Scale);
            _cache[modelId] = loaded;
            model = loaded;
            Console.WriteLine($"[MODEL] Loaded model '{modelId}' from {gltfPath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MODEL] Failed to load model '{modelId}': {ex.Message}");
            return false;
        }
    }

    public IReadOnlyList<string> GetModelIds()
    {
        return _entries.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();
    }

    private static string? FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var readme = Path.Combine(current.FullName, "README.md");
            var contentDir = Path.Combine(current.FullName, "content");
            if (File.Exists(readme) && Directory.Exists(contentDir))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}

public readonly record struct RuntimeModelAsset(VertexPositionTexture[] Vertices, short[] Indices, Texture2D Texture, float Scale);

public sealed record RuntimeModelEntry(string Id, string GltfPath, float Scale);

public sealed class ModelRegistryManifest
{
    public List<ModelRegistryEntry> Models { get; init; } = [];
}

public sealed class ModelRegistryEntry
{
    public string Id { get; init; } = string.Empty;
    public string GltfPath { get; init; } = string.Empty;
    public float Scale { get; init; } = 1f;
}

internal static class GltfRuntimeLoader
{
    public static RuntimeModelAsset Load(string gltfPath, GraphicsDevice graphicsDevice, float defaultScale)
    {
        if (!File.Exists(gltfPath))
        {
            throw new FileNotFoundException("glTF file not found", gltfPath);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(gltfPath));
        var root = doc.RootElement;
        var gltfDir = Path.GetDirectoryName(gltfPath) ?? string.Empty;

        var buffers = LoadBuffers(root, gltfDir);
        var bufferViews = root.GetProperty("bufferViews");
        var accessors = root.GetProperty("accessors");

        var mesh = root.GetProperty("meshes")[0];
        var primitive = mesh.GetProperty("primitives")[0];

        var positionAccessorIndex = primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32();
        var texCoordAccessorIndex = primitive.GetProperty("attributes").GetProperty("TEXCOORD_0").GetInt32();

        var positions = ReadVec3Accessor(accessors[positionAccessorIndex], bufferViews, buffers);
        var texCoords = ReadVec2Accessor(accessors[texCoordAccessorIndex], bufferViews, buffers);

        if (positions.Length != texCoords.Length)
        {
            throw new InvalidDataException("POSITION and TEXCOORD_0 accessor counts do not match.");
        }

        var indices = primitive.TryGetProperty("indices", out var indicesElement)
            ? ReadIndicesAccessor(accessors[indicesElement.GetInt32()], bufferViews, buffers)
            : BuildSequentialIndices(positions.Length);

        var vertices = new VertexPositionTexture[positions.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new VertexPositionTexture(positions[i], texCoords[i]);
        }

        var texture = LoadBaseColorTexture(root, primitive, gltfDir, buffers, graphicsDevice);

        return new RuntimeModelAsset(vertices, indices, texture, defaultScale);
    }

    private static List<byte[]> LoadBuffers(JsonElement root, string gltfDir)
    {
        var list = new List<byte[]>();
        if (!root.TryGetProperty("buffers", out var buffersNode))
        {
            throw new InvalidDataException("glTF has no buffers array.");
        }

        foreach (var bufferNode in buffersNode.EnumerateArray())
        {
            var uri = bufferNode.GetProperty("uri").GetString() ?? string.Empty;
            list.Add(ReadUriBytes(uri, gltfDir));
        }

        return list;
    }

    private static Texture2D LoadBaseColorTexture(
        JsonElement root,
        JsonElement primitive,
        string gltfDir,
        List<byte[]> buffers,
        GraphicsDevice graphicsDevice)
    {
        var textureBytes = GetTextureBytes(root, primitive, gltfDir, buffers);
        if (textureBytes is null)
        {
            var fallback = new Texture2D(graphicsDevice, 1, 1);
            fallback.SetData([Color.White]);
            return fallback;
        }

        using var ms = new MemoryStream(textureBytes);
        return Texture2D.FromStream(graphicsDevice, ms);
    }

    private static byte[]? GetTextureBytes(JsonElement root, JsonElement primitive, string gltfDir, List<byte[]> buffers)
    {
        if (!primitive.TryGetProperty("material", out var materialElement))
        {
            return null;
        }

        if (!root.TryGetProperty("materials", out var materials))
        {
            return null;
        }

        var material = materials[materialElement.GetInt32()];
        if (!material.TryGetProperty("pbrMetallicRoughness", out var pbr))
        {
            return null;
        }

        if (!pbr.TryGetProperty("baseColorTexture", out var baseColorTexture))
        {
            return null;
        }

        var textureIndex = baseColorTexture.GetProperty("index").GetInt32();
        var textures = root.GetProperty("textures");
        var texture = textures[textureIndex];
        var imageIndex = texture.GetProperty("source").GetInt32();
        var images = root.GetProperty("images");
        var image = images[imageIndex];

        if (image.TryGetProperty("uri", out var imageUriElement))
        {
            var uri = imageUriElement.GetString() ?? string.Empty;
            return ReadUriBytes(uri, gltfDir);
        }

        if (image.TryGetProperty("bufferView", out var bufferViewElement))
        {
            var bufferViews = root.GetProperty("bufferViews");
            var bufferView = bufferViews[bufferViewElement.GetInt32()];
            var bufferIndex = bufferView.GetProperty("buffer").GetInt32();
            var byteOffset = bufferView.TryGetProperty("byteOffset", out var offsetElement) ? offsetElement.GetInt32() : 0;
            var byteLength = bufferView.GetProperty("byteLength").GetInt32();
            var data = new byte[byteLength];
            Buffer.BlockCopy(buffers[bufferIndex], byteOffset, data, 0, byteLength);
            return data;
        }

        return null;
    }

    private static Vector3[] ReadVec3Accessor(JsonElement accessor, JsonElement bufferViews, List<byte[]> buffers)
    {
        var (raw, stride) = ReadAccessorRaw(accessor, bufferViews, buffers, 12);
        var count = accessor.GetProperty("count").GetInt32();
        var result = new Vector3[count];

        for (var i = 0; i < count; i++)
        {
            var offset = i * stride;
            result[i] = new Vector3(
                BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(offset + 0, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(offset + 4, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(offset + 8, 4)));
        }

        return result;
    }

    private static Vector2[] ReadVec2Accessor(JsonElement accessor, JsonElement bufferViews, List<byte[]> buffers)
    {
        var (raw, stride) = ReadAccessorRaw(accessor, bufferViews, buffers, 8);
        var count = accessor.GetProperty("count").GetInt32();
        var result = new Vector2[count];

        for (var i = 0; i < count; i++)
        {
            var offset = i * stride;
            result[i] = new Vector2(
                BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(offset + 0, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(offset + 4, 4)));
        }

        return result;
    }

    private static short[] ReadIndicesAccessor(JsonElement accessor, JsonElement bufferViews, List<byte[]> buffers)
    {
        var componentType = accessor.GetProperty("componentType").GetInt32();
        var count = accessor.GetProperty("count").GetInt32();
        var bytesPerIndex = componentType switch
        {
            5121 => 1,
            5123 => 2,
            5125 => 4,
            _ => throw new InvalidDataException($"Unsupported index component type: {componentType}")
        };

        var (raw, stride) = ReadAccessorRaw(accessor, bufferViews, buffers, bytesPerIndex);
        var result = new short[count];

        for (var i = 0; i < count; i++)
        {
            var offset = i * stride;
            var value = componentType switch
            {
                5121 => raw[offset],
                5123 => BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(offset, 2)),
                5125 => BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset, 4)),
                _ => 0u
            };

            if (value > short.MaxValue)
            {
                throw new InvalidDataException("Index exceeds Int16 range for MonoGame DrawUserIndexedPrimitives.");
            }

            result[i] = (short)value;
        }

        return result;
    }

    private static (byte[] Raw, int Stride) ReadAccessorRaw(JsonElement accessor, JsonElement bufferViews, List<byte[]> buffers, int defaultStride)
    {
        var bufferViewIndex = accessor.GetProperty("bufferView").GetInt32();
        var bufferView = bufferViews[bufferViewIndex];
        var bufferIndex = bufferView.GetProperty("buffer").GetInt32();

        var byteOffset = (bufferView.TryGetProperty("byteOffset", out var viewOffset) ? viewOffset.GetInt32() : 0) +
                         (accessor.TryGetProperty("byteOffset", out var accessorOffset) ? accessorOffset.GetInt32() : 0);
        var byteLength = bufferView.GetProperty("byteLength").GetInt32();
        var stride = bufferView.TryGetProperty("byteStride", out var strideElement)
            ? strideElement.GetInt32()
            : defaultStride;

        var raw = new byte[byteLength - (accessor.TryGetProperty("byteOffset", out var localOffset) ? localOffset.GetInt32() : 0)];
        Buffer.BlockCopy(buffers[bufferIndex], byteOffset, raw, 0, raw.Length);

        return (raw, stride);
    }

    private static short[] BuildSequentialIndices(int vertexCount)
    {
        if (vertexCount > short.MaxValue)
        {
            throw new InvalidDataException("Vertex count exceeds Int16 index range.");
        }

        var indices = new short[vertexCount];
        for (short i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        return indices;
    }

    private static byte[] ReadUriBytes(string uri, string baseDirectory)
    {
        if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = uri.IndexOf(',');
            if (comma < 0 || comma >= uri.Length - 1)
            {
                throw new InvalidDataException("Invalid data URI in glTF.");
            }

            var payload = uri[(comma + 1)..];
            return Convert.FromBase64String(payload);
        }

        var filePath = Path.GetFullPath(Path.Combine(baseDirectory, uri));
        return File.ReadAllBytes(filePath);
    }
}
