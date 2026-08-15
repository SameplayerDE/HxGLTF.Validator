// Port of lib/src/ext/KHR_materials_sheen/khr_materials_sheen.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsSheenExtension
{
    public const string KHR_MATERIALS_SHEEN = "KHR_materials_sheen";

    public const string SHEEN_COLOR_FACTOR = "sheenColorFactor";
    public const string SHEEN_COLOR_TEXTURE = "sheenColorTexture";
    public const string SHEEN_ROUGHNESS_FACTOR = "sheenRoughnessFactor";
    public const string SHEEN_ROUGHNESS_TEXTURE = "sheenRoughnessTexture";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_SHEEN_MEMBERS = new[]
    {
        SHEEN_COLOR_FACTOR,
        SHEEN_COLOR_TEXTURE,
        SHEEN_ROUGHNESS_FACTOR,
        SHEEN_ROUGHNESS_TEXTURE,
    };

    public static readonly Extension Value = new(
        KHR_MATERIALS_SHEEN,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsSheen.FromMap),
        });
}

internal sealed class KhrMaterialsSheen : GltfProperty
{
    private static readonly int[] Length3 = { 3 };
    private static readonly double[] DefaultSheenColorFactor = { 0.0, 0.0, 0.0 };

    public readonly double[]? SheenColorFactor;
    public readonly TextureInfo? SheenColorTexture;

    public readonly double SheenRoughnessFactor;
    public readonly TextureInfo? SheenRoughnessTexture;

    private KhrMaterialsSheen(
        double[]? sheenColorFactor,
        TextureInfo? sheenColorTexture,
        double sheenRoughnessFactor,
        TextureInfo? sheenRoughnessTexture,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(extensions, extras)
    {
        SheenColorFactor = sheenColorFactor;
        SheenColorTexture = sheenColorTexture;
        SheenRoughnessFactor = sheenRoughnessFactor;
        SheenRoughnessTexture = sheenRoughnessTexture;
    }

    public static KhrMaterialsSheen FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsSheenExtension.KHR_MATERIALS_SHEEN_MEMBERS, context);
        }

        var sheenColorFactor = JsonUtils.GetFloatList(map, KhrMaterialsSheenExtension.SHEEN_COLOR_FACTOR, context,
            min: 0, max: 1, def: DefaultSheenColorFactor, lengthsList: Length3);
        var sheenColorTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsSheenExtension.SHEEN_COLOR_TEXTURE, context, TextureInfo.FromMap);
        var sheenRoughnessFactor = JsonUtils.GetFloat(map, KhrMaterialsSheenExtension.SHEEN_ROUGHNESS_FACTOR, context, min: 0, max: 1, def: 0);
        var sheenRoughnessTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsSheenExtension.SHEEN_ROUGHNESS_TEXTURE, context, TextureInfo.FromMap);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsSheen), context);

        var sheen = new KhrMaterialsSheen(
            sheenColorFactor,
            sheenColorTexture,
            sheenRoughnessFactor,
            sheenRoughnessTexture,
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(sheen, new object?[] { sheenColorTexture, sheenRoughnessTexture }.Concat(extensions.Values));

        return sheen;
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (SheenColorTexture != null)
        {
            context.Push(KhrMaterialsSheenExtension.SHEEN_COLOR_TEXTURE);
            SheenColorTexture.Link(gltf, context);
            context.Pop();
        }

        if (SheenRoughnessTexture != null)
        {
            context.Push(KhrMaterialsSheenExtension.SHEEN_ROUGHNESS_TEXTURE);
            SheenRoughnessTexture.Link(gltf, context);
            context.Pop();
        }
    }
}
