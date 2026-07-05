using System.Collections.Generic;
using System.Text;

namespace InterServerPortal.Portal
{
    /// <summary>
    /// One hub destination: a menu <see cref="Label"/> and the local
    /// <see cref="WorldName"/> it loads. <see cref="SpawnPointId"/> (named arrival
    /// point) is kept in the serialized form for forward compatibility, but
    /// Phase 4 defers arrival points — it is always "default" (world spawn) for now.
    ///
    /// Stored per-portal in the <c>ISP.dests</c> ZDO field as a versioned,
    /// delimited string. See docs/Feature-Hub-Routing.md + docs/Data-Model-ZDO.md.
    /// </summary>
    internal class Destination
    {
        public string Label;
        public string WorldName;
        public string SpawnPointId = "default";

        public Destination() { }

        public Destination(string label, string worldName, string spawnPointId = "default")
        {
            WorldName = worldName;
            Label = string.IsNullOrEmpty(label) ? worldName : label;
            SpawnPointId = string.IsNullOrEmpty(spawnPointId) ? "default" : spawnPointId;
        }

        // ---- Serialization: v1|label;world;spawn|label;world;spawn|... ----
        // Fields are percent-escaped so a label containing the delimiters can't
        // corrupt the blob. Unknown/newer version markers deserialize to empty
        // (forward-compatible — see Data-Model-ZDO.md "Versioning").

        private const string Version = "v1";

        internal static string Serialize(List<Destination> dests)
        {
            var sb = new StringBuilder(Version);
            if (dests != null)
            {
                foreach (var d in dests)
                {
                    if (d == null || string.IsNullOrEmpty(d.WorldName)) continue;
                    sb.Append('|')
                      .Append(Encode(d.Label)).Append(';')
                      .Append(Encode(d.WorldName)).Append(';')
                      .Append(Encode(string.IsNullOrEmpty(d.SpawnPointId) ? "default" : d.SpawnPointId));
                }
            }
            return sb.ToString();
        }

        internal static List<Destination> Deserialize(string raw)
        {
            var list = new List<Destination>();
            if (string.IsNullOrEmpty(raw)) return list;

            var parts = raw.Split('|');
            if (parts.Length == 0 || parts[0] != Version) return list; // unknown version → empty

            for (int i = 1; i < parts.Length; i++)
            {
                var fields = parts[i].Split(';');
                if (fields.Length < 2) continue;
                var world = Decode(fields[1]);
                if (string.IsNullOrEmpty(world)) continue;
                var label = Decode(fields[0]);
                var spawn = fields.Length >= 3 ? Decode(fields[2]) : "default";
                list.Add(new Destination(label, world, spawn));
            }
            return list;
        }

        private static string Encode(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("%", "%25").Replace("|", "%7C").Replace(";", "%3B");
        }

        private static string Decode(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("%3B", ";").Replace("%7C", "|").Replace("%25", "%");
        }
    }
}
