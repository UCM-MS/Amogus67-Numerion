using UnityEngine;

namespace Lumen.Runtime
{
    /// <summary>The browser schedules frames at the display cadence; native FPS/VSync preferences remain in the shared save.</summary>
    public static class NativePerformance
    {
        public static void Apply(GameSave save)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            QualitySettings.globalTextureMipmapLimit = save.TextureSize >= 4096 ? 0 : 1;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.lodBias = save.TextureSize >= 4096 ? 1.6f : 1f;
        }
    }
}
