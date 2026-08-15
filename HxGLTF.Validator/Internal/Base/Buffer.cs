// Port of lib/src/base/buffer.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Buffer : GltfChildOfRootProperty
{
    public readonly GltfUri? Uri;
    public readonly int ByteLength;

    /// <summary>
    /// Users of this class need a way of distinguishing between
    /// a buffer with broken URI and a buffer without URI.
    /// The <see cref="Uri"/> field will be null in both cases.
    /// </summary>
    public readonly bool HasUri;

    public byte[]? Data;

    private Buffer(GltfUri? uri, byte[]? data, int byteLength, bool hasUri, string? name,
        Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        Uri = uri;
        Data = data;
        ByteLength = byteLength;
        HasUri = hasUri;
    }

    public static Buffer FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.BUFFER_MEMBERS, context);
        }

        var byteLength = JsonUtils.GetUint(map, Members.BYTE_LENGTH, context, min: 1, req: true);

        GltfUri? uri = null;
        byte[]? data = null;
        var hasUri = JsonUtils.Has(map, Members.URI);

        if (hasUri)
        {
            var uriString = JsonUtils.GetString(map, Members.URI, context);

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

                    // Dart: switch on the lower-cased mime type
                    var mimeType = uriData.MimeType.ToLowerInvariant();
                    if (mimeType == Members.APPLICATION_GLTF_BUFFER || mimeType == Members.APPLICATION_OCTET_STREAM)
                    {
                        data = uriData.ContentAsBytes();
                    }
                    else
                    {
                        context.AddIssue(SemanticError.BufferDataUriMimeTypeInvalid,
                            name: Members.URI, args: new object?[] { uriData.MimeType });
                    }
                }
            }
        }

        return new Buffer(uri, data, byteLength, hasUri, JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Buffer), context), JsonUtils.GetExtras(map, context));
    }
}
