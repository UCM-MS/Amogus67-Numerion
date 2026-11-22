#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Lumen.World;

namespace Lumen.Editor
{
    /// <summary>Uses owned local assets; never downloads or modifies the vendor's originals.</summary>
    public static class SyntyAcademySetup
    {
        private const string Source = "Assets/SyntyStudios/PolygonStarter/Prefabs/";
        private const string Folder = "Assets/Lumen/Resources/Generated/SyntyMaterials";
        private const string PrefabPath = "Assets/Lumen/Resources/Generated/SyntyDecor.prefab";
        private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();

        public static void Configure()
        {
            if (!AssetDatabase.IsValidFolder(Source.TrimEnd('/')))
            {
                Debug.Log("LUMEN_SYNTY: optional owned POLYGON Sampler has not been imported.");
                return;
            }
            EnsureFolder(Folder);
            var shader = Shader.Find("HDRP/Lit");
            if (!shader) throw new System.InvalidOperationException("HDRP/Lit must be configured before POLYGON decoration.");
            Materials.Clear();
            var root = new GameObject("POLYGON / Academy collection");
            int count = 0;
            try
            {
                // Place the trees on the outside edge of each island, away from station entrances.
                for (int section = 0; section < AcademyWorld.SectionPositions.Length; section++)
                {
                    var center = AcademyWorld.SectionPositions[section];
                    float outward = Mathf.Atan2(center.z, center.x);
                    for (int tree = 0; tree < 5; tree++)
                    {
                        float angle = outward + (tree - 2) * 0.42f;
                        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (tree % 2 == 0 ? 13.3f : 12.1f);
                        if (Place(root.transform, "SM_Generic_Tree_0" + (1 + (section + tree) % 4), center + offset, 4.6f + tree % 3 * 0.65f, section * 47 + tree * 73, shader)) count++;
                    }
                    for (int rock = 0; rock < 2; rock++)
                    {
                        float angle = outward + (rock == 0 ? -0.95f : 0.95f);
                        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 13.2f;
                        if (Place(root.transform, "SM_Generic_Small_Rocks_0" + (1 + (section + rock) % 5), center + offset, 0.8f, section * 39, shader)) count++;
                    }
                }
                // Real vehicle models form outdoor exhibits for transport and aerodynamics.
                if (Place(root.transform, "SM_Veh_Plane_Stunt_01", AcademyWorld.SectionPositions[1] + new Vector3(8.5f, 0, -5), 2.4f, -25, shader)) count++;
                if (Place(root.transform, "SM_PolygonCity_Veh_Car_Small_01", AcademyWorld.SectionPositions[5] + new Vector3(-8, 0, -6), 1.8f, 140, shader)) count++;
                if (Place(root.transform, "SM_Wep_Shield_04", AcademyWorld.SectionPositions[3] + new Vector3(6.5f, 0, -5), 1.6f, 15, shader)) count++;
                if (Place(root.transform, "SM_PolygonPrototype_Prop_Crate_03", AcademyWorld.SectionPositions[2] + new Vector3(7, 0, -5), 1.1f, 20, shader)) count++;
                if (Place(root.transform, "SM_Generic_Tree_02", new Vector3(-13, 0, 5), 4.8f, 0, shader)) count++;
                if (Place(root.transform, "SM_Generic_Tree_04", new Vector3(13, 0, 5), 4.6f, 70, shader)) count++;
                // A visible silhouette and layered vegetation make the floating archipelago feel inhabited.
                if (Place(root.transform, "SM_Generic_Tree_01", new Vector3(-15, 0, -5), 6.3f, 120, shader)) count++;
                if (Place(root.transform, "SM_Generic_Tree_03", new Vector3(15.4f, 0, -4), 5.8f, 20, shader)) count++;
                if (Place(root.transform, "SM_Generic_Small_Rocks_02", new Vector3(-14.6f, 0, 7), 0.75f, 20, shader)) count++;
                if (Place(root.transform, "SM_Generic_Small_Rocks_04", new Vector3(14, 0, -12), 0.7f, 70, shader)) count++;
                if (count == 0) throw new System.InvalidOperationException("POLYGON import exists but no usable prefabs were found.");
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!saved) throw new System.InvalidOperationException("POLYGON decoration prefab could not be saved.");
                AssetDatabase.SaveAssets();
                Debug.Log("LUMEN_SYNTY_READY: " + count + " owned model instances; " + Materials.Count + " HDRP material copies; no gameplay colliders.");
            }
            finally { Object.DestroyImmediate(root); Materials.Clear(); }
        }

        private static bool Place(Transform root, string name, Vector3 position, float height, float yaw, Shader shader)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source + name + ".prefab");
            if (!prefab) return false;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = name;
            instance.transform.SetParent(root, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            instance.transform.localScale = Vector3.one;
            // Decorative models must never alter authoritative server collision or trap the camera.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            foreach (var script in instance.GetComponentsInChildren<MonoBehaviour>(true)) if (script) Object.DestroyImmediate(script);
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Object.DestroyImmediate(instance); return false; }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var replacements = renderer.sharedMaterials;
                for (int i = 0; i < replacements.Length; i++) replacements[i] = ConvertMaterial(replacements[i], shader);
                renderer.sharedMaterials = replacements;
            }
            // Thin or unusually oriented sampler props must not grow beyond their exhibit footprint.
            float factor = Mathf.Min(height / Mathf.Max(0.01f, bounds.size.y),
                height * 2.4f / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z)));
            instance.transform.localScale = Vector3.one * factor;
            instance.transform.localPosition = position + new Vector3(-bounds.center.x * factor, 0.06f - bounds.min.y * factor, -bounds.center.z * factor);
            return true;
        }

        private static Material ConvertMaterial(Material original, Shader shader)
        {
            if (!original) return null;
            if (Materials.TryGetValue(original, out var cached)) return cached;
            string path = Folder + "/" + original.name.Replace('/', '_') + "_HDRP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = original.name + " / LUMEN HDRP" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            Texture texture = original.HasProperty("_MainTex") ? original.GetTexture("_MainTex") : original.mainTexture;
            material.SetTexture("_BaseColorMap", texture);
            material.SetColor("_BaseColor", original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", 0.28f);
            material.SetFloat("_DoubleSidedEnable", 1);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            Materials.Add(original, material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
#endif
