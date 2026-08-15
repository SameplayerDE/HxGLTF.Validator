// Port of lib/src/base/accessor.dart
using System.Buffers.Binary;
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

/// <summary>
/// Dart typed-data view (Uint8List/Int16List/Float32List/... .view(buffer, offset, length)) over a byte array.
/// Components are returned as double: exact for all integer component types, float widened for FLOAT.
/// </summary>
internal sealed class AccessorTypedView
{
    private readonly byte[] _data;
    private readonly long _offsetInBytes;
    private readonly int _componentType;
    private readonly int _componentLength;
    public readonly int Length;

    public AccessorTypedView(int componentType, byte[] data, long offsetInBytes, int length)
    {
        _componentType = componentType;
        _data = data;
        _offsetInBytes = offsetInBytes;
        _componentLength = Gl.ComponentTypeLength(componentType);
        Length = length;
    }

    public double this[int index]
    {
        get
        {
            var span = new ReadOnlySpan<byte>(_data, checked((int)(_offsetInBytes + (long)index * _componentLength)), _componentLength);
            switch (_componentType)
            {
                case Gl.BYTE: return (sbyte)span[0];
                case Gl.UNSIGNED_BYTE: return span[0];
                case Gl.SHORT: return BinaryPrimitives.ReadInt16LittleEndian(span);
                case Gl.UNSIGNED_SHORT: return BinaryPrimitives.ReadUInt16LittleEndian(span);
                case Gl.INT: return BinaryPrimitives.ReadInt32LittleEndian(span);
                case Gl.UNSIGNED_INT: return BinaryPrimitives.ReadUInt32LittleEndian(span);
                case Gl.FLOAT: return BinaryPrimitives.ReadSingleLittleEndian(span);
                default: throw new InvalidOperationException("Unsupported component type " + _componentType);
            }
        }
    }
}

internal abstract class Accessor : GltfChildOfRootProperty
{
    private readonly int _bufferViewIndex;
    public readonly int ByteOffset;
    public readonly int ComponentType;
    public readonly int Count;
    public readonly string? Type;
    public readonly bool Normalized;
    /// <summary>Dart List&lt;T&gt; max: null when absent or invalid; integer accessors hold exact integer values.</summary>
    public readonly double[]? Max;
    /// <summary>Dart List&lt;T&gt; min: null when absent or invalid; integer accessors hold exact integer values.</summary>
    public readonly double[]? Min;
    public readonly AccessorSparse? Sparse;

    public readonly int ComponentLength;

    private BufferView? _bufferView;
    private int _byteStride;
    private bool _isUnit;
    private bool _isClamped;
    private bool _isXyzSign;
    private bool? _containsCubicSpline;
    private AccessorUsage? _usage;

    protected Accessor(int bufferViewIndex, int byteOffset, int componentType, int count, string? type, bool normalized,
        double[]? max, double[]? min, AccessorSparse? sparse, string? name, Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        _bufferViewIndex = bufferViewIndex;
        ByteOffset = byteOffset;
        ComponentType = componentType;
        Count = count;
        Type = type;
        Normalized = normalized;
        Max = max;
        Min = min;
        Sparse = sparse;
        ComponentLength = Gl.ComponentTypeLength(componentType);
    }

    private bool IsMatrixWithGaps =>
        ((ComponentType == Gl.UNSIGNED_BYTE || ComponentType == Gl.BYTE) &&
         (Type == Members.MAT2 || Type == Members.MAT3)) ||
        ((ComponentType == Gl.UNSIGNED_SHORT || ComponentType == Gl.SHORT) &&
         Type == Members.MAT3);

    public BufferView? BufferView => _bufferView;

    /// <summary>Dart <c>ACCESSOR_TYPES_LENGTHS[type] ?? 0</c>.</summary>
    public int Components => Type != null && Members.ACCESSOR_TYPES_LENGTHS.TryGetValue(Type, out var l) ? l : 0;

    public int ElementLength
    {
        get
        {
            // TODO: generalize to non-square matrices
            if (ComponentType == Gl.UNSIGNED_BYTE || ComponentType == Gl.BYTE)
            {
                if (Type == Members.MAT2)
                {
                    return 6;
                }
                else if (Type == Members.MAT3)
                {
                    return 11;
                }
                return Components;
            }
            else if (ComponentType == Gl.UNSIGNED_SHORT || ComponentType == Gl.SHORT)
            {
                if (Type == Members.MAT3)
                {
                    return 22;
                }
                return 2 * Components;
            }
            // gl.FLOAT || gl.UNSIGNED_INT
            return 4 * Components;
        }
    }

    public int ByteStride
    {
        get
        {
            if (_byteStride != 0)
            {
                return _byteStride;
            }

            // TODO: generalize to non-square matrices
            if (ComponentType == Gl.UNSIGNED_BYTE || ComponentType == Gl.BYTE)
            {
                if (Type == Members.MAT2)
                {
                    return 8;
                }
                else if (Type == Members.MAT3)
                {
                    return 12;
                }
                return Components;
            }
            else if (ComponentType == Gl.UNSIGNED_SHORT || ComponentType == Gl.SHORT)
            {
                if (Type == Members.MAT3)
                {
                    return 24;
                }
                return 2 * Components;
            }
            // gl.FLOAT || gl.UNSIGNED_INT
            return 4 * Components;
        }
    }

    // Dart int is 64-bit; keep the product in long.
    public long ByteLength => (long)ByteStride * (Count - 1) + ElementLength;

    public bool IsFloat => Gl.FLOAT == ComponentType;
    public bool IsClamped => _isClamped;
    public bool IsUnit => _isUnit;
    public bool IsXyzSign => _isXyzSign;
    public bool ContainsCubicSpline => _containsCubicSpline == true;

    public AccessorUsage? Usage => _usage;

    public static Accessor FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.ACCESSOR_MEMBERS, context);
        }

        var bufferViewIndex = JsonUtils.GetIndex(map, Members.BUFFER_VIEW, context, req: false);

        var byteOffset = 0;
        if (bufferViewIndex == -1)
        {
            if (context.Validate && JsonUtils.Has(map, Members.BYTE_OFFSET))
            {
                context.AddIssue(SchemaError.UnsatisfiedDependency,
                    name: Members.BYTE_OFFSET, args: new object?[] { Members.BUFFER_VIEW });
            }
        }
        else
        {
            byteOffset = JsonUtils.GetUint(map, Members.BYTE_OFFSET, context, def: 0);
        }

        var componentType = JsonUtils.GetUint(map, Members.COMPONENT_TYPE, context,
            req: true, list: Gl.COMPONENT_TYPES);

        var count = JsonUtils.GetUint(map, Members.COUNT, context, req: true, min: 1);
        var type = JsonUtils.GetString(map, Members.TYPE, context,
            req: true, list: Members.ACCESSOR_TYPES_LENGTHS.Keys);

        var normalized = JsonUtils.GetBool(map, Members.NORMALIZED, context);

        // Dart: List<num> max / min (List<double> for FLOAT, List<int> otherwise)
        double[]? maxFloat = null;
        double[]? minFloat = null;
        long[]? maxInt = null;
        long[]? minInt = null;
        if (type != null && componentType != -1)
        {
            if (Members.ACCESSOR_TYPES_LENGTHS.TryGetValue(type, out var length))
            {
                if (componentType == Gl.FLOAT)
                {
                    minFloat = JsonUtils.GetFloatList(map, Members.MIN, context,
                        lengthsList: new[] { length }, singlePrecision: true);
                    maxFloat = JsonUtils.GetFloatList(map, Members.MAX, context,
                        lengthsList: new[] { length }, singlePrecision: true);
                }
                else
                {
                    minInt = JsonUtils.GetGlIntList(map, Members.MIN, context, componentType, length);
                    maxInt = JsonUtils.GetGlIntList(map, Members.MAX, context, componentType, length);
                }
            }
        }

        var sparse = JsonUtils.GetObjectFromInnerMap<AccessorSparse>(
            map, Members.SPARSE, context, (m, c) => AccessorSparse.FromMap(m, c)!);

        if (context.Validate)
        {
            if (normalized &&
                (componentType == Gl.FLOAT || componentType == Gl.UNSIGNED_INT))
            {
                context.AddIssue(SemanticError.AccessorNormalizedInvalid,
                    name: Members.NORMALIZED);
            }

            if ((type == Members.MAT2 || type == Members.MAT3 || type == Members.MAT4) &&
                byteOffset != -1 &&
                (byteOffset & 3) != 0)
            {
                context.AddIssue(SemanticError.AccessorMatrixAlignment,
                    name: Members.BYTE_OFFSET);
            }
        }

        Accessor accessor;

        switch (componentType)
        {
            case Gl.BYTE:
            case Gl.UNSIGNED_BYTE:
            case Gl.SHORT:
            case Gl.UNSIGNED_SHORT:
            case Gl.UNSIGNED_INT:
                accessor = new AccessorInt(
                    bufferViewIndex,
                    byteOffset,
                    componentType,
                    count,
                    type,
                    normalized,
                    ToDoubles(maxInt),
                    ToDoubles(minInt),
                    sparse,
                    JsonUtils.GetName(map, context),
                    JsonUtils.GetExtensions(map, typeof(Accessor), context),
                    JsonUtils.GetExtras(map, context));

                if (context.Validate)
                {
                    // accessor.min
                    if (minInt != null)
                    {
                        context.AddElementChecker(
                            accessor,
                            new MinIntegerChecker(context.GetPointerString(), minInt));
                    }

                    // accessor.max
                    if (maxInt != null)
                    {
                        context.AddElementChecker(
                            accessor,
                            new MaxIntegerChecker(context.GetPointerString(), maxInt));
                    }
                }
                break;
            default:
                accessor = new AccessorFloat(
                    bufferViewIndex,
                    byteOffset,
                    componentType,
                    count,
                    type,
                    normalized,
                    maxFloat,
                    minFloat,
                    sparse,
                    JsonUtils.GetName(map, context),
                    JsonUtils.GetExtensions(map, typeof(Accessor), context),
                    JsonUtils.GetExtras(map, context));

                if (context.Validate)
                {
                    // NaN or Infinity
                    context.AddElementChecker(
                        accessor, new InvalidFloatChecker(context.GetPointerString()));

                    // accessor.min
                    if (minFloat != null)
                    {
                        context.AddElementChecker(
                            accessor,
                            new MinFloatChecker(context.GetPointerString(), minFloat));
                    }

                    // accessor.max
                    if (maxFloat != null)
                    {
                        context.AddElementChecker(
                            accessor,
                            new MaxFloatChecker(context.GetPointerString(), maxFloat));
                    }
                }
                break;
        }

        return accessor;
    }

    private static double[]? ToDoubles(long[]? values)
    {
        if (values == null) return null;
        var result = new double[values.Length];
        for (int i = 0; i < values.Length; i++) result[i] = values[i];
        return result;
    }

    public override void Link(Gltf gltf, Context context)
    {
        _bufferView = gltf.BufferViews[_bufferViewIndex];

        if (_bufferView != null && _bufferView.ByteStride != -1)
        {
            _byteStride = _bufferView.ByteStride;
        }

        // Ensure required fields to not check for them each time
        if (ComponentType == -1 || Count == -1 || Type == null)
        {
            return;
        }

        // Check length and alignment when bufferView is present
        if (context.Validate && _bufferViewIndex != -1)
        {
            if (_bufferView == null)
            {
                context.AddIssue(LinkError.UnresolvedReference,
                    name: Members.BUFFER_VIEW, args: new object?[] { _bufferViewIndex });
            }
            else
            {
                _bufferView.MarkAsUsed();
                // Byte Stride
                if (_bufferView.ByteStride != -1 &&
                    _bufferView.ByteStride < ElementLength)
                {
                    context.AddIssue(LinkError.AccessorSmallStride,
                        args: new object?[] { _bufferView.ByteStride, ElementLength });
                }

                CheckByteOffsetAndLength(ByteOffset, ComponentLength, ByteLength,
                    _bufferView, _bufferViewIndex, context);
            }
        }

        if (Sparse != null)
        {
            if (Sparse.Count == -1 ||
                Sparse.Indices == null ||
                Sparse.Values == null)
            {
                return;
            }

            context.Push(Members.SPARSE);
            {
                if (context.Validate && Sparse.Count > Count)
                {
                    context.AddIssue(SemanticError.AccessorSparseCountOutOfRange,
                        name: Members.COUNT, args: new object?[] { Sparse.Count, Count });
                }

                Sparse.Values.Link(gltf, context);

                context.Push(Members.INDICES);
                {
                    var indices = Sparse.Indices;

                    if (indices.BufferViewIndex != -1)
                    {
                        Sparse.Indices.Link(gltf, context);

                        if (indices.BufferView == null)
                        {
                            context.AddIssue(LinkError.UnresolvedReference,
                                name: Members.BUFFER_VIEW, args: new object?[] { indices.BufferViewIndex });
                        }
                        else
                        {
                            indices.BufferView
                                .SetUsage(BufferViewUsage.Other, Members.BUFFER_VIEW, context);

                            if (context.Validate)
                            {
                                if (indices.BufferView.ByteStride != -1)
                                {
                                    context.AddIssue(SemanticError.BufferViewInvalidByteStride,
                                        name: Members.BUFFER_VIEW);
                                }

                                if (indices.ComponentType != -1)
                                {
                                    CheckByteOffsetAndLength(
                                        indices.ByteOffset,
                                        Gl.ComponentTypeLength(indices.ComponentType),
                                        (long)Gl.ComponentTypeLength(indices.ComponentType) *
                                            Sparse.Count,
                                        indices.BufferView,
                                        indices.BufferViewIndex,
                                        context);
                                }
                            }
                        }
                    }
                }
                context.Pop();
                context.Push(Members.VALUES);
                {
                    var values = Sparse.Values;

                    if (values.BufferViewIndex != -1)
                    {
                        if (values.BufferView == null)
                        {
                            context.AddIssue(LinkError.UnresolvedReference,
                                name: Members.BUFFER_VIEW, args: new object?[] { values.BufferViewIndex });
                        }
                        else
                        {
                            values.BufferView
                                .SetUsage(BufferViewUsage.Other, Members.BUFFER_VIEW, context);

                            if (context.Validate)
                            {
                                if (values.BufferView.ByteStride != -1)
                                {
                                    context.AddIssue(SemanticError.BufferViewInvalidByteStride,
                                        name: Members.BUFFER_VIEW);
                                }

                                CheckByteOffsetAndLength(
                                    values.ByteOffset,
                                    ComponentLength,
                                    (long)ComponentLength *
                                        Components *
                                        Sparse.Count,
                                    values.BufferView,
                                    values.BufferViewIndex,
                                    context);
                            }
                        }
                    }
                }
                context.Pop();
            }
            context.Pop();
        }
    }

    public void SetUsage(AccessorUsage value, string name, Context context)
    {
        MarkAsUsed();
        if (_usage == null)
        {
            _usage = value;
        }
        else if (context.Validate && _usage != value)
        {
            context.AddIssue(LinkError.AccessorUsageOverride,
                name: name, args: new object?[] { _usage, value });
        }
    }

    public void SetClamped() => _isClamped = true;

    public void SetUnit() => _isUnit = true;

    public void SetXyzSign() => _isXyzSign = true;

    public bool TrySetInterpolation(bool cubic = false)
    {
        if (_containsCubicSpline == null)
        {
            _containsCubicSpline = cubic;
        }
        else if (_containsCubicSpline != cubic)
        {
            return false;
        }
        return true;
    }

    /// <summary>Dart <c>Iterable&lt;T&gt; getElements()</c>: raw component values (integers exact).</summary>
    public abstract IEnumerable<double> GetElements();

    public abstract IEnumerable<double> GetElementsNormalized();

    /// <summary>Dart <c>double normalizeValue(num value)</c>.</summary>
    public double NormalizeValue(double value)
    {
        if (!Normalized || Gl.FLOAT == ComponentType)
        {
            return value;
        }

        var width = ComponentLength * 8;
        if (ComponentType == Gl.BYTE ||
            ComponentType == Gl.SHORT ||
            ComponentType == Gl.INT)
        {
            // Signed
            var divider = (1L << (width - 1)) - 1;
            return Math.Max(value / divider, -1);
        }
        else
        {
            // Unsigned
            var divider = (1L << width) - 1;
            return value / divider;
        }
    }

    // Dart: static bool _checkByteOffsetAndLength(int byteOffset, int componentLength, int byteLength,
    //   BufferView bufferView, [int _bufferViewIndex, Context context])
    internal static bool CheckByteOffsetAndLength(long byteOffset, int componentLength,
        long byteLength, BufferView bufferView,
        int bufferViewIndex = -1, Context? context = null)
    {
        // Local offset
        if (byteOffset == -1)
        {
            return false;
        }

        if (byteOffset % componentLength != 0)
        {
            if (context != null)
            {
                context.AddIssue(SemanticError.AccessorOffsetAlignment,
                    name: Members.BYTE_OFFSET, args: new object?[] { byteOffset, componentLength });
            }
            else
            {
                return false;
            }
        }

        // Total offset
        if (bufferView.ByteOffset == -1)
        {
            return false;
        }

        var totalOffset = bufferView.ByteOffset + byteOffset;
        if (totalOffset % componentLength != 0)
        {
            if (context != null)
            {
                context.AddIssue(LinkError.AccessorTotalOffsetAlignment,
                    args: new object?[] { totalOffset, componentLength });
            }
            else
            {
                return false;
            }
        }

        // Length
        if (byteOffset > bufferView.ByteLength)
        {
            if (context != null)
            {
                context.AddIssue(LinkError.AccessorTooLong, name: Members.BYTE_OFFSET, args: new object?[]
                {
                    byteOffset,
                    byteLength,
                    bufferViewIndex,
                    bufferView.ByteLength
                });
            }
            else
            {
                return false;
            }
        }
        else if (byteOffset + byteLength > bufferView.ByteLength)
        {
            if (context != null)
            {
                context.AddIssue(LinkError.AccessorTooLong, args: new object?[]
                {
                    byteOffset,
                    byteLength,
                    bufferViewIndex,
                    bufferView.ByteLength
                });
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private static bool ViewFits(int componentType, byte[]? buffer, long offsetInBytes, long length)
        => buffer != null &&
           buffer.Length >= offsetInBytes + (long)Gl.ComponentTypeLength(componentType) * length;

    /// <summary>Dart _typedViewIndices: unsigned integer views only.</summary>
    internal static AccessorTypedView? TypedViewIndices(int componentType, byte[]? buffer, long offsetInBytes, long length)
    {
        if (!ViewFits(componentType, buffer, offsetInBytes, length))
        {
            return null;
        }
        switch (componentType)
        {
            case Gl.UNSIGNED_BYTE:
            case Gl.UNSIGNED_SHORT:
            case Gl.UNSIGNED_INT:
                return new AccessorTypedView(componentType, buffer!, offsetInBytes, (int)length);
            default:
                return null;
        }
    }

    /// <summary>Dart _typedViewFloat: FLOAT view only.</summary>
    internal static AccessorTypedView? TypedViewFloat(int componentType, byte[]? buffer, long offsetInBytes, long length)
    {
        if (!ViewFits(componentType, buffer, offsetInBytes, length))
        {
            return null;
        }
        switch (componentType)
        {
            case Gl.FLOAT:
                return new AccessorTypedView(componentType, buffer!, offsetInBytes, (int)length);
            default:
                return null;
        }
    }

    /// <summary>Dart _typedViewInt: BYTE, UNSIGNED_BYTE, SHORT, UNSIGNED_SHORT, UNSIGNED_INT views.</summary>
    internal static AccessorTypedView? TypedViewInt(int componentType, byte[]? buffer, long offsetInBytes, long length)
    {
        if (!ViewFits(componentType, buffer, offsetInBytes, length))
        {
            return null;
        }
        switch (componentType)
        {
            case Gl.BYTE:
            case Gl.UNSIGNED_BYTE:
            case Gl.SHORT:
            case Gl.UNSIGNED_SHORT:
            /* case Gl.INT: */
            case Gl.UNSIGNED_INT:
                return new AccessorTypedView(componentType, buffer!, offsetInBytes, (int)length);
            default:
                return null;
        }
    }

    /// <summary>
    /// Shared body of Dart _AccessorInt.getElements / _AccessorFloat.getElements; the two differ only in the
    /// typed view constructor used for the base data and the sparse values.
    /// </summary>
    protected IEnumerable<double> GetElementsImpl(Func<int, byte[]?, long, long, AccessorTypedView?> typedView)
    {
        // Ensure required fields to not check for them each time
        if (ComponentType == -1 || Count == -1 || Type == null)
        {
            yield break;
        }

        var components = Components;
        var elementsCount = (long)Count * components;

        IEnumerable<double> elements;

        if (_bufferView != null)
        {
            if (_bufferView.Buffer?.Data == null)
            {
                yield break;
            }

            if (ByteStride < ElementLength)
            {
                yield break;
            }

            if (!CheckByteOffsetAndLength(ByteOffset, ComponentLength, ByteLength, _bufferView))
            {
                yield break;
            }

            var view = typedView(
                ComponentType,
                _bufferView.Buffer.Data,
                (long)_bufferView.ByteOffset + ByteOffset,
                ByteLength / ComponentLength);

            if (view == null)
            {
                yield break;
            }

            var length = view.Length;
            if (IsMatrixWithGaps)
            {
                // type is either MAT2 or MAT3 here
                // TODO: generalize to non-square matrices
                var skip = ByteStride / ComponentLength - (Type == Members.MAT2 ? 8 : 12);
                var rowCount = Type == Members.MAT2 ? 2 : 3;
                var columnCount = rowCount;

                elements = MatrixWithGapsElements(view, length, skip, rowCount, columnCount);
            }
            else
            {
                var skip = ByteStride / ComponentLength - components;
                elements = StridedElements(view, length, components, skip);
            }
        }
        else
        {
            // Base accessor is filled with zeros
            elements = Zeros(elementsCount);
        }

        if (Sparse != null)
        {
            var sparse = Sparse;
            if (sparse.Values.ByteOffset == -1 ||
                sparse.Values.BufferView == null ||
                sparse.Values.BufferView.ByteLength == -1 ||
                sparse.Values.BufferView.ByteOffset == -1 ||
                sparse.Values.BufferView.Buffer?.Data == null ||
                sparse.Indices.ComponentType == -1 ||
                sparse.Indices.ByteOffset == -1 ||
                sparse.Indices.BufferView == null ||
                sparse.Indices.BufferView.ByteLength == -1 ||
                sparse.Indices.BufferView.ByteOffset == -1 ||
                sparse.Indices.BufferView.Buffer?.Data == null)
            {
                yield break;
            }

            if (sparse.Count > Count)
            {
                yield break;
            }

            if (!CheckByteOffsetAndLength(
                    sparse.Indices.ByteOffset,
                    Gl.ComponentTypeLength(sparse.Indices.ComponentType),
                    (long)Gl.ComponentTypeLength(sparse.Indices.ComponentType) *
                        sparse.Count,
                    sparse.Indices.BufferView) ||
                !CheckByteOffsetAndLength(
                    sparse.Values.ByteOffset,
                    ComponentLength,
                    (long)ComponentLength *
                        Components *
                        sparse.Count,
                    sparse.Values.BufferView))
            {
                yield break;
            }

            var indices = TypedViewIndices(
                sparse.Indices.ComponentType,
                sparse.Indices.BufferView.Buffer.Data,
                (long)sparse.Indices.BufferView.ByteOffset + sparse.Indices.ByteOffset,
                sparse.Count);

            var values = typedView(
                ComponentType,
                sparse.Values.BufferView.Buffer.Data,
                (long)sparse.Values.BufferView.ByteOffset + sparse.Values.ByteOffset,
                (long)sparse.Count * components);

            if (indices == null || values == null)
            {
                yield break;
            }

            var baseElements = elements;

            elements = SparseElements(baseElements, indices, values, components, sparse.Count);
        }

        foreach (var e in elements)
        {
            yield return e;
        }
    }

    private static IEnumerable<double> MatrixWithGapsElements(AccessorTypedView view, int length, int skip, int rowCount, int columnCount)
    {
        var index = 0;
        var rowIndex = 0;
        var columnIndex = 0;
        while (index < length)
        {
            yield return view[index];
            index++;
            rowIndex++;
            if (rowIndex == rowCount)
            {
                index += 4 - rowIndex;
                columnIndex++;
                rowIndex = 0;
                if (columnIndex == columnCount)
                {
                    columnIndex = 0;
                    index += skip;
                }
            }
        }
    }

    private static IEnumerable<double> StridedElements(AccessorTypedView view, int length, int components, int skip)
    {
        var index = 0;
        var componentIndex = 0;
        while (index < length)
        {
            yield return view[index];
            index++;
            componentIndex++;
            if (componentIndex == components)
            {
                componentIndex = 0;
                index += skip;
            }
        }
    }

    private static IEnumerable<double> Zeros(long count)
    {
        for (long i = 0; i < count; i++)
        {
            yield return 0;
        }
    }

    private static IEnumerable<double> SparseElements(IEnumerable<double> baseElements, AccessorTypedView indices,
        AccessorTypedView values, int components, int sparseCount)
    {
        var index = 0;
        var componentIndex = 0;
        var sparsePosition = 0;
        var sparseIndex = indices[0];
        foreach (var element in baseElements)
        {
            if (componentIndex == components)
            {
                if (index == sparseIndex && sparsePosition != sparseCount - 1)
                {
                    sparsePosition++;
                    sparseIndex = indices[sparsePosition];
                }
                index++;
                componentIndex = 0;
            }

            if (index == sparseIndex)
            {
                yield return values[sparsePosition * components + componentIndex];
            }
            else
            {
                yield return element;
            }
            componentIndex++;
        }
    }
}

/// <summary>Dart _AccessorInt.</summary>
internal sealed class AccessorInt : Accessor
{
    public AccessorInt(int bufferViewIndex, int byteOffset, int componentType, int count, string? type, bool normalized,
        double[]? max, double[]? min, AccessorSparse? sparse, string? name, Dictionary<string, object?> extensions, object? extras)
        : base(bufferViewIndex, byteOffset, componentType, count, type, normalized, max, min, sparse, name, extensions, extras)
    {
    }

    public override IEnumerable<double> GetElements() => GetElementsImpl(TypedViewInt);

    public override IEnumerable<double> GetElementsNormalized()
    {
        var width = ComponentLength * 8;
        if (ComponentType == Gl.BYTE ||
            ComponentType == Gl.SHORT ||
            ComponentType == Gl.INT)
        {
            // Signed
            var denom = 1 / (double)((1L << (width - 1)) - 1);
            // TODO check if math.max could be replaced with something better
            return GetElements().Select(value => Math.Max(value * denom, -1));
        }
        else
        {
            // Unsigned
            var denom = 1 / (double)((1L << width) - 1);
            return GetElements().Select(value => value * denom);
        }
    }
}

/// <summary>Dart _AccessorFloat.</summary>
internal sealed class AccessorFloat : Accessor
{
    public AccessorFloat(int bufferViewIndex, int byteOffset, int componentType, int count, string? type, bool normalized,
        double[]? max, double[]? min, AccessorSparse? sparse, string? name, Dictionary<string, object?> extensions, object? extras)
        : base(bufferViewIndex, byteOffset, componentType, count, type, normalized, max, min, sparse, name, extensions, extras)
    {
    }

    public override IEnumerable<double> GetElements() => GetElementsImpl(TypedViewFloat);

    public override IEnumerable<double> GetElementsNormalized() => GetElements();
}

internal sealed class AccessorSparse : GltfProperty
{
    public readonly int Count;
    public readonly AccessorSparseIndices Indices;
    public readonly AccessorSparseValues Values;

    private AccessorSparse(int count, AccessorSparseIndices indices, AccessorSparseValues values,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Count = count;
        Indices = indices;
        Values = values;
    }

    /// <summary>Dart <c>List&lt;int&gt; get indicesTypedView</c>: the sparse index values, or null when unavailable.</summary>
    public long[]? IndicesTypedView
    {
        get
        {
            if (Indices.BufferView?.Buffer?.Data == null)
            {
                return null;
            }

            var view = Accessor.TypedViewIndices(
                Indices.ComponentType,
                Indices.BufferView.Buffer.Data,
                (long)Indices.BufferView.ByteOffset + Indices.ByteOffset,
                Count);
            if (view == null)
            {
                return null;
            }
            var result = new long[view.Length];
            for (int i = 0; i < result.Length; i++) result[i] = (long)view[i];
            return result;
        }
    }

    public static AccessorSparse? FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.ACCESSOR_SPARSE_MEMBERS, context);
        }

        var count = JsonUtils.GetUint(map, Members.COUNT, context, min: 1, req: true);
        var indices = JsonUtils.GetObjectFromInnerMap<AccessorSparseIndices>(
            map, Members.INDICES, context, AccessorSparseIndices.FromMap,
            req: true);
        var values = JsonUtils.GetObjectFromInnerMap<AccessorSparseValues>(
            map, Members.VALUES, context, AccessorSparseValues.FromMap,
            req: true);

        if (count == -1 || indices == null || values == null)
        {
            return null;
        }

        return new AccessorSparse(count, indices, values,
            JsonUtils.GetExtensions(map, typeof(AccessorSparse), context), JsonUtils.GetExtras(map, context));
    }
}

internal sealed class AccessorSparseIndices : GltfProperty
{
    private readonly int _bufferViewIndex;
    public readonly int ByteOffset;
    public readonly int ComponentType;

    private BufferView? _bufferView;

    private AccessorSparseIndices(int bufferViewIndex, int byteOffset, int componentType,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        _bufferViewIndex = bufferViewIndex;
        ByteOffset = byteOffset;
        ComponentType = componentType;
    }

    // Dart: library-private _bufferViewIndex, read by Accessor.
    internal int BufferViewIndex => _bufferViewIndex;

    public BufferView? BufferView => _bufferView;

    public static AccessorSparseIndices FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.ACCESSOR_SPARSE_INDICES_MEMBERS, context);
        }

        return new AccessorSparseIndices(
            JsonUtils.GetIndex(map, Members.BUFFER_VIEW, context, req: true),
            JsonUtils.GetUint(map, Members.BYTE_OFFSET, context, def: 0),
            JsonUtils.GetUint(map, Members.COMPONENT_TYPE, context,
                req: true, list: Gl.ELEMENT_ARRAY_TYPES),
            JsonUtils.GetExtensions(map, typeof(AccessorSparseIndices), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        _bufferView = gltf.BufferViews[_bufferViewIndex];
    }
}

internal sealed class AccessorSparseValues : GltfProperty
{
    private readonly int _bufferViewIndex;
    public readonly int ByteOffset;

    private BufferView? _bufferView;

    private AccessorSparseValues(int bufferViewIndex, int byteOffset,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        _bufferViewIndex = bufferViewIndex;
        ByteOffset = byteOffset;
    }

    // Dart: library-private _bufferViewIndex, read by Accessor.
    internal int BufferViewIndex => _bufferViewIndex;

    public BufferView? BufferView => _bufferView;

    public static AccessorSparseValues FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.ACCESSOR_SPARSE_VALUES_MEMBERS, context);
        }

        return new AccessorSparseValues(
            JsonUtils.GetIndex(map, Members.BUFFER_VIEW, context, req: true),
            JsonUtils.GetUint(map, Members.BYTE_OFFSET, context, def: 0),
            JsonUtils.GetExtensions(map, typeof(AccessorSparseValues), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        _bufferView = gltf.BufferViews[_bufferViewIndex];
    }
}
