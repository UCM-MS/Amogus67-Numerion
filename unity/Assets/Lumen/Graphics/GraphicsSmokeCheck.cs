using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Lumen.Graphics
{
    /// <summary>Opt-in smoke check against the real native player and graphics device. Inactive during ordinary play.</summary>
    public sealed class GraphicsSmokeCheck : MonoBehaviour
    {
        [Serializable]
        public sealed class TextureSample
        {
            public string Name;
            public int Width, Height, MipCount, Anisotropy;
            public string Format, GraphicsFormat;
            public bool Readable;
        }

        [Serializable]
        public sealed class Report
        {
            public string Utc, UnityVersion, ApplicationVersion, Platform;
            public string DeviceName, DeviceVersion, DeviceType;
            public int GraphicsMemoryMb, ShaderLevel, MaxHardwareTextureSize;
            public bool GraphicsAvailable, DeviceSupportsRayTracing, PipelineSupportsRayTracing;
            public string PipelineAsset, PipelineImplementation, RenderMode;
            public string RequestedRenderMode, RenderModeRequestReason;
            public bool RenderModeRequestAccepted;
            public float PipelineReadyAfterStartupSeconds, RenderModeRequestedAfterStartupSeconds;
            public bool AssetRayTracingEnabled, AssetReflectionsEnabled, AssetGlobalIlluminationEnabled;
            public string AssetRayTracingMode;
            public int Width, Height, TargetFrameRate, VSyncCount, TextureMipmapLimit;
            public double DisplayRefreshHz;
            public float PhysicsStepSeconds, ObservedFramesPerSecond, MeanFrameMilliseconds, P95FrameMilliseconds;
            public int FrameSampleCount;
            public string FullScreenMode, AnisotropicFiltering;
            public bool MainCameraPresent, CameraRayTracingOverride;
            public TextureSample[] LoadedSurfaceTextures;
            public int ErrorCount;
            public string[] Errors;
            public string ScreenshotPath, ScreenshotStatus;
        }

        private readonly List<string> errors = new List<string>();
        private readonly List<float> frameSamples = new List<float>(1024);
        private float started;
        private int errorCount;
        private string outputPath;
        private bool quitAfter;
        private string requestedRenderMode = "Raster";
        private bool renderModeRequestAccepted, renderModeSelectionComplete;
        private string renderModeRequestReason = "The render-mode request has not been attempted.";
        private float pipelineReadyAfterStartup, renderModeRequestedAfterStartup, modeAppliedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartWhenRequested()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!HasFlag(arguments, "-lumen-graphics-smoke")) return;
            if (FindFirstObjectByType<GraphicsSmokeCheck>() != null) return;
            var obj = new GameObject("Lumen / Opt-in graphics smoke check");
            DontDestroyOnLoad(obj);
            var check = obj.AddComponent<GraphicsSmokeCheck>();
            check.quitAfter = HasFlag(arguments, "-lumen-smoke-quit");
            string mode = ArgumentValue(arguments, "-lumen-smoke-render-mode");
            if (!string.IsNullOrWhiteSpace(mode)) check.requestedRenderMode = mode;
            string requested = ArgumentValue(arguments, "-lumen-smoke-output");
            try
            {
                check.outputPath = string.IsNullOrWhiteSpace(requested)
                    ? Path.Combine(Application.persistentDataPath, "lumen-graphics-smoke.json")
                    : Path.GetFullPath(requested);
                if (Directory.Exists(check.outputPath)) check.outputPath = Path.Combine(check.outputPath, "lumen-graphics-smoke.json");
            }
            catch (Exception exception)
            {
                check.outputPath = Path.Combine(Application.persistentDataPath, "lumen-graphics-smoke.json");
                check.errors.Add("Invalid requested output path: " + exception.Message);
                check.errorCount++;
            }
            check.started = Time.realtimeSinceStartup;
            Application.logMessageReceived += check.OnLog;
            check.StartCoroutine(check.Capture());
        }

        private void Update()
        {
            // Skip asset startup and observe the most recent frames, independently of the configured FPS cap.
            if (!renderModeSelectionComplete || Time.realtimeSinceStartup - modeAppliedAt < 2 ||
                Time.realtimeSinceStartup - started < 6 || frameSamples.Count >= 4096) return;
            float duration = Time.unscaledDeltaTime;
            if (duration > 0 && duration < 5) frameSamples.Add(duration);
        }

        private IEnumerator Capture()
        {
            // Mode changes are deliberately delayed until HDRP exists, then for three more seconds.
            // Pipeline discovery has a deadline so a broken initialization still produces a report.
            float pipelineDeadline = started + 6;
            while (!(RenderPipelineManager.currentPipeline is HDRenderPipeline) && Time.realtimeSinceStartup < pipelineDeadline)
                yield return null;
            if (RenderPipelineManager.currentPipeline is HDRenderPipeline)
            {
                pipelineReadyAfterStartup = Time.realtimeSinceStartup - started;
                yield return new WaitForSecondsRealtime(3);
                SelectRequestedRenderMode();
            }
            else
            {
                renderModeRequestReason = "HDRP did not initialize within six seconds; the render-mode request was not applied.";
                errors.Add(renderModeRequestReason); errorCount++;
                renderModeSelectionComplete = true;
            }
            // Cold shader initialization can consume the startup window. Always observe five
            // seconds of steady rendering after the two-second post-mode warmup.
            float captureAt = Mathf.Max(started + 14, modeAppliedAt + 7);
            float remaining = captureAt - Time.realtimeSinceStartup;
            if (remaining > 0) yield return new WaitForSecondsRealtime(remaining);
            var report = ReadReport();
            if (!report.MainCameraPresent) { errors.Add("The game did not create its main camera."); errorCount++; }
            if (report.GraphicsAvailable && !(Application.isEditor && Application.isBatchMode))
            {
                // Read the player's completed framebuffer, including its own UI; no desktop pixels are accessed.
                bool completed = false;
                Coroutine capture = StartCoroutine(CaptureAfterFrame(report, () => completed = true));
                float deadline = Time.realtimeSinceStartup + 4;
                while (!completed && Time.realtimeSinceStartup < deadline) yield return null;
                if (!completed)
                {
                    StopCoroutine(capture);
                    report.ScreenshotStatus = "Capture timed out: the player did not complete a rendered frame within four seconds.";
                    errors.Add(report.ScreenshotStatus); errorCount++;
                }
            }
            else report.ScreenshotStatus = report.GraphicsAvailable
                ? "Skipped: the Editor does not execute WaitForEndOfFrame in batch mode."
                : "Skipped: no real graphics device was initialized.";

            report.ErrorCount = errorCount;
            report.Errors = errors.ToArray();
            try
            {
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(outputPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("LUMEN_GRAPHICS_SMOKE_SAVED: " + outputPath);
            }
            catch (Exception exception)
            {
                Debug.LogError("LUMEN_GRAPHICS_SMOKE_SAVE_FAILED: " + exception.Message);
                if (quitAfter) Application.Quit(2);
                yield break;
            }
            Application.logMessageReceived -= OnLog;
            if (quitAfter) Application.Quit(report.ErrorCount == 0 ? 0 : 1);
            Destroy(gameObject);
        }

        private void SelectRequestedRenderMode()
        {
            renderModeRequestedAfterStartup = Time.realtimeSinceStartup - started;
            modeAppliedAt = Time.realtimeSinceStartup;
            frameSamples.Clear();
            try
            {
                if (!Enum.TryParse<RenderMode>(requestedRenderMode, true, out var requested) ||
                    !Enum.IsDefined(typeof(RenderMode), requested))
                    renderModeRequestReason = "Unknown render mode. Use Raster, RayTracing or PathTracing.";
                else
                {
                    requestedRenderMode = requested.ToString();
                    var controller = FindFirstObjectByType<GraphicsController>();
                    if (!controller) renderModeRequestReason = "No GraphicsController exists in the running scene.";
                    else renderModeRequestAccepted = controller.TrySetMode(requested, out renderModeRequestReason);
                }
            }
            catch (Exception exception)
            { renderModeRequestReason = "Mode switch failed: " + exception.GetType().Name + ": " + exception.Message; }
            renderModeSelectionComplete = true;
            if (!renderModeRequestAccepted) { errors.Add(renderModeRequestReason); errorCount++; }
            Debug.Log("LUMEN_GRAPHICS_SMOKE_MODE: requested=" + requestedRenderMode + ", accepted=" +
                renderModeRequestAccepted + ", reason=" + renderModeRequestReason);
        }

        private IEnumerator CaptureAfterFrame(Report report, Action completed)
        {
            yield return new WaitForEndOfFrame();
            CaptureScreenshot(report);
            completed();
        }

        private Report ReadReport()
        {
            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            var pipeline = RenderPipelineManager.currentPipeline as HDRenderPipeline;
            var controller = FindFirstObjectByType<GraphicsController>();
            var camera = Camera.main;
            var cameraData = camera ? camera.GetComponent<HDAdditionalCameraData>() : null;
            var report = new Report {
                Utc = DateTime.UtcNow.ToString("O"), UnityVersion = Application.unityVersion,
                ApplicationVersion = Application.version, Platform = Application.platform.ToString(),
                DeviceName = SystemInfo.graphicsDeviceName, DeviceVersion = SystemInfo.graphicsDeviceVersion,
                DeviceType = SystemInfo.graphicsDeviceType.ToString(), GraphicsMemoryMb = SystemInfo.graphicsMemorySize,
                ShaderLevel = SystemInfo.graphicsShaderLevel, MaxHardwareTextureSize = SystemInfo.maxTextureSize,
                GraphicsAvailable = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null,
                DeviceSupportsRayTracing = SystemInfo.supportsRayTracing,
                PipelineSupportsRayTracing = pipeline != null && pipeline.rayTracingSupported,
                PipelineAsset = asset ? asset.name : "None",
                PipelineImplementation = RenderPipelineManager.currentPipeline == null ? "None" : RenderPipelineManager.currentPipeline.GetType().FullName,
                RenderMode = controller ? controller.Mode.ToString() : "No graphics controller",
                RequestedRenderMode = requestedRenderMode, RenderModeRequestAccepted = renderModeRequestAccepted,
                RenderModeRequestReason = renderModeRequestReason,
                PipelineReadyAfterStartupSeconds = pipelineReadyAfterStartup,
                RenderModeRequestedAfterStartupSeconds = renderModeRequestedAfterStartup,
                Width = Screen.width, Height = Screen.height, TargetFrameRate = Application.targetFrameRate,
                VSyncCount = QualitySettings.vSyncCount, TextureMipmapLimit = QualitySettings.globalTextureMipmapLimit,
                DisplayRefreshHz = Screen.currentResolution.refreshRateRatio.value,
                PhysicsStepSeconds = Time.fixedDeltaTime, FrameSampleCount = frameSamples.Count,
                FullScreenMode = Screen.fullScreenMode.ToString(),
                AnisotropicFiltering = QualitySettings.anisotropicFiltering.ToString(),
                MainCameraPresent = camera != null,
                CameraRayTracingOverride = cameraData != null && cameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.RayTracing)
            };
            if (asset)
            {
                var settings = asset.currentPlatformRenderPipelineSettings;
                report.AssetRayTracingEnabled = settings.supportRayTracing;
                report.AssetReflectionsEnabled = settings.supportSSR;
                report.AssetGlobalIlluminationEnabled = settings.supportSSGI;
                report.AssetRayTracingMode = settings.supportedRayTracingMode.ToString();
            }
            float seconds = 0;
            foreach (float sample in frameSamples) seconds += sample;
            if (frameSamples.Count > 0 && seconds > 0)
            {
                report.ObservedFramesPerSecond = frameSamples.Count / seconds;
                report.MeanFrameMilliseconds = seconds / frameSamples.Count * 1000;
                var sorted = frameSamples.ToArray(); Array.Sort(sorted);
                report.P95FrameMilliseconds = sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Length * .95f) - 1, 0, sorted.Length - 1)] * 1000;
            }
            var textures = new List<TextureSample>();
            foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                string name = texture.name;
                if (!(name.StartsWith("grass-", StringComparison.Ordinal) || name.StartsWith("stone-", StringComparison.Ordinal) ||
                    name.StartsWith("wood-", StringComparison.Ordinal) || name.StartsWith("metal-", StringComparison.Ordinal))) continue;
                textures.Add(new TextureSample { Name = name, Width = texture.width, Height = texture.height,
                    MipCount = texture.mipmapCount, Anisotropy = texture.anisoLevel, Format = texture.format.ToString(),
                    GraphicsFormat = texture.graphicsFormat.ToString(), Readable = texture.isReadable });
            }
            textures.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            report.LoadedSurfaceTextures = textures.ToArray();
            return report;
        }

        private void CaptureScreenshot(Report report)
        {
            Texture2D screenshot = null;
            try
            {
                string path = Path.ChangeExtension(outputPath, ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false);
                screenshot.Apply(false, false);
                File.WriteAllBytes(path, screenshot.EncodeToPNG());
                report.ScreenshotPath = path;
                report.ScreenshotStatus = "Captured from the completed native player framebuffer.";
            }
            catch (Exception exception)
            {
                report.ScreenshotStatus = "Capture failed: " + exception.Message;
                errors.Add(report.ScreenshotStatus); errorCount++;
            }
            finally { if (screenshot) Destroy(screenshot); }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errorCount++;
            if (errors.Count < 12) errors.Add(type + ": " + (condition.Length > 1200 ? condition.Substring(0, 1200) : condition));
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;

        private static bool HasFlag(string[] arguments, string flag)
        { foreach (string argument in arguments) if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase)) return true; return false; }

        private static string ArgumentValue(string[] arguments, string flag)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], flag, StringComparison.OrdinalIgnoreCase)) return i + 1 < arguments.Length ? arguments[i + 1] : null;
                if (arguments[i].StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase)) return arguments[i].Substring(flag.Length + 1);
            }
            return null;
        }
    }
}
