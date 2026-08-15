// Port of lib/src/ext/extensions.dart (kDefaultExtensions; the descriptor types live in GltfProperty.cs)

namespace HxGLTF.Validator.Internal;

internal static class Extensions
{
    public static readonly IReadOnlyList<Extension> Default = new[]
    {
        ExtTextureWebPExtension.Value,
        KhrAnimationPointerExtension.Value,
        KhrLightsPunctualExtension.Value,
        KhrMaterialsAnisotropyExtension.Value,
        KhrMaterialsClearcoatExtension.Value,
        KhrMaterialsDispersionExtension.Value,
        KhrMaterialsEmissiveStrengthExtension.Value,
        KhrMaterialsIorExtension.Value,
        KhrMaterialsIridescenceExtension.Value,
        KhrMaterialsPbrSpecularGlossinessExtension.Value,
        KhrMaterialsSheenExtension.Value,
        KhrMaterialsSpecularExtension.Value,
        KhrMaterialsTransmissionExtension.Value,
        KhrMaterialsUnlitExtension.Value,
        KhrMaterialsVariantsExtension.Value,
        KhrMaterialsVolumeExtension.Value,
        KhrMeshQuantizationExtension.Value,
        KhrNodeVisibilityExtension.Value,
        KhrTextureTransformExtension.Value,
    };
}
