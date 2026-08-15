// Port of lib/src/base/animation.dart
// The checker classes AnimationInputChecker and QuaternionFloatChecker defined in animation.dart live in Internal/Data/ElementCheckers.cs.
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Animation : GltfChildOfRootProperty
{
    public readonly SafeList<AnimationChannel>? Channels;
    public readonly SafeList<AnimationSampler>? Samplers;

    private Animation(SafeList<AnimationChannel>? channels, SafeList<AnimationSampler>? samplers, string? name,
        Dictionary<string, object?> extensions, object? extras)
        : base(name, extensions, extras)
    {
        Channels = channels;
        Samplers = samplers;
    }

    public static Animation FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.ANIMATION_MEMBERS, context);
        }

        SafeList<AnimationChannel>? channels = null;
        var channelMaps = JsonUtils.GetMapList(map, Members.CHANNELS, context);
        if (channelMaps != null)
        {
            channels = new SafeList<AnimationChannel>(channelMaps.Count, Members.CHANNELS);
            context.Push(Members.CHANNELS);
            for (var i = 0; i < channelMaps.Count; i++)
            {
                var channelMap = channelMaps[i];
                context.Push(i);
                channels[i] = AnimationChannel.FromMap(channelMap, context);
                context.Pop();
            }
            context.Pop();
        }

        SafeList<AnimationSampler>? samplers = null;
        var samplerMaps = JsonUtils.GetMapList(map, Members.SAMPLERS, context);
        if (samplerMaps != null)
        {
            samplers = new SafeList<AnimationSampler>(samplerMaps.Count, Members.SAMPLERS);
            context.Push(Members.SAMPLERS);
            for (var i = 0; i < samplerMaps.Count; i++)
            {
                var samplerMap = samplerMaps[i];
                context.Push(i);
                samplers[i] = AnimationSampler.FromMap(samplerMap, context);
                context.Pop();
            }
            context.Pop();
        }

        return new Animation(channels, samplers, JsonUtils.GetName(map, context),
            JsonUtils.GetExtensions(map, typeof(Animation), context), JsonUtils.GetExtras(map, context));
    }

    public override void Link(Gltf gltf, Context context)
    {
        if (Samplers == null || Channels == null)
        {
            return;
        }

        var samplers = Samplers;
        var channels = Channels;

        context.Push(Members.SAMPLERS);
        samplers.ForEachWithIndices((i, sampler) =>
        {
            context.Push(i);

            sampler.SetInput(gltf.Accessors[sampler.InputIndex]);
            sampler.SetOutput(gltf.Accessors[sampler.OutputIndex]);

            if (sampler.InputIndex != -1)
            {
                if (sampler.Input == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: Members.INPUT, args: new object?[] { sampler.InputIndex });
                }
                else
                {
                    sampler.Input.SetUsage(AccessorUsage.AnimationInput, Members.INPUT, context);

                    var inputBufferView = sampler.Input.BufferView;
                    if (inputBufferView != null)
                    {
                        inputBufferView.SetUsage(BufferViewUsage.Other, Members.INPUT, context);
                        if (context.Validate && inputBufferView.ByteStride != -1)
                        {
                            context.AddIssue(LinkError.AnimationSamplerAccessorWithByteStride, name: Members.INPUT);
                        }
                    }

                    if (context.Validate)
                    {
                        context.Push(Members.INPUT);
                        var inputFormat = AccessorFormat.FromAccessor(sampler.Input);
                        if (!inputFormat.Equals(Members.ANIMATION_SAMPLER_INPUT_FORMAT))
                        {
                            context.AddIssue(LinkError.AnimationSamplerInputAccessorInvalidFormat,
                                args: new object?[]
                                {
                                    inputFormat,
                                    new[] { Members.ANIMATION_SAMPLER_INPUT_FORMAT },
                                });
                        }
                        else
                        {
                            context.AddElementChecker(sampler.Input, new AnimationInputChecker(context.GetPointerString()));
                        }

                        if (sampler.Input.Min == null || sampler.Input.Max == null)
                        {
                            context.AddIssue(LinkError.AnimationSamplerInputAccessorWithoutBounds);
                        }

                        if (sampler.Interpolation == Members.CUBICSPLINE &&
                            sampler.Input.Count < 2)
                        {
                            context.AddIssue(LinkError.AnimationSamplerInputAccessorTooFewElements,
                                args: new object?[] { Members.CUBICSPLINE, 2, sampler.Input.Count });
                        }
                        context.Pop();
                    }
                }
            }

            if (sampler.OutputIndex != -1)
            {
                if (sampler.Output == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: Members.OUTPUT, args: new object?[] { sampler.OutputIndex });
                }
                else
                {
                    sampler.Output.SetUsage(AccessorUsage.AnimationOutput, Members.OUTPUT, context);
                    var outputBufferView = sampler.Output.BufferView;
                    if (outputBufferView != null)
                    {
                        outputBufferView.SetUsage(BufferViewUsage.Other, Members.OUTPUT, context);
                        if (context.Validate && outputBufferView.ByteStride != -1)
                        {
                            context.AddIssue(LinkError.AnimationSamplerAccessorWithByteStride, name: Members.OUTPUT);
                        }
                    }

                    sampler.Output.BufferView?.SetUsage(BufferViewUsage.Other, Members.OUTPUT, context);
                    sampler.Output.TrySetInterpolation(cubic: Members.CUBICSPLINE == sampler.Interpolation);
                }
            }

            context.Pop();
        });

        context.Pop();
        context.Push(Members.CHANNELS);

        channels.ForEachWithIndices((i, channel) =>
        {
            context.Push(i);

            channel.SetSampler(samplers[channel.SamplerIndex]);

            if (channel.Target != null)
            {
                channel.Target.SetNode(gltf.Nodes[channel.Target.NodeIndex]);
                if (context.Validate && channel.Target.NodeIndex != -1)
                {
                    context.Push(Members.TARGET);
                    if (channel.Target.Node == null)
                    {
                        context.AddIssue(LinkError.UnresolvedReference, name: Members.NODE, args: new object?[] { channel.Target.NodeIndex });
                    }
                    else
                    {
                        channel.Target.Node.MarkAsUsed();
                        switch (channel.Target.Path)
                        {
                            case Members.TRANSLATION:
                            case Members.ROTATION:
                            case Members.SCALE:
                                if (channel.Target.Node.Matrix != null)
                                {
                                    context.AddIssue(LinkError.AnimationChannelTargetNodeMatrix);
                                }

                                if (channel.Target.Node.Skin != null)
                                {
                                    context.AddIssue(SemanticError.AnimationChannelTargetNodeSkin, name: Members.PATH);
                                }
                                break;
                            case Members.WEIGHTS:
                                // Dart: channel.target._node?.mesh?.primitives?.first?.targets == null
                                if (channel.Target.Node?.Mesh?.Primitives?[0]?.Targets == null)
                                {
                                    context.AddIssue(LinkError.AnimationChannelTargetNodeWeightsNoMorphs);
                                }
                                break;
                        }
                    }
                    context.Pop();
                }
            }

            if (channel.SamplerIndex != -1)
            {
                if (channel.Sampler == null)
                {
                    context.AddIssue(LinkError.UnresolvedReference, name: Members.SAMPLER, args: new object?[] { channel.SamplerIndex });
                }
                else
                {
                    channel.Sampler.MarkAsUsed();
                    if (channel.Target != null && channel.Sampler.Output != null)
                    {
                        if (channel.Target.Path == Members.ROTATION)
                        {
                            if (context.Validate)
                            {
                                var accessor = channel.Sampler.Output;
                                // TODO warn when there're more than two equal
                                // consequential animation frames

                                // TODO warn when interpolation may produce zero-length
                                // quaternions

                                // quaternion animation output
                                if (accessor.Components == 4)
                                {
                                    context.Push(Members.SAMPLER);
                                    context.AddElementChecker(
                                        accessor,
                                        new QuaternionFloatChecker(context.GetPointerString(),
                                            accessor.IsFloat ? null : accessor.NormalizeValue,
                                            hasTangents: Members.CUBICSPLINE == channel.Sampler.Interpolation));
                                    context.Pop();
                                }
                            }
                            channel.Sampler.Output.SetUnit();
                        }

                        if (context.Validate)
                        {
                            var outputFormat = AccessorFormat.FromAccessor(channel.Sampler.Output);
                            IReadOnlyList<AccessorFormat>? validFormats = null;
                            if (channel.Target.Path != null &&
                                Members.ANIMATION_SAMPLER_OUTPUT_FORMATS.TryGetValue(channel.Target.Path, out var formats))
                            {
                                validFormats = formats;
                            }

                            // Dart: validFormats?.contains(outputFormat) == false
                            if (validFormats != null && !validFormats.Contains(outputFormat))
                            {
                                context.AddIssue(LinkError.AnimationSamplerOutputAccessorInvalidFormat,
                                    name: Members.SAMPLER,
                                    args: new object?[] { outputFormat, validFormats, channel.Target.Path });
                            }

                            if (channel.Sampler.Input != null &&
                                channel.Sampler.Input.Count != -1 &&
                                channel.Sampler.Output.Count != -1 &&
                                channel.Sampler.Interpolation != null)
                            {
                                var expectedCount = channel.Sampler.Input.Count;

                                if (channel.Sampler.Interpolation == Members.CUBICSPLINE)
                                {
                                    expectedCount *= 3;
                                }

                                if (channel.Target.Path == Members.WEIGHTS)
                                {
                                    // Dart: channel.target._node?.mesh?.primitives?.first?.targets?.length
                                    int? targetsCount = channel.Target.Node?.Mesh?.Primitives?[0]?.Targets?.Count;
                                    expectedCount *= targetsCount ?? 0;
                                }
                                else if (!Members.ANIMATION_CHANNEL_TARGET_PATHS.Contains(channel.Target.Path))
                                {
                                    expectedCount = 0;
                                }

                                if (expectedCount != 0 &&
                                    expectedCount != channel.Sampler.Output.Count)
                                {
                                    context.AddIssue(LinkError.AnimationSamplerOutputAccessorInvalidCount,
                                        name: Members.SAMPLER,
                                        args: new object?[] { expectedCount, channel.Sampler.Output.Count });
                                }
                            }
                        }
                    }
                }

                for (var j = i + 1; j < channels.Length; j++)
                {
                    if (channel.Target != null &&
                        channel.Target.IsSameAs(channels[j]?.Target))
                    {
                        context.AddIssue(LinkError.AnimationDuplicateTargets, name: Members.TARGET, args: new object?[] { j });
                    }
                }
                context.Pop();
            }
        });
        context.Pop();

        if (context.Validate)
        {
            context.Push(Members.SAMPLERS);
            for (var i = 0; i < samplers.Length; ++i)
            {
                // Dart: samplers[i].isUsed (samplers are always non-null here, see getMapList)
                if (samplers[i]?.IsUsed == false)
                {
                    context.AddIssue(LinkError.UnusedObject, index: i);
                }
            }
            context.Pop();
        }
    }
}

internal sealed class AnimationChannel : GltfProperty
{
    private readonly int _samplerIndex;
    public readonly AnimationChannelTarget? Target;

    private AnimationSampler? _sampler;

    private AnimationChannel(int samplerIndex, AnimationChannelTarget? target,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        _samplerIndex = samplerIndex;
        Target = target;
    }

    public AnimationSampler? Sampler => _sampler;

    /// <summary>Dart <c>_samplerIndex</c> (accessed from Animation.link).</summary>
    internal int SamplerIndex => _samplerIndex;

    /// <summary>Dart <c>channel._sampler = ...</c> (accessed from Animation.link).</summary>
    internal void SetSampler(AnimationSampler? sampler) => _sampler = sampler;

    public static AnimationChannel FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.ANIMATION_CHANNEL_MEMBERS, context);
        }

        return new AnimationChannel(
            JsonUtils.GetIndex(map, Members.SAMPLER, context),
            JsonUtils.GetObjectFromInnerMap<AnimationChannelTarget>(
                map, Members.TARGET, context, AnimationChannelTarget.FromMap, req: true),
            JsonUtils.GetExtensions(map, typeof(AnimationChannel), context),
            JsonUtils.GetExtras(map, context));
    }
}

internal sealed class AnimationChannelTarget : GltfProperty
{
    private readonly int _nodeIndex;
    public readonly string? Path;

    private Node? _node;

    private AnimationChannelTarget(int nodeIndex, string? path, Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        _nodeIndex = nodeIndex;
        Path = path;
    }

    public Node? Node => _node;

    /// <summary>Dart <c>_nodeIndex</c> (accessed from Animation.link).</summary>
    internal int NodeIndex => _nodeIndex;

    /// <summary>Dart <c>channel.target._node = ...</c> (accessed from Animation.link).</summary>
    internal void SetNode(Node? node) => _node = node;

    public static AnimationChannelTarget FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.ANIMATION_CHANNEL_TARGET_MEMBERS, context);
        }

        var extensions = JsonUtils.GetExtensions(map, typeof(AnimationChannelTarget), context,
            overriddenType: typeof(Animation));

        var target = new AnimationChannelTarget(
            JsonUtils.GetIndex(map, Members.NODE, context, req: false),
            JsonUtils.GetString(map, Members.PATH, context,
                req: true, list: context.AnimationChannelTargetPaths),
            extensions,
            JsonUtils.GetExtras(map, context));

        context.RegisterObjectsOwner(target, extensions.Values);

        return target;
    }

    public bool IsSameAs(AnimationChannelTarget? other) =>
        other != null &&
        _nodeIndex != -1 &&
        _nodeIndex == other._nodeIndex &&
        Path == other.Path;
}

internal sealed class AnimationSampler : GltfProperty
{
    private readonly int _inputIndex;
    public readonly string? Interpolation;
    private readonly int _outputIndex;

    private Accessor? _input;
    private Accessor? _output;

    private AnimationSampler(int inputIndex, string? interpolation, int outputIndex,
        Dictionary<string, object?> extensions, object? extras)
        : base(extensions, extras)
    {
        _inputIndex = inputIndex;
        Interpolation = interpolation;
        _outputIndex = outputIndex;
    }

    public Accessor? Input => _input;
    public Accessor? Output => _output;

    /// <summary>Dart <c>_inputIndex</c> (accessed from Animation.link).</summary>
    internal int InputIndex => _inputIndex;

    /// <summary>Dart <c>_outputIndex</c> (accessed from Animation.link).</summary>
    internal int OutputIndex => _outputIndex;

    internal void SetInput(Accessor? input) => _input = input;

    internal void SetOutput(Accessor? output) => _output = output;

    public static AnimationSampler FromMap(JsonElement map, Context context)
    {
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.ANIMATION_SAMPLER_MEMBERS, context);
        }

        return new AnimationSampler(
            JsonUtils.GetIndex(map, Members.INPUT, context),
            JsonUtils.GetString(map, Members.INTERPOLATION, context,
                list: Members.ANIMATION_SAMPLER_INTERPOLATIONS, def: Members.LINEAR),
            JsonUtils.GetIndex(map, Members.OUTPUT, context),
            JsonUtils.GetExtensions(map, typeof(AnimationSampler), context),
            JsonUtils.GetExtras(map, context));
    }
}
