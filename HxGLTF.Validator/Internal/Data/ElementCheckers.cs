// Port of the ElementChecker implementations of lib/src/utils.dart, lib/src/base/accessor.dart,
// lib/src/base/mesh.dart, lib/src/base/animation.dart and lib/src/base/skin.dart

namespace HxGLTF.Validator.Internal;

// utils.dart

internal sealed class UnitVec3FloatChecker : ElementChecker
{
    private double _sum;

    public override string Path { get; }

    private readonly Func<double, double>? _normalizeValue;

    public UnitVec3FloatChecker(string path, Func<double, double>? normalizeValue)
    {
        Path = path;
        _normalizeValue = normalizeValue;
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        var v = _normalizeValue != null ? _normalizeValue(value) : value;
        _sum += v * v;
        if (2 == componentIndex)
        {
            if (Math.Abs(Math.Sqrt(_sum) - 1.0) > IssueConstants.UnitLengthThresholdVec3)
            {
                context.AddIssue(DataError.AccessorVector3NonUnit,
                    name: Path, args: new object?[] { index - 2, index, Math.Sqrt(_sum) });
            }
            _sum = 0.0;
        }

        return true;
    }
}

internal sealed class UnitVec3SignFloatChecker : ElementChecker
{
    private double _sum;

    public override string Path { get; }

    private readonly Func<double, double>? _normalizeValue;

    public UnitVec3SignFloatChecker(string path, Func<double, double>? normalizeValue)
    {
        Path = path;
        _normalizeValue = normalizeValue;
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        var v = _normalizeValue != null ? _normalizeValue(value) : value;
        if (3 == componentIndex)
        {
            if (1.0 != v && -1.0 != v)
            {
                context.AddIssue(DataError.AccessorInvalidSign,
                    name: Path, args: new object?[] { index - 3, index, v });
            }
        }
        else
        {
            _sum += v * v;
            if (2 == componentIndex)
            {
                if (Math.Abs(Math.Sqrt(_sum) - 1.0) > IssueConstants.UnitLengthThresholdVec3)
                {
                    context.AddIssue(DataError.AccessorVector3NonUnit,
                        name: Path, args: new object?[] { index - 2, index, Math.Sqrt(_sum) });
                }
                _sum = 0.0;
            }
        }

        return true;
    }
}

internal sealed class ClampedRangeFloatChecker : ElementChecker
{
    public override string Path { get; }

    public ClampedRangeFloatChecker(string path)
    {
        Path = path;
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        if (1.0 < value || 0.0 > value)
        {
            context.AddIssue(DataError.AccessorNonClamped,
                name: Path, args: new object?[] { index, value });
        }

        return true;
    }
}

// accessor.dart

internal sealed class InvalidFloatChecker : ElementChecker
{
    public override string Path { get; }

    public InvalidFloatChecker(string path)
    {
        Path = path;
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        if (double.IsInfinity(value) || double.IsNaN(value))
        {
            context.AddIssue(DataError.AccessorInvalidFloat,
                name: Path, args: new object?[] { index, value });
            return false;
        }
        return true;
    }
}

internal sealed class MinFloatChecker : ElementChecker
{
    private readonly int[] _invalidMinCount;
    private readonly double[] _computedMin;
    private readonly double[] _providedMin;

    public override string Path { get; }

    public MinFloatChecker(string path, double[] min)
    {
        Path = path;
        _invalidMinCount = new int[min.Length];
        _computedMin = new double[min.Length];
        _providedMin = (double[])min.Clone();
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        if (index == componentIndex || _computedMin[componentIndex] > value)
        {
            _computedMin[componentIndex] = value;
        }

        if (value < _providedMin[componentIndex])
        {
            ++_invalidMinCount[componentIndex];
        }

        return true;
    }

    public override bool Done(Context context)
    {
        for (var c = 0; c < _computedMin.Length; ++c)
        {
            if (_providedMin[c] != _computedMin[c])
            {
                context.AddIssue(DataError.AccessorMinMismatch,
                    name: Path + "/" + Members.MIN + "/" + c, args: new object?[] { _providedMin[c], _computedMin[c] });

                if (_invalidMinCount[c] > 0)
                {
                    context.AddIssue(DataError.AccessorElementOutOfMinBound,
                        name: Path + "/" + Members.MIN + "/" + c,
                        args: new object?[] { _invalidMinCount[c], _providedMin[c] });
                }
            }
        }

        return true;
    }
}

internal sealed class MaxFloatChecker : ElementChecker
{
    private readonly int[] _invalidMaxCount;
    private readonly double[] _computedMax;
    private readonly double[] _providedMax;

    public override string Path { get; }

    public MaxFloatChecker(string path, double[] max)
    {
        Path = path;
        _invalidMaxCount = new int[max.Length];
        _computedMax = new double[max.Length];
        _providedMax = (double[])max.Clone();
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        if (index == componentIndex || _computedMax[componentIndex] < value)
        {
            _computedMax[componentIndex] = value;
        }

        if (value > _providedMax[componentIndex])
        {
            ++_invalidMaxCount[componentIndex];
        }

        return true;
    }

    public override bool Done(Context context)
    {
        for (var c = 0; c < _computedMax.Length; ++c)
        {
            if (_providedMax[c] != _computedMax[c])
            {
                context.AddIssue(DataError.AccessorMaxMismatch,
                    name: Path + "/" + Members.MAX + "/" + c, args: new object?[] { _providedMax[c], _computedMax[c] });

                if (_invalidMaxCount[c] > 0)
                {
                    context.AddIssue(DataError.AccessorElementOutOfMaxBound,
                        name: Path + "/" + Members.MAX + "/" + c,
                        args: new object?[] { _invalidMaxCount[c], _providedMax[c] });
                }
            }
        }

        return true;
    }
}

internal sealed class MinIntegerChecker : ElementChecker
{
    private readonly int[] _invalidMinCount;
    private readonly long[] _computedMin;
    private readonly long[] _providedMin;

    public override string Path { get; }

    public MinIntegerChecker(string path, long[] min)
    {
        Path = path;
        _invalidMinCount = new int[min.Length];
        _computedMin = new long[min.Length];
        _providedMin = (long[])min.Clone();
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        var v = (long)value;
        if (index == componentIndex || _computedMin[componentIndex] > v)
        {
            _computedMin[componentIndex] = v;
        }

        if (v < _providedMin[componentIndex])
        {
            ++_invalidMinCount[componentIndex];
        }

        return true;
    }

    public override bool Done(Context context)
    {
        for (var c = 0; c < _computedMin.Length; ++c)
        {
            if (_providedMin[c] != _computedMin[c])
            {
                context.AddIssue(DataError.AccessorMinMismatch,
                    name: Path + "/" + Members.MIN + "/" + c, args: new object?[] { _providedMin[c], _computedMin[c] });

                if (_invalidMinCount[c] > 0)
                {
                    context.AddIssue(DataError.AccessorElementOutOfMinBound,
                        name: Path + "/" + Members.MIN + "/" + c,
                        args: new object?[] { _invalidMinCount[c], _providedMin[c] });
                }
            }
        }

        return true;
    }
}

internal sealed class MaxIntegerChecker : ElementChecker
{
    private readonly int[] _invalidMaxCount;
    private readonly long[] _computedMax;
    private readonly long[] _providedMax;

    public override string Path { get; }

    public MaxIntegerChecker(string path, long[] max)
    {
        Path = path;
        _invalidMaxCount = new int[max.Length];
        _computedMax = new long[max.Length];
        _providedMax = (long[])max.Clone();
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        var v = (long)value;
        if (index == componentIndex || _computedMax[componentIndex] < v)
        {
            _computedMax[componentIndex] = v;
        }

        if (v > _providedMax[componentIndex])
        {
            ++_invalidMaxCount[componentIndex];
        }

        return true;
    }

    public override bool Done(Context context)
    {
        for (var c = 0; c < _computedMax.Length; ++c)
        {
            if (_providedMax[c] != _computedMax[c])
            {
                context.AddIssue(DataError.AccessorMaxMismatch,
                    name: Path + "/" + Members.MAX + "/" + c, args: new object?[] { _providedMax[c], _computedMax[c] });

                if (_invalidMaxCount[c] > 0)
                {
                    context.AddIssue(DataError.AccessorElementOutOfMaxBound,
                        name: Path + "/" + Members.MAX + "/" + c,
                        args: new object?[] { _invalidMaxCount[c], _providedMax[c] });
                }
            }
        }

        return true;
    }
}

// mesh.dart

internal sealed class IndexBufferIntegerChecker : ElementChecker
{
    /*
    TODO
    points - warn on duplicates
    lines - degenerate (v(2n)=v(2n+1)), duplicates (incl. reversed)
    line_loop, line_stripe - degenerate (v(n)=v(n+1)),
    triangles - degenerate (v1=v2 | v2=v3 | v1=v3), duplicates (order-aware)
    triangle_strip - degenerate (v1=v2=v3), duplicates (order-aware)
    triangle_fan - ???
   */

    public readonly int MaxVertexIndex;
    public readonly int TotalTriangles;
    public readonly long PrimitiveRestartIndex;

    public readonly bool IsPoints;
    public readonly bool IsLines;
    public readonly bool IsLineLoop;
    public readonly bool IsLineStrip;
    public readonly bool IsTriangles;
    public readonly bool IsTriangleStrip;
    public readonly bool IsTriangleFan;

    private int _vertexIndex;
    private int _degenerateTriangles;

    private readonly long[] _triangle = new long[3];

    public override string Path { get; }

    public IndexBufferIntegerChecker(string path, int maxVertexIndex, int totalTriangles, int componentType, int modesMask)
    {
        Path = path;
        MaxVertexIndex = maxVertexIndex;
        TotalTriangles = totalTriangles;
        PrimitiveRestartIndex = Gl.TypeMax(componentType);
        IsPoints = 1 == (1 & modesMask);
        IsLines = 2 == (2 & modesMask);
        IsLineLoop = 4 == (4 & modesMask);
        IsLineStrip = 8 == (8 & modesMask);
        IsTriangles = 16 == (16 & modesMask);
        IsTriangleStrip = 32 == (32 & modesMask);
        IsTriangleFan = 64 == (64 & modesMask);
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        var v = (long)value;
        if (v > MaxVertexIndex)
        {
            context.AddIssue(DataError.AccessorIndexOob,
                name: Path, args: new object?[] { index, v, MaxVertexIndex });
        }

        if (v == PrimitiveRestartIndex)
        {
            context.AddIssue(DataError.AccessorIndexPrimitiveRestart,
                name: Path, args: new object?[] { v, index });
        }

        if (IsTriangles)
        {
            _triangle[_vertexIndex] = v;
            if (++_vertexIndex == 3)
            {
                _vertexIndex = 0;
                if (_triangle[0] == _triangle[1] ||
                    _triangle[1] == _triangle[2] ||
                    _triangle[2] == _triangle[0])
                {
                    ++_degenerateTriangles;
                }
            }
        }

        return true;
    }

    public override bool Done(Context context)
    {
        if (_degenerateTriangles > 0)
        {
            context.AddIssue(DataError.AccessorIndexTriangleDegenerate,
                name: Path, args: new object?[] { _degenerateTriangles, TotalTriangles });
        }

        return true;
    }
}

// animation.dart

internal sealed class AnimationInputChecker : ElementChecker
{
    private double _lastValue;

    public override string Path { get; }

    public AnimationInputChecker(string path)
    {
        Path = path;
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        if (value < 0.0)
        {
            context.AddIssue(DataError.AccessorAnimationInputNegative,
                name: Path, args: new object?[] { index, value });
        }
        else
        {
            if (index != 0 && value <= _lastValue)
            {
                context.AddIssue(DataError.AccessorAnimationInputNonIncreasing,
                    name: Path, args: new object?[] { index, value, _lastValue });
            }
            _lastValue = value;
        }

        return true;
    }
}

internal sealed class QuaternionFloatChecker : ElementChecker
{
    public readonly bool HasTangents;
    private readonly Func<double, double>? _normalizeValue;

    public override string Path { get; }

    public QuaternionFloatChecker(string path, Func<double, double>? normalizeValue, bool hasTangents = false)
    {
        Path = path;
        _normalizeValue = normalizeValue;
        HasTangents = hasTangents;
    }

    // used only for quaternions with cubic spline tangents
    // 0-3  - in tangent
    // 4-7  - actual value
    // 8-11 - out tangent
    private int _fullComponentIndex;

    private double _sum;

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        if (!HasTangents || 4 == (4 & _fullComponentIndex))
        {
            var v = _normalizeValue != null ? _normalizeValue(value) : value;
            _sum += v * v;
            if (3 == componentIndex)
            {
                if (Math.Abs(Math.Sqrt(_sum) - 1.0) > IssueConstants.UnitLengthThresholdVec4)
                {
                    context.AddIssue(
                        DataError.AccessorAnimationSamplerOutputNonNormalizedQuaternion,
                        name: Path,
                        args: new object?[] { index - 3, index, Math.Sqrt(_sum) });
                }
                _sum = 0;
            }
        }

        if (++_fullComponentIndex == 12)
        {
            _fullComponentIndex = 0;
        }

        return true;
    }
}

// skin.dart

internal sealed class IbmMatrixFloatChecker : ElementChecker
{
    public override string Path { get; }

    public IbmMatrixFloatChecker(string path)
    {
        Path = path;
    }

    public override bool Check(Context context, int index, int componentIndex, double value)
    {
        if (3 == componentIndex && 0 != value ||
            7 == componentIndex && 0 != value ||
            11 == componentIndex && 0 != value ||
            15 == componentIndex && 1 != value)
        {
            context.AddIssue(DataError.AccessorInvalidInverseBindMatrix,
                name: Path, args: new object?[] { index, componentIndex, value });
        }

        return true;
    }
}
