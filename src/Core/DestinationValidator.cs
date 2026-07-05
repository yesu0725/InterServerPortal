using System;
using System.Collections.Generic;

namespace InterServerPortal.Core
{
    /// <summary>
    /// Phase 3 — validate a local-world destination so a missing/corrupt/
    /// incompatible seed never strands the player. Reads the on-disk save list
    /// via <see cref="SaveSystem"/> and inspects <see cref="World.m_dataError"/>
    /// (populated by <c>World.LoadWorld</c>). Run this BEFORE leaving the server
    /// (fail fast, stay connected) and again mid-flight as a safety net.
    /// See docs/Feature-Failure-Handling.md.
    /// </summary>
    internal static class DestinationValidator
    {
        internal enum Availability
        {
            Available,     // loadable — safe to switch
            NoTarget,      // no world name configured
            NotFound,      // no save with that name in worlds_local
            BadVersion,    // save from an incompatible game / world-gen version
            Corrupt,       // corrupt / unreadable save
            MissingData,   // metadata (.fwl) or database (.db) file missing
            LoadError,     // threw / otherwise failed to load
        }

        internal struct Result
        {
            public Availability Status;
            public World World;   // non-null only when Available

            public bool IsAvailable => Status == Availability.Available;

            /// <summary>Short player-facing reason for an unavailable destination.</summary>
            public string Reason
            {
                get
                {
                    switch (Status)
                    {
                        case Availability.Available:   return "";
                        case Availability.NoTarget:    return "no destination world configured";
                        case Availability.NotFound:    return "world file not found";
                        case Availability.BadVersion:  return "incompatible world version";
                        case Availability.Corrupt:     return "world save is corrupt";
                        case Availability.MissingData: return "world save data is missing";
                        default:                       return "world could not be loaded";
                    }
                }
            }
        }

        internal static Result Validate(string worldName)
        {
            if (string.IsNullOrEmpty(worldName))
                return new Result { Status = Availability.NoTarget };

            World match = null;
            try
            {
                // Read the save list fresh from disk (same source Phase 1's
                // FindWorld used). Each World already carries m_dataError.
                List<World> worlds = SaveSystem.GetWorldList();
                if (worlds != null)
                {
                    foreach (var w in worlds)
                    {
                        if (w != null && !w.m_menu &&
                            string.Equals(w.m_name, worldName, StringComparison.OrdinalIgnoreCase))
                        {
                            match = w;
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"DestinationValidator: reading world list failed: {e}");
                return new Result { Status = Availability.LoadError };
            }

            if (match == null)
                return new Result { Status = Availability.NotFound };

            switch (match.m_dataError)
            {
                case World.SaveDataError.None:
                    return new Result { Status = Availability.Available, World = match };
                case World.SaveDataError.BadVersion:
                    return new Result { Status = Availability.BadVersion };
                case World.SaveDataError.Corrupt:
                    return new Result { Status = Availability.Corrupt };
                case World.SaveDataError.MissingMeta:
                case World.SaveDataError.MissingDB:
                    return new Result { Status = Availability.MissingData };
                default:
                    return new Result { Status = Availability.LoadError };
            }
        }
    }
}
