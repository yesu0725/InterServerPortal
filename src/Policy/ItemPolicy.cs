using System.Collections.Generic;
using UnityEngine;

namespace InterServerPortal.Policy
{
    /// <summary>
    /// Feature #5 — the wood/stone item policy. A world switch carries the whole
    /// inventory client-side regardless of prefab, so the vanilla "ores can't
    /// teleport" restriction is a policy we deliberately re-apply, not an engine
    /// limit. We reuse vanilla's own teleportability predicate
    /// (<see cref="Humanoid.IsTeleportable"/>) so modded items with teleport flags
    /// behave correctly. See docs/Feature-Item-Policy.md.
    /// </summary>
    internal static class ItemPolicy
    {
        /// <summary>
        /// Does this portal block teleport-restricted items? Fixed by prefab, exactly
        /// like vanilla and not user-configurable: wood (<c>!m_allowAllItems</c>)
        /// blocks ores/metals, stone (<c>m_allowAllItems</c>) carries everything.
        /// </summary>
        internal static bool BlocksRestricted(TeleportWorld portal)
        {
            return portal != null && !portal.m_allowAllItems;
        }

        /// <summary>
        /// Gate run just before a switch. Returns true if travel may proceed. If the
        /// portal blocks restricted items and the player is carrying some, denies
        /// with a centered message naming them (vanilla's "$msg_noteleport" wording)
        /// and returns false. Uses vanilla's own predicate, which also honours the
        /// <c>TeleportAll</c> global key.
        /// </summary>
        internal static bool CheckAndNotify(TeleportWorld portal, Player player)
        {
            if (player == null) return true;
            if (!BlocksRestricted(portal)) return true;
            if (player.IsTeleportable()) return true; // nothing restricted (or TeleportAll set)

            string msg = Localization.instance.Localize("$msg_noteleport");
            var offenders = OffendingItemNames(player);
            if (offenders.Count > 0)
            {
                msg += ": " + string.Join(", ", offenders.ToArray());
            }
            player.Message(MessageHud.MessageType.Center, msg);
            return false;
        }

        /// <summary>Distinct localized names of the player's non-teleportable items.</summary>
        private static List<string> OffendingItemNames(Player player)
        {
            var names = new List<string>();
            var inv = player.GetInventory();
            if (inv == null) return names;
            foreach (var item in inv.GetAllItems())
            {
                if (item == null || item.m_shared == null || item.m_shared.m_teleportable) continue;
                string name = Localization.instance.Localize(item.m_shared.m_name);
                if (!names.Contains(name)) names.Add(name);
            }
            return names;
        }
    }
}
