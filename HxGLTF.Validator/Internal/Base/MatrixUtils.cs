// Port of the vector_math (Dart package) pieces used by lib/src/base/node.dart and isTrsDecomposable from lib/src/utils.dart.
// Matrix4 storage is column-major: element i of the JSON list is storage[i], row = i % 4, column = i / 4.
namespace HxGLTF.Validator.Internal;

/// <summary>vector_math Vector3 (only what the validator uses).</summary>
internal sealed class Vector3
{
    public double X;
    public double Y;
    public double Z;

    public Vector3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vector3 Zero() => new(0, 0, 0);

    /// <summary>Dart Vector3.array(list).</summary>
    public static Vector3 FromArray(double[] array) => new(array[0], array[1], array[2]);

    public void SetValues(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double Length2 => X * X + Y * Y + Z * Z;

    public double Length => Math.Sqrt(Length2);

    // Dart: extension Vector3IsOneOrZero (utils.dart)
    public bool IsOne => X == 1 && Y == 1 && Z == 1;

    public bool IsZero => X == 0 && Y == 0 && Z == 0;
}

/// <summary>vector_math Quaternion (only what the validator uses). Storage order x, y, z, w.</summary>
internal sealed class Quaternion
{
    public double X;
    public double Y;
    public double Z;
    public double W;

    public Quaternion(double x, double y, double z, double w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static Quaternion Identity() => new(0, 0, 0, 1);

    public double Length2 => X * X + Y * Y + Z * Z + W * W;

    public double Length => Math.Sqrt(Length2);

    // Dart: extension QuaternionIsDefault (utils.dart)
    public bool IsDefault => X == 0 && Y == 0 && Z == 0 && W == 1;

    private double this[int i]
    {
        get => i switch { 0 => X, 1 => Y, 2 => Z, _ => W };
        set
        {
            switch (i)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                default: W = value; break;
            }
        }
    }

    /// <summary>vector_math Quaternion.setFromRotation(Matrix3). <paramref name="m"/> is a column-major 3x3 storage.</summary>
    public void SetFromRotation(double[] m)
    {
        static int Index(int row, int col) => col * 3 + row;

        var trace = m[0] + m[4] + m[8];
        if (trace > 0.0)
        {
            var s = Math.Sqrt(trace + 1.0);
            W = s * 0.5;
            s = 0.5 / s;
            X = (m[5] - m[7]) * s;
            Y = (m[6] - m[2]) * s;
            Z = (m[1] - m[3]) * s;
        }
        else
        {
            var i = m[0] < m[4]
                ? (m[4] < m[8] ? 2 : 1)
                : (m[0] < m[8] ? 2 : 0);
            var j = (i + 1) % 3;
            var k = (i + 2) % 3;
            var s = Math.Sqrt(m[Index(i, i)] - m[Index(j, j)] - m[Index(k, k)] + 1.0);
            this[i] = s * 0.5;
            s = 0.5 / s;
            W = (m[Index(k, j)] - m[Index(j, k)]) * s;
            this[j] = (m[Index(j, i)] + m[Index(i, j)]) * s;
            this[k] = (m[Index(k, i)] + m[Index(i, k)]) * s;
        }
    }
}

/// <summary>vector_math Matrix4 (only what the validator uses). Column-major storage of 16 doubles.</summary>
internal sealed class Matrix4
{
    public readonly double[] Storage = new double[16];

    private Matrix4() { }

    public static Matrix4 Zero() => new();

    /// <summary>Dart Matrix4.fromList(list): storage[i] = list[i].</summary>
    public static Matrix4 FromList(double[] list)
    {
        var m = new Matrix4();
        for (var i = 0; i < 16; i++) m.Storage[i] = list[i];
        return m;
    }

    public double this[int i]
    {
        get => Storage[i];
        set => Storage[i] = value;
    }

    public void SetFrom(Matrix4 other)
    {
        for (var i = 0; i < 16; i++) Storage[i] = other.Storage[i];
    }

    public bool IsIdentity()
    {
        var m = Storage;
        return m[0] == 1.0 && m[1] == 0.0 && m[2] == 0.0 && m[3] == 0.0 &&
               m[4] == 0.0 && m[5] == 1.0 && m[6] == 0.0 && m[7] == 0.0 &&
               m[8] == 0.0 && m[9] == 0.0 && m[10] == 1.0 && m[11] == 0.0 &&
               m[12] == 0.0 && m[13] == 0.0 && m[14] == 0.0 && m[15] == 1.0;
    }

    public double Determinant()
    {
        var m = Storage;
        var det2_01_01 = m[0] * m[5] - m[1] * m[4];
        var det2_01_02 = m[0] * m[6] - m[2] * m[4];
        var det2_01_03 = m[0] * m[7] - m[3] * m[4];
        var det2_01_12 = m[1] * m[6] - m[2] * m[5];
        var det2_01_13 = m[1] * m[7] - m[3] * m[5];
        var det2_01_23 = m[2] * m[7] - m[3] * m[6];
        var det3_201_012 = m[8] * det2_01_12 - m[9] * det2_01_02 + m[10] * det2_01_01;
        var det3_201_013 = m[8] * det2_01_13 - m[9] * det2_01_03 + m[11] * det2_01_01;
        var det3_201_023 = m[8] * det2_01_23 - m[10] * det2_01_03 + m[11] * det2_01_02;
        var det3_201_123 = m[9] * det2_01_23 - m[10] * det2_01_13 + m[11] * det2_01_12;
        return -det3_201_123 * m[12] +
               det3_201_023 * m[13] -
               det3_201_013 * m[14] +
               det3_201_012 * m[15];
    }

    /// <summary>vector_math Matrix4.decompose(translation, rotation, scale).</summary>
    public void Decompose(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        var s = Storage;
        var v = Vector3.Zero();
        v.SetValues(s[0], s[1], s[2]);
        var sx = v.Length;
        v.SetValues(s[4], s[5], s[6]);
        var sy = v.Length;
        v.SetValues(s[8], s[9], s[10]);
        var sz = v.Length;

        if (Determinant() < 0)
        {
            sx = -sx;
        }

        translation.X = s[12];
        translation.Y = s[13];
        translation.Z = s[14];

        var invSX = 1.0 / sx;
        var invSY = 1.0 / sy;
        var invSZ = 1.0 / sz;

        var m = Zero();
        m.SetFrom(this);
        m.Storage[0] *= invSX;
        m.Storage[1] *= invSX;
        m.Storage[2] *= invSX;
        m.Storage[4] *= invSY;
        m.Storage[5] *= invSY;
        m.Storage[6] *= invSY;
        m.Storage[8] *= invSZ;
        m.Storage[9] *= invSZ;
        m.Storage[10] *= invSZ;

        // Matrix4.copyRotation(Matrix3): upper-left 3x3, column-major
        var r = new double[9];
        r[0] = m.Storage[0];
        r[1] = m.Storage[1];
        r[2] = m.Storage[2];
        r[3] = m.Storage[4];
        r[4] = m.Storage[5];
        r[5] = m.Storage[6];
        r[6] = m.Storage[8];
        r[7] = m.Storage[9];
        r[8] = m.Storage[10];
        rotation.SetFromRotation(r);

        scale.X = sx;
        scale.Y = sy;
        scale.Z = sz;
    }

    /// <summary>vector_math Matrix4.setFromTranslationRotationScale.</summary>
    public void SetFromTranslationRotationScale(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        SetFromTranslationRotation(translation, rotation);
        Scale(scale);
    }

    /// <summary>vector_math Matrix4.setFromTranslationRotation.</summary>
    public void SetFromTranslationRotation(Vector3 arg0, Quaternion arg1)
    {
        var x = arg1.X;
        var y = arg1.Y;
        var z = arg1.Z;
        var w = arg1.W;
        var x2 = x + x;
        var y2 = y + y;
        var z2 = z + z;
        var xx = x * x2;
        var xy = x * y2;
        var xz = x * z2;
        var yy = y * y2;
        var yz = y * z2;
        var zz = z * z2;
        var wx = w * x2;
        var wy = w * y2;
        var wz = w * z2;

        var m = Storage;
        m[0] = 1.0 - (yy + zz);
        m[1] = xy + wz;
        m[2] = xz - wy;
        m[3] = 0.0;
        m[4] = xy - wz;
        m[5] = 1.0 - (xx + zz);
        m[6] = yz + wx;
        m[7] = 0.0;
        m[8] = xz + wy;
        m[9] = yz - wx;
        m[10] = 1.0 - (xx + yy);
        m[11] = 0.0;
        m[12] = arg0.X;
        m[13] = arg0.Y;
        m[14] = arg0.Z;
        m[15] = 1.0;
    }

    /// <summary>vector_math Matrix4.scale(Vector3): sx, sy, sz, sw = 1.0.</summary>
    public void Scale(Vector3 scale)
    {
        var sx = scale.X;
        var sy = scale.Y;
        var sz = scale.Z;
        const double sw = 1.0;
        var m = Storage;
        m[0] *= sx;
        m[1] *= sx;
        m[2] *= sx;
        m[3] *= sx;
        m[4] *= sy;
        m[5] *= sy;
        m[6] *= sy;
        m[7] *= sy;
        m[8] *= sz;
        m[9] *= sz;
        m[10] *= sz;
        m[11] *= sz;
        m[12] *= sw;
        m[13] *= sw;
        m[14] *= sw;
        m[15] *= sw;
    }

    /// <summary>vector_math Matrix4.infinityNorm: max over the four groups of 4 consecutive storage elements of the sum of absolute values.</summary>
    public double InfinityNorm()
    {
        var m = Storage;
        var norm = 0.0;
        for (var g = 0; g < 16; g += 4)
        {
            var rowNorm = 0.0;
            rowNorm += Math.Abs(m[g]);
            rowNorm += Math.Abs(m[g + 1]);
            rowNorm += Math.Abs(m[g + 2]);
            rowNorm += Math.Abs(m[g + 3]);
            norm = rowNorm > norm ? rowNorm : norm;
        }
        return norm;
    }

    /// <summary>vector_math Matrix4.absoluteError(correct) = (correct - this).infinityNorm().</summary>
    public double AbsoluteError(Matrix4 correct)
    {
        var diff = Zero();
        for (var i = 0; i < 16; i++) diff.Storage[i] = correct.Storage[i] - Storage[i];
        return diff.InfinityNorm();
    }
}

internal static class MatrixUtils
{
    // Dart utils.dart: isTrsDecomposable
    public static bool IsTrsDecomposable(Matrix4 matrix)
    {
        if (matrix[3] != 0.0 ||
            matrix[7] != 0.0 ||
            matrix[11] != 0.0 ||
            matrix[15] != 1.0)
        {
            return false;
        }

        if (matrix.Determinant() == 0.0)
        {
            return false;
        }

        var translation = Vector3.Zero();
        var rotation = Quaternion.Identity();
        var scale = Vector3.Zero();
        matrix.Decompose(translation, rotation, scale);
        var rebuilt = Matrix4.Zero();
        rebuilt.SetFromTranslationRotationScale(translation, rotation, scale);
        return rebuilt.AbsoluteError(matrix) < 0.00005;
    }
}
