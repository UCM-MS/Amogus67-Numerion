#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lumen.Core;
using Lumen.Runtime;
using Lumen.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Lumen.Editor
{
    /// <summary>Exercises the production character form and its real layout; does not claim GPU output.</summary>
    public static class ProfileValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int checks, screens;

        [MenuItem("Numerion/Validate Character Editor")]
        public static void Run()
        {
            checks = screens = 0;
            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, type) =>
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); };
            Application.logMessageReceived += capture;
            GameObject root = null;
            try
            {
                root = new GameObject("Numerion character editor validation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                root.hideFlags = HideFlags.DontSave; root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var hud = root.AddComponent<LumenHud>(); Set(hud, "canvas", root.GetComponent<Canvas>());
                // Production buttons route their click sound through the owner. Keep a real,
                // inactive owner so Awake cannot load a profile or start a world/network here.
                var owner = new GameObject("Inactive character editor owner");
                owner.SetActive(false); owner.transform.SetParent(root.transform, false);
                Set(hud, "game", owner.AddComponent<LumenGame>());
                var hudRoot = new GameObject("HUD root", typeof(RectTransform)).GetComponent<RectTransform>();
                hudRoot.SetParent(root.transform, false); Set(hud, "hudRoot", hudRoot);
                var regular = Resources.Load<Font>("Fonts/BarlowSemiCondensed-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var strong = Resources.Load<Font>("Fonts/BarlowSemiCondensed-SemiBold") ?? regular;
                Set(hud, "font", regular); Set(hud, "strongFont", strong); Set(hud, "headingFont", strong);
                foreach (var window in new[] {new Vector2(1280, 720), new Vector2(1024, 768), new Vector2(768, 1024), new Vector2(390, 844), new Vector2(941, 764)})
                    foreach (float scale in new[] {1f, .85f})
                        CheckScreen(hud, root, new Vector2(window.x, window.y - 48), scale);
                Require(errors.Count == 0, "Actual Unity character editor errors: " + string.Join(" | ", errors));
                Debug.Log("NUMERION_PROFILE_UI_PASSED: " + checks + " checks across " + screens
                    + " actual production profile layouts; fixed preview and save action, independent options scroll,44px controls, draft isolation and confirm behavior. GPU preview rendering is not exercised by this headless check.");
            }
            finally { if (root) Object.DestroyImmediate(root); Application.logMessageReceived -= capture; }
        }

        private static void CheckScreen(LumenHud hud, GameObject root, Vector2 viewport, float scale)
        {
            screens++;
            Set(hud, "layoutPreviewSize", viewport); Set(hud, "interfaceScale", scale);
            root.GetComponent<RectTransform>().sizeDelta = viewport / scale;
            Call(hud, "CleanupProfilePreview");
            var old = (GameObject)Get(hud, "modal"); if (old) Object.DestroyImmediate(old);
            Set(hud, "modal", null);
            Call(hud, "Open", "Tvoja postava", "OBJAVITEĽ / ŽIVÝ 3D NÁHĽAD");
            var original = new PlayerAppearance { SkinTone = 1, OutfitColor = 2, HairStyle = 0, Accessory = 0 };
            var source = new AvatarAppearance { SkinIndex = 1, ColorIndex = 2, Hair = 0, Hat = 0, Backpack = 0 };
            int saved = 0, savedGrade = 0, savedSkin = -1, savedOutfit = -1, savedHair = -1, savedAccessory = -1;
            string savedName = null;
            Action<string, int, int, int, int, int> commit = (name, grade, skin, outfit, hair, accessory) =>
            { saved++; savedName = name; savedGrade = grade; savedSkin = skin; savedOutfit = outfit; savedHair = hair; savedAccessory = accessory; };
            Call(hud, "BuildProfileContent", "Náhľad", 7, original, source, commit, false, false);
            Rebuild(hud);
            string context = viewport.x + "x" + viewport.y + "@" + scale;
            var body = (RectTransform)Get(hud, "body");
            var preview = (RectTransform)Get(hud, "profilePreviewPanel");
            var footer = (RectTransform)Get(hud, "profileFooter");
            var rootRect = (RectTransform)Get(hud, "profileEditorRoot");
            var outer = body.GetComponentInParent<ScrollRect>();
            var options = (ScrollRect)Get(hud, "profileControlsScroll");
            var rotation = (RectTransform)Get(hud, "profileRotationRow");
            var image = (RectTransform)Get(hud, "profileImageRect");
            Require(!outer.vertical && !outer.horizontal, context + " modal cannot scroll the preview offscreen.");
            Require(options is LumenScrollRect && options.vertical && !options.horizontal, context + " only options use the normalized scroll implementation.");
            Require(options.viewport.rect.height >= 79, context + " useful options viewport stays visible.");
            Require((bool)Get(hud, "profileWide") == (rootRect.rect.width >= 740), context + " correct responsive split.");
            AssertInside(body, preview, context + " full preview panel stays inside the modal.");
            AssertInside(body, footer, context + " confirmation stays inside the modal.");
            AssertInside(preview, image, context + " image stays inside preview frame.");
            AssertInside(preview, rotation, context + " rotation controls stay inside preview frame.");
            Require(footer.rect.height * scale >= 43.5f, context + " confirm retains44CSS height.");
            var rotationButtons = rotation.GetComponentsInChildren<Button>();
            Require(rotationButtons.Length == 3, context + " left, front and right rotation controls exist.");
            foreach (var button in rotationButtons)
            {
                var rect = (RectTransform)button.transform;
                Require(rect.rect.width * scale >= 43.5f && rect.rect.height * scale >= 43.5f,
                    context + " rotation control retains44CSS target.");
            }
            Require(options.content.rect.height > options.viewport.rect.height + 10, context + " fixture really needs scrolling.");
            var fixedPosition = preview.position; var fixedFooter = footer.position;
            options.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            Require((preview.position - fixedPosition).sqrMagnitude < .01f && (footer.position - fixedFooter).sqrMagnitude < .01f,
                context + " scrolling options cannot move preview or confirmation.");
            var input = options.content.GetComponentInChildren<InputField>();
            input.text = "Nový objaviteľ";
            var buttons = options.content.GetComponentsInChildren<Button>();
            buttons.Single(b => b.name == "POKOŽKA 5").onClick.Invoke();
            buttons.Single(b => b.name == "OBLEČENIE 8").onClick.Invoke();
            buttons.Single(b => b.GetComponentInChildren<Text>().text.StartsWith("9. ročník")).onClick.Invoke();
            buttons.Single(b => b.GetComponentInChildren<Text>().text.StartsWith("Účes:")).onClick.Invoke();
            var accessoryButton = buttons.Single(b => b.GetComponentInChildren<Text>().text.StartsWith("Doplnok:"));
            accessoryButton.onClick.Invoke(); accessoryButton.onClick.Invoke();
            Require(saved == 0 && original.SkinTone == 1 && original.OutfitColor == 2 && original.HairStyle == 0 && original.Accessory == 0,
                context + " all appearance edits remain an isolated draft.");
            Require(source.SkinIndex == 1 && source.ColorIndex == 2 && source.Hair == 0 && source.Backpack == 0,
                context + " preview input appearance is not mutated.");
            footer.GetComponentInChildren<Button>().onClick.Invoke();
            Require(saved == 1 && savedName == "Nový objaviteľ" && savedGrade == 9
                && savedSkin == 4 && savedOutfit == 7 && savedHair == 1 && savedAccessory == 2,
                context + " confirmation passes precisely the edited values once.");
            Require(input.characterLimit == 20 && input.lineType == InputField.LineType.SingleLine,
                context + " identity input preserves the shared20character limit.");
            var renderer = (LumenCharacterPreview)Get(hud, "profilePreview");
            renderer.Rotate(35); Require(Mathf.Abs(renderer.Rotation - 35) < .01f, context + " rotation state is controllable before rendering.");
            renderer.ResetRotation(); Require(Mathf.Abs(renderer.Rotation) < .01f, context + " reset returns the front orientation.");
            Call(hud, "CleanupProfilePreview");
            Require(Get(hud, "profilePreview") == null && !renderer.HasRenderingTarget,
                context + " cleanup releases preview ownership idempotently.");
            Call(hud, "CleanupProfilePreview");
        }

        private static void Rebuild(LumenHud hud)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Get(hud, "modalPanel"));
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Get(hud, "body"));
                Call(hud, "RefreshProfileEditor");
                var options = (ScrollRect)Get(hud, "profileControlsScroll");
                LayoutRebuilder.ForceRebuildLayoutImmediate(options.content);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Get(hud, "profileRotationRow"));
            }
        }
        private static void AssertInside(RectTransform parent, RectTransform child, string message)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, child);
            Require(bounds.min.x >= parent.rect.xMin - 1 && bounds.max.x <= parent.rect.xMax + 1
                && bounds.min.y >= parent.rect.yMin - 1 && bounds.max.y <= parent.rect.yMax + 1, message);
        }
        private static object Call(LumenHud hud, string method, params object[] args) => typeof(LumenHud).GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(hud, args);
        private static object Get(LumenHud hud, string field) => typeof(LumenHud).GetField(field, Private).GetValue(hud);
        private static void Set(LumenHud hud, string field, object value) => typeof(LumenHud).GetField(field, Private).SetValue(hud, value);
        private static void Require(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException("Character profile UI validation: " + message); }
    }
}
#endif
