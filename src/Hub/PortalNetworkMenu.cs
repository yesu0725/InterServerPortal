using System.Collections.Generic;
using InterServerPortal.Net;
using UnityEngine;

namespace InterServerPortal.Hub
{
    /// <summary>
    /// Same-world network travel menu: one button per other portal in the player's
    /// network, labelled by its tag (or "unnamed") with the distance from the player.
    /// Nearest first. See docs/Feature-Portal-Network.md.
    /// </summary>
    internal static class PortalNetworkMenu
    {
        internal static void Show(TeleportWorld source, List<NetPortal> portals)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            if (portals == null || portals.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center,
                    "No other portals in your network yet. Set another portal to Network mode.");
                return;
            }

            // Nearest first — the most common pick is usually a portal you can see.
            Vector3 here = player.transform.position;
            portals.Sort((a, b) =>
                (a.Pos - here).sqrMagnitude.CompareTo((b.Pos - here).sqrMagnitude));

            const float width = 420f;
            const float rowH = 44f;
            const float rowGap = 8f;
            float height = 130f + portals.Count * (rowH + rowGap);

            var win = HubWindow.Open("Travel to portal", width, height);
            if (win == null) return;

            float top = height / 2f - 78f;
            for (int i = 0; i < portals.Count; i++)
            {
                var target = portals[i]; // capture per-iteration for the closure
                int metres = Mathf.RoundToInt((target.Pos - here).magnitude);
                string name = string.IsNullOrEmpty(target.Tag) ? "unnamed portal" : target.Tag;
                string text = $"{name}   ({metres}m)";

                win.AddButton(text, 0f, top - i * (rowH + rowGap), width - 50f, rowH,
                    () => NetworkController.Travel(source, target));
            }

            win.AddButton("Cancel", 0f, -height / 2f + 34f, 150f, 38f, HubWindow.Close);
        }
    }
}
