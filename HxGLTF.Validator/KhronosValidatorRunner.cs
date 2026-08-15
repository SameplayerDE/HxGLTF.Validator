using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HxGLTF.Validator;

/// <summary>
/// Runs the official Khronos <c>gltf_validator</c> executable (https://github.com/KhronosGroup/glTF-Validator/releases)
/// and parses its JSON report into a <see cref="ValidationReport"/>. Use it as a reference or fallback next to
/// <see cref="GLTFValidator"/>. The executable is located via <see cref="ExecutablePath"/>, the environment variable
/// <c>HXGLTF_VALIDATOR</c>, or the PATH.
/// </summary>
public static class KhronosValidatorRunner
{
    /// <summary>Explicit path to <c>gltf_validator(.exe)</c>. When null the environment variable HXGLTF_VALIDATOR and the PATH are searched.</summary>
    public static string? ExecutablePath { get; set; }

    /// <summary>The executable that would be used, or null when none is found.</summary>
    public static string? ResolveExecutable()
    {
        if (!string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath)) return ExecutablePath;
        var env = Environment.GetEnvironmentVariable("HXGLTF_VALIDATOR");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
        var names = OperatingSystem.IsWindows() ? new[] { "gltf_validator.exe", "gltf_validator" } : new[] { "gltf_validator" };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var name in names)
            {
                var candidate = Path.Combine(dir.Trim(), name);
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }

    /// <summary>True when the reference validator executable can be found.</summary>
    public static bool IsAvailable => ResolveExecutable() != null;

    /// <summary>Validate a file with the reference validator. Throws <see cref="InvalidOperationException"/> when the executable is not available.</summary>
    public static ValidationReport Validate(string path, ValidationOptions? options = null)
    {
        var exe = ResolveExecutable() ?? throw new InvalidOperationException("gltf_validator executable not found. Set KhronosValidatorRunner.ExecutablePath or the HXGLTF_VALIDATOR environment variable.");
        options ??= new ValidationOptions();

        var args = new StringBuilder();
        args.Append("-o ");
        if (options.ValidateResources) args.Append("-r ");
        if (options.WriteTimestamp) args.Append("-t ");
        string? configFile = null;
        if (options.MaxIssues > 0 || options.IgnoredIssues.Count > 0 || options.OnlyIssues.Count > 0 || options.SeverityOverrides.Count > 0)
        {
            configFile = Path.GetTempFileName() + ".yaml";
            File.WriteAllText(configFile, BuildYaml(options));
            args.Append("-c ").Append(Quote(configFile)).Append(' ');
        }
        args.Append(Quote(Path.GetFullPath(path)));

        try
        {
            var psi = new ProcessStartInfo(exe, args.ToString())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start gltf_validator.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (string.IsNullOrWhiteSpace(stdout))
                throw new InvalidOperationException("gltf_validator produced no report. stderr: " + stderr);
            var report = ParseReport(stdout);
            if (options.Uri != null) report.Uri = options.Uri;
            return report;
        }
        finally
        {
            if (configFile != null) { try { File.Delete(configFile); } catch (IOException) { } }
        }
    }

    private static string Quote(string s) => "\"" + s.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string BuildYaml(ValidationOptions options)
    {
        var sb = new StringBuilder();
        if (options.MaxIssues > 0) sb.Append("max-issues: ").Append(options.MaxIssues.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (options.IgnoredIssues.Count > 0)
        {
            sb.Append("ignore:\n");
            foreach (var c in options.IgnoredIssues) sb.Append("  - ").Append(c).Append('\n');
        }
        if (options.OnlyIssues.Count > 0)
        {
            sb.Append("only:\n");
            foreach (var c in options.OnlyIssues) sb.Append("  - ").Append(c).Append('\n');
        }
        if (options.SeverityOverrides.Count > 0)
        {
            sb.Append("override:\n");
            foreach (var (code, sev) in options.SeverityOverrides) sb.Append("  ").Append(code).Append(": ").Append(((int)sev).ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Parse a JSON report produced by the reference validator (or by <see cref="ValidationReport.ToJson"/>).</summary>
    public static ValidationReport ParseReport(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var issues = new List<ValidationIssue>();
        bool truncated = false;
        if (root.TryGetProperty("issues", out var jIssues))
        {
            truncated = jIssues.TryGetProperty("truncated", out var t) && t.ValueKind == JsonValueKind.True;
            if (jIssues.TryGetProperty("messages", out var messages))
                foreach (var m in messages.EnumerateArray())
                {
                    issues.Add(new ValidationIssue(
                        m.GetProperty("code").GetString() ?? "",
                        m.GetProperty("message").GetString() ?? "",
                        (ValidationSeverity)m.GetProperty("severity").GetInt32(),
                        m.TryGetProperty("pointer", out var p) ? p.GetString() : null,
                        m.TryGetProperty("offset", out var o) ? o.GetInt64() : null));
                }
        }

        ValidationInfo? info = null;
        if (root.TryGetProperty("info", out var jInfo))
        {
            info = new ValidationInfo
            {
                Version = jInfo.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "",
                MinVersion = jInfo.TryGetProperty("minVersion", out var mv) ? mv.GetString() : null,
                Generator = jInfo.TryGetProperty("generator", out var g) ? g.GetString() : null,
                ExtensionsUsed = Strings(jInfo, "extensionsUsed"),
                ExtensionsRequired = Strings(jInfo, "extensionsRequired"),
                AnimationCount = Int(jInfo, "animationCount"),
                MaterialCount = Int(jInfo, "materialCount"),
                HasMorphTargets = Bool(jInfo, "hasMorphTargets"),
                HasSkins = Bool(jInfo, "hasSkins"),
                HasTextures = Bool(jInfo, "hasTextures"),
                HasDefaultScene = Bool(jInfo, "hasDefaultScene"),
                DrawCallCount = Int(jInfo, "drawCallCount"),
                TotalVertexCount = Long(jInfo, "totalVertexCount"),
                TotalTriangleCount = Long(jInfo, "totalTriangleCount"),
                MaxUVs = Int(jInfo, "maxUVs"),
                MaxInfluences = Int(jInfo, "maxInfluences"),
                MaxAttributes = Int(jInfo, "maxAttributes"),
            };
            if (jInfo.TryGetProperty("resources", out var res))
                foreach (var r in res.EnumerateArray())
                {
                    var entry = new ValidationResource
                    {
                        Pointer = r.TryGetProperty("pointer", out var ptr) ? ptr.GetString() ?? "" : "",
                        MimeType = r.TryGetProperty("mimeType", out var mt) ? mt.GetString() : null,
                        Storage = r.TryGetProperty("storage", out var st) ? st.GetString() : null,
                        Uri = r.TryGetProperty("uri", out var u) ? u.GetString() : null,
                        ByteLength = r.TryGetProperty("byteLength", out var bl) ? bl.GetInt64() : null,
                    };
                    if (r.TryGetProperty("image", out var img))
                        entry.Image = new ValidationImageInfo
                        {
                            Width = Int(img, "width"),
                            Height = Int(img, "height"),
                            Format = img.TryGetProperty("format", out var f) ? f.GetString() : null,
                            Primaries = img.TryGetProperty("primaries", out var pr) ? pr.GetString() : null,
                            Transfer = img.TryGetProperty("transfer", out var tr) ? tr.GetString() : null,
                            Bits = Int(img, "bits"),
                        };
                    info.Resources.Add(entry);
                }
        }

        var report = new ValidationReport(issues, truncated, info)
        {
            Uri = root.TryGetProperty("uri", out var uri) ? uri.GetString() : null,
            MimeType = root.TryGetProperty("mimeType", out var mime) ? mime.GetString() : null,
            ValidatorVersion = root.TryGetProperty("validatorVersion", out var vv) ? vv.GetString() ?? "" : "",
        };
        if (root.TryGetProperty("validatedAt", out var va) && DateTime.TryParse(va.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
            report.ValidatedAt = dt;
        return report;

        static string[] Strings(JsonElement e, string name) => e.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : Array.Empty<string>();
        static int Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
        static long Long(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
        static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    }
}
