// Port of lib/src/ext/EXT_texture_webp/ext_texture_webp.dart

using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal static class ExtTextureWebPExtension
{
    public const string IMAGE_WEBP = "image/webp";

    public static readonly Extension Value = new(
        "EXT_texture_webp",
        new Dictionary<Type, ExtensionDescriptor>
        {
            [typeof(Texture)] = new ExtensionDescriptor(ExtTextureWebPTexture.FromMap),
        },
        init: Init);

    private static void Init(Context context)
    {
        context.ImageMimeTypes.Add(IMAGE_WEBP);
    }

    public static readonly IReadOnlyList<string> EXT_TEXTURE_WEBP_TEXTURE_MEMBERS = new[] { Members.SOURCE };
}

internal sealed class ExtTextureWebPTexture : GltfProperty, IResourceValidatable
{
    private readonly int _sourceIndex;

    private Image? _source;

    public Image? Source => _source;

    private ExtTextureWebPTexture(int sourceIndex, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        _sourceIndex = sourceIndex;
    }

    public static ExtTextureWebPTexture FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, ExtTextureWebPExtension.EXT_TEXTURE_WEBP_TEXTURE_MEMBERS, context);
        }

        return new ExtTextureWebPTexture(
            JsonUtils.GetIndex(map, Members.SOURCE, context, req: false),
            JsonUtils.GetExtensions(map, typeof(ExtTextureWebPTexture), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        _source = gltf.Images[_sourceIndex];
        if (context.Validate && _sourceIndex != -1)
        {
            if (_source == null)
            {
                context.AddIssue(LinkError.UnresolvedReference, name: Members.SOURCE, args: new object?[] { _sourceIndex });
            }
            else
            {
                _source.MarkAsUsed();
            }
        }
    }

    public void ValidateResources(Gltf gltf, Context context)
    {
        var mimeType = _source?.MimeType ?? _source?.Info?.MimeType;
        if (mimeType != null && mimeType != ExtTextureWebPExtension.IMAGE_WEBP)
        {
            context.AddIssue(LinkError.TextureInvalidImageMimeType, name: Members.SOURCE,
                args: new object?[] { mimeType, new[] { ExtTextureWebPExtension.IMAGE_WEBP } });
        }
    }
}
