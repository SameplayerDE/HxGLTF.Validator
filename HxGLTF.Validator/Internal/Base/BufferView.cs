// Port of lib/src/base/buffer_view.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class BufferView : GltfChildOfRootProperty
{
    private readonly int _bufferIndex;
    public readonly int ByteOffset;
    public readonly int ByteLength;
    public readonly int ByteStride;
    private readonly int _target;

    private Buffer? _buffer;
    private BufferViewUsage? _usage;

    public int EffectiveByteStride = -1;

    private BufferView(int bufferIndex, int byteOffset, int byteLength, int byteStride, int target,
        string? name, Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        _bufferIndex = bufferIndex;
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        ByteStride = byteStride;
        _target = target;
    }

    public Buffer? Buffer => _buffer;

    public BufferViewUsage? Usage => _usage;

    // Dart: `_target != -1 ? _target : usage.target` (throws when usage is null, same as Dart).
    public int Target => _target != -1 ? _target : _usage!.Target;

    public void SetUsage(BufferViewUsage value, string name, Context context)
    {
        MarkAsUsed();
        if (_usage == null)
        {
            _usage = value;
            if (value == BufferViewUsage.IndexBuffer || value == BufferViewUsage.VertexBuffer)
            {
                context.AddIssue(LinkError.BufferViewTargetMissing, name: name);
            }
        }
        else if (context.Validate && _usage != value)
        {
            context.AddIssue(LinkError.BufferViewTargetOverride,
                name: name, args: new object?[] { _usage, value });
        }
    }

    public static BufferView FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.BUFFER_VIEW_MEMBERS, context);
        }

        var byteLength = JsonUtils.GetUint(map, Members.BYTE_LENGTH, context, req: true, min: 1);
        var byteStride = JsonUtils.GetUint(map, Members.BYTE_STRIDE, context, min: 4, max: 252);
        var target = JsonUtils.GetUint(map, Members.TARGET, context, list: Gl.TARGETS);

        if (context.Validate && byteStride != -1)
        {
            if (byteLength != -1 && byteStride > byteLength)
            {
                context.AddIssue(SemanticError.BufferViewTooBigByteStride,
                    name: Members.BYTE_STRIDE, args: new object?[] { byteStride, byteLength });
            }

            if (byteStride % 4 != 0)
            {
                context.AddIssue(SchemaError.ValueMultipleOf,
                    name: Members.BYTE_STRIDE, args: new object?[] { byteStride, 4 });
            }

            if (target == Gl.ELEMENT_ARRAY_BUFFER)
            {
                context.AddIssue(SemanticError.BufferViewInvalidByteStride,
                    name: Members.BYTE_STRIDE);
            }
        }

        return new BufferView(
            JsonUtils.GetIndex(map, Members.BUFFER, context),
            JsonUtils.GetUint(map, Members.BYTE_OFFSET, context, def: 0),
            byteLength,
            byteStride,
            target,
            JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(BufferView), context),
            JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        _buffer = gltf.Buffers[_bufferIndex];

        EffectiveByteStride = ByteStride;

        if (_target == Gl.ARRAY_BUFFER)
        {
            _usage = BufferViewUsage.VertexBuffer;
        }
        else if (_target == Gl.ELEMENT_ARRAY_BUFFER)
        {
            _usage = BufferViewUsage.IndexBuffer;
        }

        if (context.Validate && _bufferIndex != -1)
        {
            if (_buffer == null)
            {
                context.AddIssue(LinkError.UnresolvedReference,
                    name: Members.BUFFER, args: new object?[] { _bufferIndex });
            }
            else
            {
                _buffer.MarkAsUsed();
                if (_buffer.ByteLength != -1)
                {
                    if (ByteOffset >= _buffer.ByteLength)
                    {
                        context.AddIssue(LinkError.BufferViewTooLong,
                            name: Members.BYTE_OFFSET, args: new object?[] { _bufferIndex, _buffer.ByteLength });
                    }
                    else if ((long)ByteOffset + ByteLength > _buffer.ByteLength)
                    {
                        context.AddIssue(LinkError.BufferViewTooLong,
                            name: Members.BYTE_LENGTH, args: new object?[] { _bufferIndex, _buffer.ByteLength });
                    }
                }
            }
        }
    }
}
