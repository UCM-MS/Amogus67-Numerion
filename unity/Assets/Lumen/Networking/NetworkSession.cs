using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Lumen.Networking
{
    public enum SessionState { Offline, Connecting, Host, Client, DedicatedServer, Disconnecting }

    /// <summary>Direct UDP LAN session. Does not need Unity Services or Relay account credentials.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSession : MonoBehaviour
    {
        public const int MaxPlayers = 16;
        public const ushort DefaultPort = 7777;
        public const string Protocol = "LUMEN-NATIVE-2";
        public static NetworkSession Instance { get; private set; }
        public event Action<string> StatusChanged;
        public event Action<NetworkExplorer> LocalPlayerReady;
        public event Action<int, string, int> BeaconCollected;
        public SessionState State { get; private set; } = SessionState.Offline;
        public string Status { get; private set; } = "Hráš sólo";
        public NetworkExplorer LocalPlayer { get; private set; }
        public NetworkManager Manager { get; private set; }
        public NetworkAppearance PendingAppearance { get; private set; }
        public string PendingName { get; private set; } = "Objaviteľ";
        public float WorldBoundsHalfExtent = 84f;
        public Vector3 SpawnOrigin { get; private set; } = new Vector3(0f, 0f, 12f);
        public int TickRate => Manager == null ? NetworkMovement.SimulationRate : (int)Manager.NetworkConfig.TickRate;
        public int PlayerCount => Manager != null && Manager.IsListening ? NetworkExplorer.All.Count : 1;
        public ulong PingMs => Manager != null && Manager.IsClient && !Manager.IsServer && transport != null
            ? transport.GetCurrentRtt(NetworkManager.ServerClientId) : 0;
        public int TeamShards { get; private set; }
        internal int ServerContributorCount => contributors.Count;
        public ulong CollectedBeaconsMask { get; private set; }
        public IReadOnlyList<Vector3> BeaconPositions => beaconPositions;
        public IReadOnlyList<Vector3> CoursePositions => coursePositions;

        private UnityTransport transport;
        private GameObject configuredPrefab;
        private bool commandLineChecked;
        private Vector3[] beaconPositions = {
            new Vector3(8f,0f,12f), new Vector3(-34f,0f,0f), new Vector3(34f,0f,0f),
            new Vector3(-20f,0f,-34f), new Vector3(20f,0f,-34f), new Vector3(0f,0f,40f)
        };
        private readonly HashSet<ulong> contributors = new HashSet<ulong>();
        private Vector3[] coursePositions = Array.Empty<Vector3>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Manager = GetComponent<NetworkManager>() ?? gameObject.AddComponent<NetworkManager>();
            // Runtime AddComponent does not receive the Editor's serialized NetworkConfig.
            Manager.NetworkConfig ??= new NetworkConfig();
            transport = GetComponent<UnityTransport>() ?? gameObject.AddComponent<UnityTransport>();
            Manager.NetworkConfig.NetworkTransport = transport;
            Manager.NetworkConfig.TickRate = NetworkMovement.SimulationRate;
            Manager.NetworkConfig.ConnectionApproval = true;
            Manager.NetworkConfig.EnableSceneManagement = false;
            Manager.NetworkConfig.EnsureNetworkVariableLengthSafety = true;
            Manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Protocol);
            Manager.ConnectionApprovalCallback += ApproveConnection;
            Manager.OnClientConnectedCallback += OnConnected;
            Manager.OnClientDisconnectCallback += OnDisconnected;
            Manager.OnServerStarted += OnServerStarted;
        }

        public void Configure(GameObject playerPrefab, int tickRate = NetworkMovement.SimulationRate, Vector3? spawnOrigin = null)
        {
            if (Manager.IsListening) throw new InvalidOperationException("Configure the network before starting a session.");
            if (playerPrefab == null || playerPrefab.GetComponent<NetworkObject>() == null
                || playerPrefab.GetComponent<NetworkExplorer>() == null || playerPrefab.GetComponent<CharacterController>() == null)
                throw new ArgumentException("Use the Editor-created NetworkExplorer prefab with a registered NetworkObject hash.");
            configuredPrefab = playerPrefab;
            if (spawnOrigin.HasValue) SpawnOrigin = spawnOrigin.Value;
            // Fixed simulation/prediction deliberately share one immutable protocol rate.
            Manager.NetworkConfig.TickRate = NetworkMovement.SimulationRate;
            Time.fixedDeltaTime = NetworkMovement.Step;
            Manager.NetworkConfig.PlayerPrefab = playerPrefab;
            Manager.AddNetworkPrefab(playerPrefab);
            if (tickRate != NetworkMovement.SimulationRate)
                Debug.LogWarning("LUMEN simulation uses 60 Hz; the requested tick-rate override was ignored.");
        }

        public void ConfigureBeacons(Vector3[] registeredWorldPositions)
        {
            if (Manager != null && Manager.IsListening) throw new InvalidOperationException("Register beacon locations before connecting.");
            if (registeredWorldPositions == null || registeredWorldPositions.Length == 0 || registeredWorldPositions.Length > 32)
                throw new ArgumentException("Register between 1 and 32 fixed beacon locations.");
            beaconPositions = (Vector3[])registeredWorldPositions.Clone();
        }

        public void ConfigureCourse(Vector3[] fixedCheckpointFootPositions)
        {
            if (Manager != null && Manager.IsListening) throw new InvalidOperationException("Register checkpoints before connecting.");
            if (fixedCheckpointFootPositions == null || fixedCheckpointFootPositions.Length < 2 || fixedCheckpointFootPositions.Length > 24)
                throw new ArgumentException("Register between 2 and 24 fixed checkpoint foot positions.");
            foreach (Vector3 point in fixedCheckpointFootPositions)
                if (!NetworkMovement.IsFinite(point.x) || !NetworkMovement.IsFinite(point.y) || !NetworkMovement.IsFinite(point.z))
                    throw new ArgumentException("Checkpoint coordinates must be finite.");
            coursePositions = (Vector3[])fixedCheckpointFootPositions.Clone();
        }

        public bool StartHost(string playerName, NetworkAppearance appearance, ushort port = DefaultPort)
        {
            if (!Prepare(playerName, appearance, port)) return false;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            State = SessionState.Host;
            bool started = Manager.StartHost();
            if (!started) { State = SessionState.Offline; SetStatus("Hostiteľa sa nepodarilo spustiť."); }
            else SetStatus($"Hostiteľ · UDP {port} · 60 Hz · až {MaxPlayers} hráčov");
            return started;
        }

        public bool Join(string address, string playerName, NetworkAppearance appearance, ushort port = DefaultPort)
        {
            if (string.IsNullOrWhiteSpace(address) || address.Length > 253) { SetStatus("Zadaj IP adresu hostiteľa."); return false; }
            address = address.Trim();
            if (!IPAddress.TryParse(address, out IPAddress endpoint))
            { SetStatus("Zadaj číselnú IPv4 alebo IPv6 adresu hostiteľa (napr. 192.168.1.25)."); return false; }
            // Unity Transport parses numeric endpoints; normalize forms accepted by IPAddress first.
            address = endpoint.ToString();
            if (!Prepare(playerName, appearance, port)) return false;
            transport.SetConnectionData(address, port);
            State = SessionState.Connecting;
            SetStatus($"Pripájam na {address}:{port}…");
            bool started = Manager.StartClient();
            if (!started) { State = SessionState.Offline; SetStatus("Pripojenie sa nepodarilo spustiť."); }
            return started;
        }

        public bool StartDedicatedServer(ushort port = DefaultPort)
        {
            if (!Prepare("Server", default, port)) return false;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            State = SessionState.DedicatedServer;
            bool started = Manager.StartServer();
            if (!started) { State = SessionState.Offline; SetStatus("Server sa nepodarilo spustiť."); }
            else SetStatus($"Dedikovaný server · UDP {port} · 60 Hz · {MaxPlayers} miest");
            return started;
        }

        public void SendAppearance(string playerName, NetworkAppearance appearance)
        {
            PendingName = PlayerNamePolicy.Sanitize(playerName); PendingAppearance = appearance.Sanitized();
            if (LocalPlayer != null) LocalPlayer.SendAppearance(PendingName, PendingAppearance);
        }

        public void Disconnect()
        {
            if (Manager == null || !Manager.IsListening) { State = SessionState.Offline; SetStatus("Hráš sólo"); return; }
            State = SessionState.Disconnecting;
            Manager.Shutdown(); LocalPlayer = null; TeamShards = 0; CollectedBeaconsMask = 0;
            State = SessionState.Offline; SetStatus("Odpojené · hráš sólo");
        }

        public void TryStartFromCommandLine()
        {
            if (commandLineChecked) return; commandLineChecked = true;
            // NGO registers generated message handlers in AfterSceneLoad. The world calls
            // this from Awake, so wait until those runtime initializers have completed.
            StartCoroutine(StartCommandLineAfterInitialization());
        }

        private IEnumerator StartCommandLineAfterInitialization()
        {
            yield return null;
            string[] args = Environment.GetCommandLineArgs();
            ushort port = DefaultPort;
            string portText = Argument(args, "-port");
            if (portText != null && (!ushort.TryParse(portText, out port) || port == 0))
            { SetStatus("Parameter -port musí byť v rozsahu 1–65535."); yield break; }
            string name = Argument(args, "-name") ?? "Objaviteľ";
            if (Array.IndexOf(args, "-lumen-server") >= 0) StartDedicatedServer(port);
            else if (Array.IndexOf(args, "-lumen-host") >= 0) StartHost(name, default, port);
            else if (Argument(args, "-lumen-connect") is string address) Join(address, name, default, port);
        }

        private static string Argument(string[] args, string key)
        { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }

        private bool Prepare(string playerName, NetworkAppearance appearance, ushort port)
        {
            if (configuredPrefab == null) { SetStatus("Chýba sieťový prefab. Spusti LUMEN → Build Academy v Editore."); return false; }
            if (Manager.IsListening || Manager.ShutdownInProgress) { SetStatus("Najprv odpoj aktuálnu reláciu."); return false; }
            if (port == 0) { SetStatus("Port musí byť v rozsahu 1–65535."); return false; }
            PendingName = PlayerNamePolicy.Sanitize(playerName); PendingAppearance = appearance.Sanitized();
            Time.fixedDeltaTime = NetworkMovement.Step;
            TeamShards = 0; CollectedBeaconsMask = 0; contributors.Clear();
            return true;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool protocol = request.Payload != null && request.Payload.Length <= 64
                && Encoding.UTF8.GetString(request.Payload) == Protocol;
            bool hasCapacity = Manager.ConnectedClientsIds.Count < MaxPlayers;
            response.Approved = protocol && hasCapacity;
            response.CreatePlayerObject = response.Approved;
            response.Position = SpawnOrigin + new Vector3((request.ClientNetworkId % 4) * 1.6f - 2.4f, .15f,
                ((request.ClientNetworkId / 4) % 4) * 1.6f);
            response.Rotation = Quaternion.identity;
            response.Pending = false;
            response.Reason = response.Approved ? "" : !protocol ? "Server vyžaduje rovnakú verziu LUMEN." : "Server je plný (16 hráčov).";
        }

        private void OnConnected(ulong id)
        {
            if (id == Manager.LocalClientId && Manager.IsClient)
            { State = Manager.IsHost ? SessionState.Host : SessionState.Client; SetStatus(Manager.IsHost ? "Hostiteľ · 60 Hz" : "Pripojené · 60 Hz"); }
        }

        private void OnDisconnected(ulong id)
        {
            if (id != Manager.LocalClientId) return;
            LocalPlayer = null;
            if (State != SessionState.Disconnecting)
            { State = SessionState.Offline; SetStatus(string.IsNullOrEmpty(Manager.DisconnectReason) ? "Spojenie ukončené · hráš sólo" : Manager.DisconnectReason); }
        }

        private void OnServerStarted() => Debug.Log($"LUMEN server listening at 60 Hz; player capacity {MaxPlayers}.");
        private void SetStatus(string message) { Status = message; StatusChanged?.Invoke(message); }

        internal void RegisterLocalPlayer(NetworkExplorer explorer)
        {
            LocalPlayer = explorer; explorer.SendAppearance(PendingName, PendingAppearance);
            LocalPlayerReady?.Invoke(explorer);
        }

        internal void UnregisterLocalPlayer(NetworkExplorer explorer) { if (LocalPlayer == explorer) LocalPlayer = null; }

        internal string UniqueName(string desired, NetworkExplorer sender)
        {
            desired = PlayerNamePolicy.Sanitize(desired);
            foreach (NetworkExplorer peer in NetworkExplorer.All)
                if (peer != sender && string.Equals(peer.DisplayName, desired, StringComparison.OrdinalIgnoreCase))
                {
                    var elements = System.Globalization.StringInfo.GetTextElementEnumerator(desired);
                    var prefix = new StringBuilder(); int count = 0;
                    while (elements.MoveNext() && count++ < 10) prefix.Append(elements.GetTextElement());
                    return PlayerNamePolicy.Sanitize(prefix + "#" + sender.OwnerClientId);
                }
            return desired;
        }

        internal bool TryCollectBeacon(NetworkExplorer collector, int index)
        {
            if (!Manager.IsServer || index < 0 || index >= beaconPositions.Length
                || (CollectedBeaconsMask & (1UL << index)) != 0) return false;
            Vector3 offset = collector.transform.position - beaconPositions[index];
            if (offset.x * offset.x + offset.z * offset.z > 3.5f * 3.5f || Mathf.Abs(offset.y) > 5f) return false;
            CollectedBeaconsMask |= 1UL << index; TeamShards += 10; contributors.Add(collector.OwnerClientId);
            foreach (NetworkExplorer peer in NetworkExplorer.All)
                peer.SetTeamProgress(CollectedBeaconsMask, TeamShards, contributors.Count);
            collector.BroadcastBeaconCollected(index, collector.DisplayName, TeamShards);
            return true;
        }

        internal void ObserveTeamProgress(ulong mask, int shards)
        { CollectedBeaconsMask = mask; TeamShards = shards; }
        internal void NotifyBeaconCollected(int index, string name, int total) => BeaconCollected?.Invoke(index, name, total);

        private void OnDestroy()
        {
            if (Manager != null)
            {
                Manager.ConnectionApprovalCallback -= ApproveConnection;
                Manager.OnClientConnectedCallback -= OnConnected;
                Manager.OnClientDisconnectCallback -= OnDisconnected;
                Manager.OnServerStarted -= OnServerStarted;
            }
            if (Instance == this) Instance = null;
        }
    }
}
