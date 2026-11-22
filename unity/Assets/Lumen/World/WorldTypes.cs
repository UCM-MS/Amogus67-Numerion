using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lumen.World
{
    public sealed class WorldLayout
    {
        public GameObject Root;
        public Vector3 SpawnPosition = new Vector3(0, 0.15f, 12);
        public readonly List<InteractiveStation> Stations = new List<InteractiveStation>();
        public readonly List<WorldCollectible> Collectibles = new List<WorldCollectible>();
        public readonly List<Vector3> ParkourPlatforms = new List<Vector3>();
        public Bounds PlayableBounds = new Bounds(Vector3.zero, new Vector3(150, 40, 150));

        public InteractiveStation NearestStation(Vector3 position)
        {
            InteractiveStation closest = null;
            float best = float.PositiveInfinity;
            foreach (var station in Stations)
            {
                float distance = (station.transform.position - position).sqrMagnitude;
                if (distance <= station.InteractionRadius * station.InteractionRadius && distance < best)
                { closest = station; best = distance; }
            }
            return closest;
        }
    }

    public sealed class InteractiveStation : MonoBehaviour
    {
        public string Id;
        public string Subject;
        public string Mode = "quiz";
        public string Prompt;
        public float InteractionRadius = 3.4f;
    }

    public sealed class WorldCollectible : MonoBehaviour
    {
        public string Id;
        public bool IsBeacon;
        public int Value = 1;
        public bool Collected { get; private set; }
        private Transform visual;
        private Vector3 restPosition;
        private float phase;

        public void Initialize(Transform visible, int seed)
        { visual = visible; phase = seed * 0.73f; if (visual) restPosition = visual.localPosition; }

        public void SetCollected(bool collected)
        { Collected = collected; if (visual) visual.gameObject.SetActive(!collected); }

        private void Update()
        {
            if (!visual || Collected || WorldAnimation.Frozen) return;
            visual.localPosition = restPosition + Vector3.up * (Mathf.Sin(Time.time * 1.7f + phase) * 0.12f);
            visual.Rotate(0, Time.deltaTime * 33, 0, Space.Self);
        }
    }

    // Photo mode pauses visual animation only. The network simulation keeps running.
    public static class WorldAnimation { public static bool Frozen; }

    public sealed class WorldBillboard : MonoBehaviour
    {
        private Camera target;
        private void LateUpdate()
        {
            if (WorldAnimation.Frozen) return;
            if (!target) target = Camera.main;
            if (target) transform.rotation = target.transform.rotation;
        }
    }

    public static class LumenMaterials
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static Material Get(string key, Color color, float metallic = 0, float smoothness = 0.45f, float glow = 0)
        {
            if (Cache.TryGetValue(key, out var existing) && existing) return existing;
            var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Lumen / " + key, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (glow > 0)
            {
                if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", color * glow);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * glow);
                material.EnableKeyword("_EMISSION");
            }
            if (shader.name == "HDRP/Lit") UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            Cache[key] = material;
            return material;
        }

        public static Material GetSurface(string surface, Color tint, float tiles = 1, float metallic = 0, float smoothness = 0.45f)
        {
            string key = "surface-" + surface + "-" + ColorUtility.ToHtmlStringRGB(tint) + "-" + tiles + "-" + metallic + "-" + smoothness;
            if (Cache.TryGetValue(key, out var existing) && existing) return existing;
            // Architectural paint needs a readable contrast against pale limestone and warm paving.
            var material = Get(key, Color.Lerp(Color.white, tint, surface == "grass" ? 0.4f : 0.66f), metallic, smoothness);
            var albedo = Resources.Load<Texture2D>("Textures/" + surface + "-albedo");
            var normal = Resources.Load<Texture2D>("Textures/" + surface + "-normal");
            var mask = Resources.Load<Texture2D>("Textures/" + surface + "-mask");
            var scale = Vector2.one * Mathf.Max(0.5f, tiles);
            if (albedo)
            {
                material.SetTexture("_BaseColorMap", albedo);
                material.SetTextureScale("_BaseColorMap", scale);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            }
            if (normal)
            {
                material.SetTexture("_NormalMap", normal);
                material.SetTextureScale("_NormalMap", scale);
                material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_NormalMapSpace", 0);
                material.SetFloat("_NormalScale", surface == "grass" ? 0.35f : 0.55f);
            }
            if (mask)
            {
                material.SetTexture("_MaskMap", mask);
                material.SetTextureScale("_MaskMap", scale);
                material.EnableKeyword("_MASKMAP");
                material.SetFloat("_MetallicRemapMin", 0);
                material.SetFloat("_MetallicRemapMax", surface == "metal" ? 1 : 0);
                material.SetFloat("_AORemapMin", 0.45f);
                material.SetFloat("_AORemapMax", 1);
                material.SetFloat("_SmoothnessRemapMin", 0.1f);
                material.SetFloat("_SmoothnessRemapMax", surface == "metal" ? 0.85f : surface == "grass" ? 0.28f : 0.55f);
            }
            if (material.shader.name == "HDRP/Lit") UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            return material;
        }

        public static readonly Color[] OutfitColors = {
            new Color(0.55f, 0.43f, 0.91f), new Color(0.2f, 0.68f, 0.72f), new Color(0.95f, 0.61f, 0.38f),
            new Color(0.36f, 0.7f, 0.43f), new Color(0.91f, 0.42f, 0.59f), new Color(0.24f, 0.38f, 0.63f),
            new Color(0.94f, 0.79f, 0.35f), new Color(0.32f, 0.34f, 0.43f)
        };
        public static readonly Color[] SkinColors = {
            new Color(1, 0.81f, 0.64f), new Color(0.86f, 0.62f, 0.43f), new Color(0.64f, 0.4f, 0.26f),
            new Color(0.4f, 0.25f, 0.19f), new Color(0.97f, 0.88f, 0.79f), new Color(0.74f, 0.53f, 0.38f),
            new Color(0.25f, 0.14f, 0.1f), new Color(0.91f, 0.73f, 0.58f)
        };
        public static int Wrap(int index, int count) => ((index % count) + count) % count;
    }

    public static class WorldLabels
    {
        private static Font font;
        private static Material material;

        public static TextMesh Create(Transform parent, string text, Vector3 position, float size, Color color, bool billboard = true)
        {
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!material)
            {
                var shader = Shader.Find("Lumen/World Text") ?? Shader.Find("GUI/Text Shader");
                material = new Material(shader) { name = "Lumen / World labels" };
                material.mainTexture = font.material.mainTexture;
                Font.textureRebuilt += RebindAtlas;
            }
            var obj = new GameObject("Label / " + text);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            var mesh = obj.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = size / 64f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.richText = false;
            mesh.color = color;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // This raster text shader has no ray-tracing pass. Exclude glyph quads from DXR geometry.
            renderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
            if (billboard) obj.AddComponent<WorldBillboard>();
            else obj.transform.localRotation = Quaternion.Euler(0, 180, 0);
            return mesh;
        }

        private static void RebindAtlas(Font changed)
        { if (changed == font && material) material.mainTexture = font.material.mainTexture; }
    }
}
