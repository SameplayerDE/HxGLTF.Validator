// Port of lib/src/ext/KHR_materials_clearcoat/khr_materials_clearcoat.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsClearcoatExtension
{
    public const string KHR_MATERIALS_CLEARCOAT = "KHR_materials_clearcoat";

    public const string CLEARCOAT_FACTOR = "clearcoatFactor";
    public const string CLEARCOAT_TEXTURE = "clearcoatTexture";
    public const string CLEARCOAT_ROUGHNESS_FACTOR = "clearcoatRoughnessFactor";
    public const string CLEARCOAT_ROUGHNESS_TEXTURE = "clearcoatRoughnessTexture";
    public const string CLEARCOAT_NORMAL_TEXTURE = "clearcoatNormalTexture";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_CLEARCOAT_MEMBERS = new[]
    {
        CLEARCOAT_FACTOR,
        CLEARCOAT_TEXTURE,
        CLEARCOAT_ROUGHNESS_FACTOR,
        CLEARCOAT_ROUGHNESS_TEXTURE,
        CLEARCOAT_NORMAL_TEXTURE,
    };

    public static readonly Extension Value = new(
        KHR_MATERIALS_CLEARCOAT,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsClearcoat.FromMap),
        });
}

internal sealed class KhrMaterialsClearcoat : GltfProperty
{
    public readonly double ClearcoatFactor;
    public readonly TextureInfo? ClearcoatTexture;

    public readonly double ClearcoatRoughnessFactor;
    public readonly TextureInfo? ClearcoatRoughnessTexture;
    public readonly NormalTextureInfo? ClearcoatNormalTexture;

    private KhrMaterialsClearcoat(
        double clearcoatFactor,
        TextureInfo? clearcoatTexture,
        double clearcoatRoughnessFactor,
        TextureInfo? clearcoatRoughnessTexture,
        NormalTextureInfo? clearcoatNormalTexture,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(extensions, extras)
    {
        ClearcoatFactor = clearcoatFactor;
        ClearcoatTexture = clearcoatTexture;
        ClearcoatRoughnessFactor = clearcoatRoughnessFactor;
        ClearcoatRoughnessTexture = clearcoatRoughnessTexture;
        ClearcoatNormalTexture = clearcoatNormalTexture;
    }

    public static KhrMaterialsClearcoat FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsClearcoatExtension.KHR_MATERIALS_CLEARCOAT_MEMBERS, context);
        }

        var clearcoatFactor = JsonUtils.GetFloat(map, KhrMaterialsClearcoatExtension.CLEARCOAT_FACTOR, context, min: 0, max: 1, def: 0);
        var clearcoatTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsClearcoatExtension.CLEARCOAT_TEXTURE, context, TextureInfo.FromMap);
        var clearcoatRoughnessFactor = JsonUtils.GetFloat(
            map, KhrMaterialsClearcoatExtension.CLEARCOAT_ROUGHNESS_FACTOR, context, min: 0, max: 1, def: 0);
        var clearcoatRoughnessTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsClearcoatExtension.CLEARCOAT_ROUGHNESS_TEXTURE, context, TextureInfo.FromMap);
        var clearcoatNormalTexture = JsonUtils.GetObjectFromInnerMap<NormalTextureInfo>(
            map, KhrMaterialsClearcoatExtension.CLEARCOAT_NORMAL_TEXTURE, context, NormalTextureInfo.FromMap);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsClearcoat), context);

        var clearcoat = new KhrMaterialsClearcoat(
            clearcoatFactor,
            clearcoatTexture,
            clearcoatRoughnessFactor,
            clearcoatRoughnessTexture,
            clearcoatNormalTexture,
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(clearcoat, new object?[]
        {
            clearcoatTexture,
            clearcoatRoughnessTexture,
            clearcoatNormalTexture,
        }.Concat(extensions.Values));

        return clearcoat;
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (ClearcoatTexture != null)
        {
            context.Push(KhrMaterialsClearcoatExtension.CLEARCOAT_TEXTURE);
            ClearcoatTexture.Link(gltf, context);
            context.Pop();
        }

        if (ClearcoatRoughnessTexture != null)
        {
            context.Push(KhrMaterialsClearcoatExtension.CLEARCOAT_ROUGHNESS_TEXTURE);
            ClearcoatRoughnessTexture.Link(gltf, context);
            context.Pop();
        }

        if (ClearcoatNormalTexture != null)
        {
            context.Push(KhrMaterialsClearcoatExtension.CLEARCOAT_NORMAL_TEXTURE);
            ClearcoatNormalTexture.Link(gltf, context);

            object? o = this;
            while (o != null)
            {
                o = context.Owners.TryGetValue(o, out var owner) ? owner : null;
                if (o is Material material)
                {
                    var normalTexture = material.NormalTexture;
                    if (normalTexture != null && normalTexture.TexCoord != ClearcoatNormalTexture.TexCoord)
                    {
                        context.AddIssue(SemanticError.KhrMaterialsClearcoatClearcoatNormalTextureTexCoord);
                    }
                    break;
                }
            }
            context.Pop();
        }
    }
}
