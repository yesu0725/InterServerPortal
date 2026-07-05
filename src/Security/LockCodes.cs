using System;
using System.Security.Cryptography;
using System.Text;

namespace InterServerPortal.Security
{
    /// <summary>
    /// Salt+hash helpers for the optional per-portal entry code (Feature #1).
    /// The raw code is never stored — only a per-portal random salt and the
    /// SHA-256 hash of <c>salt + code</c> live in the ZDO. This is
    /// anti-casual-peek, not high security: anyone who can read the world save
    /// can read the salt+hash, it just stops trivially lifting the code.
    /// See docs/Feature-Lock-Codes.md + docs/Data-Model-ZDO.md.
    /// </summary>
    internal static class LockCodes
    {
        /// <summary>A fresh random salt (base64) for a newly set code.</summary>
        internal static string NewSalt()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        /// <summary>Hex SHA-256 of salt + code.</summary>
        internal static string Hash(string salt, string code)
        {
            using (var sha = SHA256.Create())
            {
                var data = Encoding.UTF8.GetBytes((salt ?? "") + (code ?? ""));
                var hash = sha.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>True if <paramref name="entered"/> matches the stored hash (or there is no lock).</summary>
        internal static bool Verify(string salt, string storedHash, string entered)
        {
            if (string.IsNullOrEmpty(storedHash)) return true; // no lock set
            return string.Equals(Hash(salt, entered), storedHash, StringComparison.Ordinal);
        }
    }
}
