// Port of lib/src/base/members.dart

namespace HxGLTF.Validator.Internal;

internal static class Members
{
    public const string GLTF = "glTF";

    // GltfProperty
    public const string EXTENSIONS = "extensions";
    public const string EXTRAS = "extras";

    // GltfChildOfRootProperty
    public const string NAME = "name";

    // Accessor
    public const string BUFFER_VIEW = "bufferView";
    public const string BYTE_OFFSET = "byteOffset";
    public const string COMPONENT_TYPE = "componentType";
    public const string COUNT = "count";
    public const string TYPE = "type";
    public const string NORMALIZED = "normalized";
    public const string MAX = "max";
    public const string MIN = "min";
    public const string SPARSE = "sparse";

    public static readonly string[] ACCESSOR_MEMBERS =
    {
        BUFFER_VIEW,
        BYTE_OFFSET,
        COMPONENT_TYPE,
        COUNT,
        TYPE,
        NORMALIZED,
        MAX,
        MIN,
        SPARSE,
        NAME,
    };

    // Accessor types
    public const string SCALAR = "SCALAR";
    public const string VEC2 = "VEC2";
    public const string VEC3 = "VEC3";
    public const string VEC4 = "VEC4";
    public const string MAT2 = "MAT2";
    public const string MAT3 = "MAT3";
    public const string MAT4 = "MAT4";

    /// <summary>Ordered like the Dart const map (insertion order matters for VALUE_NOT_IN_LIST messages).</summary>
    public static readonly Dictionary<string, int> ACCESSOR_TYPES_LENGTHS = new(StringComparer.Ordinal)
    {
        [SCALAR] = 1,
        [VEC2] = 2,
        [VEC3] = 3,
        [VEC4] = 4,
        [MAT2] = 4,
        [MAT3] = 9,
        [MAT4] = 16,
    };

    // AccessorSparse
    public const string INDICES = "indices";
    public const string VALUES = "values";

    public static readonly string[] ACCESSOR_SPARSE_MEMBERS = { COUNT, INDICES, VALUES };

    // AccessorSparseIndices
    public static readonly string[] ACCESSOR_SPARSE_INDICES_MEMBERS =
    {
        BUFFER_VIEW,
        BYTE_OFFSET,
        COMPONENT_TYPE,
    };

    // AccessorSparseValues
    public static readonly string[] ACCESSOR_SPARSE_VALUES_MEMBERS =
    {
        BUFFER_VIEW,
        BYTE_OFFSET,
    };

    // Animation
    public const string CHANNELS = "channels";
    public const string SAMPLERS = "samplers";

    public static readonly string[] ANIMATION_MEMBERS = { CHANNELS, SAMPLERS, NAME };

    // AnimationChannel
    public const string TARGET = "target";
    public const string SAMPLER = "sampler";

    public static readonly string[] ANIMATION_CHANNEL_MEMBERS = { TARGET, SAMPLER };

    // AnimationChannelTarget
    public const string NODE = "node";
    public const string PATH = "path";

    public static readonly string[] ANIMATION_CHANNEL_TARGET_MEMBERS = { NODE, PATH };

    public static readonly string[] ANIMATION_CHANNEL_TARGET_PATHS =
    {
        TRANSLATION,
        ROTATION,
        SCALE,
        WEIGHTS,
    };

    // AnimationSampler
    public const string INPUT = "input";
    public const string INTERPOLATION = "interpolation";
    public const string OUTPUT = "output";

    public const string LINEAR = "LINEAR";
    public const string STEP = "STEP";
    public const string CUBICSPLINE = "CUBICSPLINE";

    public static readonly string[] ANIMATION_SAMPLER_MEMBERS =
    {
        INPUT,
        INTERPOLATION,
        OUTPUT,
    };

    public static readonly string[] ANIMATION_SAMPLER_INTERPOLATIONS =
    {
        LINEAR,
        STEP,
        CUBICSPLINE,
    };

    public static readonly AccessorFormat ANIMATION_SAMPLER_INPUT_FORMAT = new(SCALAR, Gl.FLOAT);

    public static readonly Dictionary<string, AccessorFormat[]> ANIMATION_SAMPLER_OUTPUT_FORMATS = new(StringComparer.Ordinal)
    {
        [TRANSLATION] = new[] { new AccessorFormat(VEC3, Gl.FLOAT) },
        [ROTATION] = new[]
        {
            new AccessorFormat(VEC4, Gl.FLOAT),
            new AccessorFormat(VEC4, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(VEC4, Gl.BYTE, normalized: true),
            new AccessorFormat(VEC4, Gl.UNSIGNED_SHORT, normalized: true),
            new AccessorFormat(VEC4, Gl.SHORT, normalized: true),
        },
        [SCALE] = new[] { new AccessorFormat(VEC3, Gl.FLOAT) },
        [WEIGHTS] = new[]
        {
            new AccessorFormat(SCALAR, Gl.FLOAT),
            new AccessorFormat(SCALAR, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(SCALAR, Gl.BYTE, normalized: true),
            new AccessorFormat(SCALAR, Gl.UNSIGNED_SHORT, normalized: true),
            new AccessorFormat(SCALAR, Gl.SHORT, normalized: true),
        },
    };

    // Asset
    public const string COPYRIGHT = "copyright";
    public const string GENERATOR = "generator";
    public const string VERSION = "version";
    public const string MIN_VERSION = "minVersion";

    public static readonly string[] ASSET_MEMBERS =
    {
        COPYRIGHT,
        GENERATOR,
        VERSION,
        MIN_VERSION,
    };

    // Buffer
    public const string URI = "uri";
    public const string BYTE_LENGTH = "byteLength";

    public static readonly string[] BUFFER_MEMBERS = { URI, BYTE_LENGTH, NAME };

    public const string APPLICATION_OCTET_STREAM = "application/octet-stream";
    public const string APPLICATION_GLTF_BUFFER = "application/gltf-buffer";

    // BufferView
    public const string BUFFER = "buffer";
    public const string BYTE_STRIDE = "byteStride";

    public static readonly string[] BUFFER_VIEW_MEMBERS =
    {
        BUFFER,
        BYTE_OFFSET,
        BYTE_LENGTH,
        BYTE_STRIDE,
        TARGET,
        NAME,
    };

    // Camera
    public const string ORTHOGRAPHIC = "orthographic";
    public const string PERSPECTIVE = "perspective";

    public static readonly string[] CAMERA_MEMBERS =
    {
        TYPE,
        ORTHOGRAPHIC,
        PERSPECTIVE,
        NAME,
    };

    public static readonly string[] CAMERA_TYPES = { ORTHOGRAPHIC, PERSPECTIVE };

    // CameraOrthographic
    public const string XMAG = "xmag";
    public const string YMAG = "ymag";
    public const string ZFAR = "zfar";
    public const string ZNEAR = "znear";

    public static readonly string[] CAMERA_ORTHOGRAPHIC_MEMBERS =
    {
        XMAG,
        YMAG,
        ZFAR,
        ZNEAR,
    };

    // CameraPerspective
    public const string ASPECT_RATIO = "aspectRatio";
    public const string YFOV = "yfov";

    public static readonly string[] CAMERA_PERSPECTIVE_MEMBERS =
    {
        ASPECT_RATIO,
        YFOV,
        ZFAR,
        ZNEAR,
    };

    // Gltf
    public const string EXTENSIONS_USED = "extensionsUsed";
    public const string EXTENSIONS_REQUIRED = "extensionsRequired";
    public const string ACCESSORS = "accessors";
    public const string ANIMATIONS = "animations";
    public const string ASSET = "asset";
    public const string BUFFERS = "buffers";
    public const string BUFFER_VIEWS = "bufferViews";
    public const string CAMERAS = "cameras";
    public const string IMAGES = "images";
    public const string MATERIALS = "materials";
    public const string MESHES = "meshes";
    public const string NODES = "nodes";
    public const string SCENE = "scene";
    public const string SCENES = "scenes";
    public const string SKINS = "skins";
    public const string TEXTURES = "textures";

    public static readonly string[] GLTF_MEMBERS =
    {
        EXTENSIONS_USED,
        EXTENSIONS_REQUIRED,
        ACCESSORS,
        ANIMATIONS,
        ASSET,
        BUFFERS,
        BUFFER_VIEWS,
        CAMERAS,
        IMAGES,
        MATERIALS,
        MESHES,
        NODES,
        SAMPLERS,
        SCENE,
        SCENES,
        SKINS,
        TEXTURES,
    };

    // Image
    public const string MIME_TYPE = "mimeType";
    public static readonly string[] IMAGE_MEMBERS = { BUFFER_VIEW, MIME_TYPE, URI, NAME };

    public const string IMAGE_JPEG = "image/jpeg";
    public const string IMAGE_PNG = "image/png";

    // Material
    public const string PBR_METALLIC_ROUGHNESS = "pbrMetallicRoughness";
    public const string NORMAL_TEXTURE = "normalTexture";
    public const string OCCLUSION_TEXTURE = "occlusionTexture";
    public const string EMISSIVE_TEXTURE = "emissiveTexture";
    public const string EMISSIVE_FACTOR = "emissiveFactor";
    public const string ALPHA_MODE = "alphaMode";
    public const string ALPHA_CUTOFF = "alphaCutoff";
    public const string DOUBLE_SIDED = "doubleSided";

    // material.alphaMode
    public const string OPAQUE = "OPAQUE";
    public const string MASK = "MASK";
    public const string BLEND = "BLEND";

    public static readonly string[] MATERIAL_ALPHA_MODES = { OPAQUE, MASK, BLEND };

    public static readonly string[] MATERIAL_MEMBERS =
    {
        PBR_METALLIC_ROUGHNESS,
        NORMAL_TEXTURE,
        OCCLUSION_TEXTURE,
        EMISSIVE_TEXTURE,
        EMISSIVE_FACTOR,
        ALPHA_MODE,
        ALPHA_CUTOFF,
        DOUBLE_SIDED,
        NAME,
    };

    // PbrMetallicRoughness
    public const string BASE_COLOR_FACTOR = "baseColorFactor";
    public const string BASE_COLOR_TEXTURE = "baseColorTexture";
    public const string METALLIC_FACTOR = "metallicFactor";
    public const string ROUGHNESS_FACTOR = "roughnessFactor";
    public const string METALLIC_ROUGHNESS_TEXTURE = "metallicRoughnessTexture";

    public static readonly string[] PBR_METALLIC_ROUGHNESS_MEMBERS =
    {
        BASE_COLOR_FACTOR,
        BASE_COLOR_TEXTURE,
        METALLIC_FACTOR,
        ROUGHNESS_FACTOR,
        METALLIC_ROUGHNESS_TEXTURE,
    };

    // Mesh
    public const string PRIMITIVES = "primitives";
    public const string WEIGHTS = "weights";

    public static readonly string[] MESH_MEMBERS = { PRIMITIVES, WEIGHTS, NAME };

    // MeshPrimitive
    public const string ATTRIBUTES = "attributes";
    public const string MATERIAL = "material";
    public const string MODE = "mode";
    public const string TARGETS = "targets";

    public static readonly string[] MESH_PRIMITIVE_MEMBERS =
    {
        ATTRIBUTES,
        INDICES,
        MATERIAL,
        MODE,
        TARGETS,
    };

    public static readonly AccessorFormat[] MESH_PRIMITIVE_INDICES_FORMATS =
    {
        new AccessorFormat(SCALAR, Gl.UNSIGNED_BYTE),
        new AccessorFormat(SCALAR, Gl.UNSIGNED_SHORT),
        new AccessorFormat(SCALAR, Gl.UNSIGNED_INT),
    };

    // Node
    public const string CAMERA = "camera";
    public const string CHILDREN = "children";
    public const string SKIN = "skin";
    public const string MATRIX = "matrix";
    public const string MESH = "mesh";
    public const string ROTATION = "rotation";
    public const string SCALE = "scale";
    public const string TRANSLATION = "translation";

    public static readonly string[] NODE_MEMBERS =
    {
        CAMERA,
        CHILDREN,
        SKIN,
        MATRIX,
        MESH,
        ROTATION,
        SCALE,
        TRANSLATION,
        WEIGHTS,
        NAME,
    };

    // Sampler
    public const string MAG_FILTER = "magFilter";
    public const string MIN_FILTER = "minFilter";
    public const string WRAP_S = "wrapS";
    public const string WRAP_T = "wrapT";

    public static readonly string[] SAMPLER_MEMBERS =
    {
        MAG_FILTER,
        MIN_FILTER,
        WRAP_S,
        WRAP_T,
        NAME,
    };

    public static readonly int[] MAG_FILTERS = { Gl.NEAREST, Gl.LINEAR };

    public static readonly int[] MIN_FILTERS =
    {
        Gl.NEAREST,
        Gl.LINEAR,
        Gl.NEAREST_MIPMAP_NEAREST,
        Gl.LINEAR_MIPMAP_NEAREST,
        Gl.NEAREST_MIPMAP_LINEAR,
        Gl.LINEAR_MIPMAP_LINEAR,
    };

    public static readonly int[] WRAP_FILTERS =
    {
        Gl.CLAMP_TO_EDGE,
        Gl.MIRRORED_REPEAT,
        Gl.REPEAT,
    };

    // Scene
    public static readonly string[] SCENE_MEMBERS = { NODES, NAME };

    // Skin
    public const string INVERSE_BIND_MATRICES = "inverseBindMatrices";
    public const string SKELETON = "skeleton";
    public const string JOINTS = "joints";

    public static readonly string[] SKIN_MEMBERS =
    {
        INVERSE_BIND_MATRICES,
        SKELETON,
        JOINTS,
        NAME,
    };

    public static readonly AccessorFormat SKIN_IBM_FORMAT = new(MAT4, Gl.FLOAT);

    // Attribute semantics
    public const string POSITION = "POSITION";
    public const string NORMAL = "NORMAL";
    public const string TANGENT = "TANGENT";
    public const string TEXCOORD_ = "TEXCOORD";
    public const string COLOR_ = "COLOR";
    public const string JOINTS_ = "JOINTS";
    public const string WEIGHTS_ = "WEIGHTS";

    public static readonly string[] ATTRIBUTE_SEMANTIC_MEMBERS =
    {
        POSITION,
        NORMAL,
        TANGENT,
    };

    public static readonly string[] ATTRIBUTE_SEMANTIC_ARRAY_MEMBERS =
    {
        COLOR_,
        JOINTS_,
        TEXCOORD_,
        WEIGHTS_,
    };

    public static readonly string[] ATTRIBUTE_SEMANTIC_MORPH_TARGET_ARRAY_MEMBERS =
    {
        COLOR_,
        TEXCOORD_,
    };

    public static readonly Dictionary<int, string> ATTRIBUTE_TYPES = new()
    {
        [Gl.FLOAT] = SCALAR,
        [Gl.FLOAT_VEC2] = VEC2,
        [Gl.FLOAT_VEC3] = VEC3,
        [Gl.FLOAT_VEC4] = VEC4,
        [Gl.FLOAT_MAT2] = MAT2,
        [Gl.FLOAT_MAT3] = MAT3,
        [Gl.FLOAT_MAT4] = MAT4,
    };

    // Texture
    public const string SOURCE = "source";

    public static readonly string[] TEXTURE_MEMBERS = { SAMPLER, SOURCE, NAME };

    // TextureInfo
    public const string INDEX = "index";
    public const string TEX_COORD = "texCoord";

    public static readonly string[] TEXTURE_INFO_MEMBERS =
    {
        INDEX,
        TEX_COORD,
    };

    // NormalTextureInfo
    public static readonly string[] NORMAL_TEXTURE_INFO_MEMBERS =
    {
        INDEX,
        TEX_COORD,
        SCALE,
    };

    // OcclusionTextureInfo
    public const string STRENGTH = "strength";

    public static readonly string[] OCCLUSION_TEXTURE_INFO_MEMBERS =
    {
        INDEX,
        TEX_COORD,
        STRENGTH,
    };
}

/// <summary>Dart <c>BufferViewUsage</c> (members.dart): a const-instance class, not an enum; <c>toString()</c> is the bare name.</summary>
internal sealed class BufferViewUsage
{
    private readonly string _value;
    public readonly int Target;

    private BufferViewUsage(string value, int target = -1)
    {
        _value = value;
        Target = target;
    }

    public static readonly BufferViewUsage IBM = new("IBM");
    public static readonly BufferViewUsage Image = new("Image");
    public static readonly BufferViewUsage IndexBuffer = new("IndexBuffer", Gl.ELEMENT_ARRAY_BUFFER);
    public static readonly BufferViewUsage Other = new("Other");
    public static readonly BufferViewUsage VertexBuffer = new("VertexBuffer", Gl.ARRAY_BUFFER);

    public override string ToString() => _value;
}

/// <summary>Dart <c>AccessorUsage</c> (members.dart): a const-instance class, not an enum; <c>toString()</c> is the bare name.</summary>
internal sealed class AccessorUsage
{
    private readonly string _value;

    private AccessorUsage(string value)
    {
        _value = value;
    }

    public static readonly AccessorUsage AnimationInput = new("AnimationInput");
    public static readonly AccessorUsage AnimationOutput = new("AnimationOutput");
    public static readonly AccessorUsage IBM = new("IBM");
    public static readonly AccessorUsage PrimitiveIndices = new("PrimitiveIndices");
    public static readonly AccessorUsage VertexAttribute = new("VertexAttribute");

    public override string ToString() => _value;
}

/// <summary>Dart <c>AccessorFormat</c> (members.dart): value type describing (type, componentType, normalized).</summary>
internal sealed class AccessorFormat : IEquatable<AccessorFormat>
{
    public readonly string Type;
    public readonly int ComponentType;
    public readonly bool Normalized;

    public AccessorFormat(string type, int componentType, bool normalized = false)
    {
        Type = type;
        ComponentType = componentType;
        Normalized = normalized;
    }

    public static AccessorFormat FromAccessor(Accessor accessor)
        => new(accessor.Type!, accessor.ComponentType, normalized: accessor.Normalized);

    // Dart: '{$type, ${gl.TYPE_NAMES[componentType]}${normalized ? ' $NORMALIZED' : ''}}'
    public override string ToString()
        => "{" + Type + ", " + DartFormat.V(Gl.TypeName(ComponentType)) + (Normalized ? " " + Members.NORMALIZED : "") + "}";

    public bool Equals(AccessorFormat? other)
        => other is not null
           && string.Equals(other.Type, Type, StringComparison.Ordinal)
           && other.ComponentType == ComponentType
           && other.Normalized == Normalized;

    public override bool Equals(object? obj) => obj is AccessorFormat other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Type, ComponentType, Normalized);

    public static bool operator ==(AccessorFormat? left, AccessorFormat? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(AccessorFormat? left, AccessorFormat? right) => !(left == right);
}
