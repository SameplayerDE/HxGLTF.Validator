// Port of lib/src/base/material.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Material : GltfChildOfRootProperty
{
    public readonly PbrMetallicRoughness? PbrMetallicRoughness;
    public readonly NormalTextureInfo? NormalTexture;
    public readonly OcclusionTextureInfo? OcclusionTexture;
    public readonly TextureInfo? EmissiveTexture;
    public readonly double[]? EmissiveFactor;
    public readonly string? AlphaMode;
    public readonly double AlphaCutoff;
    public readonly bool DoubleSided;

    public bool NeedsTangent;

    private static readonly int[] Length3 = { 3 };
    private static readonly double[] DefaultEmissiveFactor = { 0, 0, 0 };

    public bool CanProvideTangent => NormalTexture != null;

    // Dart: Map<String, int> (insertion ordered). Dictionary keeps insertion order as long as nothing is removed.
    public readonly Dictionary<string, int> TexCoordIndices = new(StringComparer.Ordinal);

    private Material(
        PbrMetallicRoughness? pbrMetallicRoughness,
        NormalTextureInfo? normalTexture,
        OcclusionTextureInfo? occlusionTexture,
        TextureInfo? emissiveTexture,
        double[]? emissiveFactor,
        string? alphaMode,
        double alphaCutoff,
        bool doubleSided,
        string? name,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(name, extensions, extras)
    {
        PbrMetallicRoughness = pbrMetallicRoughness;
        NormalTexture = normalTexture;
        OcclusionTexture = occlusionTexture;
        EmissiveTexture = emissiveTexture;
        EmissiveFactor = emissiveFactor;
        AlphaMode = alphaMode;
        AlphaCutoff = alphaCutoff;
        DoubleSided = doubleSided;
    }

    public static Material FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.MATERIAL_MEMBERS, context);
        }

        var pbrMetallicRoughness = JsonUtils.GetObjectFromInnerMap<PbrMetallicRoughness>(
            map, Members.PBR_METALLIC_ROUGHNESS, context, PbrMetallicRoughness.FromMap);
        var normalTexture = JsonUtils.GetObjectFromInnerMap<NormalTextureInfo>(
            map, Members.NORMAL_TEXTURE, context, NormalTextureInfo.FromMap);
        var occlusionTexture = JsonUtils.GetObjectFromInnerMap<OcclusionTextureInfo>(
            map, Members.OCCLUSION_TEXTURE, context, OcclusionTextureInfo.FromMap);
        var emissiveTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, Members.EMISSIVE_TEXTURE, context, TextureInfo.FromMap);
        var emissiveFactor = JsonUtils.GetFloatList(map, Members.EMISSIVE_FACTOR, context,
            lengthsList: Length3, min: 0, max: 1, def: DefaultEmissiveFactor);
        var alphaMode = JsonUtils.GetString(map, Members.ALPHA_MODE, context,
            def: Members.OPAQUE, list: Members.MATERIAL_ALPHA_MODES);
        var alphaCutoff = JsonUtils.GetFloat(map, Members.ALPHA_CUTOFF, context, min: 0, def: 0.5);

        if (context.Validate &&
            alphaMode != Members.MASK &&
            JsonUtils.Has(map, Members.ALPHA_CUTOFF))
        {
            context.AddIssue(SemanticError.MaterialAlphaCutoffInvalidMode, name: Members.ALPHA_CUTOFF);
        }

        var doubleSided = JsonUtils.GetBool(map, Members.DOUBLE_SIDED, context);

        var extensions = JsonUtils.GetExtensions(map, typeof(Material), context);

        var material = new Material(
            pbrMetallicRoughness,
            normalTexture,
            occlusionTexture,
            emissiveTexture,
            emissiveFactor,
            alphaMode,
            alphaCutoff,
            doubleSided,
            JsonUtils.GetName(map, context),
            extensions,
            JsonUtils.GetExtras(map, context));

        var owned = new List<object?>
        {
            pbrMetallicRoughness,
            normalTexture,
            occlusionTexture,
            emissiveTexture,
        };
        owned.AddRange(extensions.Values);
        context.RegisterObjectsOwner(material, owned);

        return material;
    }

    public override void Link(Gltf gltf, Context context)
    {
        void LinkWithPath(GltfProperty? property, string name)
        {
            if (property != null)
            {
                context.Push(name);
                property.Link(gltf, context);
                context.Pop();
            }
        }

        LinkWithPath(PbrMetallicRoughness, Members.PBR_METALLIC_ROUGHNESS);
        LinkWithPath(NormalTexture, Members.NORMAL_TEXTURE);
        LinkWithPath(OcclusionTexture, Members.OCCLUSION_TEXTURE);
        LinkWithPath(EmissiveTexture, Members.EMISSIVE_TEXTURE);
    }
}

internal sealed class PbrMetallicRoughness : GltfProperty
{
    public readonly double[]? BaseColorFactor;
    public readonly TextureInfo? BaseColorTexture;

    public readonly double MetallicFactor;
    public readonly double RoughnessFactor;
    public readonly TextureInfo? MetallicRoughnessTexture;

    private static readonly int[] Length4 = { 4 };
    private static readonly double[] DefaultBaseColorFactor = { 1, 1, 1, 1 };

    private PbrMetallicRoughness(
        double[]? baseColorFactor,
        TextureInfo? baseColorTexture,
        double metallicFactor,
        double roughnessFactor,
        TextureInfo? metallicRoughnessTexture,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(extensions, extras)
    {
        BaseColorFactor = baseColorFactor;
        BaseColorTexture = baseColorTexture;
        MetallicFactor = metallicFactor;
        RoughnessFactor = roughnessFactor;
        MetallicRoughnessTexture = metallicRoughnessTexture;
    }

    public static PbrMetallicRoughness FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.PBR_METALLIC_ROUGHNESS_MEMBERS, context);
        }

        var baseColorFactor = JsonUtils.GetFloatList(map, Members.BASE_COLOR_FACTOR, context,
            lengthsList: Length4, min: 0, max: 1, def: DefaultBaseColorFactor);
        var baseColorTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, Members.BASE_COLOR_TEXTURE, context, TextureInfo.FromMap);
        var metallicFactor = JsonUtils.GetFloat(map, Members.METALLIC_FACTOR, context, min: 0, max: 1, def: 1);
        var roughnessFactor = JsonUtils.GetFloat(map, Members.ROUGHNESS_FACTOR, context, min: 0, max: 1, def: 1);
        var metallicRoughnessTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, Members.METALLIC_ROUGHNESS_TEXTURE, context, TextureInfo.FromMap);

        var extensions = JsonUtils.GetExtensions(map, typeof(PbrMetallicRoughness), context);

        var pbrMr = new PbrMetallicRoughness(
            baseColorFactor,
            baseColorTexture,
            metallicFactor,
            roughnessFactor,
            metallicRoughnessTexture,
            extensions,
            JsonUtils.GetExtras(map, context));

        var owned = new List<object?> { baseColorTexture, metallicRoughnessTexture };
        owned.AddRange(extensions.Values);
        context.RegisterObjectsOwner(pbrMr, owned);

        return pbrMr;
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (BaseColorTexture != null)
        {
            context.Push(Members.BASE_COLOR_TEXTURE);
            BaseColorTexture.Link(gltf, context);
            context.Pop();
        }

        if (MetallicRoughnessTexture != null)
        {
            context.Push(Members.METALLIC_ROUGHNESS_TEXTURE);
            MetallicRoughnessTexture.Link(gltf, context);
            context.Pop();
        }
    }
}

internal sealed class OcclusionTextureInfo : TextureInfo
{
    public readonly double Strength;

    private OcclusionTextureInfo(int index, int texCoord, double strength,
        Dictionary<string, object?> extensions, object? extras)
        : base(index, texCoord, extensions, extras)
    {
        Strength = strength;
    }

    public static new OcclusionTextureInfo FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.OCCLUSION_TEXTURE_INFO_MEMBERS, context);
        }

        var extensions = JsonUtils.GetExtensions(map, typeof(OcclusionTextureInfo), context,
            overriddenType: typeof(Material));

        var occlusionTextureInfo = new OcclusionTextureInfo(
            JsonUtils.GetIndex(map, Members.INDEX, context),
            JsonUtils.GetUint(map, Members.TEX_COORD, context, def: 0),
            JsonUtils.GetFloat(map, Members.STRENGTH, context, min: 0, max: 1, def: 1),
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(occlusionTextureInfo, extensions.Values);

        return occlusionTextureInfo;
    }
}

internal sealed class NormalTextureInfo : TextureInfo
{
    public readonly double Scale;

    private NormalTextureInfo(int index, int texCoord, double scale,
        Dictionary<string, object?> extensions, object? extras)
        : base(index, texCoord, extensions, extras)
    {
        Scale = scale;
    }

    public static new NormalTextureInfo FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.NORMAL_TEXTURE_INFO_MEMBERS, context);
        }

        var extensions = JsonUtils.GetExtensions(map, typeof(NormalTextureInfo), context,
            overriddenType: typeof(Material));

        var normalTextureInfo = new NormalTextureInfo(
            JsonUtils.GetIndex(map, Members.INDEX, context),
            JsonUtils.GetUint(map, Members.TEX_COORD, context, def: 0),
            JsonUtils.GetFloat(map, Members.SCALE, context, def: 1),
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(normalTextureInfo, extensions.Values);

        return normalTextureInfo;
    }

    public override void Link(Gltf gltf, Context context)
    {
        base.Link(gltf, context);
        object? o = this;
        while (o != null)
        {
            o = context.Owners.TryGetValue(o, out var owner) ? owner : null;
            if (o is Material material)
            {
                material.NeedsTangent = true;
                break;
            }
        }
    }
}

internal class TextureInfo : GltfProperty
{
    private readonly int _index;
    public readonly int TexCoord;

    private Texture? _texture;

    protected TextureInfo(int index, int texCoord, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        _index = index;
        TexCoord = texCoord;
    }

    public Texture? Texture => _texture;

    public static TextureInfo FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.TEXTURE_INFO_MEMBERS, context);
        }

        var extensions = JsonUtils.GetExtensions(map, typeof(TextureInfo), context, overriddenType: typeof(Material));

        var textureInfo = new TextureInfo(
            JsonUtils.GetIndex(map, Members.INDEX, context),
            JsonUtils.GetUint(map, Members.TEX_COORD, context, def: 0),
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(textureInfo, extensions.Values);

        return textureInfo;
    }

    public override void Link(Gltf gltf, Context context)
    {
        _texture = gltf.Textures[_index];

        if (context.Validate && _index != -1)
        {
            if (_texture == null)
            {
                context.AddIssue(LinkError.UnresolvedReference, name: Members.INDEX, args: new object?[] { _index });
            }
            else
            {
                _texture.MarkAsUsed();
            }
        }

        object? o = this;
        while (o != null)
        {
            o = context.Owners.TryGetValue(o, out var owner) ? owner : null;
            if (o is Material material)
            {
                material.TexCoordIndices[context.GetPointerString()] = TexCoord;
                break;
            }
        }
    }
}
