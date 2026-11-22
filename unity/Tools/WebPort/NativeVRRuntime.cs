using System;
using UnityEngine;

namespace Lumen.Runtime
{
    /// <summary>OpenXR is available in the native player. The WebGL player does not claim WebXR support.</summary>
    public sealed class NativeVRRuntime : MonoBehaviour
    {
        public bool Active => false;
        public bool Busy => false;
        public string Status => "Táto Unity Web verzia sa hrá na obrazovke. PC VR cez OpenXR je dostupné vo Windows verzii.";
        public event Action<bool> ActiveChanged;
        public Vector2 Move => Vector2.zero;
        public float HeadYaw => 0;
        public bool HeadTracked => false;
        public bool OwnsCamera => false;
        private LumenGame game;
        public void Initialize(LumenGame owner) { game = owner; }
        public bool ConsumeJump() => false;
        public void StartXR() { game?.Notify(Status); }
        public void StopXR() { }
    }
}
