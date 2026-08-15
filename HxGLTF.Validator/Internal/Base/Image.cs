// Port of lib/src/base/image.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Image : GltfChildOfRootProperty
{
    private readonly int _bufferViewIndex;
    public readonly string? MimeType;
    public readonly GltfUri? Uri;

    public byte[]? Data;
    private BufferView? _bufferView;

    public ImageInfo? Info;

    private Image(int bufferViewIndex, GltfUri? uri, string? mimeType, byte[]? data,
        string? name, Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        _bufferViewIndex = bufferViewIndex;
        Uri = uri;
        MimeType = mimeType;
        Data = data;
    }

    public BufferView? BufferView => _bufferView;

    public static Image FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.IMAGE_MEMBERS, context);
        }

        var bufferViewIndex = JsonUtils.GetIndex(map, Members.BUFFER_VIEW, context, req: false);
        var mimeType =
            JsonUtils.GetString(map, Members.MIME_TYPE, context, list: context.ImageMimeTypes);
        var uriString = JsonUtils.GetString(map, Members.URI, context, req: false);

        if (context.Validate)
        {
            if (bufferViewIndex != -1 && mimeType == null)
            {
                context.AddIssue(SchemaError.UnsatisfiedDependency,
                    name: Members.BUFFER_VIEW, args: new object?[] { Members.MIME_TYPE });
            }

            if (((bufferViewIndex != -1) && (uriString != null)) ||
                ((bufferViewIndex == -1) && (uriString == null)))
            {
                context.AddIssue(SchemaError.OneOfMismatch, args: new object?[] { Members.BUFFER_VIEW, Members.URI });
            }
        }

        GltfUri? uri = null;
        byte[]? data = null;

        if (uriString != null)
        {
            if (context.IsGlb)
            {
                context.AddIssue(DataError.UriGlb, name: Members.URI);
            }

            DartUriData? uriData = null;
            try
            {
                uriData = DartUriData.Parse(uriString);
            }
            catch (DartUriFormatException)
            {
                uri = JsonUtils.GetUri(uriString, context);
            }

            if (uriData != null)
            {
                if (context.IsGlb)
                {
                    context.AddIssue(DataError.DataUriGlb, name: Members.URI);
                }

                data = uriData.ContentAsBytes();

                // Dart: ImageInfo.detectCodec(data)?.mimeType != uriData.mimeType.toLowerCase()
                var codec = ImageInfo.DetectCodec(data);
                var detectedMimeType = codec.HasValue ? codec.Value.MimeType() : null;
                var declaredMimeType = uriData.MimeType.ToLowerInvariant();
                if (context.Validate &&
                    detectedMimeType != declaredMimeType)
                {
                    context.AddIssue(SchemaError.InvalidUri, name: Members.URI, args: new object?[]
                    {
                        uriString,
                        "The declared mediatype does not match the embedded content."
                    });
                    // Drop invalid data
                    data = null;
                }
            }
        }

        return new Image(bufferViewIndex, uri, mimeType, data, JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Image), context), JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (_bufferViewIndex != -1)
        {
            _bufferView = gltf.BufferViews[_bufferViewIndex];

            if (_bufferView == null)
            {
                context.AddIssue(LinkError.UnresolvedReference,
                    name: Members.BUFFER_VIEW, args: new object?[] { _bufferViewIndex });
            }
            else
            {
                _bufferView.SetUsage(BufferViewUsage.Image, Members.BUFFER_VIEW, context);
                if (_bufferView.ByteStride != -1)
                {
                    context.AddIssue(LinkError.ImageBufferViewWithByteStride,
                        name: Members.BUFFER_VIEW);
                }
            }
        }
    }

    public void TryLoadFromBufferView()
    {
        if (_bufferView?.Buffer?.Data != null)
        {
            // in the worst case, `data` will remain `null`
            // Dart: Uint8List.view(buffer, byteOffset, byteLength) throws ArgumentError (caught) when the
            // range does not fit into the buffer or byteOffset/byteLength are negative.
            var buffer = _bufferView.Buffer.Data;
            long byteOffset = _bufferView.ByteOffset;
            long byteLength = _bufferView.ByteLength;
            if (byteOffset >= 0 && byteLength >= 0 && byteOffset + byteLength <= buffer.Length)
            {
                var view = new byte[byteLength];
                Array.Copy(buffer, byteOffset, view, 0, byteLength);
                Data = view;
            }
        }
    }
}
