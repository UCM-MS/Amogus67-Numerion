using System.Collections.Generic;
using UnityEngine;

namespace Lumen.World
{
    /// <summary>One clock drives the garden's small kinetic exhibits, without per-frame allocations.</summary>
    public sealed class MathExhibitMotion : MonoBehaviour
    {
        private struct Piece
        {
            public Transform Target;
            public Vector3 Position, Axis;
            public Quaternion Rotation;
            public float Sway, Bob, Speed, Phase, Spin;
        }
        private readonly List<Piece> pieces = new List<Piece>(12);
        private float clock;

        public void Add(Transform target, Vector3 axis, float sway, float bob, float speed, float phase = 0, float spin = 0)
        {
            pieces.Add(new Piece { Target = target, Position = target.localPosition, Rotation = target.localRotation,
                Axis = axis, Sway = sway, Bob = bob, Speed = speed, Phase = phase, Spin = spin });
        }

        private void Update()
        {
            if (WorldAnimation.Frozen) return;
            clock += Time.deltaTime;
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (!piece.Target) continue;
                float wave = Mathf.Sin(clock * piece.Speed + piece.Phase);
                piece.Target.localPosition = piece.Position + Vector3.up * (wave * piece.Bob);
                piece.Target.localRotation = piece.Rotation * Quaternion.AngleAxis(wave * piece.Sway + clock * piece.Spin, piece.Axis);
            }
        }
    }
}
