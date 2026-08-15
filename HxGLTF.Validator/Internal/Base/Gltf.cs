// Port of lib/src/base/gltf.dart
using System.Text.Json;

namespace HxGLTF.Validator.Internal;

internal sealed class Gltf : GltfProperty
{
    public readonly IReadOnlyList<string> ExtensionsUsed;
    public readonly IReadOnlyList<string> ExtensionsRequired;
    public readonly SafeList<Accessor> Accessors;
    public readonly SafeList<Animation> Animations;
    public readonly Asset Asset;
    public readonly SafeList<Buffer> Buffers;
    public readonly SafeList<BufferView> BufferViews;
    public readonly SafeList<Camera> Cameras;
    public readonly SafeList<Image> Images;
    public readonly SafeList<Material> Materials;
    public readonly SafeList<Mesh> Meshes;
    public readonly SafeList<Node> Nodes;
    public readonly SafeList<Sampler> Samplers;
    public readonly Scene? Scene;
    public readonly SafeList<Scene> Scenes;
    public readonly SafeList<Skin> Skins;
    public readonly SafeList<Texture> Textures;

    private Gltf(
        IReadOnlyList<string> extensionsUsed,
        IReadOnlyList<string> extensionsRequired,
        SafeList<Accessor> accessors,
        SafeList<Animation> animations,
        Asset asset,
        SafeList<Buffer> buffers,
        SafeList<BufferView> bufferViews,
        SafeList<Camera> cameras,
        SafeList<Image> images,
        SafeList<Material> materials,
        SafeList<Mesh> meshes,
        SafeList<Node> nodes,
        SafeList<Sampler> samplers,
        Scene? scene,
        SafeList<Scene> scenes,
        SafeList<Skin> skins,
        SafeList<Texture> textures,
        Dictionary<string, object?> extensions,
        object? extras)
        : base(extensions, extras)
    {
        ExtensionsUsed = extensionsUsed;
        ExtensionsRequired = extensionsRequired;
        Accessors = accessors;
        Animations = animations;
        Asset = asset;
        Buffers = buffers;
        BufferViews = bufferViews;
        Cameras = cameras;
        Images = images;
        Materials = materials;
        Meshes = meshes;
        Nodes = nodes;
        Samplers = samplers;
        Scene = scene;
        Scenes = scenes;
        Skins = skins;
        Textures = textures;
    }

    /// <summary>Dart <c>Gltf.fromMap</c>. Returns null when the asset is missing/invalid or has an unknown major version.</summary>
    public static Gltf? FromMap(JsonElement map, Context context)
    {
        void ResetPath() => context.Path.Clear();

        ResetPath();
        if (context.Validate)
        {
            JsonUtils.CheckMembers(map, Members.GLTF_MEMBERS, context);

            // See https://github.com/KhronosGroup/glTF/pull/1025
            if (JsonUtils.Has(map, Members.EXTENSIONS_REQUIRED) &&
                !JsonUtils.Has(map, Members.EXTENSIONS_USED))
            {
                context.AddIssue(SchemaError.UnsatisfiedDependency,
                    name: Members.EXTENSIONS_REQUIRED, args: new object?[] { Members.EXTENSIONS_USED });
            }
        }

        var extensionsUsed = JsonUtils.GetStringList(map, Members.EXTENSIONS_USED, context) ?? Array.Empty<string>();

        var extensionsRequired = JsonUtils.GetStringList(map, Members.EXTENSIONS_REQUIRED, context) ?? Array.Empty<string>();

        context.InitExtensions(extensionsUsed, extensionsRequired);

        // Helper function for converting JSON array to List of proper glTF objects
        SafeList<T> ToSafeList<T>(string name, FromMapFunction<T> fromMap) where T : class
        {
            if (!JsonUtils.Has(map, name))
            {
                return SafeList<T>.Empty(name);
            }

            ResetPath();

            var itemsList = map.GetProperty(name);
            if (itemsList.ValueKind == JsonValueKind.Array)
            {
                var length = itemsList.GetArrayLength();
                if (length > 0)
                {
                    var items = new SafeList<T>(length, name);
                    context.Push(name);
                    var i = 0;
                    foreach (var itemMap in itemsList.EnumerateArray())
                    {
                        if (itemMap.ValueKind == JsonValueKind.Object)
                        {
                            // JSON mandates all keys to be string
                            context.Push(i);
                            items[i] = fromMap(itemMap, context);
                            context.Pop();
                        }
                        else
                        {
                            context.AddIssue(SchemaError.TypeMismatch,
                                index: i, args: new object?[] { JsonUtils.Value(itemMap), "object" });
                        }
                        i++;
                    }
                    return items;
                }
                else
                {
                    context.AddIssue(SchemaError.EmptyEntity, name: name);
                    return SafeList<T>.Empty(name);
                }
            }
            else
            {
                context.AddIssue(SchemaError.TypeMismatch,
                    name: name, args: new object?[] { JsonUtils.Value(itemsList), "array" });
                return SafeList<T>.Empty(name);
            }
        }

        // Helper function for converting JSON dictionary to proper glTF object
        T? ToValue<T>(string name, FromMapFunction<T> fromMap, bool req = false) where T : class
        {
            ResetPath();
            var item = JsonUtils.GetMap(map, name, context, req: req);
            if (JsonUtils.IsUndefined(item))
            {
                return null;
            }
            context.Push(name);
            return fromMap(item, context);
        }

        var asset = ToValue<Asset>(Members.ASSET, Asset.FromMap, req: true);

        if (asset?.Version == null)
        {
            return null;
        }
        else if (asset.MajorVersion != 2)
        {
            context.AddIssue(SemanticError.UnknownAssetMajorVersion,
                args: new object?[] { asset.MajorVersion }, name: Members.VERSION);
            return null;
        }
        else if (asset.MinorVersion > 0)
        {
            context.AddIssue(SemanticError.UnknownAssetMinorVersion,
                args: new object?[] { asset.MinorVersion }, name: Members.VERSION);
        }

        var accessors = ToSafeList<Accessor>(Members.ACCESSORS, Accessor.FromMap);

        var animations = ToSafeList<Animation>(Members.ANIMATIONS, Animation.FromMap);

        var buffers = ToSafeList<Buffer>(Members.BUFFERS, Buffer.FromMap);

        var bufferViews = ToSafeList<BufferView>(Members.BUFFER_VIEWS, BufferView.FromMap);

        var cameras = ToSafeList<Camera>(Members.CAMERAS, Camera.FromMap);

        var images = ToSafeList<Image>(Members.IMAGES, Image.FromMap);

        var materials = ToSafeList<Material>(Members.MATERIALS, Material.FromMap);

        var meshes = ToSafeList<Mesh>(Members.MESHES, Mesh.FromMap);

        var nodes = ToSafeList<Node>(Members.NODES, Node.FromMap);

        var samplers = ToSafeList<Sampler>(Members.SAMPLERS, Sampler.FromMap);

        var scenes = ToSafeList<Scene>(Members.SCENES, Scene.FromMap);

        ResetPath();
        var sceneIndex = JsonUtils.GetIndex(map, Members.SCENE, context, req: false);
        var scene = scenes[sceneIndex];

        if (context.Validate && sceneIndex != -1 && scene == null)
        {
            context.AddIssue(LinkError.UnresolvedReference, name: Members.SCENE, args: new object?[] { sceneIndex });
        }

        var skins = ToSafeList<Skin>(Members.SKINS, Skin.FromMap);

        var textures = ToSafeList<Texture>(Members.TEXTURES, Texture.FromMap);

        ResetPath();
        var extensions = JsonUtils.GetExtensions(map, typeof(Gltf), context);

        ResetPath();

        var gltf = new Gltf(
            extensionsUsed,
            extensionsRequired,
            accessors,
            animations,
            asset,
            buffers,
            bufferViews,
            cameras,
            images,
            materials,
            meshes,
            nodes,
            samplers,
            scene,
            scenes,
            skins,
            textures,
            extensions,
            JsonUtils.GetExtras(map, context));

        // Step 2: linking IDs
        void LinkCollection<T>(SafeList<T> list, Type type) where T : GltfProperty
        {
            context.Push(list.Name);
            list.ForEachWithIndices((i, item) =>
            {
                context.Push(i);
                item.Link(gltf, context);
                context.Pop();
            });

            if (context.LinkableExtensions.TryGetValue(type, out var linkableExtensions))
            {
                var oldPath = context.Path.ToArray();
                foreach (var entry in linkableExtensions)
                {
                    context.Path.Clear();
                    context.Path.AddRange(entry.Path);
                    entry.Object.Link(gltf, context);
                }
                context.Path.Clear();
                context.Path.AddRange(oldPath);
            }

            context.Pop();
        }

        // Fixed order
        LinkCollection(bufferViews, typeof(BufferView));

        LinkCollection(accessors, typeof(Accessor));

        LinkCollection(images, typeof(Image));
        LinkCollection(textures, typeof(Texture));
        LinkCollection(materials, typeof(Material));

        LinkCollection(meshes, typeof(Mesh));

        LinkCollection(nodes, typeof(Node));
        LinkCollection(skins, typeof(Skin));

        LinkCollection(animations, typeof(Animation));
        LinkCollection(scenes, typeof(Scene));

        // Link root-level extensions
        if (extensions.Count > 0)
        {
            context.Push(Members.EXTENSIONS);
            foreach (var (name, obj) in extensions)
            {
                if (obj is ILinkable linkable)
                {
                    context.Push(name);
                    linkable.Link(gltf, context);
                    context.Pop();
                }
            }
            context.Pop();
        }

        // Check node tree loops, skins, and orphaned objects
        if (context.Validate)
        {
            context.Push(Members.NODES);
            var seenNodes = new HashSet<Node>(ReferenceEqualityComparer.Instance);
            gltf.Nodes.ForEachWithIndices((i, node) =>
            {
                if (!node.IsJoint &&
                    node.Children == null &&
                    node.Mesh == null &&
                    node.Camera == null &&
                    node.Extensions.Count == 0 &&
                    node.Extras == null)
                {
                    context.AddIssue(SemanticError.NodeEmpty, index: i);
                }

                // Node has a parent, check for loops
                if (node.Parent != null)
                {
                    seenNodes.Clear();
                    var temp = node;
                    while (temp.Parent != null)
                    {
                        if (seenNodes.Add(temp))
                        {
                            temp = temp.Parent;
                        }
                        else
                        {
                            if (ReferenceEquals(temp, node))
                            {
                                context.AddIssue(LinkError.NodeLoop, index: i);
                            }
                            break;
                        }
                    }
                }

                // Node has a skinned mesh, check hierarchy and scenes
                if (node.Skin != null)
                {
                    if (node.HasTransform)
                    {
                        context.AddIssue(SemanticError.NodeSkinnedMeshLocalTransforms, index: i);
                    }
                    if (node.Parent != null)
                    {
                        var parent = node.Parent;
                        while (parent != null)
                        {
                            if (parent.HasTransform)
                            {
                                context.AddIssue(SemanticError.NodeSkinnedMeshParentTransforms, index: i);
                                break;
                            }
                            parent = parent.Parent;
                        }
                        context.AddIssue(SemanticError.NodeSkinnedMeshNonRoot, index: i);
                    }

                    var topCommonRoot = node.Skin.CommonRoots.FirstOrDefault(root => root.Parent == null);
                    if (topCommonRoot != null &&
                        !node.Scenes.All(topCommonRoot.Scenes.Contains))
                    {
                        context.AddIssue(SemanticError.NodeSkinNoScene, index: i);
                    }
                }
            });
            context.Pop();

            // Checking unused objects
            var collections = new IReadOnlyList<Usable?>[]
            {
                accessors,
                buffers,
                bufferViews,
                cameras,
                images,
                materials,
                meshes,
                nodes,
                samplers,
                skins,
                textures,
            };
            var collectionNames = new[]
            {
                accessors.Name,
                buffers.Name,
                bufferViews.Name,
                cameras.Name,
                images.Name,
                materials.Name,
                meshes.Name,
                nodes.Name,
                samplers.Name,
                skins.Name,
                textures.Name,
            };

            for (var c = 0; c < collections.Length; c++)
            {
                var collection = collections[c];
                if (collection.Count == 0)
                {
                    continue;
                }

                context.Push(collectionNames[c]);
                for (var i = 0; i < collection.Count; ++i)
                {
                    if (collection[i]?.IsUsed == false)
                    {
                        context.AddIssue(LinkError.UnusedObject, index: i);
                    }
                }
                context.Pop();
            }

            if (context.ExtensionCollections.Count > 0)
            {
                foreach (var (collection, path) in context.ExtensionCollections)
                {
                    if (collection.Count == 0)
                    {
                        continue;
                    }

                    context.Path.Clear();
                    context.Path.AddRange(path);
                    for (var i = 0; i < collection.Count; ++i)
                    {
                        if (collection[i]?.IsUsed == false)
                        {
                            context.AddIssue(LinkError.UnusedObject, index: i);
                        }
                    }
                }
                context.Path.Clear();
            }

            // Check for meshes with unused static weights
            context.Push(Members.MESHES);
            for (var i = 0; i < meshes.Length; ++i)
            {
                var mesh = meshes[i];
                if (mesh?.Weights != null && mesh.IsUsed && !mesh.AreWeightsUsed)
                {
                    context.Push(i);
                    context.AddIssue(LinkError.UnusedMeshWeights, name: Members.WEIGHTS);
                    context.Pop();
                }
            }
            context.Path.Clear();
        }

        return gltf;
    }

    public void ValidateResources(Context context)
    {
        void ValidateResourcesInCollection<T>(SafeList<T> list) where T : class, IResourceValidatable
        {
            context.Path.Clear();
            context.Push(list.Name);
            list.ForEachWithIndices((i, item) =>
            {
                context.Push(i);
                item.ValidateResources(this, context);
                context.Pop();
            });
            context.Pop();
        }

        // Only textures require this validation step for now.
        ValidateResourcesInCollection(Textures);

        var extensions = context.ResourceValidatableExtensions;
        if (extensions != null)
        {
            foreach (var entry in extensions)
            {
                context.Path.Clear();
                context.Path.AddRange(entry.Path);
                entry.Object.ValidateResources(this, context);
            }
            context.Path.Clear();
        }
    }
}
