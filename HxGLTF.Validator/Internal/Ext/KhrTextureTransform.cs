// Port of lib/src/ext/KHR_texture_transform/khr_texture_transform.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrTextureTransformExtension
{
    // EXT_texture_transform
    public const string KHR_TEXTURE_TRANSFORM = "KHR_texture_transform";
    public const string OFFSET = "offset";

    public static readonly IReadOnlyList<string> KHR_TEXTURE_TRANSFORM_MEMBERS = new[]
    {
        OFFSET,
        Members.ROTATION,
        Members.SCALE,
        Members.TEX_COORD,
    };

    public static readonly Extension Value = new(
        KHR_TEXTURE_TRANSFORM,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(TextureInfo)] = new ExtensionDescriptor(KhrTextureTransform.FromMap),
            [typeof(NormalTextureInfo)] = new ExtensionDescriptor(KhrTextureTransform.FromMap),
            [typeof(OcclusionTextureInfo)] = new ExtensionDescriptor(KhrTextureTransform.FromMap),
        });
}

internal sealed class KhrTextureTransform : GltfProperty
{
    private static readonly int[] Length2 = { 2 };
    private static readonly double[] DefaultOffset = { 0.0, 0.0 };
    private static readonly double[] DefaultScale = { 1.0, 1.0 };

    public readonly double[]? Offset;
    public readonly double Rotation;
    public readonly double[]? Scale;
    public readonly int TexCoord;

    private KhrTextureTransform(double[]? offset, double rotation, double[]? scale, int texCoord,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Offset = offset;
        Rotation = rotation;
        Scale = scale;
        TexCoord = texCoord;
    }

    public static KhrTextureTransform FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrTextureTransformExtension.KHR_TEXTURE_TRANSFORM_MEMBERS, context);
        }

        return new KhrTextureTransform(
            JsonUtils.GetFloatList(map, KhrTextureTransformExtension.OFFSET, context,
                def: DefaultOffset, lengthsList: Length2),
            JsonUtils.GetFloat(map, Members.ROTATION, context, def: 0),
            JsonUtils.GetFloatList(map, Members.SCALE, context,
                def: DefaultScale, lengthsList: Length2),
            JsonUtils.GetUint(map, Members.TEX_COORD, context),
            JsonUtils.GetExtensions(map, typeof(KhrTextureTransform), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        object? o = this;
        while (o != null)
        {
            o = context.Owners.TryGetValue(o, out var owner) ? owner : null;
            if (o is Material material)
            {
                material.TexCoordIndices[context.GetPointerString()] = TexCoord;
                break;
            }
        }
    }
}
