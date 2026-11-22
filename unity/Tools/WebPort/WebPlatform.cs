using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Lumen.Core;

namespace Lumen.Runtime
{
    /// <summary>Browser saves use PlayerPrefs, which Unity persists in IndexedDB.</summary>
    public static class WebProfileStorage
    {
        private const string SaveKey = "lumen-unity-web-v1";
        public static bool HasSave => PlayerPrefs.HasKey(SaveKey);
        public static string Read() => PlayerPrefs.GetString(SaveKey, "");
        public static void Write(string json)
        {
            string previous = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(previous)) PlayerPrefs.SetString(SaveKey + ".backup", previous);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }
    }

    public sealed class LumenWebPlatform : MonoBehaviour
    {
        public static string MobileQualityStatus { get; private set; } = "";
        public static bool TouchViewport { get; internal set; }
        public static float CssViewportWidth { get; internal set; }
        public static float CssViewportHeight { get; internal set; }
        private bool keyboardFocus, keyboardChoiceReceived;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void LumenWebReady(string detail);
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { new GameObject("LUMEN Web Platform").AddComponent<LumenWebPlatform>(); }
        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = keyboardChoiceReceived && keyboardFocus;
            LumenWebReady(JsonUtility.ToJson(new WebReady { engine = Application.unityVersion, curriculumQuestions = ContentCatalog.CreateDefault().Questions.Count, companions = CompanionRules.Species.Length, platform = "Unity WebGL 2", network = "shared HTTPS", cloudProfiles = true }));
#endif
        }
        public void SetMobileProfile(string quality)
        {
            var game = FindFirstObjectByType<LumenGame>();
            if (game != null && !game.Save.HasCreatedProfile) game.Save.TextureSize = 2048;
            // Start conservatively on phones; existing explicit 4K preferences are preserved.
            if (game == null || game.Save.TextureSize <= 2048)
            {
                QualitySettings.globalTextureMipmapLimit = quality == "low" ? 2 : 1;
                MobileQualityStatus = quality == "low" ? "Mobil · automatický 1K detail na úsporu pamäte; predvoľba textúr 2K." : "Mobil · úsporný 2K detail. Plné 4K mapy zostávajú dostupné v nastaveniach.";
            }
            else MobileQualityStatus = "Mobil · zachovaná tvoja voľba plných 4K textúr; vyššia spotreba pamäte.";
            QualitySettings.shadowDistance = 42;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.antiAliasing = 0;
            QualitySettings.lodBias = .85f;
        }
        public void SaveNow()
        {
            var game = FindFirstObjectByType<LumenGame>();
            if (game != null) ProfileStore.Save(game.Save);
        }
        public void SetKeyboardFocus(string value)
        {
            keyboardChoiceReceived = true; keyboardFocus = value == "1";
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = keyboardFocus && Application.isFocused;
#endif
        }
        private void OnApplicationFocus(bool focused)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = focused && keyboardChoiceReceived && keyboardFocus;
#endif
        }
        [Serializable] private sealed class WebReady { public string engine; public int curriculumQuestions, companions; public string platform, network; public bool cloudProfiles; }
    }
}
