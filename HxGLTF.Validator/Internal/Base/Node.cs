// Port of lib/src/base/node.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Node : GltfChildOfRootProperty
{
    private readonly int _cameraIndex;
    private readonly int[]? _childrenIndices;
    private readonly int _skinIndex;
    public readonly Matrix4? Matrix;
    private readonly int _meshIndex;
    public readonly Vector3? Translation;
    public readonly Quaternion? Rotation;
    public readonly Vector3? Scale;
    public readonly double[]? Weights;
    // Dart: Set<Scene> (LinkedHashSet, insertion ordered)
    private readonly List<Scene> _scenes = new();

    private Camera? _camera;
    private Node?[]? _children;
    private Mesh? _mesh;
    private Node? _parent;
    private Skin? _skin;

    public bool IsJoint;

    private static readonly int[] Length16 = { 16 };
    private static readonly int[] Length3 = { 3 };
    private static readonly int[] Length4 = { 4 };

    private Node(
        int cameraIndex,
        int[]? childrenIndices,
        int skinIndex,
        Matrix4? matrix,
        int meshIndex,
        Vector3? translation,
        Quaternion? rotation,
        Vector3? scale,
        double[]? weights,
        string? name,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(name, extensions, extras)
    {
        _cameraIndex = cameraIndex;
        _childrenIndices = childrenIndices;
        _skinIndex = skinIndex;
        Matrix = matrix;
        _meshIndex = meshIndex;
        Translation = translation;
        Rotation = rotation;
        Scale = scale;
        Weights = weights;
    }

    public static Node FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.NODE_MEMBERS, context);
        }

        Matrix4? matrix = null;
        if (JsonUtils.Has(map, Members.MATRIX))
        {
            var matrixList = JsonUtils.GetFloatList(map, Members.MATRIX, context, lengthsList: Length16);
            if (matrixList != null)
            {
                matrix = Matrix4.FromList(matrixList);
            }
        }

        Vector3? translation = null;
        if (JsonUtils.Has(map, Members.TRANSLATION))
        {
            var translationList = JsonUtils.GetFloatList(map, Members.TRANSLATION, context, lengthsList: Length3);
            if (translationList != null)
            {
                translation = Vector3.FromArray(translationList);
            }
        }

        Quaternion? rotation = null;
        if (JsonUtils.Has(map, Members.ROTATION))
        {
            var rotationList = JsonUtils.GetFloatList(map, Members.ROTATION, context,
                lengthsList: Length4, min: -1, max: 1);
            if (rotationList != null)
            {
                rotation = new Quaternion(rotationList[0], rotationList[1], rotationList[2], rotationList[3]);
                if (context.Validate &&
                    Math.Abs(1.0 - rotation.Length) > IssueConstants.UnitLengthThresholdVec4)
                {
                    context.AddIssue(SemanticError.RotationNonUnit, name: Members.ROTATION);
                }
            }
        }

        Vector3? scale = null;
        if (JsonUtils.Has(map, Members.SCALE))
        {
            var scaleList = JsonUtils.GetFloatList(map, Members.SCALE, context, lengthsList: Length3);
            if (scaleList != null)
            {
                scale = Vector3.FromArray(scaleList);
            }
        }

        var cameraIndex = JsonUtils.GetIndex(map, Members.CAMERA, context, req: false);
        var childrenIndices = JsonUtils.GetIndicesList(map, Members.CHILDREN, context);
        var meshIndex = JsonUtils.GetIndex(map, Members.MESH, context, req: false);
        var skinIndex = JsonUtils.GetIndex(map, Members.SKIN, context, req: false);
        var weightsList = JsonUtils.GetFloatList(map, Members.WEIGHTS, context);

        if (context.Validate)
        {
            if (meshIndex == -1)
            {
                if (skinIndex != -1)
                {
                    context.AddIssue(SchemaError.UnsatisfiedDependency, name: Members.SKIN, args: new object?[] { Members.MESH });
                }

                if (weightsList != null)
                {
                    context.AddIssue(SchemaError.UnsatisfiedDependency, name: Members.WEIGHTS, args: new object?[] { Members.MESH });
                }
            }

            if (matrix != null)
            {
                if (translation != null || rotation != null || scale != null)
                {
                    context.AddIssue(SemanticError.NodeMatrixTrs, name: Members.MATRIX);
                }

                if (matrix.IsIdentity())
                {
                    context.AddIssue(SemanticError.NodeDefaultMatrix, name: Members.MATRIX);
                }
                else if (!MatrixUtils.IsTrsDecomposable(matrix))
                {
                    context.AddIssue(SemanticError.NodeNonTrsMatrix, name: Members.MATRIX);
                }
            }
        }

        return new Node(
            cameraIndex,
            childrenIndices,
            skinIndex,
            matrix,
            meshIndex,
            translation,
            rotation,
            scale,
            weightsList,
            JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Node), context),
            JsonUtils.GetExtras(map, context));
    }

    public Camera? Camera => _camera;
    public Node?[]? Children => _children;
    public Mesh? Mesh => _mesh;
    public Node? Parent => _parent;
    public Skin? Skin => _skin;
    public IReadOnlyList<Scene> Scenes => _scenes;

    public bool HasTransform => !((Matrix == null || Matrix.IsIdentity()) &&
                                  (Translation == null || Translation.IsZero) &&
                                  (Rotation == null || Rotation.IsDefault) &&
                                  (Scale == null || Scale.IsOne));

    public override void Link(Gltf gltf, Context context)
    {
        _camera = gltf.Cameras[_cameraIndex];
        _skin = gltf.Skins[_skinIndex];
        _mesh = gltf.Meshes[_meshIndex];

        if (context.Validate)
        {
            if (_cameraIndex != -1)
            {
                if (_camera == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: Members.CAMERA, args: new object?[] { _cameraIndex });
                }
                else
                {
                    _camera.MarkAsUsed();
                }
            }

            if (_skinIndex != -1)
            {
                if (_skin == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: Members.SKIN, args: new object?[] { _skinIndex });
                }
                else
                {
                    _skin.MarkAsUsed();
                }
            }

            if (_meshIndex != -1)
            {
                if (_mesh == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: Members.MESH, args: new object?[] { _meshIndex });
                }
                else
                {
                    _mesh.MarkAsUsed();
                    if (_mesh.Primitives != null)
                    {
                        // Dart: _mesh.primitives[0].targets?.length (primitives are never empty here, see getMapList)
                        int? targetsLength = _mesh.Primitives[0]?.Targets?.Count;
                        if (Weights != null &&
                            targetsLength != Weights.Length)
                        {
                            context.AddIssue(LinkError.NodeWeightsInvalid,
                                name: Members.WEIGHTS,
                                args: new object?[] { Weights.Length, targetsLength });
                        }

                        if (Weights == null && _mesh.Weights != null)
                        {
                            _mesh.MarkWeightsAsUsed();
                        }

                        if (_skin != null)
                        {
                            if (_mesh.Primitives.NonNull().All(primitive => primitive.JointsCount == 0))
                            {
                                context.AddIssue(LinkError.NodeSkinWithNonSkinnedMesh);
                            }
                        }
                        else
                        {
                            if (_mesh.Primitives.Any(primitive => primitive.JointsCount != 0))
                            {
                                context.AddIssue(LinkError.NodeSkinnedMeshWithoutSkin);
                            }
                        }
                    }
                }
            }
        }

        if (_childrenIndices != null)
        {
            _children = new Node?[_childrenIndices.Length];

            JsonUtils.ResolveNodeList(
                _childrenIndices, _children, gltf.Nodes, Members.CHILDREN, context,
                (node, nodeIndex, index) =>
                {
                    if (node._parent != null)
                    {
                        context.AddIssue(LinkError.NodeParentOverride, index: index, args: new object?[] { nodeIndex });
                    }
                    node._parent = this;
                });
        }
    }

    public void AddScene(Scene scene) => AddScene(scene, new HashSet<Node>(ReferenceEqualityComparer.Instance));

    private void AddScene(Scene scene, HashSet<Node> seenNodes)
    {
        if (!_scenes.Contains(scene))
        {
            _scenes.Add(scene);
        }
        if (_children == null || !seenNodes.Add(this))
        {
            return;
        }
        foreach (var node in _children)
        {
            node?.AddScene(scene, seenNodes);
        }
    }
}
