// Port of lib/src/gl.dart

namespace HxGLTF.Validator.Internal;

internal static class Gl
{
    public const int POINTS = 0;
    public const int LINES = 1;
    public const int LINE_LOOP = 2;
    public const int LINE_STRIP = 3;
    public const int TRIANGLES = 4;
    public const int TRIANGLE_STRIP = 5;
    public const int TRIANGLE_FAN = 6;

    public static readonly string[] MODES_NAMES =
    {
        "POINTS",
        "LINES",
        "LINE_LOOP",
        "LINE_STRIP",
        "TRIANGLES",
        "TRIANGLE_STRIP",
        "TRIANGLE_FAN",
    };

    public const int NEVER = 512;
    public const int LESS = 513;
    public const int EQUAL = 514;
    public const int LEQUAL = 515;
    public const int GREATER = 516;
    public const int NOTEQUAL = 517;
    public const int GEQUAL = 518;
    public const int ALWAYS = 519;

    public const int FRONT = 1028;
    public const int BACK = 1029;
    public const int FRONT_AND_BACK = 1032;

    public const int CW = 2304;
    public const int CCW = 2305;

    public const int CULL_FACE = 2884;
    public const int DEPTH_TEST = 2929;
    public const int BLEND = 3042;
    public const int SCISSOR_TEST = 3089;
    public const int POLYGON_OFFSET_FILL = 32823;
    public const int SAMPLE_ALPHA_TO_COVERAGE = 32926;

    public const int TEXTURE_2D = 3553;

    public const int BYTE = 5120;
    public const int UNSIGNED_BYTE = 5121;
    public const int SHORT = 5122;
    public const int UNSIGNED_SHORT = 5123;
    public const int INT = 5124;
    public const int UNSIGNED_INT = 5125;
    public const int FLOAT = 5126;

    public static readonly int[] COMPONENT_TYPES =
    {
        BYTE,
        UNSIGNED_BYTE,
        SHORT,
        UNSIGNED_SHORT,
        UNSIGNED_INT,
        FLOAT,
    };

    public static int ComponentTypeLength(int componentType)
    {
        switch (componentType)
        {
            case BYTE:
            case UNSIGNED_BYTE:
                return 1;
            case SHORT:
            case UNSIGNED_SHORT:
                return 2;
            case INT:
            case UNSIGNED_INT:
            case FLOAT:
                return 4;
            default:
                return -1;
        }
    }

    public static readonly int[] ELEMENT_ARRAY_TYPES =
    {
        UNSIGNED_BYTE,
        UNSIGNED_SHORT,
        UNSIGNED_INT,
    };

    public const int ALPHA = 6406;
    public const int RGB = 6407;
    public const int RGBA = 6408;
    public const int LUMINANCE = 6409;
    public const int LUMINANCE_ALPHA = 6410;

    public const int NEAREST = 9728;
    public const int LINEAR = 9729;
    public const int NEAREST_MIPMAP_NEAREST = 9984;
    public const int LINEAR_MIPMAP_NEAREST = 9985;
    public const int NEAREST_MIPMAP_LINEAR = 9986;
    public const int LINEAR_MIPMAP_LINEAR = 9987;

    public const int CLAMP_TO_EDGE = 33071;
    public const int MIRRORED_REPEAT = 33648;
    public const int REPEAT = 10497;

    public const int FUNC_ADD = 32774;
    public const int FUNC_SUBTRACT = 32778;
    public const int FUNC_REVERSE_SUBTRACT = 32779;

    public const int ZERO = 0;
    public const int ONE = 1;
    public const int SRC_COLOR = 768;
    public const int ONE_MINUS_SRC_COLOR = 769;
    public const int SRC_ALPHA = 770;
    public const int ONE_MINUS_SRC_ALPHA = 771;
    public const int DST_ALPHA = 772;
    public const int ONE_MINUS_DST_ALPHA = 773;
    public const int DST_COLOR = 774;
    public const int ONE_MINUS_DST_COLOR = 775;
    public const int SRC_ALPHA_SATURATE = 776;
    public const int CONSTANT_COLOR = 32769;
    public const int ONE_MINUS_CONSTANT_COLOR = 32770;
    public const int CONSTANT_ALPHA = 32771;
    public const int ONE_MINUS_CONSTANT_ALPHA = 32772;

    public const int UNSIGNED_SHORT_4_4_4_4 = 32819;
    public const int UNSIGNED_SHORT_5_5_5_1 = 32820;
    public const int UNSIGNED_SHORT_5_6_5 = 33635;

    public static readonly int[] TARGETS = { ARRAY_BUFFER, ELEMENT_ARRAY_BUFFER };

    public const int ARRAY_BUFFER = 34962;
    public const int ELEMENT_ARRAY_BUFFER = 34963;

    public const int FRAGMENT_SHADER = 35632;
    public const int VERTEX_SHADER = 35633;

    public const int FLOAT_VEC2 = 35664;
    public const int FLOAT_VEC3 = 35665;
    public const int FLOAT_VEC4 = 35666;
    public const int INT_VEC2 = 35667;
    public const int INT_VEC3 = 35668;
    public const int INT_VEC4 = 35669;
    public const int BOOL = 35670;
    public const int BOOL_VEC2 = 35671;
    public const int BOOL_VEC3 = 35672;
    public const int BOOL_VEC4 = 35673;
    public const int FLOAT_MAT2 = 35674;
    public const int FLOAT_MAT3 = 35675;
    public const int FLOAT_MAT4 = 35676;
    public const int SAMPLER_2D = 35678;

    public static readonly Dictionary<int, int> TYPE_LENGTHS = new()
    {
        [BYTE] = 1,
        [UNSIGNED_BYTE] = 1,
        [SHORT] = 1,
        [UNSIGNED_SHORT] = 1,
        [INT] = 1,
        [UNSIGNED_INT] = 1,
        [FLOAT] = 1,
        [FLOAT_VEC2] = 2,
        [FLOAT_VEC3] = 3,
        [FLOAT_VEC4] = 4,
        [INT_VEC2] = 2,
        [INT_VEC3] = 3,
        [INT_VEC4] = 4,
        [BOOL] = 1,
        [BOOL_VEC2] = 2,
        [BOOL_VEC3] = 3,
        [BOOL_VEC4] = 4,
        [FLOAT_MAT2] = 4,
        [FLOAT_MAT3] = 9,
        [FLOAT_MAT4] = 16,
        [SAMPLER_2D] = 1,
    };

    public static readonly Dictionary<int, string> TYPE_NAMES = new()
    {
        [BYTE] = "BYTE",
        [UNSIGNED_BYTE] = "UNSIGNED_BYTE",
        [SHORT] = "SHORT",
        [UNSIGNED_SHORT] = "UNSIGNED_SHORT",
        [INT] = "INT",
        [UNSIGNED_INT] = "UNSIGNED_INT",
        [FLOAT] = "FLOAT",
        [FLOAT_VEC2] = "FLOAT_VEC2",
        [FLOAT_VEC3] = "FLOAT_VEC3",
        [FLOAT_VEC4] = "FLOAT_VEC4",
        [INT_VEC2] = "INT_VEC2",
        [INT_VEC3] = "INT_VEC3",
        [INT_VEC4] = "INT_VEC4",
        [BOOL] = "BOOL",
        [BOOL_VEC2] = "BOOL_VEC2",
        [BOOL_VEC3] = "BOOL_VEC3",
        [BOOL_VEC4] = "BOOL_VEC4",
        [FLOAT_MAT2] = "FLOAT_MAT2",
        [FLOAT_MAT3] = "FLOAT_MAT3",
        [FLOAT_MAT4] = "FLOAT_MAT4",
        [SAMPLER_2D] = "SAMPLER_2D",
    };

    /// <summary>Dart <c>gl.TYPE_NAMES[type]</c>: null for unknown types (Dart prints "null" then).</summary>
    public static string? TypeName(int type) => TYPE_NAMES.TryGetValue(type, out var name) ? name : null;

    public static long TypeMin(int type)
    {
        switch (type)
        {
            case UNSIGNED_BYTE:
            case UNSIGNED_SHORT:
            case UNSIGNED_INT:
                return 0;
            case BYTE:
                return -128;
            case SHORT:
                return -32768;
            case INT:
                return -2147483648L;
            default:
                throw new ArgumentException("Invalid GL type", nameof(type));
        }
    }

    public static long TypeMax(int type)
    {
        switch (type)
        {
            case BYTE:
                return 127;
            case UNSIGNED_BYTE:
                return 255;
            case SHORT:
                return 32767;
            case UNSIGNED_SHORT:
                return 65535;
            case INT:
                return 2147483647;
            case UNSIGNED_INT:
                return 4294967295L;
            default:
                throw new ArgumentException("Invalid GL type", nameof(type));
        }
    }

    public static readonly int[] BOOL_TYPES = { BOOL, BOOL_VEC2, BOOL_VEC3, BOOL_VEC4 };

    public static readonly int[] FLOAT_TYPES =
    {
        FLOAT,
        FLOAT_VEC2,
        FLOAT_VEC3,
        FLOAT_VEC4,
        FLOAT_MAT2,
        FLOAT_MAT3,
        FLOAT_MAT4,
    };

    public static readonly int[] INT_TYPES =
    {
        BYTE,
        UNSIGNED_BYTE,
        SHORT,
        UNSIGNED_SHORT,
        INT,
        UNSIGNED_INT,
        INT_VEC2,
        INT_VEC3,
        INT_VEC4,
    };
}
