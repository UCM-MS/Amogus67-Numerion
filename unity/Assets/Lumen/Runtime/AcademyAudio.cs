using System;
using UnityEngine;

namespace Lumen.Runtime
{
    /// <summary>Original synthesized ambient score and soft interface cues; no downloaded audio.</summary>
    public sealed class AcademyAudio : MonoBehaviour
    {
        private AudioSource music, effects;
        private static readonly double[] Notes = { 440, 554.365, 659.255, 880, 659.255, 554.365, 493.883, 659.255 };
        private AudioClip ambience, click, correct, incorrect;
        public void Initialize(GameOptions options)
        {
            music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.spatialBlend = 0;
            effects = gameObject.AddComponent<AudioSource>(); effects.spatialBlend = 0;
            ambience = Make("LUMEN / discovery ambience", 12, (t, duration) => {
                double envelope = Math.Pow(Math.Sin(Math.PI * t / duration), 2);
                double pad = Math.Sin(2 * Math.PI * 146.832 * t) + .6 * Math.Sin(2 * Math.PI * 220 * t) + .45 * Math.Sin(2 * Math.PI * 293.665 * t);
                int beat = (int)(t * 2) % 8; 
                double pulse = (t * 2) % 1; double bell = Math.Sin(2 * Math.PI * Notes[beat] * t) * Math.Exp(-pulse * 7) * .16;
                return (float)(envelope * (.025 * pad + bell * .13));
            });
            click = Tone("LUMEN / tap", .075f, 660, 880);
            correct = Tone("LUMEN / insight", .6f, 523.25f, 783.99f);
            incorrect = Tone("LUMEN / reflection", .28f, 261.63f, 220);
            music.clip = ambience; Apply(options); music.Play();
        }
        public void Apply(GameOptions options) { AudioListener.volume = options.MasterVolume; if (music) music.volume = options.MusicVolume; if (effects) effects.volume = options.EffectsVolume; }
        public void Click() { if (effects && click) effects.PlayOneShot(click, .25f); }
        public void Answer(bool success) { if (effects) effects.PlayOneShot(success ? correct : incorrect, .4f); }
        private static AudioClip Tone(string name, float seconds, float first, float second) => Make(name, seconds, (t, length) => {
            double phase = t / length; double f = phase < .45 ? first : second;
            return (float)(Math.Sin(2 * Math.PI * f * t) * Math.Sin(Math.PI * phase) * Math.Exp(-phase * 2) * .2);
        });
        private static AudioClip Make(string name, float seconds, Func<double,double,float> sample)
        {
            const int rate = 22050; var data = new float[(int)(seconds * rate)];
            for (int i = 0; i < data.Length; i++) data[i] = sample(i / (double)rate, seconds);
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        private void OnDestroy() { foreach (var clip in new[] { ambience, click, correct, incorrect }) if (clip) Destroy(clip); }
    }
}
