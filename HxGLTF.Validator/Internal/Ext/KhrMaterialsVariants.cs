// Port of lib/src/ext/KHR_materials_variants/KHR_materials_variants.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsVariantsExtension
{
    public static readonly Extension Value = new(
        "KHR_materials_variants",
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Gltf)] = new ExtensionDescriptor(KhrMaterialsVariantsGltf.FromMap),
            [typeof(MeshPrimitive)] = new ExtensionDescriptor(KhrMaterialsVariantsMeshPrimitive.FromMap, localLink: true),
        });

    public const string VARIANTS = "variants";
    public const string MAPPINGS = "mappings";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_VARIANTS_GLTF_MEMBERS = new[] { VARIANTS };

    public static readonly IReadOnlyList<string> KHR_MATERIALS_VARIANTS_VARIANT_MEMBERS = new[] { Members.NAME };

    public static readonly IReadOnlyList<string> KHR_MATERIALS_VARIANTS_MESH_PRIMITIVE_MEMBERS = new[] { MAPPINGS };

    public static readonly IReadOnlyList<string> KHR_MATERIALS_VARIANTS_MAPPING_MEMBERS = new[]
    {
        VARIANTS,
        Members.MATERIAL,
        Members.NAME,
    };
}

internal sealed class KhrMaterialsVariantsGltf : GltfProperty
{
    public readonly SafeList<KhrMaterialsVariantsVariant> Variants;

    private KhrMaterialsVariantsGltf(SafeList<KhrMaterialsVariantsVariant> variants, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Variants = variants;
    }

    public static KhrMaterialsVariantsGltf FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsVariantsExtension.KHR_MATERIALS_VARIANTS_GLTF_MEMBERS, context);
        }

        SafeList<KhrMaterialsVariantsVariant> variants;
        var variantMaps = JsonUtils.GetMapList(map, KhrMaterialsVariantsExtension.VARIANTS, context);
        if (variantMaps != null)
        {
            variants = new SafeList<KhrMaterialsVariantsVariant>(variantMaps.Count, KhrMaterialsVariantsExtension.VARIANTS);
            context.Push(KhrMaterialsVariantsExtension.VARIANTS);
            for (var i = 0; i < variantMaps.Count; i++)
            {
                var variantMap = variantMaps[i];
                context.Push(i);
                variants[i] = KhrMaterialsVariantsVariant.FromMap(variantMap, context);
                context.Pop();
            }
            context.Pop();
        }
        else
        {
            variants = SafeList<KhrMaterialsVariantsVariant>.Empty(KhrMaterialsVariantsExtension.VARIANTS);
        }

        return new KhrMaterialsVariantsGltf(
            variants,
            JsonUtils.GetExtensions(map, typeof(KhrMaterialsVariantsGltf), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        context.Push(KhrMaterialsVariantsExtension.VARIANTS);
        context.ExtensionCollections.Add(new(Variants, context.Path.ToArray()));
        Variants.ForEachWithIndices((i, variant) =>
        {
            context.Push(i);
            variant.Link(gltf, context);
            context.Pop();
        });
        context.Pop();
    }
}

internal sealed class KhrMaterialsVariantsVariant : GltfChildOfRootProperty
{
    private KhrMaterialsVariantsVariant(string? name, Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
    }

    public static KhrMaterialsVariantsVariant FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsVariantsExtension.KHR_MATERIALS_VARIANTS_VARIANT_MEMBERS, context);
        }

        return new KhrMaterialsVariantsVariant(
            JsonUtils.GetName(map, context, req: true),
            JsonUtils.GetExtensions(map, typeof(KhrMaterialsVariantsVariant), context),
            JsonUtils.GetExtras(map, context));
    }
}

internal sealed class KhrMaterialsVariantsMeshPrimitive : GltfProperty
{
    public readonly SafeList<KhrMaterialsVariantsMapping> Mappings;

    private KhrMaterialsVariantsMeshPrimitive(SafeList<KhrMaterialsVariantsMapping> mappings, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Mappings = mappings;
    }

    public static KhrMaterialsVariantsMeshPrimitive FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsVariantsExtension.KHR_MATERIALS_VARIANTS_MESH_PRIMITIVE_MEMBERS, context);
        }

        SafeList<KhrMaterialsVariantsMapping> mappings;
        var mappingMaps = JsonUtils.GetMapList(map, KhrMaterialsVariantsExtension.MAPPINGS, context);
        if (mappingMaps != null)
        {
            mappings = new SafeList<KhrMaterialsVariantsMapping>(mappingMaps.Count, KhrMaterialsVariantsExtension.MAPPINGS);
            context.Push(KhrMaterialsVariantsExtension.MAPPINGS);
            for (var i = 0; i < mappingMaps.Count; i++)
            {
                var mappingMap = mappingMaps[i];
                context.Push(i);
                mappings[i] = KhrMaterialsVariantsMapping.FromMap(mappingMap, context);
                context.Pop();
            }
            context.Pop();
        }
        else
        {
            mappings = SafeList<KhrMaterialsVariantsMapping>.Empty(KhrMaterialsVariantsExtension.MAPPINGS);
        }

        var variants = new KhrMaterialsVariantsMeshPrimitive(
            mappings,
            JsonUtils.GetExtensions(map, typeof(KhrMaterialsVariantsMeshPrimitive), context),
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(variants, mappings);

        return variants;
    }

    public override void Link(Gltf gltf, Context context)
    {
        context.Push(KhrMaterialsVariantsExtension.MAPPINGS);

        var uniqueVariants = new HashSet<int>();
        Mappings.ForEachWithIndices((i, mapping) =>
        {
            context.Push(i);
            mapping.Link(gltf, context, uniqueIndices: uniqueVariants);
            context.Pop();
        });

        context.Pop();
    }
}

internal sealed class KhrMaterialsVariantsMapping : GltfProperty
{
    private readonly int[]? _variantIndices;
    private readonly int _materialIndex;
    public readonly string? Name;

    private Material? _material;
    public Material? Material => _material;

    private KhrMaterialsVariantsVariant?[]? _variants;
    public KhrMaterialsVariantsVariant?[]? Variants => _variants;

    private KhrMaterialsVariantsMapping(int[]? variantIndices, int materialIndex, string? name,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        _variantIndices = variantIndices;
        _materialIndex = materialIndex;
        Name = name;
    }

    public static KhrMaterialsVariantsMapping FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsVariantsExtension.KHR_MATERIALS_VARIANTS_MAPPING_MEMBERS, context);
        }

        return new KhrMaterialsVariantsMapping(
            JsonUtils.GetIndicesList(map, KhrMaterialsVariantsExtension.VARIANTS, context, req: true),
            JsonUtils.GetIndex(map, Members.MATERIAL, context),
            JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(KhrMaterialsVariantsMapping), context),
            JsonUtils.GetExtras(map, context));
    }

    // Dart: link(gltf, context, {@required Set<int> uniqueIndices}); the parameterless override is never used
    // (the extension is localLink and KhrMaterialsVariantsMeshPrimitive.Link calls the overload below).
    public override void Link(Gltf gltf, Context context) => Link(gltf, context, new HashSet<int>());

    public void Link(Gltf gltf, Context context, HashSet<int> uniqueIndices)
    {
        gltf.Extensions.TryGetValue(KhrMaterialsVariantsExtension.Value.Name, out var khrMaterialsVariantsGltfObject);
        if (khrMaterialsVariantsGltfObject is KhrMaterialsVariantsGltf khrMaterialsVariantsGltf)
        {
            if (_variantIndices != null)
            {
                context.Push(KhrMaterialsVariantsExtension.VARIANTS);
                _variants = new KhrMaterialsVariantsVariant?[_variantIndices.Length];
                for (var i = 0; i < _variantIndices.Length; i++)
                {
                    var variantIndex = _variantIndices[i];
                    var variant = khrMaterialsVariantsGltf.Variants[variantIndex];
                    if (context.Validate && variantIndex != -1)
                    {
                        if (!uniqueIndices.Add(variantIndex))
                        {
                            context.AddIssue(LinkError.KhrMaterialsVariantsNonUniqueVariant, index: i);
                        }
                        if (variant == null)
                        {
                            context.AddIssue(LinkError.UnresolvedReference, index: i, args: new object?[] { variantIndex });
                        }
                        else
                        {
                            variant.MarkAsUsed();
                        }
                    }
                    _variants[i] = variant;
                }
                context.Pop();
            }

            _material = gltf.Materials[_materialIndex];
            if (context.Validate && _materialIndex != -1)
            {
                if (_material == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: Members.MATERIAL, args: new object?[] { _materialIndex });
                }
                else
                {
                    _material.MarkAsUsed();

                    // Find the mesh primitive
                    object? o = this;
                    while (o != null)
                    {
                        o = context.Owners.TryGetValue(o, out var owner) ? owner : null;
                        if (o is MeshPrimitive primitive)
                        {
                            foreach (var (pointer, texCoord) in _material.TexCoordIndices)
                            {
                                if (texCoord != -1)
                                {
                                    if (texCoord + 1 > primitive.TexCoordCount)
                                    {
                                        context.AddIssue(LinkError.MeshPrimitiveTooFewTexcoords,
                                            name: Members.MATERIAL, args: new object?[] { pointer, texCoord });
                                    }
                                    else
                                    {
                                        // mark as used
                                        primitive.MarkUsedTexCoord(texCoord);
                                    }
                                }
                            }

                            break;
                        }
                    }
                }
            }
        }
        else if (context.Validate)
        {
            context.AddIssue(SchemaError.UnsatisfiedDependency,
                args: new object?[] { "/" + Members.EXTENSIONS + "/" + KhrMaterialsVariantsExtension.Value.Name });
        }
    }
}
