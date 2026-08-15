// Port of lib/src/ext/KHR_materials_volume/khr_materials_volume.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsVolumeExtension
{
    public const string KHR_MATERIALS_VOLUME = "KHR_materials_volume";

    public const string ATTENUATION_COLOR = "attenuationColor";
    public const string ATTENUATION_DISTANCE = "attenuationDistance";
    public const string THICKNESS_FACTOR = "thicknessFactor";
    public const string THICKNESS_TEXTURE = "thicknessTexture";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_VOLUME_MEMBERS = new[]
    {
        ATTENUATION_COLOR,
        ATTENUATION_DISTANCE,
        THICKNESS_FACTOR,
        THICKNESS_TEXTURE,
    };

    public static readonly Extension Value = new(
        KHR_MATERIALS_VOLUME,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsVolume.FromMap),
        });
}

internal sealed class KhrMaterialsVolume : GltfProperty
{
    private static readonly int[] Length3 = { 3 };
    private static readonly double[] DefaultAttenuationColor = { 1.0, 1.0, 1.0 };

    public readonly double[]? AttenuationColor;
    public readonly double AttenuationDistance;
    public readonly double ThicknessFactor;
    public readonly TextureInfo? ThicknessTexture;

    private KhrMaterialsVolume(
        double[]? attenuationColor,
        double attenuationDistance,
        double thicknessFactor,
        TextureInfo? thicknessTexture,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(extensions, extras)
    {
        AttenuationColor = attenuationColor;
        AttenuationDistance = attenuationDistance;
        ThicknessFactor = thicknessFactor;
        ThicknessTexture = thicknessTexture;
    }

    public static KhrMaterialsVolume FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsVolumeExtension.KHR_MATERIALS_VOLUME_MEMBERS, context);
        }

        var attenuationColor = JsonUtils.GetFloatList(map, KhrMaterialsVolumeExtension.ATTENUATION_COLOR, context,
            lengthsList: Length3, min: 0, max: 1, def: DefaultAttenuationColor);

        var attenuationDistance = JsonUtils.GetFloat(map, KhrMaterialsVolumeExtension.ATTENUATION_DISTANCE, context, exclMin: 0);

        var thicknessFactor = JsonUtils.GetFloat(map, KhrMaterialsVolumeExtension.THICKNESS_FACTOR, context, min: 0, def: 0);

        var thicknessTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsVolumeExtension.THICKNESS_TEXTURE, context, TextureInfo.FromMap);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsVolume), context);

        var volume = new KhrMaterialsVolume(attenuationColor, attenuationDistance, thicknessFactor, thicknessTexture,
            extensions, JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(volume, new object?[] { thicknessTexture }.Concat(extensions.Values));

        return volume;
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (ThicknessTexture != null)
        {
            context.Push(KhrMaterialsVolumeExtension.THICKNESS_TEXTURE);
            ThicknessTexture.Link(gltf, context);
            context.Pop();
        }

        if (!context.Validate)
        {
            return;
        }

        object? o = this;
        while (o != null)
        {
            o = context.Owners.TryGetValue(o, out var owner) ? owner : null;
            if (o is Material material)
            {
                // The volume extension needs to be combined with an extension
                // that allows light to transmit through the surface.
                // Also suppress the warning when an unknown extension is present.
                // Dart: `e is Map` (unknown extensions are kept as raw maps; here they are JsonElement values).
                if (!material.Extensions.ContainsKey(KhrMaterialsTransmissionExtension.KHR_MATERIALS_TRANSMISSION) &&
                    !material.Extensions.Values.Any(e => e is JsonElement))
                {
                    context.AddIssue(SemanticError.KhrMaterialsVolumeNoTransmission);
                }

                if (material.DoubleSided && ThicknessFactor > 0)
                {
                    context.AddIssue(SemanticError.KhrMaterialsVolumeDoubleSided);
                }
                break;
            }
        }
    }
}
