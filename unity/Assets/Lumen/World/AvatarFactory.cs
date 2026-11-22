using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Lumen.World
{
    [Serializable]
    public sealed class AvatarAppearance
    {
        public int ColorIndex;
        public int SkinIndex;
        public int Hair;
        public int Hat;
        public int Backpack = 1;
        public int Pet;
        public int PetGear;
        public AvatarAppearance Copy() => (AvatarAppearance)MemberwiseClone();
    }

    public static class AvatarFactory
    {
        private static readonly Dictionary<PrimitiveType, Mesh> Meshes = new Dictionary<PrimitiveType, Mesh>();

        public static AvatarVisual Create(string playerName, AvatarAppearance appearance = null)
        {
            var root = new GameObject("Explorer / " + playerName);
            var avatar = root.AddComponent<AvatarVisual>();
            avatar.Initialize(playerName, appearance ?? new AvatarAppearance());
            return avatar;
        }

        internal static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color, float metallic = 0)
        {
            if (!Meshes.TryGetValue(type, out var mesh))
            {
                var sample = GameObject.CreatePrimitive(type); sample.SetActive(false);
                mesh = sample.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.Destroy(sample); Meshes[type] = mesh;
            }
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = LumenMaterials.Get("avatar-" + ColorUtility.ToHtmlStringRGB(color) + "-" + metallic, color, metallic, 0.48f);
            return obj.transform;
        }
    }

    public sealed class AvatarVisual : MonoBehaviour
    {
        public GameObject Root => gameObject;
        public AvatarAppearance Appearance { get; private set; }
        public string PlayerName { get; private set; }
        private Transform model;
        private Transform armLeft, armRight, legLeft, legRight, head;
        private Transform pet;
        private TextMesh label;
        private float gait;
        private bool visible = true;
        private bool nameVisible = true;

        internal void Initialize(string playerName, AvatarAppearance appearance)
        {
            label = WorldLabels.Create(transform, "", new Vector3(0, 2.32f, 0), 0.62f, new Color(0.15f, 0.2f, 0.3f));
            SetName(playerName); ApplyAppearance(appearance);
        }

        public void SetName(string playerName)
        {
            var clean = new StringBuilder(20);
            foreach (char ch in playerName ?? "Objaviteľ")
            {
                if (!char.IsControl(ch)) clean.Append(ch);
                if (clean.Length == 20) break;
            }
            PlayerName = clean.ToString().Trim();
            if (PlayerName.Length == 0) PlayerName = "Objaviteľ";
            gameObject.name = "Explorer / " + PlayerName;
            if (label) label.text = PlayerName;
        }

        public void ApplyAppearance(AvatarAppearance appearance)
        {
            Appearance = (appearance ?? new AvatarAppearance()).Copy();
            if (model) { model.gameObject.SetActive(false); Destroy(model.gameObject); }
            if (pet) { pet.gameObject.SetActive(false); Destroy(pet.gameObject); }
            var group = new GameObject("Customizable explorer"); group.transform.SetParent(transform, false); model = group.transform;
            Color outfit = LumenMaterials.OutfitColors[LumenMaterials.Wrap(Appearance.ColorIndex, LumenMaterials.OutfitColors.Length)];
            Color skin = LumenMaterials.SkinColors[LumenMaterials.Wrap(Appearance.SkinIndex, LumenMaterials.SkinColors.Length)];
            Color dark = new Color(0.17f, 0.22f, 0.32f);
            Color hair = Appearance.SkinIndex % 2 == 0 ? new Color(0.25f, 0.19f, 0.22f) : new Color(0.4f, 0.26f, 0.16f);
            Color pale = new Color(0.98f, 0.95f, 0.86f);
            AvatarFactory.Part("Explorer jacket", PrimitiveType.Cube, model, new Vector3(0, 1, 0), new Vector3(0.67f, 0.58f, 0.36f), outfit);
            AvatarFactory.Part("Jacket zip", PrimitiveType.Cube, model, new Vector3(0, 1, 0.19f), new Vector3(0.025f, 0.55f, 0.02f), pale);
            AvatarFactory.Part("Academy badge", PrimitiveType.Sphere, model, new Vector3(-0.19f, 1.15f, 0.2f), Vector3.one * 0.1f, new Color(0.97f, 0.81f, 0.35f), 0.7f);
            head = AvatarFactory.Part("Face", PrimitiveType.Sphere, model, new Vector3(0, 1.52f, 0), new Vector3(0.58f, 0.58f, 0.53f), skin);
            for (int side = -1; side <= 1; side += 2)
            {
                AvatarFactory.Part("Eye", PrimitiveType.Sphere, model, new Vector3(side * 0.12f, 1.56f, 0.247f), new Vector3(0.055f, 0.07f, 0.035f), dark);
                AvatarFactory.Part("Eye glint", PrimitiveType.Sphere, model, new Vector3(side * 0.12f - 0.008f, 1.575f, 0.265f), Vector3.one * 0.019f, pale);
            }
            AvatarFactory.Part("Smile", PrimitiveType.Cube, model, new Vector3(0, 1.405f, 0.255f), new Vector3(0.13f, 0.025f, 0.02f), dark);
            armLeft = Limb("Left arm", new Vector3(-0.42f, 1.2f, 0), new Vector3(0.21f, 0.49f, 0.24f), outfit);
            armRight = Limb("Right arm", new Vector3(0.42f, 1.2f, 0), new Vector3(0.21f, 0.49f, 0.24f), outfit);
            AvatarFactory.Part("Left hand", PrimitiveType.Sphere, armLeft, new Vector3(0, -0.52f, 0), new Vector3(0.19f, 0.19f, 0.19f), skin);
            AvatarFactory.Part("Right hand", PrimitiveType.Sphere, armRight, new Vector3(0, -0.52f, 0), new Vector3(0.19f, 0.19f, 0.19f), skin);
            legLeft = Limb("Left leg", new Vector3(-0.18f, 0.72f, 0), new Vector3(0.26f, 0.57f, 0.28f), dark);
            legRight = Limb("Right leg", new Vector3(0.18f, 0.72f, 0), new Vector3(0.26f, 0.57f, 0.28f), dark);
            AvatarFactory.Part("Left shoe", PrimitiveType.Cube, legLeft, new Vector3(0, -0.63f, 0.07f), new Vector3(0.29f, 0.17f, 0.42f), pale);
            AvatarFactory.Part("Right shoe", PrimitiveType.Cube, legRight, new Vector3(0, -0.63f, 0.07f), new Vector3(0.29f, 0.17f, 0.42f), pale);
            BuildHair(hair);
            BuildHat(outfit, pale);
            BuildBackpack(outfit, dark);
            BuildPet(Appearance.Pet, outfit);
            SetVisible(visible);
        }

        private Transform Limb(string name, Vector3 position, Vector3 size, Color tint)
        {
            var pivot = new GameObject(name).transform; pivot.SetParent(model, false); pivot.localPosition = position;
            AvatarFactory.Part("Clothing", PrimitiveType.Cube, pivot, Vector3.down * size.y * 0.5f, size, tint);
            return pivot;
        }

        private void BuildHair(Color hairColor)
        {
            int style = LumenMaterials.Wrap(Appearance.Hair, 4);
            if (style == 3) return;
            AvatarFactory.Part("Hair cap", PrimitiveType.Sphere, model, new Vector3(0, 1.75f, -0.035f), new Vector3(0.61f, 0.25f, 0.55f), hairColor);
            if (style == 1)
            {
                for (int i = -1; i <= 1; i++)
                {
                    var tuft = AvatarFactory.Part("Playful hair tuft", PrimitiveType.Cube, model, new Vector3(i * 0.15f, 1.86f, 0), new Vector3(0.14f, 0.25f, 0.22f), hairColor);
                    tuft.localRotation = Quaternion.Euler(0, 0, i * 18);
                }
            }
            else if (style == 2)
            {
                AvatarFactory.Part("Long hair", PrimitiveType.Sphere, model, new Vector3(0, 1.45f, -0.23f), new Vector3(0.64f, 0.7f, 0.3f), hairColor);
                AvatarFactory.Part("Hair bun", PrimitiveType.Sphere, model, new Vector3(0, 1.89f, -0.12f), Vector3.one * 0.32f, hairColor);
            }
            else AvatarFactory.Part("Side fringe", PrimitiveType.Cube, model, new Vector3(-0.15f, 1.69f, 0.23f), new Vector3(0.24f, 0.2f, 0.13f), hairColor);
        }

        private void BuildHat(Color outfit, Color pale)
        {
            switch (LumenMaterials.Wrap(Appearance.Hat, 4))
            {
                case 1:
                    AvatarFactory.Part("Explorer cap", PrimitiveType.Sphere, model, new Vector3(0, 1.8f, 0), new Vector3(0.69f, 0.24f, 0.66f), outfit);
                    AvatarFactory.Part("Cap visor", PrimitiveType.Cube, model, new Vector3(0, 1.77f, 0.3f), new Vector3(0.56f, 0.055f, 0.34f), outfit);
                    break;
                case 2:
                    AvatarFactory.Part("Academy hat brim", PrimitiveType.Cylinder, model, new Vector3(0, 1.85f, 0), new Vector3(0.85f, 0.04f, 0.85f), outfit);
                    AvatarFactory.Part("Academy hat", PrimitiveType.Cylinder, model, new Vector3(0, 2, 0), new Vector3(0.55f, 0.18f, 0.55f), outfit);
                    AvatarFactory.Part("Hat band", PrimitiveType.Cylinder, model, new Vector3(0, 1.91f, 0), new Vector3(0.57f, 0.035f, 0.57f), pale);
                    break;
                case 3:
                    AvatarFactory.Part("Achievement crown", PrimitiveType.Cylinder, model, new Vector3(0, 1.84f, 0), new Vector3(0.64f, 0.07f, 0.64f), new Color(0.97f, 0.8f, 0.3f), 0.6f);
                    for (int point = 0; point < 5; point++)
                    {
                        float a = point * Mathf.PI * 2 / 5;
                        AvatarFactory.Part("Crown point", PrimitiveType.Cube, model, new Vector3(Mathf.Cos(a) * 0.25f, 1.98f, Mathf.Sin(a) * 0.25f), new Vector3(0.1f, 0.23f, 0.1f), new Color(0.97f, 0.8f, 0.3f), 0.6f);
                    }
                    break;
            }
        }

        private void BuildBackpack(Color outfit, Color dark)
        {
            int style = LumenMaterials.Wrap(Appearance.Backpack, 4);
            if (style == 0) return;
            if (style == 2)
                AvatarFactory.Part("Explorer cape", PrimitiveType.Cube, model, new Vector3(0, 0.85f, -0.27f), new Vector3(0.67f, 0.84f, 0.045f), outfit);
            else
            {
                AvatarFactory.Part("Field backpack", PrimitiveType.Cube, model, new Vector3(0, 1.02f, -0.27f), new Vector3(0.5f, 0.54f, 0.3f), Color.Lerp(outfit, dark, 0.3f));
                AvatarFactory.Part("Backpack pocket", PrimitiveType.Cube, model, new Vector3(0, 0.93f, -0.45f), new Vector3(0.37f, 0.25f, 0.1f), outfit);
                if (style == 3)
                    for (int side = -1; side <= 1; side += 2)
                        AvatarFactory.Part("Science pack cylinder", PrimitiveType.Cylinder, model, new Vector3(side * 0.27f, 1, -0.35f), new Vector3(0.16f, 0.3f, 0.16f), new Color(0.73f, 0.82f, 0.88f), 0.75f);
            }
        }

        private void BuildPet(int style, Color accent)
        {
            pet = CompanionModels.Build(transform, style, accent, Appearance.PetGear);
            if (pet) pet.localPosition = new Vector3(1.1f, 0.5f, -0.8f);
        }

        public void Tick(float deltaTime, bool moving)
        {
            if (!model || WorldAnimation.Frozen) return;
            gait += deltaTime * (moving ? 9 : 1.8f);
            float swing = moving ? Mathf.Sin(gait) * 29 : Mathf.Sin(gait) * 2;
            armLeft.localRotation = Quaternion.Euler(swing, 0, -3);
            armRight.localRotation = Quaternion.Euler(-swing, 0, 3);
            legLeft.localRotation = Quaternion.Euler(-swing, 0, 0);
            legRight.localRotation = Quaternion.Euler(swing, 0, 0);
            model.localPosition = new Vector3(0, moving ? Mathf.Abs(Mathf.Sin(gait)) * 0.035f : Mathf.Sin(gait) * 0.012f, 0);
            if (pet)
            {
                pet.localPosition = new Vector3(1.1f, 0.5f + Mathf.Sin(gait * 0.8f) * 0.07f, -0.8f);
                pet.localRotation = Quaternion.Euler(0, Mathf.Sin(gait * 0.4f) * 8, 0);
            }
        }

        public void SetVisible(bool show)
        { visible = show; if (model) model.gameObject.SetActive(show); if (label) label.gameObject.SetActive(show && nameVisible); if (pet) pet.gameObject.SetActive(show); }

        public void SetNameVisible(bool show) { nameVisible = show; if (label) label.gameObject.SetActive(show && visible); }
    }
}
