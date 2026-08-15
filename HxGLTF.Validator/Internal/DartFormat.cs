using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

/// <summary>
/// Reproduces Dart's <c>toString()</c> conventions so that message texts are byte-identical to the reference validator:
/// ints without decimals, doubles as <c>1.0</c>, <c>0.5</c>, <c>1e+21</c>, lists as <c>[a, b]</c>, iterables as <c>(a, b)</c>,
/// maps as <c>{a: 1}</c>, null as <c>null</c>.
/// </summary>
internal static class DartFormat
{
    /// <summary>Marks a value that Dart would print as an Iterable: <c>(a, b)</c>.</summary>
    public sealed class Iterable
    {
        public readonly IEnumerable<object?> Items;
        public Iterable(IEnumerable<object?> items) => Items = items;
    }

    public static Iterable Iter(IEnumerable<object?> items) => new(items);
    public static Iterable Iter<T>(IEnumerable<T> items) => new(items.Cast<object?>());

    /// <summary>Dart <c>toString()</c> of a value.</summary>
    public static string V(object? value)
    {
        switch (value)
        {
            case null: return "null";
            case string s: return s;
            case bool b: return b ? "true" : "false";
            case int i: return i.ToString(CultureInfo.InvariantCulture);
            case long l: return l.ToString(CultureInfo.InvariantCulture);
            case uint u: return u.ToString(CultureInfo.InvariantCulture);
            case ulong ul: return ul.ToString(CultureInfo.InvariantCulture);
            case short sh: return sh.ToString(CultureInfo.InvariantCulture);
            case ushort ush: return ush.ToString(CultureInfo.InvariantCulture);
            case byte by: return by.ToString(CultureInfo.InvariantCulture);
            case sbyte sb: return sb.ToString(CultureInfo.InvariantCulture);
            case float f: return Double(f);
            case double d: return Double(d);
            case Iterable it: return "(" + string.Join(", ", it.Items.Select(V)) + ")";
            case JsonElement el: return Json(el);
            case System.Collections.IDictionary dict:
            {
                var parts = new List<string>();
                foreach (System.Collections.DictionaryEntry e in dict) parts.Add(V(e.Key) + ": " + V(e.Value));
                return "{" + string.Join(", ", parts) + "}";
            }
            case System.Collections.IEnumerable list: return "[" + string.Join(", ", list.Cast<object?>().Select(V)) + "]";
            default: return value.ToString() ?? "null";
        }
    }

    /// <summary>Dart <c>"'$o'"</c>.</summary>
    public static string Q(object? o) => "'" + V(o) + "'";

    /// <summary>Dart <c>_mbq</c>: quote strings only.</summary>
    public static string Mbq(object? o) => o is string s ? "'" + s + "'" : V(o);

    /// <summary>Dart <c>iterable.map(_mbq)</c> printed: <c>('a', 2)</c>.</summary>
    public static string MbqIter(object? o)
    {
        var items = o switch
        {
            Iterable it => it.Items,
            string s => new object?[] { s },
            System.Collections.IEnumerable e => e.Cast<object?>(),
            _ => new[] { o },
        };
        return "(" + string.Join(", ", items.Select(Mbq)) + ")";
    }

    /// <summary>Dart double.toString().</summary>
    public static string Double(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsPositiveInfinity(d)) return "Infinity";
        if (double.IsNegativeInfinity(d)) return "-Infinity";
        if (d == 0) return 1 / d < 0 ? "-0.0" : "0.0";

        // Shortest round-trip digits and decimal exponent, then Dart/JS layout rules.
        var shortest = d.ToString("R", CultureInfo.InvariantCulture);
        ExtractDigits(shortest, out var negative, out var digits, out var exponent); // value = 0.digits * 10^exponent

        var sb = new StringBuilder();
        if (negative) sb.Append('-');
        int n = exponent; // number of digits before the decimal point (JS "n")
        int k = digits.Length;
        if (k <= n && n <= 21)
        {
            sb.Append(digits).Append('0', n - k).Append(".0");
        }
        else if (0 < n && n <= 21)
        {
            sb.Append(digits, 0, n).Append('.').Append(digits, n, k - n);
        }
        else if (-6 < n && n <= 0)
        {
            sb.Append("0.").Append('0', -n).Append(digits);
        }
        else
        {
            int e = n - 1;
            sb.Append(digits[0]);
            if (k > 1) sb.Append('.').Append(digits, 1, k - 1);
            sb.Append('e').Append(e < 0 ? '-' : '+').Append(Math.Abs(e).ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static void ExtractDigits(string roundTrip, out bool negative, out string digits, out int exponent)
    {
        negative = false;
        var s = roundTrip;
        if (s.StartsWith('-')) { negative = true; s = s[1..]; }
        int exp10 = 0;
        int ePos = s.IndexOf('E');
        if (ePos < 0) ePos = s.IndexOf('e');
        if (ePos >= 0)
        {
            exp10 = int.Parse(s[(ePos + 1)..], CultureInfo.InvariantCulture);
            s = s[..ePos];
        }
        int dot = s.IndexOf('.');
        string intPart = dot >= 0 ? s[..dot] : s;
        string fracPart = dot >= 0 ? s[(dot + 1)..] : "";
        var all = (intPart + fracPart).TrimStart('0');
        int leadingZerosRemoved = (intPart + fracPart).Length - all.Length;
        // position of decimal point relative to start of (intPart+fracPart) is intPart.Length; after trimming leading zeros:
        int pointPos = intPart.Length - leadingZerosRemoved + exp10;
        all = all.TrimEnd('0');
        if (all.Length == 0) { all = "0"; pointPos = 1; }
        digits = all;
        exponent = pointPos;
    }

    /// <summary>Dart toString of decoded JSON values (maps as {k: v}, lists as [a, b], numbers per Dart).</summary>
    public static string Json(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Null: return "null";
            case JsonValueKind.True: return "true";
            case JsonValueKind.False: return "false";
            case JsonValueKind.String: return el.GetString()!;
            case JsonValueKind.Number: return V(JsonUtils.NumberValue(el));
            case JsonValueKind.Array: return "[" + string.Join(", ", el.EnumerateArray().Select(Json)) + "]";
            case JsonValueKind.Object: return "{" + string.Join(", ", el.EnumerateObject().Select(p => p.Name + ": " + Json(p.Value))) + "}";
            default: return "null";
        }
    }

    /// <summary>Dart <c>0x%08x</c> style used for GLB chunk types.</summary>
    public static string Hex8(uint v) => "0x" + v.ToString("x8", CultureInfo.InvariantCulture);
}
