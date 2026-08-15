using System.Text;
using System.Text.RegularExpressions;

namespace HxGLTF.Validator.Internal;

// Port of lib/src/context.dart and the Issue class of lib/src/errors.dart.

internal sealed class IssueType
{
    public readonly string Code;
    public readonly ValidationSeverity Severity;
    public readonly Func<IReadOnlyList<object?>, string>? Message;

    public IssueType(string code, Func<IReadOnlyList<object?>, string>? message, ValidationSeverity severity = ValidationSeverity.Error)
    {
        Code = code;
        Message = message;
        Severity = severity;
    }

    public override string ToString() => Code;
}

internal sealed class Issue
{
    public readonly IssueType Type;
    public readonly ValidationSeverity? SeverityOverride;
    public readonly string? Pointer;
    public readonly long? Offset;
    private readonly IReadOnlyList<object?> _args;

    private static readonly object?[] NoArgs = Array.Empty<object?>();

    public Issue(IssueType type, IReadOnlyList<object?>? args, string? pointer = null, long? offset = null, ValidationSeverity? severityOverride = null)
    {
        Type = type;
        _args = args ?? NoArgs;
        Pointer = pointer;
        Offset = offset;
        SeverityOverride = severityOverride;
    }

    public string Message => Type.Message != null ? Type.Message(_args).TrimEnd() : Type.Code;

    public ValidationSeverity Severity => SeverityOverride ?? Type.Severity;

    public ValidationIssue ToValidationIssue() => new(Type.Code, Message, Severity, Pointer, Offset);

    public override string ToString()
    {
        if (!string.IsNullOrEmpty(Pointer)) return Pointer + ": " + Message;
        if (Offset != null) return "@" + Offset + ": " + Message;
        return Message;
    }
}

internal sealed class IssuesLimitExceededException : Exception
{
}

internal sealed class Context
{
    public readonly bool Validate;
    public readonly ValidationOptions Options;
    public readonly List<string> Path = new();

    /// <summary>Dart <c>context.path.add(x)</c>.</summary>
    public void Push(string token) => Path.Add(token);

    /// <summary>Dart <c>context.path.add(i.toString())</c>.</summary>
    public void Push(int index) => Path.Add(index.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Dart <c>context.path.removeLast()</c>.</summary>
    public void Pop() => Path.RemoveAt(Path.Count - 1);

    private static readonly Regex ExtNameFormat = new("^([A-Z0-9]+)_[A-Za-z0-9_]+$", RegexOptions.CultureInvariant);

    public Context(ValidationOptions? options = null, bool validate = true)
    {
        Validate = validate;
        Options = options ?? new ValidationOptions();
    }

    public readonly Dictionary<Accessor, List<ElementChecker>> AccessorElementCheckers = new(ReferenceEqualityComparer.Instance);

    public void AddElementChecker(Accessor accessor, ElementChecker checker)
    {
        if (!AccessorElementCheckers.TryGetValue(accessor, out var list))
            AccessorElementCheckers[accessor] = list = new List<ElementChecker>();
        list.Add(checker);
    }

    public readonly Dictionary<object, object> Owners = new(ReferenceEqualityComparer.Instance);

    public readonly Dictionary<Type, List<LinkableExtensionEntry>> LinkableExtensions = new();

    public readonly List<ResourceValidatableExtensionEntry> ResourceValidatableExtensions = new();

    public readonly Dictionary<BufferView, HashSet<Accessor>> BufferViewAccessors = new(ReferenceEqualityComparer.Instance);

    /// <summary>Extension-provided root collections (lights, variants) with the path they live at, for the UNUSED_OBJECT sweep.</summary>
    public readonly List<KeyValuePair<IReadOnlyList<Usable?>, string[]>> ExtensionCollections = new();

    public void RegisterObjectsOwner(object owner, IEnumerable<object?> objects)
    {
        foreach (var o in objects)
            if (o != null) Owners[o] = owner;
    }

    public bool IsTruncated { get; private set; }

    private readonly Dictionary<ExtensionTuple, ExtensionDescriptor> _extensionDescriptors = new();
    public IReadOnlyDictionary<ExtensionTuple, ExtensionDescriptor> ExtensionDescriptors => _extensionDescriptors;

    private readonly List<string> _extensionsUsed = new();
    public IReadOnlyList<string> ExtensionsUsed => _extensionsUsed;

    private readonly List<string> _extensionsLoaded = new();
    public IReadOnlyList<string> ExtensionsLoaded => _extensionsLoaded;

    private readonly List<ValidationResource> _resources = new();
    public IReadOnlyList<ValidationResource> Resources => _resources;

    private readonly List<Extension> _userExtensions = new();

    private readonly List<Issue> _issues = new();
    public IReadOnlyList<Issue> Issues => _issues;

    private readonly StringBuilder _sb = new();

    public string GetPointerString(string? token = null)
    {
        if (Path.Count == 0 && token != null && token.StartsWith('/'))
            return token; // Special case: token is already a pointer string

        if (token != null) Path.Add(token);

        _sb.Append('/');
        for (int i = 0; i < Path.Count; i++)
        {
            if (i > 0) _sb.Append('/');
            _sb.Append(Path[i].Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal));
        }

        if (token != null) Path.RemoveAt(Path.Count - 1);

        var result = _sb.ToString();
        _sb.Clear();
        return result;
    }

    public void RegisterExtensions(IEnumerable<Extension> userExtensions) => _userExtensions.AddRange(userExtensions);

    public void InitExtensions(IReadOnlyList<string> extensionsUsed, IReadOnlyList<string> extensionsRequired)
    {
        _extensionsUsed.AddRange(extensionsUsed);

        for (int i = 0; i < extensionsUsed.Count; ++i)
        {
            var extensionName = extensionsUsed[i];

            if (!ExtNameFormat.IsMatch(extensionName))
                AddIssue(SemanticError.InvalidExtensionNameFormat, name: "/" + Members.EXTENSIONS_USED + "/" + i);

            var extension = _userExtensions.FirstOrDefault(e => e.Name == extensionName)
                            ?? Extensions.Default.FirstOrDefault(e => e.Name == extensionName);

            if (extension == null)
            {
                AddIssue(LinkError.UnsupportedExtension, name: "/" + Members.EXTENSIONS_USED + "/" + i, args: new object?[] { extensionName });
                continue;
            }

            if (extension.Functions != null)
                foreach (var (type, funcs) in extension.Functions)
                    _extensionDescriptors[new ExtensionTuple(type, extension.Name)] = funcs;

            extension.Init?.Invoke(this);

            if (Validate && extension.Required && !extensionsRequired.Contains(extensionName))
                AddIssue(SemanticError.NonRequiredExtension, name: "/" + Members.EXTENSIONS_USED + "/" + i, args: new object?[] { extensionName });

            _extensionsLoaded.Add(extensionName);
        }

        if (Validate)
        {
            for (int i = 0; i < extensionsRequired.Count; ++i)
            {
                var value = extensionsRequired[i];
                if (!extensionsUsed.Contains(value))
                    AddIssue(SemanticError.UnusedExtensionRequired, name: "/" + Members.EXTENSIONS_REQUIRED + "/" + i, args: new object?[] { value });
            }
        }
    }

    public void AddIssue(IssueType issueType, string? name = null, IReadOnlyList<object?>? args = null, long? offset = null, int? index = null, bool noPointer = false)
    {
        if (Options.IgnoredIssues.Contains(issueType.Code)) return;
        if (Options.OnlyIssues.Count > 0 && !Options.OnlyIssues.Contains(issueType.Code)) return;

        if (Options.MaxIssues > 0 && _issues.Count == Options.MaxIssues)
        {
            IsTruncated = true;
            throw new IssuesLimitExceededException();
        }

        ValidationSeverity? severityOverride = Options.SeverityOverrides.TryGetValue(issueType.Code, out var so) ? so : null;

        if (offset != null)
        {
            _issues.Add(new Issue(issueType, args, offset: offset, severityOverride: severityOverride));
        }
        else
        {
            var token = index != null ? index.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : name;
            _issues.Add(new Issue(issueType, args, pointer: noPointer ? "" : GetPointerString(token), severityOverride: severityOverride));
        }
    }

    public void AddResource(ValidationResource info) => _resources.Add(info);

    public bool IsGlb { get; private set; }

    public void SetGlb() => IsGlb = true;

    public readonly List<string> ImageMimeTypes = new() { Members.IMAGE_JPEG, Members.IMAGE_PNG };

    public readonly Dictionary<string, HashSet<AccessorFormat>> AttributeAccessorFormats = new(StringComparer.Ordinal)
    {
        [Members.POSITION] = new() { new AccessorFormat(Members.VEC3, Gl.FLOAT) },
        [Members.NORMAL] = new() { new AccessorFormat(Members.VEC3, Gl.FLOAT) },
        [Members.TANGENT] = new() { new AccessorFormat(Members.VEC4, Gl.FLOAT) },
        [Members.TEXCOORD_] = new()
        {
            new AccessorFormat(Members.VEC2, Gl.FLOAT),
            new AccessorFormat(Members.VEC2, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(Members.VEC2, Gl.UNSIGNED_SHORT, normalized: true),
        },
        [Members.COLOR_] = new()
        {
            new AccessorFormat(Members.VEC3, Gl.FLOAT),
            new AccessorFormat(Members.VEC3, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.UNSIGNED_SHORT, normalized: true),
            new AccessorFormat(Members.VEC4, Gl.FLOAT),
            new AccessorFormat(Members.VEC4, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(Members.VEC4, Gl.UNSIGNED_SHORT, normalized: true),
        },
        [Members.JOINTS_] = new()
        {
            new AccessorFormat(Members.VEC4, Gl.UNSIGNED_BYTE),
            new AccessorFormat(Members.VEC4, Gl.UNSIGNED_SHORT),
        },
        [Members.WEIGHTS_] = new()
        {
            new AccessorFormat(Members.VEC4, Gl.FLOAT),
            new AccessorFormat(Members.VEC4, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(Members.VEC4, Gl.UNSIGNED_SHORT, normalized: true),
        },
    };

    public readonly Dictionary<string, HashSet<AccessorFormat>> MorphAttributeAccessorFormats = new(StringComparer.Ordinal)
    {
        [Members.POSITION] = new() { new AccessorFormat(Members.VEC3, Gl.FLOAT) },
        [Members.NORMAL] = new() { new AccessorFormat(Members.VEC3, Gl.FLOAT) },
        [Members.TANGENT] = new() { new AccessorFormat(Members.VEC3, Gl.FLOAT) },
        [Members.TEXCOORD_] = new()
        {
            new AccessorFormat(Members.VEC2, Gl.FLOAT),
            new AccessorFormat(Members.VEC2, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC2, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(Members.VEC2, Gl.SHORT, normalized: true),
            new AccessorFormat(Members.VEC2, Gl.UNSIGNED_SHORT, normalized: true),
        },
        [Members.COLOR_] = new()
        {
            new AccessorFormat(Members.VEC3, Gl.FLOAT),
            new AccessorFormat(Members.VEC3, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.SHORT, normalized: true),
            new AccessorFormat(Members.VEC3, Gl.UNSIGNED_SHORT, normalized: true),
            new AccessorFormat(Members.VEC4, Gl.FLOAT),
            new AccessorFormat(Members.VEC4, Gl.BYTE, normalized: true),
            new AccessorFormat(Members.VEC4, Gl.UNSIGNED_BYTE, normalized: true),
            new AccessorFormat(Members.VEC4, Gl.SHORT, normalized: true),
            new AccessorFormat(Members.VEC4, Gl.UNSIGNED_SHORT, normalized: true),
        },
    };

    public readonly List<string> AnimationChannelTargetPaths = new(Members.ANIMATION_CHANNEL_TARGET_PATHS);
}
