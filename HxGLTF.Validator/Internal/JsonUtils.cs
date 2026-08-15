using System.Text.Json;
using System.Text.RegularExpressions;

namespace HxGLTF.Validator.Internal;

/// <summary>
/// Port of lib/src/utils.dart: schema-level getters that read a JSON member with type/range/enum checks and report
/// SchemaError issues exactly like the reference validator. Absent members are JsonValueKind.Undefined,
/// JSON null literals are JsonValueKind.Null (reported as TYPE_MISMATCH with value null).
/// </summary>
internal static class JsonUtils
{
    private const string KArray = "array";
    private const string KBoolean = "boolean";
    private const string KInteger = "integer";
    private const string KNumber = "number";
    private const string KObject = "object";
    private const string KString = "string";

    /// <summary>The Dart value of a JSON number: long when the literal is an integer, double otherwise.</summary>
    public static object NumberValue(JsonElement el)
    {
        if (el.TryGetInt64(out var l)) return l;
        return el.GetDouble();
    }

    /// <summary>Dart runtime value of an element (for message args): long/double/string/bool/null, JsonElement for arrays and objects.</summary>
    public static object? Value(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Undefined => null,
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => NumberValue(el),
        _ => el,
    };

    /// <summary>Dart _tryFixInt: whole doubles become ints. Returns long, double, or the raw value.</summary>
    public static object? TryFixInt(object? value)
    {
        if (value is double d && !double.IsInfinity(d) && !double.IsNaN(d) && Math.Floor(d) == d && Math.Abs(d) < 9.2e18)
            return (long)d;
        return value;
    }

    private static object? GetGuarded(JsonElement map, string name, string type, Context context)
    {
        if (map.ValueKind != JsonValueKind.Object || !map.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Null)
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new object?[] { null, type });
            return null;
        }
        return Value(value);
    }

    public static bool Has(JsonElement map, string name)
        => map.ValueKind == JsonValueKind.Object && map.TryGetProperty(name, out _);

    public static int GetIndex(JsonElement map, string name, Context context, bool req = true)
    {
        var value = TryFixInt(GetGuarded(map, name, KInteger, context));
        if (value is long l)
        {
            if (l >= 0) return (int)Math.Min(l, int.MaxValue);
            context.AddIssue(SchemaError.InvalidIndex, name: name);
        }
        else if (value == null)
        {
            if (req) context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
        }
        else
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KInteger });
        }
        return -1;
    }

    public static bool GetBool(JsonElement map, string name, Context context, bool req = false, bool def = false)
    {
        var value = GetGuarded(map, name, KBoolean, context);
        if (value == null)
        {
            if (!req) return def;
            context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
            return def;
        }
        if (value is bool b) return b;
        context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KBoolean });
        return def;
    }

    public static int GetUint(JsonElement map, string name, Context context, bool req = false, int min = 0, int max = -1, int def = -1, IEnumerable<int>? list = null)
    {
        var value = TryFixInt(GetGuarded(map, name, KInteger, context));
        if (value is long l)
        {
            if (list != null)
            {
                if (!CheckEnum(name, l, list.Select(i => (long)i), context)) return -1;
            }
            else if (l < min || (max != -1 && l > max))
            {
                context.AddIssue(SchemaError.ValueNotInRange, name: name, args: new object?[] { l });
                return -1;
            }
            return (int)Math.Min(l, int.MaxValue);
        }
        if (value == null)
        {
            if (!req) return def;
            context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
        }
        else
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KInteger });
        }
        return -1;
    }

    public static double GetFloat(JsonElement map, string name, Context context, bool req = false, double standalone = double.NaN,
        double min = double.NegativeInfinity, double exclMin = double.NegativeInfinity, double max = double.PositiveInfinity,
        double exclMax = double.PositiveInfinity, double def = double.NaN)
    {
        var value = GetGuarded(map, name, KNumber, context);
        if (value is long or double)
        {
            double v = value is long l ? l : (double)value;
            if (v != standalone && (v < min || v <= exclMin || v > max || v >= exclMax))
            {
                context.AddIssue(SchemaError.ValueNotInRange, name: name, args: new[] { value });
                return double.NaN;
            }
            return v;
        }
        if (value == null)
        {
            if (!req) return def;
            context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
        }
        else
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KNumber });
        }
        return double.NaN;
    }

    public static string? GetString(JsonElement map, string name, Context context, bool req = false, IEnumerable<string>? list = null, string? def = null, Regex? regexp = null)
    {
        var value = GetGuarded(map, name, KString, context);
        if (value is string s)
        {
            if (list != null)
            {
                CheckEnum(name, s, list, context);
            }
            else if (regexp != null && !regexp.IsMatch(s))
            {
                context.AddIssue(SchemaError.PatternMismatch, name: name, args: new object?[] { s, regexp.ToString() });
                return null;
            }
            return s;
        }
        if (value == null)
        {
            if (!req) return def;
            context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
        }
        else
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KString });
        }
        return null;
    }

    public static GltfUri? GetUri(string uriString, Context context)
    {
        if (GltfUri.TryParse(uriString, out var uri, out var error))
        {
            if (uri!.IsNonRelative)
                context.AddIssue(SemanticError.NonRelativeUri, name: Members.URI, args: new object?[] { uriString });
            return uri;
        }
        context.AddIssue(SchemaError.InvalidUri, name: Members.URI, args: new object?[] { uriString, error });
        return null;
    }

    /// <summary>Returns the object member, an empty object when absent (unless req), or default(JsonElement) (Undefined) on failure.</summary>
    public static JsonElement GetMap(JsonElement map, string name, Context context, bool req = false)
    {
        if (map.ValueKind == JsonValueKind.Object && map.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.Object) return value;
            if (value.ValueKind == JsonValueKind.Null)
            {
                context.AddIssue(SchemaError.TypeMismatch, name: name, args: new object?[] { null, KObject });
                if (req) return default;
                return EmptyObject;
            }
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { Value(value), KObject });
            if (req) return default;
            return EmptyObject;
        }
        if (req)
        {
            context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
            return default;
        }
        return EmptyObject;
    }

    /// <summary>An empty JSON object element (Dart returns {} for absent optional maps).</summary>
    public static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement;

    public static bool IsUndefined(JsonElement el) => el.ValueKind == JsonValueKind.Undefined;

    public static T? GetObjectFromInnerMap<T>(JsonElement map, string name, Context context, FromMapFunction<T> fromMap, bool req = false) where T : class
    {
        if (map.ValueKind == JsonValueKind.Object && map.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                context.Path.Add(name);
                var obj = fromMap(value, context);
                context.Path.RemoveAt(context.Path.Count - 1);
                return obj;
            }
            if (value.ValueKind == JsonValueKind.Null)
            {
                context.AddIssue(SchemaError.TypeMismatch, name: name, args: new object?[] { null, KObject });
                return null;
            }
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { Value(value), KObject });
            return null;
        }
        if (req) context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
        return null;
    }

    public static int[]? GetIndicesList(JsonElement map, string name, Context context, bool req = false)
    {
        var value = GetGuarded(map, name, KArray, context);
        if (value is JsonElement { ValueKind: JsonValueKind.Array } arr)
        {
            int len = arr.GetArrayLength();
            if (len == 0)
            {
                context.AddIssue(SchemaError.EmptyEntity, name: name);
                return null;
            }
            var result = new int[len];
            int i = 0;
            if (context.Validate)
            {
                context.Path.Add(name);
                var unique = new HashSet<long>();
                foreach (var e in arr.EnumerateArray())
                {
                    var v = TryFixInt(Value(e));
                    if (v is long l && l >= 0)
                    {
                        if (!unique.Add(l)) context.AddIssue(SchemaError.ArrayDuplicateElements, index: i);
                        result[i] = (int)Math.Min(l, int.MaxValue);
                    }
                    else
                    {
                        result[i] = -1;
                        context.AddIssue(SchemaError.InvalidIndex, index: i);
                    }
                    i++;
                }
                context.Path.RemoveAt(context.Path.Count - 1);
            }
            else
            {
                foreach (var e in arr.EnumerateArray())
                {
                    var v = TryFixInt(Value(e));
                    result[i++] = v is long l && l >= 0 ? (int)Math.Min(l, int.MaxValue) : -1;
                }
            }
            return result;
        }
        if (value == null)
        {
            if (req) context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
        }
        else
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KArray });
        }
        return null;
    }

    /// <summary>Ordered map (JSON member order) of key to index; invalid entries are -1.</summary>
    public static List<KeyValuePair<string, int>>? GetIndicesMap(JsonElement map, string name, Context context, Action<string> checkKey)
    {
        var value = GetGuarded(map, name, KObject, context);
        if (value is JsonElement { ValueKind: JsonValueKind.Object } obj)
        {
            var result = new List<KeyValuePair<string, int>>();
            foreach (var p in obj.EnumerateObject()) result.Add(new(p.Name, -1));
            if (result.Count == 0)
            {
                context.AddIssue(SchemaError.EmptyEntity, name: name);
                return null;
            }
            context.Path.Add(name);
            int i = 0;
            foreach (var p in obj.EnumerateObject())
            {
                checkKey(p.Name);
                var v = TryFixInt(Value(p.Value));
                if (v is long l && l >= 0)
                {
                    result[i] = new(p.Name, (int)Math.Min(l, int.MaxValue));
                }
                else
                {
                    result[i] = new(p.Name, -1);
                    context.AddIssue(SchemaError.InvalidIndex, name: p.Name);
                }
                i++;
            }
            context.Path.RemoveAt(context.Path.Count - 1);
            return result;
        }
        if (value == null)
        {
            context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
        }
        else
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KObject });
        }
        return null;
    }

    public static List<List<KeyValuePair<string, int>>>? GetIndicesMapsList(JsonElement map, string name, Context context, Action<string> checkKey)
    {
        var list = GetGuarded(map, name, KArray, context);
        if (list is JsonElement { ValueKind: JsonValueKind.Array } arr)
        {
            var result = new List<List<KeyValuePair<string, int>>>();
            if (context.Validate)
            {
                if (arr.GetArrayLength() == 0)
                {
                    context.AddIssue(SchemaError.EmptyEntity, name: name);
                    return null;
                }
                var invalidElementFound = false;
                context.Path.Add(name);
                int i = 0;
                foreach (var innerMap in arr.EnumerateArray())
                {
                    if (innerMap.ValueKind == JsonValueKind.Object)
                    {
                        var entries = new List<KeyValuePair<string, int>>();
                        if (!innerMap.EnumerateObject().Any())
                        {
                            context.AddIssue(SchemaError.EmptyEntity, index: i);
                            invalidElementFound = true;
                        }
                        else
                        {
                            context.Path.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            foreach (var p in innerMap.EnumerateObject())
                            {
                                checkKey(p.Name);
                                var v = TryFixInt(Value(p.Value));
                                if (v is long l && l >= 0)
                                {
                                    entries.Add(new(p.Name, (int)Math.Min(l, int.MaxValue)));
                                }
                                else
                                {
                                    entries.Add(new(p.Name, -1));
                                    context.AddIssue(SchemaError.InvalidIndex, name: p.Name);
                                }
                            }
                            context.Path.RemoveAt(context.Path.Count - 1);
                        }
                        result.Add(entries);
                    }
                    else
                    {
                        context.AddIssue(SchemaError.ArrayTypeMismatch, args: new[] { Value(innerMap), KObject });
                        invalidElementFound = true;
                    }
                    i++;
                }
                context.Path.RemoveAt(context.Path.Count - 1);
                if (invalidElementFound) return null;
            }
            else
            {
                foreach (var innerMap in arr.EnumerateArray())
                {
                    var entries = new List<KeyValuePair<string, int>>();
                    if (innerMap.ValueKind == JsonValueKind.Object)
                        foreach (var p in innerMap.EnumerateObject())
                        {
                            var v = TryFixInt(Value(p.Value));
                            entries.Add(new(p.Name, v is long l && l >= 0 ? (int)Math.Min(l, int.MaxValue) : -1));
                        }
                    result.Add(entries);
                }
            }
            return result;
        }
        if (list != null)
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { list, KArray });
        }
        return null;
    }

    public static double[]? GetFloatList(JsonElement map, string name, Context context, bool req = false, bool singlePrecision = false,
        double min = double.NegativeInfinity, double max = double.PositiveInfinity, double[]? def = null, IEnumerable<int>? lengthsList = null)
    {
        var value = GetGuarded(map, name, KArray, context);
        if (value is JsonElement { ValueKind: JsonValueKind.Array } arr)
        {
            int len = arr.GetArrayLength();
            if (len == 0)
            {
                context.AddIssue(SchemaError.EmptyEntity, name: name);
                return null;
            }
            if (lengthsList != null && !CheckEnum(name, (long)len, lengthsList.Select(i => (long)i), context, lengthList: true))
                return null;

            var wrongMemberFound = false;
            var result = new double[len];
            int i = 0;
            foreach (var e in arr.EnumerateArray())
            {
                var v = Value(e);
                if (v is long or double)
                {
                    double d = v is long l ? l : (double)v;
                    if (context.Validate && (double.IsInfinity(d) || d < min || d > max))
                    {
                        context.Path.Add(name);
                        context.AddIssue(SchemaError.ValueNotInRange, index: i, args: new[] { v });
                        context.Path.RemoveAt(context.Path.Count - 1);
                        wrongMemberFound = true;
                    }
                    result[i] = singlePrecision ? (float)d : d;
                }
                else
                {
                    context.AddIssue(SchemaError.ArrayTypeMismatch, name: name, args: new[] { v, KNumber });
                    wrongMemberFound = true;
                }
                i++;
            }
            if (wrongMemberFound) return null;
            return result;
        }
        if (value == null)
        {
            if (!req) return def == null ? null : (double[])def.Clone();
            context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
        }
        else
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KArray });
        }
        return null;
    }

    /// <summary>Integer list stored in a GL component type (accessor min/max). Returns null when invalid.</summary>
    public static long[]? GetGlIntList(JsonElement map, string name, Context context, int type, int length)
    {
        var value = GetGuarded(map, name, KArray, context);
        if (value is JsonElement { ValueKind: JsonValueKind.Array } arr)
        {
            int len = arr.GetArrayLength();
            if (len != length)
            {
                context.AddIssue(SchemaError.ArrayLengthNotInList, name: name, args: new object?[] { (long)len, new List<object?> { (long)length } });
                return null;
            }
            long min = Gl.TypeMin(type);
            long max = Gl.TypeMax(type);
            var result = new long[length];
            var wrongMemberFound = false;
            int i = 0;
            foreach (var e in arr.EnumerateArray())
            {
                var v = TryFixInt(Value(e));
                if (v is long l)
                {
                    if (context.Validate && (l < min || l > max))
                    {
                        context.AddIssue(SemanticError.InvalidGlValue, name: name, args: new object?[] { l, Gl.TypeName(type) });
                        wrongMemberFound = true;
                    }
                    result[i] = l;
                }
                else
                {
                    context.AddIssue(SchemaError.ArrayTypeMismatch, name: name, args: new[] { v, KInteger });
                    wrongMemberFound = true;
                }
                i++;
            }
            if (wrongMemberFound) return null;
            return result;
        }
        if (value != null)
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KArray });
        }
        return null;
    }

    public static string[]? GetStringList(JsonElement map, string name, Context context)
    {
        var value = GetGuarded(map, name, KArray, context);
        if (value is JsonElement { ValueKind: JsonValueKind.Array } arr)
        {
            int len = arr.GetArrayLength();
            if (len == 0)
            {
                context.AddIssue(SchemaError.EmptyEntity, name: name);
                return null;
            }
            var result = new string[len];
            int i = 0;
            if (context.Validate)
            {
                var wrongMemberFound = false;
                context.Path.Add(name);
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in arr.EnumerateArray())
                {
                    if (e.ValueKind == JsonValueKind.String)
                    {
                        var s = e.GetString()!;
                        if (!unique.Add(s)) context.AddIssue(SchemaError.ArrayDuplicateElements, index: i);
                        result[i] = s;
                    }
                    else
                    {
                        context.AddIssue(SchemaError.ArrayTypeMismatch, index: i, args: new[] { Value(e), KString });
                        wrongMemberFound = true;
                    }
                    i++;
                }
                context.Path.RemoveAt(context.Path.Count - 1);
                if (wrongMemberFound) return null;
            }
            else
            {
                foreach (var e in arr.EnumerateArray()) result[i++] = e.ValueKind == JsonValueKind.String ? e.GetString()! : "";
            }
            return result;
        }
        if (value != null)
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KArray });
        }
        return null;
    }

    public static List<JsonElement>? GetMapList(JsonElement map, string name, Context context)
    {
        var value = GetGuarded(map, name, KArray, context);
        if (value is JsonElement { ValueKind: JsonValueKind.Array } arr)
        {
            if (arr.GetArrayLength() == 0)
            {
                context.AddIssue(SchemaError.EmptyEntity, name: name);
                return null;
            }
            var invalidElementFound = false;
            var result = new List<JsonElement>(arr.GetArrayLength());
            foreach (var v in arr.EnumerateArray())
            {
                if (v.ValueKind != JsonValueKind.Object)
                {
                    context.AddIssue(SchemaError.ArrayTypeMismatch, name: name, args: new[] { Value(v), KObject });
                    invalidElementFound = true;
                }
                result.Add(v);
            }
            if (invalidElementFound) return null;
            return result;
        }
        if (value == null)
        {
            context.AddIssue(SchemaError.UndefinedProperty, args: new object?[] { name });
        }
        else
        {
            context.AddIssue(SchemaError.TypeMismatch, name: name, args: new[] { value, KArray });
        }
        return null;
    }

    public static string? GetName(JsonElement map, Context context, bool req = false)
        => GetString(map, Members.NAME, context, req: req);

    public static Dictionary<string, object?> GetExtensions(JsonElement map, Type type, Context context, Type? overriddenType = null)
    {
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal);
        var extensionMaps = GetMap(map, Members.EXTENSIONS, context);
        if (IsUndefined(extensionMaps) || !extensionMaps.EnumerateObject().Any())
            return extensions;

        context.Path.Add(Members.EXTENSIONS);
        int count = extensionMaps.EnumerateObject().Count();
        foreach (var prop in extensionMaps.EnumerateObject())
        {
            var extension = prop.Name;
            // Fetch extension JSON map first to ensure schema compliance.
            // (Dart getMap never returns null for optional members: wrong types become an empty map.)
            var extensionMap = GetMap(extensionMaps, extension, context);

            if (!context.ExtensionsLoaded.Contains(extension))
            {
                if (context.Validate && !context.ExtensionsUsed.Contains(extension))
                    context.AddIssue(LinkError.UndeclaredExtension, name: extension);
                extensions[extension] = extensionMap;
                continue;
            }

            if (!context.ExtensionDescriptors.TryGetValue(new ExtensionTuple(type, extension), out var descriptor))
            {
                context.AddIssue(LinkError.UnexpectedExtensionObject, name: extension);
                continue;
            }

            if (count > 1 && descriptor.Standalone)
                context.AddIssue(SemanticError.MultipleExtensions, name: extension);

            {
                context.Path.Add(extension);
                var obj = descriptor.FromMap(extensionMap, context);
                extensions[extension] = obj;
                if (!descriptor.LocalLink && obj is ILinkable linkable)
                {
                    var key = overriddenType ?? type;
                    if (!context.LinkableExtensions.TryGetValue(key, out var list))
                        context.LinkableExtensions[key] = list = new List<LinkableExtensionEntry>();
                    list.Add(new LinkableExtensionEntry(linkable, context.Path.ToArray()));
                }
                if (obj is IResourceValidatable rv)
                    context.ResourceValidatableExtensions.Add(new ResourceValidatableExtensionEntry(rv, context.Path.ToArray()));
                context.Path.RemoveAt(context.Path.Count - 1);
            }
        }
        context.Path.RemoveAt(context.Path.Count - 1);
        return extensions;
    }

    public static object? GetExtras(JsonElement map, Context context)
    {
        if (map.ValueKind != JsonValueKind.Object || !map.TryGetProperty(Members.EXTRAS, out var extras)) return null;
        if (context.Validate && extras.ValueKind != JsonValueKind.Null && extras.ValueKind != JsonValueKind.Object)
            context.AddIssue(SemanticError.NonObjectExtras, name: Members.EXTRAS);
        return extras.ValueKind == JsonValueKind.Null ? null : extras;
    }

    public static bool CheckEnum<T>(string name, T value, IEnumerable<T> list, Context context, bool lengthList = false)
    {
        var items = list as IList<T> ?? list.ToList();
        if (!items.Contains(value))
        {
            context.AddIssue(lengthList ? SchemaError.ArrayLengthNotInList : SchemaError.ValueNotInList,
                name: name, args: new object?[] { value, DartFormat.Iter(items.Cast<object?>()) });
            return false;
        }
        return true;
    }

    public static void CheckMembers(JsonElement map, IReadOnlyList<string> knownMembers, Context context, bool useSuper = true)
    {
        if (map.ValueKind != JsonValueKind.Object) return;
        foreach (var p in map.EnumerateObject())
        {
            var k = p.Name;
            if (knownMembers.Contains(k)) continue;
            if (useSuper && (k == Members.EXTENSIONS || k == Members.EXTRAS)) continue;
            context.AddIssue(SchemaError.UnexpectedProperty, name: k);
        }
    }

    public static void ResolveNodeList(int[] sourceList, Node?[] targetList, SafeList<Node> nodes, string name, Context context,
        Action<Node, int, int>? handleNode = null)
    {
        context.Path.Add(name);
        for (int i = 0; i < sourceList.Length; i++)
        {
            var nodeIndex = sourceList[i];
            if (nodeIndex == -1) continue;
            var node = nodes[nodeIndex];
            if (node != null)
            {
                node.MarkAsUsed();
                targetList[i] = node;
                handleNode?.Invoke(node, nodeIndex, i);
            }
            else
            {
                context.AddIssue(LinkError.UnresolvedReference, index: i, args: new object?[] { (long)nodeIndex });
            }
        }
        context.Path.RemoveAt(context.Path.Count - 1);
    }

    public static bool IsPot(long value) => value != 0 && (value & (value - 1)) == 0;

    public static long PadLength(long length) => length + (-length & 3);

    /// <summary>Dart doubleToSingle.</summary>
    public static double DoubleToSingle(double value) => (float)value;
}
