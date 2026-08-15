// Port of lib/src/data_access/validate_accessors.dart

namespace HxGLTF.Validator.Internal;

internal static class ValidateAccessors
{
    private static Accessor? GuardAccessor(Accessor? accessor)
    {
        if (accessor == null)
        {
            return null;
        }

        // Skip broken accessors
        if (accessor.Type == null ||
            accessor.ComponentType == -1 ||
            accessor.Count == -1)
        {
            return null;
        }

        // Skip empty accessors
        if (accessor.BufferView == null && accessor.Sparse == null)
        {
            return null;
        }

        return accessor;
    }

    /// <summary>Dart validateAccessorsData(gltf, context).</summary>
    public static void ValidateAccessorsData(Gltf gltf, Context context)
    {
        // Check sparse accessors data
        gltf.Accessors.ForEachWithIndices((i, accessor) =>
        {
            if (GuardAccessor(accessor)?.Sparse != null)
            {
                context.Path.Clear();
                context.Push(Members.ACCESSORS);
                context.Push(i);

                // Check sparse indices
                var view = accessor.Sparse!.IndicesTypedView;
                if (view != null)
                {
                    var index = 0;
                    long lastValue = -1;
                    foreach (var rawValue in view)
                    {
                        // Dart: sparse indices are unsigned integers
                        var value = (long)rawValue;
                        if (lastValue != -1 && value <= lastValue)
                        {
                            context.AddIssue(DataError.AccessorSparseIndicesNonIncreasing,
                                name: Members.SPARSE, args: new object?[] { index, value, lastValue });
                        }
                        if (value >= accessor.Count)
                        {
                            context.AddIssue(DataError.AccessorSparseIndexOob,
                                name: Members.SPARSE, args: new object?[] { index, value, accessor.Count });
                        }
                        lastValue = value;
                        ++index;
                    }
                }
            }
        });

        // Perform scheduled domain-specific element checks
        ProcessAccessorElements(context);

        // Validate skinning influences.
        // This requires a pre-pass to get effective joints limits and subsequent
        // complex simultaneous iteration over several accessors.
        var influencesIterators = new List<NumIterator>();
        var influencesCheckers = new List<InfluencesChecker>();

        context.Path.Clear();
        context.Push(Members.MESHES);

        for (var meshIndex = 0; meshIndex < gltf.Meshes.Length; meshIndex++)
        {
            var mesh = gltf.Meshes[meshIndex];

            // Broken mesh or mesh.primitives objects
            if (mesh?.Primitives == null)
            {
                continue;
            }

            // Skip meshes without joints data
            if (mesh.Primitives.NonNull().All(primitive => primitive.JointsCount == 0))
            {
                continue;
            }

            // Find the minimum number of skin.joints that is used with this mesh
            var maxJoints = -1;
            var limitingSkinIndex = -1;

            foreach (var node in gltf.Nodes)
            {
                if (node == null)
                {
                    continue;
                }
                if (node.Mesh == mesh && node.Skin?.Joints != null)
                {
                    var jointsCount = node.Skin.Joints.Length;
                    if (maxJoints == -1 || jointsCount < maxJoints)
                    {
                        maxJoints = jointsCount;
                        limitingSkinIndex = IndexOf(gltf.Skins, node.Skin);
                    }
                }
            }

            // skip the mesh if all skins are broken
            if (maxJoints < 1)
            {
                continue;
            }

            context.Push(meshIndex);
            context.Push(Members.PRIMITIVES);

            mesh.Primitives.ForEachWithIndices((primitiveIndex, primitive) =>
            {
                var skipValidation = false;
                var vertexCount = primitive.VertexCount;

                // joints/weights pair covers a subset of primitive influences
                var jointsIterators = new NumIterator?[primitive.JointsCount];
                var weightsIterators = new NumIterator?[primitive.JointsCount];
                for (var i = 0; i < primitive.JointsCount; i++)
                {
                    var jointsAccessor = GuardAccessor(GetAttribute(primitive, Members.JOINTS_ + "_" + i));
                    var weightsAccessor = GuardAccessor(GetAttribute(primitive, Members.WEIGHTS_ + "_" + i));

                    // skip when accessors are broken or have wrong count
                    if (jointsAccessor?.Count != vertexCount ||
                        weightsAccessor?.Count != vertexCount)
                    {
                        skipValidation = true;
                        break;
                    }

                    jointsIterators[i] = new NumIterator(jointsAccessor!.GetElements());
                    weightsIterators[i] = new NumIterator(weightsAccessor!.GetElementsNormalized());
                }

                // skip primitive
                if (skipValidation)
                {
                    return;
                }

                context.Push(primitiveIndex);
                context.Push(Members.ATTRIBUTES);

                // add iterators from the current primitive to the global list
                influencesIterators.AddRange(jointsIterators!);
                influencesIterators.AddRange(weightsIterators!);

                // add a checker from the current primitive to the global list
                influencesCheckers.Add(new InfluencesChecker(context.GetPointerString(),
                    jointsIterators!, weightsIterators!, maxJoints - 1, limitingSkinIndex));

                context.Pop();
                context.Pop();
            });
            context.Pop();
            context.Pop();
        }
        context.Pop();

        // Skip the final loops
        if (influencesIterators.Count == 0)
        {
            return;
        }

        while (StepInfluencesIterators(influencesIterators))
        {
            foreach (var checker in influencesCheckers)
            {
                if (!checker.IsDone)
                {
                    checker.CheckNext(context);
                }
            }
        }
    }

    private static Accessor? GetAttribute(MeshPrimitive primitive, string semantic) =>
        primitive.Attributes.TryGetValue(semantic, out var accessor) ? accessor : null;

    private static int IndexOf(SafeList<Skin> skins, Skin skin)
    {
        for (var i = 0; i < skins.Length; i++)
        {
            if (ReferenceEquals(skins[i], skin))
            {
                return i;
            }
        }
        return -1;
    }

    private static bool StepInfluencesIterators(List<NumIterator> influencesIterators)
    {
        // step all iterators
        foreach (var iterator in influencesIterators)
        {
            iterator.MoveNext();
        }

        // remove finished
        influencesIterators.RemoveAll(iterator => iterator.Current == null);

        return influencesIterators.Count > 0;
    }

    private static void ProcessAccessorElements(Context context)
    {
        foreach (var entry in context.AccessorElementCheckers)
        {
            var accessor = GuardAccessor(entry.Key);
            if (accessor == null)
            {
                continue;
            }

            var components = accessor.Components;
            var elementCheckers = entry.Value;

            context.Path.Clear();

            var index = 0;
            var componentIndex = 0;
            var notEmpty = false;

            foreach (var value in accessor.GetElements())
            {
                for (var t = 0; t < elementCheckers.Count; t++)
                {
                    elementCheckers[t].Check(context, index, componentIndex, value);
                }

                if (++componentIndex == components)
                {
                    componentIndex = 0;
                }
                ++index;
                notEmpty = true;
            }

            if (notEmpty)
            {
                for (var t = 0; t < elementCheckers.Count; t++)
                {
                    elementCheckers[t].Done(context);
                }
            }
        }
    }
}

/// <summary>Dart Iterator&lt;num&gt; semantics: <see cref="Current"/> is null before the first and after the last element.</summary>
internal sealed class NumIterator
{
    private readonly IEnumerator<double> _enumerator;
    private bool _finished;

    public NumIterator(IEnumerable<double> source)
    {
        _enumerator = source.GetEnumerator();
    }

    public double? Current { get; private set; }

    public bool MoveNext()
    {
        if (!_finished && _enumerator.MoveNext())
        {
            Current = _enumerator.Current;
            return true;
        }
        _finished = true;
        Current = null;
        return false;
    }
}

internal sealed class InfluencesChecker
{
    public readonly NumIterator[] JointsIterators;
    public readonly NumIterator[] WeightsIterators;
    public readonly int MaxJointIndex;
    public readonly int LimitingSkinIndex;
    public readonly string Path;

    private int _index;
    private int _componentIndex;

    public bool IsDone => _done;
    private bool _done;

    private double _sum;
    private double _threshold;
    private readonly HashSet<long> _currentIndices = new();

    public InfluencesChecker(string path, NumIterator[] jointsIterators, NumIterator[] weightsIterators,
        int maxJointIndex, int limitingSkinIndex)
    {
        Path = path;
        JointsIterators = jointsIterators;
        WeightsIterators = weightsIterators;
        MaxJointIndex = maxJointIndex;
        LimitingSkinIndex = limitingSkinIndex;
    }

    public void CheckNext(Context context)
    {
        for (var i = 0; i < JointsIterators.Length; ++i)
        {
            var jointValue = JointsIterators[i].Current;

            if (jointValue == null)
            {
                // all iterators for the same primitive must yield the same
                // amount of elements
                _done = true;
                return;
            }

            // Dart: joints accessors are integer accessors, values print as ints
            var joint = (long)jointValue.Value;

            if (joint > MaxJointIndex)
            {
                context.AddIssue(DataError.AccessorJointsIndexOob,
                    name: Path + "/" + Members.JOINTS_ + "_" + i,
                    args: new object?[]
                    {
                        _index,
                        _componentIndex,
                        joint,
                        MaxJointIndex,
                        LimitingSkinIndex
                    });
                continue;
            }

            var weightValue = WeightsIterators[i].Current;

            if (weightValue == null)
            {
                // insufficient weights data
                _done = true;
                return;
            }

            var weight = weightValue.Value;

            if (weight != 0)
            {
                var unique = true;
                if (!_currentIndices.Add(joint))
                {
                    context.AddIssue(DataError.AccessorJointsIndexDuplicate,
                        name: Path + "/" + Members.JOINTS_ + "_" + i,
                        args: new object?[] { _index, _componentIndex, joint });
                    unique = false;
                }

                if (weight < 0)
                {
                    context.AddIssue(DataError.AccessorWeightsNegative,
                        name: Path + "/" + Members.WEIGHTS_ + "_" + i,
                        args: new object?[] { _index, _componentIndex, weight });
                }
                else if (unique)
                {
                    // keep sum within float32 precision
                    _sum = JsonUtils.DoubleToSingle(_sum + weight);
                    _threshold += 2e-7;
                }
            }
            else if (joint != 0)
            {
                context.AddIssue(DataError.AccessorJointsUsedZeroWeight,
                    name: Path + "/" + Members.JOINTS_ + "_" + i,
                    args: new object?[] { _index, _componentIndex, joint });
            }
        }

        if (4 == ++_componentIndex)
        {
            if (Math.Abs(_sum - 1.0) > _threshold)
            {
                for (var i = 0; i < JointsIterators.Length; i++)
                {
                    context.AddIssue(DataError.AccessorWeightsNonNormalized,
                        name: Path + "/" + Members.WEIGHTS_ + "_" + i, args: new object?[] { _index - 3, _index, _sum });
                }
            }
            _currentIndices.Clear();
            _componentIndex = 0;
            _threshold = 0;
            _sum = 0;
        }
        _index++;
    }
}
