// TykitServer.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Tykit
{
    [InitializeOnLoad]
    public static class TykitServer
    {
        private const int PortRangeBase = 8090;
        private const int PortRangeSize = 64; // 8090-8153

        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static int _activePort;
        private static readonly ConcurrentQueue<RequestContext> _requestQueue = new();

        // Heartbeat — updated by ProcessQueue on main thread. Listener thread reads it
        // to detect when the main thread is blocked (e.g. by a modal dialog).
        private static long _lastProcessTicks = DateTime.UtcNow.Ticks;

        private static readonly string ServerInfoFile = Path.Combine(
            Application.dataPath, "..", "Temp", "tykit.json");

        static TykitServer()
        {
            TykitBootstrap.EnsureCommandsRegistered();
            Start();
            EditorApplication.update += ProcessQueue;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.quitting += OnQuitting;
        }

        private static void Start()
        {
            // Primary port: deterministic hash of project path
            // Fallback: try nearby ports if primary is occupied
            int preferredPort = GetPreferredPort();
            int maxPort = PortRangeBase + PortRangeSize - 1;

            // Try preferred port first, then scan from there
            int[] portsToTry = new int[PortRangeSize];
            portsToTry[0] = preferredPort;
            int idx = 1;
            for (int p = PortRangeBase; p <= maxPort && idx < PortRangeSize; p++)
            {
                if (p != preferredPort)
                    portsToTry[idx++] = p;
            }

            for (int i = 0; i < portsToTry.Length; i++)
            {
                int port = portsToTry[i];
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{port}/");
                    _listener.Start();
                    _activePort = port;

                    _listenerThread = new Thread(ListenLoop) { IsBackground = true };
                    _listenerThread.Start();

                    WriteServerInfo();
                    string hint = port == preferredPort ? "preferred" : $"fallback, preferred was {preferredPort}";
                    Debug.Log($"[tykit] Listening on http://localhost:{port}/ ({hint})");
                    return;
                }
                catch (Exception)
                {
                    _listener?.Close();
                }
            }
            Debug.LogError($"[tykit] Failed to start: all ports {PortRangeBase}-{maxPort} in use");
        }

        /// <summary>
        /// Deterministic port from project path hash.
        /// Same algorithm in tykit_mcp.py — keep in sync.
        /// </summary>
        private static int GetPreferredPort()
        {
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            uint hash = StableHash(projectPath);
            return PortRangeBase + (int)(hash % (uint)PortRangeSize);
        }

        /// <summary>
        /// FNV-1a 32-bit hash — simple, deterministic, no crypto dependency.
        /// </summary>
        private static uint StableHash(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s)
            {
                h ^= c;
                h *= 16777619u;
            }
            return h;
        }

        private static void OnBeforeReload()
        {
            // Drain queue — respond with error to in-flight requests
            while (_requestQueue.TryDequeue(out var rc))
            {
                Respond(rc.HttpContext, 503,
                    new JObject { ["success"] = false, ["error"] = "Domain reload in progress" }.ToString());
            }
            StopListener();
        }

        private static void OnQuitting()
        {
            StopListener();
            try { File.Delete(ServerInfoFile); } catch { }
        }

        private static void StopListener()
        {
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
            _listener = null;
        }

        private static void ListenLoop()
        {
            while (_listener != null && _listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    string body;
                    using (var reader = new StreamReader(ctx.Request.InputStream))
                        body = reader.ReadToEnd();

                    // /ping responds immediately (no Unity API needed)
                    if (ctx.Request.Url.AbsolutePath == "/ping")
                    {
                        var ping = new JObject
                        {
                            ["status"] = "ok",
                            ["port"] = _activePort,
                            ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
                            ["project"] = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                        };
                        Respond(ctx, 200, ping.ToString());
                        continue;
                    }

                    // /focus-unity — listener-thread recovery: bring Unity's main window to foreground.
                    // Use when Unity has background-throttled domain reload or asset import.
                    // Windows only. Must run on listener thread because main thread may be stalled.
                    if (ctx.Request.Url.AbsolutePath == "/focus-unity")
                    {
                        var focusResult = FocusUnityWindow();
                        Respond(ctx, 200, focusResult.ToString());
                        continue;
                    }

                    // /dismiss-dialog — listener-thread recovery: WM_CLOSE to foreground dialog.
                    // Windows only. Must run on listener thread because main thread may be blocked by the dialog itself.
                    if (ctx.Request.Url.AbsolutePath == "/dismiss-dialog")
                    {
                        var dismissResult = DismissForegroundDialog();
                        Respond(ctx, 200, dismissResult.ToString());
                        continue;
                    }

                    // /health — listener-thread diagnostic: queue state + main thread heartbeat.
                    // Use this when POST commands time out to detect blocked main thread (modal dialogs).
                    if (ctx.Request.Url.AbsolutePath == "/health")
                    {
                        double sinceProcessSec = (DateTime.UtcNow - new DateTime(_lastProcessTicks, DateTimeKind.Utc)).TotalSeconds;
                        int queueDepth = _requestQueue.Count;
                        double oldestQueuedSec = 0;
                        if (_requestQueue.TryPeek(out var oldest))
                            oldestQueuedSec = (DateTime.UtcNow - oldest.EnqueueTime).TotalSeconds;

                        // Heuristic: main thread considered "blocked" if > 5s since last process AND queue non-empty
                        bool mainThreadBlocked = sinceProcessSec > 5 && queueDepth > 0;

                        var health = new JObject
                        {
                            ["status"] = mainThreadBlocked ? "blocked" : "ok",
                            ["port"] = _activePort,
                            ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
                            ["queueDepth"] = queueDepth,
                            ["oldestQueuedSec"] = Math.Round(oldestQueuedSec, 2),
                            ["mainThreadIdleSec"] = Math.Round(sinceProcessSec, 2),
                            ["mainThreadBlocked"] = mainThreadBlocked,
                            ["hint"] = mainThreadBlocked
                                ? "Main thread not processing. Check Unity window for modal dialog (save/compile error/import), or call dismiss-dialog."
                                : "OK"
                        };
                        Respond(ctx, 200, health.ToString());
                        continue;
                    }

                    _requestQueue.Enqueue(new RequestContext
                    {
                        HttpContext = ctx,
                        Body = body,
                        EnqueueTime = DateTime.UtcNow
                    });
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e)
                {
                    Debug.LogWarning($"[tykit] Listener error: {e.Message}");
                }
            }
        }

        private static void ProcessQueue()
        {
            // Heartbeat for /health endpoint (listener thread reads this to detect blocked main thread)
            Interlocked.Exchange(ref _lastProcessTicks, DateTime.UtcNow.Ticks);

            while (_requestQueue.TryDequeue(out var rc))
            {
                // Timeout stale requests
                if ((DateTime.UtcNow - rc.EnqueueTime).TotalSeconds > 30)
                {
                    Respond(rc.HttpContext, 504,
                        new JObject { ["success"] = false, ["error"] = "Request timed out in queue" }.ToString());
                    continue;
                }

                try
                {
                    var result = CommandRegistry.Execute(rc.Body);
                    Respond(rc.HttpContext, result["success"]?.Value<bool>() == true ? 200 : 400, result.ToString());
                }
                catch (Exception e)
                {
                    var err = new JObject { ["success"] = false, ["error"] = e.ToString() };
                    Respond(rc.HttpContext, 500, err.ToString());
                }
            }
        }

        private static void Respond(HttpListenerContext ctx, int statusCode, string json)
        {
            try
            {
                var response = ctx.Response;
                response.StatusCode = statusCode;
                response.ContentType = "application/json";
                var buffer = Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[tykit] Response error: {e.Message}");
            }
        }

        private static void WriteServerInfo()
        {
            try
            {
                var info = new JObject
                {
                    ["port"] = _activePort,
                    ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
                    ["project"] = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                    ["timestamp"] = DateTime.Now.ToString("o")
                };
                File.WriteAllText(ServerInfoFile, info.ToString());
            }
            catch { }
        }

        private class RequestContext
        {
            public HttpListenerContext HttpContext;
            public string Body;
            public DateTime EnqueueTime;
        }

        // ═══ Listener-thread recovery actions (Windows only) ═══
        //
        // These run in the HTTP listener thread and do NOT touch Unity's main thread,
        // so they work even when the main thread is blocked by a modal dialog or package resolve.

#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const uint WM_CLOSE = 0x0010;
#endif

        private static JObject FocusUnityWindow()
        {
#if UNITY_EDITOR_WIN
            try
            {
                int unityPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var candidates = new List<(IntPtr hwnd, string title)>();

                EnumWindows((hwnd, lParam) =>
                {
                    if (!IsWindowVisible(hwnd)) return true;
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid != unityPid) return true;

                    int len = GetWindowTextLength(hwnd);
                    if (len == 0) return true;
                    var sb = new StringBuilder(len + 1);
                    GetWindowText(hwnd, sb, sb.Capacity);
                    candidates.Add((hwnd, sb.ToString()));
                    return true;
                }, IntPtr.Zero);

                if (candidates.Count == 0)
                    return Err("No visible Unity windows found");

                // Prefer the main editor window (title usually contains "Unity" and project name)
                IntPtr target = IntPtr.Zero;
                string targetTitle = null;
                foreach (var c in candidates)
                {
                    if (c.title.Contains("Unity") && c.title.Contains("-"))
                    {
                        target = c.hwnd;
                        targetTitle = c.title;
                        break;
                    }
                }
                if (target == IntPtr.Zero)
                {
                    target = candidates[0].hwnd;
                    targetTitle = candidates[0].title;
                }

                ShowWindow(target, SW_RESTORE);
                SetForegroundWindow(target);

                return Ok(new JObject
                {
                    ["focused"] = targetTitle,
                    ["candidateCount"] = candidates.Count
                });
            }
            catch (Exception e)
            {
                return Err($"focus-unity failed: {e.Message}");
            }
#else
            return Err("focus-unity is Windows-only");
#endif
        }

        private static JObject DismissForegroundDialog()
        {
#if UNITY_EDITOR_WIN
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return Err("No foreground window");

                GetWindowThreadProcessId(hwnd, out uint pid);
                int unityPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                if (pid != unityPid)
                    return Err($"Foreground window owned by pid {pid}, not Unity ({unityPid})");

                var sb = new StringBuilder(256);
                GetWindowText(hwnd, sb, sb.Capacity);

                PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                return Ok(new JObject
                {
                    ["dismissed"] = sb.ToString(),
                    ["hwnd"] = hwnd.ToInt64().ToString("X")
                });
            }
            catch (Exception e)
            {
                return Err($"dismiss-dialog failed: {e.Message}");
            }
#else
            return Err("dismiss-dialog is Windows-only");
#endif
        }

        private static JObject Ok(object data) =>
            new JObject { ["success"] = true, ["data"] = data is JToken t ? t : JToken.FromObject(data), ["error"] = null };
        private static JObject Err(string msg) =>
            new JObject { ["success"] = false, ["data"] = null, ["error"] = msg };
    }
}
