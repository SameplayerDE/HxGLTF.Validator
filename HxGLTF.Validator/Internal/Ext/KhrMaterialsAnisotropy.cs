// Port of lib/src/ext/KHR_materials_anisotropy/khr_materials_anisotropy.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsAnisotropyExtension
{
    public const string KHR_MATERIALS_ANISOTROPY = "KHR_materials_anisotropy";

    public const string ANISOTROPY_STRENGTH = "anisotropyStrength";
    public const string ANISOTROPY_ROTATION = "anisotropyRotation";
    public const string ANISOTROPY_TEXTURE = "anisotropyTexture";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_ANISOTROPY_MEMBERS = new[]
    {
        ANISOTROPY_STRENGTH,
        ANISOTROPY_ROTATION,
        ANISOTROPY_TEXTURE,
    };

    public static readonly Extension Value = new(
        KHR_MATERIALS_ANISOTROPY,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsAnisotropy.FromMap),
        });
}

internal sealed class KhrMaterialsAnisotropy : GltfProperty
{
    public readonly double AnisotropyStrength;
    public readonly double AnisotropyRotation;
    public readonly TextureInfo? AnisotropyTexture;

    private KhrMaterialsAnisotropy(double anisotropyStrength, double anisotropyRotation, TextureInfo? anisotropyTexture,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        AnisotropyStrength = anisotropyStrength;
        AnisotropyRotation = anisotropyRotation;
        AnisotropyTexture = anisotropyTexture;
    }

    public static KhrMaterialsAnisotropy FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsAnisotropyExtension.KHR_MATERIALS_ANISOTROPY_MEMBERS, context);
        }

        var anisotropyStrength = JsonUtils.GetFloat(map, KhrMaterialsAnisotropyExtension.ANISOTROPY_STRENGTH, context, min: 0, max: 1, def: 0);
        var anisotropyRotation = JsonUtils.GetFloat(map, KhrMaterialsAnisotropyExtension.ANISOTROPY_ROTATION, context, def: 0);
        var anisotropyTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsAnisotropyExtension.ANISOTROPY_TEXTURE, context, TextureInfo.FromMap);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsAnisotropy), context);

        var anisotropy = new KhrMaterialsAnisotropy(
            anisotropyStrength,
            anisotropyRotation,
            anisotropyTexture,
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(anisotropy, new object?[] { anisotropyTexture }.Concat(extensions.Values));

        return anisotropy;
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (AnisotropyTexture != null)
        {
            context.Push(KhrMaterialsAnisotropyExtension.ANISOTROPY_TEXTURE);
            AnisotropyTexture.Link(gltf, context);

            object? o = this;
            while (o != null)
            {
                o = context.Owners.TryGetValue(o, out var owner) ? owner : null;
                if (o is Material material)
                {
                    material.NeedsTangent = true;
                    var normalTexture = material.NormalTexture;
                    if (normalTexture != null && normalTexture.TexCoord != AnisotropyTexture.TexCoord)
                    {
                        context.AddIssue(SemanticError.KhrMaterialsAnisotropyAnisotropyTextureTexCoord);
                    }
                    break;
                }
            }
            context.Pop();
        }
    }
}
