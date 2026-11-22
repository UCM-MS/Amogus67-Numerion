using System;
using UnityEngine;

namespace Lumen.Networking
{
    /// <summary>The same capsule and contact response are used by solo, shared Web and native simulation.</summary>
    public static class ExplorerPhysics
    {
        public const float Height = 1.8f;
        public const float Radius = .3f;
        public const float SkinWidth = .05f;
        public const float StepOffset = .3f;
        public const float SlopeLimit = 50f;
        public const float GroundStickSpeed = -2f;
        public const float TerminalSpeed = -36f;

        public static void Configure(CharacterController controller)
        {
            if (!controller) throw new ArgumentNullException(nameof(controller));
            controller.height = Height;
            controller.center = Vector3.up * (Height * .5f);
            controller.radius = Radius;
            controller.skinWidth = SkinWidth;
            controller.stepOffset = StepOffset;
            controller.slopeLimit = SlopeLimit;
            controller.minMoveDistance = 0;
            controller.detectCollisions = true;
            controller.enableOverlapRecovery = true;
        }

        public static float ResolveVerticalSpeed(float speed, CollisionFlags contacts, bool grounded)
        {
            // A ceiling consumes upward momentum immediately; otherwise a jump sticks against its underside.
            if (speed > 0 && (contacts & CollisionFlags.Above) != 0) return 0;
            if (speed < 0 && (grounded || (contacts & CollisionFlags.Below) != 0)) return GroundStickSpeed;
            return Mathf.Max(TerminalSpeed, speed);
        }
    }
}
