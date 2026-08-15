// Port of lib/src/base/skin.dart
// The checker class IbmMatrixFloatChecker defined in skin.dart lives in Internal/Data/ElementCheckers.cs.
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Skin : GltfChildOfRootProperty
{
    private readonly int _inverseBindMatricesIndex;
    private readonly int _skeletonIndex;
    private readonly int[]? _jointsIndices;

    private Accessor? _inverseBindMatrices;
    private Node?[]? _joints;
    private Node? _skeleton;
    // Dart: Set<Node> (LinkedHashSet, insertion ordered; retainWhere keeps order)
    private readonly List<Node> _commonRoots = new();

    public IReadOnlyList<Node> CommonRoots => _commonRoots;

    private Skin(
        int inverseBindMatricesIndex,
        int skeletonIndex,
        int[]? jointsIndices,
        string? name,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(name, extensions, extras)
    {
        _inverseBindMatricesIndex = inverseBindMatricesIndex;
        _skeletonIndex = skeletonIndex;
        _jointsIndices = jointsIndices;
    }

    public Accessor? InverseBindMatrices => _inverseBindMatrices;
    public Node?[]? Joints => _joints;
    public Node? Skeleton => _skeleton;

    public static Skin FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.SKIN_MEMBERS, context);
        }

        return new Skin(
            JsonUtils.GetIndex(map, Members.INVERSE_BIND_MATRICES, context, req: false),
            JsonUtils.GetIndex(map, Members.SKELETON, context, req: false),
            JsonUtils.GetIndicesList(map, Members.JOINTS, context, req: true),
            JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Skin), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        _inverseBindMatrices = gltf.Accessors[_inverseBindMatricesIndex];

        _skeleton = gltf.Nodes[_skeletonIndex];

        if (_jointsIndices != null)
        {
            _joints = new Node?[_jointsIndices.Length];

            JsonUtils.ResolveNodeList(_jointsIndices, _joints, gltf.Nodes, Members.JOINTS, context,
                (node, nodeIndex, index) =>
                {
                    node.IsJoint = true;

                    var parents = new HashSet<Node>(ReferenceEqualityComparer.Instance);

                    var temp = node;
                    while (temp != null && parents.Add(temp))
                    {
                        temp = temp.Parent;
                    }

                    if (_commonRoots.Count == 0)
                    {
                        // Dart: _commonRoots.addAll(parents) (parents is insertion ordered: node, parent, grandparent, ...)
                        temp = node;
                        var seen = new HashSet<Node>(ReferenceEqualityComparer.Instance);
                        while (temp != null && seen.Add(temp))
                        {
                            _commonRoots.Add(temp);
                            temp = temp.Parent;
                        }
                    }
                    else
                    {
                        _commonRoots.RemoveAll(n => !parents.Contains(n));
                    }
                });

            if (_commonRoots.Count == 0)
            {
                context.AddIssue(SemanticError.SkinNoCommonRoot, name: Members.JOINTS);
            }
        }

        if (_inverseBindMatricesIndex != -1)
        {
            if (_inverseBindMatrices == null)
            {
                context.AddIssue(LinkError.UnresolvedReference,
                    name: Members.INVERSE_BIND_MATRICES, args: new object?[] { _inverseBindMatricesIndex });
            }
            else
            {
                _inverseBindMatrices.SetUsage(AccessorUsage.IBM, Members.INVERSE_BIND_MATRICES, context);
                _inverseBindMatrices.BufferView?.SetUsage(BufferViewUsage.IBM, Members.INVERSE_BIND_MATRICES, context);

                if (context.Validate)
                {
                    context.Push(Members.INVERSE_BIND_MATRICES);

                    if (_inverseBindMatrices.BufferView != null &&
                        _inverseBindMatrices.BufferView.ByteStride != -1)
                    {
                        context.AddIssue(LinkError.SkinIbmAccessorWithByteStride);
                    }

                    var format = AccessorFormat.FromAccessor(_inverseBindMatrices);
                    if (!format.Equals(Members.SKIN_IBM_FORMAT))
                    {
                        context.AddIssue(LinkError.SkinIbmInvalidFormat, args: new object?[]
                        {
                            format,
                            new[] { Members.SKIN_IBM_FORMAT },
                        });
                    }
                    else
                    {
                        context.AddElementChecker(_inverseBindMatrices,
                            new IbmMatrixFloatChecker(context.GetPointerString()));
                    }

                    if (_joints != null && _inverseBindMatrices.Count < _joints.Length)
                    {
                        context.AddIssue(LinkError.InvalidIbmAccessorCount,
                            args: new object?[] { _joints.Length, _inverseBindMatrices.Count });
                    }
                    context.Pop();
                }
            }
        }

        if (context.Validate && _skeletonIndex != -1)
        {
            if (_skeleton == null)
            {
                context.AddIssue(LinkError.UnresolvedReference, name: Members.SKELETON, args: new object?[] { _skeletonIndex });
            }
            else if (!_commonRoots.Contains(_skeleton))
            {
                context.AddIssue(SemanticError.SkinSkeletonInvalid, name: Members.SKELETON);
            }
        }
    }
}
