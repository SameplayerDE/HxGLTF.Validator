using System.Diagnostics;

namespace HxGLTF.Validator;

/// <summary>Severity of a validation issue. Numeric values match the Khronos glTF-Validator report format.</summary>
public enum ValidationSeverity
{
    Error = 0,
    Warning = 1,
    Information = 2,
    Hint = 3,
}

/// <summary>One validation issue, identical in content to a message of the Khronos glTF-Validator.</summary>
[DebuggerDisplay("{Severity} {Code,nq} @ {Pointer ?? Offset?.ToString(),nq}: {Message}")]
public sealed class ValidationIssue
{
    /// <summary>Stable issue code, e.g. <c>ACCESSOR_TOO_LONG</c>.</summary>
    public string Code { get; }

    /// <summary>Human readable message, byte-identical to the reference validator.</summary>
    public string Message { get; }

    public ValidationSeverity Severity { get; }

    /// <summary>JSON pointer to the offending location ("" = document root). Null for GLB container issues.</summary>
    public string? Pointer { get; }

    /// <summary>Byte offset for GLB container issues. Null for JSON issues.</summary>
    public long? Offset { get; }

    public ValidationIssue(string code, string message, ValidationSeverity severity, string? pointer, long? offset)
    {
        Code = code;
        Message = message;
        Severity = severity;
        Pointer = pointer;
        Offset = offset;
    }

    public override string ToString()
        => Offset.HasValue
            ? $"{Severity} {Code} @ offset {Offset}: {Message}"
            : $"{Severity} {Code} @ {Pointer}: {Message}";
}

/// <summary>Options for <see cref="GLTFValidator"/>. Defaults mirror the reference validator's library defaults.</summary>
public sealed class ValidationOptions
{
    /// <summary>Also load buffers and images and validate accessor data and image headers (the reference CLI needs
    /// <c>--validate-resources</c> for this; the library default here is true).</summary>
    public bool ValidateResources { get; set; } = true;

    /// <summary>Stop after this many issues (0 = unlimited). The report is then marked as truncated.</summary>
    public int MaxIssues { get; set; }

    /// <summary>Issue codes that are never reported.</summary>
    public HashSet<string> IgnoredIssues { get; } = new(StringComparer.Ordinal);

    /// <summary>When non-empty, only these issue codes are reported.</summary>
    public HashSet<string> OnlyIssues { get; } = new(StringComparer.Ordinal);

    /// <summary>Per-code severity overrides.</summary>
    public Dictionary<string, ValidationSeverity> SeverityOverrides { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Resolves external resources (buffers, images) referenced by URI. Receives the URI as written in the file and
    /// returns the bytes; throw <see cref="FileNotFoundException"/> for a missing resource (reported as IO_ERROR
    /// "Resource not found (uri).", any other exception is reported with its message) or return null to skip the
    /// resource silently (the reference validator does this for non-relative URIs).
    /// Default: files relative to the validated file.
    /// </summary>
    public Func<string, byte[]?>? ExternalResourceResolver { get; set; }

    /// <summary>Write the <c>validatedAt</c> timestamp into the report.</summary>
    public bool WriteTimestamp { get; set; }

    /// <summary>The <c>uri</c> written into the report. Defaults to the path given to Validate.</summary>
    public string? Uri { get; set; }
}

/// <summary>The <c>info</c> section of a validation report.</summary>
public sealed class ValidationInfo
{
    public string Version = "";
    public string? MinVersion;
    public string? Generator;
    public string[] ExtensionsUsed = Array.Empty<string>();
    public string[] ExtensionsRequired = Array.Empty<string>();
    public List<ValidationResource> Resources = new();
    public int AnimationCount;
    public int MaterialCount;
    public bool HasMorphTargets;
    public bool HasSkins;
    public bool HasTextures;
    public bool HasDefaultScene;
    public int DrawCallCount;
    public long TotalVertexCount;
    public long TotalTriangleCount;
    public int MaxUVs;
    public int MaxInfluences;
    public int MaxAttributes;
}

/// <summary>One entry of <c>info.resources</c>: a buffer or image the validator inspected.</summary>
public sealed class ValidationResource
{
    public string Pointer = "";
    public string? MimeType;
    /// <summary><c>data-uri</c>, <c>buffer-view</c>, <c>glb</c> or <c>external</c>.</summary>
    public string? Storage;
    public string? Uri;
    public long? ByteLength;
    public ValidationImageInfo? Image;
}

/// <summary>Image header information for an image resource.</summary>
public sealed class ValidationImageInfo
{
    public int Width;
    public int Height;
    /// <summary><c>rgb</c>, <c>rgba</c>, <c>luminance</c>, <c>luminance-alpha</c>.</summary>
    public string? Format;
    /// <summary><c>srgb</c> or <c>custom</c>.</summary>
    public string? Primaries;
    /// <summary><c>linear</c>, <c>srgb</c> or <c>custom</c>.</summary>
    public string? Transfer;
    public int Bits;
}
