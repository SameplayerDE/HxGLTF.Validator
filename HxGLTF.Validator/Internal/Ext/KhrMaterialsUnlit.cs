// Port of lib/src/ext/KHR_materials_unlit/khr_materials_unlit.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsUnlitExtension
{
    public const string KHR_MATERIALS_UNLIT = "KHR_materials_unlit";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_UNLIT_MEMBERS = Array.Empty<string>();

    public static readonly Extension Value = new(
        KHR_MATERIALS_UNLIT,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsUnlit.FromMap, standalone: true),
        });
}

internal sealed class KhrMaterialsUnlit : GltfProperty
{
    private KhrMaterialsUnlit(Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
    }

    public static KhrMaterialsUnlit FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsUnlitExtension.KHR_MATERIALS_UNLIT_MEMBERS, context);
        }

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsUnlit), context);

        return new KhrMaterialsUnlit(extensions, JsonUtils.GetExtras(map, context));
    }
}
