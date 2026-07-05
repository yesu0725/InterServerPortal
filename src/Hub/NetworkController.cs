using InterServerPortal.Net;
using InterServerPortal.Policy;
using InterServerPortal.Portal;
using InterServerPortal.Security;
using UnityEngine;

namespace InterServerPortal.Hub
{
    /// <summary>
    /// Orchestrates same-world network-portal travel: lock gate → item restriction →
    /// fetch the player's other network portals → selection menu → in-world teleport.
    /// Unlike the inter-server hub this never switches worlds; it reuses vanilla's
    /// own teleport (fade + exit offset), so there is no loading screen.
    /// </summary>
    internal static class NetworkController
    {
        // Ignore a network portal's trigger briefly after arriving on it, so the
        // spawn-overlap doesn't immediately re-open the menu (same idea as the
        // inter-server arm guard, but same-world so it's just a short cooldown).
        private static float _suppressUntil;
        internal static bool ArrivalSuppressed => Time.time < _suppressUntil;

        /// <summary>Walked through a same-world network portal.</summary>
        internal static void Begin(TeleportWorld portal)
        {
            if (portal == null || HubWindow.IsOpen) return;
            if (Player.m_localPlayer == null) return;

            // Entry-code gate first (shared with the inter-server hub).
            if (PortalData.IsLocked(portal.m_nview) && !LockGate.IsUnlocked(portal))
            {
                if (LockGate.InCooldown(portal, out float remaining))
                {
                    Notify($"Portal locked — wait {Mathf.CeilToInt(remaining)}s before trying again.");
                    return;
                }
                CodePrompt.Show(portal, () =>
                {
                    LockGate.MarkUnlocked(portal);
                    Proceed(portal);
                });
                return;
            }

            Proceed(portal);
        }

        private static void Proceed(TeleportWorld portal)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            // Item restriction is fixed by prefab (wood blocks ore, stone allows),
            // exactly like a vanilla teleport through this piece.
            if (!ItemPolicy.CheckAndNotify(portal, player)) return;

            PortalNetwork.Request(portal, list => PortalNetworkMenu.Show(portal, list));
        }

        /// <summary>Commit to a chosen destination portal (closes the menu first).</summary>
        internal static void Travel(TeleportWorld source, NetPortal target)
        {
            HubWindow.Close();
            var player = Player.m_localPlayer;
            if (player == null || source == null) return;

            // Vanilla exit placement: a bit in front of the target portal, lifted up.
            Vector3 forward = target.Rot * Vector3.forward;
            Vector3 pos = target.Pos + forward * source.m_exitDistance + Vector3.up;
            player.TeleportTo(pos, target.Rot, distantTeleport: true);

            _suppressUntil = Time.time + 2f; // swallow the arrival-portal trigger
            Plugin.Debug($"Network teleport → {(string.IsNullOrEmpty(target.Tag) ? "(unnamed)" : target.Tag)}.");
        }

        private static void Notify(string message)
        {
            var player = Player.m_localPlayer;
            if (player != null) player.Message(MessageHud.MessageType.Center, message);
        }
    }
}
