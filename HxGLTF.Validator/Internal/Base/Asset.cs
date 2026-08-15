// Port of lib/src/base/asset.dart
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HxGLTF.Validator.Internal;

internal sealed class Asset : GltfProperty
{
    public static readonly Regex VersionRegexp = new(@"^([0-9]+)\.([0-9]+)$", RegexOptions.CultureInvariant);

    public readonly string? Copyright;
    public readonly string? Generator;
    public readonly string? Version;
    public readonly string? MinVersion;

    private Asset(string? copyright, string? generator, string? version, string? minVersion,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Copyright = copyright;
        Generator = generator;
        Version = version;
        MinVersion = minVersion;
    }

    private static int ParseGroup(string value, int group)
        => int.Parse(VersionRegexp.Match(value).Groups[group].Value, CultureInfo.InvariantCulture);

    public int MajorVersion
    {
        get
        {
            if (Version == null || !VersionRegexp.IsMatch(Version)) return 0;
            return ParseGroup(Version, 1);
        }
    }

    public int MinorVersion
    {
        get
        {
            if (Version == null || !VersionRegexp.IsMatch(Version)) return 0;
            return ParseGroup(Version, 2);
        }
    }

    public int MajorMinVersion
    {
        get
        {
            if (MinVersion == null || !VersionRegexp.IsMatch(MinVersion)) return 2;
            return ParseGroup(MinVersion, 1);
        }
    }

    public int MinorMinVersion
    {
        get
        {
            if (MinVersion == null || !VersionRegexp.IsMatch(MinVersion)) return 0;
            return ParseGroup(MinVersion, 2);
        }
    }

    public static Asset FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.ASSET_MEMBERS, context);
        }

        var asset = new Asset(
            JsonUtils.GetString(map, Members.COPYRIGHT, context),
            JsonUtils.GetString(map, Members.GENERATOR, context),
            JsonUtils.GetString(map, Members.VERSION, context, req: true, regexp: VersionRegexp),
            JsonUtils.GetString(map, Members.MIN_VERSION, context, regexp: VersionRegexp),
            JsonUtils.GetExtensions(map, typeof(Asset), context),
            JsonUtils.GetExtras(map, context));

        if (context.Validate && asset.MinVersion != null && asset.Version != null)
        {
            // Check that minVersion isn't greater than version
            if (asset.MajorMinVersion > asset.MajorVersion ||
                (asset.MajorMinVersion == asset.MajorVersion &&
                 asset.MinorMinVersion > asset.MinorVersion))
            {
                context.AddIssue(SemanticError.MinVersionGreaterThanVersion,
                    name: Members.MIN_VERSION, args: new object?[] { asset.MinVersion, asset.Version });
            }
        }

        return asset;
    }
}
