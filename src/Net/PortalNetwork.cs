using System;
using System.Collections.Generic;
using InterServerPortal.Portal;
using UnityEngine;

namespace InterServerPortal.Net
{
    /// <summary>One reachable portal in the player's same-world network.</summary>
    internal struct NetPortal
    {
        public ZDOID Id;
        public Vector3 Pos;
        public Quaternion Rot;
        public string Tag;
    }

    /// <summary>
    /// Same-world "portal network" backend (Phase 8). A network-mode portal meshes
    /// with every other network-mode portal the same player built; walking through
    /// one lists the others and teleports you there — an ordinary in-world teleport,
    /// no world switch. See docs/Feature-Portal-Network.md.
    ///
    /// The authoritative portal list is <see cref="ZDOMan.GetPortals"/>, which holds
    /// every portal ZDO in the world (loaded or not) — the same list vanilla uses to
    /// pair portals. When we're the world host (single-player / listen server) that
    /// list is complete locally; a remote client on a dedicated server asks the
    /// server for it over a routed RPC so distant portals aren't missed.
    /// </summary>
    internal static class PortalNetwork
    {
        private const string RpcRequest = "ISP_ReqNet";
        private const string RpcResponse = "ISP_RespNet";
        private const float RequestTimeout = 5f;

        private static readonly int LinkHash = "ISP.link".GetStableHashCode();

        // The ZRoutedRpc we registered on. Each connection (and every world switch)
        // creates a fresh instance with no handlers, so re-register when it changes.
        private static ZRoutedRpc _registeredOn;
        private static Action<List<NetPortal>> _pending;
        private static float _pendingSince;

        /// <summary>Register the routed RPCs once ZRoutedRpc exists (client + server),
        /// re-registering whenever the instance is replaced by a reconnect/switch.</summary>
        internal static void EnsureRegistered()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(rpc, _registeredOn)) return;
            rpc.Register<ZPackage>(RpcRequest, OnServerRequest);
            rpc.Register<ZPackage>(RpcResponse, OnClientResponse);
            _registeredOn = rpc;
            _pending = null; // any request from the previous connection is void
            Plugin.Debug("Portal-network RPCs registered.");
        }

        /// <summary>Drop a pending request that never got a reply (keeps travel from hanging).</summary>
        internal static void Tick()
        {
            if (_pending != null && Time.time - _pendingSince > RequestTimeout)
            {
                _pending = null;
                var p = Player.m_localPlayer;
                if (p != null) p.Message(MessageHud.MessageType.Center,
                    "Portal network unavailable (server did not respond).");
            }
        }

        /// <summary>
        /// Fetch the player's network portals (excluding <paramref name="source"/>)
        /// and hand them to <paramref name="onResult"/>. Resolves synchronously when
        /// we host the world, else round-trips the dedicated server.
        /// </summary>
        internal static void Request(TeleportWorld source, Action<List<NetPortal>> onResult)
        {
            var player = Player.m_localPlayer;
            if (player == null || source == null || source.m_nview == null) return;

            long playerId = player.GetPlayerID();
            ZDOID exclude = source.m_nview.GetZDO().m_uid;

            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                onResult(Enumerate(playerId, exclude)); // we hold the full ZDO set
                return;
            }

            EnsureRegistered();
            _pending = onResult;
            _pendingSince = Time.time;
            var pkg = new ZPackage();
            pkg.Write(playerId);
            pkg.Write(exclude);
            ZRoutedRpc.instance.InvokeRoutedRPC(RpcRequest, pkg); // → server
        }

        /// <summary>Server side: enumerate and reply to the requesting peer.</summary>
        private static void OnServerRequest(long sender, ZPackage pkg)
        {
            long playerId = pkg.ReadLong();
            ZDOID exclude = pkg.ReadZDOID();
            var list = Enumerate(playerId, exclude);

            var resp = new ZPackage();
            resp.Write(list.Count);
            foreach (var e in list)
            {
                resp.Write(e.Id);
                resp.Write(e.Pos);
                resp.Write(e.Rot);
                resp.Write(e.Tag ?? "");
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcResponse, resp);
        }

        /// <summary>Client side: parse the reply and fire the pending callback.</summary>
        private static void OnClientResponse(long sender, ZPackage pkg)
        {
            int count = pkg.ReadInt();
            var list = new List<NetPortal>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new NetPortal
                {
                    Id = pkg.ReadZDOID(),
                    Pos = pkg.ReadVector3(),
                    Rot = pkg.ReadQuaternion(),
                    Tag = pkg.ReadString(),
                });
            }
            var cb = _pending;
            _pending = null;
            cb?.Invoke(list);
        }

        /// <summary>
        /// All network-mode portals built by <paramref name="playerId"/>, minus the
        /// one being used. Reads straight from the portal ZDOs (creator + our link
        /// flag), so it spans the whole map regardless of what's loaded.
        /// </summary>
        internal static List<NetPortal> Enumerate(long playerId, ZDOID exclude)
        {
            var result = new List<NetPortal>();
            if (ZDOMan.instance == null) return result;

            foreach (var zdo in ZDOMan.instance.GetPortals())
            {
                if (zdo == null || !zdo.IsValid()) continue;
                if (zdo.m_uid == exclude) continue;
                if (zdo.GetLong(ZDOVars.s_creator, 0L) != playerId) continue;
                if (zdo.GetInt(LinkHash, PortalData.LinkTag) != PortalData.LinkNetwork) continue;

                result.Add(new NetPortal
                {
                    Id = zdo.m_uid,
                    Pos = zdo.GetPosition(),
                    Rot = zdo.GetRotation(),
                    Tag = zdo.GetString(ZDOVars.s_tag, ""),
                });
            }
            return result;
        }
    }
}
