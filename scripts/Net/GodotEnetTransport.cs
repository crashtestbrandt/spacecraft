// Klotho INetworkTransport over Godot's built-in ENet (ENetConnection / ENetPacketPeer).
//
// Replaces Klotho's default LiteNetLibTransport so the wire is ENet, the transport Godot ships and the
// one the Steam transport will sit beside later. Main-thread only: every call (Listen / Connect / Send /
// PollEvents) comes from GodotSessionDriver._Process, and ENetConnection is not thread-safe.
//
// Delivery mapping. ENet orders per channel, so each Klotho DeliveryMethod owns one channel — that
// keeps Send and Broadcast on a single per-peer ordered stream for the same method, which late-join
// relies on (see INetworkTransport.Send docs).
//
//   Klotho DeliveryMethod | ENet channel | ENet flags
//   ----------------------|--------------|----------------------------------------
//   ReliableOrdered       | 0            | FlagReliable
//   Sequenced             | 1            | (none) -> unreliable, drops stale packets
//   Unreliable            | 1            | FlagUnsequenced
//   Reliable              | 2            | FlagReliable (ENet has no reliable-unordered; own channel so
//                         |              | it never head-of-line blocks channel 0)
//
// Peer ids. Host: guests get the smallest free id from 0 (LiteNetLib's convention, which Klotho was
// written against); the host's own LocalPeerId is 0. Guest: the server peer is id 0 and LocalPeerId is 0.
//
// Disconnect payload. Klotho attaches one byte (a reject reason) to DisconnectPeer. ENet carries a
// 32-bit int on disconnect, so the byte travels as 0x100 | b; a data value without bit 8 set means
// "no payload" (-1), which keeps a payload byte of 0 distinguishable from none.
//
// Known gap: ENet reports no disconnect reason, so a remote drop is RemoteDisconnect whether the far
// side closed cleanly or timed out; only a failed connect attempt maps to NetworkFailure.
using System;
using System.Collections.Generic;
using System.Net;
using global::Godot;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;

namespace Spacecraft.Net
{
    public sealed class GodotEnetTransport : INetworkTransport
    {
        const int ChannelReliableOrdered = 0;
        const int ChannelUnreliable      = 1;
        const int ChannelReliable        = 2;
        const int ChannelCount           = 3;
        const int PayloadFlag            = 0x100;

        readonly IKLogger _logger;
        ENetConnection _host;
        bool _isServer;
        bool _isConnected;
        int  _localPeerId;
        bool _localDisconnectRequested;
        bool _everConnected;   // guest: distinguishes a failed connect from a later drop

        // peerId <-> ENetPacketPeer. Peers compare by native instance so the same wire peer always maps
        // to the same id even if Godot hands back a fresh managed wrapper.
        readonly Dictionary<int, ENetPacketPeer> _idToPeer = new();
        readonly Dictionary<IntPtr, int>         _nativeToId = new();
        readonly SortedSet<int>                  _freeIds = new();
        int _nextId;

        public GodotEnetTransport(IKLogger logger)
        {
            _logger = logger;
        }

        public bool IsServer    => _isServer;
        public bool IsConnected => _isConnected;
        public int  LocalPeerId => _localPeerId;

        public string RemoteAddress { get; private set; }
        public int    RemotePort    { get; private set; }

        int _lastDisconnectPayload = -1;
        public int LastDisconnectPayload => _lastDisconnectPayload;

        public event Action<int, byte[], int> OnDataReceived;
        public event Action<int>              OnPeerConnected;
        public event Action<int>              OnPeerDisconnected;
        public event Action                   OnConnected;
        public event Action<DisconnectReason> OnDisconnected;

        // ── INetworkTransport ──

        public bool Listen(string address, int port, int maxConnections)
        {
            TearDownHost();
            _isServer = true;
            _localPeerId = 0;
            if (maxConnections <= 0) maxConnections = 32;

            string bind = string.IsNullOrEmpty(address) ? "*" : address;
            var host = new ENetConnection();
            var err = host.CreateHostBound(bind, port, maxConnections, ChannelCount);
            if (err != Error.Ok)
            {
                _logger?.KError($"[EnetTransport] Listen failed on {bind}:{port} — {err} (port in use?)");
                return false;
            }
            _host = host;
            _logger?.KInformation($"[EnetTransport] Listening on {bind}:{port} (max {maxConnections} peers)");
            return true;
        }

        public bool Connect(string address, int port)
        {
            RemoteAddress = address;
            RemotePort = port;
            // A retried Connect (reconnect state machine) must drop the previous host so its socket and
            // any half-open peer go away instead of piling up on the server.
            TearDownHost();
            _isServer = false;
            _isConnected = false;
            _everConnected = false;
            _localDisconnectRequested = false;

            string ip = ResolveAddress(address);
            if (ip == null)
            {
                _logger?.KError($"[EnetTransport] Could not resolve host '{address}'");
                return false;
            }

            var host = new ENetConnection();
            var err = host.CreateHost(1, ChannelCount);
            if (err != Error.Ok)
            {
                _logger?.KError($"[EnetTransport] Client host create failed — {err}");
                return false;
            }
            var peer = host.ConnectToHost(ip, port, ChannelCount);
            if (peer == null)
            {
                host.Destroy();
                _logger?.KError($"[EnetTransport] ConnectToHost({ip}:{port}) returned null");
                return false;
            }
            _host = host;
            _logger?.KTrace($"[EnetTransport] Connecting to {address} ({ip}):{port}");
            return true;
        }

        public void Disconnect()
        {
            bool wasConnected = _isConnected;
            _localDisconnectRequested = true;
            TearDownHost();
            _isConnected = false;
            if (!_isServer && wasConnected)
                OnDisconnected?.Invoke(DisconnectReason.LocalDisconnect);
        }

        public void DisconnectPeer(int peerId)
        {
            if (_idToPeer.TryGetValue(peerId, out var peer))
                peer.PeerDisconnectLater(0);
        }

        public void DisconnectPeer(int peerId, byte[] data)
        {
            if (!_idToPeer.TryGetValue(peerId, out var peer)) return;
            int payload = (data != null && data.Length >= 1) ? (PayloadFlag | data[0]) : 0;
            // Later, not Now: the reject/notify message queued just before this must still go out.
            peer.PeerDisconnectLater(payload);
        }

        public IEnumerable<int> GetConnectedPeerIds() => _idToPeer.Keys;

        public void Send(int peerId, byte[] data, DeliveryMethod method)
            => Send(peerId, data, data.Length, method);

        public void Send(int peerId, byte[] data, int length, DeliveryMethod method)
        {
            if (!_idToPeer.TryGetValue(peerId, out var peer)) return;
            Map(method, out int channel, out int flags);
            peer.Send(channel, new ReadOnlySpan<byte>(data, 0, length), flags);
        }

        public void Broadcast(byte[] data, DeliveryMethod method)
            => Broadcast(data, data.Length, method);

        public void Broadcast(byte[] data, int length, DeliveryMethod method)
        {
            if (_host == null) return;
            Map(method, out int channel, out int flags);
            _host.Broadcast(channel, new ReadOnlySpan<byte>(data, 0, length), flags);
        }

        public void PollEvents()
        {
            if (_host == null) return;
            // Service returns one event per call; drain until it reports none.
            for (int guard = 0; guard < 4096; guard++)
            {
                if (_host == null) return;   // a handler tore the host down
                var ev = _host.Service(0);
                var type = (ENetConnection.EventType)(int)ev[0];
                if (type == ENetConnection.EventType.None) return;
                if (type == ENetConnection.EventType.Error)
                {
                    _logger?.KError($"[EnetTransport] ENet service error");
                    return;
                }
                var peer = ev[1].As<ENetPacketPeer>();
                int data = (int)ev[2];
                switch (type)
                {
                    case ENetConnection.EventType.Connect:    HandleConnect(peer);          break;
                    case ENetConnection.EventType.Disconnect: HandleDisconnect(peer, data); break;
                    case ENetConnection.EventType.Receive:    HandleReceive(peer);          break;
                }
            }
        }

        public void FlushSendQueue() => _host?.Flush();

        // ── ENet event handlers ──

        void HandleConnect(ENetPacketPeer peer)
        {
            int peerId = Register(peer);
            if (peerId < 0) return;
            _logger?.KInformation($"[EnetTransport] Peer {peerId} connected: {peer.GetRemoteAddress()}:{peer.GetRemotePort()}");
            if (!_isServer)
            {
                _localPeerId = peerId;
                _isConnected = true;
                _everConnected = true;
                OnConnected?.Invoke();
            }
            OnPeerConnected?.Invoke(peerId);
        }

        void HandleDisconnect(ENetPacketPeer peer, int data)
        {
            bool known = TryGetId(peer, out int peerId);
            if (known)
            {
                Unregister(peer, peerId);
                _logger?.KWarning($"[EnetTransport] Peer {peerId} disconnected (data=0x{data:X})");
                OnPeerDisconnected?.Invoke(peerId);
            }
            if (_isServer) return;

            _isConnected = false;
            _lastDisconnectPayload = (data & PayloadFlag) != 0 ? (data & 0xFF) : -1;
            DisconnectReason reason;
            if (_localDisconnectRequested)        reason = DisconnectReason.LocalDisconnect;
            else if (!_everConnected)             reason = DisconnectReason.NetworkFailure;   // connect attempt failed
            else if (_lastDisconnectPayload >= 0) reason = DisconnectReason.ConnectionRejected;
            else                                  reason = DisconnectReason.RemoteDisconnect;
            OnDisconnected?.Invoke(reason);
            _lastDisconnectPayload = -1;
        }

        void HandleReceive(ENetPacketPeer peer)
        {
            if (!TryGetId(peer, out int peerId)) return;
            while (peer.GetAvailablePacketCount() > 0)
            {
                byte[] packet = peer.GetPacket();
                if (packet == null || packet.Length == 0) continue;
                OnDataReceived?.Invoke(peerId, packet, packet.Length);
            }
        }

        // ── peer map ──

        int Register(ENetPacketPeer peer)
        {
            IntPtr key = peer.NativeInstance;
            if (_nativeToId.ContainsKey(key))
            {
                _logger?.KError($"[EnetTransport] Peer already registered");
                return -1;
            }
            int id;
            if (_freeIds.Count > 0) { id = _freeIds.Min; _freeIds.Remove(id); }
            else id = _nextId++;
            _idToPeer[id] = peer;
            _nativeToId[key] = id;
            return id;
        }

        void Unregister(ENetPacketPeer peer, int id)
        {
            _idToPeer.Remove(id);
            _nativeToId.Remove(peer.NativeInstance);
            _freeIds.Add(id);
        }

        bool TryGetId(ENetPacketPeer peer, out int id) => _nativeToId.TryGetValue(peer.NativeInstance, out id);

        void TearDownHost()
        {
            if (_host != null)
            {
                foreach (var p in _idToPeer.Values) p.PeerDisconnectNow(0);
                _host.Destroy();
                _host = null;
            }
            _idToPeer.Clear();
            _nativeToId.Clear();
            _freeIds.Clear();
            _nextId = 0;
        }

        static void Map(DeliveryMethod method, out int channel, out int flags)
        {
            switch (method)
            {
                case DeliveryMethod.Unreliable: channel = ChannelUnreliable;      flags = (int)ENetPacketPeer.FlagUnsequenced; break;
                case DeliveryMethod.Sequenced:  channel = ChannelUnreliable;      flags = 0;                                    break;
                case DeliveryMethod.Reliable:   channel = ChannelReliable;        flags = (int)ENetPacketPeer.FlagReliable;    break;
                default:                        channel = ChannelReliableOrdered; flags = (int)ENetPacketPeer.FlagReliable;    break;
            }
        }

        static string ResolveAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;
            if (IPAddress.TryParse(address, out _)) return address;
            string resolved = IP.ResolveHostname(address);
            return string.IsNullOrEmpty(resolved) ? null : resolved;
        }
    }
}
