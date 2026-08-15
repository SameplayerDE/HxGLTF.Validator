// Port of lib/src/ext/KHR_materials_specular/khr_materials_specular.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsSpecularExtension
{
    public const string KHR_MATERIALS_SPECULAR = "KHR_materials_specular";

    public const string SPECULAR_FACTOR = "specularFactor";
    public const string SPECULAR_TEXTURE = "specularTexture";
    public const string SPECULAR_COLOR_FACTOR = "specularColorFactor";
    public const string SPECULAR_COLOR_TEXTURE = "specularColorTexture";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_SPECULAR_MEMBERS = new[]
    {
        SPECULAR_FACTOR,
        SPECULAR_TEXTURE,
        SPECULAR_COLOR_FACTOR,
        SPECULAR_COLOR_TEXTURE,
    };

    public static readonly Extension Value = new(
        KHR_MATERIALS_SPECULAR,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsSpecular.FromMap),
        });
}

internal sealed class KhrMaterialsSpecular : GltfProperty
{
    private static readonly int[] Length3 = { 3 };
    private static readonly double[] DefaultSpecularColorFactor = { 1.0, 1.0, 1.0 };

    public readonly double SpecularFactor;
    public readonly TextureInfo? SpecularTexture;

    public readonly double[]? SpecularColorFactor;
    public readonly TextureInfo? SpecularColorTexture;

    private KhrMaterialsSpecular(
        double specularFactor,
        TextureInfo? specularTexture,
        double[]? specularColorFactor,
        TextureInfo? specularColorTexture,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(extensions, extras)
    {
        SpecularFactor = specularFactor;
        SpecularTexture = specularTexture;
        SpecularColorFactor = specularColorFactor;
        SpecularColorTexture = specularColorTexture;
    }

    public static KhrMaterialsSpecular FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsSpecularExtension.KHR_MATERIALS_SPECULAR_MEMBERS, context);
        }

        var specularFactor = JsonUtils.GetFloat(map, KhrMaterialsSpecularExtension.SPECULAR_FACTOR, context, min: 0, max: 1, def: 1);
        var specularTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsSpecularExtension.SPECULAR_TEXTURE, context, TextureInfo.FromMap);

        var specularColorFactor = JsonUtils.GetFloatList(map, KhrMaterialsSpecularExtension.SPECULAR_COLOR_FACTOR, context,
            lengthsList: Length3, min: 0, def: DefaultSpecularColorFactor);
        var specularColorTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsSpecularExtension.SPECULAR_COLOR_TEXTURE, context, TextureInfo.FromMap);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsSpecular), context);

        var specular = new KhrMaterialsSpecular(
            specularFactor,
            specularTexture,
            specularColorFactor,
            specularColorTexture,
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(specular, new object?[] { specularTexture, specularColorTexture }.Concat(extensions.Values));

        return specular;
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (SpecularTexture != null)
        {
            context.Push(KhrMaterialsSpecularExtension.SPECULAR_TEXTURE);
            SpecularTexture.Link(gltf, context);
            context.Pop();
        }

        if (SpecularColorTexture != null)
        {
            context.Push(KhrMaterialsSpecularExtension.SPECULAR_COLOR_TEXTURE);
            SpecularColorTexture.Link(gltf, context);
            context.Pop();
        }
    }
}
