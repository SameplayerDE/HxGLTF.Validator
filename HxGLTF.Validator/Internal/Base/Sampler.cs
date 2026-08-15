// Port of lib/src/base/sampler.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Sampler : GltfChildOfRootProperty
{
    public readonly int MagFilter;
    public readonly int MinFilter;
    public readonly int WrapS;
    public readonly int WrapT;

    private Sampler(int magFilter, int minFilter, int wrapS, int wrapT, string? name,
        Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        MagFilter = magFilter;
        MinFilter = minFilter;
        WrapS = wrapS;
        WrapT = wrapT;
    }

    public static Sampler FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.SAMPLER_MEMBERS, context);
        }

        return new Sampler(
            JsonUtils.GetUint(map, Members.MAG_FILTER, context, list: Members.MAG_FILTERS),
            JsonUtils.GetUint(map, Members.MIN_FILTER, context, list: Members.MIN_FILTERS),
            JsonUtils.GetUint(map, Members.WRAP_S, context, list: Members.WRAP_FILTERS, def: Gl.REPEAT),
            JsonUtils.GetUint(map, Members.WRAP_T, context, list: Members.WRAP_FILTERS, def: Gl.REPEAT),
            JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Sampler), context),
            JsonUtils.GetExtras(map, context));
    }
}
