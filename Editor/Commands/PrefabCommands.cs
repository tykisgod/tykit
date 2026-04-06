// Commands/PrefabCommands.cs
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Tykit
{
    [InitializeOnLoad]
    public static class PrefabCommands
    {
        static PrefabCommands()
        {
            RegisterCommands();
        }

        public static void RegisterCommands()
        {
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "prefab-apply",
                    "Apply all changes from a prefab instance in the scene back to its source prefab asset.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("Prefab instance GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")))),
                ApplyPrefab);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "prefab-revert",
                    "Revert all changes on a prefab instance to the source prefab state.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("Prefab instance GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")))),
                RevertPrefab);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "prefab-open",
                    "Open a prefab asset in prefab edit mode. Subsequent scene queries operate on the prefab contents.",
                    "scene.control",
                    true,
                    CommandSchema.Object(
                        ("assetPath", CommandSchema.String("Prefab asset path, e.g. Assets/Prefabs/Ship.prefab.")))),
                OpenPrefab);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "prefab-close",
                    "Exit prefab edit mode and return to the main scene.",
                    "scene.control",
                    true),
                _ => ClosePrefab());
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "prefab-source",
                    "Get the source prefab asset path for a prefab instance.",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("Prefab instance GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")))),
                GetPrefabSource);
        }

        private static JObject ApplyPrefab(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return CommandRegistry.Error($"{go.name} is not a prefab instance");

            var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(sourcePath))
                return CommandRegistry.Error("Cannot resolve source prefab path");

            try
            {
                PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
                return CommandRegistry.Ok(new JObject
                {
                    ["applied"] = go.name,
                    ["sourcePath"] = sourcePath
                });
            }
            catch (Exception e)
            {
                return CommandRegistry.Error($"Apply failed: {e.Message}");
            }
        }

        private static JObject RevertPrefab(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return CommandRegistry.Error($"{go.name} is not a prefab instance");

            try
            {
                PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);
                return CommandRegistry.Ok($"Reverted {go.name}");
            }
            catch (Exception e)
            {
                return CommandRegistry.Error($"Revert failed: {e.Message}");
            }
        }

        private static JObject OpenPrefab(JObject args)
        {
            var assetPath = args["assetPath"]?.Value<string>();
            if (string.IsNullOrEmpty(assetPath))
                return CommandRegistry.Error("Missing 'assetPath'");

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
                return CommandRegistry.Error($"Prefab not found: {assetPath}");

            AssetDatabase.OpenAsset(asset);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return CommandRegistry.Error("Failed to enter prefab mode");

            return CommandRegistry.Ok(new JObject
            {
                ["assetPath"] = stage.assetPath,
                ["rootInstanceId"] = stage.prefabContentsRoot.GetInstanceID()
            });
        }

        private static JObject ClosePrefab()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return CommandRegistry.Error("Not in prefab edit mode");

            // Stage closes by opening the main scene / returning via StageUtility
            UnityEditor.SceneManagement.StageUtility.GoToMainStage();
            return CommandRegistry.Ok("Exited prefab mode");
        }

        private static JObject GetPrefabSource(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return CommandRegistry.Ok(new JObject { ["isPrefabInstance"] = false });

            var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return CommandRegistry.Ok(new JObject
            {
                ["isPrefabInstance"] = true,
                ["sourcePath"] = sourcePath
            });
        }
    }
}
