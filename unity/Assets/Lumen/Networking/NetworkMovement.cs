using System;
using Unity.Netcode;
using UnityEngine;

namespace Lumen.Networking
{
    public struct ExplorerInput : INetworkSerializable
    {
        public uint Sequence;
        public Vector2 Move;
        public float Yaw;
        public bool Jump;
        public bool Sprint;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence); serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Yaw); serializer.SerializeValue(ref Jump); serializer.SerializeValue(ref Sprint);
        }
    }

    public struct ExplorerSnapshot : INetworkSerializable
    {
        public uint Tick;
        public double ServerTime;
        public uint AcknowledgedInput;
        public Vector3 Position;
        public float Yaw;
        public float VerticalSpeed;
        public bool Grounded;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick); serializer.SerializeValue(ref ServerTime); serializer.SerializeValue(ref AcknowledgedInput);
            serializer.SerializeValue(ref Position); serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref VerticalSpeed); serializer.SerializeValue(ref Grounded);
        }
    }

    public static class NetworkMovement
    {
        public const int SimulationRate = 60;
        public const float Step = 1f / SimulationRate;
        public const float WalkSpeed = 6.2f;
        public const float SprintSpeed = 8.5f;
        public const float Gravity = -22f;
        public const float JumpSpeed = 7.2f;

        public static bool IsNewer(uint incoming, uint previous) => unchecked((int)(incoming - previous)) > 0;
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool TrySanitize(ExplorerInput input, out ExplorerInput sanitized)
        {
            sanitized = input;
            if (!IsFinite(input.Move.x) || !IsFinite(input.Move.y) || !IsFinite(input.Yaw)
                || Mathf.Abs(input.Yaw) > 100000f || input.Move.sqrMagnitude > 4f) return false;
            sanitized.Move = Vector2.ClampMagnitude(input.Move, 1f);
            sanitized.Yaw = Mathf.Repeat(input.Yaw, 360f);
            return true;
        }

        public static ExplorerSnapshot Simulate(CharacterController controller, ExplorerSnapshot state, ExplorerInput command)
        {
            bool grounded = controller.isGrounded || (state.Grounded && state.VerticalSpeed <= 0f);
            float vertical = grounded && state.VerticalSpeed < 0f ? -2f : state.VerticalSpeed;
            if (command.Jump && grounded) vertical = JumpSpeed;
            vertical = Mathf.Max(vertical + Gravity * Step, -36f);
            Vector3 direction = Quaternion.Euler(0f, command.Yaw, 0f) * new Vector3(command.Move.x, 0f, command.Move.y);
            Vector3 motion = direction * (command.Sprint ? SprintSpeed : WalkSpeed);
            motion.y = vertical;
            CollisionFlags flags = controller.Move(motion * Step);
            if ((flags & CollisionFlags.Above) != 0 && vertical > 0f) vertical = 0f;
            state.Position = controller.transform.position;
            state.Grounded = (flags & CollisionFlags.Below) != 0 || controller.isGrounded;
            state.VerticalSpeed = state.Grounded && vertical < 0f ? -2f : vertical;
            if (command.Move.sqrMagnitude > .001f)
                state.Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            state.AcknowledgedInput = command.Sequence;
            controller.transform.rotation = Quaternion.Euler(0f, state.Yaw, 0f);
            return state;
        }
    }
}
