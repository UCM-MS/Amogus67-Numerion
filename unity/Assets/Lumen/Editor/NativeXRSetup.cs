#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace Lumen.Editor
{
    /// <summary>Optional Windows PC OpenXR configuration; does not enable XR on desktop startup.</summary>
    public static class NativeXRSetup
    {
        [MenuItem("LUMEN/Prepare Optional PC VR")]
        public static void Configure()
        {
            const BuildTargetGroup target = BuildTargetGroup.Standalone;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget targets))
            {
                var existing = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                targets = existing.Length > 0 ? AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(existing[0])) : null;
                if (targets == null)
                {
                    EnsureFolder("Assets/XR");
                    targets = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(targets, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, targets, true);
            }
            if (!targets.HasSettingsForBuildTarget(target)) targets.CreateDefaultSettingsForBuildTarget(target);
            if (!targets.HasManagerSettingsForBuildTarget(target)) targets.CreateDefaultManagerSettingsForBuildTarget(target);
            var settings = targets.SettingsForBuildTarget(target);
            var manager = settings.Manager;
            settings.InitManagerOnStart = false;
            manager.automaticLoading = false; manager.automaticRunning = false;
            if (!XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).FullName, target))
                throw new InvalidOperationException("Failed to assign the installed OpenXR loader.");

            // Public helper also creates the OpenXR package settings container when absent.
            FeatureHelpers.RefreshFeatures(target);
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
            if (openXR == null) throw new InvalidOperationException("OpenXR Standalone settings were not created.");
            openXR.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            Enable<OculusTouchControllerProfile>(openXR);
            Enable<ValveIndexControllerProfile>(openXR);
            Enable<MicrosoftMotionControllerProfile>(openXR);
            Enable<HTCViveControllerProfile>(openXR);
            Enable<KHRSimpleControllerProfile>(openXR);
            EditorUtility.SetDirty(openXR); EditorUtility.SetDirty(manager); EditorUtility.SetDirty(settings); EditorUtility.SetDirty(targets);
            AssetDatabase.SaveAssets();
            Debug.Log("LUMEN_NATIVE_XR_CONFIGURED: Standalone OpenXR; manual startup; single-pass instanced; controller profiles enabled.");
        }

        private static void Enable<T>(OpenXRSettings settings) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var feature = settings.GetFeature<T>();
            if (feature == null) throw new InvalidOperationException("Missing installed OpenXR feature " + typeof(T).Name);
            feature.enabled = true; EditorUtility.SetDirty(feature);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
#endif
