using System.Text.Json;

namespace HxGLTF.Validator.Internal;

// Port of lib/src/base/gltf_property.dart and lib/src/ext/extensions.dart (descriptor types).

internal delegate T FromMapFunction<out T>(JsonElement map, Context context);

internal interface ILinkable
{
    void Link(Gltf gltf, Context context);
}

internal interface IResourceValidatable
{
    void ValidateResources(Gltf gltf, Context context);
}

/// <summary>Dart mixin Usable: tracks whether a root object is referenced by anything.</summary>
internal abstract class Usable
{
    public bool IsUsed { get; private set; }

    public void MarkAsUsed() => IsUsed = true;
}

internal abstract class GltfProperty : Usable, ILinkable
{
    /// <summary>Parsed extension objects (typed objects for known extensions, raw JsonElement for unknown ones).</summary>
    public readonly Dictionary<string, object?> Extensions;

    /// <summary>Raw extras (JsonElement) or null.</summary>
    public readonly object? Extras;

    protected GltfProperty(Dictionary<string, object?> extensions, object? extras)
    {
        Extensions = extensions;
        Extras = extras;
    }

    public virtual void Link(Gltf gltf, Context context) { }
}

internal abstract class GltfChildOfRootProperty : GltfProperty
{
    public readonly string? Name;

    protected GltfChildOfRootProperty(string? name, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Name = name;
    }
}

internal interface IGltfResource
{
    string? UriString { get; }
    GltfUri? Uri { get; }
}

/// <summary>Per-type entry of an extension: how to parse the extension object found on that glTF type.</summary>
internal sealed class ExtensionDescriptor
{
    public readonly FromMapFunction<object?> FromMap;
    public readonly bool Standalone;
    public readonly bool LocalLink;

    public ExtensionDescriptor(FromMapFunction<object?> fromMap, bool standalone = false, bool localLink = false)
    {
        FromMap = fromMap;
        Standalone = standalone;
        LocalLink = localLink;
    }
}

/// <summary>An extension known to the validator: name, per-type descriptors, optional init and required flag.</summary>
internal sealed class Extension
{
    public readonly string Name;
    public readonly IReadOnlyDictionary<Type, ExtensionDescriptor>? Functions;
    public readonly Action<Context>? Init;
    public readonly bool Required;

    public Extension(string name, IReadOnlyDictionary<Type, ExtensionDescriptor>? functions, Action<Context>? init = null, bool required = false)
    {
        Name = name;
        Functions = functions;
        Init = init;
        Required = required;
    }
}

internal readonly record struct ExtensionTuple(Type Type, string Name);

internal sealed class LinkableExtensionEntry
{
    public readonly ILinkable Object;
    public readonly string[] Path;

    public LinkableExtensionEntry(ILinkable obj, string[] path)
    {
        Object = obj;
        Path = path;
    }
}

internal sealed class ResourceValidatableExtensionEntry
{
    public readonly IResourceValidatable Object;
    public readonly string[] Path;

    public ResourceValidatableExtensionEntry(IResourceValidatable obj, string[] path)
    {
        Object = obj;
        Path = path;
    }
}

/// <summary>Dart SafeList: fixed length, out-of-range and broken entries read as null.</summary>
internal sealed class SafeList<T> : IReadOnlyList<T?> where T : class
{
    private readonly T?[] _list;
    public readonly string Name;

    public SafeList(int length, string name)
    {
        _list = length == 0 ? Array.Empty<T?>() : new T?[length];
        Name = name;
    }

    public static SafeList<T> Empty(string name) => new(0, name);

    public T? this[int index]
    {
        get => index < 0 || index >= _list.Length ? null : _list[index];
        set => _list[index] = value;
    }

    public int Count => _list.Length;
    public int Length => _list.Length;
    public bool IsEmpty => _list.Length == 0;
    public bool IsNotEmpty => _list.Length > 0;

    /// <summary>Iterate non-null (non-broken) entries with their index.</summary>
    public void ForEachWithIndices(Action<int, T> action)
    {
        for (int i = 0; i < _list.Length; i++)
        {
            var e = _list[i];
            if (e == null) continue;
            action(i, e);
        }
    }

    /// <summary>All non-null entries.</summary>
    public IEnumerable<T> NonNull()
    {
        foreach (var e in _list) if (e != null) yield return e;
    }

    public bool Any(Func<T, bool> predicate)
    {
        foreach (var e in _list) if (e != null && predicate(e)) return true;
        return false;
    }

    public IEnumerator<T?> GetEnumerator() => ((IEnumerable<T?>)_list).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
