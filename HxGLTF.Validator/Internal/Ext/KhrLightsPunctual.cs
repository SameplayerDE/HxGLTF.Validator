// Port of lib/src/ext/KHR_lights_punctual/khr_lights_punctual.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrLightsPunctualExtension
{
    public static readonly Extension Value = new(
        "KHR_lights_punctual",
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Gltf)] = new ExtensionDescriptor(KhrLightsPunctualGltf.FromMap),
            [typeof(Node)] = new ExtensionDescriptor(KhrLightsPunctualNode.FromMap),
        });

    public const string LIGHTS = "lights";
    public const string LIGHT = "light";
    public const string COLOR = "color";
    public const string INTENSITY = "intensity";
    public const string SPOT = "spot";
    public const string DIRECTIONAL = "directional";
    public const string POINT = "point";
    public const string RANGE = "range";
    public const string INNER_CONE_ANGLE = "innerConeAngle";
    public const string OUTER_CONE_ANGLE = "outerConeAngle";

    public static readonly IReadOnlyList<string> KHR_LIGHTS_PUNCTUAL_GLTF_MEMBERS = new[] { LIGHTS };

    public static readonly IReadOnlyList<string> KHR_LIGHTS_PUNCTUAL_NODE_MEMBERS = new[] { LIGHT };

    public static readonly IReadOnlyList<string> KHR_LIGHTS_PUNCTUAL_LIGHT_MEMBERS = new[]
    {
        COLOR,
        INTENSITY,
        SPOT,
        Members.TYPE,
        RANGE,
        Members.NAME,
    };

    public static readonly IReadOnlyList<string> KHR_LIGHTS_PUNCTUAL_LIGHT_TYPES = new[]
    {
        DIRECTIONAL,
        POINT,
        SPOT,
    };

    public static readonly IReadOnlyList<string> KHR_LIGHTS_PUNCTUAL_LIGHT_SPOT_MEMBERS = new[]
    {
        INNER_CONE_ANGLE,
        OUTER_CONE_ANGLE,
    };
}

internal sealed class KhrLightsPunctualGltf : GltfProperty
{
    public readonly SafeList<KhrLightsPunctualLight> Lights;

    private KhrLightsPunctualGltf(SafeList<KhrLightsPunctualLight> lights, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Lights = lights;
    }

    public static KhrLightsPunctualGltf FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrLightsPunctualExtension.KHR_LIGHTS_PUNCTUAL_GLTF_MEMBERS, context);
        }

        SafeList<KhrLightsPunctualLight> lights;
        var lightMaps = JsonUtils.GetMapList(map, KhrLightsPunctualExtension.LIGHTS, context);
        if (lightMaps != null)
        {
            lights = new SafeList<KhrLightsPunctualLight>(lightMaps.Count, KhrLightsPunctualExtension.LIGHTS);
            context.Push(KhrLightsPunctualExtension.LIGHTS);
            for (var i = 0; i < lightMaps.Count; i++)
            {
                var lightMap = lightMaps[i];
                context.Push(i);
                lights[i] = KhrLightsPunctualLight.FromMap(lightMap, context);
                context.Pop();
            }
            context.Pop();
        }
        else
        {
            lights = SafeList<KhrLightsPunctualLight>.Empty(KhrLightsPunctualExtension.LIGHTS);
        }

        return new KhrLightsPunctualGltf(
            lights,
            JsonUtils.GetExtensions(map, typeof(KhrLightsPunctualGltf), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (Lights != null)
        {
            context.Push(KhrLightsPunctualExtension.LIGHTS);
            context.ExtensionCollections.Add(new(Lights, context.Path.ToArray()));
            Lights.ForEachWithIndices((i, light) =>
            {
                context.Push(i);
                light.Link(gltf, context);
                context.Pop();
            });
            context.Pop();
        }
    }
}

internal sealed class KhrLightsPunctualLight : GltfChildOfRootProperty
{
    private static readonly int[] Length3 = { 3 };
    private static readonly double[] DefaultColor = { 1.0, 1.0, 1.0 };

    public readonly double[]? Color;
    public readonly double Intensity;
    public readonly KhrLightsPunctualLightSpot? Spot;
    public readonly string? Type;
    public readonly double Range;

    private KhrLightsPunctualLight(double[]? color, double intensity, KhrLightsPunctualLightSpot? spot, string? type,
        double range, string? name, Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        Color = color;
        Intensity = intensity;
        Spot = spot;
        Type = type;
        Range = range;
    }

    public static KhrLightsPunctualLight FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrLightsPunctualExtension.KHR_LIGHTS_PUNCTUAL_LIGHT_MEMBERS, context);
        }

        var color = JsonUtils.GetFloatList(map, KhrLightsPunctualExtension.COLOR, context,
            min: 0, max: 1, lengthsList: Length3, def: DefaultColor);

        var intensity = JsonUtils.GetFloat(map, KhrLightsPunctualExtension.INTENSITY, context, def: 1, min: 0);

        var type = JsonUtils.GetString(map, Members.TYPE, context,
            list: KhrLightsPunctualExtension.KHR_LIGHTS_PUNCTUAL_LIGHT_TYPES, req: true);

        KhrLightsPunctualLightSpot? spot = null;
        if (type == KhrLightsPunctualExtension.SPOT)
        {
            spot = JsonUtils.GetObjectFromInnerMap(map, KhrLightsPunctualExtension.SPOT, context, KhrLightsPunctualLightSpot.FromMap, req: true);
        }
        else if (context.Validate && JsonUtils.Has(map, KhrLightsPunctualExtension.SPOT))
        {
            context.AddIssue(SemanticError.ExtraProperty, name: KhrLightsPunctualExtension.SPOT);
        }

        var range = JsonUtils.GetFloat(map, KhrLightsPunctualExtension.RANGE, context, exclMin: 0);

        if (context.Validate && type == KhrLightsPunctualExtension.DIRECTIONAL && !double.IsNaN(range))
        {
            context.AddIssue(SemanticError.ExtraProperty, name: KhrLightsPunctualExtension.RANGE);
        }

        return new KhrLightsPunctualLight(
            color,
            intensity,
            spot,
            type,
            range,
            JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(KhrLightsPunctualLight), context),
            JsonUtils.GetExtras(map, context));
    }
}

internal sealed class KhrLightsPunctualLightSpot : GltfProperty
{
    public readonly double InnerConeAngle;
    public readonly double OuterConeAngle;

    private KhrLightsPunctualLightSpot(double innerConeAngle, double outerConeAngle, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        InnerConeAngle = innerConeAngle;
        OuterConeAngle = outerConeAngle;
    }

    public static KhrLightsPunctualLightSpot FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrLightsPunctualExtension.KHR_LIGHTS_PUNCTUAL_LIGHT_SPOT_MEMBERS, context);
        }

        var innerConeAngle = JsonUtils.GetFloat(map, KhrLightsPunctualExtension.INNER_CONE_ANGLE, context,
            def: 0, min: 0, exclMax: 1.5707963267948966);

        var outerConeAngle = JsonUtils.GetFloat(map, KhrLightsPunctualExtension.OUTER_CONE_ANGLE, context,
            def: 0.7853981633974483, exclMin: 0, max: 1.5707963267948966);

        if (context.Validate && outerConeAngle <= innerConeAngle)
        {
            context.AddIssue(SemanticError.KhrLightsPunctualLightSpotAngles,
                name: KhrLightsPunctualExtension.OUTER_CONE_ANGLE, args: new object?[] { innerConeAngle, outerConeAngle });
        }

        return new KhrLightsPunctualLightSpot(
            innerConeAngle,
            outerConeAngle,
            JsonUtils.GetExtensions(map, typeof(KhrLightsPunctualLightSpot), context),
            JsonUtils.GetExtras(map, context));
    }
}

internal sealed class KhrLightsPunctualNode : GltfProperty
{
    private readonly int _lightIndex;

    private KhrLightsPunctualLight? _light;

    private KhrLightsPunctualNode(int lightIndex, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        _lightIndex = lightIndex;
    }

    public static KhrLightsPunctualNode FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrLightsPunctualExtension.KHR_LIGHTS_PUNCTUAL_NODE_MEMBERS, context);
        }

        return new KhrLightsPunctualNode(
            JsonUtils.GetIndex(map, KhrLightsPunctualExtension.LIGHT, context),
            JsonUtils.GetExtensions(map, typeof(KhrLightsPunctualNode), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        gltf.Extensions.TryGetValue(KhrLightsPunctualExtension.Value.Name, out var lightsExtension);
        if (lightsExtension is KhrLightsPunctualGltf lightsGltf)
        {
            _light = lightsGltf.Lights[_lightIndex];

            if (context.Validate && _lightIndex != -1)
            {
                if (_light == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: KhrLightsPunctualExtension.LIGHT, args: new object?[] { _lightIndex });
                }
                else
                {
                    _light.MarkAsUsed();
                }
            }
        }
        else if (context.Validate)
        {
            context.AddIssue(SchemaError.UnsatisfiedDependency,
                args: new object?[] { "/" + Members.EXTENSIONS + "/" + KhrLightsPunctualExtension.Value.Name });
        }
    }

    public KhrLightsPunctualLight? Light => _light;
}
