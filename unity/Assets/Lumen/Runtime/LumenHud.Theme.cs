using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        private static readonly Color Brass = new Color(.75f, .61f, .36f);
        private static readonly Color Chalk = new Color(.94f, .91f, .83f);
        private static readonly Color SoftInk = new Color(.10f, .15f, .20f, .93f);
        private static readonly Color Frame = new Color(.59f, .55f, .45f, .45f);
        private RectTransform hudBrand, hudStats, hudQuest, hudControls, hudMenu, hudInteraction, hudNotification, hudGuide, hudQuickbar;
        private RectTransform modalSafeRoot, modalPanel;
        private Text modalHeading, modalKicker;
        private Button modalClose;
        private GameObject titlePlayerCard;
        private Button tutorialSkip;
        private Font headingFont, strongFont;
        private static Font fallbackFont;
        private int layoutWidth = -1, layoutHeight = -1, menuSection;
        private Rect layoutSafeArea;
        private float interfaceScale = 1;
        private bool lastCompact;
        private WindowKind windowKind;
        private enum WindowKind { Page, Title, Menu }

        private bool CompactViewport
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (LumenWebPlatform.TouchViewport) return true;
#endif
                return Screen.width < Screen.height || Screen.width < 900 ||
                    ((Application.isMobilePlatform || Touchscreen.current != null) && Screen.width < 1200);
            }
        }

        private void ConfigureInterfaceScale(float value)
        { interfaceScale = Mathf.Clamp(value, .85f, 1.2f); ApplyViewportLayout(true); }

        private void ApplyViewportLayout(bool force = false)
        {
            if (canvas == null || Screen.width < 1 || Screen.height < 1) return;
            bool compact = CompactViewport;
            bool landscapeTouch = compact && Screen.width > Screen.height;
            var scaler = canvas.GetComponent<CanvasScaler>();
            float referenceWidth=Screen.width>Screen.height?1280:640;
            var reference = compact ? new Vector2(referenceWidth, referenceWidth * Screen.height / Screen.width) : new Vector2(1920, 1080);
            reference /= interfaceScale;
            if (scaler.referenceResolution != reference) { scaler.referenceResolution = reference; force = true; }
            scaler.matchWidthOrHeight = compact ? 0 : .5f;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            if (!force && layoutWidth == Screen.width && layoutHeight == Screen.height && layoutSafeArea == safe && lastCompact==compact) return;
            layoutWidth = Screen.width; layoutHeight = Screen.height; layoutSafeArea = safe;lastCompact=compact;
            Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            if (hudRoot) Stretch(hudRoot, min, max);
            if (modalSafeRoot) Stretch(modalSafeRoot, min, max);

            float safeWidth = safe.width / Mathf.Max(.01f, canvas.scaleFactor);
            // First-frame canvas scale is settled by CanvasScaler later in the frame.
            if (compact) safeWidth = referenceWidth / interfaceScale * safe.width / Screen.width;
            foreach(var button in canvas.GetComponentsInChildren<LumenButtonLayout>(true))
            {
                button.MinimumHeight=compact?Mathf.Max(92,88/interfaceScale):64;
                var element=button.GetComponent<LayoutElement>();if(element)element.minHeight=button.MinimumHeight;
            }
            if (hudBrand)
            {
                hudBrand.gameObject.SetActive(!compact);
                Anchor(hudBrand, new Vector2(0,1), new Vector2(0,1), new Vector2(26,-24), new Vector2(230,68));
                Anchor(hudStats, new Vector2(1,1), new Vector2(1,1), new Vector2(compact ? -166 : -26,-24), new Vector2(compact ? Mathf.Min(440,safeWidth-192) : 400,84));
                Anchor(hudMenu, new Vector2(1,compact?1:0), new Vector2(1,compact?1:0), new Vector2(-18,compact?-18:24), new Vector2(compact?138:180,compact?92:64));
                Anchor(hudQuest, new Vector2(0,1), new Vector2(0,1), new Vector2(26,landscapeTouch?-24:compact?-112:-108), new Vector2(landscapeTouch?420:compact?safeWidth-52:420,compact?86:104));
                Anchor(hudControls, Vector2.zero, Vector2.zero, new Vector2(26,24), new Vector2(440,42));
                hudControls.gameObject.SetActive(!compact);
                hudQuickbar.gameObject.SetActive(!compact);
                Anchor(hudQuickbar, new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(0,24), new Vector2(490,64));
                Anchor(hudInteraction, new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(0,compact?250:106), new Vector2(compact?safeWidth-52:640,compact?96:82));
                interact.gameObject.SetActive(!compact);
                Anchor(hudNotification, new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,compact?-214:-24), new Vector2(compact?safeWidth-52:660,78));
                Anchor(hudGuide, new Vector2(compact?0:1,1), new Vector2(compact?0:1,1), new Vector2(compact?26:-26,landscapeTouch?-124:compact?-214:-116), new Vector2(landscapeTouch?420:compact?safeWidth-52:370,compact?164:240));
                tutorialText.gameObject.SetActive(false);
                tutorialSkip.gameObject.SetActive(!compact);
            }
            if (modalPanel)
            {
                if (windowKind == WindowKind.Title)
                {
                    Stretch(modalPanel, compact?new Vector2(.04f,.035f):new Vector2(.055f,.10f), compact?new Vector2(.96f,.965f):new Vector2(.39f,.90f));
                    if (titlePlayerCard) titlePlayerCard.SetActive(!compact);
                }
                else Stretch(modalPanel, compact?new Vector2(.04f,.025f):new Vector2(.15f,.065f), compact?new Vector2(.96f,.975f):new Vector2(.85f,.935f));
                var stack = modalPanel.GetComponent<VerticalLayoutGroup>();
                if (stack) { int pad=compact?18:32;stack.padding=new RectOffset(pad,pad,pad,pad);stack.spacing=compact?12:16; }
            }
        }

        private RectTransform HudBlock(string name, Color color)
        {
            var image = ImageNode(name, hudRoot, color); image.raycastTarget = false;
            var stack = Stack(image.rectTransform, 12, 4);
            return stack;
        }

        private void Rule(Transform parent, Color color)
        {
            var line=ImageNode("Fine rule",parent,color);line.raycastTarget=false;
            SetHeight(line.rectTransform,1);
        }

        private Button NavigationLink(Transform parent, string title, Action action, bool primary = false, bool dark = false)
        {
            var button=ButtonNode(parent,title,action,primary?Lime:dark?new Color(.14f,.20f,.24f,.78f):Cream,dark&&!primary?Chalk:Ink,28);
            var caption=ButtonText(button);caption.alignment=TextAnchor.MiddleLeft;
            caption.font=ChooseFont(title,strongFont!=null?strongFont:font);
            return button;
        }

        private void MenuEntry(string title, string description, Action action)
        {
            NavigationLink(body,title+"   →",action);
            Label(body,description,23,Muted);
            Rule(body,Frame);
        }

        private static Font ChooseFont(string value, Font preferred)
        {
            if (fallbackFont == null) fallbackFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (preferred == null) return fallbackFont;
            foreach(char c in value??"") if (!char.IsWhiteSpace(c) && !preferred.HasCharacter(c)) return fallbackFont;
            return preferred;
        }
    }

    /// <summary>Button rows become a single column before narrow touch targets can collide.</summary>
    internal sealed class LumenAdaptiveRow : MonoBehaviour
    {
        public float BaseHeight;
        private HorizontalLayoutGroup horizontal;
        private VerticalLayoutGroup vertical;
        private LayoutElement layout;
        private void LateUpdate()
        {
            var rect=(RectTransform)transform;
            if (!horizontal) horizontal=GetComponent<HorizontalLayoutGroup>();
            if (!layout) layout=GetComponent<LayoutElement>();
            if (!horizontal || !layout) return;
            int count=0;bool buttonsOnly=true;float required=0;
            foreach(Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                var element=child.GetComponent<LayoutElement>();if(element&&element.ignoreLayout)continue;
                count++;buttonsOnly &= child.GetComponent<Button>()!=null;
                required=Mathf.Max(required,LayoutUtility.GetPreferredHeight((RectTransform)child));
            }
            bool column=buttonsOnly&&count>1&&rect.rect.width<680;
            if(column&&!vertical)
            {
                vertical=gameObject.AddComponent<VerticalLayoutGroup>();vertical.spacing=12;
                vertical.childControlWidth=true;vertical.childControlHeight=true;
                vertical.childForceExpandWidth=true;vertical.childForceExpandHeight=false;
            }
            horizontal.enabled=!column;if(vertical)vertical.enabled=column;
            float desired=column?required*count+12*Mathf.Max(0,count-1):Mathf.Max(BaseHeight,required);
            if(Mathf.Abs(layout.preferredHeight-desired)>.5f)
            {layout.minHeight=desired;layout.preferredHeight=desired;LayoutRebuilder.MarkLayoutForRebuild(rect);}
        }
    }

    internal sealed class LumenMenuTabLayout : MonoBehaviour
    {
        private void LateUpdate()
        {
            var rect=(RectTransform)transform;var grid=GetComponent<GridLayoutGroup>();
            int columns=rect.rect.width<760?2:4;
            float height=rect.rect.width<760?92:64;
            var desired=new Vector2(Mathf.Max(80,(rect.rect.width-(columns-1)*10)/columns),height);
            if(grid.cellSize!=desired){grid.cellSize=desired;grid.constraintCount=columns;}
            var element=GetComponent<LayoutElement>();float total=height*(4/columns)+(4/columns-1)*10;
            if(Mathf.Abs(element.preferredHeight-total)>.5f){element.minHeight=total;element.preferredHeight=total;}
        }
    }
}
