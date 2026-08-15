// Port of lib/src/base/camera.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Camera : GltfChildOfRootProperty
{
    public readonly string? Type;
    public readonly CameraOrthographic? Orthographic;
    public readonly CameraPerspective? Perspective;

    private Camera(string? type, CameraOrthographic? orthographic, CameraPerspective? perspective, string? name,
        Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        Type = type;
        Orthographic = orthographic;
        Perspective = perspective;
    }

    public static Camera FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.CAMERA_MEMBERS, context);
        }

        if (context.Validate &&
            JsonUtils.Has(map, Members.CAMERA_TYPES[0]) &&
            JsonUtils.Has(map, Members.CAMERA_TYPES[1]))
        {
            context.AddIssue(SchemaError.OneOfMismatch, args: Members.CAMERA_TYPES.Cast<object?>().ToArray());
        }

        var type = JsonUtils.GetString(map, Members.TYPE, context, req: true, list: Members.CAMERA_TYPES);

        CameraOrthographic? orthographic = null;
        CameraPerspective? perspective = null;

        if (type == Members.ORTHOGRAPHIC)
        {
            orthographic = JsonUtils.GetObjectFromInnerMap(
                map, Members.ORTHOGRAPHIC, context, CameraOrthographic.FromMap,
                req: true);
        }
        else if (type == Members.PERSPECTIVE)
        {
            perspective = JsonUtils.GetObjectFromInnerMap(
                map, Members.PERSPECTIVE, context, CameraPerspective.FromMap,
                req: true);
        }

        return new Camera(type, orthographic, perspective, JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Camera), context), JsonUtils.GetExtras(map, context));
    }
}

internal sealed class CameraOrthographic : GltfProperty
{
    public readonly double Xmag;
    public readonly double Ymag;
    public readonly double Zfar;
    public readonly double Znear;

    private CameraOrthographic(double xmag, double ymag, double zfar, double znear,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        Xmag = xmag;
        Ymag = ymag;
        Zfar = zfar;
        Znear = znear;
    }

    public static CameraOrthographic FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.CAMERA_ORTHOGRAPHIC_MEMBERS, context);
        }

        var xmag = JsonUtils.GetFloat(map, Members.XMAG, context, req: true);
        var ymag = JsonUtils.GetFloat(map, Members.YMAG, context, req: true);

        var zfar = JsonUtils.GetFloat(map, Members.ZFAR, context, req: true, exclMin: 0);
        var znear = JsonUtils.GetFloat(map, Members.ZNEAR, context, req: true, min: 0);

        if (context.Validate)
        {
            // Dart: comparisons with NaN (missing/invalid values) are false, same as in C#.
            if (zfar <= znear)
            {
                context.AddIssue(SemanticError.CameraZfarLequalZnear);
            }

            if (xmag == 0.0)
            {
                context.AddIssue(SemanticError.CameraXmagYmagZero, name: Members.XMAG);
            }
            else if (xmag < 0.0)
            {
                context.AddIssue(SemanticError.CameraXmagYmagNegative, name: Members.XMAG);
            }

            if (ymag == 0.0)
            {
                context.AddIssue(SemanticError.CameraXmagYmagZero, name: Members.YMAG);
            }
            else if (ymag < 0.0)
            {
                context.AddIssue(SemanticError.CameraXmagYmagNegative, name: Members.YMAG);
            }
        }

        return new CameraOrthographic(
            xmag,
            ymag,
            zfar,
            znear,
            JsonUtils.GetExtensions(map, typeof(CameraOrthographic), context),
            JsonUtils.GetExtras(map, context));
    }
}

internal sealed class CameraPerspective : GltfProperty
{
    public readonly double AspectRatio;
    public readonly double Yfov;
    public readonly double Zfar;
    public readonly double Znear;

    private CameraPerspective(double aspectRatio, double yfov, double zfar, double znear,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        AspectRatio = aspectRatio;
        Yfov = yfov;
        Zfar = zfar;
        Znear = znear;
    }

    public static CameraPerspective FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.CAMERA_PERSPECTIVE_MEMBERS, context);
        }

        var yfov = JsonUtils.GetFloat(map, Members.YFOV, context, req: true, exclMin: 0);

        if (context.Validate && yfov >= Math.PI)
        {
            context.AddIssue(SemanticError.CameraYFovGequalPi);
        }

        var zfar = JsonUtils.GetFloat(map, Members.ZFAR, context, exclMin: 0);
        var znear = JsonUtils.GetFloat(map, Members.ZNEAR, context, req: true, exclMin: 0);

        if (context.Validate && zfar <= znear)
        {
            context.AddIssue(SemanticError.CameraZfarLequalZnear);
        }

        return new CameraPerspective(
            JsonUtils.GetFloat(map, Members.ASPECT_RATIO, context, exclMin: 0),
            yfov,
            zfar,
            znear,
            JsonUtils.GetExtensions(map, typeof(CameraPerspective), context),
            JsonUtils.GetExtras(map, context));
    }
}
