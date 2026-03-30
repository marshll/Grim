using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Grim.Shared;

public sealed record NetworkFrame(string MessageType, string JsonPayload);

public static class NetworkCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static async Task WriteMessageAsync<T>(
        Stream stream,
        string messageType,
        T payload,
        CancellationToken cancellationToken)
    {
        var jsonPayload = JsonSerializer.Serialize(payload, JsonOptions);
        var frame = new NetworkFrame(messageType, jsonPayload);
        var frameJson = JsonSerializer.Serialize(frame, JsonOptions);
        var frameBytes = Encoding.UTF8.GetBytes(frameJson);

        var lengthPrefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, frameBytes.Length);

        await stream.WriteAsync(lengthPrefix, cancellationToken);
        await stream.WriteAsync(frameBytes, cancellationToken);
    }

    public static async Task<NetworkFrame?> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthPrefix = new byte[sizeof(int)];
        var prefixRead = await ReadExactAsync(stream, lengthPrefix, cancellationToken);
        if (!prefixRead)
        {
            return null;
        }

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthPrefix);
        if (payloadLength <= 0 || payloadLength > 1024 * 1024)
        {
            throw new InvalidDataException($"Invalid frame size: {payloadLength}");
        }

        var payload = new byte[payloadLength];
        var payloadRead = await ReadExactAsync(stream, payload, cancellationToken);
        if (!payloadRead)
        {
            return null;
        }

        var frameJson = Encoding.UTF8.GetString(payload);
        return JsonSerializer.Deserialize<NetworkFrame>(frameJson, JsonOptions)
            ?? throw new InvalidDataException("Frame deserialization returned null");
    }

    public static T DeserializePayload<T>(NetworkFrame frame)
    {
        return JsonSerializer.Deserialize<T>(frame.JsonPayload, JsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize payload for type {typeof(T).Name}");
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var readTotal = 0;

        while (readTotal < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(readTotal, buffer.Length - readTotal),
                cancellationToken);

            if (bytesRead == 0)
            {
                return false;
            }

            readTotal += bytesRead;
        }

        return true;
    }
}