using System;
using System.Collections.Generic;
using InterServerPortal.Core;
using InterServerPortal.Portal;
using InterServerPortal.Security;
using UnityEngine;

namespace InterServerPortal.Hub
{
    /// <summary>One selectable row in the travel menu.</summary>
    internal class HubEntry
    {
        public string Label;
        public Destination Destination;   // null for the server-return entry
        public bool IsServerReturn;
        public DestinationValidator.Availability Availability = DestinationValidator.Availability.Available;
        public string UnavailableReason;

        public bool IsActionable =>
            IsServerReturn || Availability == DestinationValidator.Availability.Available;
    }

    /// <summary>
    /// Phase 4 hub routing entry point. Reads a portal's destination list, works
    /// out which way each entry goes (to a local world, or back to the origin
    /// server), validates each, and either travels straight to a single
    /// destination or opens the selection menu. See docs/Feature-Hub-Routing.md.
    /// </summary>
    internal static class HubController
    {
        /// <summary>Interacting with / walking through a flagged portal.</summary>
        internal static void BeginTravel(TeleportWorld portal)
        {
            if (portal == null || HubWindow.IsOpen) return;
            if (WorldSwitcher.InProgress || !WorldSwitcher.PortalArmed) return;

            // Phase 5: entry code gate runs first. A recently-entered code is
            // remembered so travel isn't re-prompted on every use.
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
                    ProceedTravel(portal);
                });
                return;
            }

            ProceedTravel(portal);
        }

        private static void ProceedTravel(TeleportWorld portal)
        {
            // Phase 6: item policy gate. Wood portals (or an ISP.policy=block
            // override) refuse to carry teleport-restricted items; stone (or
            // allow) lets everything through. Runs just before the switch, after
            // the lock gate. Denies with a message naming the offending items.
            if (!Policy.ItemPolicy.CheckAndNotify(portal, Player.m_localPlayer))
            {
                return;
            }

            var nview = portal.m_nview;
            var dests = PortalData.GetDestinations(nview);

            bool inLocalWorld = ZNet.instance != null &&
                                ZNet.instance.IsServer() && !ZNet.instance.IsDedicated();
            string currentWorld = ZNet.World != null ? ZNet.World.m_name : "";

            var entries = new List<HubEntry>();

            // From a local world, the return trip to the origin server is a routing option.
            if (inLocalWorld && ReturnRegistry.HasOrigin)
            {
                entries.Add(new HubEntry { IsServerReturn = true, Label = "Return to origin server" });
            }

            foreach (var d in dests)
            {
                // Can't hop to the world we're already standing in.
                if (inLocalWorld &&
                    string.Equals(d.WorldName, currentWorld, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var check = DestinationValidator.Validate(d.WorldName);
                entries.Add(new HubEntry
                {
                    Destination = d,
                    Label = d.Label,
                    Availability = check.Status,
                    UnavailableReason = check.IsAvailable ? null : check.Reason,
                });
            }

            if (entries.Count == 0)
            {
                Notify("This portal has no destinations. Use L.Shift+Use to configure one.");
                return;
            }

            // Fast path: exactly one entry and it's usable → skip the menu.
            if (entries.Count == 1 && entries[0].IsActionable)
            {
                Go(entries[0]);
                return;
            }

            DestinationMenu.Show(entries);
        }

        /// <summary>Commit to a chosen entry (closes the menu first).</summary>
        internal static void Go(HubEntry entry)
        {
            HubWindow.Close();
            if (entry == null || !entry.IsActionable) return;

            if (entry.IsServerReturn)
            {
                WorldSwitcher.RequestReturnToServer();
            }
            else
            {
                WorldSwitcher.RequestSwitchToLocal(entry.Destination.WorldName);
            }
        }

        private static void Notify(string message)
        {
            var player = Player.m_localPlayer;
            if (player != null) player.Message(MessageHud.MessageType.Center, message);
        }
    }
}
