using System;
using System.Collections;
using UnityEngine;

namespace InterServerPortal.Core
{
    /// <summary>
    /// The core mechanic (Phase 1 prototype). Drives the disconnect →
    /// load-other-world sequence. Because a switch spans a scene reload
    /// (Game scene → start scene → main scene), the intent is stashed in a
    /// static <see cref="Pending"/> that survives the reload, and resumed from
    /// the FejdStartup start-scene patch. See docs/Core-Mechanic-World-Switching.md.
    ///
    /// Prototype scope: hotkey-triggered, single hardcoded target world. No
    /// portals, UI, validation, or item policy yet — those are later phases.
    /// </summary>
    internal static class WorldSwitcher
    {
        internal enum SwitchKind { None, ToLocal, ToServer }

        internal class PendingSwitch
        {
            public SwitchKind Kind;
            public string LocalWorldName;                // ToLocal
            public ReturnRegistry.Origin ServerOrigin;   // ToServer
        }

        /// <summary>Survives the scene reload; consumed by the FejdStartup patch.</summary>
        internal static PendingSwitch Pending;

        /// <summary>Guards against double-triggering during the fade/reload.</summary>
        internal static bool InProgress;

        /// <summary>
        /// After a switch, the player loads back in standing inside the portal
        /// they just used (their logout position). Unity fires OnTriggerEnter
        /// once on that spawn-overlap, which would immediately switch again — an
        /// infinite bounce between the two worlds. So portal switching is
        /// DISARMED whenever a switch commits (<see cref="Leave"/>) and only
        /// RE-ARMED once the local player has stepped clear of every inter-server
        /// portal. Static so it survives the scene reload. See
        /// docs/Portal-System.md → "Arrival re-trigger".
        ///
        /// Starts DISARMED and self-arms on the first frame the player is clear
        /// of any inter-server portal — so logging in while standing on one does
        /// not insta-switch either.
        /// </summary>
        internal static bool PortalArmed = false;

        /// <summary>
        /// True from the moment a switch commits (<see cref="Leave"/>) until a
        /// NEW local player has spawned in the destination world. It stops the
        /// arm/disarm tick from re-arming during teardown — the old world's
        /// portals are destroyed before the old player is, which would otherwise
        /// look "clear of portals" and re-arm mid-flight, causing an arrival
        /// bounce. See PortalData.UpdateArming.
        /// </summary>
        internal static bool AwaitingArrival;

        // ---- Requests (called while still in the Game scene) ----

        /// <summary>Leave the current session and load the given local world.</summary>
        internal static void RequestSwitchToLocal(string localWorldName)
        {
            if (!Guard()) return;

            // In a local single-player world we ARE the server. Switching to the
            // SAME world would just waste a loading screen — skip it. A different
            // seed is a genuine local→local hop (Phase 4) and is allowed.
            if (IsLocalHost())
            {
                string current = ZNet.World != null ? ZNet.World.m_name : "";
                if (string.Equals(current, localWorldName, StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Log.LogInfo("Already in that local world; ignoring switch-to-local.");
                    return;
                }
            }

            // Phase 3: validate the destination while still safely connected.
            // If it can't be entered, notify and abort — no teardown, no strand.
            var check = DestinationValidator.Validate(localWorldName);
            if (!check.IsAvailable)
            {
                string msg = $"'{localWorldName}' is unavailable: {check.Reason}.";
                Plugin.Log.LogWarning("Switch to local blocked — " + msg);
                Notify(msg);
                return;
            }

            // Capture/refresh the origin only when we're leaving a real server.
            // On a local→local hop we keep the existing saved origin so the
            // player can still return to the server from the new local world.
            if (IsRemoteClient())
            {
                var origin = CaptureCurrentServerOrigin();
                if (origin != null && origin.IsValid)
                {
                    ReturnRegistry.Save(origin);
                }
                else
                {
                    Plugin.Log.LogWarning(
                        "Could not capture current server address — the return trip may not work. " +
                        "(Are you connected to a dedicated server via IP?)");
                }
            }

            // Announce the crossing to Discord (no-op unless a webhook is configured).
            // Only for a genuine server → local switch (we're a remote client leaving
            // a real server), not a local→local hop. Fired here, while the local
            // player + session still exist, so we can read the player's name and the
            // origin server we're leaving.
            if (IsRemoteClient())
            {
                var traveller = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "";
                string fromServer = ZNet.World != null ? ZNet.World.m_name : "";
                Net.DiscordNotifier.NotifyTravelToLocal(traveller, localWorldName, fromServer);
            }

            InProgress = true;
            Pending = new PendingSwitch { Kind = SwitchKind.ToLocal, LocalWorldName = localWorldName };
            Plugin.Log.LogInfo($"Switching to local world '{localWorldName}' …");
            Leave();
        }

        /// <summary>Return from the local world to the remembered origin server.</summary>
        internal static void RequestReturnToServer()
        {
            if (!Guard()) return;
            // If we're a client we're already on a remote server. Reconnecting
            // would just reload the server we're on — skip it.
            if (IsRemoteClient())
            {
                Plugin.Log.LogInfo("Already connected to a server; ignoring return-to-server.");
                return;
            }

            var origin = ReturnRegistry.Load();
            if (origin == null)
            {
                Plugin.Log.LogWarning("RequestReturnToServer: no saved origin server to return to.");
                return;
            }

            InProgress = true;
            Pending = new PendingSwitch { Kind = SwitchKind.ToServer, ServerOrigin = origin };
            Plugin.Log.LogInfo($"Returning to server {origin.Host}:{origin.Port} …");
            Leave();
        }

        // ---- Resume (called from FejdStartup start scene, after reload) ----

        internal static IEnumerator ResumePending(FejdStartup fejd)
        {
            var pending = Pending;
            Pending = null;
            if (pending == null) yield break;

            // Let FejdStartup finish initializing (world list, login flow) before we drive it.
            for (int i = 0; i < 10 && FejdStartup.instance == null; i++) yield return null;
            fejd = FejdStartup.instance;
            if (fejd == null)
            {
                Plugin.Log.LogError("ResumePending: FejdStartup.instance never appeared; aborting switch.");
                InProgress = false;
                yield break;
            }
            // A couple of frames of settle time.
            yield return null;
            yield return null;

            switch (pending.Kind)
            {
                case SwitchKind.ToLocal:
                    StartLocalWorld(fejd, pending.LocalWorldName);
                    break;
                case SwitchKind.ToServer:
                    JoinServer(fejd, pending.ServerOrigin);
                    break;
            }
        }

        // ---- Sequence steps ----

        private static void Leave()
        {
            // Disarm portal switching for the arrival: the player will spawn on
            // the portal they just used and must step clear before it can fire
            // again (see PortalArmed). AwaitingArrival keeps it disarmed through
            // the whole teardown+load, not just the current frame.
            PortalArmed = false;
            AwaitingArrival = true;

            // Logout(save: true, changeToStartScene: true) tears down the session
            // cleanly and loads the start scene (FejdStartup). Do NOT skip the
            // normal teardown — a partial teardown is the main crash source.
            if (Game.instance != null)
            {
                Game.instance.Logout(true, true);
            }
            else
            {
                Plugin.Log.LogError("Leave: Game.instance is null; cannot log out.");
                InProgress = false;
                Pending = null;
            }
        }

        private static void StartLocalWorld(FejdStartup fejd, string worldName)
        {
            try
            {
                // Re-validate mid-flight: pre-flight already passed, but the file
                // could have changed, or been unreadable on this second read.
                // We've already left the server here, so recovery = fall back to
                // the origin rather than stranding the player on the menu.
                var check = DestinationValidator.Validate(worldName);
                if (!check.IsAvailable)
                {
                    Plugin.Log.LogError(
                        $"StartLocalWorld: '{worldName}' unavailable mid-flight ({check.Reason}). " +
                        "Falling back to the origin server.");
                    FallbackToOrigin(fejd);
                    return;
                }

                var world = check.World;
                fejd.m_world = world;
                fejd.m_startingWorld = true;

                // Host a private, non-open single-player session for this world.
                ZNet.SetServer(true, false, false, world.m_name, "", world);
                fejd.TransitionToMainScene();
                Plugin.Log.LogInfo($"Loading local world '{world.m_name}' …");

                // Transition committed. Clear the guard so the next switch (e.g.
                // the return trip) can fire once the new world finishes loading.
                // Player.m_localPlayer is null during the load, so no hotkey can
                // re-trigger before arrival.
                InProgress = false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"StartLocalWorld failed: {e}");
                // Teardown already happened — try to get the player back to the
                // server instead of leaving them stuck on the menu.
                FallbackToOrigin(fejd);
            }
        }

        /// <summary>
        /// A local load failed after we'd already left the server. Recover by
        /// reconnecting to the remembered origin; if there is none, drop cleanly
        /// to the main menu (never a hung/black state). See
        /// docs/Feature-Failure-Handling.md → "Fallback if a switch fails mid-flight".
        /// </summary>
        private static void FallbackToOrigin(FejdStartup fejd)
        {
            var origin = ReturnRegistry.Load();
            if (origin != null && origin.IsValid)
            {
                Plugin.Log.LogWarning(
                    $"Falling back — reconnecting to origin server {origin.Host}:{origin.Port}.");
                JoinServer(fejd, origin); // clears InProgress + drives the transition
            }
            else
            {
                Plugin.Log.LogError(
                    "Fallback failed: no saved origin server to return to. Staying on the main menu.");
                InProgress = false;
            }
        }

        private static void JoinServer(FejdStartup fejd, ReturnRegistry.Origin origin)
        {
            try
            {
                // We deliberately do NOT call FejdStartup.JoinServer(): for a
                // dedicated server it runs an async matchmaking IP resolution
                // (MultiBackendMatchmaking.GetServerIPAsync) plus privilege/
                // server-browser gates. For a known IP:port that async step
                // often never resolves (no matchmaking entry for a raw/local
                // IP), so HasServerHost() stays false and the transition
                // silently bails. Instead we set the host directly — exactly
                // what vanilla's async callback ultimately does — so
                // HasServerHost() is immediately true and the main scene loads
                // and connects. See docs/Core-Mechanic-World-Switching.md.
                var backend = ParseBackend(origin.Backend);
                NormalizeHostPort(origin.Host, origin.Port, out var host, out var port);

                ZNet.SetServer(false, false, false, "", "", null);
                ZNet.ResetServerHost();
                ZNet.SetServerHost(host, port, backend);

                if (!string.IsNullOrEmpty(origin.Password))
                {
                    FejdStartup.ServerPassword = origin.Password;
                }

                fejd.m_startingWorld = false;
                fejd.TransitionToMainScene();
                Plugin.Log.LogInfo($"Connecting to host='{host}' port={port} (backend {backend}) …");

                // Transition committed; clear the guard (see StartLocalWorld).
                InProgress = false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"JoinServer failed: {e}");
                InProgress = false;
            }
        }

        private static OnlineBackendType ParseBackend(string name)
        {
            if (!string.IsNullOrEmpty(name) &&
                Enum.TryParse(name, out OnlineBackendType parsed) &&
                parsed != OnlineBackendType.None)
            {
                return parsed;
            }
            // Default: Valheim dedicated servers use Steam networking.
            return OnlineBackendType.Steamworks;
        }

        // ---- Helpers ----

        private static bool Guard()
        {
            if (InProgress)
            {
                Plugin.Log.LogWarning("A world switch is already in progress; ignoring.");
                return false;
            }
            if (Player.m_localPlayer == null)
            {
                Plugin.Log.LogWarning("No local player; not in a playable session.");
                return false;
            }
            return true;
        }

        /// <summary>True when this client is hosting the world (local single-player).</summary>
        private static bool IsLocalHost()
        {
            return ZNet.instance != null && ZNet.instance.IsServer() && !ZNet.instance.IsDedicated();
        }

        /// <summary>True when this client is connected to a remote (dedicated) server.</summary>
        private static bool IsRemoteClient()
        {
            return ZNet.instance != null && !ZNet.instance.IsServer();
        }

        /// <summary>Read the address of the server we are currently connected to.</summary>
        private static ReturnRegistry.Origin CaptureCurrentServerOrigin()
        {
            // When we joined a dedicated server by IP, ZNet's static server-host
            // config holds host/port/backend. (For Steam-friend joins this is a
            // steam id and the return trip is out of prototype scope.)
            NormalizeHostPort(ZNet.m_serverHost, ZNet.m_serverHostPort, out var host, out var port);
            if (string.IsNullOrEmpty(host) || port <= 0) return null;

            return new ReturnRegistry.Origin
            {
                Host = host,
                Port = port,
                Password = ZNet.m_serverPassword ?? "",
                Backend = ZNet.m_onlineBackend.ToString(),
                Timestamp = DateTime.UtcNow.ToString("o"),
            };
        }

        /// <summary>
        /// ZNet.m_serverHost sometimes already contains the port ("ip:port")
        /// while m_serverHostPort holds it too. If we stored the combined form,
        /// ClientConnect would build "ip:port:port" and fail. Split off a
        /// trailing numeric ":port" so host is bare. Bare IPv6 (multiple colons,
        /// unbracketed) is left untouched.
        /// </summary>
        internal static void NormalizeHostPort(string rawHost, int rawPort, out string host, out int port)
        {
            host = rawHost ?? "";
            port = rawPort;

            int lastColon = host.LastIndexOf(':');
            int firstColon = host.IndexOf(':');
            bool looksLikeBareIPv6 = firstColon >= 0 && firstColon != lastColon && !host.Contains("]");
            if (lastColon > 0 && !looksLikeBareIPv6)
            {
                var portPart = host.Substring(lastColon + 1);
                if (int.TryParse(portPart, out var embeddedPort) && embeddedPort > 0)
                {
                    host = host.Substring(0, lastColon);
                    if (port <= 0) port = embeddedPort;
                }
            }
        }

        /// <summary>Centered message to the local player, if one exists (no-op on the menu).</summary>
        private static void Notify(string message)
        {
            var player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.Center, message);
            }
        }
    }
}
