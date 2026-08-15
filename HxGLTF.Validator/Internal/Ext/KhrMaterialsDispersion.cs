// Port of lib/src/ext/KHR_materials_dispersion/khr_materials_dispersion.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsDispersionExtension
{
    public const string KHR_MATERIALS_DISPERSION = "KHR_materials_dispersion";

    public const string DISPERSION = "dispersion";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_DISPERSION_MEMBERS = new[] { DISPERSION };

    public static readonly Extension Value = new(
        KHR_MATERIALS_DISPERSION,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsDispersion.FromMap),
        });
}

internal sealed class KhrMaterialsDispersion : GltfProperty
{
    public readonly double Dispersion;

    private KhrMaterialsDispersion(double dispersion, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Dispersion = dispersion;
    }

    public static KhrMaterialsDispersion FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsDispersionExtension.KHR_MATERIALS_DISPERSION_MEMBERS, context);
        }

        var dispersion = JsonUtils.GetFloat(map, KhrMaterialsDispersionExtension.DISPERSION, context, min: 0, def: 0);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsDispersion), context);

        return new KhrMaterialsDispersion(dispersion, extensions, JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
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
                // The dispersion extension needs to be combined
                // with KHR_materials_volume.
                if (!material.Extensions.ContainsKey(KhrMaterialsVolumeExtension.KHR_MATERIALS_VOLUME))
                {
                    context.AddIssue(SemanticError.KhrMaterialsDispersionNoVolume);
                }
                break;
            }
        }
    }
}
