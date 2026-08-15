using System.Reflection;
using HxGLTF.Validator.Internal;

namespace HxGLTF.Validator.Tests;

public class IssuesTests
{
    private static readonly Type[] IssueClasses =
    {
        typeof(DataError), typeof(IoError), typeof(SchemaError),
        typeof(SemanticError), typeof(LinkError), typeof(GlbError),
    };

    private static int CountFields(Type t)
        => t.GetFields(BindingFlags.Public | BindingFlags.Static).Count(f => f.FieldType == typeof(IssueType));

    private static List<IssueType> AllIssueTypes()
    {
        var list = new List<IssueType>();
        foreach (var t in IssueClasses)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType == typeof(IssueType))
                    list.Add((IssueType)f.GetValue(null)!);
            }
        }
        return list;
    }

    [Fact]
    public void Has_All_Issue_Types_From_Errors_Dart()
    {
        // errors.dart declares 170 issue types: 33 DataError, 1 IoError, 16 SchemaError,
        // 53 SemanticError, 51 LinkError, 16 GlbError.
        var all = AllIssueTypes();
        Assert.Equal(170, all.Count);
        Assert.Equal(33, CountFields(typeof(DataError)));
        Assert.Equal(1, CountFields(typeof(IoError)));
        Assert.Equal(16, CountFields(typeof(SchemaError)));
        Assert.Equal(53, CountFields(typeof(SemanticError)));
        Assert.Equal(51, CountFields(typeof(LinkError)));
        Assert.Equal(16, CountFields(typeof(GlbError)));
    }

    [Fact]
    public void All_Codes_Are_Unique_And_Have_Messages()
    {
        var all = AllIssueTypes();
        var codes = all.Select(i => i.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, i => Assert.NotNull(i.Message));
        Assert.All(codes, c => Assert.Matches("^[A-Z][A-Z0-9_]*$", c));
    }

    [Fact]
    public void Default_Severities()
    {
        Assert.Equal(ValidationSeverity.Error, LinkError.AccessorTooLong.Severity);
        Assert.Equal(ValidationSeverity.Warning, SchemaError.ValueNotInList.Severity);
        Assert.Equal(ValidationSeverity.Warning, SchemaError.UnexpectedProperty.Severity);
        Assert.Equal(ValidationSeverity.Information, LinkError.UnusedObject.Severity);
        Assert.Equal(ValidationSeverity.Hint, LinkError.BufferViewTargetMissing.Severity);
        Assert.Equal(ValidationSeverity.Warning, GlbError.UnknownChunkType.Severity);
        Assert.Equal(ValidationSeverity.Information, DataError.AccessorIndexTriangleDegenerate.Severity);
    }

    private static string Render(IssueType type, params object?[] args)
        => new Issue(type, args, pointer: "").Message;

    [Fact]
    public void Messages_Match_Reference_Corpus()
    {
        // Strings taken from the Khronos corpus *.report.json files.
        Assert.Equal(
            "Accessor (offset: 0, length: 60) does not fit referenced bufferView [0] length 16.",
            Render(LinkError.AccessorTooLong, 0, 60, 0, 16));

        Assert.Equal(
            "Invalid value 'NotaVEC3'. Valid values are ('SCALAR', 'VEC2', 'VEC3', 'VEC4', 'MAT2', 'MAT3', 'MAT4').",
            Render(SchemaError.ValueNotInList, "NotaVEC3", DartFormat.Iter(Members.ACCESSOR_TYPES_LENGTHS.Keys)));

        Assert.Equal(
            "Type mismatch. Property value 'material' is not a 'object'.",
            Render(SchemaError.TypeMismatch, "material", "object"));
        Assert.Equal(
            "Type mismatch. Property value 0 is not a 'object'.",
            Render(SchemaError.TypeMismatch, 0L, "object"));
        Assert.Equal(
            "Type mismatch. Array element [] is not a 'object'.",
            Render(SchemaError.ArrayTypeMismatch, Array.Empty<object>(), "object"));

        Assert.Equal(
            "Invalid accessor format '{VEC4, FLOAT}' for this attribute semantic. Must be one of ('{VEC3, FLOAT}').",
            Render(LinkError.MeshPrimitiveAttributesAccessorInvalidFormat,
                new AccessorFormat(Members.VEC4, Gl.FLOAT),
                new[] { new AccessorFormat(Members.VEC3, Gl.FLOAT) }));

        Assert.Equal(
            "Exactly one of ('orthographic', 'perspective') properties must be defined.",
            Render(SchemaError.OneOfMismatch, "orthographic", "perspective"));

        Assert.Equal(
            "The length of weights array (2) does not match the number of morph targets (1).",
            Render(LinkError.NodeWeightsInvalid, 2, 1));
        Assert.Equal(
            "The length of weights array (2) does not match the number of morph targets (0).",
            Render(LinkError.NodeWeightsInvalid, 2, null));

        Assert.Equal(
            "Declared maximum value for this component (1.0) does not match actual maximum (2.0).",
            Render(DataError.AccessorMaxMismatch, 1.0, 2.0));
        Assert.Equal(
            "Declared maximum value for this component (1) does not match actual maximum (2).",
            Render(DataError.AccessorMaxMismatch, 1L, 2L));

        Assert.Equal(
            "Vector3 at accessor indices 6..8 is not of unit length: 1.7320508075688772.",
            Render(DataError.AccessorVector3NonUnit, 6, 8, Math.Sqrt(3.0)));

        Assert.Equal(
            "Unknown GLB chunk type: 0x004b4e55.",
            Render(GlbError.UnknownChunkType, DartFormat.Hex8(0x004b4e55)));
        Assert.Equal("Invalid GLB magic value (0).", Render(GlbError.InvalidMagic, 0));

        Assert.Equal(
            "Override of previously set bufferView target or usage. Initial: 'IndexBuffer', new: 'VertexBuffer'.",
            Render(LinkError.BufferViewTargetOverride, BufferViewUsage.IndexBuffer, BufferViewUsage.VertexBuffer));

        // Trailing space in the Dart template is trimmed by Issue.message.
        Assert.Equal(
            "Invalid indices accessor format '{VEC2, FLOAT}'. Must be one of ('{SCALAR, UNSIGNED_BYTE}', '{SCALAR, UNSIGNED_SHORT}', '{SCALAR, UNSIGNED_INT}').",
            Render(LinkError.MeshPrimitiveIndicesAccessorInvalidFormat,
                new AccessorFormat(Members.VEC2, Gl.FLOAT), Members.MESH_PRIMITIVE_INDICES_FORMATS));

        Assert.Equal("some io text", Render(IoError.IoErrorIssue, "some io text"));
    }

    [Fact]
    public void AccessorFormat_Value_Semantics()
    {
        var a = new AccessorFormat(Members.VEC2, Gl.UNSIGNED_BYTE, normalized: true);
        var b = new AccessorFormat(Members.VEC2, Gl.UNSIGNED_BYTE, normalized: true);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new AccessorFormat(Members.VEC2, Gl.UNSIGNED_BYTE));
        Assert.Equal("{VEC2, UNSIGNED_BYTE normalized}", a.ToString());
        Assert.Equal("{VEC3, FLOAT}", new AccessorFormat(Members.VEC3, Gl.FLOAT).ToString());
        Assert.Equal("{VEC3, null}", new AccessorFormat(Members.VEC3, 1234).ToString());
        Assert.Contains(a, new HashSet<AccessorFormat> { b });
    }

    [Fact]
    public void Gl_Helpers()
    {
        Assert.Equal(1, Gl.ComponentTypeLength(Gl.BYTE));
        Assert.Equal(2, Gl.ComponentTypeLength(Gl.UNSIGNED_SHORT));
        Assert.Equal(4, Gl.ComponentTypeLength(Gl.FLOAT));
        Assert.Equal(-1, Gl.ComponentTypeLength(0));
        Assert.Equal(-128L, Gl.TypeMin(Gl.BYTE));
        Assert.Equal(0L, Gl.TypeMin(Gl.UNSIGNED_INT));
        Assert.Equal(4294967295L, Gl.TypeMax(Gl.UNSIGNED_INT));
        Assert.Equal(2147483647L, Gl.TypeMax(Gl.INT));
        Assert.Throws<ArgumentException>(() => Gl.TypeMin(Gl.FLOAT));
        Assert.Equal("FLOAT", Gl.TypeName(Gl.FLOAT));
        Assert.Null(Gl.TypeName(42));
        Assert.Equal("TRIANGLES", Gl.MODES_NAMES[Gl.TRIANGLES]);
        Assert.Equal(new[] { Gl.ARRAY_BUFFER, Gl.ELEMENT_ARRAY_BUFFER }, Gl.TARGETS);
    }

    [Fact]
    public void Members_Constants()
    {
        Assert.Equal(new[] { "translation", "rotation", "scale", "weights" }, Members.ANIMATION_CHANNEL_TARGET_PATHS);
        Assert.Equal(16, Members.ACCESSOR_TYPES_LENGTHS[Members.MAT4]);
        Assert.Equal("TEXCOORD", Members.TEXCOORD_);
        Assert.Equal(17, Members.GLTF_MEMBERS.Length);
        Assert.Equal(0.00674, IssueConstants.UnitLengthThresholdVec3);
        Assert.Equal("IBM", AccessorUsage.IBM.ToString());
        Assert.Equal(Gl.ARRAY_BUFFER, BufferViewUsage.VertexBuffer.Target);
        Assert.Equal(-1, BufferViewUsage.Other.Target);
    }
}
