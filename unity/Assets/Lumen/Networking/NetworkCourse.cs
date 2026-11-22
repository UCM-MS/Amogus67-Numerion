using System;
using Unity.Netcode;
using UnityEngine;

namespace Lumen.Networking
{
    public sealed partial class NetworkExplorer
    {
        public event Action<float> CourseCompleted;
        public int CourseCheckpoint => courseCheckpoint.Value;
        public bool CourseActive => courseActive.Value;
        public float BestCourseSeconds => bestCourseSeconds.Value;
        private readonly NetworkVariable<int> courseCheckpoint = new NetworkVariable<int>(-1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> courseActive = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> bestCourseSeconds = new NetworkVariable<float>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private double courseStartedAt;
        private double lastCourseRequest = -10;

        public void StartCourse() { if (IsOwner && IsSpawned) StartCourseRpc(); }
        public void ReturnToCheckpoint(int index) { if (IsOwner && IsSpawned && index >= 0 && index < 24) ReturnToCheckpointRpc(index); }
        public void ResetToSpawn() { if (IsOwner && IsSpawned) ResetToSpawnRpc(); }

        private bool AcceptCourseRequest(ulong sender)
        {
            if (!IsServer || sender != OwnerClientId) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastCourseRequest < .75) return false;
            lastCourseRequest = now; return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void StartCourseRpc(RpcParams rpcParams = default)
        {
            if (!AcceptCourseRequest(rpcParams.Receive.SenderClientId) || NetworkSession.Instance == null
                || NetworkSession.Instance.CoursePositions.Count < 2) return;
            courseCheckpoint.Value = 0; courseActive.Value = true;
            courseStartedAt = Time.realtimeSinceStartupAsDouble;
            AuthoritativeTeleport(NetworkSession.Instance.CoursePositions[0] + Vector3.up * .1f);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ReturnToCheckpointRpc(int index, RpcParams rpcParams = default)
        {
            if (!AcceptCourseRequest(rpcParams.Receive.SenderClientId) || !courseActive.Value || NetworkSession.Instance == null
                || index < 0 || index > courseCheckpoint.Value || index >= NetworkSession.Instance.CoursePositions.Count) return;
            AuthoritativeTeleport(NetworkSession.Instance.CoursePositions[index] + Vector3.up * .1f);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ResetToSpawnRpc(RpcParams rpcParams = default)
        {
            if (!AcceptCourseRequest(rpcParams.Receive.SenderClientId) || NetworkSession.Instance == null) return;
            courseActive.Value = false; courseCheckpoint.Value = -1;
            AuthoritativeTeleport(NetworkSession.Instance.SpawnOrigin + Vector3.up * .1f);
        }

        private void AuthoritativeTeleport(Vector3 destination)
        {
            if (!IsServer) return;
            serverCommands.Clear(); latestCommand.Move = Vector2.zero; latestCommand.Jump = false;
            latestCommand.Sequence = lastReceivedSequence;
            SetControllerPosition(destination);
            simulation.Position = destination; simulation.VerticalSpeed = 0f; simulation.Grounded = false;
            simulation.AcknowledgedInput = lastReceivedSequence;
            simulation.ServerTime = NetworkManager.ServerTime.Time; simulation.Tick++;
            ReceiveSnapshotRpc(simulation);
        }

        private void TrackServerCourse()
        {
            if (!IsServer || !courseActive.Value || !simulation.Grounded || NetworkSession.Instance == null) return;
            int next = courseCheckpoint.Value + 1;
            var points = NetworkSession.Instance.CoursePositions;
            if (next >= points.Count) return;
            Vector3 difference = transform.position - points[next];
            if (difference.x * difference.x + difference.z * difference.z > 1.8f * 1.8f || Mathf.Abs(difference.y) > 1.5f) return;
            courseCheckpoint.Value = next;
            if (next != points.Count - 1) return;
            float seconds = (float)(Time.realtimeSinceStartupAsDouble - courseStartedAt);
            courseActive.Value = false;
            if (bestCourseSeconds.Value <= 0f || seconds < bestCourseSeconds.Value) bestCourseSeconds.Value = seconds;
            CourseCompletedRpc(seconds);
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void CourseCompletedRpc(float seconds) => CourseCompleted?.Invoke(seconds);
    }
}
