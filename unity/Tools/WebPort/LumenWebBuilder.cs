#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Lumen.Networking;
using Lumen.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lumen.Editor
{
    public static class LumenWebBuilder
    {
        public const string ScenePath = "Assets/Lumen/Generated/AcademyWeb.unity";

        [MenuItem("LUMEN Web/Prepare Academy")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Lumen/Generated");
            Directory.CreateDirectory("Assets/Lumen/Resources/Generated");
            AssetDatabase.Refresh();
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            QualitySettings.vSyncCount = 0;
            QualitySettings.globalTextureMipmapLimit = 1;
            QualitySettings.antiAliasing = 2;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 90;
            var graphicsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (graphicsAsset != null)
            {
                var serialized = new SerializedObject(graphicsAsset);
                var included = serialized.FindProperty("m_AlwaysIncludedShaders");
                foreach (string name in new[] { "Standard", "Lumen/World Text", "Skybox/Procedural", "UI/Default" })
                {
                    var shader = Shader.Find(name);
                    if (!shader) throw new InvalidOperationException("Required browser shader is missing: " + name);
                    bool found = false;
                    for (int i = 0; i < included.arraySize; i++) found |= included.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                    if (!found) { included.InsertArrayElementAtIndex(included.arraySize); included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader; }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Lumen/Resources/Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize = 4096;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = false;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.compressionQuality = 70;
                if (path.Contains("-normal")) importer.textureType = TextureImporterType.NormalMap;
                var platform = importer.GetPlatformTextureSettings("WebGL");
                platform.overridden = true; platform.maxTextureSize = 4096;
                platform.format = TextureImporterFormat.Automatic;
                platform.compressionQuality = 70;
                importer.SetPlatformTextureSettings(platform);
                importer.SaveAndReimport();
            }

            PlayerSettings.companyName = "Lumen Academy";
            PlayerSettings.productName = "LUMEN · Unity Web";
            PlayerSettings.bundleVersion = "6.6.0-web";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "sk.lumen.academy.web");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            // API origin validation accepts HTTP only for explicitly configured local/private servers.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.template = "PROJECT:Lumen";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            // Phones use the same build with runtime DPR/mipmap limits; allow heap growth.
            var memory = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var initialMemory = memory.FindProperty("webGLInitialMemorySize"); if (initialMemory != null) initialMemory.intValue = 128;
            var maximumMemory = memory.FindProperty("webGLMaximumMemorySize"); if (maximumMemory != null) maximumMemory.intValue = 1024;
            var growthMode = memory.FindProperty("webGLMemoryGrowthMode"); if (growthMode != null) growthMode.intValue = (int)WebGLMemoryGrowthMode.Geometric;
            memory.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = playerSettings.FindProperty("activeInputHandler");
            if (input != null) input.intValue = 1;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lumen/Resources/Generated/Explorer.prefab");
            if (!prefab || !prefab.GetComponent<NetworkExplorer>()) throw new InvalidOperationException("The projection must contain the native Explorer prefab.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("LUMEN · Unity Web Academy").AddComponent<LumenGame>().ExplorerPrefab = prefab;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("LUMEN_WEB_READY: shared gameplay and HTTPS online/cloud, Built-in Forward, WebGL 2, up to 4K textures with selectable 2K/4K quality, IndexedDB saves.");
        }

        [MenuItem("LUMEN Web/Build WebGL Player")]
        public static void Build()
        {
            Prepare();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/WebGL"));
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("LUMEN Web build failed: " + report.summary.result + ", errors: " + report.summary.totalErrors);
            Debug.Log("LUMEN_WEB_BUILD_SUCCEEDED: " + output + " · bytes " + report.summary.totalSize);
        }
    }
}
#endif
