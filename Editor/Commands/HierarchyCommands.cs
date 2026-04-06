// Commands/HierarchyCommands.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace Tykit
{
    [InitializeOnLoad]
    public static class HierarchyCommands
    {
        static HierarchyCommands()
        {
            RegisterCommands();
        }

        public static void RegisterCommands()
        {
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "hierarchy",
                    "Read the scene hierarchy as a flattened tree. Provide 'id'/'path'/'name' to start from a specific GameObject; omit to start from active scene roots.",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("Optional root GameObject instanceId.")),
                        ("path", CommandSchema.String("Optional root hierarchy path.")),
                        ("name", CommandSchema.String("Optional root GameObject name.")),
                        ("depth", CommandSchema.Integer("Maximum traversal depth. Default is 3.")))),
                GetHierarchy);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "find",
                    "Find GameObjects by name, tag, type, or hierarchy path. Supports parent scoping and inactive objects.",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("name", CommandSchema.String("Exact GameObject name.")),
                        ("tag", CommandSchema.String("Unity tag.")),
                        ("type", CommandSchema.String("Component or GameObject type name.")),
                        ("path", CommandSchema.String("Exact hierarchy path, e.g. ===UI===/Canvas/Panel/Button.")),
                        ("parentId", CommandSchema.Integer("Limit search to children of the given GameObject id.")),
                        ("includeInactive", CommandSchema.Boolean("Include inactive GameObjects. Default false.")))),
                FindGameObjects);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "inspect",
                    "Inspect a GameObject and return basic transform and component information.",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")))),
                InspectGameObject);
        }

        // args: {"depth":3} for scene roots, or {"id":12345,"depth":3} for a subtree.
        private static JObject GetHierarchy(JObject args)
        {
            int maxDepth = args["depth"]?.Value<int>() ?? 3;
            var arr = new JArray();

            // If id/name/path provided, start from that GameObject's subtree.
            if (args["id"] != null || args["name"] != null || args["path"] != null)
            {
                var (root, err) = CommandRegistry.ResolveGameObject(args);
                if (root == null) return CommandRegistry.Error(err);
                BuildHierarchy(root.transform, 0, maxDepth, arr);
                return CommandRegistry.Ok(arr);
            }

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
                BuildHierarchy(root.transform, 0, maxDepth, arr);

            return CommandRegistry.Ok(arr);
        }

        private static void BuildHierarchy(Transform t, int depth, int maxDepth, JArray arr)
        {
            if (depth >= maxDepth) return;

            var node = new JObject
            {
                ["name"] = t.name,
                ["instanceId"] = t.gameObject.GetInstanceID(),
                ["active"] = t.gameObject.activeSelf,
                ["depth"] = depth
            };
            arr.Add(node);

            for (int i = 0; i < t.childCount; i++)
                BuildHierarchy(t.GetChild(i), depth + 1, maxDepth, arr);
        }

        // args: {"name":"Ship"} or {"tag":"Player"} or {"type":"Camera"} or {"path":"World/Ship"}
        //   Optional: parentId (limit to subtree), includeInactive
        private static JObject FindGameObjects(JObject args)
        {
            var results = new JArray();

            var name = args["name"]?.Value<string>();
            var tag = args["tag"]?.Value<string>();
            var typeName = args["type"]?.Value<string>();
            var pathExact = args["path"]?.Value<string>();
            var parentId = args["parentId"]?.Value<int?>();
            bool includeInactive = args["includeInactive"]?.Value<bool>() ?? false;

            // Resolve parent scope if given
            Transform scopeRoot = null;
            if (parentId.HasValue)
            {
                var parentObj = EditorUtility.InstanceIDToObject(parentId.Value) as GameObject;
                if (parentObj == null)
                    return CommandRegistry.Error($"parentId not found: {parentId}");
                scopeRoot = parentObj.transform;
            }

            // Path exact match (full hierarchy path)
            if (!string.IsNullOrEmpty(pathExact))
            {
                foreach (var go in EnumerateGameObjects(scopeRoot, includeInactive))
                {
                    if (GetPath(go.transform) == pathExact)
                        results.Add(FormatGameObject(go));
                }
                return CommandRegistry.Ok(results);
            }

            if (!string.IsNullOrEmpty(name))
            {
                if (scopeRoot != null || includeInactive)
                {
                    foreach (var go in EnumerateGameObjects(scopeRoot, includeInactive))
                    {
                        if (go.name == name)
                            results.Add(FormatGameObject(go));
                    }
                }
                else
                {
                    var go = GameObject.Find(name);
                    if (go != null) results.Add(FormatGameObject(go));
                }
            }
            else if (!string.IsNullOrEmpty(tag))
            {
                try
                {
                    foreach (var go in GameObject.FindGameObjectsWithTag(tag))
                    {
                        if (scopeRoot != null && !IsDescendant(go.transform, scopeRoot)) continue;
                        if (!includeInactive && !go.activeInHierarchy) continue;
                        results.Add(FormatGameObject(go));
                    }
                }
                catch (UnityException e)
                {
                    return CommandRegistry.Error($"Invalid tag: {e.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(typeName))
            {
                var type = TypeHelper.FindType(typeName);
                if (type == null)
                    return CommandRegistry.Error($"Type not found: {typeName}");

                var inactiveMode = includeInactive
                    ? FindObjectsInactive.Include
                    : FindObjectsInactive.Exclude;

                foreach (var obj in UnityEngine.Object.FindObjectsByType(type, inactiveMode, FindObjectsSortMode.None))
                {
                    GameObject go = null;
                    if (obj is Component c) go = c.gameObject;
                    else if (obj is GameObject g) go = g;
                    if (go == null) continue;

                    if (scopeRoot != null && !IsDescendant(go.transform, scopeRoot)) continue;
                    results.Add(FormatGameObject(go));
                }
            }
            else
            {
                return CommandRegistry.Error("Provide 'name', 'tag', 'type', or 'path'");
            }

            return CommandRegistry.Ok(results);
        }

        private static System.Collections.Generic.IEnumerable<GameObject> EnumerateGameObjects(
            Transform scopeRoot, bool includeInactive)
        {
            if (scopeRoot != null)
            {
                // Walk subtree
                var stack = new System.Collections.Generic.Stack<Transform>();
                stack.Push(scopeRoot);
                while (stack.Count > 0)
                {
                    var t = stack.Pop();
                    if (includeInactive || t.gameObject.activeInHierarchy)
                        yield return t.gameObject;
                    for (int i = 0; i < t.childCount; i++)
                        stack.Push(t.GetChild(i));
                }
                yield break;
            }

            // All scene GameObjects
            var inactiveMode = includeInactive
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(inactiveMode, FindObjectsSortMode.None))
                yield return go;
        }

        private static bool IsDescendant(Transform child, Transform ancestor)
        {
            var t = child;
            while (t != null)
            {
                if (t == ancestor) return true;
                t = t.parent;
            }
            return false;
        }

        // args: {"id":12345} or {"name":"Ship"}
        private static JObject InspectGameObject(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            var components = new JArray();
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                components.Add(comp.GetType().Name);
            }

            var children = new JArray();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                children.Add(new JObject
                {
                    ["name"] = child.name,
                    ["instanceId"] = child.GetInstanceID(),
                    ["active"] = child.activeSelf
                });
            }

            var data = new JObject
            {
                ["name"] = go.name,
                ["instanceId"] = go.GetInstanceID(),
                ["active"] = go.activeSelf,
                ["layer"] = LayerMask.LayerToName(go.layer),
                ["tag"] = go.tag,
                ["position"] = Vec3ToJson(go.transform.position),
                ["rotation"] = Vec3ToJson(go.transform.eulerAngles),
                ["scale"] = Vec3ToJson(go.transform.localScale),
                ["childCount"] = go.transform.childCount,
                ["children"] = children,
                ["components"] = components
            };

            return CommandRegistry.Ok(data);
        }

        private static JObject FormatGameObject(GameObject go)
        {
            var components = new JArray();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType() != typeof(Transform))
                    components.Add(c.GetType().Name);
            }

            return new JObject
            {
                ["name"] = go.name,
                ["instanceId"] = go.GetInstanceID(),
                ["path"] = GetPath(go.transform),
                ["components"] = components
            };
        }

        private static string GetPath(Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private static JArray Vec3ToJson(Vector3 v)
        {
            return new JArray(v.x, v.y, v.z);
        }
    }
}
