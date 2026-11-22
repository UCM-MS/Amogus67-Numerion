using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    /// <summary>CSS-sized multitouch controls feed the same gamepad paths as desktop input.</summary>
    public sealed class LumenWebTouchControls : MonoBehaviour
    {
        private GameObject controls;
        private CanvasScaler scaler;
        private RectTransform safe;
        private bool touchRequested, touchChoiceReceived;
        private LumenGame game;
        private Vector2 cssViewport, previousScreen;
        private Rect previousSafe;
        private int resumeFrame;
        private static Sprite circle;
        [Serializable] private sealed class Viewport { public float width, height; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { new GameObject("LUMEN Web Controls").AddComponent<LumenWebTouchControls>(); }
        private IEnumerator Start()
        {
            yield return null;
            controls = new GameObject("Touch controller", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            controls.transform.SetParent(transform, false);
            var canvas = controls.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            scaler = controls.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            safe = new GameObject("Safe touch area", typeof(RectTransform)).GetComponent<RectTransform>(); safe.SetParent(controls.transform, false);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
            var stick = Disc("Pohyb", safe, new Vector2(0, 0), new Vector2(83, 91), new Vector2(126, 126), new Color(.08f,.19f,.23f,.4f));
            var knob = Disc("Palec", stick.transform, new Vector2(.5f,.5f), Vector2.zero, new Vector2(48,48), new Color(.76f,.86f,.73f,.95f));
            knob.GetComponent<Image>().raycastTarget = false;
            var movement = stick.AddComponent<LumenTouchVector>(); movement.controlPath = "<Gamepad>/leftStick"; movement.Knob = knob.GetComponent<RectTransform>();
            Caption(stick.transform, "POHYB", new Vector2(0,-45), 10);
            var look = new GameObject("Potiahni pre kameru", typeof(RectTransform), typeof(Image)); look.transform.SetParent(safe, false);
            var area = look.GetComponent<RectTransform>(); area.anchorMin = new Vector2(.46f,.33f); area.anchorMax = new Vector2(.98f,.72f); area.offsetMin = area.offsetMax = Vector2.zero;
            look.GetComponent<Image>().color = new Color(1,1,1,.006f);
            var camera = look.AddComponent<LumenTouchVector>(); camera.controlPath = "<Gamepad>/rightStick"; camera.Swipe = true;
            Caption(look.transform, "POTIAHNI PRE KAMERU", new Vector2(0,-12), 10);
            TouchButton("Skok ↑", new Vector2(-43,105), "<Gamepad>/buttonSouth");
            TouchButton("Objav", new Vector2(-117,70), "<Gamepad>/buttonWest");
            TouchButton("Beh\npodrž", new Vector2(-117,146), "<Gamepad>/leftStickPress");
            // Browser choice is authoritative, including an explicit OFF received during Start's yield.
            if (!touchChoiceReceived) touchRequested = false;
            UpdateLayout(); controls.SetActive(false);
        }
        public void SetEnabled(string value) { touchChoiceReceived = true; touchRequested = value == "1"; LumenWebPlatform.TouchViewport = touchRequested; ReleaseInput(); }
        public void SetViewport(string json)
        {
            try { var view = JsonUtility.FromJson<Viewport>(json); if (view != null && view.width > 0 && view.height > 0) { cssViewport = new Vector2(view.width, view.height); LumenWebPlatform.CssViewportWidth = view.width; LumenWebPlatform.CssViewportHeight = view.height; } }
            catch (ArgumentException) { }
            ReleaseInput(); UpdateLayout();
        }
        public void ReleaseInput() { resumeFrame = Time.frameCount + 2; if (controls != null) controls.SetActive(false); }
        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseInput(); }
        private void OnApplicationPause(bool paused) { if (paused) ReleaseInput(); }
        private void UpdateLayout()
        {
            if (controls == null) return;
            previousScreen = new Vector2(Screen.width,Screen.height); previousSafe = Screen.safeArea;
            scaler.scaleFactor = cssViewport.x > 0 ? Mathf.Max(.5f, Screen.width / cssViewport.x) : Mathf.Max(1, Screen.dpi > 0 ? Screen.dpi / 160f : 1);
            safe.anchorMin = new Vector2(previousSafe.xMin / Mathf.Max(1,Screen.width),previousSafe.yMin / Mathf.Max(1,Screen.height));
            safe.anchorMax = new Vector2(previousSafe.xMax / Mathf.Max(1,Screen.width),previousSafe.yMax / Mathf.Max(1,Screen.height));
            foreach (var vector in controls.GetComponentsInChildren<LumenTouchVector>(true)) vector.PixelScale = scaler.scaleFactor;
        }
        private void Update()
        {
            if (controls == null) return;
            if (game == null) game = FindFirstObjectByType<LumenGame>();
            if (previousScreen.x != Screen.width || previousScreen.y != Screen.height || previousSafe != Screen.safeArea) { ReleaseInput(); UpdateLayout(); }
            bool visible = touchChoiceReceived && touchRequested && Application.isFocused && game != null && !game.ModalOpen && Time.frameCount >= resumeFrame;
            if (controls.activeSelf != visible) controls.SetActive(visible);
        }
        private void TouchButton(string title, Vector2 position, string path)
        {
            var button = Disc(title, safe, new Vector2(1,0), position, new Vector2(64,64), new Color(.94f,.93f,.85f,.94f));
            button.AddComponent<LumenTouchButton>().controlPath = path; Caption(button.transform, title, Vector2.zero, 13);
        }
        private static GameObject Disc(string title,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var go = new GameObject(title,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=position;rect.sizeDelta=size;
            var image=go.GetComponent<Image>();image.sprite=Circle();image.color=color;
            return go;
        }
        private static void Caption(Transform parent,string title,Vector2 position,int size)
        {
            var go=new GameObject("Caption",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(150,42);
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=title;text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.1f,.22f,.25f);text.raycastTarget=false;
        }
        private static Sprite Circle()
        {
            if(circle!=null)return circle;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);texture.name="LUMEN touch disc";
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float a=Mathf.Clamp01((31.5f-Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f)))*1.5f);texture.SetPixel(x,y,new Color(1,1,1,a));}
            texture.Apply();circle=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f));return circle;
        }
    }
    public sealed class LumenTouchVector : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Knob; public bool Swipe; public float PixelScale=1;
        private string path;private int pointer=int.MinValue,lastDragFrame=-1;private Vector2 origin;
        protected override string controlPathInternal { get=>path;set=>path=value; }
        public void OnPointerDown(PointerEventData e){if(pointer!=int.MinValue)return;pointer=e.pointerId;origin=e.position;if(!Swipe)OnDrag(e);}
        public void OnDrag(PointerEventData e)
        {
            if(e.pointerId!=pointer)return;
            Vector2 value=Swipe ? e.delta / Mathf.Max(.01f,PixelScale) / Mathf.Max(.005f,Time.unscaledDeltaTime) / 500f : (e.position-origin)/Mathf.Max(.01f,PixelScale)/43f;
            value=Vector2.ClampMagnitude(value,1);SendValueToControl(value);lastDragFrame=Time.frameCount;
            if(Knob!=null)Knob.anchoredPosition=value*38;
        }
        public void OnPointerUp(PointerEventData e){if(e.pointerId==pointer)ResetValue();}
        private void LateUpdate(){if(Swipe&&lastDragFrame<Time.frameCount)SendValueToControl(Vector2.zero);}
        private void ResetValue(){pointer=int.MinValue;SendValueToControl(Vector2.zero);if(Knob!=null)Knob.anchoredPosition=Vector2.zero;}
        protected override void OnDisable(){ResetValue();base.OnDisable();}
    }
    public sealed class LumenTouchButton : OnScreenControl, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private string path;private int pointer=int.MinValue;
        protected override string controlPathInternal {get=>path;set=>path=value;}
        public void OnPointerDown(PointerEventData e){if(pointer!=int.MinValue)return;pointer=e.pointerId;SendValueToControl(1f);}
        public void OnPointerUp(PointerEventData e){if(e.pointerId==pointer)ResetValue();}
        public void OnPointerExit(PointerEventData e){if(e.pointerId==pointer)ResetValue();}
        private void ResetValue(){pointer=int.MinValue;SendValueToControl(0f);}
        protected override void OnDisable(){ResetValue();base.OnDisable();}
    }
}
