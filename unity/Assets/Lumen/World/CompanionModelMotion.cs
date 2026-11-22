using System.Collections.Generic;
using UnityEngine;

namespace Lumen.World
{
    public sealed class CompanionModelMotion : MonoBehaviour
    {
        private struct SwayPart { public Transform Transform; public Quaternion Rest; public Vector3 Axis; public float Amplitude, Speed, Phase; }
        private readonly List<SwayPart> parts = new List<SwayPart>(8);

        public void Sway(Transform target, Vector3 axis, float amplitude, float speed, float phase)
            => parts.Add(new SwayPart { Transform = target, Rest = target.localRotation, Axis = axis, Amplitude = amplitude, Speed = speed, Phase = phase });

        private void Update()
        {
            if (WorldAnimation.Frozen) return;
            float clock = Time.time;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part.Transform) part.Transform.localRotation = part.Rest * Quaternion.AngleAxis(Mathf.Sin(clock * part.Speed + part.Phase) * part.Amplitude, part.Axis);
            }
        }
    }
}
