// Port of lib/src/base/texture.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Texture : GltfChildOfRootProperty, IResourceValidatable
{
    private readonly int _samplerIndex;
    private readonly int _sourceIndex;

    private Sampler? _sampler;
    private Image? _source;

    private Texture(int samplerIndex, int sourceIndex, string? name,
        Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        _samplerIndex = samplerIndex;
        _sourceIndex = sourceIndex;
    }

    public Sampler? Sampler => _sampler;
    public Image? Source => _source;

    public static Texture FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.TEXTURE_MEMBERS, context);
        }

        return new Texture(
            JsonUtils.GetIndex(map, Members.SAMPLER, context, req: false),
            JsonUtils.GetIndex(map, Members.SOURCE, context, req: false),
            JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Texture), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        _source = gltf.Images[_sourceIndex];
        _sampler = gltf.Samplers[_samplerIndex];

        if (context.Validate)
        {
            if (_sourceIndex != -1)
            {
                if (_source == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference,
                        name: Members.SOURCE, args: new object?[] { _sourceIndex });
                }
                else
                {
                    _source.MarkAsUsed();
                }
            }

            if (_samplerIndex != -1)
            {
                if (_sampler == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference,
                        name: Members.SAMPLER, args: new object?[] { _samplerIndex });
                }
                else
                {
                    _sampler.MarkAsUsed();
                }
            }
        }
    }

    public void ValidateResources(Gltf gltf, Context context)
    {
        // The core spec allows only JPEG and PNG.
        var types = new[] { Members.IMAGE_JPEG, Members.IMAGE_PNG };
        var mimeType = _source?.MimeType ?? _source?.Info?.MimeType;
        if (mimeType != null && !types.Contains(mimeType))
        {
            context.AddIssue(LinkError.TextureInvalidImageMimeType,
                name: Members.SOURCE, args: new object?[] { mimeType, types });
        }
    }
}
