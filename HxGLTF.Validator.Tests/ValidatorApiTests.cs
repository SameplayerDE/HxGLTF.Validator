using System.Text;
using System.Text.Json.Nodes;
using HxGLTF;

namespace HxGLTF.Validator.Tests;

public class ValidatorApiTests
{
    private static string Corpus(string relative) => Path.Combine(AppContext.BaseDirectory, "Data", "khronos", relative.Replace('/', Path.DirectorySeparatorChar));

    private static readonly string ValidGltf = """{"asset":{"version":"2.0"}}""";

    [Fact]
    public void Minimal_document_is_valid_and_report_json_has_reference_layout()
    {
        var report = GLTFValidator.Validate(Encoding.UTF8.GetBytes(ValidGltf), "a.gltf", new ValidationOptions { Uri = "a.gltf" });
        Assert.True(report.IsValid);
        Assert.Equal("model/gltf+json", report.MimeType);
        var json = JsonNode.Parse(report.ToJson())!.AsObject();
        Assert.Equal(new[] { "uri", "mimeType", "validatorVersion", "issues", "info" }, json.Select(p => p.Key).ToArray());
        Assert.Equal(new[] { "numErrors", "numWarnings", "numInfos", "numHints", "messages", "truncated" }, json["issues"]!.AsObject().Select(p => p.Key).ToArray());
        Assert.Equal("2.0", (string)json["info"]!["version"]!);
        Assert.Null(json["validatedAt"]);
    }

    [Fact]
    public void Timestamp_is_written_when_requested()
    {
        var report = GLTFValidator.Validate(Encoding.UTF8.GetBytes(ValidGltf), "a.gltf", new ValidationOptions { WriteTimestamp = true });
        Assert.NotNull(report.ValidatedAt);
        Assert.Contains("\"validatedAt\"", report.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void MaxIssues_truncates_the_report()
    {
        var file = Corpus("base/data/mesh/invalid_attribute.gltf"); // 2 errors + 1 info in the reference report
        var report = GLTFValidator.Validate(file, new ValidationOptions { MaxIssues = 1 });
        Assert.True(report.Truncated);
        Assert.Single(report.Issues);
        Assert.Contains("\"truncated\": true", report.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Ignored_only_and_severity_overrides()
    {
        var file = Corpus("base/data/mesh/invalid_attribute.gltf");
        var full = GLTFValidator.Validate(file);
        Assert.Contains(full.Issues, i => i.Code == "MESH_PRIMITIVE_INVALID_ATTRIBUTE");
        Assert.Contains(full.Issues, i => i.Code == "UNUSED_OBJECT");

        var ignored = GLTFValidator.Validate(file, new ValidationOptions { IgnoredIssues = { "UNUSED_OBJECT" } });
        Assert.DoesNotContain(ignored.Issues, i => i.Code == "UNUSED_OBJECT");

        var only = GLTFValidator.Validate(file, new ValidationOptions { OnlyIssues = { "UNUSED_OBJECT" } });
        Assert.All(only.Issues, i => Assert.Equal("UNUSED_OBJECT", i.Code));

        var overridden = GLTFValidator.Validate(file, new ValidationOptions { SeverityOverrides = { ["MESH_PRIMITIVE_INVALID_ATTRIBUTE"] = ValidationSeverity.Hint } });
        Assert.All(overridden.Issues.Where(i => i.Code == "MESH_PRIMITIVE_INVALID_ATTRIBUTE"), i => Assert.Equal(ValidationSeverity.Hint, i.Severity));
        Assert.True(overridden.IsValid);
        Assert.Equal(2, overridden.NumHints);
    }

    [Fact]
    public void ValidateResources_false_skips_data_checks_and_resources()
    {
        var file = Corpus("base/data/accessor_data/out_of_range_elements_float.gltf");
        var full = GLTFValidator.Validate(file);
        Assert.Contains(full.Issues, i => i.Code.StartsWith("ACCESSOR_", StringComparison.Ordinal));
        var structural = GLTFValidator.Validate(file, new ValidationOptions { ValidateResources = false });
        Assert.DoesNotContain(structural.Issues, i => i.Code == "ACCESSOR_MIN_MISMATCH" || i.Code == "ACCESSOR_MAX_MISMATCH");
        Assert.Empty(structural.Info!.Resources);
        Assert.NotEmpty(full.Info!.Resources);
    }

    [Fact]
    public void Missing_external_resource_is_an_io_error_and_null_resolver_result_skips()
    {
        var gltf = """{"asset":{"version":"2.0"},"buffers":[{"byteLength":4,"uri":"missing.bin"}]}""";
        var report = GLTFValidator.Validate(Encoding.UTF8.GetBytes(gltf), "a.gltf", new ValidationOptions
        {
            ExternalResourceResolver = uri => throw new FileNotFoundException("x", uri),
        });
        var io = Assert.Single(report.Issues, i => i.Code == "IO_ERROR");
        Assert.Equal("Resource not found (missing.bin).", io.Message);
        Assert.Equal("/buffers/0/uri", io.Pointer);

        var skipped = GLTFValidator.Validate(Encoding.UTF8.GetBytes(gltf), "a.gltf", new ValidationOptions { ExternalResourceResolver = _ => null });
        Assert.DoesNotContain(skipped.Issues, i => i.Code == "IO_ERROR");
    }

    [Fact]
    public void Glb_from_memory_is_detected_by_magic()
    {
        var glb = File.ReadAllBytes(Corpus("base/data/glb/valid.glb"));
        var report = GLTFValidator.Validate(glb);
        Assert.Equal("model/gltf-binary", report.MimeType);
    }

    [Fact]
    public void ToLoadMessages_maps_severities()
    {
        var file = Corpus("base/data/mesh/invalid_attribute.gltf");
        var report = GLTFValidator.Validate(file);
        var messages = report.ToLoadMessages().ToList();
        Assert.Equal(report.Issues.Count, messages.Count);
        Assert.Contains(messages, m => m.Severity == LoadSeverity.Error && m.Code == "MESH_PRIMITIVE_INVALID_ATTRIBUTE");
        Assert.Contains(messages, m => m.Severity == LoadSeverity.Info && m.Code == "UNUSED_OBJECT");
    }

    [Fact]
    public void ReadValidated_loads_and_merges_report_reading_resources_once()
    {
        var file = Corpus("base/data/mesh/invalid_attribute.gltf");
        var gltf = GLTFReaderValidation.ReadValidated(file, out var validation, new GLTFReadOptions { ValidateImageFiles = false });
        Assert.NotNull(gltf.Meshes);
        Assert.Contains(gltf.Report.Messages, m => m.Code == "MESH_PRIMITIVE_INVALID_ATTRIBUTE");
        Assert.Equal(validation.Issues.Count, gltf.Report.Messages.Count(m => validation.Issues.Any(i => i.Code == m.Code && i.Message == m.Message)));
    }

    [Fact]
    public void GLTFFile_Validate_extension_appends_to_report()
    {
        var file = Corpus("base/data/mesh/invalid_attribute.gltf");
        var gltf = GLTFReader.Read(file, new GLTFReadOptions { ValidateImageFiles = false });
        int before = gltf.Report.Messages.Count;
        var report = gltf.Validate();
        Assert.Equal(before + report.Issues.Count, gltf.Report.Messages.Count);
    }

    [Fact]
    public void ParseReport_round_trips_ToJson()
    {
        var file = Corpus("base/data/mesh/invalid_attribute.gltf");
        var report = GLTFValidator.Validate(file, new ValidationOptions { Uri = "x.gltf" });
        var parsed = KhronosValidatorRunner.ParseReport(report.ToJson());
        Assert.Equal(report.ToJson(), parsed.ToJson());
    }

    [Fact]
    public void Khronos_runner_cross_check_when_available()
    {
        if (!KhronosValidatorRunner.IsAvailable) return; // reference binary not installed
        var file = Corpus("base/data/mesh/invalid_attribute.gltf");
        var reference = KhronosValidatorRunner.Validate(file, new ValidationOptions { Uri = "x" });
        var ours = GLTFValidator.Validate(file, new ValidationOptions { Uri = "x" });
        var a = JsonNode.Parse(reference.ToJson())!.AsObject();
        var b = JsonNode.Parse(ours.ToJson())!.AsObject();
        a.Remove("validatorVersion"); b.Remove("validatorVersion");
        a.Remove("uri"); b.Remove("uri");
        Assert.True(JsonNode.DeepEquals(a, b), "reference: " + a.ToJsonString() + " ours: " + b.ToJsonString());
    }
}
