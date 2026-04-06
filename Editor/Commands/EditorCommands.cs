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
            RegisterCommands();
            Application.logMessageReceived += OnLogMessage;
        }

        public static void RegisterCommands()
        {
            CommandRegistry.Register(
                CommandRegistry.Describe("play", "Enter Play Mode.", "editor.control", true),
                _ => SetPlayMode(true));
            CommandRegistry.Register(
                CommandRegistry.Describe("stop", "Exit Play Mode.", "editor.control", true),
                _ => SetPlayMode(false));
            CommandRegistry.Register(
                CommandRegistry.Describe("pause", "Toggle Play Mode pause state.", "editor.control", true),
                _ => TogglePause());
            CommandRegistry.Register(
                CommandRegistry.Describe("save-scene", "Save all open scenes.", "scene.control", true),
                _ => SaveScene());
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "console",
                    "Read recent console entries from tykit's internal log buffer.",
                    "editor.console",
                    false,
                    CommandSchema.Object(
                        ("count", CommandSchema.Integer("Maximum number of entries to return.")),
                        ("filter", CommandSchema.String("Optional substring filter for log type or message.")))),
                GetConsole);
            CommandRegistry.Register(
                CommandRegistry.Describe("clear-console", "Clear the Unity Console and tykit log buffer.", "editor.console", true),
                _ => ClearConsole());
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "menu",
                    "Execute a Unity editor menu item.",
                    "editor.control",
                    true,
                    CommandSchema.Object(
                        ("item", CommandSchema.String("Menu path, for example Window/General/Console")))),
                ExecuteMenu);
            CommandRegistry.Register(
                CommandRegistry.Describe("commands", "List registered tykit command names.", "meta.query", false),
                _ => ListCommands());
            CommandRegistry.Register(
                CommandRegistry.Describe("describe-commands", "List tykit command metadata descriptors.", "meta.query", false),
                _ => CommandRegistry.DescribeCommands());
            CommandRegistry.Register(
                CommandRegistry.Describe("status", "Read current editor status, scene, and platform.", "editor.state", false),
                _ => GetStatus());
            CommandRegistry.Register(
                CommandRegistry.Describe("undo", "Perform one editor undo step.", "editor.control", true),
                _ => PerformUndo());
            CommandRegistry.Register(
                CommandRegistry.Describe("redo", "Perform one editor redo step.", "editor.control", true),
                _ => PerformRedo());
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "select",
                    "Select one or more GameObjects by instanceId, path, or name. Pings by default; pass ping:false to skip highlight.",
                    "selection",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("ids", CommandSchema.Array(CommandSchema.Integer(), "Array of GameObject instanceIds for multi-select.")),
                        ("path", CommandSchema.String("Hierarchy path, for example World/Ship.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("ping", CommandSchema.Boolean("Whether to ping (highlight) in editor. Default true.")))),
                SelectGameObject);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "ping",
                    "Highlight an object in the editor without changing selection. Works for GameObjects (id/path/name) or assets (assetPath).",
                    "selection",
                    false,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject or asset instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("assetPath", CommandSchema.String("Asset path, e.g. Assets/Prefabs/Ship.prefab.")))),
                PingObject);
            CommandRegistry.Register(
                CommandRegistry.Describe("get-selection", "Read the current editor selection.", "selection", false),
                _ => GetSelection());
            CommandRegistry.Register(
                CommandRegistry.Describe("compile-status", "Read current Unity compilation and asset update state.", "editor.state", false),
                _ => GetCompileStatus());
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "open-scene",
                    "Open a scene asset.",
                    "scene.control",
                    true,
                    CommandSchema.Object(
                        ("path", CommandSchema.String("Scene asset path, for example Assets/Scenes/Main.unity.")),
                        ("mode", CommandSchema.String("Open mode.", "single", "additive")))),
                OpenScene);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "new-scene",
                    "Create a new scene and save it to the given asset path.",
                    "scene.control",
                    true,
                    CommandSchema.Object(
                        ("path", CommandSchema.String("Target scene asset path.")))),
                NewScene);
            CommandRegistry.Register(
                CommandRegistry.Describe("list-scenes", "List all scene assets under Assets/.", "scene.query", false),
                _ => ListScenes());
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "save-scene-as",
                    "Save the active scene to a new asset path.",
                    "scene.control",
                    true,
                    CommandSchema.Object(
                        ("path", CommandSchema.String("Target scene asset path.")))),
                SaveSceneAs);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "set-active-scene",
                    "Set the active scene by path or name when multiple scenes are loaded.",
                    "scene.control",
                    true,
                    CommandSchema.Object(
                        ("path", CommandSchema.String("Scene asset path.")),
                        ("name", CommandSchema.String("Scene name (without extension).")))),
                SetActiveScene);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "dismiss-dialog",
                    "Attempt to dismiss a modal dialog blocking Unity's main thread (Windows only). Best-effort; returns what it tried.",
                    "editor.control",
                    true),
                _ => DismissDialog());
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "batch",
                    "Execute multiple commands in one request. Returns an array of results in order.",
                    "meta.control",
                    true,
                    CommandSchema.Object(
                        ("commands", CommandSchema.Array(new JObject(), "Array of {command, args} objects.")),
                        ("stopOnError", CommandSchema.Boolean("If true, abort the batch on first error. Default false.")))),
                BatchExecute);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "editor-prefs",
                    "Read or write an EditorPrefs value. Omit 'value' to read; include it to write.",
                    "editor.prefs",
                    true,
                    CommandSchema.Object(
                        ("key", CommandSchema.String("EditorPrefs key.")),
                        ("value", new JObject { ["description"] = "Optional value (string/int/float/bool). Omit to read." }),
                        ("type", CommandSchema.String("When reading: 'string'/'int'/'float'/'bool'. Default 'string'.")),
                        ("delete", CommandSchema.Boolean("If true, delete the key.")))),
                EditorPrefsCmd);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "player-prefs",
                    "Read or write a PlayerPrefs value. Omit 'value' to read; include it to write.",
                    "editor.prefs",
                    true,
                    CommandSchema.Object(
                        ("key", CommandSchema.String("PlayerPrefs key.")),
                        ("value", new JObject { ["description"] = "Optional value (string/int/float). Omit to read." }),
                        ("type", CommandSchema.String("When reading: 'string'/'int'/'float'. Default 'string'.")),
                        ("delete", CommandSchema.Boolean("If true, delete the key.")))),
                PlayerPrefsCmd);
        }

        private static JObject SetPlayMode(bool play)
        {
            // Auto-save dirty scenes before entering play mode to prevent
            // the "Save modified scenes?" modal dialog from blocking the main thread.
            if (play)
            {
                try
                {
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (scene.isLoaded && scene.isDirty && !string.IsNullOrEmpty(scene.path))
                            EditorSceneManager.SaveScene(scene);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[tykit] Auto-save before play failed: {e.Message}");
                }
            }

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
            bool doPing = args["ping"]?.Value<bool>() ?? true;

            // Multi-select via ids array
            var idsArr = args["ids"] as JArray;
            if (idsArr != null && idsArr.Count > 0)
            {
                var selected = new System.Collections.Generic.List<GameObject>();
                foreach (var token in idsArr)
                {
                    var obj = EditorUtility.InstanceIDToObject(token.Value<int>()) as GameObject;
                    if (obj != null) selected.Add(obj);
                }
                if (selected.Count == 0)
                    return CommandRegistry.Error("No valid GameObjects found for given ids");

                Selection.objects = selected.ToArray();
                if (doPing && selected[0] != null)
                    EditorGUIUtility.PingObject(selected[0]);

                var names = new JArray();
                foreach (var g in selected) names.Add(g.name);
                return CommandRegistry.Ok(new JObject
                {
                    ["count"] = selected.Count,
                    ["names"] = names
                });
            }

            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            Selection.activeGameObject = go;
            if (doPing) EditorGUIUtility.PingObject(go);
            return CommandRegistry.Ok(new JObject
            {
                ["name"] = go.name,
                ["instanceId"] = go.GetInstanceID()
            });
        }

        // args: {"id":12345} or {"path":"World/Ship"} or {"assetPath":"Assets/Prefabs/Ship.prefab"}
        private static JObject PingObject(JObject args)
        {
            // Asset path first
            var assetPath = args["assetPath"]?.Value<string>();
            if (!string.IsNullOrEmpty(assetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null) return CommandRegistry.Error($"Asset not found: {assetPath}");
                EditorGUIUtility.PingObject(asset);
                return CommandRegistry.Ok($"Pinged asset: {assetPath}");
            }

            // GameObject by id/path/name
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            EditorGUIUtility.PingObject(go);
            return CommandRegistry.Ok($"Pinged: {go.name}");
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

            // Auto-save dirty scenes before switching to prevent "Save modified scene?" modal.
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s.isLoaded && s.isDirty && !string.IsNullOrEmpty(s.path))
                        EditorSceneManager.SaveScene(s);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[tykit] Auto-save before open-scene failed: {e.Message}");
            }

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

        // args: {"path":"Assets/Scenes/NewScene.unity"}
        private static JObject SaveSceneAs(JObject args)
        {
            var path = args["path"]?.Value<string>();
            if (string.IsNullOrEmpty(path))
                return CommandRegistry.Error("Missing 'path' field");

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return CommandRegistry.Error("No active scene to save");

            bool ok = EditorSceneManager.SaveScene(scene, path, false);
            return ok
                ? CommandRegistry.Ok(new JObject { ["path"] = path })
                : CommandRegistry.Error("Save failed");
        }

        // args: {"path":"Assets/Scenes/Foo.unity"} or {"name":"Foo"}
        private static JObject SetActiveScene(JObject args)
        {
            var path = args["path"]?.Value<string>();
            var name = args["name"]?.Value<string>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if ((!string.IsNullOrEmpty(path) && s.path == path) ||
                    (!string.IsNullOrEmpty(name) && s.name == name))
                {
                    SceneManager.SetActiveScene(s);
                    return CommandRegistry.Ok(new JObject { ["name"] = s.name, ["path"] = s.path });
                }
            }
            return CommandRegistry.Error("Scene not found among loaded scenes");
        }

        // Best-effort modal dialog dismissal (Windows only).
        // Finds the foreground dialog owned by the Unity process and sends WM_CLOSE.
        private static JObject DismissDialog()
        {
#if UNITY_EDITOR_WIN
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return CommandRegistry.Error("No foreground window");

                GetWindowThreadProcessId(hwnd, out uint pid);
                int unityPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                if (pid != unityPid)
                    return CommandRegistry.Error($"Foreground window belongs to pid {pid}, not Unity ({unityPid})");

                // WM_CLOSE = 0x0010
                const uint WM_CLOSE = 0x0010;
                PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                return CommandRegistry.Ok($"Sent WM_CLOSE to hwnd {hwnd.ToInt64():X}");
            }
            catch (Exception e)
            {
                return CommandRegistry.Error($"dismiss-dialog failed: {e.Message}");
            }
#else
            return CommandRegistry.Error("dismiss-dialog is Windows-only");
#endif
        }

#if UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
#endif

        // args: {"commands":[{"command":"find","args":{...}},{"command":"...","args":{...}}], "stopOnError":false}
        private static JObject BatchExecute(JObject args)
        {
            var cmds = args["commands"] as JArray;
            if (cmds == null)
                return CommandRegistry.Error("Missing 'commands' array");

            bool stopOnError = args["stopOnError"]?.Value<bool>() ?? false;
            var results = new JArray();

            foreach (var cmdToken in cmds)
            {
                if (!(cmdToken is JObject cmdObj))
                {
                    results.Add(CommandRegistry.Error("Each batch item must be an object"));
                    if (stopOnError) break;
                    continue;
                }

                try
                {
                    var result = CommandRegistry.Execute(cmdObj.ToString());
                    results.Add(result);
                    if (stopOnError && result["success"]?.Value<bool>() != true)
                        break;
                }
                catch (Exception e)
                {
                    results.Add(CommandRegistry.Error($"Batch item failed: {e.Message}"));
                    if (stopOnError) break;
                }
            }

            return CommandRegistry.Ok(results);
        }

        // args: {"key":"Foo.Bar"} (read) or {"key":"Foo.Bar","value":"..."} (write) or {"key":"Foo.Bar","delete":true}
        private static JObject EditorPrefsCmd(JObject args)
        {
            var key = args["key"]?.Value<string>();
            if (string.IsNullOrEmpty(key)) return CommandRegistry.Error("Missing 'key'");

            if (args["delete"]?.Value<bool>() == true)
            {
                EditorPrefs.DeleteKey(key);
                return CommandRegistry.Ok($"Deleted {key}");
            }

            var valueToken = args["value"];
            if (valueToken != null)
            {
                switch (valueToken.Type)
                {
                    case JTokenType.Integer: EditorPrefs.SetInt(key, valueToken.Value<int>()); break;
                    case JTokenType.Float: EditorPrefs.SetFloat(key, valueToken.Value<float>()); break;
                    case JTokenType.Boolean: EditorPrefs.SetBool(key, valueToken.Value<bool>()); break;
                    default: EditorPrefs.SetString(key, valueToken.Value<string>()); break;
                }
                return CommandRegistry.Ok($"Set {key}");
            }

            var type = args["type"]?.Value<string>() ?? "string";
            object value = type switch
            {
                "int" => (object)EditorPrefs.GetInt(key),
                "float" => EditorPrefs.GetFloat(key),
                "bool" => EditorPrefs.GetBool(key),
                _ => EditorPrefs.GetString(key)
            };
            return CommandRegistry.Ok(new JObject { ["key"] = key, ["value"] = JToken.FromObject(value) });
        }

        private static JObject PlayerPrefsCmd(JObject args)
        {
            var key = args["key"]?.Value<string>();
            if (string.IsNullOrEmpty(key)) return CommandRegistry.Error("Missing 'key'");

            if (args["delete"]?.Value<bool>() == true)
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                return CommandRegistry.Ok($"Deleted {key}");
            }

            var valueToken = args["value"];
            if (valueToken != null)
            {
                switch (valueToken.Type)
                {
                    case JTokenType.Integer: PlayerPrefs.SetInt(key, valueToken.Value<int>()); break;
                    case JTokenType.Float: PlayerPrefs.SetFloat(key, valueToken.Value<float>()); break;
                    default: PlayerPrefs.SetString(key, valueToken.Value<string>()); break;
                }
                PlayerPrefs.Save();
                return CommandRegistry.Ok($"Set {key}");
            }

            var type = args["type"]?.Value<string>() ?? "string";
            object value = type switch
            {
                "int" => (object)PlayerPrefs.GetInt(key),
                "float" => PlayerPrefs.GetFloat(key),
                _ => PlayerPrefs.GetString(key)
            };
            return CommandRegistry.Ok(new JObject { ["key"] = key, ["value"] = JToken.FromObject(value) });
        }

        private class LogEntry
        {
            public string Type;
            public string Message;
        }
    }
}
