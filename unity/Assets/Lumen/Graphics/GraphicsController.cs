using System;
using Lumen.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Lumen.Graphics
{
    public enum RenderMode { Raster, RayTracing, PathTracing }

    /// <summary>Native HDRP effects. A selected mode is accepted only when its actual runtime prerequisites are present.</summary>
    public sealed class GraphicsController : MonoBehaviour
    {
        public RenderMode Mode { get; private set; } = RenderMode.Raster;
        public bool IsPhotoMode => Mode == RenderMode.PathTracing;
        public event Action<bool> PhotoModeChanged;
        public string LastStatus { get; private set; } = "HDRP · rasterizácia";
        public const int PhotoSamples = 256;

        private Volume volume;
        private VolumeProfile profile;
        private PathTracing pathTracing;
        private ScreenSpaceReflection reflections;
        private GlobalIllumination illumination;
        private HDAdditionalCameraData cameraData;
        private Camera gameCamera;
        private RenderMode previousGameplayMode = RenderMode.Raster;
        private Light sun;

        public static GraphicsController Create(Camera camera)
        {
            if (!camera) throw new ArgumentNullException(nameof(camera));
            var holder = new GameObject("LUMEN / HDRP graphics");
            var controller = holder.AddComponent<GraphicsController>();
            controller.Initialize(camera);
            return controller;
        }

        private void Initialize(Camera camera)
        {
            gameCamera = camera;
            cameraData = camera.GetComponent<HDAdditionalCameraData>() ?? camera.gameObject.AddComponent<HDAdditionalCameraData>();
            cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            cameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            cameraData.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
            cameraData.dithering = true;
            cameraData.stopNaNs = true;
            cameraData.volumeLayerMask = 1;
            cameraData.customRenderingSettings = true;
            SetCameraFrameFeature(FrameSettingsField.RayTracing, false);
            SetCameraFrameFeature(FrameSettingsField.SSR, true);
            SetCameraFrameFeature(FrameSettingsField.SSGI, false);
            SetCameraFrameFeature(FrameSettingsField.SSAO, true);

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 50; volume.weight = 1;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Lumen runtime HDRP profile";
            volume.sharedProfile = profile;

            var environment = profile.Add<VisualEnvironment>(true);
            environment.skyType.Override(3); // GradientSky's documented SkyUniqueID.
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            var sky = profile.Add<GradientSky>(true);
            sky.top.Override(new Color(0.22f, 0.49f, 0.72f));
            sky.middle.Override(new Color(0.61f, 0.8f, 0.87f));
            sky.bottom.Override(new Color(0.87f, 0.86f, 0.79f));
            sky.gradientDiffusion.Override(1.3f);
            sky.skyIntensityMode.Override(SkyIntensityMode.Lux);
            sky.desiredLuxValue.Override(18000);

            var exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(13.2f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.14f);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.62f);
            var occlusion = profile.Add<ScreenSpaceAmbientOcclusion>(true);
            occlusion.intensity.Override(0.85f);
            occlusion.radius.Override(1.1f);
            occlusion.directLightingStrength.Override(0.12f);
            occlusion.temporalAccumulation.Override(true);
            occlusion.rayTracing.Override(false);
            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(8);
            color.saturation.Override(4);
            var fog = profile.Add<Fog>(true);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(false);
            fog.meanFreePath.Override(170);
            fog.baseHeight.Override(-18);
            fog.maximumHeight.Override(10);
            fog.maxFogDistance.Override(500);
            fog.tint.Override(new Color(0.74f, 0.85f, 0.9f));

            reflections = profile.Add<ScreenSpaceReflection>(true);
            reflections.enabled.Override(true);
            reflections.tracing.Override(RayCastingMode.RayMarching);
            reflections.reflectSky.Override(true);
            reflections.minSmoothness = 0.55f;
            reflections.smoothnessFadeStart = 0.7f;
            illumination = profile.Add<GlobalIllumination>(true);
            illumination.enable.Override(false);
            illumination.tracing.Override(RayCastingMode.RayMarching);
            pathTracing = profile.Add<PathTracing>(true);
            pathTracing.enable.Override(false);
            pathTracing.maximumSamples.Override(PhotoSamples);
            pathTracing.minimumDepth.Override(1);
            pathTracing.maximumDepth.Override(8);
            pathTracing.maximumIntensity.Override(10);
            pathTracing.layerMask.Override(~0);
            var raySettings = profile.Add<RayTracingSettings>(true);
            raySettings.rayBias.Override(0.001f);
            raySettings.cullingDistance.Override(180);
            raySettings.extendCameraCulling.Override(true);

            var lightObject = new GameObject("Lumen / Afternoon sun");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(43, -32, 0);
            sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, 0.94f, 0.82f);
            sun.lightUnit = LightUnit.Lux;
            sun.intensity = 55000;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.25f;
            lightObject.AddComponent<HDAdditionalLightData>();
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1;
            LastStatus = "HDRP · rasterizácia · TAA · odrazy v obraze";
        }

        public bool RayTracingAvailable(out string reason)
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive)
            { reason = "VR používa HDRP rasterizáciu. DXR a fotorežim môžeš zapnúť po návrate na monitor."; return false; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            { reason = "Server bez grafického zariadenia."; return false; }
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
            { reason = "Táto HDRP verzia používa ray tracing na Windows s DirectX 12."; return false; }
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
            { reason = "Ray tracing vyžaduje DirectX 12. Spusť hru s -force-d3d12."; return false; }
            if (!SystemInfo.supportsRayTracing)
            { reason = "Grafická karta alebo jej ovládač nepodporuje hardvérový ray tracing (DXR)."; return false; }
            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (!asset)
            { reason = "Aktívny renderovací pipeline nie je HDRP."; return false; }
            var settings = asset.currentPlatformRenderPipelineSettings;
            if (!settings.supportRayTracing)
            { reason = "Ray tracing nie je povolený v aktívnom HDRP nastavení projektu."; return false; }
            if (!(RenderPipelineManager.currentPipeline is HDRenderPipeline pipeline))
            { reason = "HDRP sa ešte inicializuje. Skús režim zapnúť po prvom vykreslenom snímku."; return false; }
            if (!pipeline.rayTracingSupported)
            { reason = "HDRP nepovolil ray tracing pre aktuálnu kombináciu grafického zariadenia, ovládača a nastavenia."; return false; }
            reason = "DXR · " + SystemInfo.graphicsDeviceName;
            return true;
        }

        public bool TrySetMode(RenderMode mode, out string message)
        {
            if (!Enum.IsDefined(typeof(RenderMode), mode))
            { message = "Neznámy grafický režim."; return false; }
            if (mode != RenderMode.Raster && !RayTracingAvailable(out message)) return false;
            if (mode == RenderMode.RayTracing)
            {
                var asset = (HDRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                var settings = asset.currentPlatformRenderPipelineSettings;
                if (!settings.supportSSR || !settings.supportSSGI)
                { message = "Aktívny HDRP profil nemá povolené odrazy a globálne osvetlenie."; return false; }
            }
            bool wasPhoto = IsPhotoMode;
            if (mode == RenderMode.PathTracing && !wasPhoto) previousGameplayMode = Mode;
            Mode = mode;
            pathTracing.enable.Override(mode == RenderMode.PathTracing);
            bool rayGameplay = mode == RenderMode.RayTracing;
            reflections.enabled.Override(mode != RenderMode.PathTracing);
            reflections.tracing.Override(rayGameplay ? RayCastingMode.RayTracing : RayCastingMode.RayMarching);
            illumination.enable.Override(rayGameplay);
            illumination.tracing.Override(rayGameplay ? RayCastingMode.RayTracing : RayCastingMode.RayMarching);
            if (rayGameplay)
            {
                var settings = ((HDRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).currentPlatformRenderPipelineSettings;
                var quality = settings.supportedRayTracingMode == RenderPipelineSettings.SupportedRayTracingMode.Performance
                    ? RayTracingMode.Performance : RayTracingMode.Quality;
                reflections.mode.Override(quality);
                illumination.mode.Override(quality);
                reflections.sampleCount.Override(1);
                illumination.sampleCount.Override(1);
                reflections.rayLength = 80;
                illumination.rayLength = 60;
                reflections.denoise = true;
                illumination.denoise = true;
            }
            SetCameraFrameFeature(FrameSettingsField.RayTracing, mode != RenderMode.Raster);
            SetCameraFrameFeature(FrameSettingsField.SSR, mode != RenderMode.PathTracing);
            SetCameraFrameFeature(FrameSettingsField.SSGI, rayGameplay);
            cameraData.antialiasing = mode == RenderMode.PathTracing
                ? HDAdditionalCameraData.AntialiasingMode.None
                : HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            WorldAnimation.Frozen = IsPhotoMode;
            if (RenderPipelineManager.currentPipeline is HDRenderPipeline pipeline) pipeline.ResetPathTracing();
            if (wasPhoto != IsPhotoMode) PhotoModeChanged?.Invoke(IsPhotoMode);
            message = mode == RenderMode.PathTracing
                ? "Path tracing · fotorežim · 256 vzoriek · pohyb a animácie pozastavené; pripojenie zostáva aktívne."
                : rayGameplay ? "DXR aktívny · ray-traced odrazy a globálne osvetlenie."
                : "HDRP · rasterizácia · TAA · odrazy v obraze";
            LastStatus = message;
            return true;
        }

        public bool ExitPhotoMode(out string message) => TrySetMode(previousGameplayMode, out message);

        public void ResetPhotoAccumulation()
        { if (RenderPipelineManager.currentPipeline is HDRenderPipeline pipeline) pipeline.ResetPathTracing(); }

        private void SetCameraFrameFeature(FrameSettingsField field, bool enabled)
        {
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
        }

        private void OnDestroy()
        {
            WorldAnimation.Frozen = false;
            if (profile)
            {
                foreach (var component in profile.components) if (component) Destroy(component);
                Destroy(profile);
            }
        }
    }
}
