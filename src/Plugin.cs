using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn;
using UnityEngine;

namespace InterServerPortal
{
    /// <summary>
    /// InterServerPortal — step through a portal to switch to your own local
    /// single-player world and back, without the manual logout/relog cycle; plus
    /// same-world "portal networks" that mesh all of a player's portals together.
    ///
    /// Travel is portals-only. Per-frame this drives the arrival re-arm guard and
    /// keeps the portal-network RPCs registered. See docs/Roadmap-Phases.md.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Main.ModGuid)] // Jötunn — hard dependency (UI + config helpers)
    [BepInProcess("valheim.exe")]         // client
    [BepInProcess("valheim_server.exe")]  // dedicated server (multi-regional server plan)
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.interserverportal";
        public const string PluginName = "InterServerPortal";
        public const string PluginVersion = "0.9.1";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;

        // --- Config ---
        internal ConfigEntry<bool> DebugLogging;
        internal ConfigEntry<bool> RememberServerPassword;
        internal ConfigEntry<string> DiscordWebhookUrl;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            DebugLogging = Config.Bind(
                "General", "DebugLogging", false,
                "Enable verbose debug logging for InterServerPortal.");

            RememberServerPassword = Config.Bind(
                "General", "RememberServerPassword", false,
                "Persist the origin server password so the return trip can auto-reconnect to " +
                "password-protected servers. Stored in plain text — leave off unless you need it.");

            DiscordWebhookUrl = Config.Bind(
                "Discord", "WebhookUrl", "",
                "Discord webhook URL. When set, posts a message whenever a player steps through " +
                "a portal into their own local world. Leave empty to disable.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded (Phase 9 — per-mode portal glow + Discord travel notify).");
        }

        private void Update()
        {
            // Arm/disarm tick — must run every frame (incl. during a switch when
            // the player is briefly null) so it can tell teardown from a real
            // arrival (see PortalData.UpdateArming).
            Portal.PortalData.UpdateArming();

            // Same-world portal network: register the routed RPCs once networking is
            // up (runs on client + server), and time out a stuck travel request.
            Net.PortalNetwork.EnsureRegistered();
            Net.PortalNetwork.Tick();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            Instance = null;
        }

        /// <summary>Verbose logging gated behind the DebugLogging config toggle.</summary>
        internal static void Debug(string message)
        {
            if (Instance != null && Instance.DebugLogging.Value)
            {
                Log.LogInfo($"[debug] {message}");
            }
        }
    }
}
