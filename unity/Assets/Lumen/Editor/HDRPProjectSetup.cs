#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Lumen.Editor
{
    /// <summary>
    /// Reproducible HDRP 17.3 project setup. Uses public Editor APIs; HDRP creates its own resource settings.
    /// Reference: Unity-Technologies/Graphics, 6000.3/staging, HDRenderPipelineAsset.Reset and
    /// RenderPipelineGlobalSettingsUtils.Create(Type, string, RenderPipelineGlobalSettings).
    /// </summary>
    public static class HDRPProjectSetup
    {
        private const string SettingsFolder = "Assets/Lumen/Settings";
        private const string PipelinePath = SettingsFolder + "/LumenHDRP.asset";
        private const string VariantsFolder = "Assets/Lumen/Resources/Generated/MaterialVariants";
        private const string TextureFolder = "Assets/Lumen/Resources/Textures/";
        private static readonly string[] Surfaces = { "grass", "stone", "wood", "metal" };

        public static void Configure()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(VariantsFolder);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] {
                GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11
            });
            // Static batching is incompatible with DXR instance geometry. SRP batching remains available.
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneWindows64, false);

            ConfigureTextures();
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(PipelinePath);
            if (!asset)
            {
                // HDRP's constructor is private, so ObjectFactory rejects this type. The native
                // ScriptableObject factory supports it; EnsureGlobalSettings below initializes resources.
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                asset.name = "Lumen HDRP / Raster + DXR";
                AssetDatabase.CreateAsset(asset, PipelinePath);
            }
            var settings = asset.currentPlatformRenderPipelineSettings;
            settings.supportRayTracing = true;
            settings.supportedRayTracingMode = RenderPipelineSettings.SupportedRayTracingMode.Both;
            settings.supportSSR = true;
            settings.supportSSGI = true;
            settings.supportMotionVectors = true;
            settings.supportSSAO = true;
            settings.hdShadowInitParams.supportScreenSpaceShadows = true;
            settings.hdShadowInitParams.maxScreenSpaceShadowSlots = 4;
            settings.hdShadowInitParams.maxDirectionalShadowMapResolution = 4096;
            asset.currentPlatformRenderPipelineSettings = settings;
            AssetDatabase.SetLabels(asset, new[] { "HDRenderPipelineAsset", "Lumen" });
            EditorUtility.SetDirty(asset);

            var globals = EnsureGlobalSettings();
            ConfigureFrameDefaults(globals);
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            IncludeWorldTextShader();
            PreserveInstancingVariants();
            PreserveRuntimeMaterialVariants();
            AssetDatabase.SaveAssets();
            Debug.Log("LUMEN HDRP configured: DX12 + DX11 fallback, raster / DXR / path-tracing support, native 4K PBR textures.");
        }

        private static RenderPipelineGlobalSettings EnsureGlobalSettings()
        {
            var globals = GraphicsSettings.GetSettingsForRenderPipeline<HDRenderPipeline>();
            if (globals) return globals;

            // HDRenderPipelineGlobalSettings became internal in HDRP 17.3. TypeCache discovers it;
            // the public Core utility populates all IRenderPipelineResources from their ResourcePath attributes.
            // No internal Ensure methods or serialized resource GUIDs are called or fabricated here.
            Type type = TypeCache.GetTypesDerivedFrom<RenderPipelineGlobalSettings>().FirstOrDefault(candidate =>
                candidate.FullName == "UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings");
            if (type == null) throw new InvalidOperationException("HDRP 17.3 Global Settings type was not loaded.");
            string globalPath = SettingsFolder + "/LumenHDRPGlobalSettings.asset";
            globals = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(globalPath);
            if (!globals) globals = RenderPipelineGlobalSettingsUtils.Create(type, globalPath);
            if (!globals) throw new InvalidOperationException("HDRP Global Settings could not be created.");
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<HDRenderPipeline>(globals);
            EditorUtility.SetDirty(globals);
            return globals;
        }

        private static void ConfigureFrameDefaults(RenderPipelineGlobalSettings globals)
        {
            // RenderingPathFrameSettings is internal in 17.3. Its serialized m_Camera is a public
            // FrameSettings value, so use SerializedProperty.boxedValue rather than reflecting private fields.
            var serialized = new SerializedObject(globals);
            var iterator = serialized.GetIterator();
            bool found = false;
            while (iterator.Next(true))
            {
                if (iterator.name != "m_Camera" || iterator.type != nameof(FrameSettings) ||
                    !iterator.propertyPath.StartsWith("m_Settings.", StringComparison.Ordinal)) continue;
                if (!(iterator.boxedValue is FrameSettings camera)) continue;
                camera.SetEnabled(FrameSettingsField.RayTracing, true);
                camera.SetEnabled(FrameSettingsField.ShadowMaps, true);
                camera.SetEnabled(FrameSettingsField.ScreenSpaceShadows, true);
                camera.SetEnabled(FrameSettingsField.SSR, true);
                camera.SetEnabled(FrameSettingsField.SSGI, false);
                camera.SetEnabled(FrameSettingsField.MotionVectors, true);
                camera.SetEnabled(FrameSettingsField.ObjectMotionVectors, true);
                camera.SetEnabled(FrameSettingsField.Postprocess, true);
                camera.SetEnabled(FrameSettingsField.ExposureControl, true);
                camera.SetEnabled(FrameSettingsField.AtmosphericScattering, true);
                iterator.boxedValue = camera;
                found = true;
                break;
            }
            if (!found) throw new InvalidOperationException("HDRP 17.3 camera frame defaults were not found in Global Settings.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(globals);
        }

        private static void IncludeWorldTextShader()
        {
            var shader = Shader.Find("Lumen/World Text");
            if (!shader) throw new InvalidOperationException("Missing Lumen/World Text shader. Wait for shader import to finish.");
            var settings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
            if (shaders == null || !shaders.isArray)
                throw new InvalidOperationException("Graphics Settings shader inclusion list was not available.");
            for (int i = 0; i < shaders.arraySize; i++)
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            int index = shaders.arraySize;
            shaders.InsertArrayElementAtIndex(index);
            shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PreserveRuntimeMaterialVariants()
        {
            var shader = Shader.Find("HDRP/Lit");
            if (!shader) throw new InvalidOperationException("Missing HDRP/Lit shader. Restore the HDRP package first.");
            SaveVariant("Opaque", shader, null);
            foreach (string surface in Surfaces) SaveVariant(surface, shader, surface);
            // Resources material dependencies preserve precisely the runtime Lit keyword combinations.
            // Putting HDRP/Lit in Always Included would compile a very large number of unused variants.
        }

        private static void PreserveInstancingVariants()
        {
            // Scene scanning cannot discover the academy's runtime-created renderers.
            var settings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var stripping = settings.FindProperty("m_InstancingStripping");
            if (stripping == null || stripping.propertyType != SerializedPropertyType.Enum)
                throw new InvalidOperationException("Graphics Settings instancing stripping mode was not available.");
            int keepAll = Array.FindIndex(stripping.enumNames, value =>
                string.Equals(value.Replace("_", "").Replace(" ", ""), "KeepAll", StringComparison.OrdinalIgnoreCase));
            if (keepAll < 0) throw new InvalidOperationException("Graphics Settings does not expose the Keep All instancing mode.");
            stripping.enumValueIndex = keepAll;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SaveVariant(string name, Shader shader, string surface)
        {
            string path = VariantsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = !material;
            if (created) material = new Material(shader) { name = "Lumen / Build variant / " + name };
            material.shader = shader;
            material.enableInstancing = true;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_SurfaceType", 0);
            material.SetFloat("_Metallic", surface == "metal" ? 0.8f : 0);
            material.SetFloat("_Smoothness", surface == "metal" ? 0.75f : 0.35f);
            if (surface != null)
            {
                material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + surface + "-albedo.png"));
                material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + surface + "-normal.png"));
                material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + surface + "-mask.png"));
                material.SetFloat("_NormalMapSpace", 0);
            }
            HDMaterial.ValidateMaterial(material);
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
        }

        private static void ConfigureTextures()
        {
            foreach (string surface in Surfaces)
            foreach (string channel in new[] { "albedo", "normal", "mask" })
            {
                string path = TextureFolder + surface + "-" + channel + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Texture has not been imported: " + path);
                bool normal = channel == "normal";
                bool srgb = channel == "albedo";
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                var platform = importer.GetPlatformTextureSettings("Standalone");
                bool changed = importer.textureType != type || importer.sRGBTexture != srgb ||
                    importer.maxTextureSize != 4096 || !importer.mipmapEnabled || importer.isReadable ||
                    importer.anisoLevel != 8 || importer.filterMode != FilterMode.Trilinear ||
                    importer.wrapMode != TextureWrapMode.Repeat || !importer.streamingMipmaps ||
                    importer.textureCompression != TextureImporterCompression.CompressedHQ ||
                    !platform.overridden || platform.maxTextureSize != 4096 || platform.format != TextureImporterFormat.BC7;
                if (!changed) continue;
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = 4096;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = true;
                importer.isReadable = false;
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression = false;
                if (normal) importer.convertToNormalmap = false;
                platform.name = "Standalone";
                platform.overridden = true;
                platform.maxTextureSize = 4096;
                platform.format = TextureImporterFormat.BC7;
                platform.textureCompression = TextureImporterCompression.CompressedHQ;
                platform.compressionQuality = 100;
                platform.crunchedCompression = false;
                importer.SetPlatformTextureSettings(platform);
                importer.SaveAndReimport();
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] pieces = path.Split('/');
            string parent = pieces[0];
            for (int i = 1; i < pieces.Length; i++)
            {
                string next = parent + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, pieces[i]);
                parent = next;
            }
        }
    }
}
#endif
