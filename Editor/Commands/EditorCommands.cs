using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Tykit
{
    [InitializeOnLoad]
    public static class EditorCommands
    {
        private static readonly ConcurrentQueue<LogEntry> _consoleLogs = new();
        private const int MaxLogs = 200;
        private static int _logCount;

        static EditorCommands()
        {
            CommandRegistry.Register("play", _ => SetPlayMode(true));
            CommandRegistry.Register("stop", _ => SetPlayMode(false));
            CommandRegistry.Register("pause", _ => TogglePause());
            CommandRegistry.Register("save-scene", _ => SaveScene());
            CommandRegistry.Register("console", GetConsole);
            CommandRegistry.Register("clear-console", _ => ClearConsole());
            CommandRegistry.Register("menu", ExecuteMenu);
            CommandRegistry.Register("commands", _ => ListCommands());
            CommandRegistry.Register("status", _ => GetStatus());
            CommandRegistry.Register("undo", _ => PerformUndo());
            CommandRegistry.Register("redo", _ => PerformRedo());
            CommandRegistry.Register("select", SelectGameObject);
            CommandRegistry.Register("get-selection", _ => GetSelection());
            CommandRegistry.Register("compile-status", _ => GetCompileStatus());
            CommandRegistry.Register("open-scene", OpenScene);
            CommandRegistry.Register("new-scene", NewScene);
            CommandRegistry.Register("list-scenes", _ => ListScenes());

            Application.logMessageReceived += OnLogMessage;
        }

        private static JObject SetPlayMode(bool play)
        {
            EditorApplication.isPlaying = play;
            return CommandRegistry.Ok(play ? "Play mode starting" : "Play mode stopping");
        }

        private static JObject TogglePause()
        {
            EditorApplication.isPaused = !EditorApplication.isPaused;
            return CommandRegistry.Ok($"Paused: {EditorApplication.isPaused}");
        }

        private static JObject SaveScene()
        {
            var saved = EditorSceneManager.SaveOpenScenes();
            return saved
                ? CommandRegistry.Ok("Scenes saved")
                : CommandRegistry.Error("Failed to save scenes");
        }

        // args: {"count":50,"filter":"error"}
        private static JObject GetConsole(JObject args)
        {
            int count = args["count"]?.Value<int>() ?? 50;
            var filter = args["filter"]?.Value<string>();

            var snapshot = _consoleLogs.ToArray();
            var start = Math.Max(0, snapshot.Length - count);

            var lines = new JArray();
            for (int i = start; i < snapshot.Length; i++)
            {
                var entry = snapshot[i];
                if (filter != null &&
                    !entry.Type.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !entry.Message.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                lines.Add(new JObject { ["type"] = entry.Type, ["message"] = entry.Message });
            }

            return CommandRegistry.Ok(lines);
        }

        // args: {"item":"Window/General/Console"}
        private static JObject ExecuteMenu(JObject args)
        {
            var item = args["item"]?.Value<string>();
            if (string.IsNullOrEmpty(item))
                return CommandRegistry.Error("Missing 'item' field");

            var result = EditorApplication.ExecuteMenuItem(item);
            return result
                ? CommandRegistry.Ok($"Executed: {item}")
                : CommandRegistry.Error($"Menu item not found or failed: {item}");
        }

        private static JObject ListCommands()
        {
            return CommandRegistry.Ok(new JArray(CommandRegistry.GetCommandNames().OrderBy(n => n).ToArray()));
        }

        private static JObject GetStatus()
        {
            return CommandRegistry.Ok(new JObject
            {
                ["isPlaying"] = EditorApplication.isPlaying,
                ["isPaused"] = EditorApplication.isPaused,
                ["isCompiling"] = EditorApplication.isCompiling,
                ["activeScene"] = SceneManager.GetActiveScene().name,
                ["sceneCount"] = SceneManager.sceneCount,
                ["platform"] = EditorUserBuildSettings.activeBuildTarget.ToString()
            });
        }

        private static JObject PerformUndo()
        {
            Undo.PerformUndo();
            return CommandRegistry.Ok("Undo performed");
        }

        private static JObject PerformRedo()
        {
            Undo.PerformRedo();
            return CommandRegistry.Ok("Redo performed");
        }

        // args: {"id":12345} or {"name":"Ship"}
        private static JObject SelectGameObject(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            return CommandRegistry.Ok(new JObject
            {
                ["name"] = go.name,
                ["instanceId"] = go.GetInstanceID()
            });
        }

        // args: {"path":"Assets/Scenes/MyScene.unity","mode":"single"}
        // mode: "single" (default) or "additive"
        private static JObject OpenScene(JObject args)
        {
            var path = args["path"]?.Value<string>();
            if (string.IsNullOrEmpty(path))
                return CommandRegistry.Error("Missing 'path' field (e.g. Assets/Scenes/MyScene.unity)");

            if (!File.Exists(path))
                return CommandRegistry.Error($"Scene not found: {path}");

            var modeStr = args["mode"]?.Value<string>() ?? "single";
            var mode = modeStr == "additive"
                ? OpenSceneMode.Additive
                : OpenSceneMode.Single;

            if (EditorApplication.isPlaying)
                return CommandRegistry.Error("Cannot open scene while in Play mode");

            var scene = EditorSceneManager.OpenScene(path, mode);
            return CommandRegistry.Ok(new JObject
            {
                ["name"] = scene.name,
                ["path"] = scene.path,
                ["mode"] = modeStr
            });
        }

        // args: {"path":"Assets/Scenes/MyScene.unity"}
        // Creates a new empty scene and saves it to the given path.
        private static JObject NewScene(JObject args)
        {
            var path = args["path"]?.Value<string>();
            if (string.IsNullOrEmpty(path))
                return CommandRegistry.Error("Missing 'path' (e.g. Assets/Scenes/MyScene.unity)");

            if (EditorApplication.isPlaying)
                return CommandRegistry.Error("Cannot create scene while in Play mode");

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);

            return CommandRegistry.Ok(new JObject
            {
                ["name"] = scene.name,
                ["path"] = path
            });
        }

        private static JObject GetSelection()
        {
            var gos = Selection.gameObjects;
            if (gos.Length == 0)
                return CommandRegistry.Ok(new JArray());

            var arr = new JArray();
            foreach (var go in gos)
            {
                arr.Add(new JObject
                {
                    ["name"] = go.name,
                    ["instanceId"] = go.GetInstanceID()
                });
            }
            return CommandRegistry.Ok(arr);
        }

        private static JObject GetCompileStatus()
        {
            return CommandRegistry.Ok(new JObject
            {
                ["isCompiling"] = EditorApplication.isCompiling,
                ["isUpdating"] = EditorApplication.isUpdating
            });
        }

        private static JObject ListScenes()
        {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            var scenes = new JArray();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                scenes.Add(path);
            }
            return CommandRegistry.Ok(scenes);
        }

        private static JObject ClearConsole()
        {
            // Clear tykit internal buffer
            while (_consoleLogs.TryDequeue(out _)) { }
            _logCount = 0;

            // Clear Unity Editor Console panel
            var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
            logEntries?.GetMethod("Clear", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, null);

            return CommandRegistry.Ok("Console cleared");
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            _consoleLogs.Enqueue(new LogEntry { Type = type.ToString(), Message = condition });
            // Trim old entries (approximate — ConcurrentQueue doesn't have RemoveAt)
            if (++_logCount > MaxLogs * 2)
            {
                while (_consoleLogs.Count > MaxLogs)
                    _consoleLogs.TryDequeue(out _);
                _logCount = _consoleLogs.Count;
            }
        }

        private class LogEntry
        {
            public string Type;
            public string Message;
        }
    }
}
