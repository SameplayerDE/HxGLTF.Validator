// Port of lib/src/ext/KHR_materials_iridescence/khr_materials_iridescence.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsIridescenceExtension
{
    public const string KHR_MATERIALS_IRIDESCENCE = "KHR_materials_iridescence";

    public const string IRIDESCENCE_FACTOR = "iridescenceFactor";
    public const string IRIDESCENCE_TEXTURE = "iridescenceTexture";
    public const string IRIDESCENCE_IOR = "iridescenceIor";
    public const string IRIDESCENCE_THICKNESS_MINIMUM = "iridescenceThicknessMinimum";
    public const string IRIDESCENCE_THICKNESS_MAXIMUM = "iridescenceThicknessMaximum";
    public const string IRIDESCENCE_THICKNESS_TEXTURE = "iridescenceThicknessTexture";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_IRIDESCENCE_MEMBERS = new[]
    {
        IRIDESCENCE_FACTOR,
        IRIDESCENCE_TEXTURE,
        IRIDESCENCE_IOR,
        IRIDESCENCE_THICKNESS_MINIMUM,
        IRIDESCENCE_THICKNESS_MAXIMUM,
        IRIDESCENCE_THICKNESS_TEXTURE,
    };

    public static readonly Extension Value = new(
        KHR_MATERIALS_IRIDESCENCE,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsIridescence.FromMap),
        });
}

internal sealed class KhrMaterialsIridescence : GltfProperty
{
    public readonly double IridescenceFactor;
    public readonly TextureInfo? IridescenceTexture;

    public readonly double IridescenceIor;
    public readonly double IridescenceThicknessMinimum;
    public readonly double IridescenceThicknessMaximum;
    public readonly TextureInfo? IridescenceThicknessTexture;

    private KhrMaterialsIridescence(
        double iridescenceFactor,
        TextureInfo? iridescenceTexture,
        double iridescenceIor,
        double iridescenceThicknessMinimum,
        double iridescenceThicknessMaximum,
        TextureInfo? iridescenceThicknessTexture,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(extensions, extras)
    {
        IridescenceFactor = iridescenceFactor;
        IridescenceTexture = iridescenceTexture;
        IridescenceIor = iridescenceIor;
        IridescenceThicknessMinimum = iridescenceThicknessMinimum;
        IridescenceThicknessMaximum = iridescenceThicknessMaximum;
        IridescenceThicknessTexture = iridescenceThicknessTexture;
    }

    public static KhrMaterialsIridescence FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsIridescenceExtension.KHR_MATERIALS_IRIDESCENCE_MEMBERS, context);
        }

        var iridescenceFactor = JsonUtils.GetFloat(map, KhrMaterialsIridescenceExtension.IRIDESCENCE_FACTOR, context, min: 0, max: 1, def: 0);
        var iridescenceTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsIridescenceExtension.IRIDESCENCE_TEXTURE, context, TextureInfo.FromMap);
        var iridescenceIor = JsonUtils.GetFloat(map, KhrMaterialsIridescenceExtension.IRIDESCENCE_IOR, context, min: 1, def: 1.3);
        var iridescenceThicknessMinimum = JsonUtils.GetFloat(map, KhrMaterialsIridescenceExtension.IRIDESCENCE_THICKNESS_MINIMUM, context, min: 0, def: 100);
        var iridescenceThicknessMaximum = JsonUtils.GetFloat(map, KhrMaterialsIridescenceExtension.IRIDESCENCE_THICKNESS_MAXIMUM, context, min: 0, def: 400);
        var iridescenceThicknessTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsIridescenceExtension.IRIDESCENCE_THICKNESS_TEXTURE, context, TextureInfo.FromMap);

        if (context.Validate)
        {
            if (iridescenceThicknessTexture != null)
            {
                if (iridescenceThicknessMinimum == iridescenceThicknessMaximum)
                {
                    context.AddIssue(SemanticError.KhrMaterialsIridescenceThicknessTextureUnused,
                        name: KhrMaterialsIridescenceExtension.IRIDESCENCE_THICKNESS_TEXTURE);
                }
            }
            else
            {
                if (!double.IsNaN(iridescenceThicknessMinimum) &&
                    JsonUtils.Has(map, KhrMaterialsIridescenceExtension.IRIDESCENCE_THICKNESS_MINIMUM))
                {
                    context.AddIssue(SemanticError.KhrMaterialsIridescenceThicknessRangeWithoutTexture,
                        name: KhrMaterialsIridescenceExtension.IRIDESCENCE_THICKNESS_MINIMUM);
                }
            }
        }

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsIridescence), context);

        var iridescence = new KhrMaterialsIridescence(
            iridescenceFactor,
            iridescenceTexture,
            iridescenceIor,
            iridescenceThicknessMinimum,
            iridescenceThicknessMaximum,
            iridescenceThicknessTexture,
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(iridescence, new object?[]
        {
            iridescenceTexture,
            iridescenceThicknessTexture,
        }.Concat(extensions.Values));

        return iridescence;
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (IridescenceTexture != null)
        {
            context.Push(KhrMaterialsIridescenceExtension.IRIDESCENCE_TEXTURE);
            IridescenceTexture.Link(gltf, context);
            context.Pop();
        }

        if (IridescenceThicknessTexture != null)
        {
            context.Push(KhrMaterialsIridescenceExtension.IRIDESCENCE_THICKNESS_TEXTURE);
            IridescenceThicknessTexture.Link(gltf, context);
            context.Pop();
        }
    }
}
