using System.Collections.Generic;
using UnityEngine;

namespace InterServerPortal.Security
{
    /// <summary>
    /// Client-side session state for locked portals: a wrong-attempt throttle
    /// (escalating cooldown to discourage brute forcing) and a short "recently
    /// unlocked" memory so a correct code isn't re-prompted on every use.
    /// Keyed per-portal by ZDOID. See docs/Feature-Lock-Codes.md.
    /// </summary>
    internal static class LockGate
    {
        private const float UnlockWindow = 300f;   // remember a correct code for 5 min
        private const float CooldownStep = 2f;      // seconds per consecutive failure
        private const float CooldownMax = 30f;

        private class State
        {
            public int Fails;
            public float CooldownUntil;
            public float UnlockedUntil;
        }

        private static readonly Dictionary<ZDOID, State> _states = new Dictionary<ZDOID, State>();

        private static bool TryKey(TeleportWorld portal, out ZDOID key)
        {
            key = ZDOID.None;
            if (portal == null || portal.m_nview == null || !portal.m_nview.IsValid()) return false;
            var zdo = portal.m_nview.GetZDO();
            if (zdo == null) return false;
            key = zdo.m_uid;
            return true;
        }

        private static State Get(ZDOID key)
        {
            if (!_states.TryGetValue(key, out var s))
            {
                s = new State();
                _states[key] = s;
            }
            return s;
        }

        /// <summary>True while the player entered the correct code recently.</summary>
        internal static bool IsUnlocked(TeleportWorld portal)
        {
            if (!TryKey(portal, out var key)) return false;
            return _states.TryGetValue(key, out var s) && Time.time < s.UnlockedUntil;
        }

        internal static void MarkUnlocked(TeleportWorld portal)
        {
            if (!TryKey(portal, out var key)) return;
            var s = Get(key);
            s.UnlockedUntil = Time.time + UnlockWindow;
            s.Fails = 0;
            s.CooldownUntil = 0f;
        }

        /// <summary>True if a wrong-attempt cooldown is still active; reports the remaining seconds.</summary>
        internal static bool InCooldown(TeleportWorld portal, out float remaining)
        {
            remaining = 0f;
            if (!TryKey(portal, out var key)) return false;
            if (!_states.TryGetValue(key, out var s)) return false;
            remaining = s.CooldownUntil - Time.time;
            return remaining > 0f;
        }

        internal static void RegisterFail(TeleportWorld portal)
        {
            if (!TryKey(portal, out var key)) return;
            var s = Get(key);
            s.Fails++;
            s.CooldownUntil = Time.time + Mathf.Min(s.Fails * CooldownStep, CooldownMax);
        }
    }
}
