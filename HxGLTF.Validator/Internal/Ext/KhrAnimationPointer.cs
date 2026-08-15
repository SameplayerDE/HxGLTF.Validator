// Port of lib/src/ext/KHR_animation_pointer/khr_animation_pointer.dart

using System.Text.Json;
using System.Text.RegularExpressions;

namespace HxGLTF.Validator.Internal;

internal static class KhrAnimationPointerExtension
{
    public const string KHR_ANIMATION_POINTER = "KHR_animation_pointer";

    public const string POINTER = "pointer";

    public static readonly IReadOnlyList<string> KHR_ANIMATION_POINTER_MEMBERS = new[] { POINTER };

    public static readonly Extension Value = new(
        KHR_ANIMATION_POINTER,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(AnimationChannelTarget)] = new ExtensionDescriptor(KhrAnimationPointer.FromMap),
        },
        init: Init);

    private static void Init(Context context)
    {
        context.AnimationChannelTargetPaths.Add(POINTER);
    }
}

internal sealed class KhrAnimationPointer : GltfProperty
{
    public readonly string? Pointer;

    private KhrAnimationPointer(string? pointer, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Pointer = pointer;
    }

    // Dart: RegExp(r'^(?:\/(?:[^/~]|~0|~1)*)*$'). The pattern text is printed in PATTERN_MISMATCH messages, so it must
    // stay identical to Dart; .NET's $ additionally matches before a trailing newline (irrelevant for JSON pointers).
    private static readonly Regex PointerRegExp = new(@"^(?:\/(?:[^/~]|~0|~1)*)*$", RegexOptions.CultureInvariant);

    public static KhrAnimationPointer FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrAnimationPointerExtension.KHR_ANIMATION_POINTER_MEMBERS, context);
        }

        var pointer = JsonUtils.GetString(map, KhrAnimationPointerExtension.POINTER, context, req: true, regexp: PointerRegExp);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrAnimationPointer), context);

        return new KhrAnimationPointer(pointer, extensions, JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        // TODO:
        // * pointer existence
        // * channel target uniqueness
        // * output accessor compatibility
        context.AddIssue(LinkError.IncompleteExtensionSupport);

        object? o = this;
        while (o != null)
        {
            o = context.Owners.TryGetValue(o, out var owner) ? owner : null;
            if (o is AnimationChannelTarget target)
            {
                if (target.Node != null)
                {
                    context.AddIssue(SemanticError.KhrAnimationPointerAnimationChannelTargetNode);
                }
                if (target.Path != KhrAnimationPointerExtension.POINTER)
                {
                    context.AddIssue(SemanticError.KhrAnimationPointerAnimationChannelTargetPath, args: new object?[] { target.Path });
                }
                break;
            }
        }
    }
}
