using System;
using System.IO;
using System.Net;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using PeakEngine.Core;
using PeakEngine.Game;
using UnityEngine;

namespace PeakEngine
{
    /// <summary>
    /// Where everything starts. BepInEx (the mod loader) finds this class because of the
    /// [BepInPlugin] line and creates it when the game boots.
    ///
    /// All it does is wire the pieces together:
    ///   1. start a tiny web server on http://127.0.0.1:8777
    ///   2. register the API routes (see Routes.cs)
    ///   3. create a hidden object whose Update() runs every frame, which is where queued web
    ///      requests actually touch the game (see Core/MainThread.cs)
    /// </summary>
    [BepInPlugin(Id, Name, Routes.PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.kobalosz.peakengine";
        public const string Name = "PeakEngine";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            // Settings live in BepInEx/config/com.kobalosz.peakengine.cfg (created on first run).
            ConfigEntry<int> port = Config.Bind("Server", "Port", 8777,
                "Port of the control panel. Open http://127.0.0.1:<port> in a browser while the game runs.");

            // The panel's HTML/CSS/JS sits in a "panel" folder next to this DLL.
            string pluginFolder = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".";
            string panelFolder = Path.Combine(pluginFolder, "panel");

            var router = new Router(panelFolder, port.Value, work => MainThread.Run(work), message => Log.LogWarning(message));
            Routes.Register(router);

            try
            {
                // 127.0.0.1 ("loopback") means only programs on this computer can connect.
                // The server runs on a background thread, which ends by itself when the game closes.
                var server = new HttpServer(IPAddress.Loopback, port.Value, router.Handle, message => Log.LogWarning(message));
                server.Start();
                Log.LogInfo($"Control panel: http://127.0.0.1:{port.Value}");
            }
            catch (Exception e)
            {
                Log.LogError($"Could not start the web server on port {port.Value}: {e.Message}. " +
                             "Is another program using that port? You can change it in BepInEx/config/" + Id + ".cfg");
                return;
            }

            // Our own object that survives scene changes and gets an Update() call every frame.
            var runner = new GameObject("PeakEngine");
            runner.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(runner);
            runner.AddComponent<Runner>();

            ReportCompatibility();
        }

        private static void ReportCompatibility()
        {
            try
            {
                var checks = Diagnostics.Run();
                int problems = Diagnostics.CountProblems(checks);
                if (problems == 0)
                {
                    Log.LogInfo("Compatibility check passed: everything PeakEngine uses was found in this game version.");
                    return;
                }
                Log.LogWarning($"Compatibility check: {problems} thing(s) PeakEngine uses were not found. Some features will not work:");
                foreach (var check in checks)
                    if (!(bool)check["ok"]) Log.LogWarning($"  missing {check["name"]} (used for: {check["usedFor"]})");
            }
            catch (Exception e)
            {
                Log.LogWarning("Compatibility check could not run: " + e.Message);
            }
        }

        /// <summary>The per-frame heartbeat. Unity calls Update() on the main thread once per frame.</summary>
        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                MainThread.Pump(); // run cheats requested by the control panel
                Toggles.Tick();    // keep always-on cheats applied
            }
        }
    }
}
