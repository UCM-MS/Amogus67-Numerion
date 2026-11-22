using UnityEngine;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        /// <summary>Original companion portraits, drawn with shared uGUI shapes on PC and WebGL.</summary>
        private RectTransform PetPortrait(Transform parent, string speciesId, bool locked = false, float height = 150)
        {
            if (float.IsNaN(height) || float.IsInfinity(height)) height = 150;
            height = Mathf.Clamp(height, 64, 360);
            var atlas = GetComponent<LumenPetPortraitAtlas>() ?? gameObject.AddComponent<LumenPetPortraitAtlas>();
            var root = new GameObject("Pet portrait · " + (speciesId ?? "unknown"), typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            SetWidth(root, 140);
            SetHeight(root, height);
            root.sizeDelta = new Vector2(140, height);

            var art = new GameObject("Portrait illustration", typeof(RectTransform)).GetComponent<RectTransform>();
            art.SetParent(root, false);
            Anchor(art, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(140, 150));
            art.localScale = Vector3.one * Mathf.Min(1, height / 150);

            // These three layers keep every silhouette inside one quiet, consistent frame.
            PortraitShape(art, "Cream frame", rounded, 0, 0, 138, 146, Cream, locked);
            PortraitShape(art, "Soft halo", atlas.Circle, 0, 7, 112, 112, Lavender, locked);
            PortraitShape(art, "Ground shadow", atlas.Circle, 0, -48, 80, 10, new Color(.30f, .25f, .46f, .13f), locked);

            Color gold = new Color(1f, .77f, .25f);
            Color pale = new Color(1f, .96f, .83f);
            Color dark = new Color(.10f, .16f, .25f);
            Color cyan = new Color(.22f, .93f, .98f);
            Sprite oval = atlas.Circle, triangle = atlas.Triangle;
            string glyph;

            switch ((speciesId ?? "").ToLowerInvariant())
            {
                case "ember":
                case "fox":
                {
                    var fur = new Color(.96f, .49f, .24f);
                    PortraitShape(art, "Fox body", oval, 0, -25, 62, 49, fur, locked);
                    PortraitShape(art, "Left pointed ear", triangle, -28, 43, 29, 40, fur, locked, 14);
                    PortraitShape(art, "Right pointed ear", triangle, 28, 43, 29, 40, fur, locked, -14);
                    PortraitShape(art, "Left cream lining", triangle, -28, 43, 15, 24, pale, locked, 14);
                    PortraitShape(art, "Right cream lining", triangle, 28, 43, 15, 24, pale, locked, -14);
                    PortraitShape(art, "Fox face", oval, 0, 11, 87, 69, fur, locked);
                    PortraitShape(art, "Fox muzzle", oval, 0, -6, 51, 29, pale, locked);
                    PortraitEyes(art, atlas, 18, 15, 7, 11, dark, locked);
                    PortraitShape(art, "Fox nose", triangle, 0, -3, 11, 9, dark, locked, 180);
                    glyph = "+";
                    break;
                }
                case "bolt":
                case "robot":
                {
                    var metal = new Color(.51f, .75f, .84f);
                    var trim = new Color(.23f, .39f, .49f);
                    PortraitShape(art, "Robot antenna", rounded, 0, 49, 5, 21, trim, locked);
                    PortraitShape(art, "Signal orb", oval, 0, 61, 14, 14, cyan, locked);
                    PortraitShape(art, "Robot torso", rounded, 0, -28, 60, 40, metal, locked);
                    PortraitShape(art, "Left robot hand", oval, -38, -26, 16, 22, trim, locked);
                    PortraitShape(art, "Right robot hand", oval, 38, -26, 16, 22, trim, locked);
                    PortraitShape(art, "Robot head", rounded, 0, 12, 87, 62, metal, locked);
                    PortraitShape(art, "Dark glass visor", rounded, 0, 14, 70, 36, dark, locked);
                    PortraitEyes(art, atlas, 17, 16, 10, 15, cyan, locked);
                    PortraitShape(art, "Robot battery", rounded, 0, -29, 25, 8, cyan, locked);
                    glyph = "=";
                    break;
                }
                case "astra":
                case "owl":
                {
                    var plum = new Color(.58f, .42f, .76f);
                    var feather = new Color(.35f, .25f, .56f);
                    PortraitShape(art, "Left owl wing", oval, -39, -10, 25, 59, feather, locked, -16);
                    PortraitShape(art, "Right owl wing", oval, 39, -10, 25, 59, feather, locked, 16);
                    PortraitShape(art, "Left ear plume", triangle, -27, 41, 24, 35, feather, locked, 17);
                    PortraitShape(art, "Right ear plume", triangle, 27, 41, 24, 35, feather, locked, -17);
                    PortraitShape(art, "Owl round body", oval, 0, 0, 85, 94, plum, locked);
                    PortraitShape(art, "Left facial disc", oval, -18, 14, 40, 44, pale, locked);
                    PortraitShape(art, "Right facial disc", oval, 18, 14, 40, 44, pale, locked);
                    PortraitEyes(art, atlas, 18, 17, 10, 16, dark, locked);
                    PortraitShape(art, "Owl beak", triangle, 0, -1, 15, 16, gold, locked, 180);
                    glyph = "90°";
                    break;
                }
                case "zlomko":
                case "slime":
                {
                    var mint = new Color(.25f, .84f, .65f);
                    var sheen = new Color(.62f, 1f, .81f);
                    PortraitShape(art, "Left slime puddle", oval, -31, -34, 38, 23, mint, locked);
                    PortraitShape(art, "Centre slime puddle", oval, 0, -37, 43, 24, mint, locked);
                    PortraitShape(art, "Right slime puddle", oval, 31, -34, 38, 23, mint, locked);
                    PortraitShape(art, "Slime body", oval, 0, -2, 99, 80, mint, locked);
                    PortraitShape(art, "Slime crystal", triangle, 5, 46, 29, 37, sheen, locked);
                    PortraitShape(art, "Satellite crystal", triangle, 27, 36, 15, 23, gold, locked, -18);
                    PortraitShape(art, "Slime shiny spot", oval, 26, 10, 13, 25, sheen, locked, -22);
                    PortraitEyes(art, atlas, 19, 1, 8, 12, dark, locked);
                    PortraitShape(art, "Slime smile", rounded, 0, -17, 13, 4, dark, locked);
                    glyph = "1/2";
                    break;
                }
                case "percenta":
                case "dragon":
                {
                    var coral = new Color(.91f, .34f, .48f);
                    var rose = new Color(.66f, .20f, .40f);
                    PortraitShape(art, "Left dragon wing", triangle, -35, -3, 37, 52, rose, locked, 34);
                    PortraitShape(art, "Right dragon wing", triangle, 35, -3, 37, 52, rose, locked, -34);
                    PortraitShape(art, "Dragon body", oval, 0, -26, 59, 48, coral, locked);
                    PortraitShape(art, "Left golden horn", triangle, -23, 45, 14, 35, gold, locked, 13);
                    PortraitShape(art, "Right golden horn", triangle, 23, 45, 14, 35, gold, locked, -13);
                    PortraitShape(art, "Dragon head", oval, 0, 13, 76, 66, coral, locked);
                    PortraitShape(art, "Dragon pale snout", oval, 0, -4, 43, 26, pale, locked);
                    PortraitEyes(art, atlas, 17, 17, 7, 11, dark, locked);
                    PortraitShape(art, "Dragon nose", rounded, 0, -1, 10, 4, rose, locked);
                    glyph = "%";
                    break;
                }
                case "rovnik":
                case "guardian":
                {
                    var teal = new Color(.24f, .57f, .57f);
                    var sand = new Color(.79f, .67f, .46f);
                    PortraitShape(art, "Guardian body", rounded, 0, -24, 77, 49, teal, locked);
                    PortraitShape(art, "Left swept horn", triangle, -36, 40, 23, 40, pale, locked, 35);
                    PortraitShape(art, "Right swept horn", triangle, 36, 40, 23, 40, pale, locked, -35);
                    PortraitShape(art, "Guardian left hoof", rounded, -25, -46, 23, 13, dark, locked);
                    PortraitShape(art, "Guardian right hoof", rounded, 25, -46, 23, 13, dark, locked);
                    PortraitShape(art, "Guardian face", oval, 0, 12, 88, 66, sand, locked);
                    PortraitShape(art, "Guardian muzzle", oval, 0, -7, 54, 29, pale, locked);
                    PortraitEyes(art, atlas, 21, 16, 7, 11, dark, locked);
                    PortraitShape(art, "Balance jewel", rounded, 0, 31, 11, 11, teal, locked, 45);
                    glyph = "=";
                    break;
                }
                case "geora":
                case "turtle":
                {
                    var green = new Color(.34f, .64f, .38f);
                    var emerald = new Color(.16f, .43f, .36f);
                    var jade = new Color(.53f, .82f, .51f);
                    PortraitShape(art, "Left turtle flipper", oval, -41, -25, 26, 19, green, locked, 28);
                    PortraitShape(art, "Right turtle flipper", oval, 41, -25, 26, 19, green, locked, -28);
                    PortraitShape(art, "Turtle domed shell", oval, 0, 8, 103, 83, emerald, locked);
                    PortraitShape(art, "Left shell facet", triangle, -24, 19, 32, 40, jade, locked, 90);
                    PortraitShape(art, "Right shell facet", triangle, 24, 19, 32, 40, jade, locked, -90);
                    PortraitShape(art, "Geometric shell plate", triangle, 0, 27, 35, 35, pale, locked);
                    PortraitShape(art, "Turtle head", oval, 0, -18, 51, 41, green, locked);
                    PortraitEyes(art, atlas, 12, -13, 6, 9, dark, locked);
                    glyph = "a²";
                    break;
                }
                case "prizma":
                case "manta":
                {
                    var ice = new Color(.53f, .77f, .97f);
                    var lilac = new Color(.67f, .54f, .91f);
                    PortraitShape(art, "Manta trailing tail", rounded, 0, -30, 5, 42, ice, locked);
                    PortraitShape(art, "Left swept wing", triangle, -39, 6, 58, 50, lilac, locked, 90);
                    PortraitShape(art, "Right swept wing", triangle, 39, 6, 58, 50, lilac, locked, -90);
                    PortraitShape(art, "Left luminous wingtip", triangle, -55, 6, 13, 18, cyan, locked, 90);
                    PortraitShape(art, "Right luminous wingtip", triangle, 55, 6, 13, 18, cyan, locked, -90);
                    PortraitShape(art, "Faceted manta body", rounded, 0, 6, 64, 64, ice, locked, 45);
                    PortraitShape(art, "Manta crest", triangle, 0, 38, 25, 36, lilac, locked);
                    PortraitEyes(art, atlas, 14, 6, 7, 11, dark, locked);
                    glyph = "V";
                    break;
                }
                case "datax":
                case "beetle":
                {
                    var shell = new Color(.18f, .30f, .48f);
                    var jade = new Color(.29f, .79f, .72f);
                    PortraitShape(art, "Beetle left upper leg", rounded, -39, -8, 29, 7, gold, locked, 28);
                    PortraitShape(art, "Beetle right upper leg", rounded, 39, -8, 29, 7, gold, locked, -28);
                    PortraitShape(art, "Beetle left lower leg", rounded, -37, -32, 27, 7, gold, locked, -25);
                    PortraitShape(art, "Beetle right lower leg", rounded, 37, -32, 27, 7, gold, locked, 25);
                    PortraitShape(art, "Left data antenna", rounded, -21, 42, 6, 31, gold, locked, 23);
                    PortraitShape(art, "Right data antenna", rounded, 21, 42, 6, 31, gold, locked, -23);
                    PortraitShape(art, "Beetle dark shell", oval, 0, -15, 78, 70, shell, locked);
                    PortraitShape(art, "Beetle jade head", oval, 0, 18, 64, 44, jade, locked);
                    PortraitEyes(art, atlas, 16, 21, 9, 13, dark, locked);
                    glyph = "n";
                    break;
                }
                case "ratio":
                case "rabbit":
                {
                    var blue = new Color(.43f, .75f, .83f);
                    var lining = new Color(.96f, .69f, .70f);
                    PortraitShape(art, "Rabbit pear body", oval, 0, -28, 61, 42, blue, locked);
                    PortraitShape(art, "Left tall rabbit ear", oval, -21, 42, 22, 57, blue, locked, 10);
                    PortraitShape(art, "Right tall rabbit ear", oval, 21, 42, 22, 57, blue, locked, -10);
                    PortraitShape(art, "Left inner ear", oval, -21, 44, 11, 40, lining, locked, 10);
                    PortraitShape(art, "Right inner ear", oval, 21, 44, 11, 40, lining, locked, -10);
                    PortraitShape(art, "Rabbit head", oval, 0, 3, 76, 62, blue, locked);
                    PortraitShape(art, "Rabbit muzzle", oval, 0, -13, 41, 24, pale, locked);
                    PortraitEyes(art, atlas, 17, 9, 7, 11, dark, locked);
                    PortraitShape(art, "Rabbit rose nose", triangle, 0, -9, 9, 8, lining, locked, 180);
                    glyph = "1:2";
                    break;
                }
                default:
                    PortraitShape(art, "Unknown companion crystal", rounded, 0, 7, 62, 62, Purple, locked, 45);
                    PortraitEyes(art, atlas, 14, 8, 7, 11, pale, locked);
                    glyph = "+";
                    break;
            }

            PortraitShape(art, "Topic seal", oval, 45, -49, 29, 29, locked ? new Color(.72f, .75f, .79f) : Mint, false);
            var caption = new GameObject("Portrait topic", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            caption.transform.SetParent(art, false);
            Anchor(caption.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(45, -49), new Vector2(30, 27));
            caption.font = font;
            caption.fontSize = glyph.Length > 1 && !locked ? 12 : 20;
            caption.fontStyle = FontStyle.Bold;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            caption.supportRichText = false;
            caption.raycastTarget = false;
            caption.color = locked ? Cream : Ink;
            caption.text = locked ? "?" : glyph;
            return root;
        }

        private void PortraitEyes(Transform parent, LumenPetPortraitAtlas atlas, float spacing, float y, float width, float height, Color color, bool locked)
        {
            PortraitShape(parent, "Left eye", atlas.Circle, -spacing, y, width, height, color, locked);
            PortraitShape(parent, "Right eye", atlas.Circle, spacing, y, width, height, color, locked);
        }

        private RectTransform PortraitShape(Transform parent, string name, Sprite sprite, float x, float y, float width, float height, Color color, bool locked, float rotation = 0)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.type = sprite == rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            image.color = locked ? Color.Lerp(color, new Color(.57f, .60f, .65f, color.a), .78f) : color;
            Anchor(image.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, y), new Vector2(width, height));
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            return image.rectTransform;
        }
    }

    /// <summary>Two small first-party shape sprites shared by all portraits of one HUD, then released with it.</summary>
    internal sealed class LumenPetPortraitAtlas : MonoBehaviour
    {
        private Sprite circle, triangle;
        private Texture2D circleTexture, triangleTexture;
        internal Sprite Circle => circle != null ? circle : CreateShape(false);
        internal Sprite Triangle => triangle != null ? triangle : CreateShape(true);

        private Sprite CreateShape(bool pointed)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = pointed ? "LUMEN portrait triangle" : "LUMEN portrait circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float px = x + .5f - size * .5f, py = y + .5f - size * .5f;
                // Signed distances provide one antialiased pixel around the oval or triangle.
                float distance = pointed ? Mathf.Min(py + 59, (59 - py - Mathf.Abs(px) * 2) / Mathf.Sqrt(5)) : 62 - Mathf.Sqrt(px * px + py * py);
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(distance + .5f));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            if (pointed) { triangle = sprite; triangleTexture = texture; }
            else { circle = sprite; circleTexture = texture; }
            return sprite;
        }

        private void OnDestroy()
        {
            if (circle != null) Destroy(circle);
            if (triangle != null) Destroy(triangle);
            if (circleTexture != null) Destroy(circleTexture);
            if (triangleTexture != null) Destroy(triangleTexture);
        }
    }
}
