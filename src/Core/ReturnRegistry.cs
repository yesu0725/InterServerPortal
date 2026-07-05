using System;
using System.IO;
using BepInEx;

namespace InterServerPortal.Core
{
    /// <summary>
    /// Persists where "back" is when a player leaves a server for their local
    /// world, so the return trip works even after a crash/restart. Stored as a
    /// tiny key=value file in the BepInEx config folder — no JSON dependency.
    ///
    /// Passwords are only written when the RememberServerPassword config is on
    /// (see docs/Data-Model-ZDO.md — "never persist raw passwords unless opt-in").
    /// </summary>
    internal static class ReturnRegistry
    {
        internal class Origin
        {
            public string Host;
            public int Port;
            public string Password;   // may be empty
            public string Backend;    // OnlineBackendType name (informational)
            public string Timestamp;

            public bool IsValid => !string.IsNullOrEmpty(Host) && Port > 0;
        }

        private static string FilePath =>
            Path.Combine(Paths.ConfigPath, "InterServerPortal.return.txt");

        internal static void Save(Origin origin)
        {
            try
            {
                var pw = (Plugin.Instance != null && Plugin.Instance.RememberServerPassword.Value)
                    ? origin.Password ?? ""
                    : "";

                var lines = new[]
                {
                    "host=" + (origin.Host ?? ""),
                    "port=" + origin.Port,
                    "password=" + pw,
                    "backend=" + (origin.Backend ?? ""),
                    "timestamp=" + (origin.Timestamp ?? DateTime.UtcNow.ToString("o")),
                };
                File.WriteAllLines(FilePath, lines);
                Plugin.Debug($"ReturnRegistry saved: {origin.Host}:{origin.Port} (pw stored: {pw.Length > 0})");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ReturnRegistry.Save failed: {e}");
            }
        }

        internal static Origin Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var o = new Origin();
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var idx = line.IndexOf('=');
                    if (idx < 0) continue;
                    var key = line.Substring(0, idx);
                    var val = line.Substring(idx + 1);
                    switch (key)
                    {
                        case "host": o.Host = val; break;
                        case "port": int.TryParse(val, out var p); o.Port = p; break;
                        case "password": o.Password = val; break;
                        case "backend": o.Backend = val; break;
                        case "timestamp": o.Timestamp = val; break;
                    }
                }
                return o.IsValid ? o : null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ReturnRegistry.Load failed: {e}");
                return null;
            }
        }

        internal static bool HasOrigin => Load() != null;
    }
}
