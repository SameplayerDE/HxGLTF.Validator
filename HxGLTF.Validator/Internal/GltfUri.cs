using System.Text;

namespace HxGLTF.Validator.Internal;

/// <summary>Reproduces Dart's <c>FormatException.toString()</c> layout (message, position, source line and caret).</summary>
internal static class DartFormatException
{
    public static string Format(string message, string? source, int? offset)
    {
        var report = "FormatException";
        if (!string.IsNullOrEmpty(message)) report = report + ": " + message;
        if (source == null) return report;

        if (offset != null && (offset < 0 || offset > source.Length)) offset = null;
        if (offset == null)
        {
            if (source.Length > 78) source = source[..75] + "...";
            return report + "\n" + source;
        }

        int off = offset.Value;
        int lineNum = 1, lineStart = 0;
        bool previousCharWasCR = false;
        for (int i = 0; i < off; i++)
        {
            char c = source[i];
            if (c == '\n')
            {
                if (lineStart != i || !previousCharWasCR) lineNum++;
                lineStart = i + 1;
                previousCharWasCR = false;
            }
            else if (c == '\r')
            {
                lineNum++;
                lineStart = i + 1;
                previousCharWasCR = true;
            }
        }
        if (lineNum > 1)
            report += " (at line " + lineNum + ", character " + (off - lineStart + 1) + ")";
        else
            report += " (at character " + (off + 1) + ")";

        int lineEnd = source.Length;
        for (int i = off; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '\n' || c == '\r') { lineEnd = i; break; }
        }
        int length = lineEnd - lineStart;
        int start = lineStart, end = lineEnd;
        string prefix = "", postfix = "";
        if (length > 78)
        {
            int index = off - lineStart;
            if (index < 75) { end = start + 75; postfix = "..."; }
            else if (end - off < 75) { start = end - 75; prefix = "..."; }
            else { start = off - 36; end = off + 36; prefix = postfix = "..."; }
        }
        var slice = source[start..end];
        int markOffset = off - start + prefix.Length;
        return report + "\n" + prefix + slice + postfix + "\n" + new string(' ', markOffset) + "^";
    }
}

/// <summary>Thrown by the Dart-compatible URI/data-URI parsers; <see cref="Message"/> is the Dart-formatted text.</summary>
internal sealed class DartUriFormatException : Exception
{
    public DartUriFormatException(string message) : base(message) { }
}

/// <summary>Minimal port of Dart's UriData (RFC 2397 data URIs) with identical error messages.</summary>
internal sealed class DartUriData
{
    public readonly string Text;
    private readonly List<int> _separatorIndices;
    public readonly bool IsBase64;

    private DartUriData(string text, List<int> indices)
    {
        Text = text;
        _separatorIndices = indices;
        IsBase64 = indices.Count % 2 == 1;
    }

    /// <summary>Dart UriData.parse: throws <see cref="DartUriFormatException"/> when the text is not a data URI or is malformed.</summary>
    public static DartUriData Parse(string uri)
    {
        if (uri.Length >= 5 && string.Equals(uri[..5], "data:", StringComparison.OrdinalIgnoreCase))
            return ParseInternal(uri, 5);
        throw new DartUriFormatException(DartFormatException.Format("Expected a data URI", uri, 0));
    }

    /// <summary>Dart _parse(text, start).</summary>
    public static DartUriData ParseInternal(string text, int start)
    {
        const int comma = 0x2c, slash = 0x2f, semicolon = 0x3b, equals = 0x3d;
        var indices = new List<int> { start - 1 };
        int slashIndex = -1;
        int ch = -1;
        int i = start;
        for (; i < text.Length; i++)
        {
            ch = text[i];
            if (ch == comma || ch == semicolon) break;
            if (ch == slash)
            {
                if (slashIndex < 0) { slashIndex = i; continue; }
                throw new DartUriFormatException(DartFormatException.Format("Invalid MIME type", text, i));
            }
        }
        if (i >= text.Length) ch = -1;
        if (slashIndex < 0 && i > start)
            throw new DartUriFormatException(DartFormatException.Format("Invalid MIME type", text, i));

        while (ch != comma)
        {
            indices.Add(i);
            i++;
            int equalsIndex = -1;
            for (; i < text.Length; i++)
            {
                ch = text[i];
                if (ch == equals) { if (equalsIndex < 0) equalsIndex = i; }
                else if (ch == semicolon || ch == comma) break;
            }
            if (i >= text.Length) ch = -1;
            if (equalsIndex >= 0)
            {
                indices.Add(equalsIndex);
            }
            else
            {
                var lastSeparator = indices[^1];
                if (ch != comma || i != lastSeparator + 7 || string.CompareOrdinal(text, lastSeparator + 1, "base64", 0, 6) != 0)
                    throw new DartUriFormatException(DartFormatException.Format("Expecting '='", text, i));
                break;
            }
        }
        indices.Add(i);
        var data = new DartUriData(text, indices);
        if (data.IsBase64) ValidateBase64(text, i + 1, text.Length);
        return data;
    }

    /// <summary>Dart base64.normalize: validates the base64 alphabet and padding, throwing "Invalid base64 data" at the offending index.</summary>
    private static void ValidateBase64(string source, int start, int end)
    {
        // Emulates the checks of Dart's _Base64Decoder normalization: alphabet + '%3D' escapes + padding rules.
        int i = start;
        int digits = 0;
        int firstPadding = -1;
        int paddingCount = 0;
        for (; i < end; i++)
        {
            char c = source[i];
            if (c == '%')
            {
                // must be %3D or %3d (padding)
                if (i + 2 < end && source[i + 1] == '3' && (source[i + 2] == 'D' || source[i + 2] == 'd'))
                {
                    if (firstPadding < 0) firstPadding = i;
                    paddingCount++;
                    i += 2;
                    continue;
                }
                throw new DartUriFormatException(DartFormatException.Format("Invalid base64 data", source, i));
            }
            if (c == '=')
            {
                if (firstPadding < 0) firstPadding = i;
                paddingCount++;
                continue;
            }
            bool alpha = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '-' || c == '_';
            if (!alpha || paddingCount > 0)
                throw new DartUriFormatException(DartFormatException.Format("Invalid base64 data", source, i));
            digits++;
        }
        if (paddingCount > 0)
        {
            if (paddingCount > 2 || (digits + paddingCount) % 4 != 0)
                throw new DartUriFormatException(DartFormatException.Format("Invalid base64 padding, padded length must be multiple of four, is " + (digits + paddingCount), source, firstPadding));
        }
        else if (digits % 4 == 1)
        {
            throw new DartUriFormatException(DartFormatException.Format("Invalid base64 encoding length ", source, end));
        }
    }

    public string MimeType
    {
        get
        {
            int start = _separatorIndices[0] + 1;
            int end = _separatorIndices[1];
            if (start == end) return "text/plain";
            return Uri.UnescapeDataString(Text[start..end]);
        }
    }

    /// <summary>Dart contentAsBytes.</summary>
    public byte[] ContentAsBytes()
    {
        int start = _separatorIndices[^1] + 1;
        var content = Text[start..];
        if (IsBase64)
        {
            content = content.Replace("%3D", "=", StringComparison.OrdinalIgnoreCase).Replace('-', '+').Replace('_', '/');
            content = content.TrimEnd('=');
            int pad = (4 - content.Length % 4) % 4;
            if (pad == 3) pad = 0;
            content += new string('=', pad);
            return Convert.FromBase64String(content);
        }
        // percent-decoded bytes
        var bytes = new List<byte>(content.Length);
        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (c == '%' && i + 2 < content.Length && Uri.IsHexDigit(content[i + 1]) && Uri.IsHexDigit(content[i + 2]))
            {
                bytes.Add(Convert.ToByte(content.Substring(i + 1, 2), 16));
                i += 2;
            }
            else
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
            }
        }
        return bytes.ToArray();
    }
}

/// <summary>Minimal port of Dart's Uri as used by the validator: scheme/authority/path/query/fragment detection and Dart-like parse errors.</summary>
internal sealed class GltfUri
{
    public readonly string Original;
    public readonly string? Scheme;
    public readonly bool HasAuthority;
    public readonly string Path;
    public readonly bool HasQuery;
    public readonly bool HasFragment;
    public readonly DartUriData? Data;

    private GltfUri(string original, string? scheme, bool hasAuthority, string path, bool hasQuery, bool hasFragment, DartUriData? data)
    {
        Original = original;
        Scheme = scheme;
        HasAuthority = hasAuthority;
        Path = path;
        HasQuery = hasQuery;
        HasFragment = hasFragment;
        Data = data;
    }

    public bool HasScheme => !string.IsNullOrEmpty(Scheme);
    public bool HasAbsolutePath => Path.StartsWith('/');
    public bool IsNonRelative => HasScheme || HasAuthority || HasAbsolutePath || HasQuery || HasFragment;

    public override string ToString() => Original;

    /// <summary>Dart Uri.parse. On failure <paramref name="error"/> holds the Dart-formatted FormatException text.</summary>
    public static bool TryParse(string text, out GltfUri? uri, out string? error)
    {
        uri = null;
        error = null;
        try
        {
            uri = Parse(text);
            return true;
        }
        catch (DartUriFormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static GltfUri Parse(string text)
    {
        // scheme
        string? scheme = null;
        int rest = 0;
        int colon = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == ':') { colon = i; break; }
            if (c == '/' || c == '?' || c == '#') break;
        }
        if (colon == 0)
            throw new DartUriFormatException(DartFormatException.Format("Invalid empty scheme", text, 0));
        if (colon > 0)
        {
            var s = text[..colon];
            if (!char.IsAsciiLetter(s[0]))
                throw new DartUriFormatException(DartFormatException.Format("Scheme not starting with alphabetic character", text, 0));
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (!(char.IsAsciiLetterOrDigit(c) || c == '+' || c == '-' || c == '.'))
                    throw new DartUriFormatException(DartFormatException.Format("Illegal scheme character", text, i));
            }
            scheme = s.ToLowerInvariant();
            rest = colon + 1;
        }

        DartUriData? data = null;
        if (scheme == "data")
        {
            data = DartUriData.ParseInternal(text, 5);
        }

        bool hasAuthority = false;
        int p = rest;
        if (text.Length >= p + 2 && text[p] == '/' && text[p + 1] == '/')
        {
            hasAuthority = true;
            p += 2;
            while (p < text.Length && text[p] != '/' && text[p] != '?' && text[p] != '#') p++;
        }
        int pathStart = p;
        while (p < text.Length && text[p] != '?' && text[p] != '#') p++;
        var path = text[pathStart..p];
        bool hasQuery = false, hasFragment = false;
        if (p < text.Length && text[p] == '?')
        {
            hasQuery = true;
            while (p < text.Length && text[p] != '#') p++;
        }
        if (p < text.Length && text[p] == '#') hasFragment = true;

        return new GltfUri(text, scheme, hasAuthority, path, hasQuery, hasFragment, data);
    }
}
