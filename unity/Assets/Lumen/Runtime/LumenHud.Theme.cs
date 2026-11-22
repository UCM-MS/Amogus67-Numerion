using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Lumen.Core;
using System.Collections.Generic;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        private static readonly Color Brass = new Color(.91f, .67f, .30f);
        private static readonly Color Chalk = new Color(.91f, .93f, .94f);
        private static readonly Color SoftInk = new Color(.065f, .085f, .105f, .94f);
        private static readonly Color Frame = new Color(.32f, .38f, .43f, .6f);
        private RectTransform hudBrand, hudStats, hudQuest, hudControls, hudMenu, hudChat, hudInteraction, hudNotification, hudGuide, hudQuickbar;
        private RectTransform modalSafeRoot, modalPanel;
        private Text modalHeading, modalKicker;
        private Button modalClose;
        private GameObject titlePlayerCard;
        private RectTransform titleNavigation, menuEntryRow;
        private int menuEntryIndex;
        private Button tutorialSkip;
        private Font headingFont, strongFont;
        private static Font fallbackFont;
        private int layoutWidth = -1, layoutHeight = -1, menuSection;
        private Rect layoutSafeArea;
        private float interfaceScale = 1;
        private bool lastCompact;
        private bool lastTouchInterface;
        private Vector2 layoutCssSize;
#if UNITY_EDITOR
        private Vector2 layoutPreviewSize;
#endif
        private WindowKind windowKind;
        private enum WindowKind { Page, Title, Menu }

        private Vector2 UiViewportSize
        {
            get
            {
#if UNITY_EDITOR
                if(layoutPreviewSize.x>0&&layoutPreviewSize.y>0)return layoutPreviewSize;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
                if(LumenWebPlatform.CssViewportWidth>0&&LumenWebPlatform.CssViewportHeight>0)
                    return new Vector2(LumenWebPlatform.CssViewportWidth,LumenWebPlatform.CssViewportHeight);
#endif
                var pixels=new Vector2(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));
#if !UNITY_WEBGL
                float referenceScale=Mathf.Max(1,Mathf.Min(pixels.x/1600f,pixels.y/900f));
                return pixels/referenceScale;
#else
                return pixels;
#endif
            }
        }
        private bool CompactViewport=>UiViewportSize.x<1100||UiViewportSize.y<720;
        private float MinimumTargetSize=>44f/Mathf.Min(1,interfaceScale);
        private bool TouchInterface
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return LumenWebPlatform.TouchViewport;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        private void ConfigureInterfaceScale(float value)
        { interfaceScale = Mathf.Clamp(value, .85f, 1.2f); ApplyViewportLayout(true); }

        private void ApplyViewportLayout(bool force = false)
        {
            if (canvas == null || Screen.width < 1 || Screen.height < 1) return;
            Vector2 viewport=UiViewportSize;
            bool compact = CompactViewport, portrait=viewport.y>viewport.x;
            var scaler = canvas.GetComponent<CanvasScaler>();
            var reference=viewport/interfaceScale;
            if (scaler.referenceResolution != reference) { scaler.referenceResolution = reference; force = true; }
            scaler.matchWidthOrHeight=0;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            if (!force && layoutWidth == Screen.width && layoutHeight == Screen.height && layoutSafeArea == safe && lastCompact==compact&&layoutCssSize==viewport&&lastTouchInterface==TouchInterface) return;
            layoutWidth = Screen.width; layoutHeight = Screen.height; layoutSafeArea = safe;lastCompact=compact;layoutCssSize=viewport;lastTouchInterface=TouchInterface;
            Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            if (hudRoot) Stretch(hudRoot, min, max);
            if (modalSafeRoot) Stretch(modalSafeRoot, min, max);

            float safeWidth=reference.x*safe.width/Screen.width,safeHeight=reference.y*safe.height/Screen.height;
            foreach(var button in canvas.GetComponentsInChildren<LumenButtonLayout>(true))
            {
                button.MinimumHeight=MinimumTargetSize;
                var element=button.GetComponent<LayoutElement>();if(element)element.minHeight=button.MinimumHeight;
            }
            foreach(var tabs in canvas.GetComponentsInChildren<LumenMenuTabLayout>(true)) tabs.MinimumCellHeight=MinimumTargetSize;
            foreach(var scrollbar in canvas.GetComponentsInChildren<LumenScrollbarTarget>(true)) scrollbar.Configure(MinimumTargetSize);
            if (hudBrand)
            {
                hudBrand.gameObject.SetActive(!compact);
                Anchor(hudBrand,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-16),new Vector2(154,44));
                Anchor(hudStats,new Vector2(1,1),new Vector2(1,1),new Vector2(compact?-108:-16,-12),new Vector2(Mathf.Min(260,safeWidth-132),54));
                Anchor(hudMenu,new Vector2(1,compact?1:0),new Vector2(1,compact?1:0),new Vector2(-12,compact?-12:16),new Vector2(84,MinimumTargetSize));
                Anchor(hudChat,TouchInterface?new Vector2(.5f,0):Vector2.zero,TouchInterface?new Vector2(.5f,0):Vector2.zero,new Vector2(TouchInterface?0:16,TouchInterface?16:62),new Vector2(96,MinimumTargetSize));
                Anchor(hudQuest,new Vector2(0,1),new Vector2(0,1),new Vector2(16,compact&&!portrait?-12:-72),new Vector2(Mathf.Min(310,safeWidth-32),64));
                Anchor(hudControls,Vector2.zero,Vector2.zero,new Vector2(16,16),new Vector2(310,36));
                hudControls.gameObject.SetActive(viewport.x>=1100&&!TouchInterface);
                hudQuickbar.gameObject.SetActive(viewport.x>=1000&&!TouchInterface);
                Anchor(hudQuickbar,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,16),new Vector2(360,MinimumTargetSize));
                Anchor(hudInteraction,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,TouchInterface?160:74),new Vector2(Mathf.Min(520,safeWidth-32),64));
                interact.gameObject.SetActive(!TouchInterface);
                Anchor(hudNotification,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,portrait?-144:-12),new Vector2(Mathf.Min(480,safeWidth-32),54));
                Anchor(hudGuide,new Vector2(portrait?0:1,1),new Vector2(portrait?0:1,1),new Vector2(portrait?16:-16,portrait?-144:-80),new Vector2(Mathf.Min(300,safeWidth-32),compact?116:154));
                tutorialText.gameObject.SetActive(false);
                tutorialSkip.gameObject.SetActive(!compact);
            }
            if (modalPanel)
            {
                if (windowKind == WindowKind.Title)
                {
                    Anchor(modalPanel,Vector2.zero,Vector2.zero,new Vector2(16,20),new Vector2(Mathf.Min(360,safeWidth-32),Mathf.Min(260,safeHeight-120)));
                    if (titlePlayerCard) titlePlayerCard.SetActive(!compact);
                    if(titleNavigation)
                    {
                        float width=portrait?safeWidth-32:Mathf.Min(760,safeWidth-280);
                        int columns=width<520?2:4;float cell=MinimumTargetSize;
                        Anchor(titleNavigation,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-16),new Vector2(width,cell*(4/columns)+8*(4/columns-1)));
                    }
                }
                else Anchor(modalPanel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(Mathf.Min(windowKind==WindowKind.Menu?980:1100,safeWidth-24),Mathf.Min(740,safeHeight-24)));
                var stack = modalPanel.GetComponent<VerticalLayoutGroup>();
                if (stack) {stack.padding=new RectOffset(16,16,16,16);stack.spacing=8;}
                if(titlePlayerCard)
                {
                    var identity=titlePlayerCard.GetComponent<RectTransform>();
                    Anchor(identity,new Vector2(1,1),new Vector2(1,1),new Vector2(-16,-16),new Vector2(230,112));
                }
            }
        }

        private RectTransform HudBlock(string name, Color color)
        {
            var image = ImageNode(name, hudRoot, color); image.raycastTarget = false;
            var stack = Stack(image.rectTransform, 8, 4);
            return stack;
        }

        private void Rule(Transform parent, Color color)
        {
            var line=ImageNode("Fine rule",parent,color);line.raycastTarget=false;
            SetHeight(line.rectTransform,1);
        }

        private Button NavigationLink(Transform parent, string title, Action action, bool primary = false, bool dark = false)
        {
            var button=ButtonNode(parent,title,action,primary?Lime:Lavender,primary?ButtonInk:Ink,28);
            var caption=ButtonText(button);caption.alignment=TextAnchor.MiddleLeft;
            caption.font=ChooseFont(title,strongFont!=null?strongFont:font);
            return button;
        }

        private void MenuEntry(string title, string description, Action action)
        {
            if(menuEntryIndex++%2==0) menuEntryRow=CardRow(body);
            var card=Card(menuEntryRow,Cream);
            NavigationLink(card,title+"   →",action);
            Label(card,description,23,Muted);
        }

        private RectTransform CardRow(Transform parent)
        {
            var row=Row(parent,0);
            row.GetComponent<LumenAdaptiveRow>().StackCards=true;
            return row;
        }

        private RectTransform TextColumn(Transform parent)
        {
            var column=new GameObject("Details",typeof(RectTransform),typeof(LayoutElement)).GetComponent<RectTransform>();column.SetParent(parent,false);
            column.GetComponent<LayoutElement>().flexibleWidth=1;column.GetComponent<LayoutElement>().minWidth=0;
            return Stack(column,0,3);
        }

        private RectTransform CSSRow(Transform parent,float minimumHeight=0,bool keepHorizontal=false)
        {
            var row=Row(parent,0);var group=row.GetComponent<LumenAdaptiveRow>();
            group.spacing=6;group.BaseHeight=minimumHeight;group.KeepHorizontal=keepHorizontal;return row;
        }

        private Text CompactLabel(Transform parent,string value,int pixels,Color color)
        {
            var label=Label(parent,value,pixels,color);label.fontSize=pixels;
            label.font=ChooseFont(value,pixels>=20?strongFont:font);label.GetComponent<LayoutElement>().minHeight=pixels*1.25f;
            return label;
        }

        private Button CompactButton(Transform parent,string value,Action action,Color background,int pixels=17)
        {
            var button=ButtonNode(parent,value,action,background,null,26);var caption=ButtonText(button);caption.fontSize=pixels;
            caption.GetComponent<LayoutElement>().minHeight=pixels*1.25f;
            caption.rectTransform.offsetMin=new Vector2(12,6);caption.rectTransform.offsetMax=new Vector2(-12,-6);
            button.GetComponent<LayoutElement>().minHeight=MinimumTargetSize;button.GetComponent<LumenButtonLayout>().MinimumHeight=MinimumTargetSize;
            return button;
        }

        private RectTransform CompactCard(Transform parent,Color color)
        {
            var card=Card(parent,color);var stack=card.GetComponent<VerticalLayoutGroup>();stack.padding=new RectOffset(8,8,8,8);stack.spacing=4;return card;
        }

        private RectTransform BuildQuestionBlock(Transform parent,Question question,Action<int> submit)
        {
            var card=CompactCard(parent,Cream);card.name="Question and answers";
            var prompt=CompactLabel(card,question.Prompt,20,Ink);prompt.gameObject.name="Question prompt";
            var choices=new List<Button>();RectTransform answerRow=null;
            for(int i=0;i<question.Options.Length;i++)
            {
                int option=i;
                if(i%2==0){answerRow=CSSRow(card);answerRow.GetComponent<LumenAdaptiveRow>().MinimumHorizontalWidth=520;}
                var answer=CompactButton(answerRow,((char)('A'+i))+"  "+question.Options[i],()=>{foreach(var choice in choices)choice.interactable=false;submit(option);},Lavender,18);
                answer.gameObject.name="Answer "+(char)('A'+i);choices.Add(answer);
            }
            return card;
        }

        private void AddScrollIndicator(ScrollRect scroll,Transform parent,RectTransform viewport)
        {
            viewport.offsetMax=new Vector2(-48,viewport.offsetMax.y);
            var track=ImageNode("Scroll position",parent,Color.clear);
            Stretch(track.rectTransform,new Vector2(1,0),Vector2.one);track.rectTransform.offsetMin=new Vector2(-44,0);track.rectTransform.offsetMax=Vector2.zero;
            var rail=ImageNode("Scrollbar rail",track.transform,new Color(.045f,.06f,.075f,.7f));rail.raycastTarget=false;
            Stretch(rail.rectTransform,new Vector2(.5f,0),new Vector2(.5f,1));rail.rectTransform.offsetMin=new Vector2(-2,0);rail.rectTransform.offsetMax=new Vector2(2,0);
            var handle=ImageNode("Scroll handle",track.transform,Color.clear);Stretch(handle.rectTransform,Vector2.zero,Vector2.one);
            var grip=ImageNode("Visible scrollbar grip",handle.transform,new Color(.47f,.53f,.57f));grip.raycastTarget=false;
            Stretch(grip.rectTransform,new Vector2(.5f,0),new Vector2(.5f,1));grip.rectTransform.offsetMin=new Vector2(-3,0);grip.rectTransform.offsetMax=new Vector2(3,0);
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle.rectTransform;bar.targetGraphic=grip;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            var sizing=scroll.gameObject.AddComponent<LumenScrollbarTarget>();sizing.Track=track.rectTransform;sizing.Viewport=viewport;sizing.Configure(MinimumTargetSize);
        }

        private void DisablePanelScrolling(ScrollRect scroll)
        {
            scroll.horizontal=false;scroll.vertical=false;scroll.StopMovement();
            if(scroll.verticalScrollbar){scroll.verticalScrollbar.gameObject.SetActive(false);scroll.verticalScrollbar=null;}
            var sizing=scroll.GetComponent<LumenScrollbarTarget>();if(sizing)sizing.ReserveViewport=false;
            scroll.viewport.offsetMax=Vector2.zero;
        }

        private static Font ChooseFont(string value, Font preferred)
        {
            if (fallbackFont == null) fallbackFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (preferred == null) return fallbackFont;
            foreach(char c in value??"") if (!char.IsWhiteSpace(c) && !preferred.HasCharacter(c)) return fallbackFont;
            return preferred;
        }
    }

    internal sealed class LumenScrollbarTarget : MonoBehaviour
    {
        public RectTransform Track, Viewport;
        public bool ReserveViewport=true;
        public void Configure(float width)
        {
            if(!ReserveViewport||!Track||!Viewport)return;
            Track.offsetMin=new Vector2(-width,0);Track.offsetMax=Vector2.zero;
            Viewport.offsetMax=new Vector2(-width-4,Viewport.offsetMax.y);
        }
    }

    /// <summary>One layout group handles both axes; narrow rows never add a conflicting second LayoutGroup.</summary>
    internal sealed class LumenAdaptiveRow : HorizontalOrVerticalLayoutGroup
    {
        public float BaseHeight;
        public bool StackCards;
        public bool KeepHorizontal;
        public float MinimumHorizontalWidth=420;
        private bool column;

        public override float minHeight => Mathf.Max(BaseHeight,base.minHeight);
        public override float preferredHeight => Mathf.Max(minHeight,base.preferredHeight);

        private bool RefreshColumn()
        {
            bool buttonsOnly=rectChildren.Count>1;
            foreach(var child in rectChildren)buttonsOnly&=child.GetComponent<Button>()!=null;
            bool next=!KeepHorizontal&&rectChildren.Count>1&&rectTransform.rect.width<(StackCards?680:MinimumHorizontalWidth)&&(buttonsOnly||StackCards);
            bool changed=next!=column;column=next;return changed;
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            RefreshColumn();CalcAlongAxis(0,column);
        }

        public override void CalculateLayoutInputVertical()
        {
            RefreshColumn();CalcAlongAxis(1,column);
        }

        public override void SetLayoutHorizontal()
        {
            // A parent can assign our new width after horizontal measurement in the same rebuild.
            if(RefreshColumn())CalcAlongAxis(0,column);
            SetChildrenAlongAxis(0,column);
        }

        public override void SetLayoutVertical() { SetChildrenAlongAxis(1,column); }
    }

    internal sealed class LumenMenuTabLayout : MonoBehaviour
    {
        public float MinimumCellHeight=44;
        private void LateUpdate()
        {
            var rect=(RectTransform)transform;var grid=GetComponent<GridLayoutGroup>();
            int columns=rect.rect.width<520?2:4;
            float height=MinimumCellHeight;
            var desired=new Vector2(Mathf.Max(80,(rect.rect.width-(columns-1)*grid.spacing.x)/columns),height);
            if(grid.cellSize!=desired){grid.cellSize=desired;grid.constraintCount=columns;}
            var element=GetComponent<LayoutElement>()??gameObject.AddComponent<LayoutElement>();element.minWidth=0;float total=height*(4/columns)+(4/columns-1)*grid.spacing.y;
            if(Mathf.Abs(element.preferredHeight-total)>.5f){element.minHeight=total;element.preferredHeight=total;}
        }
    }

    /// <summary>Pointer and keyboard focus share an amber outline; unavailable actions remain readable.</summary>
    internal sealed class LumenButtonFocus : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        private Button button;
        private Text caption;
        private Image surface;
        private Outline border;
        private Color background,foreground,accent,frame;
        private bool hovered,selected,active,available;
        public void Initialize(Button owner,Text text,Outline edge,bool isActive,Color focusColor,Color borderColor)
        {
            button=owner;caption=text;border=edge;surface=owner.GetComponent<Image>();background=surface.color;foreground=text.color;
            accent=focusColor;frame=borderColor;active=isActive;Apply();
        }
        public void MarkActive(bool value) { active=value;if(surface)background=surface.color;Apply(); }
        private void LateUpdate() { if(button&&available!=button.IsInteractable())Apply(); }
        private void Apply()
        {
            if(!button||!caption||!surface||!border)return;
            available=button.IsInteractable();surface.color=available?background:new Color(.11f,.135f,.16f);
            caption.color=available?foreground:new Color(.53f,.58f,.62f);
            border.effectColor=available&&(hovered||selected||active)?accent:frame;
        }
        public void OnPointerEnter(PointerEventData eventData) { hovered=true;Apply(); }
        public void OnPointerExit(PointerEventData eventData) { hovered=false;Apply(); }
        public void OnSelect(BaseEventData eventData) { selected=true;Apply(); }
        public void OnDeselect(BaseEventData eventData) { selected=false;Apply(); }
        private void OnEnable() { Apply(); }
        private void OnDisable() { hovered=false;selected=false; }
    }
}
