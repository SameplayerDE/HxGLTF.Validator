// Port of lib/src/errors.dart (issue type definitions; the Issue class lives in Context.cs)

namespace HxGLTF.Validator.Internal;

internal static class IssueConstants
{
    // These values are slightly greater
    // than the maximum error from signed 8-bit quantization
    public const double UnitLengthThresholdVec3 = 0.00674;
    public const double UnitLengthThresholdVec4 = 0.00769;

    // This value is slightly greater
    // than the maximum error from unsigned 8-bit quantization
    // 1..2 elements - 0 * step
    // 3..4 elements - 1 * step
    // 5..6 elements - 2 * step
    // ...
    public const double UnitSumThresholdStep = 0.0039216;
}

internal static class IssueFormat
{
    /// <summary>Dart <c>(x as Iterable).map(_q)</c> printed: <c>('a', 'b')</c> (every element quoted).</summary>
    public static string QIter(object? o)
    {
        var items = o switch
        {
            DartFormat.Iterable it => it.Items,
            string s => new object?[] { s },
            System.Collections.IEnumerable e => e.Cast<object?>(),
            _ => new[] { o },
        };
        return "(" + string.Join(", ", items.Select(DartFormat.Q)) + ")";
    }
}

internal static class DataError
{
    private static IssueType Make(string code, Func<IReadOnlyList<object?>, string> message, ValidationSeverity severity = ValidationSeverity.Error)
        => new(code, message, severity);

    public static readonly IssueType BufferByteLengthMismatch = Make(
        "BUFFER_BYTE_LENGTH_MISMATCH",
        args => $"Actual data byte length ({DartFormat.V(args[0])}) is less than the declared buffer byte length ({DartFormat.V(args[1])}).");

    public static readonly IssueType BufferGlbChunkTooBig = Make(
        "BUFFER_GLB_CHUNK_TOO_BIG",
        args => $"GLB-stored BIN chunk contains {DartFormat.V(args[0])} extra padding byte(s).",
        ValidationSeverity.Warning);

    public static readonly IssueType AccessorMinMismatch = Make(
        "ACCESSOR_MIN_MISMATCH",
        args => $"Declared minimum value for this component ({DartFormat.V(args[0])}) does not match actual minimum ({DartFormat.V(args[1])}).");

    public static readonly IssueType AccessorMaxMismatch = Make(
        "ACCESSOR_MAX_MISMATCH",
        args => $"Declared maximum value for this component ({DartFormat.V(args[0])}) does not match actual maximum ({DartFormat.V(args[1])}).");

    public static readonly IssueType AccessorElementOutOfMinBound = Make(
        "ACCESSOR_ELEMENT_OUT_OF_MIN_BOUND",
        args => $"Accessor contains {DartFormat.V(args[0])} element(s) less than declared minimum value {DartFormat.V(args[1])}.");

    public static readonly IssueType AccessorElementOutOfMaxBound = Make(
        "ACCESSOR_ELEMENT_OUT_OF_MAX_BOUND",
        args => $"Accessor contains {DartFormat.V(args[0])} element(s) greater than declared maximum value {DartFormat.V(args[1])}.");

    public static readonly IssueType AccessorVector3NonUnit = Make(
        "ACCESSOR_VECTOR3_NON_UNIT",
        args => $"Vector3 at accessor indices {DartFormat.V(args[0])}..{DartFormat.V(args[1])} is not of unit length: {DartFormat.V(args[2])}.");

    public static readonly IssueType AccessorInvalidSign = Make(
        "ACCESSOR_INVALID_SIGN",
        args => $"Vector3 with sign at accessor indices {DartFormat.V(args[0])}..{DartFormat.V(args[1])} has invalid w component: {DartFormat.V(args[2])}. Must be 1.0 or -1.0.");

    public static readonly IssueType AccessorAnimationSamplerOutputNonNormalizedQuaternion = Make(
        "ACCESSOR_ANIMATION_SAMPLER_OUTPUT_NON_NORMALIZED_QUATERNION",
        args => $"Animation sampler output accessor element at indices {DartFormat.V(args[0])}..{DartFormat.V(args[1])} is not of unit length: {DartFormat.V(args[2])}.");

    public static readonly IssueType AccessorNonClamped = Make(
        "ACCESSOR_NON_CLAMPED",
        args => $"Accessor element at index {DartFormat.V(args[0])} is not clamped to 0..1 range: {DartFormat.V(args[1])}.");

    public static readonly IssueType AccessorInvalidFloat = Make(
        "ACCESSOR_INVALID_FLOAT",
        args => $"Accessor element at index {DartFormat.V(args[0])} is {DartFormat.V(args[1])}.");

    public static readonly IssueType AccessorIndexOob = Make(
        "ACCESSOR_INDEX_OOB",
        args => $"Indices accessor element at index {DartFormat.V(args[0])} has value {DartFormat.V(args[1])} that is greater than the maximum vertex index available ({DartFormat.V(args[2])}).");

    public static readonly IssueType AccessorIndexTriangleDegenerate = Make(
        "ACCESSOR_INDEX_TRIANGLE_DEGENERATE",
        args => $"Indices accessor contains {DartFormat.V(args[0])} degenerate triangles (out of {DartFormat.V(args[1])}).",
        ValidationSeverity.Information);

    public static readonly IssueType AccessorIndexPrimitiveRestart = Make(
        "ACCESSOR_INDEX_PRIMITIVE_RESTART",
        args => $"Indices accessor contains primitive restart value ({DartFormat.V(args[0])}) at index {DartFormat.V(args[1])}.");

    public static readonly IssueType AccessorAnimationInputNegative = Make(
        "ACCESSOR_ANIMATION_INPUT_NEGATIVE",
        args => $"Animation input accessor element at index {DartFormat.V(args[0])} is negative: {DartFormat.V(args[1])}.");

    public static readonly IssueType AccessorAnimationInputNonIncreasing = Make(
        "ACCESSOR_ANIMATION_INPUT_NON_INCREASING",
        args => $"Animation input accessor element at index {DartFormat.V(args[0])} is less than or equal to previous: {DartFormat.V(args[1])} <= {DartFormat.V(args[2])}.");

    public static readonly IssueType AccessorSparseIndicesNonIncreasing = Make(
        "ACCESSOR_SPARSE_INDICES_NON_INCREASING",
        args => $"Accessor sparse indices element at index {DartFormat.V(args[0])} is less than or equal to previous: {DartFormat.V(args[1])} <= {DartFormat.V(args[2])}.");

    public static readonly IssueType AccessorSparseIndexOob = Make(
        "ACCESSOR_SPARSE_INDEX_OOB",
        args => $"Accessor sparse indices element at index {DartFormat.V(args[0])} is greater than or equal to the number of accessor elements: {DartFormat.V(args[1])} >= {DartFormat.V(args[2])}.");

    public static readonly IssueType AccessorInvalidInverseBindMatrix = Make(
        "ACCESSOR_INVALID_IBM",
        args => $"Matrix element at index {DartFormat.V(args[0])} (component index {DartFormat.V(args[1])}) contains invalid value: {DartFormat.V(args[2])}.");

    public static readonly IssueType ImageDataInvalid = Make(
        "IMAGE_DATA_INVALID",
        args => $"Image data is invalid. {DartFormat.V(args[0])}");

    public static readonly IssueType ImageMimeTypeInvalid = Make(
        "IMAGE_MIME_TYPE_INVALID",
        args => $"Recognized image format {DartFormat.Q(args[0])} does not match declared image format {DartFormat.Q(args[1])}.");

    public static readonly IssueType ImageUnexpectedEos = Make(
        "IMAGE_UNEXPECTED_EOS",
        args => "Unexpected end of image stream.");

    public static readonly IssueType ImageUnrecognizedFormat = Make(
        "IMAGE_UNRECOGNIZED_FORMAT",
        args => "Image format not recognized.",
        ValidationSeverity.Warning);

    public static readonly IssueType ImageNonEnabledMimeType = Make(
        "IMAGE_NON_ENABLED_MIME_TYPE",
        args => $"{DartFormat.Q(args[0])} MIME type requires an extension.");

    public static readonly IssueType ImageNonPowerOfTwoDimensions = Make(
        "IMAGE_NPOT_DIMENSIONS",
        args => $"Image has non-power-of-two dimensions: {DartFormat.V(args[0])}x{DartFormat.V(args[1])}.",
        ValidationSeverity.Information);

    public static readonly IssueType ImageFeaturesUnsupported = Make(
        "IMAGE_FEATURES_UNSUPPORTED",
        args => "Image contains unsupported features like non-default colorspace information, non-square pixels, or animation.",
        ValidationSeverity.Warning);

    public static readonly IssueType UriGlb = Make(
        "URI_GLB",
        args => "URI is used in GLB container.",
        ValidationSeverity.Information);

    public static readonly IssueType DataUriGlb = Make(
        "DATA_URI_GLB",
        args => "Data URI is used in GLB container.",
        ValidationSeverity.Warning);

    public static readonly IssueType AccessorJointsIndexOob = Make(
        "ACCESSOR_JOINTS_INDEX_OOB",
        args => $"Joints accessor element at index {DartFormat.V(args[0])} (component index {DartFormat.V(args[1])}) has value {DartFormat.V(args[2])} that is greater than the maximum joint index ({DartFormat.V(args[3])}) set by skin {DartFormat.V(args[4])}.");

    public static readonly IssueType AccessorJointsIndexDuplicate = Make(
        "ACCESSOR_JOINTS_INDEX_DUPLICATE",
        args => $"Joints accessor element at index {DartFormat.V(args[0])} (component index {DartFormat.V(args[1])}) has value {DartFormat.V(args[2])} that is already in use for the vertex.");

    public static readonly IssueType AccessorWeightsNegative = Make(
        "ACCESSOR_WEIGHTS_NEGATIVE",
        args => $"Weights accessor element at index {DartFormat.V(args[0])} (component index {DartFormat.V(args[1])}) has negative value {DartFormat.V(args[2])}.");

    public static readonly IssueType AccessorWeightsNonNormalized = Make(
        "ACCESSOR_WEIGHTS_NON_NORMALIZED",
        args => $"Weights accessor elements (at indices {DartFormat.V(args[0])}..{DartFormat.V(args[1])}) have non-normalized sum: {DartFormat.V(args[2])}.");

    public static readonly IssueType AccessorJointsUsedZeroWeight = Make(
        "ACCESSOR_JOINTS_USED_ZERO_WEIGHT",
        args => $"Joints accessor element at index {DartFormat.V(args[0])} (component index {DartFormat.V(args[1])}) is used with zero weight but has non-zero value ({DartFormat.V(args[2])}).",
        ValidationSeverity.Warning);
}

internal static class IoError
{
    private static IssueType Make(string code, Func<IReadOnlyList<object?>, string> message, ValidationSeverity severity = ValidationSeverity.Error)
        => new(code, message, severity);

    // Dart: (args) => args[0].toString()
    public static readonly IssueType IoErrorIssue = Make(
        "IO_ERROR",
        args => DartFormat.V(args[0]));
}

internal static class SchemaError
{
    private static IssueType Make(string code, Func<IReadOnlyList<object?>, string> message, ValidationSeverity severity = ValidationSeverity.Error)
        => new(code, message, severity);

    public static readonly IssueType ArrayLengthNotInList = Make(
        "ARRAY_LENGTH_NOT_IN_LIST",
        args => $"Invalid array length {DartFormat.V(args[0])}. Valid lengths are: {DartFormat.MbqIter(args[1])}.");

    public static readonly IssueType ArrayTypeMismatch = Make(
        "ARRAY_TYPE_MISMATCH",
        args => $"Type mismatch. Array element {DartFormat.Mbq(args[0])} is not a {DartFormat.Q(args[1])}.");

    public static readonly IssueType ArrayDuplicateElements = Make(
        "DUPLICATE_ELEMENTS",
        args => "Duplicate element.");

    public static readonly IssueType InvalidIndex = Make(
        "INVALID_INDEX",
        args => "Index must be a non-negative integer.");

    public static readonly IssueType InvalidJson = Make(
        "INVALID_JSON",
        args => $"Invalid JSON data. Parser output: {DartFormat.V(args[0])}");

    public static readonly IssueType InvalidUri = Make(
        "INVALID_URI",
        args => $"Invalid URI {DartFormat.Q(args[0])}. Parser output:\n{DartFormat.V(args[1])}");

    public static readonly IssueType EmptyEntity = Make(
        "EMPTY_ENTITY",
        args => "Entity cannot be empty.");

    // Dart: 'Exactly one of ${args.map(_q)} properties must be defined.'
    public static readonly IssueType OneOfMismatch = Make(
        "ONE_OF_MISMATCH",
        args => $"Exactly one of {IssueFormat.QIter(args)} properties must be defined.");

    public static readonly IssueType PatternMismatch = Make(
        "PATTERN_MISMATCH",
        args => $"Value {DartFormat.Q(args[0])} does not match regexp pattern {DartFormat.Q(args[1])}.");

    public static readonly IssueType TypeMismatch = Make(
        "TYPE_MISMATCH",
        args => $"Type mismatch. Property value {DartFormat.Mbq(args[0])} is not a {DartFormat.Q(args[1])}.");

    public static readonly IssueType ValueNotInList = Make(
        "VALUE_NOT_IN_LIST",
        args => $"Invalid value {DartFormat.Mbq(args[0])}. Valid values are {DartFormat.MbqIter(args[1])}.",
        ValidationSeverity.Warning);

    public static readonly IssueType ValueNotInRange = Make(
        "VALUE_NOT_IN_RANGE",
        args => $"Value {DartFormat.V(args[0])} is out of range.");

    public static readonly IssueType ValueMultipleOf = Make(
        "VALUE_MULTIPLE_OF",
        args => $"Value {DartFormat.V(args[0])} is not a multiple of {DartFormat.V(args[1])}.");

    public static readonly IssueType UndefinedProperty = Make(
        "UNDEFINED_PROPERTY",
        args => $"Property {DartFormat.Q(args[0])} must be defined.");

    public static readonly IssueType UnexpectedProperty = Make(
        "UNEXPECTED_PROPERTY",
        args => "Unexpected property.",
        ValidationSeverity.Warning);

    public static readonly IssueType UnsatisfiedDependency = Make(
        "UNSATISFIED_DEPENDENCY",
        args => $"Dependency failed. {DartFormat.Q(args[0])} must be defined.");
}

internal static class SemanticError
{
    private static IssueType Make(string code, Func<IReadOnlyList<object?>, string> message, ValidationSeverity severity = ValidationSeverity.Error)
        => new(code, message, severity);

    public static readonly IssueType UnknownAssetMajorVersion = Make(
        "UNKNOWN_ASSET_MAJOR_VERSION",
        args => $"Unknown glTF major asset version: {DartFormat.V(args[0])}.");

    public static readonly IssueType UnknownAssetMinorVersion = Make(
        "UNKNOWN_ASSET_MINOR_VERSION",
        args => $"Unknown glTF minor asset version: {DartFormat.V(args[0])}.",
        ValidationSeverity.Warning);

    public static readonly IssueType MinVersionGreaterThanVersion = Make(
        "ASSET_MIN_VERSION_GREATER_THAN_VERSION",
        args => $"Asset minVersion {DartFormat.Q(args[0])} is greater than version {DartFormat.Q(args[1])}.");

    public static readonly IssueType InvalidGlValue = Make(
        "INVALID_GL_VALUE",
        args => $"Invalid value {DartFormat.V(args[0])} for GL type {DartFormat.Q(args[1])}.");

    public static readonly IssueType AccessorNormalizedInvalid = Make(
        "ACCESSOR_NORMALIZED_INVALID",
        args => "Only (u)byte and (u)short accessors can be normalized.");

    public static readonly IssueType AccessorOffsetAlignment = Make(
        "ACCESSOR_OFFSET_ALIGNMENT",
        args => $"Offset {DartFormat.V(args[0])} is not a multiple of componentType length {DartFormat.V(args[1])}.");

    public static readonly IssueType AccessorMatrixAlignment = Make(
        "ACCESSOR_MATRIX_ALIGNMENT",
        args => "Matrix accessors must be aligned to 4-byte boundaries.");

    public static readonly IssueType AccessorSparseCountOutOfRange = Make(
        "ACCESSOR_SPARSE_COUNT_OUT_OF_RANGE",
        args => $"Sparse accessor overrides more elements ({DartFormat.V(args[0])}) than the base accessor contains ({DartFormat.V(args[1])}).");

    public static readonly IssueType AnimationChannelTargetNodeSkin = Make(
        "ANIMATION_CHANNEL_TARGET_NODE_SKIN",
        args => "Animated TRS properties will not affect a skinned mesh.",
        ValidationSeverity.Warning);

    public static readonly IssueType BufferDataUriMimeTypeInvalid = Make(
        "BUFFER_DATA_URI_MIME_TYPE_INVALID",
        args => $"Data URI media type must be 'application/octet-stream' or 'application/gltf-buffer'. Found {DartFormat.Q(args[0])} instead.");

    public static readonly IssueType BufferViewTooBigByteStride = Make(
        "BUFFER_VIEW_TOO_BIG_BYTE_STRIDE",
        args => $"Buffer view's byteStride ({DartFormat.V(args[0])}) is greater than byteLength ({DartFormat.V(args[1])}).");

    public static readonly IssueType BufferViewInvalidByteStride = Make(
        "BUFFER_VIEW_INVALID_BYTE_STRIDE",
        args => "Only buffer views with raw vertex data can have byteStride.");

    public static readonly IssueType CameraXmagYmagNegative = Make(
        "CAMERA_XMAG_YMAG_NEGATIVE",
        args => "xmag and ymag should not be negative.",
        ValidationSeverity.Warning);

    public static readonly IssueType CameraXmagYmagZero = Make(
        "CAMERA_XMAG_YMAG_ZERO",
        args => "xmag and ymag must not be zero.");

    public static readonly IssueType CameraYFovGequalPi = Make(
        "CAMERA_YFOV_GEQUAL_PI",
        args => "yfov should be less than Pi.",
        ValidationSeverity.Warning);

    public static readonly IssueType CameraZfarLequalZnear = Make(
        "CAMERA_ZFAR_LEQUAL_ZNEAR",
        args => "zfar must be greater than znear.");

    public static readonly IssueType MaterialAlphaCutoffInvalidMode = Make(
        "MATERIAL_ALPHA_CUTOFF_INVALID_MODE",
        args => "Alpha cutoff is supported only for 'MASK' alpha mode.",
        ValidationSeverity.Warning);

    public static readonly IssueType MeshPrimitiveInvalidAttribute = Make(
        "MESH_PRIMITIVE_INVALID_ATTRIBUTE",
        args => "Invalid attribute name.");

    public static readonly IssueType MeshPrimitivesUnequalTargetsCount = Make(
        "MESH_PRIMITIVES_UNEQUAL_TARGETS_COUNT",
        args => "All primitives must have the same number of morph targets.");

    public static readonly IssueType MeshPrimitiveNoPosition = Make(
        "MESH_PRIMITIVE_NO_POSITION",
        args => "No POSITION attribute found.",
        ValidationSeverity.Warning);

    public static readonly IssueType MeshPrimitiveIndexedSemanticContinuity = Make(
        "MESH_PRIMITIVE_INDEXED_SEMANTIC_CONTINUITY",
        args => $"Indices for indexed attribute semantic {DartFormat.Q(args[0])} must start with 0 and be continuous. Total expected indices: {DartFormat.V(args[1])}, total provided indices: {DartFormat.V(args[2])}.");

    public static readonly IssueType MeshPrimitiveTangentWithoutNormal = Make(
        "MESH_PRIMITIVE_TANGENT_WITHOUT_NORMAL",
        args => "TANGENT attribute without NORMAL found.",
        ValidationSeverity.Warning);

    public static readonly IssueType MeshPrimitiveJointsWeightsMismatch = Make(
        "MESH_PRIMITIVE_JOINTS_WEIGHTS_MISMATCH",
        args => $"Number of JOINTS attribute semantics ({DartFormat.V(args[0])}) does not match the number of WEIGHTS ({DartFormat.V(args[1])}).");

    public static readonly IssueType MeshInvalidWeightsCount = Make(
        "MESH_INVALID_WEIGHTS_COUNT",
        args => $"The length of weights array ({DartFormat.V(args[0])}) does not match the number of morph targets ({DartFormat.V(args[1])}).");

    public static readonly IssueType NodeMatrixTrs = Make(
        "NODE_MATRIX_TRS",
        args => "A node can have either a matrix or any combination of translation/rotation/scale (TRS) properties.");

    public static readonly IssueType NodeDefaultMatrix = Make(
        "NODE_MATRIX_DEFAULT",
        args => "Do not specify default transform matrix.",
        ValidationSeverity.Information);

    public static readonly IssueType NodeNonTrsMatrix = Make(
        "NODE_MATRIX_NON_TRS",
        args => "Matrix must be decomposable to TRS.");

    public static readonly IssueType RotationNonUnit = Make(
        "ROTATION_NON_UNIT",
        args => "Rotation quaternion must be normalized.");

    public static readonly IssueType UnusedExtensionRequired = Make(
        "UNUSED_EXTENSION_REQUIRED",
        args => $"Unused extension {DartFormat.Q(args[0])} cannot be required.");

    public static readonly IssueType NonRequiredExtension = Make(
        "NON_REQUIRED_EXTENSION",
        args => $"Extension {DartFormat.Q(args[0])} cannot be optional.");

    public static readonly IssueType InvalidExtensionNameFormat = Make(
        "INVALID_EXTENSION_NAME_FORMAT",
        args => "Extension name has invalid format.",
        ValidationSeverity.Warning);

    public static readonly IssueType NodeEmpty = Make(
        "NODE_EMPTY",
        args => "Empty node encountered.",
        ValidationSeverity.Information);

    public static readonly IssueType NodeSkinnedMeshLocalTransforms = Make(
        "NODE_SKINNED_MESH_LOCAL_TRANSFORMS",
        args => "Local transforms will not affect a skinned mesh.",
        ValidationSeverity.Warning);

    public static readonly IssueType NodeSkinnedMeshParentTransforms = Make(
        "NODE_SKINNED_MESH_PARENT_TRANSFORMS",
        args => "Node with a skinned mesh has parent nodes with transforms. Parent transforms will not affect a skinned mesh.",
        ValidationSeverity.Warning);

    public static readonly IssueType NodeSkinnedMeshNonRoot = Make(
        "NODE_SKINNED_MESH_NON_ROOT",
        args => "Node with a skinned mesh is not root. Parent transforms will not affect a skinned mesh.",
        ValidationSeverity.Information);

    public static readonly IssueType NodeSkinNoScene = Make(
        "NODE_SKIN_NO_SCENE",
        args => "A node with a skinned mesh is used in a scene that does not contain joint nodes.");

    public static readonly IssueType SkinNoCommonRoot = Make(
        "SKIN_NO_COMMON_ROOT",
        args => "Joints do not have a common root.");

    public static readonly IssueType SkinSkeletonInvalid = Make(
        "SKIN_SKELETON_INVALID",
        args => "Skeleton node is not a common root.");

    public static readonly IssueType NonRelativeUri = Make(
        "NON_RELATIVE_URI",
        args => $"Non-relative URI found: {DartFormat.Q(args[0])}.",
        ValidationSeverity.Warning);

    public static readonly IssueType MultipleExtensions = Make(
        "MULTIPLE_EXTENSIONS",
        args => "This extension may be incompatible with other extensions for the object.",
        ValidationSeverity.Warning);

    public static readonly IssueType NonObjectExtras = Make(
        "NON_OBJECT_EXTRAS",
        args => "Prefer JSON Objects for extras.",
        ValidationSeverity.Information);

    public static readonly IssueType ExtraProperty = Make(
        "EXTRA_PROPERTY",
        args => "This property should not be defined as it will not be used.",
        ValidationSeverity.Information);

    public static readonly IssueType KhrAnimationPointerAnimationChannelTargetNode = Make(
        "KHR_ANIMATION_POINTER_ANIMATION_CHANNEL_TARGET_NODE",
        args => "This extension requires the animation channel target node to be undefined.");

    public static readonly IssueType KhrAnimationPointerAnimationChannelTargetPath = Make(
        "KHR_ANIMATION_POINTER_ANIMATION_CHANNEL_TARGET_PATH",
        args => $"This extension requires the animation channel target path to be 'pointer'. Found {DartFormat.Q(args[0])} instead.");

    public static readonly IssueType KhrLightsPunctualLightSpotAngles = Make(
        "KHR_LIGHTS_PUNCTUAL_LIGHT_SPOT_ANGLES",
        args => $"outerConeAngle ({DartFormat.V(args[1])}) is less than or equal to innerConeAngle ({DartFormat.V(args[0])}).");

    public static readonly IssueType KhrMaterialsAnisotropyAnisotropyTextureTexCoord = Make(
        "KHR_MATERIALS_ANISOTROPY_ANISOTROPY_TEXTURE_TEXCOORD",
        args => "Normal and anisotropy textures should use the same texture coords.",
        ValidationSeverity.Warning);

    public static readonly IssueType KhrMaterialsClearcoatClearcoatNormalTextureTexCoord = Make(
        "KHR_MATERIALS_CLEARCOAT_CLEARCOAT_NORMAL_TEXTURE_TEXCOORD",
        args => "Normal and clearcoat normal textures should use the same texture coords.",
        ValidationSeverity.Warning);

    public static readonly IssueType KhrMaterialsDispersionNoVolume = Make(
        "KHR_MATERIALS_DISPERSION_NO_VOLUME",
        args => "The dispersion extension needs to be combined with the volume extension.",
        ValidationSeverity.Warning);

    public static readonly IssueType KhrMaterialsEmissiveStrengthZeroFactor = Make(
        "KHR_MATERIALS_EMISSIVE_STRENGTH_ZERO_FACTOR",
        args => "Emissive strength has no effect when the emissive factor is zero or undefined.",
        ValidationSeverity.Warning);

    public static readonly IssueType KhrMaterialsVolumeNoTransmission = Make(
        "KHR_MATERIALS_VOLUME_NO_TRANSMISSION",
        args => "The volume extension needs to be combined with an extension that allows light to transmit through the surface.",
        ValidationSeverity.Warning);

    public static readonly IssueType KhrMaterialsVolumeDoubleSided = Make(
        "KHR_MATERIALS_VOLUME_DOUBLE_SIDED",
        args => "The volume extension should not be used with double-sided materials.",
        ValidationSeverity.Warning);

    public static readonly IssueType KhrMaterialsIridescenceThicknessRangeWithoutTexture = Make(
        "KHR_MATERIALS_IRIDESCENCE_THICKNESS_RANGE_WITHOUT_TEXTURE",
        args => "Thickness minimum has no effect when a thickness texture is not defined.",
        ValidationSeverity.Information);

    public static readonly IssueType KhrMaterialsIridescenceThicknessTextureUnused = Make(
        "KHR_MATERIALS_IRIDESCENCE_THICKNESS_TEXTURE_UNUSED",
        args => "Thickness texture has no effect when the thickness minimum is equal to the thickness maximum.",
        ValidationSeverity.Information);
}

internal static class LinkError
{
    private static IssueType Make(string code, Func<IReadOnlyList<object?>, string> message, ValidationSeverity severity = ValidationSeverity.Error)
        => new(code, message, severity);

    public static readonly IssueType AccessorTotalOffsetAlignment = Make(
        "ACCESSOR_TOTAL_OFFSET_ALIGNMENT",
        args => $"Accessor's total byteOffset {DartFormat.V(args[0])} isn't a multiple of componentType length {DartFormat.V(args[1])}.");

    public static readonly IssueType AccessorSmallStride = Make(
        "ACCESSOR_SMALL_BYTESTRIDE",
        args => $"Referenced bufferView's byteStride value {DartFormat.V(args[0])} is less than accessor element's length {DartFormat.V(args[1])}.");

    public static readonly IssueType AccessorTooLong = Make(
        "ACCESSOR_TOO_LONG",
        args => $"Accessor (offset: {DartFormat.V(args[0])}, length: {DartFormat.V(args[1])}) does not fit referenced bufferView [{DartFormat.V(args[2])}] length {DartFormat.V(args[3])}.");

    public static readonly IssueType AccessorUsageOverride = Make(
        "ACCESSOR_USAGE_OVERRIDE",
        args => $"Override of previously set accessor usage. Initial: {DartFormat.Q(args[0])}, new: {DartFormat.Q(args[1])}.");

    public static readonly IssueType AnimationDuplicateTargets = Make(
        "ANIMATION_DUPLICATE_TARGETS",
        args => $"Animation channel has the same target as channel {DartFormat.V(args[0])}.");

    public static readonly IssueType AnimationChannelTargetNodeMatrix = Make(
        "ANIMATION_CHANNEL_TARGET_NODE_MATRIX",
        args => "Animation channel cannot target TRS properties of a node with defined matrix.");

    public static readonly IssueType AnimationChannelTargetNodeWeightsNoMorphs = Make(
        "ANIMATION_CHANNEL_TARGET_NODE_WEIGHTS_NO_MORPHS",
        args => "Animation channel cannot target WEIGHTS when mesh does not have morph targets.");

    public static readonly IssueType AnimationSamplerInputAccessorWithoutBounds = Make(
        "ANIMATION_SAMPLER_INPUT_ACCESSOR_WITHOUT_BOUNDS",
        args => "accessor.min and accessor.max must be defined for animation input accessor.");

    public static readonly IssueType AnimationSamplerInputAccessorInvalidFormat = Make(
        "ANIMATION_SAMPLER_INPUT_ACCESSOR_INVALID_FORMAT",
        args => $"Invalid Animation sampler input accessor format {DartFormat.Q(args[0])}. Must be one of {IssueFormat.QIter(args[1])}.");

    public static readonly IssueType AnimationSamplerOutputAccessorInvalidFormat = Make(
        "ANIMATION_SAMPLER_OUTPUT_ACCESSOR_INVALID_FORMAT",
        args => $"Invalid animation sampler output accessor format {DartFormat.Q(args[0])} for path {DartFormat.Q(args[2])}. Must be one of {IssueFormat.QIter(args[1])}.");

    public static readonly IssueType AnimationSamplerInputAccessorTooFewElements = Make(
        "ANIMATION_SAMPLER_INPUT_ACCESSOR_TOO_FEW_ELEMENTS",
        args => $"Animation sampler output accessor with {DartFormat.Q(args[0])} interpolation must have at least {DartFormat.V(args[1])} elements. Got {DartFormat.V(args[2])}.");

    public static readonly IssueType AnimationSamplerOutputAccessorInvalidCount = Make(
        "ANIMATION_SAMPLER_OUTPUT_ACCESSOR_INVALID_COUNT",
        args => $"Animation sampler output accessor of count {DartFormat.V(args[0])} expected. Found {DartFormat.V(args[1])}.");

    public static readonly IssueType AnimationSamplerAccessorWithByteStride = Make(
        "ANIMATION_SAMPLER_ACCESSOR_WITH_BYTESTRIDE",
        args => "bufferView.byteStride must not be defined for buffer views used by animation sampler accessors.");

    public static readonly IssueType BufferMissingGlbData = Make(
        "BUFFER_MISSING_GLB_DATA",
        args => "Buffer refers to an unresolved GLB binary chunk.");

    public static readonly IssueType BufferViewTooLong = Make(
        "BUFFER_VIEW_TOO_LONG",
        args => $"BufferView does not fit buffer ({DartFormat.V(args[0])}) byteLength ({DartFormat.V(args[1])}).");

    public static readonly IssueType BufferViewTargetOverride = Make(
        "BUFFER_VIEW_TARGET_OVERRIDE",
        args => $"Override of previously set bufferView target or usage. Initial: {DartFormat.Q(args[0])}, new: {DartFormat.Q(args[1])}.");

    public static readonly IssueType BufferViewTargetMissing = Make(
        "BUFFER_VIEW_TARGET_MISSING",
        args => "bufferView.target should be set for vertex or index data.",
        ValidationSeverity.Hint);

    public static readonly IssueType ImageBufferViewWithByteStride = Make(
        "IMAGE_BUFFER_VIEW_WITH_BYTESTRIDE",
        args => "bufferView.byteStride must not be defined for buffer views containing image data.");

    public static readonly IssueType IncompleteExtensionSupport = Make(
        "INCOMPLETE_EXTENSION_SUPPORT",
        args => "Validation support for this extension is incomplete; the asset may have undetected issues.",
        ValidationSeverity.Information);

    public static readonly IssueType InvalidIbmAccessorCount = Make(
        "INVALID_IBM_ACCESSOR_COUNT",
        args => $"IBM accessor must have at least {DartFormat.V(args[0])} elements. Found {DartFormat.V(args[1])}.");

    public static readonly IssueType MeshPrimitiveAttributesAccessorInvalidFormat = Make(
        "MESH_PRIMITIVE_ATTRIBUTES_ACCESSOR_INVALID_FORMAT",
        args => $"Invalid accessor format {DartFormat.Q(args[0])} for this attribute semantic. Must be one of {IssueFormat.QIter(args[1])}.");

    public static readonly IssueType MeshPrimitiveAttributesAccessorUnsignedInt = Make(
        "MESH_PRIMITIVE_ATTRIBUTES_ACCESSOR_UNSIGNED_INT",
        args => "Mesh attributes cannot use UNSIGNED_INT component type.");

    public static readonly IssueType MeshPrimitivePositionAccessorWithoutBounds = Make(
        "MESH_PRIMITIVE_POSITION_ACCESSOR_WITHOUT_BOUNDS",
        args => "accessor.min and accessor.max must be defined for POSITION attribute accessor.");

    public static readonly IssueType MeshPrimitiveAccessorWithoutByteStride = Make(
        "MESH_PRIMITIVE_ACCESSOR_WITHOUT_BYTESTRIDE",
        args => "bufferView.byteStride must be defined when two or more accessors use the same buffer view.");

    public static readonly IssueType MeshPrimitiveAccessorUnaligned = Make(
        "MESH_PRIMITIVE_ACCESSOR_UNALIGNED",
        args => "Vertex attribute data must be aligned to 4-byte boundaries.");

    public static readonly IssueType MeshPrimitiveIndicesAccessorWithByteStride = Make(
        "MESH_PRIMITIVE_INDICES_ACCESSOR_WITH_BYTESTRIDE",
        args => "bufferView.byteStride must not be defined for indices accessor.");

    public static readonly IssueType MeshPrimitiveIndicesAccessorInvalidFormat = Make(
        "MESH_PRIMITIVE_INDICES_ACCESSOR_INVALID_FORMAT",
        args => $"Invalid indices accessor format {DartFormat.Q(args[0])}. Must be one of {IssueFormat.QIter(args[1])}. ");

    public static readonly IssueType MeshPrimitiveIncompatibleMode = Make(
        "MESH_PRIMITIVE_INCOMPATIBLE_MODE",
        args => $"Number of vertices or indices ({DartFormat.V(args[0])}) is not compatible with used drawing mode ({DartFormat.Q(args[1])}).",
        ValidationSeverity.Warning);

    public static readonly IssueType MeshPrimitiveTooFewTexcoords = Make(
        "MESH_PRIMITIVE_TOO_FEW_TEXCOORDS",
        args => $"Material is incompatible with mesh primitive: Texture binding {DartFormat.Q(args[0])} needs 'TEXCOORD_{DartFormat.V(args[1])}' attribute.");

    public static readonly IssueType MeshPrimitiveNoTangentSpace = Make(
        "MESH_PRIMITIVE_NO_TANGENT_SPACE",
        args => "Material requires a tangent space but the mesh primitive does not provide it and the material does not contain a normal map to generate it.");

    public static readonly IssueType MeshPrimitiveGeneratedTangentSpace = Make(
        "MESH_PRIMITIVE_GENERATED_TANGENT_SPACE",
        args => "Material requires a tangent space but the mesh primitive does not provide it. Runtime-generated tangent space may be non-portable across implementations.",
        ValidationSeverity.Warning);

    public static readonly IssueType MeshPrimitiveUnequalAccessorsCount = Make(
        "MESH_PRIMITIVE_UNEQUAL_ACCESSOR_COUNT",
        args => "All accessors of the same primitive must have the same count.");

    public static readonly IssueType MeshPrimitiveMorphTargetNoBaseAccessor = Make(
        "MESH_PRIMITIVE_MORPH_TARGET_NO_BASE_ACCESSOR",
        args => "The mesh primitive does not define this attribute semantic.");

    public static readonly IssueType MeshPrimitiveMorphTargetInvalidAttributeCount = Make(
        "MESH_PRIMITIVE_MORPH_TARGET_INVALID_ATTRIBUTE_COUNT",
        args => "Base accessor has different count.");

    public static readonly IssueType NodeLoop = Make(
        "NODE_LOOP",
        args => "Node is a part of a node loop.");

    public static readonly IssueType NodeParentOverride = Make(
        "NODE_PARENT_OVERRIDE",
        args => $"Value overrides parent of node {DartFormat.V(args[0])}.");

    // Dart: '... morph targets (${args[1] ?? 0}).'
    public static readonly IssueType NodeWeightsInvalid = Make(
        "NODE_WEIGHTS_INVALID",
        args => $"The length of weights array ({DartFormat.V(args[0])}) does not match the number of morph targets ({DartFormat.V(args[1] ?? 0)}).");

    public static readonly IssueType NodeSkinWithNonSkinnedMesh = Make(
        "NODE_SKIN_WITH_NON_SKINNED_MESH",
        args => "Node has skin defined, but mesh has no joints data.");

    public static readonly IssueType NodeSkinnedMeshWithoutSkin = Make(
        "NODE_SKINNED_MESH_WITHOUT_SKIN",
        args => "Node uses skinned mesh, but has no skin defined.",
        ValidationSeverity.Warning);

    public static readonly IssueType SceneNonRootNode = Make(
        "SCENE_NON_ROOT_NODE",
        args => $"Node {DartFormat.V(args[0])} is not a root node.");

    public static readonly IssueType SkinIbmInvalidFormat = Make(
        "SKIN_IBM_INVALID_FORMAT",
        args => $"Invalid IBM accessor format {DartFormat.Q(args[0])}. Must be one of {IssueFormat.QIter(args[1])}. ");

    public static readonly IssueType SkinIbmAccessorWithByteStride = Make(
        "SKIN_IBM_ACCESSOR_WITH_BYTESTRIDE",
        args => "bufferView.byteStride must not be defined for buffer views used by inverse bind matrices accessors.");

    public static readonly IssueType TextureInvalidImageMimeType = Make(
        "TEXTURE_INVALID_IMAGE_MIME_TYPE",
        args => $"Invalid MIME type {DartFormat.Q(args[0])} for the texture source. Valid MIME types are {IssueFormat.QIter(args[1])}.");

    public static readonly IssueType UndeclaredExtension = Make(
        "UNDECLARED_EXTENSION",
        args => "Extension is not declared in extensionsUsed.");

    public static readonly IssueType UnexpectedExtensionObject = Make(
        "UNEXPECTED_EXTENSION_OBJECT",
        args => "Unexpected location for this extension.");

    public static readonly IssueType UnresolvedReference = Make(
        "UNRESOLVED_REFERENCE",
        args => $"Unresolved reference: {DartFormat.V(args[0])}.");

    public static readonly IssueType UnsupportedExtension = Make(
        "UNSUPPORTED_EXTENSION",
        args => $"Cannot validate an extension as it is not supported by the validator: {DartFormat.Q(args[0])}.",
        ValidationSeverity.Information);

    public static readonly IssueType UnusedObject = Make(
        "UNUSED_OBJECT",
        args => "This object may be unused.",
        ValidationSeverity.Information);

    public static readonly IssueType UnusedMeshWeights = Make(
        "UNUSED_MESH_WEIGHTS",
        args => "The static morph target weights are always overridden.",
        ValidationSeverity.Information);

    public static readonly IssueType UnusedMeshTangent = Make(
        "UNUSED_MESH_TANGENT",
        args => "Tangents are not used because the material has no normal texture.",
        ValidationSeverity.Information);

    public static readonly IssueType KhrMaterialsVariantsNonUniqueVariant = Make(
        "KHR_MATERIALS_VARIANTS_NON_UNIQUE_VARIANT",
        args => "This variant is used more than once for this mesh primitive.");
}

internal static class GlbError
{
    private static IssueType Make(string code, Func<IReadOnlyList<object?>, string> message, ValidationSeverity severity = ValidationSeverity.Error)
        => new(code, message, severity);

    public static readonly IssueType InvalidMagic = Make(
        "GLB_INVALID_MAGIC",
        args => $"Invalid GLB magic value ({DartFormat.V(args[0])}).");

    public static readonly IssueType InvalidVersion = Make(
        "GLB_INVALID_VERSION",
        args => $"Invalid GLB version value {DartFormat.V(args[0])}.");

    public static readonly IssueType LengthTooSmall = Make(
        "GLB_LENGTH_TOO_SMALL",
        args => $"Declared GLB length ({DartFormat.V(args[0])}) is too small.");

    public static readonly IssueType ChunkLengthUnaligned = Make(
        "GLB_CHUNK_LENGTH_UNALIGNED",
        args => $"Length of {DartFormat.V(args[0])} chunk is not aligned to 4-byte boundaries.");

    public static readonly IssueType LengthMismatch = Make(
        "GLB_LENGTH_MISMATCH",
        args => $"Declared length ({DartFormat.V(args[0])}) does not match GLB length ({DartFormat.V(args[1])}).");

    public static readonly IssueType ChunkTooBig = Make(
        "GLB_CHUNK_TOO_BIG",
        args => $"Chunk ({DartFormat.V(args[0])}) length ({DartFormat.V(args[1])}) does not fit total GLB length.");

    public static readonly IssueType EmptyChunk = Make(
        "GLB_EMPTY_CHUNK",
        args => $"Chunk ({DartFormat.V(args[0])}) cannot have zero length.");

    public static readonly IssueType EmptyBinChunk = Make(
        "GLB_EMPTY_BIN_CHUNK",
        args => "Empty BIN chunk should be omitted.",
        ValidationSeverity.Information);

    public static readonly IssueType DuplicateChunk = Make(
        "GLB_DUPLICATE_CHUNK",
        args => $"Chunk of type {DartFormat.V(args[0])} has already been used.");

    public static readonly IssueType UnexpectedEndOfChunkHeader = Make(
        "GLB_UNEXPECTED_END_OF_CHUNK_HEADER",
        args => "Unexpected end of chunk header.");

    public static readonly IssueType UnexpectedEndOfChunkData = Make(
        "GLB_UNEXPECTED_END_OF_CHUNK_DATA",
        args => "Unexpected end of chunk data.");

    public static readonly IssueType UnexpectedEndOfHeader = Make(
        "GLB_UNEXPECTED_END_OF_HEADER",
        args => "Unexpected end of header.");

    public static readonly IssueType UnexpectedFirstChunk = Make(
        "GLB_UNEXPECTED_FIRST_CHUNK",
        args => $"First chunk must be of JSON type. Found {DartFormat.V(args[0])} instead.");

    public static readonly IssueType UnexpectedBinChunk = Make(
        "GLB_UNEXPECTED_BIN_CHUNK",
        args => "BIN chunk must be the second chunk.");

    public static readonly IssueType UnknownChunkType = Make(
        "GLB_UNKNOWN_CHUNK_TYPE",
        args => $"Unknown GLB chunk type: {DartFormat.V(args[0])}.",
        ValidationSeverity.Warning);

    public static readonly IssueType ExtraData = Make(
        "GLB_EXTRA_DATA",
        args => "Extra data after the end of GLB stream.",
        ValidationSeverity.Warning);
}
