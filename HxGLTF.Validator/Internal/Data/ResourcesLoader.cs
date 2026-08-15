// Port of lib/src/data_access/resources_loader.dart

namespace HxGLTF.Validator.Internal;

// Dart: _Storage
internal enum ResourceStorage { DataUri, BufferView, GLB, External }

/// <summary>Dart ResourceInfo; converted to the public <see cref="ValidationResource"/> via <see cref="ToValidationResource"/>.</summary>
internal sealed class ResourceInfo
{
    public readonly string Pointer;
    public string? MimeType;
    public ResourceStorage? Storage;
    public long? ByteLength;
    public string? Uri;
    public ImageInfo? Image;

    public ResourceInfo(string pointer)
    {
        Pointer = pointer;
    }

    private static readonly string[] StorageString = { "data-uri", "buffer-view", "glb", "external" };

    /// <summary>Dart ResourceInfo.toMap().</summary>
    public ValidationResource ToValidationResource() => new()
    {
        Pointer = Pointer,
        MimeType = MimeType,
        Storage = Storage != null ? StorageString[(int)Storage.Value] : null,
        Uri = Uri,
        ByteLength = ByteLength,
        Image = Image?.ToValidationImageInfo(),
    };
}

internal sealed class ResourcesLoader
{
    public readonly Gltf Gltf;
    public readonly Context Context;

    // Dart: externalBytesFetch([Uri uri]) - called without a URI it returns the GLB BIN chunk.
    private readonly byte[]? _glbBinaryBuffer;

    // Dart: externalBytesFetch(uri) / externalStreamFetch(uri). Synchronous here: given the URI as written,
    // returns the resource bytes, null when the resource does not exist, or throws (message becomes IO_ERROR).
    private readonly Func<string, byte[]?>? _externalFetch;

    public ResourcesLoader(Context context, Gltf gltf, byte[]? glbBinaryBuffer, Func<string, byte[]?>? externalFetch)
    {
        Context = context;
        Gltf = gltf;
        _glbBinaryBuffer = glbBinaryBuffer;
        _externalFetch = externalFetch;
    }

    // Dart: externalBytesFetch(uri) may return null (resource intentionally not fetched, e.g. non-relative URIs
    // in the reference harness) which skips the resource silently; a missing file throws
    // GltfExternalResourceNotFoundException. Resolvers signal "missing" with FileNotFoundException.
    private byte[]? FetchExternal(GltfUri uri)
    {
        if (_externalFetch == null) return null;
        try
        {
            return _externalFetch(uri.ToString());
        }
        catch (FileNotFoundException)
        {
            throw new GltfExternalResourceNotFoundException(uri.ToString());
        }
        catch (DirectoryNotFoundException)
        {
            throw new GltfExternalResourceNotFoundException(uri.ToString());
        }
    }

    public void Load(bool validateAccessorData = true)
    {
        try
        {
            LoadBuffers();
            LoadImages();
            if (Context.Validate)
            {
                if (validateAccessorData)
                {
                    ValidateAccessors.ValidateAccessorsData(Gltf, Context);
                }

                Gltf.ValidateResources(Context);
            }
        }
        catch (IssuesLimitExceededException)
        {
            return;
        }
    }

    private void LoadBuffers()
    {
        Context.Path.Clear();
        Context.Push(Members.BUFFERS);

        for (var i = 0; i < Gltf.Buffers.Length; i++)
        {
            var buffer = Gltf.Buffers[i];
            if (buffer == null)
            {
                continue;
            }

            Context.Push(i);

            var info = new ResourceInfo(Context.GetPointerString())
            {
                MimeType = Members.APPLICATION_GLTF_BUFFER,
            };

            byte[]? FetchBuffer(Buffer buffer)
            {
                // Ignore buffers with invalid byte length
                if (buffer.ByteLength == -1)
                {
                    return null;
                }
                if (buffer.Uri != null)
                {
                    // External fetch
                    info.Storage = ResourceStorage.External;
                    info.Uri = buffer.Uri.ToString();
                    return FetchExternal(buffer.Uri);
                }
                else if (buffer.Data != null)
                {
                    // Data URI
                    info.Storage = ResourceStorage.DataUri;
                    return buffer.Data;
                }
                else if (Context.IsGlb && i == 0 && !buffer.HasUri)
                {
                    // GLB Buffer
                    info.Storage = ResourceStorage.GLB;
                    var data = _glbBinaryBuffer;
                    if (Context.Validate && data == null)
                    {
                        Context.AddIssue(LinkError.BufferMissingGlbData);
                    }
                    return data;
                }
                return null;
            }

            byte[]? data = null;
            try
            {
                data = FetchBuffer(buffer);
            }
            catch (Exception e) when (e is not IssuesLimitExceededException)
            {
                // likely IO error
                Context.AddIssue(IoError.IoErrorIssue, args: new object?[] { e.Message }, name: Members.URI);
            }

            if (data != null)
            {
                info.ByteLength = data.Length;
                if (data.Length < buffer.ByteLength)
                {
                    Context.AddIssue(DataError.BufferByteLengthMismatch,
                        args: new object?[] { data.Length, buffer.ByteLength });
                }
                else
                {
                    if (Context.IsGlb && i == 0 && !buffer.HasUri)
                    {
                        var paddedLength = JsonUtils.PadLength(buffer.ByteLength);
                        if (data.Length > paddedLength)
                        {
                            Context.AddIssue(DataError.BufferGlbChunkTooBig,
                                args: new object?[] { data.Length - paddedLength });
                        }
                    }
                    buffer.Data ??= data;
                }
            }
            Context.AddResource(info.ToValidationResource());
            Context.Pop();
        }
    }

    private void LoadImages()
    {
        Context.Path.Clear();
        Context.Push(Members.IMAGES);

        for (var i = 0; i < Gltf.Images.Length; i++)
        {
            var image = Gltf.Images[i];
            if (image == null)
            {
                continue;
            }

            Context.Push(i);

            var resourceInfo = new ResourceInfo(Context.GetPointerString());

            byte[]? FetchImageData(Image image)
            {
                if (image.Extensions.Count == 0)
                {
                    if (image.Uri != null)
                    {
                        // External fetch
                        resourceInfo.Storage = ResourceStorage.External;
                        resourceInfo.Uri = image.Uri.ToString();
                        return FetchExternal(image.Uri);
                    }
                    else if (image.Data != null)
                    {
                        // Data URI, preloaded on phase 2 of GltfLoader
                        resourceInfo.Storage = ResourceStorage.DataUri;
                        return image.Data;
                    }
                    else if (image.BufferView != null)
                    {
                        // BufferView
                        resourceInfo.Storage = ResourceStorage.BufferView;
                        image.TryLoadFromBufferView();
                        if (image.Data != null)
                        {
                            return image.Data;
                        }
                    }
                }
                return null;
            }

            byte[]? imageData = null;
            try
            {
                imageData = FetchImageData(image);
            }
            catch (Exception e) when (e is not IssuesLimitExceededException)
            {
                // likely IO error
                Context.AddIssue(IoError.IoErrorIssue, args: new object?[] { e.Message }, name: Members.URI);
            }

            ImageInfo? imageInfo = null;
            if (imageData != null)
            {
                try
                {
                    imageInfo = ImageInfo.Parse(imageData);
                    if (Context.Validate &&
                        !Context.ImageMimeTypes.Contains(imageInfo.MimeType))
                    {
                        Context.AddIssue(DataError.ImageNonEnabledMimeType,
                            args: new object?[] { imageInfo.MimeType });
                    }
                }
                catch (UnsupportedImageFormatException)
                {
                    Context.AddIssue(DataError.ImageUnrecognizedFormat);
                }
                catch (UnexpectedEndOfStreamException)
                {
                    Context.AddIssue(DataError.ImageUnexpectedEos);
                }
                catch (InvalidDataFormatException e)
                {
                    Context.AddIssue(DataError.ImageDataInvalid, args: new object?[] { e.Message });
                }
                catch (Exception e) when (e is not IssuesLimitExceededException)
                {
                    // TODO: refactor npm wrapper to remove this
                    Context.AddIssue(IoError.IoErrorIssue, args: new object?[] { e.Message }, name: Members.URI);
                }
                if (imageInfo != null)
                {
                    resourceInfo.MimeType = imageInfo.MimeType;

                    if (Context.Validate)
                    {
                        if (image.MimeType != null &&
                            image.MimeType != imageInfo.MimeType)
                        {
                            Context.AddIssue(DataError.ImageMimeTypeInvalid,
                                args: new object?[] { imageInfo.MimeType, image.MimeType },
                                name: resourceInfo.Storage == ResourceStorage.BufferView
                                    ? Members.BUFFER_VIEW
                                    : Members.URI);
                        }

                        if (!JsonUtils.IsPot(imageInfo.Width) || !JsonUtils.IsPot(imageInfo.Height))
                        {
                            Context.AddIssue(DataError.ImageNonPowerOfTwoDimensions,
                                args: new object?[] { imageInfo.Width, imageInfo.Height });
                        }

                        if (imageInfo.HasCustomColorInfo ||
                            imageInfo.HasAnimation ||
                            imageInfo.HasNonSquarePixels)
                        {
                            Context.AddIssue(DataError.ImageFeaturesUnsupported);
                        }
                    }

                    // Store image metadata in glTF image object
                    image.Info = imageInfo;

                    // Store image metadata in ResourceInfo
                    resourceInfo.Image = imageInfo;
                }
            }
            Context.AddResource(resourceInfo.ToValidationResource());
            Context.Pop();
        }
    }
}
