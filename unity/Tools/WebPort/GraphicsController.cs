using System;
using Lumen.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lumen.Graphics
{
    public enum RenderMode { Raster, RayTracing, PathTracing }

    /// <summary>The browser projection uses the Built-in forward renderer, independently of native HDRP.</summary>
    public sealed class GraphicsController : MonoBehaviour
    {
        public RenderMode Mode => RenderMode.Raster;
        public bool IsPhotoMode => false;
        public event Action<bool> PhotoModeChanged;
        public string LastStatus { get; private set; } = "Unity Web · WebGL 2 · tiene · materiály s normálovými mapami";
        public const int PhotoSamples = 256;

        public static GraphicsController Create(Camera camera)
        {
            if (!camera) throw new ArgumentNullException(nameof(camera));
            var holder = new GameObject("LUMEN / Unity Web graphics");
            var graphics = holder.AddComponent<GraphicsController>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.64f, .81f, .88f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.renderingPath = RenderingPath.Forward;
            QualitySettings.antiAliasing = 2;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 90;
            QualitySettings.shadowCascades = 2;
            QualitySettings.softParticles = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.59f, .72f, .8f);
            RenderSettings.ambientEquatorColor = new Color(.56f, .59f, .58f);
            RenderSettings.ambientGroundColor = new Color(.3f, .33f, .35f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.73f, .84f, .88f);
            RenderSettings.fogDensity = .0038f;

            var lightObject = new GameObject("Lumen / Afternoon sun");
            lightObject.transform.SetParent(holder.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(43, -32, 0);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, .94f, .83f);
            sun.intensity = 1.18f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .7f;
            sun.shadowBias = .045f;
            sun.shadowNormalBias = .3f;
            RenderSettings.sun = sun;
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader)
            {
                var sky = new Material(skyShader) { name = "Lumen / Web sky" };
                sky.SetFloat("_SunSize", .035f);
                sky.SetFloat("_AtmosphereThickness", .8f);
                sky.SetColor("_SkyTint", new Color(.51f, .62f, .71f));
                sky.SetColor("_GroundColor", new Color(.69f, .76f, .77f));
                sky.SetFloat("_Exposure", 1.1f);
                RenderSettings.skybox = sky;
            }
            return graphics;
        }

        public bool RayTracingAvailable(out string reason)
        { reason = "Webová verzia používa WebGL 2. Ray tracing a path tracing sú dostupné vo Windows verzii."; return false; }

        public bool TrySetMode(RenderMode mode, out string message)
        {
            if (mode != RenderMode.Raster) return RayTracingAvailable(out message);
            WorldAnimation.Frozen = false;
            message = LastStatus;
            return true;
        }
        public bool ExitPhotoMode(out string message) => TrySetMode(RenderMode.Raster, out message);
        public void ResetPhotoAccumulation() { }
    }
}
