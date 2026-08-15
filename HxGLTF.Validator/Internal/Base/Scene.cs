// Port of lib/src/base/scene.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Scene : GltfChildOfRootProperty
{
    private readonly int[]? _nodesIndices;
    public Node?[]? Nodes;

    private Scene(int[]? nodesIndices, string? name, Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        _nodesIndices = nodesIndices;
    }

    public static Scene FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.SCENE_MEMBERS, context);
        }

        var nodesIndices = JsonUtils.GetIndicesList(map, Members.NODES, context);

        return new Scene(nodesIndices, JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Scene), context), JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (_nodesIndices == null)
        {
            return;
        }

        Nodes = new Node?[_nodesIndices.Length];

        JsonUtils.ResolveNodeList(_nodesIndices, Nodes, gltf.Nodes, Members.NODES, context,
            (node, nodeIndex, index) =>
            {
                if (node.Parent != null)
                {
                    context.AddIssue(LinkError.SceneNonRootNode,
                        index: index, args: new object?[] { nodeIndex });
                }

                node.AddScene(this);
            });
    }
}
