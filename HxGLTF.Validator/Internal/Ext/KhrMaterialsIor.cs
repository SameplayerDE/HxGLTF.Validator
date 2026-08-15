// Port of lib/src/ext/KHR_materials_ior/khr_materials_ior.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsIorExtension
{
    public const string KHR_MATERIALS_IOR = "KHR_materials_ior";

    public const string IOR = "ior";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_IOR_MEMBERS = new[] { IOR };

    public static readonly Extension Value = new(
        KHR_MATERIALS_IOR,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsIor.FromMap),
        });
}

internal sealed class KhrMaterialsIor : GltfProperty
{
    public readonly double Ior;

    private KhrMaterialsIor(double ior, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Ior = ior;
    }

    public static KhrMaterialsIor FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsIorExtension.KHR_MATERIALS_IOR_MEMBERS, context);
        }

        var ior = JsonUtils.GetFloat(map, KhrMaterialsIorExtension.IOR, context, min: 1, def: 1.5, standalone: 0);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsIor), context);

        return new KhrMaterialsIor(ior, extensions, JsonUtils.GetExtras(map, context));
    }
}
