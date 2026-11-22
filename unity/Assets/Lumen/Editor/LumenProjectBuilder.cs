#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Lumen.Networking;
using Lumen.Runtime;
using Lumen.World;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SessionState = UnityEditor.SessionState;

namespace Lumen.Editor
{
    [InitializeOnLoad]
    public static class LumenProjectBuilder
    {
        public const string ScenePath = "Assets/Lumen/Generated/Academy.unity";
        public const string PrefabPath = "Assets/Lumen/Resources/Generated/Explorer.prefab";
        private const string PreparedKey = "Lumen.Native.Prepared";

        static LumenProjectBuilder() { EditorApplication.delayCall += AutoPrepare; }

        private static void AutoPrepare()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(PreparedKey, false) || File.Exists(ScenePath)) return;
            SessionState.SetBool(PreparedKey, true);
            try { Prepare(); }
            catch (Exception exception) { Debug.LogException(exception); SessionState.SetBool(PreparedKey, false); }
        }

        [MenuItem("LUMEN/Prepare Academy")]
        public static void Prepare()
        {
            EnsureFolder("Assets/Lumen/Generated"); EnsureFolder("Assets/Lumen/Resources/Generated");
            HDRPProjectSetup.Configure();
            SyntyAcademySetup.Configure();
            NativeXRSetup.Configure();
            ConfigurePlayer();
            GameObject prefab = PreparePrefab();
            var previousScene = SceneManager.GetActiveScene();
            bool replaceScene = Application.isBatchMode || (string.IsNullOrEmpty(previousScene.path) && !previousScene.isDirty);
            if (!replaceScene && string.IsNullOrEmpty(previousScene.path))
                throw new InvalidOperationException("Pred prípravou akadémie ulož svoju rozpracovanú scénu.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, replaceScene ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("LUMEN · Akadémia objavov");
            root.AddComponent<LumenGame>().ExplorerPrefab = prefab;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (!replaceScene) EditorSceneManager.CloseScene(scene, true);
            if (!replaceScene && previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
            if (!replaceScene && !Application.isBatchMode && previousScene.IsValid() && !previousScene.isDirty)
                EditorSceneManager.OpenScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("LUMEN_READY: Academy scene, registered network prefab, HDRP, DXR and 4K import settings prepared.");
        }

        private static GameObject PreparePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && existing.GetComponent<NetworkObject>() != null && existing.GetComponent<NetworkExplorer>() != null)
            { ValidateAndSavePrefabHash(existing); return existing; }
            var go = new GameObject("Lumen Network Explorer");
            var capsule = go.AddComponent<CharacterController>();
            capsule.height = 1.8f; capsule.center = new Vector3(0, .9f, 0); capsule.radius = .3f;
            capsule.stepOffset = .3f; capsule.slopeLimit = 50; capsule.skinWidth = .05f;
            go.AddComponent<NetworkObject>(); go.AddComponent<NetworkExplorer>();
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            ValidateAndSavePrefabHash(prefab); return prefab;
        }

        private static void ValidateAndSavePrefabHash(GameObject prefab)
        {
            var networkObject = prefab.GetComponent<NetworkObject>();
            // NGO's real validator derives the hash from the imported prefab GUID and stable local file ID.
            var validate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (validate == null) throw new InvalidOperationException("NGO prefab validator is unavailable.");
            validate.Invoke(networkObject, null);
            var serialized = new SerializedObject(networkObject);
            var hash = serialized.FindProperty("GlobalObjectIdHash");
            if (hash == null || hash.longValue == 0) throw new InvalidOperationException("The imported player prefab has no valid NGO hash.");
            EditorUtility.SetDirty(networkObject); PrefabUtility.SavePrefabAsset(prefab); AssetDatabase.SaveAssets();
            Debug.Log("LUMEN_PREFAB_HASH: " + hash.longValue);
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Lumen Academy";
            PlayerSettings.productName = "LUMEN · Akadémia objavov";
            PlayerSettings.bundleVersion = "6.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "sk.lumen.academy");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground = true;
            // OnlineAcademyClient permits plain HTTP only for loopback and private LAN addresses.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            // OpenXR requires the Input System; do not rely on the obsolete Input manager.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 1; settings.ApplyModifiedPropertiesWithoutUndo(); }
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 1f / NetworkMovement.SimulationRate;
        }

        [MenuItem("LUMEN/Build Windows Player")]
        public static void BuildWindows()
        {
            Validate();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows"));
            Directory.CreateDirectory(root);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = Path.Combine(root, "LumenAcademy.exe"),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("LUMEN build failed: " + report.summary.result + ", errors: " + report.summary.totalErrors);
            Debug.Log("LUMEN_BUILD_SUCCEEDED: " + root);
        }

        [MenuItem("LUMEN/Validate Academy")]
        public static void Validate()
        {
            Prepare();
            PersistenceValidation.Run();
            var layout = AcademyWorld.Build(true);
            AcademyRouteValidation.Validate(layout);
            if (layout.Root.GetComponentsInChildren<Renderer>(true).Length != 0)
                throw new InvalidOperationException("Headless world allocated renderers.");
            if (layout.Stations.Count < 12 || layout.Collectibles.Count < 18 || layout.ParkourPlatforms.Count != 9)
                throw new InvalidOperationException("Academy content is incomplete.");
            var controller = new GameObject("Physics validator").AddComponent<CharacterController>();
            controller.height = 1.8f; controller.center = Vector3.up * .9f;
            controller.transform.position = layout.SpawnPosition + Vector3.up;
            Physics.SyncTransforms();
            if (!Physics.Raycast(controller.transform.position, Vector3.down, 4))
                throw new InvalidOperationException("No physical floor beneath the spawn.");
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
            UnityEngine.Object.DestroyImmediate(layout.Root);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<NetworkExplorer>())
                throw new InvalidOperationException("Network prefab is missing its player behaviour.");
            Debug.Log("LUMEN_VALIDATION_PASSED: headless colliders, six subjects, collectibles, course and network prefab.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
