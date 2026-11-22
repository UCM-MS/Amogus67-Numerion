using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Lumen.Networking
{
    /// <summary>60 Hz authoritative controller, owner prediction/replay, and 100 ms remote interpolation.</summary>
    [RequireComponent(typeof(NetworkObject), typeof(CharacterController))]
    public sealed partial class NetworkExplorer : NetworkBehaviour
    {
        public static event Action<NetworkExplorer> Spawned;
        public static event Action<NetworkExplorer> Despawned;
        private static readonly List<NetworkExplorer> players = new List<NetworkExplorer>();
        public static IReadOnlyList<NetworkExplorer> All => players;
        public event Action AppearanceChanged;
        public string DisplayName => displayName.Value.ToString();
        public NetworkAppearance Appearance => appearance.Value;
        public ulong SharedBeaconsMask => beaconsMask.Value;
        public int TeamShards => teamShards.Value;
        public int TeamContributors => contributors.Value;
        public bool IsGrounded => simulation.Grounded;
        public float Speed { get; private set; }
        public uint AuthoritativeTick => IsServer ? simulation.Tick : lastSnapshotTick;

        private readonly NetworkVariable<FixedString128Bytes> displayName = new NetworkVariable<FixedString128Bytes>(
            new FixedString128Bytes("Objaviteľ"), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkAppearance> appearance = new NetworkVariable<NetworkAppearance>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> beaconsMask = new NetworkVariable<ulong>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> teamShards = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> contributors = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly Queue<ExplorerInput> serverCommands = new Queue<ExplorerInput>();
        private readonly List<ExplorerInput> pendingInputs = new List<ExplorerInput>(128);
        private readonly List<ExplorerSnapshot> snapshots = new List<ExplorerSnapshot>(24);
        private CharacterController controller;
        private ExplorerSnapshot simulation;
        private ExplorerInput latestCommand;
        private Vector2 desiredMove;
        private float desiredYaw;
        private bool desiredSprint, jumpLatched;
        private uint nextInputSequence, lastReceivedSequence, lastSnapshotTick;
        private double lastInputAt, tokenUpdatedAt, appearanceUpdatedAt = -10, beaconRequestedAt = -10;
        private float inputTokens = 16f;
        private bool initialized;
        private int jumpRepeatTicks;

        private void Awake() => controller = GetComponent<CharacterController>();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            controller ??= GetComponent<CharacterController>();
            simulation = new ExplorerSnapshot { Position = transform.position, Yaw = transform.eulerAngles.y };
            // Remote objects are render-only, so their controllers do not obstruct local prediction.
            controller.enabled = IsServer || IsOwner;
            foreach (NetworkExplorer other in players)
                if (other != null && other.controller != null) Physics.IgnoreCollision(controller, other.controller, true);
            players.Add(this);
            displayName.OnValueChanged += OnNameChanged; appearance.OnValueChanged += OnAppearanceChanged;
            beaconsMask.OnValueChanged += OnTeamMaskChanged; teamShards.OnValueChanged += OnTeamShardsChanged;
            if (IsServer && NetworkSession.Instance != null)
            {
                displayName.Value = new FixedString128Bytes("Objaviteľ " + OwnerClientId);
                SetTeamProgress(NetworkSession.Instance.CollectedBeaconsMask, NetworkSession.Instance.TeamShards, NetworkSession.Instance.ServerContributorCount);
            }
            initialized = true;
            InitializeQuizServer();
            if (IsOwner) NetworkSession.Instance?.RegisterLocalPlayer(this);
            if (IsOwner) NetworkSession.Instance?.ObserveTeamProgress(beaconsMask.Value, teamShards.Value);
            Spawned?.Invoke(this); AppearanceChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            initialized = false; players.Remove(this);
            displayName.OnValueChanged -= OnNameChanged; appearance.OnValueChanged -= OnAppearanceChanged;
            beaconsMask.OnValueChanged -= OnTeamMaskChanged; teamShards.OnValueChanged -= OnTeamShardsChanged;
            pendingInputs.Clear(); snapshots.Clear(); serverCommands.Clear();
            if (IsOwner) NetworkSession.Instance?.UnregisterLocalPlayer(this);
            Despawned?.Invoke(this); base.OnNetworkDespawn();
        }

        /// <param name="move">Camera-relative strafe/forward input, normalized on the server.</param>
        public void SetInput(Vector2 move, float worldYawDegrees, bool jump, bool sprint = false)
        {
            if (!IsOwner) return;
            desiredMove = Vector2.ClampMagnitude(move, 1f);
            desiredYaw = NetworkMovement.IsFinite(worldYawDegrees) ? Mathf.Repeat(worldYawDegrees, 360f) : worldYawDegrees;
            desiredSprint = sprint; jumpLatched |= jump;
        }

        public void SendAppearance(string name, NetworkAppearance value)
        {
            if (!IsOwner || !IsSpawned) return;
            SubmitAppearanceRpc(new FixedString128Bytes(PlayerNamePolicy.Sanitize(name)), value.Sanitized());
        }

        private void FixedUpdate()
        {
            if (!initialized || !IsSpawned) return;
            Vector3 previousPosition = transform.position;
            if (IsOwner)
            {
                if (jumpLatched) jumpRepeatTicks = 3;
                var command = new ExplorerInput { Sequence = ++nextInputSequence, Move = desiredMove, Yaw = desiredYaw,
                    Jump = jumpRepeatTicks-- > 0, Sprint = desiredSprint };
                jumpRepeatTicks = Mathf.Max(0, jumpRepeatTicks);
                jumpLatched = false;
                if (IsServer) AcceptInput(command);
                else
                {
                    if (NetworkMovement.TrySanitize(command, out command))
                    {
                        pendingInputs.Add(command);
                        if (pendingInputs.Count > 180) pendingInputs.RemoveAt(0);
                        simulation = NetworkMovement.Simulate(controller, simulation, command);
                        SubmitInputRpc(command);
                    }
                }
            }
            if (IsServer)
            {
                if (serverCommands.Count > 0) latestCommand = serverCommands.Dequeue();
                else latestCommand.Jump = false;
                if (Time.realtimeSinceStartupAsDouble - lastInputAt > .35)
                { latestCommand.Move = Vector2.zero; latestCommand.Jump = false; }
                simulation = NetworkMovement.Simulate(controller, simulation, latestCommand);
                Vector3 p = simulation.Position;
                float bounds = NetworkSession.Instance != null ? NetworkSession.Instance.WorldBoundsHalfExtent : 84f;
                if (p.y < -20f || Mathf.Abs(p.x) > bounds || Mathf.Abs(p.z) > bounds)
                {
                    SetControllerPosition(NetworkSession.Instance != null ? NetworkSession.Instance.SpawnOrigin : Vector3.up * 2f);
                    simulation.Position = transform.position; simulation.VerticalSpeed = 0f; simulation.Grounded = false;
                }
                TrackServerCourse();
                simulation.Tick++; simulation.ServerTime = NetworkManager.ServerTime.Time;
                ReceiveSnapshotRpc(simulation);
            }
            Speed = new Vector2(transform.position.x - previousPosition.x, transform.position.z - previousPosition.z).magnitude / NetworkMovement.Step;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputRpc(ExplorerInput command, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            AcceptInput(command);
        }

        private void AcceptInput(ExplorerInput command)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            inputTokens = Mathf.Min(16f, inputTokens + (float)(now - tokenUpdatedAt) * 90f); tokenUpdatedAt = now;
            if (inputTokens < 1f) return;
            inputTokens -= 1f;
            if (!NetworkMovement.IsNewer(command.Sequence, lastReceivedSequence)
                || !NetworkMovement.TrySanitize(command, out command)) return;
            lastReceivedSequence = command.Sequence; lastInputAt = now;
            // At most one command is simulated per fixed step. Backlog cannot increase movement speed.
            while (serverCommands.Count >= 6) serverCommands.Dequeue();
            serverCommands.Enqueue(command);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
        private void ReceiveSnapshotRpc(ExplorerSnapshot snapshot)
        {
            if (IsServer || !NetworkMovement.IsNewer(snapshot.Tick, lastSnapshotTick)) return;
            lastSnapshotTick = snapshot.Tick;
            if (IsOwner)
            {
                // Rewind to the acknowledged authoritative position, then replay only unacknowledged input.
                pendingInputs.RemoveAll(input => !NetworkMovement.IsNewer(input.Sequence, snapshot.AcknowledgedInput));
                SetControllerPosition(snapshot.Position); transform.rotation = Quaternion.Euler(0f, snapshot.Yaw, 0f);
                simulation = snapshot;
                foreach (ExplorerInput command in pendingInputs) simulation = NetworkMovement.Simulate(controller, simulation, command);
            }
            else
            {
                snapshots.Add(snapshot);
                if (snapshots.Count > 24) snapshots.RemoveAt(0);
                simulation.Grounded = snapshot.Grounded;
            }
        }

        private void Update()
        {
            if (!initialized || IsServer || IsOwner || snapshots.Count == 0) return;
            double renderTime = NetworkManager.ServerTime.Time - .1;
            while (snapshots.Count >= 2 && snapshots[1].ServerTime <= renderTime) snapshots.RemoveAt(0);
            ExplorerSnapshot from = snapshots[0], to = snapshots.Count > 1 ? snapshots[1] : from;
            float blend = to.ServerTime > from.ServerTime ? Mathf.Clamp01((float)((renderTime - from.ServerTime) / (to.ServerTime - from.ServerTime))) : 1f;
            Vector3 previousPosition = transform.position;
            transform.SetPositionAndRotation(Vector3.Lerp(from.Position, to.Position, blend),
                Quaternion.Euler(0f, Mathf.LerpAngle(from.Yaw, to.Yaw, blend), 0f));
            Speed = new Vector2(transform.position.x - previousPosition.x, transform.position.z - previousPosition.z).magnitude / Mathf.Max(.001f, Time.deltaTime);
        }

        private void SetControllerPosition(Vector3 value)
        { controller.enabled = false; transform.position = value; controller.enabled = IsServer || IsOwner; }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitAppearanceRpc(FixedString128Bytes name, NetworkAppearance value, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - appearanceUpdatedAt < .5) return;
            appearanceUpdatedAt = now;
            string clean = NetworkSession.Instance != null ? NetworkSession.Instance.UniqueName(name.ToString(), this) : PlayerNamePolicy.Sanitize(name.ToString());
            displayName.Value = new FixedString128Bytes(clean); appearance.Value = value.Sanitized();
        }

        public void TryCollectBeacon(int index)
        { if (IsOwner && IsSpawned && index >= 0 && index < 32) CollectBeaconRpc(index); }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void CollectBeaconRpc(int index, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - beaconRequestedAt < .25) return;
            beaconRequestedAt = now;
            NetworkSession.Instance?.TryCollectBeacon(this, index);
        }

        internal void SetTeamProgress(ulong mask, int shards, int contributorCount)
        { if (IsServer) { beaconsMask.Value = mask; teamShards.Value = shards; contributors.Value = contributorCount; } }

        internal void BroadcastBeaconCollected(int index, string name, int total) => BeaconCollectedRpc(index, new FixedString128Bytes(name), total);

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void BeaconCollectedRpc(int index, FixedString128Bytes name, int total) => NetworkSession.Instance?.NotifyBeaconCollected(index, name.ToString(), total);

        private void OnNameChanged(FixedString128Bytes before, FixedString128Bytes after) => AppearanceChanged?.Invoke();
        private void OnAppearanceChanged(NetworkAppearance before, NetworkAppearance after) => AppearanceChanged?.Invoke();
        private void OnTeamMaskChanged(ulong before, ulong after) { if (IsOwner) NetworkSession.Instance?.ObserveTeamProgress(after, teamShards.Value); }
        private void OnTeamShardsChanged(int before, int after) { if (IsOwner) NetworkSession.Instance?.ObserveTeamProgress(beaconsMask.Value, after); }
    }
}
