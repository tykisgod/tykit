// Commands/AssetCommands.cs
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace Tykit
{
    [InitializeOnLoad]
    public static class AssetCommands
    {
        static AssetCommands()
        {
            RegisterCommands();
        }

        public static void RegisterCommands()
        {
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "create-prefab",
                    "Save a scene GameObject as a prefab asset.",
                    "assets.mutate",
                    true,
                    CommandSchema.Object(
                        ("source", CommandSchema.String("GameObject name to save as prefab.")),
                        ("path", CommandSchema.String("Prefab asset path under Assets/.")))),
                CreatePrefab);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "create-material",
                    "Create a material asset.",
                    "assets.mutate",
                    true,
                    CommandSchema.Object(
                        ("path", CommandSchema.String("Material asset path under Assets/.")),
                        ("shader", CommandSchema.String("Shader name. Defaults to Universal Render Pipeline/Lit.")))),
                CreateMaterial);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "create-physics-material-2d",
                    "Create a PhysicsMaterial2D asset.",
                    "assets.mutate",
                    true,
                    CommandSchema.Object(
                        ("path", CommandSchema.String("PhysicsMaterial2D asset path under Assets/.")),
                        ("friction", CommandSchema.Number("Friction value.")),
                        ("bounciness", CommandSchema.Number("Bounciness value.")))),
                CreatePhysicsMaterial2D);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "list-assets",
                    "List matching assets under Assets/.",
                    "assets.query",
                    false,
                    CommandSchema.Object(
                        ("filter", CommandSchema.String("AssetDatabase filter string, for example t:Prefab.")),
                        ("path", CommandSchema.String("Optional folder path under Assets/.")))),
                ListAssets);
            CommandRegistry.Register(
                CommandRegistry.Describe("refresh", "Run AssetDatabase.Refresh().", "assets.mutate", true),
                _ => Refresh());
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "find-assets",
                    "Find assets by type name and optional folder. Returns path + instanceId for each hit.",
                    "assets.query",
                    false,
                    CommandSchema.Object(
                        ("type", CommandSchema.String("Asset type: Prefab/Material/Scene/Texture2D/ScriptableObject or a custom type name.")),
                        ("path", CommandSchema.String("Optional folder scope under Assets/.")),
                        ("name", CommandSchema.String("Optional name substring filter.")))),
                FindAssets);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "create-scriptable-object",
                    "Create a ScriptableObject instance and save it as an asset.",
                    "assets.mutate",
                    true,
                    CommandSchema.Object(
                        ("type", CommandSchema.String("ScriptableObject type name.")),
                        ("path", CommandSchema.String("Asset path, e.g. Assets/Data/NewItem.asset.")))),
                CreateScriptableObject);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "load-asset",
                    "Load an asset by path and return basic metadata.",
                    "assets.query",
                    false,
                    CommandSchema.Object(
                        ("path", CommandSchema.String("Asset path under Assets/.")))),
                LoadAsset);
        }

        // args: {"type":"Prefab","path":"Assets/Prefabs/","name":"Ship"}
        private static JObject FindAssets(JObject args)
        {
            var typeName = args["type"]?.Value<string>();
            var folder = args["path"]?.Value<string>();
            var nameFilter = args["name"]?.Value<string>();

            string filter = !string.IsNullOrEmpty(typeName) ? $"t:{typeName}" : "";
            if (!string.IsNullOrEmpty(nameFilter))
                filter += " " + nameFilter;

            string[] searchPaths = string.IsNullOrEmpty(folder) ? null : new[] { folder };
            var guids = searchPaths != null
                ? AssetDatabase.FindAssets(filter, searchPaths)
                : AssetDatabase.FindAssets(filter);

            var results = new JArray();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                results.Add(new JObject
                {
                    ["path"] = path,
                    ["guid"] = guid,
                    ["name"] = asset != null ? asset.name : System.IO.Path.GetFileNameWithoutExtension(path),
                    ["instanceId"] = asset != null ? asset.GetInstanceID() : 0,
                    ["type"] = asset != null ? asset.GetType().Name : null
                });
            }
            return CommandRegistry.Ok(new JObject
            {
                ["count"] = results.Count,
                ["assets"] = results
            });
        }

        // args: {"type":"ItemConfig","path":"Assets/Data/NewItem.asset"}
        private static JObject CreateScriptableObject(JObject args)
        {
            var typeName = args["type"]?.Value<string>();
            var path = args["path"]?.Value<string>();
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(path))
                return CommandRegistry.Error("Required: 'type', 'path'");
            if (!IsValidAssetPath(path))
                return CommandRegistry.Error("Path must start with 'Assets/' and not contain '..'");

            var type = TypeHelper.FindType(typeName);
            if (type == null)
                return CommandRegistry.Error($"Type not found: {typeName}");
            if (!typeof(ScriptableObject).IsAssignableFrom(type))
                return CommandRegistry.Error($"Type is not a ScriptableObject: {typeName}");

            EnsureDirectoryExists(path);
            var instance = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();

            return CommandRegistry.Ok(new JObject
            {
                ["path"] = path,
                ["instanceId"] = instance.GetInstanceID(),
                ["type"] = type.Name
            });
        }

        // args: {"path":"Assets/Prefabs/Ship.prefab"}
        private static JObject LoadAsset(JObject args)
        {
            var path = args["path"]?.Value<string>();
            if (string.IsNullOrEmpty(path))
                return CommandRegistry.Error("Missing 'path'");

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null)
                return CommandRegistry.Error($"Asset not found: {path}");

            return CommandRegistry.Ok(new JObject
            {
                ["path"] = path,
                ["name"] = asset.name,
                ["instanceId"] = asset.GetInstanceID(),
                ["type"] = asset.GetType().Name
            });
        }

        // args: {"source":"Ship","path":"Assets/Res/Prefabs/Ship.prefab"}
        private static JObject CreatePrefab(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args.ContainsKey("source")
                ? new JObject { ["name"] = args["source"] }
                : args);
            if (go == null) return CommandRegistry.Error(err ?? "Provide 'source' (GameObject name) and 'path'");

            var path = args["path"]?.Value<string>();
            if (string.IsNullOrEmpty(path))
                return CommandRegistry.Error("Missing 'path'");
            if (!IsValidAssetPath(path))
                return CommandRegistry.Error("Path must start with 'Assets/' and not contain '..'");

            EnsureDirectoryExists(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            return prefab != null
                ? CommandRegistry.Ok($"Prefab saved: {path}")
                : CommandRegistry.Error("Failed to save prefab");
        }

        // args: {"path":"Assets/Res/Materials/NewMat.mat","shader":"Universal Render Pipeline/Lit"}
        private static JObject CreateMaterial(JObject args)
        {
            var path = args["path"]?.Value<string>();
            if (string.IsNullOrEmpty(path))
                return CommandRegistry.Error("Missing 'path'");
            if (!IsValidAssetPath(path))
                return CommandRegistry.Error("Path must start with 'Assets/' and not contain '..'");

            var shaderName = args["shader"]?.Value<string>() ?? "Universal Render Pipeline/Lit";
            var shader = Shader.Find(shaderName);
            if (shader == null)
                return CommandRegistry.Error($"Shader not found: {shaderName}");

            EnsureDirectoryExists(path);
            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();

            return CommandRegistry.Ok($"Material created: {path}");
        }

        // args: {"path":"Assets/.../Bounce.physicsMaterial2D","friction":0,"bounciness":1}
        private static JObject CreatePhysicsMaterial2D(JObject args)
        {
            var path = args["path"]?.Value<string>();
            if (string.IsNullOrEmpty(path))
                return CommandRegistry.Error("Missing 'path'");

            EnsureDirectoryExists(path);
            var mat = new PhysicsMaterial2D();
            mat.friction = args["friction"]?.Value<float>() ?? 0f;
            mat.bounciness = args["bounciness"]?.Value<float>() ?? 1f;
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();

            return CommandRegistry.Ok(new JObject
            {
                ["path"] = path,
                ["friction"] = mat.friction,
                ["bounciness"] = mat.bounciness
            });
        }

        // args: {"filter":"t:Prefab","path":"Assets/Res"} or {"filter":"t:Material"} or {"filter":"Mario"}
        private static JObject ListAssets(JObject args)
        {
            var filter = args["filter"]?.Value<string>() ?? "";
            var searchPath = args["path"]?.Value<string>();
            var folders = string.IsNullOrEmpty(searchPath)
                ? new[] { "Assets" }
                : new[] { searchPath };

            var guids = AssetDatabase.FindAssets(filter, folders);
            var results = new JArray();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                results.Add(path);
            }
            return CommandRegistry.Ok(results);
        }

        private static JObject Refresh()
        {
            AssetDatabase.Refresh();
            return CommandRegistry.Ok("AssetDatabase refreshed");
        }

        private static bool IsValidAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var normalized = path.Replace('\\', '/');
            return normalized.StartsWith("Assets/") && !normalized.Contains("..");
        }

        private static void EnsureDirectoryExists(string assetPath)
        {
            if (!IsValidAssetPath(assetPath)) return;
            var dir = System.IO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
        }
    }
}
