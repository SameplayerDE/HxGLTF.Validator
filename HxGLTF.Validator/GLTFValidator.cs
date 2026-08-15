using HxGLTF.Validator.Internal;

namespace HxGLTF.Validator;

/// <summary>
/// Pure C# port of the Khronos glTF-Validator. Validates a glTF 2.0 / GLB document and returns a
/// <see cref="ValidationReport"/> with the same issue codes, messages, pointers and JSON layout as the reference validator.
/// </summary>
public static class GLTFValidator
{
    /// <summary>Validate a file. External resources are resolved relative to the file unless the options provide a resolver.</summary>
    public static ValidationReport Validate(string path, ValidationOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        options ??= new ValidationOptions();
        var bytes = File.ReadAllBytes(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        options.ExternalResourceResolver ??= uri => ResolveFromDirectory(directory, uri);
        options.Uri ??= path.Replace('\\', '/');
        return Validate(bytes, Path.GetFileName(path), options);
    }

    /// <summary>Validate in-memory data (GLB or glTF JSON). <paramref name="fileName"/> selects the reader by extension when given.</summary>
    public static ValidationReport Validate(byte[] data, string? fileName = null, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        options ??= new ValidationOptions();
        var context = new Context(options);

        GltfReaderResult? readerResult = null;
        try
        {
            readerResult = GltfReader.Read(data, fileName, context);
            if (options.ValidateResources && readerResult?.Gltf != null)
            {
                var loader = new ResourcesLoader(context, readerResult.Gltf, readerResult.Buffer, options.ExternalResourceResolver);
                loader.Load();
            }
        }
        catch (IssuesLimitExceededException)
        {
            // validation stopped early; the report is marked as truncated
        }

        return BuildReport(context, readerResult, options);
    }

    /// <summary>Validate a stream (read completely into memory first).</summary>
    public static ValidationReport Validate(Stream stream, string? fileName = null, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Validate(ms.ToArray(), fileName, options);
    }

    private static byte[]? ResolveFromDirectory(string directory, string uri)
    {
        string path;
        try
        {
            var decoded = Uri.UnescapeDataString(uri);
            path = Path.Combine(directory, decoded);
        }
        catch (UriFormatException)
        {
            return null;
        }
        if (!File.Exists(path)) throw new FileNotFoundException("Resource not found", path);
        return File.ReadAllBytes(path);
    }

    private static ValidationReport BuildReport(Context context, GltfReaderResult? readerResult, ValidationOptions options)
    {
        var issues = context.Issues.Select(i => i.ToValidationIssue()).ToList();
        var info = BuildInfo(context, readerResult?.Gltf);
        var report = new ValidationReport(issues, context.IsTruncated, info)
        {
            Uri = options.Uri,
            MimeType = readerResult?.MimeType,
            ValidatedAt = options.WriteTimestamp ? DateTime.UtcNow : null,
        };
        return report;
    }

    private static ValidationInfo? BuildInfo(Context context, Gltf? root)
    {
        if (root?.Asset?.Version == null) return null;

        var info = new ValidationInfo
        {
            Version = root.Asset.Version,
            MinVersion = root.Asset.MinVersion,
            Generator = root.Asset.Generator,
            ExtensionsUsed = root.ExtensionsUsed.Distinct(StringComparer.Ordinal).ToArray(),
            ExtensionsRequired = root.ExtensionsRequired.Distinct(StringComparer.Ordinal).ToArray(),
            Resources = context.Resources.ToList(),
            AnimationCount = root.Animations.Length,
            MaterialCount = root.Materials.Length,
            HasMorphTargets = root.Meshes.Any(mesh => mesh.Primitives != null && mesh.Primitives.Any(p => p != null && p.Targets != null)),
            HasSkins = root.Skins.IsNotEmpty,
            HasTextures = root.Textures.IsNotEmpty,
            HasDefaultScene = root.Scene != null,
        };

        int drawCallCount = 0, maxAttributes = 0, maxUVs = 0, maxInfluences = 0;
        long totalVertexCount = 0, totalTriangleCount = 0;
        foreach (var mesh in root.Meshes.NonNull())
        {
            if (mesh.Primitives == null) continue;
            drawCallCount += mesh.Primitives.Count;
            foreach (var primitive in mesh.Primitives)
            {
                if (primitive == null) continue;
                if (primitive.VertexCount != -1) totalVertexCount += primitive.VertexCount;
                totalTriangleCount += primitive.TrianglesCount;
                maxAttributes = Math.Max(maxAttributes, primitive.Attributes.Count);
                maxUVs = Math.Max(maxUVs, primitive.TexCoordCount);
                maxInfluences = Math.Max(maxInfluences, primitive.JointsCount * 4);
            }
        }
        info.DrawCallCount = drawCallCount;
        info.TotalVertexCount = totalVertexCount;
        info.TotalTriangleCount = totalTriangleCount;
        info.MaxUVs = maxUVs;
        info.MaxInfluences = maxInfluences;
        info.MaxAttributes = maxAttributes;
        return info;
    }
}
