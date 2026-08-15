using HxGLTF.Core;

namespace HxGLTF.Validator;

/// <summary>
/// Bridges the validator and the HxGLTF loader: validate and load in one go, sharing the file bytes and the
/// external resources, and merge the validation issues into <see cref="GLTFFile.Report"/>.
/// </summary>
public static class GLTFReaderValidation
{
    /// <summary>
    /// Validate and load a file. The file and its external buffers/images are read once and shared by validator and
    /// loader (only the JSON text is tokenized by both). Validation issues are appended to <c>file.Report</c>
    /// (severity mapped 1:1) and the full <see cref="ValidationReport"/> is returned as well.
    /// </summary>
    public static GLTFFile ReadValidated(string path, out ValidationReport validation, GLTFReadOptions? readOptions = null, ValidationOptions? validationOptions = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? "";
        var bytes = File.ReadAllBytes(fullPath);

        var cache = new ResourceCache(directory);
        readOptions ??= new GLTFReadOptions();
        readOptions.ResourceResolver ??= cache.Resolve;
        validationOptions ??= new ValidationOptions();
        validationOptions.ExternalResourceResolver ??= cache.Resolve;
        validationOptions.Uri ??= path.Replace('\\', '/');

        validation = GLTFValidator.Validate(bytes, Path.GetFileName(fullPath), validationOptions);
        var file = GLTFReader.Read(bytes, fullPath, readOptions);
        Merge(file.Report, validation);
        return file;
    }

    /// <summary>Same as <see cref="ReadValidated(string, out ValidationReport, GLTFReadOptions?, ValidationOptions?)"/> without the separate report.</summary>
    public static GLTFFile ReadValidated(string path, GLTFReadOptions? readOptions = null, ValidationOptions? validationOptions = null)
        => ReadValidated(path, out _, readOptions, validationOptions);

    /// <summary>Validate the file a loaded <see cref="GLTFFile"/> came from and append the issues to its report.</summary>
    public static ValidationReport Validate(this GLTFFile file, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (string.IsNullOrEmpty(file.Path) || !File.Exists(file.Path))
            throw new InvalidOperationException("The GLTFFile has no file path to validate (loaded from memory).");
        var report = GLTFValidator.Validate(file.Path, options);
        Merge(file.Report, report);
        return report;
    }

    /// <summary>Append all validation issues to a load report.</summary>
    public static void Merge(GLTFLoadReport target, ValidationReport validation)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(validation);
        foreach (var m in validation.ToLoadMessages())
            target.Add(m.Severity, m.Code, m.Pointer, m.Message);
    }

    private sealed class ResourceCache
    {
        private readonly string _directory;
        private readonly Dictionary<string, byte[]?> _cache = new(StringComparer.Ordinal);

        public ResourceCache(string directory) => _directory = directory;

        public byte[]? Resolve(string uri)
        {
            if (_cache.TryGetValue(uri, out var cached))
            {
                if (cached == null) throw new FileNotFoundException("Resource not found", uri);
                return cached;
            }
            byte[]? bytes = null;
            try
            {
                var path = Path.Combine(_directory, Uri.UnescapeDataString(uri));
                if (File.Exists(path)) bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or UriFormatException or ArgumentException)
            {
                bytes = null;
            }
            _cache[uri] = bytes;
            if (bytes == null) throw new FileNotFoundException("Resource not found", uri);
            return bytes;
        }
    }
}
