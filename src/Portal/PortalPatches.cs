using HarmonyLib;
using InterServerPortal.Core;
using UnityEngine;

namespace InterServerPortal.Portal
{
    /// <summary>
    /// Hook the vanilla <see cref="TeleportWorld"/> so a flagged portal drives
    /// the hub routing / world switch while an unflagged portal keeps its
    /// untouched vanilla tag-pairing/teleport behavior. See docs/Portal-System.md.
    ///
    /// Branch points (Phase 4 interaction model):
    ///  • <c>Teleport</c>  — walking through a flagged portal opens hub routing
    ///    (travel) instead of the vanilla paired teleport.
    ///  • <c>Interact</c>  — alt-use opens the config/editor panel (flag +
    ///    destinations); plain use on a flagged portal opens the travel menu, on
    ///    an unflagged portal falls through to the vanilla tag input.
    ///  • <c>GetHoverText</c> — append the flag state + the use hints.
    /// </summary>
    [HarmonyPatch(typeof(TeleportWorld))]
    internal static class TeleportWorldPatches
    {
        /// <summary>Track every live portal so the arrival re-trigger guard can
        /// tell when the player has stepped clear (see PortalData.UpdateArming).</summary>
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void Awake_Postfix(TeleportWorld __instance)
        {
            PortalData.Register(__instance);
        }

        /// <summary>
        /// Called from TeleportWorldTrigger.OnTriggerEnter when a player steps
        /// into the portal. For a flagged portal we swallow the vanilla teleport
        /// and run the inter-server switch for the local player instead.
        /// </summary>
        [HarmonyPatch(nameof(TeleportWorld.Teleport))]
        [HarmonyPrefix]
        private static bool Teleport_Prefix(TeleportWorld __instance, Player player)
        {
            bool interServer = PortalData.IsInterServer(__instance.m_nview);
            bool network = !interServer && PortalData.IsNetwork(__instance.m_nview);

            if (!interServer && !network)
            {
                return true; // vanilla tag-paired portal → untouched
            }

            // Only the local player travels on their own client. The trigger only
            // ever fires for Player.m_localPlayer, but guard anyway and always
            // swallow the vanilla paired teleport on a portal we drive.
            if (player == null || player != Player.m_localPlayer)
            {
                return false;
            }
            if (WorldSwitcher.InProgress)
            {
                return false;
            }

            // Same-world network portal: pick a destination among the player's other
            // network portals (no world switch). Swallow the trigger we just arrived
            // on so it doesn't re-open the menu.
            if (network)
            {
                if (!Hub.NetworkController.ArrivalSuppressed)
                {
                    Hub.NetworkController.Begin(__instance);
                }
                return false;
            }
            // Disarmed right after an arrival: the player spawned on this portal
            // and must step clear before it fires again (prevents the world-to-
            // world bounce). Swallow the vanilla teleport too — never let a
            // flagged portal do a paired teleport.
            if (!WorldSwitcher.PortalArmed)
            {
                Plugin.Debug("Portal trigger ignored — switching disarmed until player steps clear.");
                return false;
            }

            // Hub routing decides the destination (single → direct, multiple → menu).
            Hub.HubController.BeginTravel(__instance);
            return false;
        }

        /// <summary>
        /// Alt-use opens the config/editor panel (build access required). Plain
        /// use always falls through to the vanilla tag-input panel — travel only
        /// happens by walking through a flagged portal.
        /// </summary>
        [HarmonyPatch(nameof(TeleportWorld.Interact))]
        [HarmonyPrefix]
        private static bool Interact_Prefix(
            TeleportWorld __instance, Humanoid human, bool hold, bool alt, ref bool __result)
        {
            if (hold || !alt)
            {
                return true; // vanilla handles hold + plain use (tag input)
            }

            // Alt-use → config/editor panel (build access, same as editing the tag).
            if (!PrivateArea.CheckAccess(__instance.transform.position))
            {
                human.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                __result = true;
                return false;
            }
            Hub.PortalConfigPanel.Show(__instance);
            __result = true;
            return false;
        }

        /// <summary>Show the portal's mode and the use hints in the hover tooltip.</summary>
        [HarmonyPatch(nameof(TeleportWorld.GetHoverText))]
        [HarmonyPostfix]
        private static void GetHoverText_Postfix(TeleportWorld __instance, ref string __result)
        {
            bool isp = PortalData.IsInterServer(__instance.m_nview);
            bool network = PortalData.IsNetwork(__instance.m_nview);

            string status;
            if (isp)
            {
                status = "InterServerPortal: <color=#7dd3fc>ON</color>";
            }
            else if (network)
            {
                status = "InterServerPortal: <color=#c4b5fd>NETWORK</color>";
            }
            else
            {
                status = "InterServerPortal: <color=grey>off</color>";
            }

            if ((isp || network) && PortalData.IsLocked(__instance.m_nview))
            {
                status += "  <color=#ffb0b0>[locked]</color>";
            }

            string hints;
            if (isp)
            {
                hints = "walk through to travel   " +
                        "[<color=yellow><b>L.Shift + $KEY_Use</b></color>] configure";
            }
            else if (network)
            {
                hints = "walk through to travel your portal network   " +
                        "[<color=yellow><b>L.Shift + $KEY_Use</b></color>] configure";
            }
            else
            {
                hints = "[<color=yellow><b>L.Shift + $KEY_Use</b></color>] configure (InterServerPortal)";
            }

            __result += "\n" + Localization.instance.Localize(status + "\n" + hints);
        }
    }
}
