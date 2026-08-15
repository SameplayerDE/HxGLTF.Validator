// Port of lib/src/ext/KHR_materials_emissive_strength/khr_materials_emissive_strength.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsEmissiveStrengthExtension
{
    public const string KHR_MATERIALS_EMISSIVE_STRENGTH = "KHR_materials_emissive_strength";

    public const string EMISSIVE_STRENGTH = "emissiveStrength";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_EMISSIVE_STRENGTH_MEMBERS = new[] { EMISSIVE_STRENGTH };

    public static readonly Extension Value = new(
        KHR_MATERIALS_EMISSIVE_STRENGTH,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsEmissiveStrength.FromMap),
        });
}

internal sealed class KhrMaterialsEmissiveStrength : GltfProperty
{
    public readonly double EmissiveStrength;

    private KhrMaterialsEmissiveStrength(double emissiveStrength, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        EmissiveStrength = emissiveStrength;
    }

    public static KhrMaterialsEmissiveStrength FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsEmissiveStrengthExtension.KHR_MATERIALS_EMISSIVE_STRENGTH_MEMBERS, context);
        }

        var emissiveStrength = JsonUtils.GetFloat(map, KhrMaterialsEmissiveStrengthExtension.EMISSIVE_STRENGTH, context, min: 0, def: 1);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsEmissiveStrength), context);

        return new KhrMaterialsEmissiveStrength(emissiveStrength, extensions, JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (!context.Validate || double.IsNaN(EmissiveStrength) || EmissiveStrength == 1)
        {
            return;
        }
        object? o = this;
        while (o != null)
        {
            o = context.Owners.TryGetValue(o, out var owner) ? owner : null;
            if (o is Material material)
            {
                var factor = material.EmissiveFactor;
                if (factor != null && factor[0] == 0 && factor[1] == 0 && factor[2] == 0)
                {
                    context.AddIssue(SemanticError.KhrMaterialsEmissiveStrengthZeroFactor);
                }
                break;
            }
        }
    }
}
