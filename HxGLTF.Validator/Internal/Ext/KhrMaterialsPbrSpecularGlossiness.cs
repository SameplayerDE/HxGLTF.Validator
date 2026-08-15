// Port of lib/src/ext/KHR_materials_pbrSpecularGlossiness/khr_materials_pbr_specular_glossiness.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsPbrSpecularGlossinessExtension
{
    public const string KHR_MATERIALS_PBRSPECULARGLOSSINESS = "KHR_materials_pbrSpecularGlossiness";

    public const string DIFFUSE_FACTOR = "diffuseFactor";
    public const string DIFFUSE_TEXTURE = "diffuseTexture";
    public const string SPECULAR_FACTOR = "specularFactor";
    public const string GLOSSINESS_FACTOR = "glossinessFactor";
    public const string SPECULAR_GLOSSINESS_TEXTURE = "specularGlossinessTexture";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_PBRSPECULARGLOSSINESS_MEMBERS = new[]
    {
        DIFFUSE_FACTOR,
        DIFFUSE_TEXTURE,
        SPECULAR_FACTOR,
        GLOSSINESS_FACTOR,
        SPECULAR_GLOSSINESS_TEXTURE,
    };

    public static readonly Extension Value = new(
        KHR_MATERIALS_PBRSPECULARGLOSSINESS,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsPbrSpecularGlossiness.FromMap, standalone: true),
        });
}

internal sealed class KhrMaterialsPbrSpecularGlossiness : GltfProperty
{
    private static readonly int[] Length4 = { 4 };
    private static readonly int[] Length3 = { 3 };
    private static readonly double[] DefaultDiffuseFactor = { 1.0, 1.0, 1.0, 1.0 };
    private static readonly double[] DefaultSpecularFactor = { 1.0, 1.0, 1.0 };

    public readonly double[]? DiffuseFactor;
    public readonly TextureInfo? DiffuseTexture;

    public readonly double[]? SpecularFactor;
    public readonly double GlossinessFactor;
    public readonly TextureInfo? SpecularGlossinessTexture;

    private KhrMaterialsPbrSpecularGlossiness(
        double[]? diffuseFactor,
        TextureInfo? diffuseTexture,
        double[]? specularFactor,
        double glossinessFactor,
        TextureInfo? specularGlossinessTexture,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(extensions, extras)
    {
        DiffuseFactor = diffuseFactor;
        DiffuseTexture = diffuseTexture;
        SpecularFactor = specularFactor;
        GlossinessFactor = glossinessFactor;
        SpecularGlossinessTexture = specularGlossinessTexture;
    }

    public static KhrMaterialsPbrSpecularGlossiness FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsPbrSpecularGlossinessExtension.KHR_MATERIALS_PBRSPECULARGLOSSINESS_MEMBERS, context);
        }

        var diffuseFactor = JsonUtils.GetFloatList(map, KhrMaterialsPbrSpecularGlossinessExtension.DIFFUSE_FACTOR, context,
            lengthsList: Length4, min: 0, max: 1, def: DefaultDiffuseFactor);
        var diffuseTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsPbrSpecularGlossinessExtension.DIFFUSE_TEXTURE, context, TextureInfo.FromMap);
        var specularFactor = JsonUtils.GetFloatList(map, KhrMaterialsPbrSpecularGlossinessExtension.SPECULAR_FACTOR, context,
            lengthsList: Length3, min: 0, max: 1, def: DefaultSpecularFactor);
        var glossinessFactor = JsonUtils.GetFloat(map, KhrMaterialsPbrSpecularGlossinessExtension.GLOSSINESS_FACTOR, context, min: 0, max: 1, def: 1);
        var specularGlossinessTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsPbrSpecularGlossinessExtension.SPECULAR_GLOSSINESS_TEXTURE, context, TextureInfo.FromMap);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsPbrSpecularGlossiness), context);

        var pbrSg = new KhrMaterialsPbrSpecularGlossiness(
            diffuseFactor,
            diffuseTexture,
            specularFactor,
            glossinessFactor,
            specularGlossinessTexture,
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(pbrSg, new object?[] { diffuseTexture, specularGlossinessTexture }.Concat(extensions.Values));

        return pbrSg;
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (DiffuseTexture != null)
        {
            context.Push(KhrMaterialsPbrSpecularGlossinessExtension.DIFFUSE_TEXTURE);
            DiffuseTexture.Link(gltf, context);
            context.Pop();
        }

        if (SpecularGlossinessTexture != null)
        {
            context.Push(KhrMaterialsPbrSpecularGlossinessExtension.SPECULAR_GLOSSINESS_TEXTURE);
            SpecularGlossinessTexture.Link(gltf, context);
            context.Pop();
        }
    }
}
