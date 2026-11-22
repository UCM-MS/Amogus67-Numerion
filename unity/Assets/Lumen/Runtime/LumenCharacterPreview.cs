using System.Collections.Generic;
using Lumen.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if !UNITY_WEBGL
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Lumen.Runtime
{
    /// <summary>A temporary, isolated rendering of the real explorer model; never an in-world player.</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class LumenCharacterPreview : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const int PreviewLayer = 31;
        private GameObject stage;
        private AvatarVisual avatar;
        private Camera previewCamera;
        private RenderTexture target;
        private RawImage surface;
        private float yaw;
        private int dragPointer = int.MinValue;
        private readonly Dictionary<Light, int> sceneLightMasks = new Dictionary<Light, int>();
#if !UNITY_WEBGL
        private VolumeProfile previewVolumeProfile;
#endif
        public AvatarAppearance DraftAppearance => avatar?.Appearance?.Copy();
        public float Rotation => yaw;
        public bool HasRenderingTarget => target != null;

        public void Initialize(AvatarAppearance appearance)
        {
            Release();
            surface = GetComponent<RawImage>(); surface.raycastTarget = true;
            stage = new GameObject("Numerion temporary character preview") { hideFlags = HideFlags.DontSave, layer = PreviewLayer };
            stage.transform.position = new Vector3(0, -12000, 0);
            avatar = AvatarFactory.Create("Náhľad postavy", appearance?.Copy());
            avatar.transform.SetParent(stage.transform, false); avatar.SetNameVisible(false);
            SetLayer(avatar.transform);
            // Exclude the reserved preview layer from existing lights, then restore their exact masks on close.
            // Local preview lights cannot illuminate the game world.
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light && light.gameObject.scene.IsValid()) { sceneLightMasks[light] = light.cullingMask; light.cullingMask &= ~(1 << PreviewLayer); }
            var cameraObject = new GameObject("Character preview camera") { layer = PreviewLayer };
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(.1f, 1.25f, 5);
            cameraObject.transform.LookAt(stage.transform.position + new Vector3(0, 1.05f, 0));
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.orthographic = true; previewCamera.orthographicSize = 1.25f;
            previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 12;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.055f, .075f, .09f, 1);
            previewCamera.allowHDR = true; previewCamera.allowMSAA = false; previewCamera.depth = -10;
#if !UNITY_WEBGL
            var cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
            cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            cameraData.backgroundColorHDR = previewCamera.backgroundColor;
            cameraData.volumeLayerMask = 1 << PreviewLayer;
            cameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
            cameraData.customRenderingSettings = true;
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.RayTracing] = true;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.RayTracing, false);
            previewVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            previewVolumeProfile.name = "Temporary character preview exposure";
            var volume = stage.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 1000; volume.sharedProfile = previewVolumeProfile;
            var exposure = previewVolumeProfile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(7.3f);
#endif
            AddLight("Portrait key", new Vector3(-2.5f, 3.5f, 3), new Color(1, .94f, .83f), true);
            AddLight("Portrait fill", new Vector3(2.3f, 2, 1.3f), new Color(.66f, .8f, 1), false);
            ResetRotation(); UpdateTarget();
        }

        private void AddLight(string title, Vector3 position, Color color, bool key)
        {
            var holder = new GameObject(title) { layer = PreviewLayer }; holder.transform.SetParent(stage.transform, false);
            holder.transform.localPosition = position;
            var light = holder.AddComponent<Light>(); light.type = LightType.Point; light.range = 12;
            light.cullingMask = 1 << PreviewLayer; light.color = color; light.shadows = LightShadows.None;
#if UNITY_WEBGL
            light.intensity = key ? 2.4f : 1.1f;
#else
            light.lightUnit = LightUnit.Candela; light.intensity = key ? 2400 : 900;
            holder.AddComponent<HDAdditionalLightData>();
#endif
        }

        public void SetPlayerName(string value) { if (avatar) avatar.SetName(value); }
        public void SetAppearance(AvatarAppearance appearance)
        {
            if (!avatar) return;
            avatar.ApplyAppearance(appearance?.Copy()); avatar.SetNameVisible(false); SetLayer(avatar.transform);
        }
        public void Rotate(float degrees)
        {
            yaw = Mathf.Repeat(yaw + degrees + 180, 360) - 180;
            if (avatar) avatar.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }
        public void ResetRotation() { yaw = 0; Rotate(0); }
        public void OnBeginDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) dragPointer = data.pointerId; }
        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId != dragPointer) return;
            float scale = GetComponentInParent<Canvas>()?.scaleFactor ?? 1;
            Rotate(-data.delta.x / Mathf.Max(.01f, scale) * .55f);
        }
        public void OnEndDrag(PointerEventData data) { if (dragPointer == data.pointerId) dragPointer = int.MinValue; }
        private void LateUpdate()
        {
            if (!stage || !isActiveAndEnabled) return;
            UpdateTarget(); avatar?.Tick(Time.unscaledDeltaTime, false);
        }
        private void UpdateTarget()
        {
            if (!previewCamera || !surface) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { previewCamera.enabled = false; return; }
            var rect = ((RectTransform)transform).rect;
            if (rect.width < 10 || rect.height < 10) return;
            int width = Mathf.Clamp(Mathf.CeilToInt(rect.width / 32) * 32, 128, 768);
            int height = Mathf.Clamp(Mathf.CeilToInt(rect.height / 32) * 32, 128, 768);
            if (target && target.width == width && target.height == height) return;
            ReleaseTarget();
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Numerion character preview", antiAliasing = 1, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            target.Create(); previewCamera.targetTexture = target; previewCamera.aspect = (float)width / height;
            // Framing remains stable at both wide and portrait aspect ratios.
            previewCamera.orthographicSize = Mathf.Max(1.25f, .67f / previewCamera.aspect);
            surface.texture = target; previewCamera.enabled = true;
        }
        private void ReleaseTarget()
        {
            if (previewCamera) previewCamera.targetTexture = null;
            if (surface) surface.texture = null;
            if (target) { target.Release(); DestroyOwned(target); target = null; }
        }
        public void Release()
        {
            dragPointer = int.MinValue;
            if (previewCamera) previewCamera.enabled = false;
            ReleaseTarget();
            foreach (var pair in sceneLightMasks) if (pair.Key) pair.Key.cullingMask = pair.Value;
            sceneLightMasks.Clear();
            if (stage) { stage.SetActive(false); DestroyOwned(stage); stage = null; }
            avatar = null; previewCamera = null;
#if !UNITY_WEBGL
            if (previewVolumeProfile)
            {
                foreach (var component in previewVolumeProfile.components) if (component) DestroyOwned(component);
                DestroyOwned(previewVolumeProfile); previewVolumeProfile = null;
            }
#endif
        }
        private static void SetLayer(Transform node)
        {
            node.gameObject.layer = PreviewLayer;
            var renderer = node.GetComponent<Renderer>();
            if (renderer) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off; }
            foreach (Transform child in node) SetLayer(child);
        }
        private static void DestroyOwned(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
