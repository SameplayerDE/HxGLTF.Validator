// Port of lib/src/base/mesh.dart
// The checker class IndexBufferIntegerChecker defined in mesh.dart lives in Internal/Data/ElementCheckers.cs.
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Mesh : GltfChildOfRootProperty
{
    public readonly SafeList<MeshPrimitive>? Primitives;
    public readonly double[]? Weights;

    private bool _weightsUsed;

    public bool AreWeightsUsed => _weightsUsed;

    private Mesh(SafeList<MeshPrimitive>? primitives, double[]? weights, string? name,
        Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        Primitives = primitives;
        Weights = weights;
    }

    public static Mesh FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.MESH_MEMBERS, context);
        }

        var weights = JsonUtils.GetFloatList(map, Members.WEIGHTS, context);

        var primitivesMaps = JsonUtils.GetMapList(map, Members.PRIMITIVES, context);

        SafeList<MeshPrimitive>? primitives = null;
        if (primitivesMaps != null)
        {
            primitives = new SafeList<MeshPrimitive>(primitivesMaps.Count, Members.PRIMITIVES);

            context.Push(Members.PRIMITIVES);
            var targetCount = 0;
            for (var i = 0; i < primitivesMaps.Count; i++)
            {
                context.Push(i);
                var primitive = MeshPrimitive.FromMap(primitivesMaps[i], context);
                if (context.Validate)
                {
                    var primitiveTargetCount = primitive.TargetsIndices?.Count ?? 0;
                    if (i == 0)
                    {
                        targetCount = primitiveTargetCount;
                    }
                    else if (targetCount != primitiveTargetCount)
                    {
                        context.AddIssue(SemanticError.MeshPrimitivesUnequalTargetsCount,
                            name: primitiveTargetCount > 0 ? Members.TARGETS : null);
                    }
                }
                primitives[i] = primitive;
                context.Pop();
            }
            context.Pop();

            if (context.Validate &&
                weights != null &&
                targetCount != weights.Length)
            {
                context.AddIssue(SemanticError.MeshInvalidWeightsCount,
                    name: Members.WEIGHTS, args: new object?[] { weights.Length, targetCount });
            }
        }

        return new Mesh(primitives, weights, JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Mesh), context), JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        context.Push(Members.PRIMITIVES);
        Primitives?.ForEachWithIndices((i, primitive) =>
        {
            context.Push(i);

            context.Push(Members.EXTENSIONS);
            foreach (var (key, value) in primitive.Extensions)
            {
                if (value is ILinkable linkable)
                {
                    context.Push(key);
                    linkable.Link(gltf, context);
                    context.Pop();
                }
            }
            context.Pop();

            primitive.Link(gltf, context);
            context.Pop();
        });
        context.Pop();
    }

    public void MarkWeightsAsUsed()
    {
        _weightsUsed = true;
    }
}

internal sealed class MeshPrimitive : GltfProperty
{
    private readonly List<KeyValuePair<string, int>>? _attributesIndices;
    private readonly int _indicesIndex;
    private readonly int _materialIndex;
    public readonly int Mode;
    private readonly List<List<KeyValuePair<string, int>>>? _targetsIndices;

    /// <summary>Dart <c>_targetsIndices</c> (read by Mesh.fromMap for the targets count check).</summary>
    internal List<List<KeyValuePair<string, int>>>? TargetsIndices => _targetsIndices;

    public readonly bool HasPosition;
    public readonly bool HasNormal;
    public readonly bool HasTangent;
    public readonly int ColorCount;
    public readonly int JointsCount;
    public readonly int WeightsCount;
    public readonly int TexCoordCount;

    public readonly Dictionary<string, Accessor> Attributes = new(StringComparer.Ordinal);

    private int _count = -1;
    private int _vertexCount = -1;
    private List<Dictionary<string, Accessor?>>? _targets;
    private Accessor? _indices;
    private Material? _material;

    private readonly int[] _unusedTexCoords;

    private MeshPrimitive(
        List<KeyValuePair<string, int>>? attributesIndices,
        int indicesIndex,
        int materialIndex,
        int mode,
        List<List<KeyValuePair<string, int>>>? targetsIndices,
        bool hasPosition,
        bool hasNormal,
        bool hasTangent,
        int colorCount,
        int jointsCount,
        int weightsCount,
        int texCoordCount,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(extensions, extras)
    {
        _attributesIndices = attributesIndices;
        _indicesIndex = indicesIndex;
        _materialIndex = materialIndex;
        Mode = mode;
        _targetsIndices = targetsIndices;
        HasPosition = hasPosition;
        HasNormal = hasNormal;
        HasTangent = hasTangent;
        ColorCount = colorCount;
        JointsCount = jointsCount;
        WeightsCount = weightsCount;
        TexCoordCount = texCoordCount;
        _unusedTexCoords = new int[texCoordCount];
        for (var i = 0; i < texCoordCount; i++) _unusedTexCoords[i] = i;
    }

    public static MeshPrimitive FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.MESH_PRIMITIVE_MEMBERS, context);
        }

        var hasPosition = false;
        var hasNormal = false;
        var hasTangent = false;

        var colorCount = 0;
        var maxColor = -1;
        var jointsCount = 0;
        var maxJoints = -1;
        var weightsCount = 0;
        var maxWeights = -1;
        var texCoordCount = 0;
        var maxTexcoord = -1;

        static int ParseAttributeSemanticIndex(string codeUnits)
        {
            if (codeUnits.Length == 0 || codeUnits.Length > 1 && codeUnits[0] == 0x30)
            {
                return -1;
            }

            var index = 0;
            for (var i = 0; i < codeUnits.Length; ++i)
            {
                var digit = codeUnits[i] - 0x30;
                if (digit > 9 || digit < 0)
                {
                    return -1;
                }
                index = 10 * index + digit;
            }
            return index;
        }

        void CheckAttributeSemanticName(string semantic)
        {
            // Skip on custom semantics
            if (semantic.Length > 0 && semantic[0] == 95 /*underscore*/)
            {
                return;
            }

            switch (semantic)
            {
                case Members.POSITION:
                    hasPosition = true;
                    break;
                case Members.NORMAL:
                    hasNormal = true;
                    break;
                case Members.TANGENT:
                    hasTangent = true;
                    break;
                default:
                    var semParts = semantic.Split('_');
                    var arraySemantic = semParts[0];

                    if (!Members.ATTRIBUTE_SEMANTIC_ARRAY_MEMBERS.Contains(arraySemantic) ||
                        semParts.Length != 2)
                    {
                        context.AddIssue(SemanticError.MeshPrimitiveInvalidAttribute, name: semantic);
                        break;
                    }

                    var index = ParseAttributeSemanticIndex(semParts[1]);
                    if (index != -1)
                    {
                        switch (arraySemantic)
                        {
                            case Members.COLOR_:
                                colorCount++;
                                maxColor = index > maxColor ? index : maxColor;
                                break;
                            case Members.JOINTS_:
                                jointsCount++;
                                maxJoints = index > maxJoints ? index : maxJoints;
                                break;
                            case Members.TEXCOORD_:
                                texCoordCount++;
                                maxTexcoord = index > maxTexcoord ? index : maxTexcoord;
                                break;
                            case Members.WEIGHTS_:
                                weightsCount++;
                                maxWeights = index > maxWeights ? index : maxWeights;
                                break;
                        }
                    }
                    else
                    {
                        context.AddIssue(SemanticError.MeshPrimitiveInvalidAttribute, name: semantic);
                    }
                    break;
            }
        }

        var mode = JsonUtils.GetUint(map, Members.MODE, context,
            min: Gl.POINTS, max: Gl.TRIANGLE_FAN, def: Gl.TRIANGLES);

        var attributes = JsonUtils.GetIndicesMap(map, Members.ATTRIBUTES, context, CheckAttributeSemanticName);

        if (attributes != null)
        {
            context.Push(Members.ATTRIBUTES);
            if (!hasPosition)
            {
                context.AddIssue(SemanticError.MeshPrimitiveNoPosition);
            }

            if (!hasNormal && hasTangent)
            {
                context.AddIssue(SemanticError.MeshPrimitiveTangentWithoutNormal, name: Members.TANGENT);
            }

            // Check for indexed semantics continuity -
            // they must start with zero and do not have gaps.
            // Otherwise, the semantic will be completely ignored.
            int CheckContinuity(int maxIndex, int count, string name)
            {
                if (maxIndex + 1 != count)
                {
                    context.AddIssue(SemanticError.MeshPrimitiveIndexedSemanticContinuity,
                        args: new object?[] { name, maxIndex + 1, count });
                    return 0;
                }
                return count;
            }

            colorCount = CheckContinuity(maxColor, colorCount, Members.COLOR_);
            jointsCount = CheckContinuity(maxJoints, jointsCount, Members.JOINTS_);
            weightsCount = CheckContinuity(maxWeights, weightsCount, Members.WEIGHTS_);
            texCoordCount = CheckContinuity(maxTexcoord, texCoordCount, Members.TEXCOORD_);

            if (jointsCount != weightsCount)
            {
                context.AddIssue(SemanticError.MeshPrimitiveJointsWeightsMismatch,
                    args: new object?[] { jointsCount, weightsCount });

                // Block joints data from further processing
                jointsCount = 0;
                weightsCount = 0;
            }

            context.Pop();
        }

        void CheckMorphTargetAttributeSemanticName(string semantic)
        {
            // Skip custom semantics
            if (semantic.Length > 0 && semantic[0] == 95 /*underscore*/)
            {
                return;
            }

            if (Members.ATTRIBUTE_SEMANTIC_MEMBERS.Contains(semantic))
            {
                return;
            }

            var semParts = semantic.Split('_');
            if (!Members.ATTRIBUTE_SEMANTIC_MORPH_TARGET_ARRAY_MEMBERS.Contains(semParts[0]) ||
                semParts.Length != 2 ||
                ParseAttributeSemanticIndex(semParts[1]) == -1)
            {
                context.AddIssue(SemanticError.MeshPrimitiveInvalidAttribute, name: semantic);
            }
        }

        var targets = JsonUtils.GetIndicesMapsList(map, Members.TARGETS, context, CheckMorphTargetAttributeSemanticName);

        var primitive = new MeshPrimitive(
            attributes,
            JsonUtils.GetIndex(map, Members.INDICES, context, req: false),
            JsonUtils.GetIndex(map, Members.MATERIAL, context, req: false),
            mode,
            targets,
            hasPosition,
            hasNormal,
            hasTangent,
            colorCount,
            jointsCount,
            weightsCount,
            texCoordCount,
            JsonUtils.GetExtensions(map, typeof(MeshPrimitive), context),
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(primitive, primitive.Extensions.Values);

        return primitive;
    }

    public int Count => _count;
    public int VertexCount => _vertexCount;
    public List<Dictionary<string, Accessor?>>? Targets => _targets;
    public Accessor? Indices => _indices;
    public Material? Material => _material;

    public int TrianglesCount
    {
        get
        {
            switch (Mode)
            {
                case Gl.TRIANGLES:
                    return Count / 3;
                case Gl.TRIANGLE_STRIP:
                case Gl.TRIANGLE_FAN:
                    return Count > 2 ? Count - 2 : 0;
                default:
                    return 0;
            }
        }
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (_attributesIndices != null)
        {
            context.Push(Members.ATTRIBUTES);
            foreach (var (semantic, accessorIndex) in _attributesIndices)
            {
                if (accessorIndex == -1)
                {
                    continue;
                }

                var accessor = gltf.Accessors[accessorIndex];

                if (accessor == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: semantic, args: new object?[] { accessorIndex });
                    continue;
                }

                Attributes[semantic] = accessor;

                accessor.SetUsage(AccessorUsage.VertexAttribute, semantic, context);
                accessor.BufferView?.SetUsage(BufferViewUsage.VertexBuffer, semantic, context);

                if (context.Validate)
                {
                    if (semantic == Members.POSITION &&
                        (accessor.Min == null || accessor.Max == null))
                    {
                        context.AddIssue(LinkError.MeshPrimitivePositionAccessorWithoutBounds, name: Members.POSITION);
                    }

                    var format = AccessorFormat.FromAccessor(accessor);
                    context.AttributeAccessorFormats.TryGetValue(semantic.Split('_')[0], out var validFormats);
                    if (validFormats != null)
                    {
                        if (!validFormats.Contains(format))
                        {
                            context.AddIssue(LinkError.MeshPrimitiveAttributesAccessorInvalidFormat,
                                name: semantic,
                                args: new object?[] { format, validFormats });
                        }
                        else
                        {
                            if (semantic == Members.NORMAL)
                            {
                                accessor.SetUnit();
                                context.Push(Members.NORMAL);
                                context.AddElementChecker(
                                    accessor,
                                    new UnitVec3FloatChecker(context.GetPointerString(),
                                        accessor.IsFloat ? null : accessor.NormalizeValue));
                                context.Pop();
                            }
                            else if (semantic == Members.TANGENT)
                            {
                                accessor.SetUnit();
                                accessor.SetXyzSign();
                                context.Push(Members.TANGENT);
                                context.AddElementChecker(
                                    accessor,
                                    new UnitVec3SignFloatChecker(context.GetPointerString(),
                                        accessor.IsFloat ? null : accessor.NormalizeValue));
                                context.Pop();
                            }
                            else if (semantic == Members.COLOR_ + "_0" &&
                                     Gl.FLOAT == accessor.ComponentType)
                            {
                                accessor.SetClamped();
                                context.Push(semantic);
                                context.AddElementChecker(accessor,
                                    new ClampedRangeFloatChecker(context.GetPointerString()));
                                context.Pop();
                            }
                        }
                    }
                    else if (accessor.ComponentType == Gl.UNSIGNED_INT)
                    {
                        context.AddIssue(LinkError.MeshPrimitiveAttributesAccessorUnsignedInt, name: semantic);
                    }

                    if ((accessor.ByteOffset != -1 &&
                         accessor.ByteOffset % 4 != 0) ||
                        (accessor.ElementLength % 4 != 0 &&
                         accessor.BufferView != null &&
                         accessor.BufferView.ByteStride == -1))
                    {
                        context.AddIssue(LinkError.MeshPrimitiveAccessorUnaligned, name: semantic);
                    }
                }

                // Mandatory checks even with disabled
                // validation to always set `effectiveByteStride` and `count`

                if (_vertexCount == -1)
                {
                    _vertexCount = accessor.Count;
                    _count = _vertexCount;
                }
                else if (_vertexCount != accessor.Count)
                {
                    context.AddIssue(LinkError.MeshPrimitiveUnequalAccessorsCount, name: semantic);
                }

                if (accessor.BufferView != null &&
                    accessor.BufferView.ByteStride == -1)
                {
                    if (accessor.BufferView.EffectiveByteStride == -1)
                    {
                        accessor.BufferView.EffectiveByteStride = accessor.ElementLength;
                    }

                    CheckAccessorRefs(accessor, semantic, context);
                }
            }
            context.Pop();
        }

        if (_indicesIndex != -1)
        {
            _indices = gltf.Accessors[_indicesIndex];

            if (_indices == null)
            {
                context.AddIssue(LinkError.UnresolvedReference, name: Members.INDICES, args: new object?[] { _indicesIndex });
            }
            else
            {
                _count = _indices.Count;

                _indices.SetUsage(AccessorUsage.PrimitiveIndices, Members.INDICES, context);
                _indices.BufferView?.SetUsage(BufferViewUsage.IndexBuffer, Members.INDICES, context);

                if (context.Validate)
                {
                    context.Push(Members.INDICES);
                    if (_indices.BufferView != null &&
                        _indices.BufferView.ByteStride != -1)
                    {
                        context.AddIssue(LinkError.MeshPrimitiveIndicesAccessorWithByteStride);
                    }

                    var format = AccessorFormat.FromAccessor(_indices);
                    if (!Members.MESH_PRIMITIVE_INDICES_FORMATS.Contains(format))
                    {
                        context.AddIssue(LinkError.MeshPrimitiveIndicesAccessorInvalidFormat,
                            args: new object?[] { format, Members.MESH_PRIMITIVE_INDICES_FORMATS });
                    }
                    else
                    {
                        var maxVertexIndex = VertexCount != -1 ? VertexCount - 1 : -1;
                        var modesMask = Mode != -1 ? 1 << Mode : -1;

                        if (modesMask != 0 && maxVertexIndex >= -1)
                        {
                            context.AddElementChecker(
                                _indices,
                                new IndexBufferIntegerChecker(
                                    path: context.GetPointerString(),
                                    maxVertexIndex: maxVertexIndex,
                                    totalTriangles: _count / 3,
                                    modesMask: modesMask,
                                    componentType: _indices.ComponentType));
                        }
                    }
                    context.Pop();
                }
            }
        }

        /*
        LINES = 1;
        LINE_LOOP = 2;
        LINE_STRIP = 3;
        TRIANGLES = 4;
        TRIANGLE_STRIP = 5
        TRIANGLE_FAN = 6;
        */

        if ((context.Validate && _count != -1) &&
            ((Mode == 1 && _count % 2 != 0) ||
             ((Mode == 2 || Mode == 3) && _count < 2) ||
             (Mode == 4 && _count % 3 != 0) ||
             ((Mode == 5 || Mode == 6) && _count < 3)))
        {
            context.AddIssue(LinkError.MeshPrimitiveIncompatibleMode,
                args: new object?[] { _count, Gl.MODES_NAMES[Mode] });
        }

        _material = gltf.Materials[_materialIndex];

        if (context.Validate)
        {
            if (_materialIndex != -1)
            {
                if (_material == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: Members.MATERIAL, args: new object?[] { _materialIndex });
                }
                else
                {
                    _material.MarkAsUsed();

                    if (!(HasNormal && HasTangent) && _material.NeedsTangent)
                    {
                        context.AddIssue(
                            _material.CanProvideTangent
                                ? LinkError.MeshPrimitiveGeneratedTangentSpace
                                : LinkError.MeshPrimitiveNoTangentSpace,
                            name: Members.MATERIAL);
                    }

                    foreach (var (pointer, texCoord) in _material.TexCoordIndices)
                    {
                        if (texCoord != -1)
                        {
                            if (texCoord + 1 > TexCoordCount)
                            {
                                context.AddIssue(LinkError.MeshPrimitiveTooFewTexcoords,
                                    name: Members.MATERIAL, args: new object?[] { pointer, texCoord });
                            }
                            else
                            {
                                MarkUsedTexCoord(texCoord);
                            }
                        }
                    }
                }
            }

            if (HasTangent && (_material == null || !_material.NeedsTangent))
            {
                context.Push(Members.ATTRIBUTES);
                context.AddIssue(LinkError.UnusedMeshTangent, name: Members.TANGENT);
                context.Pop();
            }

            foreach (var unusedIndex in _unusedTexCoords.Where(i => i != -1))
            {
                context.Push(Members.ATTRIBUTES);
                context.AddIssue(LinkError.UnusedObject, name: Members.TEXCOORD_ + "_" + unusedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                context.Pop();
            }
        }

        if (_targetsIndices != null)
        {
            context.Push(Members.TARGETS);
            _targets = new List<Dictionary<string, Accessor?>>(_targetsIndices.Count);
            for (var i = 0; i < _targetsIndices.Count; i++)
            {
                _targets.Add(new Dictionary<string, Accessor?>(StringComparer.Ordinal));
            }

            for (var i = 0; i < _targetsIndices.Count; i++)
            {
                var targetIndices = _targetsIndices[i];

                context.Push(i);
                foreach (var (semantic, accessorIndex) in targetIndices)
                {
                    if (accessorIndex == -1)
                    {
                        continue;
                    }

                    var accessor = gltf.Accessors[accessorIndex];

                    if (accessor == null)
                    {
                        context.AddIssue(LinkError.UnresolvedReference, name: semantic, args: new object?[] { accessorIndex });
                    }
                    else
                    {
                        if (context.Validate)
                        {
                            accessor.SetUsage(AccessorUsage.VertexAttribute, semantic, context);
                            accessor.BufferView?.SetUsage(BufferViewUsage.VertexBuffer, semantic, context);

                            Attributes.TryGetValue(semantic, out var baseAccessor);

                            if (baseAccessor == null)
                            {
                                context.AddIssue(LinkError.MeshPrimitiveMorphTargetNoBaseAccessor, name: semantic);
                            }
                            else
                            {
                                if (baseAccessor.Count != accessor.Count)
                                {
                                    context.AddIssue(LinkError.MeshPrimitiveMorphTargetInvalidAttributeCount, name: semantic);
                                }
                            }

                            if (semantic == Members.POSITION &&
                                ((accessor.Min == null) || accessor.Max == null))
                            {
                                context.AddIssue(LinkError.MeshPrimitivePositionAccessorWithoutBounds, name: Members.POSITION);
                            }

                            var format = AccessorFormat.FromAccessor(accessor);
                            context.MorphAttributeAccessorFormats.TryGetValue(semantic.Split('_')[0], out var validFormats);

                            if (validFormats != null && !validFormats.Contains(format))
                            {
                                context.AddIssue(LinkError.MeshPrimitiveAttributesAccessorInvalidFormat,
                                    name: semantic,
                                    args: new object?[] { format, validFormats });
                            }

                            if ((accessor.ByteOffset != -1 &&
                                 accessor.ByteOffset % 4 != 0) ||
                                (accessor.ElementLength % 4 != 0 &&
                                 accessor.BufferView != null &&
                                 accessor.BufferView.ByteStride == -1))
                            {
                                context.AddIssue(LinkError.MeshPrimitiveAccessorUnaligned, name: semantic);
                            }
                        }

                        // Mandatory checks even with disabled
                        // validation to always set `effectiveByteStride`

                        if (accessor.BufferView != null &&
                            accessor.BufferView.ByteStride == -1)
                        {
                            if (accessor.BufferView.EffectiveByteStride == -1)
                            {
                                accessor.BufferView.EffectiveByteStride = accessor.ElementLength;
                            }

                            CheckAccessorRefs(accessor, semantic, context);
                        }
                    }

                    _targets[i][semantic] = accessor;
                }
                context.Pop();
            }
            context.Pop();
        }
    }

    public void MarkUsedTexCoord(int texCoord)
    {
        _unusedTexCoords[texCoord] = -1;
    }

    private static void CheckAccessorRefs(Accessor accessor, string semantic, Context context)
    {
        var bufferView = accessor.BufferView!;
        if (bufferView.ByteStride == -1)
        {
            if (!context.BufferViewAccessors.TryGetValue(bufferView, out var accessors))
            {
                accessors = new HashSet<Accessor>(ReferenceEqualityComparer.Instance);
                context.BufferViewAccessors[bufferView] = accessors;
            }
            if (accessors.Add(accessor) && accessors.Count > 1)
            {
                context.AddIssue(LinkError.MeshPrimitiveAccessorWithoutByteStride, name: semantic);
            }
        }
    }
}
