using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace InterServerPortal.Net
{
    /// <summary>
    /// Fire-and-forget Discord webhook posts, used to announce when a player leaves
    /// a server for their own local world. Disabled unless a webhook URL is set
    /// (Discord/WebhookUrl).
    ///
    /// The POST runs on a background thread with an explicit TLS 1.2 handshake, so
    /// it (a) completes even though the calling code immediately tears the session
    /// down for the world switch, and (b) works on Valheim's Mono runtime, whose
    /// default cert handling otherwise rejects Discord's certificate.
    /// </summary>
    internal static class DiscordNotifier
    {
        private static bool _tlsConfigured;

        /// <summary>Announce a server → local-world switch (no-op if no webhook set).</summary>
        internal static void NotifyTravelToLocal(string playerName, string worldName, string fromServer)
        {
            var url = Plugin.Instance?.DiscordWebhookUrl?.Value;
            if (string.IsNullOrEmpty(url)) return;

            string who = string.IsNullOrEmpty(playerName) ? "A player" : playerName;
            string where = string.IsNullOrEmpty(worldName) ? "their local world" : $"their local world **{worldName}**";
            string from = string.IsNullOrEmpty(fromServer) ? "" : $" (from **{fromServer}**)";
            string content = $"\U0001F300 **{who}** stepped through a portal to {where}{from}.";

            Post(url, content);
        }

        private static void Post(string url, string content)
        {
            string json = "{\"content\":\"" + Escape(content) + "\"}";
            ThreadPool.QueueUserWorkItem(_ => Send(url, json));
        }

        private static void Send(string url, string json)
        {
            try
            {
                EnsureTls();

                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.UserAgent = "InterServerPortal";
                req.Timeout = 10000;

                byte[] body = Encoding.UTF8.GetBytes(json);
                req.ContentLength = body.Length;
                using (var stream = req.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }

                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    Plugin.Debug($"Discord travel notification sent ({(int)resp.StatusCode}).");
                }
            }
            catch (WebException we) when (we.Response is HttpWebResponse r)
            {
                string detail = "";
                try { using (var s = new StreamReader(r.GetResponseStream())) detail = s.ReadToEnd(); } catch { }
                Plugin.Log.LogWarning($"Discord notify failed: HTTP {(int)r.StatusCode} {detail}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Discord notify failed: {e.Message}");
            }
        }

        /// <summary>
        /// Force TLS 1.2 and accept Discord's certificate. Valheim's Mono ships
        /// without a usable CA root store, so the default validation rejects the
        /// handshake; a webhook URL is a low-risk, user-supplied endpoint.
        /// </summary>
        private static void EnsureTls()
        {
            if (_tlsConfigured) return;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback += (s, cert, chain, errors) => true;
            _tlsConfigured = true;
        }

        /// <summary>Minimal JSON string escaping for the message content.</summary>
        private static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
