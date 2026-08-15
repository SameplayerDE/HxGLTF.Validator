// Port of lib/src/gltf_reader.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class GltfReaderResult
{
    public readonly string MimeType;
    public readonly Gltf? Gltf;
    public readonly byte[]? Buffer;

    public GltfReaderResult(string mimeType, Gltf? gltf, byte[]? buffer)
    {
        MimeType = mimeType;
        Gltf = gltf;
        Buffer = buffer;
    }
}

internal static class GltfReader
{
    public const string JsonMimeType = "model/gltf+json";

    /// <summary>
    /// Dart <c>GltfReader.filename(...)</c> falling back to <c>GltfReader.detect(...)</c>: picks the GLB reader for
    /// <c>.glb</c> files, the JSON reader for <c>.gltf</c> files, otherwise sniffs the first byte.
    /// Throws <see cref="GltfInvalidFormatException"/> when the format cannot be detected.
    /// </summary>
    public static GltfReaderResult? Read(byte[] data, string? filename, Context context)
    {
        if (filename != null)
        {
            var lower = filename.ToLowerInvariant();
            if (lower.EndsWith(".glb", StringComparison.Ordinal))
            {
                return GlbReader.Read(data, context);
            }

            if (lower.EndsWith(".gltf", StringComparison.Ordinal))
            {
                return GltfJsonReader.Read(data, context);
            }
        }

        return Detect(data, context);
    }

    /// <summary>Dart <c>GltfReader.detect</c>: detects the glTF type based on the first byte.</summary>
    public static GltfReaderResult? Detect(byte[] data, Context context)
    {
        // Letter "g"
        const byte g = 0x67;

        // Allowed whitespace chars
        const byte ht = 0x09;
        const byte sp = 0x20;
        const byte lf = 0x0A;
        const byte cr = 0x0D;

        // Left curly bracket
        const byte cl = 0x7B;

        // UTF-8 BOM first byte
        const byte bom = 0xEF;

        if (data.Length == 0)
        {
            throw new GltfInvalidFormatException();
        }

        var b = data[0];
        if (g == b)
        {
            return GlbReader.Read(data, context);
        }
        if (cl == b ||
            ht == b ||
            sp == b ||
            lf == b ||
            cr == b ||
            bom == b)
        {
            return GltfJsonReader.Read(data, context);
        }

        throw new GltfInvalidFormatException();
    }
}

internal static class GltfJsonReader
{
    public const string MimeType = GltfReader.JsonMimeType;

    /// <summary>Dart <c>GltfJsonReader(stream, context).read()</c> over a complete byte array.</summary>
    public static GltfReaderResult? Read(byte[] data, Context context)
    {
        var gltf = Process(data, closed: true, context, out var aborted, out var completed);
        // Dart: the reader completes with null (no result, no mimeType) unless the JSON was a map.
        return completed ? new GltfReaderResult(MimeType, gltf, null) : null;
    }

    public static Gltf? Process(ReadOnlySpan<byte> data, bool closed, Context context, out bool abortedBeforeClose)
        => Process(data, closed, context, out abortedBeforeClose, out _);

    /// <summary>
    /// Emulates the Dart chunked JSON decoding: BOM check on the first bytes, INVALID_JSON on parse errors,
    /// TYPE_MISMATCH when the root is not an object, then <c>Gltf.FromMap</c>. Returns null when aborted.
    /// When <paramref name="closed"/> is false the stream never ended (truncated GLB chunk): only errors that
    /// Dart's incremental parser would have thrown while consuming the bytes are reported and no root is built;
    /// <paramref name="abortedBeforeClose"/> is then true (Dart: the reader cancelled its still-open input stream).
    /// </summary>
    public static Gltf? Process(ReadOnlySpan<byte> data, bool closed, Context context, out bool abortedBeforeClose, out bool completedWithResult)
    {
        abortedBeforeClose = false;
        completedWithResult = false;

        // UTF-8 BOM may appear only at the beginning of stream.
        if (data.Length > 0 && 0xEF == data[0])
        {
            context.AddIssue(SchemaError.InvalidJson,
                noPointer: true,
                args: new object?[] { "BOM found at the beginning of UTF-8 stream." });
        }

        // Dart: the UTF-8 JSON parser skips a complete BOM; offsets stay relative to the original bytes.
        var bomLength = 0;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            bomLength = 3;
        }
        var body = data[bomLength..];

        if (!closed)
        {
            // Dart: addSlice(data, ..., isLast: false); only syntax errors inside the received bytes surface.
            var error = TryParsePartial(body, bomLength);
            if (error != null)
            {
                context.AddIssue(SchemaError.InvalidJson, noPointer: true, args: new object?[] { error });
                abortedBeforeClose = true;
            }
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions { MaxDepth = 1024 });
        }
        catch (JsonException e)
        {
            context.AddIssue(SchemaError.InvalidJson, noPointer: true,
                args: new object?[] { FormatJsonError(e, body, bomLength) });
            return null;
        }

        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            try
            {
                var gltf = Gltf.FromMap(root, context);
                completedWithResult = true;
                return gltf;
            }
            catch (IssuesLimitExceededException)
            {
                return null;
            }
        }

        context.AddIssue(SchemaError.TypeMismatch,
            noPointer: true, args: new object?[] { JsonUtils.Value(root), "object" });
        return null;
    }

    /// <summary>
    /// Dart's _ChunkedJsonParser.fail(): "Unexpected character" at the offending byte offset, or
    /// "Unexpected end of input" when the parser ran out of data. Printed as FormatException.toString()
    /// with a non-string source: "FormatException: message (at offset N)".
    /// </summary>
    private static string FormatJsonError(JsonException e, ReadOnlySpan<byte> body, int bomLength)
    {
        var offset = ComputeOffset(e, body);
        var message = offset >= body.Length ? "Unexpected end of input" : "Unexpected character";
        return "FormatException: " + message + " (at offset " + (offset + bomLength).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
    }

    private static int ComputeOffset(JsonException e, ReadOnlySpan<byte> body)
    {
        var line = e.LineNumber ?? 0;
        var column = e.BytePositionInLine ?? 0;

        // Find the byte offset of the requested line (System.Text.Json counts '\n' line terminators).
        long offset = 0;
        var currentLine = 0;
        while (currentLine < line && offset < body.Length)
        {
            if (body[(int)offset] == (byte)'\n')
            {
                currentLine++;
            }
            offset++;
        }
        offset += column;
        if (offset > body.Length) offset = body.Length;
        if (offset < 0) offset = 0;
        return (int)offset;
    }

    /// <summary>Parses an unfinished JSON stream; returns the Dart-style error text or null when no error is reachable yet.</summary>
    private static string? TryParsePartial(ReadOnlySpan<byte> body, int bomLength)
    {
        var reader = new Utf8JsonReader(body, isFinalBlock: false,
            new JsonReaderState(new JsonReaderOptions { MaxDepth = 1024 }));
        try
        {
            while (reader.Read())
            {
            }
        }
        catch (JsonException e)
        {
            var offset = ComputeOffset(e, body);
            if (offset >= body.Length)
            {
                // Dart would keep waiting for more data.
                return null;
            }
            return "FormatException: Unexpected character (at offset " + (offset + bomLength).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        }
        return null;
    }
}

internal sealed class GltfInvalidFormatException : Exception
{
    public GltfInvalidFormatException() : base("Invalid data: could not detect glTF format.") { }

    public override string ToString() => Message;
}

internal sealed class GltfExternalResourceNotFoundException : Exception
{
    public readonly string Path;

    public GltfExternalResourceNotFoundException(string path) : base("Resource not found (" + path + ").")
    {
        Path = path;
    }

    public override string ToString() => Message;
}
