using System.Text.Json;
using System.Text.Json.Nodes;

namespace HxGLTF.Validator.Tests;

/// <summary>
/// Runs every case of the Khronos glTF-Validator test corpus (vendored under Data/khronos) and compares our report with the
/// expected <c>*.report.json</c> structurally (validatorVersion removed, messages/resources order significant),
/// exactly like the reference test harness does.
/// </summary>
public static class Corpus
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "Data", "khronos");

    /// <summary>All (input, expectedReport) pairs below a corpus folder, e.g. "base/data/mesh" or "ext/KHR_lights_punctual".</summary>
    public static IEnumerable<(string file, string report)> Cases(string folder)
    {
        var dir = Path.Combine(Root, folder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(dir)) yield break;
        foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (!f.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) continue;
            var report = f + ".report.json";
            if (File.Exists(report)) yield return (f, report);
        }
    }

    /// <summary>The uri the reference harness writes: "test/&lt;path relative to the corpus root&gt;" with forward slashes.</summary>
    public static string ExpectedUri(string file)
        => "test/" + Path.GetRelativePath(Root, file).Replace('\\', '/');

    public static ValidationReport Run(string file)
    {
        var dir = Path.GetDirectoryName(file)!;
        var options = new ValidationOptions
        {
            WriteTimestamp = false,
            Uri = ExpectedUri(file),
            ExternalResourceResolver = uri =>
            {
                // reference harness: non-relative URIs are not fetched (null); missing files -> not found
                if (uri.Contains("://", StringComparison.Ordinal) || uri.StartsWith('/')) return null;
                var path = Path.Combine(dir, Uri.UnescapeDataString(uri));
                if (!File.Exists(path)) throw new FileNotFoundException("Resource not found", uri);
                return File.ReadAllBytes(path);
            },
        };
        return GLTFValidator.Validate(File.ReadAllBytes(file), Path.GetFileName(file), options);
    }

    /// <summary>Compare with the expected report; returns null when equal, else a description of the first difference.</summary>
    public static string? Compare(string file, string reportFile)
    {
        var expected = JsonNode.Parse(File.ReadAllText(reportFile))!.AsObject();
        expected.Remove("validatorVersion");
        var actual = JsonNode.Parse(Run(file).ToJson())!.AsObject();
        actual.Remove("validatorVersion");
        if (JsonNode.DeepEquals(expected, actual)) return null;
        return Describe(expected, actual);
    }

    private static string Describe(JsonObject expected, JsonObject actual)
    {
        var sb = new System.Text.StringBuilder();
        var em = expected["issues"]?["messages"]?.AsArray() ?? new JsonArray();
        var am = actual["issues"]?["messages"]?.AsArray() ?? new JsonArray();
        int n = Math.Max(em.Count, am.Count);
        for (int i = 0; i < n; i++)
        {
            var e = i < em.Count ? em[i]!.ToJsonString() : "<none>";
            var a = i < am.Count ? am[i]!.ToJsonString() : "<none>";
            if (e != a)
            {
                sb.Append("  message[").Append(i).Append("] expected ").Append(e).Append(" actual ").Append(a).AppendLine();
                if (sb.Length > 1500) break;
            }
        }
        var ei = expected["info"]?.ToJsonString();
        var ai = actual["info"]?.ToJsonString();
        if (ei != ai) sb.Append("  info expected ").Append(ei).Append(" actual ").Append(ai).AppendLine();
        foreach (var key in new[] { "uri", "mimeType" })
        {
            var e = expected[key]?.ToJsonString();
            var a = actual[key]?.ToJsonString();
            if (e != a) sb.Append("  ").Append(key).Append(" expected ").Append(e).Append(" actual ").Append(a).AppendLine();
        }
        var et = expected["issues"]?["truncated"]?.ToJsonString();
        var at = actual["issues"]?["truncated"]?.ToJsonString();
        if (et != at) sb.Append("  truncated expected ").Append(et).Append(" actual ").Append(at).AppendLine();
        return sb.Length == 0 ? "  (differs in a way not shown)" : sb.ToString();
    }

    public static void AssertFolder(string folder)
    {
        var cases = Cases(folder).ToList();
        Assert.NotEmpty(cases);
        var failures = new List<string>();
        foreach (var (file, report) in cases)
        {
            string? diff;
            try
            {
                diff = Compare(file, report);
            }
            catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
            {
                diff = "  EXCEPTION " + ex.GetType().Name + ": " + ex.Message;
            }
            if (diff != null) failures.Add(Path.GetFileName(file) + Environment.NewLine + diff);
        }
        Assert.True(failures.Count == 0, $"{failures.Count}/{cases.Count} cases differ in {folder}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }
}

public class ConformanceBaseTests
{
    [Fact] public void Accessor() => Corpus.AssertFolder("base/data/accessor");
    [Fact] public void AccessorData() => Corpus.AssertFolder("base/data/accessor_data");
    [Fact] public void Animation() => Corpus.AssertFolder("base/data/animation");
    [Fact] public void AnimationData() => Corpus.AssertFolder("base/data/animation_data");
    [Fact] public void Asset() => Corpus.AssertFolder("base/data/asset");
    [Fact] public void Buffer() => Corpus.AssertFolder("base/data/buffer");
    [Fact] public void BufferView() => Corpus.AssertFolder("base/data/buffer_view");
    [Fact] public void Camera() => Corpus.AssertFolder("base/data/camera");
    [Fact] public void Glb() => Corpus.AssertFolder("base/data/glb");
    [Fact] public void Image() => Corpus.AssertFolder("base/data/image");
    [Fact] public void Json() => Corpus.AssertFolder("base/data/json");
    [Fact] public void Material() => Corpus.AssertFolder("base/data/material");
    [Fact] public void Mesh() => Corpus.AssertFolder("base/data/mesh");
    [Fact] public void MeshData() => Corpus.AssertFolder("base/data/mesh_data");
    [Fact] public void Node() => Corpus.AssertFolder("base/data/node");
    [Fact] public void Root() => Corpus.AssertFolder("base/data/root");
    [Fact] public void Sampler() => Corpus.AssertFolder("base/data/sampler");
    [Fact] public void Scene() => Corpus.AssertFolder("base/data/scene");
    [Fact] public void Skin() => Corpus.AssertFolder("base/data/skin");
    [Fact] public void SkinData() => Corpus.AssertFolder("base/data/skin_data");
    [Fact] public void Texture() => Corpus.AssertFolder("base/data/texture");
}

public class ConformanceExtTests
{
    [Fact] public void EXT_texture_webp() => Corpus.AssertFolder("ext/EXT_texture_webp");
    [Fact] public void KHR_animation_pointer() => Corpus.AssertFolder("ext/KHR_animation_pointer");
    [Fact] public void KHR_lights_punctual() => Corpus.AssertFolder("ext/KHR_lights_punctual");
    [Fact] public void KHR_materials_anisotropy() => Corpus.AssertFolder("ext/KHR_materials_anisotropy");
    [Fact] public void KHR_materials_clearcoat() => Corpus.AssertFolder("ext/KHR_materials_clearcoat");
    [Fact] public void KHR_materials_dispersion() => Corpus.AssertFolder("ext/KHR_materials_dispersion");
    [Fact] public void KHR_materials_emissive_strength() => Corpus.AssertFolder("ext/KHR_materials_emissive_strength");
    [Fact] public void KHR_materials_ior() => Corpus.AssertFolder("ext/KHR_materials_ior");
    [Fact] public void KHR_materials_iridescence() => Corpus.AssertFolder("ext/KHR_materials_iridescence");
    [Fact] public void KHR_materials_pbrSpecularGlossiness() => Corpus.AssertFolder("ext/KHR_materials_pbrSpecularGlossiness");
    [Fact] public void KHR_materials_sheen() => Corpus.AssertFolder("ext/KHR_materials_sheen");
    [Fact] public void KHR_materials_specular() => Corpus.AssertFolder("ext/KHR_materials_specular");
    [Fact] public void KHR_materials_transmission() => Corpus.AssertFolder("ext/KHR_materials_transmission");
    [Fact] public void KHR_materials_unlit() => Corpus.AssertFolder("ext/KHR_materials_unlit");
    [Fact] public void KHR_materials_variants() => Corpus.AssertFolder("ext/KHR_materials_variants");
    [Fact] public void KHR_materials_volume() => Corpus.AssertFolder("ext/KHR_materials_volume");
    [Fact] public void KHR_mesh_quantization() => Corpus.AssertFolder("ext/KHR_mesh_quantization");
    [Fact] public void KHR_node_visibility() => Corpus.AssertFolder("ext/KHR_node_visibility");
    [Fact] public void KHR_texture_transform() => Corpus.AssertFolder("ext/KHR_texture_transform");

    [Fact]
    public void Corpus_is_complete()
    {
        int baseCount = Corpus.Cases("base").Count();
        int extCount = Corpus.Cases("ext").Count();
        Assert.True(baseCount >= 260, $"base cases: {baseCount}");
        Assert.True(extCount >= 100, $"ext cases: {extCount}");
    }
}
