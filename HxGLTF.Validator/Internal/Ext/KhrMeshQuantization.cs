// Port of lib/src/ext/KHR_mesh_quantization/khr_mesh_quantization.dart

namespace HxGLTF.Validator.Internal;

internal static class KhrMeshQuantizationExtension
{
    public const string KHR_MESH_QUANTIZATION = "KHR_mesh_quantization";

    private static void Init(Context context)
    {
        context.AttributeAccessorFormats[Members.POSITION].UnionWith(new[]
        {
            new AccessorFormat(Members.VEC3, Gl.BYTE),
            new AccessorFormat(Members.VEC3, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.UNSIGNED_BYTE),
            new AccessorFormat(Members.VEC3, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.SHORT),
            new AccessorFormat(Members.VEC3, Gl.SHORT, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.UNSIGNED_SHORT),
            new AccessorFormat(Members.VEC3, Gl.UNSIGNED_SHORT, normalized: true),
        });

        context.AttributeAccessorFormats[Members.NORMAL].UnionWith(new[]
        {
            new AccessorFormat(Members.VEC3, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.SHORT, normalized: true),
        });

        context.AttributeAccessorFormats[Members.TANGENT].UnionWith(new[]
        {
            new AccessorFormat(Members.VEC4, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC4, Gl.SHORT, normalized: true),
        });

        context.AttributeAccessorFormats[Members.TEXCOORD_].UnionWith(new[]
        {
            new AccessorFormat(Members.VEC2, Gl.BYTE),
            new AccessorFormat(Members.VEC2, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC2, Gl.UNSIGNED_BYTE),
            new AccessorFormat(Members.VEC2, Gl.SHORT),
            new AccessorFormat(Members.VEC2, Gl.SHORT, normalized: true),
            new AccessorFormat(Members.VEC2, Gl.UNSIGNED_SHORT),
        });

        context.MorphAttributeAccessorFormats[Members.POSITION].UnionWith(new[]
        {
            new AccessorFormat(Members.VEC3, Gl.BYTE),
            new AccessorFormat(Members.VEC3, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.SHORT),
            new AccessorFormat(Members.VEC3, Gl.SHORT, normalized: true),
        });

        context.MorphAttributeAccessorFormats[Members.NORMAL].UnionWith(new[]
        {
            new AccessorFormat(Members.VEC3, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.SHORT, normalized: true),
        });

        context.MorphAttributeAccessorFormats[Members.TANGENT].UnionWith(new[]
        {
            new AccessorFormat(Members.VEC3, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.SHORT, normalized: true),
        });

        context.MorphAttributeAccessorFormats[Members.TEXCOORD_].UnionWith(new[]
        {
            new AccessorFormat(Members.VEC2, Gl.BYTE),
            new AccessorFormat(Members.VEC2, Gl.SHORT),
        });
    }

    public static readonly Extension Value = new(
        KHR_MESH_QUANTIZATION,
        new Dictionary<Type, ExtensionDescriptor>(),
        init: Init,
        required: true);
}
