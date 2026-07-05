using HarmonyLib;
using InterServerPortal.Core;

namespace InterServerPortal.Patches
{
    /// <summary>
    /// After a switch leaves the Game scene, control lands back in the start
    /// scene (FejdStartup). If there is a pending switch, resume it here to
    /// drive the second half (start local world / rejoin server) without any
    /// manual menu navigation. See docs/Core-Mechanic-World-Switching.md.
    /// </summary>
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
    internal static class FejdStartup_Start_Patch
    {
        private static void Postfix(FejdStartup __instance)
        {
            if (WorldSwitcher.Pending == null) return;

            Plugin.Log.LogInfo("FejdStartup reached with a pending switch — resuming.");
            // Run the resume coroutine on the persistent plugin object so it
            // survives even if this FejdStartup is torn down by the transition.
            if (Plugin.Instance != null)
            {
                Plugin.Instance.StartCoroutine(WorldSwitcher.ResumePending(__instance));
            }
        }
    }
}
