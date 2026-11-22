using System;
using UnityEngine;

namespace Lumen.Runtime
{
    [Serializable] public sealed class GameOptions
    {
        public float MasterVolume = .8f, MusicVolume = .35f, EffectsVolume = .7f;
        public float MouseSensitivity = 1, FieldOfView = 62, InterfaceScale = 1;
        public bool InvertCamera, ReduceMotion, PetHelp = true, ShowNames = true;
        public void Normalize()
        {
            MasterVolume = Safe(MasterVolume, 0, 1, .8f); MusicVolume = Safe(MusicVolume, 0, 1, .35f); EffectsVolume = Safe(EffectsVolume, 0, 1, .7f);
            MouseSensitivity = Safe(MouseSensitivity, .4f, 2.5f, 1); FieldOfView = Safe(FieldOfView, 50, 85, 62); InterfaceScale = Safe(InterfaceScale, .85f, 1.2f, 1);
        }
        private static float Safe(float v, float min, float max, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, min, max);
    }
    public sealed partial class LumenGame
    {
        public AcademyAudio Audio { get; private set; }
        private void InitializeOptions()
        {
            Save.Options ??= new GameOptions(); Save.Options.Normalize();
            Audio = gameObject.AddComponent<AcademyAudio>(); Audio.Initialize(Save.Options); ApplyUserOptions();
        }
        public void ApplyUserOptions()
        {
            Save.Options ??= new GameOptions(); Save.Options.Normalize(); Audio?.Apply(Save.Options);
            if (ViewCamera && !(VR != null && VR.OwnsCamera)) ViewCamera.fieldOfView = Save.Options.FieldOfView;
            Hud?.ApplyInterfaceScale(Save.Options.InterfaceScale);
            foreach (var avatar in sharedAvatars.Values) if (avatar) avatar.SetNameVisible(Save.Options.ShowNames);
            foreach (var peer in peers) if (peer.Key && !peer.Key.IsOwner && peer.Value) peer.Value.SetNameVisible(Save.Options.ShowNames);
        }
    }
}
