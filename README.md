# HxGLTF.Validator

Pure C# port of the Khronos glTF-Validator (https://github.com/KhronosGroup/glTF-Validator). It validates glTF 2.0 and
GLB files and produces the same issue codes, the same message texts, the same JSON pointers and the same JSON report
layout as the reference validator. It is verified against the reference validator's own test corpus (372 files with
expected reports, all identical). No external tools or native dependencies.

Targets .NET 8 / .NET 10. MIT (the validation logic and the test corpus derive from the Apache-2.0 licensed
Khronos project, see NOTICE.md).

```bash
dotnet add package H073.HxGLTF.Validator
```

Build from source: `dotnet build` and `dotnet test` in the repository root (the tests run the full Khronos corpus).
The library depends on the `H073.HxGLTF` loader package (https://github.com/SameplayerDE/KaiserLib).

## Getting started

### 1. Validate a file

```csharp
using HxGLTF.Validator;

ValidationReport report = GLTFValidator.Validate("Assets/robot.glb");

Console.WriteLine(report.IsValid); // no errors
Console.WriteLine($"{report.NumErrors} errors, {report.NumWarnings} warnings, {report.NumInfos} infos, {report.NumHints} hints");
foreach (ValidationIssue issue in report.Issues)
    Console.WriteLine($"{issue.Severity} {issue.Code} @ {issue.Pointer ?? issue.Offset?.ToString()}: {issue.Message}");
```

Typical output:

```
Error MESH_PRIMITIVE_UNEQUAL_ACCESSOR_COUNT @ /meshes/0/primitives/0/attributes/NORMAL: All accessors of the same primitive must have the same count.
Warning NODE_SKINNED_MESH_NON_ROOT @ /nodes/3: Node with a skinned mesh is not root. Parent transforms will not affect a skinned mesh.
Information UNUSED_OBJECT @ /materials/2: This object may be unused.
Hint BUFFER_VIEW_TARGET_MISSING @ /bufferViews/1: bufferView.target should be set for vertex or index data.
```

Other inputs:

```csharp
report = GLTFValidator.Validate(bytes, "robot.glb"); // memory; the name selects GLB or JSON, otherwise the first byte is sniffed
report = GLTFValidator.Validate(stream, "robot.gltf");
```

### 2. Write the report

```csharp
File.WriteAllText("robot.report.json", report.ToJson());
```

The JSON is the reference format (same keys in the same order), so it can be fed to any tool that understands
glTF-Validator reports:

```json
{
  "uri": "Assets/robot.glb",
  "mimeType": "model/gltf-binary",
  "validatorVersion": "2.0.0-dev.3.11",
  "issues": {
    "numErrors": 0, "numWarnings": 1, "numInfos": 0, "numHints": 0,
    "messages": [ { "code": "NODE_SKINNED_MESH_NON_ROOT", "message": "...", "severity": 1, "pointer": "/nodes/3" } ],
    "truncated": false
  },
  "info": {
    "version": "2.0", "generator": "...", "extensionsUsed": ["KHR_materials_transmission"],
    "resources": [ { "pointer": "/buffers/0", "mimeType": "application/gltf-buffer", "storage": "glb", "byteLength": 123456 } ],
    "animationCount": 2, "materialCount": 4, "hasMorphTargets": false, "hasSkins": true, "hasTextures": true,
    "hasDefaultScene": true, "drawCallCount": 7, "totalVertexCount": 15321, "totalTriangleCount": 21044,
    "maxUVs": 1, "maxInfluences": 4, "maxAttributes": 5
  }
}
```

`report.Info` gives the same data typed (`Info.Resources` lists every buffer and image with storage kind, byte length
and decoded image header: width, height, format, primaries, transfer, bits).

### 3. Options

```csharp
var options = new ValidationOptions
{
    ValidateResources = true, // load buffers/images, check accessor data and image headers (default true; the reference CLI needs -r)
    MaxIssues = 100, // stop after 100 issues, report.Truncated becomes true (0 = unlimited)
    IgnoredIssues = { "UNUSED_OBJECT", "NODE_EMPTY" },
    OnlyIssues = { }, // when non-empty, only these codes are reported
    SeverityOverrides = { ["IMAGE_NPOT_DIMENSIONS"] = ValidationSeverity.Hint },
    WriteTimestamp = false, // validatedAt in the report
    Uri = null, // report uri, defaults to the given path
    ExternalResourceResolver = null, // see below
};
var report = GLTFValidator.Validate("robot.gltf", options);
```

External resources: by default `.bin` buffers and images are read relative to the validated file. Supply
`ExternalResourceResolver` to load from somewhere else (archive, cache, network). Contract: return the bytes;
throw `FileNotFoundException` for a missing resource (reported as `IO_ERROR`, "Resource not found (uri)."); return
`null` to skip the resource silently (what the reference validator does for non-relative URIs).

### 4. Validate and load in one step (HxGLTF integration)

```csharp
using HxGLTF;
using HxGLTF.Validator;

GLTFFile gltf = GLTFReaderValidation.ReadValidated("robot.glb", out ValidationReport validation);
// gltf is the normal HxGLTF file; gltf.Report now also contains every validator issue as a LoadMessage
foreach (LoadMessage m in gltf.Report.Messages)
    Console.WriteLine($"{m.Severity} {m.Code} {m.Pointer}: {m.Message}");
```

The file bytes are read once and external buffers/images are loaded once and shared between validator and loader
(only the JSON text is tokenized by both). Validation is opt-in because it costs about as much as loading; use it in
asset pipelines, CI and editors, and plain `GLTFReader.Read` at runtime.

Validate an already loaded file later:

```csharp
ValidationReport report = gltf.Validate(); // re-reads gltf.Path, appends the issues to gltf.Report
```

Severity mapping: Error, Warning, Information and Hint map 1:1 to `LoadSeverity.Error/Warning/Info/Hint`.

### 5. Reference validator (optional)

If the official `gltf_validator` executable is installed (releases of the Khronos project), you can run it through the
same API and get the same `ValidationReport` type, for example to cross-check:

```csharp
if (KhronosValidatorRunner.IsAvailable) // found via KhronosValidatorRunner.ExecutablePath, HXGLTF_VALIDATOR or PATH
{
    ValidationReport reference = KhronosValidatorRunner.Validate("robot.glb", options);
    Console.WriteLine(reference.ToJson() == report.ToJson());
}
ValidationReport parsed = KhronosValidatorRunner.ParseReport(File.ReadAllText("some.report.json"));
```

## What is checked

The port implements every issue of the reference validator (170 codes):

| Group | Examples |
|---|---|
| Schema | unexpected/undefined properties, type mismatches, values out of range or not in the allowed list, invalid indices, invalid URIs, one-of and dependency rules |
| Semantic | accessor alignment and normalization, camera parameters, node matrix vs TRS, non-unit rotations, extension declarations, mesh attribute semantics, skin roots and skeletons, alpha cutoff, unused required extensions |
| Link | unresolved references, accessor/bufferView usage conflicts, animation sampler formats and counts, primitive accessor formats and counts, morph target consistency, node loops and multiple parents, scene roots, unused objects |
| Data | buffer lengths, accessor min/max vs actual data, NaN/Inf, index bounds and primitive restart, degenerate triangles, joints/weights, unit normals and tangents, quaternion normalization, animation input monotonicity, sparse indices, inverse bind matrices, image headers (PNG, JPEG, WebP), NPOT dimensions, MIME mismatches |
| GLB | magic, version, length, chunk alignment, chunk order and duplicates, truncated data, unknown chunks |

Extensions validated exactly like the reference: EXT_texture_webp, KHR_animation_pointer, KHR_lights_punctual,
KHR_materials_anisotropy, KHR_materials_clearcoat, KHR_materials_dispersion, KHR_materials_emissive_strength,
KHR_materials_ior, KHR_materials_iridescence, KHR_materials_pbrSpecularGlossiness, KHR_materials_sheen,
KHR_materials_specular, KHR_materials_transmission, KHR_materials_unlit, KHR_materials_variants,
KHR_materials_volume, KHR_mesh_quantization, KHR_node_visibility, KHR_texture_transform. Other extensions
(for example EXT_meshopt_compression, KHR_draco_mesh_compression) are reported as `UNSUPPORTED_EXTENSION`
(information) and their objects are not inspected, which is what the reference validator does too.

The full code list with descriptions is in the reference project's ISSUES.md.

## Differences to the reference validator

- Report messages that embed Dart runtime text (`INVALID_JSON` parser output, `INVALID_URI` format errors) are
  reproduced for the corpus cases; unusual JSON syntax errors may be worded differently.
- The reference CLI validates resources only with `-r`; this library validates them by default (`ValidateResources`).

## License

MIT for the port; the validation rules, message texts and test corpus are derived from the Khronos glTF-Validator
(Apache-2.0), see NOTICE.md.
