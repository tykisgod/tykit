using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Tykit
{
    public static class CommandRegistry
    {
        private static readonly Dictionary<string, Func<JObject, JObject>> _commands = new();

        public static void Register(string name, Func<JObject, JObject> handler)
        {
            _commands[name] = handler;
        }

        public static bool HasCommand(string name) => _commands.ContainsKey(name);

        public static IEnumerable<string> GetCommandNames() => _commands.Keys;

        public static void ClearAll() => _commands.Clear();

        public static JObject Execute(string requestBody)
        {
            JObject request;
            try
            {
                request = JObject.Parse(requestBody);
            }
            catch (Exception e)
            {
                return Error($"Invalid JSON: {e.Message}");
            }

            var command = request["command"]?.Value<string>();
            if (string.IsNullOrEmpty(command))
                return Error("Missing 'command' field");

            if (command == "batch")
                return ExecuteBatch(request);

            if (!_commands.TryGetValue(command, out var handler))
                return Error($"Unknown command: {command}. Available: {string.Join(", ", _commands.Keys)}");

            var args = request["args"] as JObject ?? new JObject();

            try
            {
                return handler(args);
            }
            catch (Exception e)
            {
                return Error(e.ToString());
            }
        }

        private static JObject ExecuteBatch(JObject request)
        {
            var commands = request["args"]?["commands"] as JArray;
            if (commands == null || commands.Count == 0)
                return Error("batch requires non-empty args.commands array");

            var results = new JArray();
            var instanceIds = new List<int>();

            foreach (var cmd in commands)
            {
                var cmdObj = cmd as JObject;
                if (cmdObj == null)
                {
                    results.Add(Error("Invalid command entry"));
                    instanceIds.Add(0);
                    continue;
                }

                var name = cmdObj["command"]?.Value<string>();
                if (string.IsNullOrEmpty(name) || !_commands.TryGetValue(name, out var handler))
                {
                    results.Add(Error($"Unknown command: {name}"));
                    instanceIds.Add(0);
                    continue;
                }

                var args = cmdObj["args"] as JObject ?? new JObject();
                SubstituteRefs(args, instanceIds);

                JObject result;
                try
                {
                    result = handler(args);
                }
                catch (Exception e)
                {
                    result = Error(e.ToString());
                }

                results.Add(result);
                var data = result["data"] as JObject;
                instanceIds.Add(data?["instanceId"]?.Value<int>() ?? 0);
            }

            return Ok(results);
        }

        private static void SubstituteRefs(JObject args, List<int> instanceIds)
        {
            var keys = args.Properties().Select(p => p.Name).ToList();
            foreach (var key in keys)
            {
                if (args[key] is JValue jv && jv.Type == JTokenType.String)
                {
                    var str = jv.Value<string>();
                    if (str.StartsWith("$") && int.TryParse(str.Substring(1), out var refIdx)
                        && refIdx >= 0 && refIdx < instanceIds.Count)
                    {
                        args[key] = instanceIds[refIdx];
                    }
                }
            }
        }

        // --- Public helpers for command implementations ---

        public static JObject Ok(JToken data = null)
        {
            return new JObject { ["success"] = true, ["data"] = data, ["error"] = null };
        }

        public static JObject Error(string message)
        {
            return new JObject { ["success"] = false, ["data"] = null, ["error"] = message };
        }

        /// <summary>
        /// Find a GameObject by instanceId, path, or name.
        /// </summary>
        public static (GameObject go, string error) ResolveGameObject(JObject args)
        {
            if (args["id"] != null)
            {
                var id = args["id"].Value<int>();
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(id) as GameObject;
                if (obj == null)
                    return (null, $"No GameObject with instanceId {id}");
                return (obj, null);
            }

            var path = args["path"]?.Value<string>();
            var name = args["name"]?.Value<string>();
            var query = !string.IsNullOrEmpty(path) ? path : name;

            if (string.IsNullOrEmpty(query))
                return (null, "Provide 'id', 'path', or 'name' to identify the GameObject");

            var go = GameObject.Find(query);
            if (go == null)
                return (null, $"GameObject not found: {query}");

            return (go, null);
        }
    }

    /// <summary>
    /// Find Type by short name across loaded assemblies.
    /// </summary>
    public static class TypeHelper
    {
        public static Type FindType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == typeName)
                            return t;
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException) { }
            }
            return null;
        }
    }
}
