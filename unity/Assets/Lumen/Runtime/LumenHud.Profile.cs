using System;
using System.Collections.Generic;
using Lumen.Core;
using Lumen.World;
using UnityEngine;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        private RectTransform profileEditorRoot, profilePreviewPanel, profileControlsPanel, profileFooter, profileImageRect;
        private ScrollRect profileControlsScroll;
        private LumenCharacterPreview profilePreview;
        private Text profilePreviewName, profilePreviewHint;
        private RectTransform profileRotationRow;
        private Vector2 profileLastSize;
        private bool profileWide;

        public void ShowProfileEditor(bool initial)
        {
            Open(initial ? "Nová postava" : "Tvoja postava", "OBJAVITEĽ / ŽIVÝ 3D NÁHĽAD");
            FeaturePanel = "PROFILE";
            profileRequired = initial && !game.Save.HasCreatedProfile;
            BuildProfileContent(game.Profile.DisplayName, game.Profile.Grade, game.Profile.Appearance,
                game.Appearance(), (name, grade, skin, outfit, hair, accessory) =>
                {
                    game.ApplyProfile(name, AgeBand.Ages13To15, skin, outfit, hair, accessory, grade);
                    profileRequired = false;
                    CleanupProfilePreview(); CloseModal();
                    if (initial) game.EnterAcademy(true);
                }, true, initial);
        }

        // Builds the production layout without needing to start a world or a network connection.
        // The validation fixture uses createPreview=false, so it never allocates a graphics target.
        private void BuildProfileContent(string playerName, int selectedGrade, PlayerAppearance sourceAppearance,
            AvatarAppearance sourceVisual, Action<string, int, int, int, int, int> apply, bool createPreview, bool initial)
        {
            var appearance = sourceAppearance ?? new PlayerAppearance();
            int skin = appearance.SkinTone, outfit = appearance.OutfitColor;
            int hair = appearance.HairStyle, accessory = appearance.Accessory;
            int grade = CurriculumLabels.NormalizeGrade(selectedGrade);
            var visual = (sourceVisual ?? new AvatarAppearance()).Copy(); visual.Pet = 0;

            var outer = body.GetComponentInParent<ScrollRect>();
            DisablePanelScrolling(outer);
            body.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            Stretch(body, Vector2.zero, Vector2.one); body.pivot = new Vector2(.5f, 1);

            profileEditorRoot = new GameObject("Character editor", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            profileEditorRoot.SetParent(body, false);
            var flexible = profileEditorRoot.GetComponent<LayoutElement>(); flexible.minHeight = 240; flexible.flexibleHeight = 1;
            profilePreviewPanel = ImageNode("Fixed 3D character preview", profileEditorRoot, new Color(.055f, .075f, .09f)).rectTransform;
            profilePreviewName = CompactLabel(profilePreviewPanel, playerName, 20, Ink);
            profilePreviewName.alignment = TextAnchor.MiddleCenter;
            profilePreviewHint = CompactLabel(profilePreviewPanel, "Potiahni a otoč · 0° = spredu", 13, Muted);
            profilePreviewHint.alignment = TextAnchor.MiddleCenter;
            var image = new GameObject("Live character render", typeof(RectTransform), typeof(RawImage), typeof(LumenCharacterPreview));
            image.transform.SetParent(profilePreviewPanel, false); profileImageRect = image.GetComponent<RectTransform>();
            profilePreview = image.GetComponent<LumenCharacterPreview>();
            image.GetComponent<RawImage>().color = Color.white;

            profileRotationRow = CSSRow(profilePreviewPanel, MinimumTargetSize, true);
            profileRotationRow.name = "Character rotation controls";
            CompactButton(profileRotationRow, "<", () => profilePreview?.Rotate(-35), Lavender, 20);
            CompactButton(profileRotationRow, "0°", () => profilePreview?.ResetRotation(), Lavender, 16);
            CompactButton(profileRotationRow, ">", () => profilePreview?.Rotate(35), Lavender, 20);

            profileControlsPanel = ImageNode("Character options", profileEditorRoot, new Color(.08f, .10f, .125f)).rectTransform;
            profileControlsScroll = profileControlsPanel.gameObject.AddComponent<LumenScrollRect>();
            profileControlsScroll.horizontal = false; profileControlsScroll.vertical = true;
            profileControlsScroll.movementType = ScrollRect.MovementType.Clamped;
            profileControlsScroll.scrollSensitivity = 28; profileControlsScroll.inertia = true;
            profileControlsScroll.decelerationRate = .08f;
            var viewport = new GameObject("Character options viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            viewport.SetParent(profileControlsPanel, false); Stretch(viewport, Vector2.zero, Vector2.one);
            viewport.GetComponent<Image>().color = Color.clear;
            var content = new GameObject("Character options content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            Stack(content, 10, 7); content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            profileControlsScroll.viewport = viewport; profileControlsScroll.content = content;
            AddScrollIndicator(profileControlsScroll, profileControlsPanel, viewport);

            CompactLabel(content, "Zmeny hneď uvidíš na postave. Uložia sa až po potvrdení.", 15, Muted);
            CompactLabel(content, "MENO OBJAVITEĽA", 13, Muted);
            var name = ProfileNameInput(content, playerName);
            name.onValueChanged.AddListener(value => { Put(profilePreviewName, string.IsNullOrWhiteSpace(value) ? "Objaviteľ" : value); profilePreview?.SetPlayerName(value); });
            CompactLabel(content, "Meno vidia aj ostatní hráči v miestnosti.", 13, Muted);
            CompactLabel(content, "ROČNÍK ZÁKLADNEJ ŠKOLY", 13, Muted);
            var gradeRow = CSSRow(content, MinimumTargetSize, true);
            var gradeButtons = new List<Button>();
            for (int option = 7; option <= 9; option++)
            {
                int chosen = option;
                var button = CompactButton(gradeRow, chosen + ". ročník", () =>
                {
                    grade = chosen;
                    for (int i = 0; i < gradeButtons.Count; i++) Tint(gradeButtons[i], i + 7 == grade ? Selected : Lavender);
                }, option == grade ? Selected : Lavender, 16);
                gradeButtons.Add(button);
            }
            CompactLabel(content, "Ročník vyberá učivo. Tvoji spoločníci a postup zostávajú.", 14, Muted);

            Action update = () =>
            {
                visual.SkinIndex = skin; visual.ColorIndex = outfit; visual.Hair = hair;
                visual.Hat = accessory == 3 ? 2 : accessory == 1 ? 1 : 0;
                visual.Backpack = accessory == 2 ? 1 : 0;
                profilePreview?.SetAppearance(visual);
            };
            ProfilePalette(content, "POKOŽKA", LumenMaterials.SkinColors, skin, value => { skin = value; update(); });
            ProfilePalette(content, "OBLEČENIE", LumenMaterials.OutfitColors, outfit, value => { outfit = value; update(); });
            CompactLabel(content, "ÚČES", 13, Muted);
            var hairButton = CompactButton(content, HairLabel(hair), null, Lavender, 17);
            hairButton.onClick.AddListener(() => { hair = (hair + 1) % 4; ButtonText(hairButton).text = HairLabel(hair); update(); });
            CompactLabel(content, "DOPLNOK", 13, Muted);
            var accessoryButton = CompactButton(content, AccessoryLabel(accessory), null, Lavender, 17);
            accessoryButton.onClick.AddListener(() => { accessory = (accessory + 1) % 4; ButtonText(accessoryButton).text = AccessoryLabel(accessory); update(); });
            CompactLabel(content, "Potiahni zoznam alebo použi koliesko. Náhľad aj potvrdenie zostávajú na mieste.", 14, Muted);

            profileFooter = new GameObject("Persistent profile confirmation", typeof(RectTransform)).GetComponent<RectTransform>();
            profileFooter.SetParent(profileEditorRoot, false);
            var save = CompactButton(profileFooter, initial ? "Ideme objavovať" : "Uložiť postavu", () => apply?.Invoke(name.text, grade, skin, outfit, hair, accessory), Lime, 18);
            save.name = "Confirm character draft";
            Stretch(save.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var resize = profileEditorRoot.gameObject.AddComponent<LumenProfileLayoutRefresh>();
            resize.Refresh = RefreshProfileEditor;
            profileLastSize = Vector2.zero;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(modalPanel);
            RefreshProfileEditor();
            if (createPreview) { profilePreview.Initialize(visual); update(); }
        }

        private InputField ProfileNameInput(Transform parent, string value)
        {
            var image = ImageNode("Character name input", parent, new Color(.04f, .06f, .075f));
            var field = image.gameObject.AddComponent<InputField>(); field.targetGraphic = image;
            var caption = CompactLabel(image.transform, "", 19, Ink);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one);
            caption.rectTransform.offsetMin = new Vector2(10, 6); caption.rectTransform.offsetMax = new Vector2(-10, -6);
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            field.textComponent = caption; field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 20; field.text = value ?? "";
            field.selectionColor = new Color(.91f, .67f, .30f, .35f);
            var size = image.gameObject.AddComponent<LayoutElement>(); size.minHeight = MinimumTargetSize; size.preferredHeight = MinimumTargetSize;
            return field;
        }

        private void ProfilePalette(Transform parent, string label, Color[] colors, int current, Action<int> change)
        {
            CompactLabel(parent, label, 13, Muted);
            var selectedMarks = new List<GameObject>(); RectTransform row = null;
            for (int i = 0; i < colors.Length; i++)
            {
                if (i % 4 == 0) row = CSSRow(parent, MinimumTargetSize, true);
                int chosen = i;
                var button = CompactButton(row, "", () =>
                {
                    for (int j = 0; j < selectedMarks.Count; j++) selectedMarks[j].SetActive(j == chosen);
                    change(chosen);
                }, colors[i], 22);
                ButtonText(button).color = colors[i].grayscale > .55f ? ButtonInk : Color.white;
                button.name = label + " " + (i + 1);
                var mark = new GameObject("Selected colour", typeof(RectTransform), typeof(LumenProfileSelectionTick));
                mark.transform.SetParent(button.transform, false);
                var graphic = mark.GetComponent<LumenProfileSelectionTick>(); graphic.raycastTarget = false;
                graphic.color = colors[i].grayscale > .55f ? ButtonInk : Color.white;
                Anchor(graphic.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(20, 20));
                mark.SetActive(i == current); selectedMarks.Add(mark);
            }
        }

        private void RefreshProfileEditor()
        {
            if (!profileEditorRoot || !profilePreviewPanel || !profileControlsPanel || !profileFooter) return;
            Vector2 size = profileEditorRoot.rect.size;
            if (size.x < 50 || size.y < 50 || (size - profileLastSize).sqrMagnitude < .2f) return;
            profileLastSize = size;
            float footerHeight = MinimumTargetSize, gap = 10, availableHeight = size.y - footerHeight - gap;
            profileWide = size.x >= 740;
            if (profileWide)
            {
                float previewWidth = Mathf.Clamp(size.x * .36f, 260, 370);
                ProfileRect(profilePreviewPanel, 0, 0, previewWidth, availableHeight);
                ProfileRect(profileControlsPanel, previewWidth + gap, 0, size.x - previewWidth - gap, availableHeight);
            }
            else
            {
                float previewHeight = Mathf.Clamp(availableHeight * .37f, 150, 245);
                // On very short landscape windows keep both an options viewport and the fixed confirmation visible.
                previewHeight = Mathf.Min(previewHeight, Mathf.Max(102, availableHeight - 110));
                ProfileRect(profilePreviewPanel, 0, 0, size.x, previewHeight);
                ProfileRect(profileControlsPanel, 0, previewHeight + gap, size.x, Mathf.Max(80, availableHeight - previewHeight - gap));
            }
            ProfileRect(profileFooter, 0, size.y - footerHeight, size.x, footerHeight);
            float previewW = profilePreviewPanel.rect.width, previewH = profilePreviewPanel.rect.height;
            bool compactPreview = !profileWide;
            ProfileRect(profilePreviewName.rectTransform, compactPreview ? previewW * .53f : 8, 6,
                compactPreview ? previewW * .44f : previewW - 16, 26);
            ProfileRect(profilePreviewHint.rectTransform, compactPreview ? previewW * .53f : 8, 34,
                compactPreview ? previewW * .44f : previewW - 16, compactPreview ? 34 : 20);
            ProfileRect(profileImageRect, 2, compactPreview ? 2 : 58,
                compactPreview ? previewW * .51f : previewW - 4, compactPreview ? previewH - 4 : Mathf.Max(60, previewH - 116));
            ProfileRect(profileRotationRow, compactPreview ? previewW * .53f : 8,
                previewH - footerHeight - 8, compactPreview ? previewW * .44f : previewW - 16, footerHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(profileRotationRow);
        }

        private static void ProfileRect(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top); rect.sizeDelta = new Vector2(width, height);
        }

        public void CleanupProfilePreview()
        {
            if (profilePreview) profilePreview.Release();
            profilePreview = null;
            profileEditorRoot = profilePreviewPanel = profileControlsPanel = profileFooter = profileImageRect = null;
            profileControlsScroll = null;
        }
    }

    public sealed class LumenProfileLayoutRefresh : MonoBehaviour
    {
        public Action Refresh;
        private void LateUpdate() => Refresh?.Invoke();
    }

    /// <summary>Font-independent selection mark shared by every character colour swatch.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LumenProfileSelectionTick : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            Vector2 a = new Vector2(rect.xMin + rect.width * .16f, rect.yMin + rect.height * .48f);
            Vector2 b = new Vector2(rect.xMin + rect.width * .40f, rect.yMin + rect.height * .24f);
            Vector2 c = new Vector2(rect.xMin + rect.width * .85f, rect.yMin + rect.height * .78f);
            AddStroke(mesh, a, b, Mathf.Max(2, rect.width * .12f));
            AddStroke(mesh, b, c, Mathf.Max(2, rect.width * .12f));
        }

        private void AddStroke(VertexHelper mesh, Vector2 from, Vector2 to, float width)
        {
            Vector2 axis = to - from;
            Vector2 normal = new Vector2(-axis.y, axis.x).normalized * width * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(from - normal, color, Vector2.zero); mesh.AddVert(from + normal, color, Vector2.zero);
            mesh.AddVert(to + normal, color, Vector2.zero); mesh.AddVert(to - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
