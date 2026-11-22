using UnityEngine;

namespace Lumen.Runtime
{
    /// <summary>Rendering follows the display independently of the fixed network/physics clock.</summary>
    public static class NativePerformance
    {
        public static void Apply(GameSave save)
        {
            QualitySettings.vSyncCount = save.VSync ? 1 : 0;
            Application.targetFrameRate = save.FrameLimit > 0 ? save.FrameLimit : -1;
            QualitySettings.globalTextureMipmapLimit = save.TextureSize >= 4096 ? 0 : 1;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.lodBias = save.TextureSize >= 4096 ? 1.6f : 1f;
        }
    }
}
