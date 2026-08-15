using System.Diagnostics;
using System.Text.Json;

namespace HxGLTF.Validator;

/// <summary>
/// The result of a validation: issues, counts and the info block. <see cref="ToJson"/> writes the exact JSON
/// report format of the Khronos glTF-Validator (same keys, same order), so reports are directly comparable.
/// </summary>
[DebuggerDisplay("{NumErrors} errors, {NumWarnings} warnings, {NumInfos} infos, {NumHints} hints")]
public sealed class ValidationReport
{
    /// <summary>Version string of the reference validator this port mirrors.</summary>
    public const string ReferenceValidatorVersion = "2.0.0-dev.3.11";

    /// <summary>The URI as written into the report (null when validating from memory without a uri).</summary>
    public string? Uri { get; internal set; }

    /// <summary><c>model/gltf+json</c> or <c>model/gltf-binary</c>; null when the container could not be detected.</summary>
    public string? MimeType { get; internal set; }

    public string ValidatorVersion { get; internal set; } = ReferenceValidatorVersion;

    public DateTime? ValidatedAt { get; internal set; }

    public IReadOnlyList<ValidationIssue> Issues => _issues;
    private readonly List<ValidationIssue> _issues;

    public int NumErrors { get; }
    public int NumWarnings { get; }
    public int NumInfos { get; }
    public int NumHints { get; }

    /// <summary>True when <see cref="ValidationOptions.MaxIssues"/> was reached and validation stopped early.</summary>
    public bool Truncated { get; }

    /// <summary>The info block; null when the asset version could not be read (parsing was aborted).</summary>
    public ValidationInfo? Info { get; }

    /// <summary>True when the report contains no errors.</summary>
    public bool IsValid => NumErrors == 0;

    public ValidationReport(List<ValidationIssue> issues, bool truncated, ValidationInfo? info)
    {
        _issues = issues;
        Truncated = truncated;
        Info = info;
        foreach (var issue in issues)
        {
            switch (issue.Severity)
            {
                case ValidationSeverity.Error: NumErrors++; break;
                case ValidationSeverity.Warning: NumWarnings++; break;
                case ValidationSeverity.Information: NumInfos++; break;
                default: NumHints++; break;
            }
        }
    }

    /// <summary>Issues of one severity.</summary>
    public IEnumerable<ValidationIssue> Of(ValidationSeverity severity) => _issues.Where(i => i.Severity == severity);

    /// <summary>The report as JSON, byte-compatible with the reference validator's output.</summary>
    public string ToJson(bool indented = true)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            WriteTo(w);
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Write the report as JSON to a writer (see <see cref="ToJson"/>).</summary>
    public void WriteTo(Utf8JsonWriter w)
    {
        w.WriteStartObject();
        if (Uri != null) w.WriteString("uri", Uri);
        if (MimeType != null) w.WriteString("mimeType", MimeType);
        w.WriteString("validatorVersion", ValidatorVersion);
        if (ValidatedAt != null) w.WriteString("validatedAt", ValidatedAt.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", System.Globalization.CultureInfo.InvariantCulture));

        w.WriteStartObject("issues");
        w.WriteNumber("numErrors", NumErrors);
        w.WriteNumber("numWarnings", NumWarnings);
        w.WriteNumber("numInfos", NumInfos);
        w.WriteNumber("numHints", NumHints);
        w.WriteStartArray("messages");
        foreach (var issue in _issues)
        {
            w.WriteStartObject();
            w.WriteString("code", issue.Code);
            w.WriteString("message", issue.Message);
            w.WriteNumber("severity", (int)issue.Severity);
            if (issue.Pointer != null) w.WriteString("pointer", issue.Pointer);
            else if (issue.Offset != null) w.WriteNumber("offset", issue.Offset.Value);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteBoolean("truncated", Truncated);
        w.WriteEndObject();

        if (Info != null)
        {
            var info = Info;
            w.WriteStartObject("info");
            w.WriteString("version", info.Version);
            if (info.MinVersion != null) w.WriteString("minVersion", info.MinVersion);
            if (info.Generator != null) w.WriteString("generator", info.Generator);
            if (info.ExtensionsUsed.Length > 0) WriteStrings(w, "extensionsUsed", info.ExtensionsUsed);
            if (info.ExtensionsRequired.Length > 0) WriteStrings(w, "extensionsRequired", info.ExtensionsRequired);
            if (info.Resources.Count > 0)
            {
                w.WriteStartArray("resources");
                foreach (var r in info.Resources)
                {
                    w.WriteStartObject();
                    w.WriteString("pointer", r.Pointer);
                    if (r.MimeType != null) w.WriteString("mimeType", r.MimeType);
                    if (r.Storage != null) w.WriteString("storage", r.Storage);
                    if (r.Uri != null) w.WriteString("uri", r.Uri);
                    if (r.ByteLength != null) w.WriteNumber("byteLength", r.ByteLength.Value);
                    if (r.Image != null)
                    {
                        w.WriteStartObject("image");
                        w.WriteNumber("width", r.Image.Width);
                        w.WriteNumber("height", r.Image.Height);
                        if (r.Image.Format != null) w.WriteString("format", r.Image.Format);
                        if (r.Image.Primaries != null) w.WriteString("primaries", r.Image.Primaries);
                        if (r.Image.Transfer != null) w.WriteString("transfer", r.Image.Transfer);
                        if (r.Image.Bits > 0) w.WriteNumber("bits", r.Image.Bits);
                        w.WriteEndObject();
                    }
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            w.WriteNumber("animationCount", info.AnimationCount);
            w.WriteNumber("materialCount", info.MaterialCount);
            w.WriteBoolean("hasMorphTargets", info.HasMorphTargets);
            w.WriteBoolean("hasSkins", info.HasSkins);
            w.WriteBoolean("hasTextures", info.HasTextures);
            w.WriteBoolean("hasDefaultScene", info.HasDefaultScene);
            w.WriteNumber("drawCallCount", info.DrawCallCount);
            w.WriteNumber("totalVertexCount", info.TotalVertexCount);
            w.WriteNumber("totalTriangleCount", info.TotalTriangleCount);
            w.WriteNumber("maxUVs", info.MaxUVs);
            w.WriteNumber("maxInfluences", info.MaxInfluences);
            w.WriteNumber("maxAttributes", info.MaxAttributes);
            w.WriteEndObject();
        }
        w.WriteEndObject();

        static void WriteStrings(Utf8JsonWriter w, string name, string[] values)
        {
            w.WriteStartArray(name);
            foreach (var v in values) w.WriteStringValue(v);
            w.WriteEndArray();
        }
    }

    /// <summary>Convert the issues into HxGLTF <see cref="LoadMessage"/>s (for merging into <see cref="GLTFFile.Report"/>).</summary>
    public IEnumerable<LoadMessage> ToLoadMessages()
    {
        foreach (var issue in _issues)
        {
            var severity = issue.Severity switch
            {
                ValidationSeverity.Error => LoadSeverity.Error,
                ValidationSeverity.Warning => LoadSeverity.Warning,
                ValidationSeverity.Information => LoadSeverity.Info,
                _ => LoadSeverity.Hint,
            };
            yield return new LoadMessage(severity, issue.Code, issue.Pointer ?? (issue.Offset != null ? "@" + issue.Offset : ""), issue.Message);
        }
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("Validation: ").Append(NumErrors).Append(" errors, ").Append(NumWarnings).Append(" warnings, ")
          .Append(NumInfos).Append(" infos, ").Append(NumHints).AppendLine(" hints");
        foreach (var i in _issues) sb.Append("  ").AppendLine(i.ToString());
        return sb.ToString();
    }
}
