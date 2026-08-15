// Port of lib/src/ext/KHR_materials_transmission/khr_materials_transmission.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class KhrMaterialsTransmissionExtension
{
    public const string KHR_MATERIALS_TRANSMISSION = "KHR_materials_transmission";

    public const string TRANSMISSION_FACTOR = "transmissionFactor";
    public const string TRANSMISSION_TEXTURE = "transmissionTexture";

    public static readonly IReadOnlyList<string> KHR_MATERIALS_TRANSMISSION_MEMBERS = new[]
    {
        TRANSMISSION_FACTOR,
        TRANSMISSION_TEXTURE,
    };

    public static readonly Extension Value = new(
        KHR_MATERIALS_TRANSMISSION,
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Material)] = new ExtensionDescriptor(KhrMaterialsTransmission.FromMap),
        });
}

internal sealed class KhrMaterialsTransmission : GltfProperty
{
    public readonly double TransmissionFactor;
    public readonly TextureInfo? TransmissionTexture;

    private KhrMaterialsTransmission(double transmissionFactor, TextureInfo? transmissionTexture,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        TransmissionFactor = transmissionFactor;
        TransmissionTexture = transmissionTexture;
    }

    public static KhrMaterialsTransmission FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, KhrMaterialsTransmissionExtension.KHR_MATERIALS_TRANSMISSION_MEMBERS, context);
        }

        var transmissionFactor = JsonUtils.GetFloat(map, KhrMaterialsTransmissionExtension.TRANSMISSION_FACTOR, context, min: 0, max: 1, def: 0);
        var transmissionTexture = JsonUtils.GetObjectFromInnerMap<TextureInfo>(
            map, KhrMaterialsTransmissionExtension.TRANSMISSION_TEXTURE, context, TextureInfo.FromMap);

        var extensions = JsonUtils.GetExtensions(map, typeof(KhrMaterialsTransmission), context);

        var transmission = new KhrMaterialsTransmission(transmissionFactor, transmissionTexture, extensions, JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(transmission, new object?[] { transmissionTexture }.Concat(extensions.Values));

        return transmission;
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (TransmissionTexture != null)
        {
            context.Push(KhrMaterialsTransmissionExtension.TRANSMISSION_TEXTURE);
            TransmissionTexture.Link(gltf, context);
            context.Pop();
        }
    }
}
