// Port of lib/src/ext/KHR_node_visibility/khr_node_visibility.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrNodeVisibilityExtension
{
    public const string KHR_NODE_VISIBILITY = "KHR_node_visibility";
    public const string VISIBLE = "visible";

    public static readonly IReadOnlyList<string> KHR_NODE_VISIBILITY_MEMBERS = new[] { VISIBLE };

    public static readonly Extension Value = new(
        KHR_NODE_VISIBILITY,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Node)] = new ExtensionDescriptor(KhrNodeVisibility.FromMap),
        });
}

internal sealed class KhrNodeVisibility : GltfProperty
{
    public readonly bool Visible;

    private KhrNodeVisibility(bool visible, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Visible = visible;
    }

    public static KhrNodeVisibility FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrNodeVisibilityExtension.KHR_NODE_VISIBILITY_MEMBERS, context);
        }
        return new KhrNodeVisibility(
            JsonUtils.GetBool(map, KhrNodeVisibilityExtension.VISIBLE, context, def: true),
            JsonUtils.GetExtensions(map, typeof(KhrNodeVisibility), context),
            JsonUtils.GetExtras(map, context));
    }
}
