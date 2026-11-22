using Lumen.World;
using UnityEngine;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public static class LumenEmotes
    {
        public static readonly string[] Ids = { "wave", "cheer", "heart", "laugh", "think", "gg" };
        public static bool IsValid(string id) => id == "wave" || id == "cheer" || id == "heart" || id == "laugh" || id == "think" || id == "gg";
        public static string Label(string id)
        {
            switch (id)
            {
                case "wave": return "Ahoj";
                case "cheer": return "Super";
                case "heart": return "Srdce";
                case "laugh": return "Smiech";
                case "think": return "Myslím";
                case "gg": return "GG";
                default: return "Reakcia";
            }
        }
    }

    /// <summary>Original vector reactions. No emoji font, sprites, per-frame arrays or generated assets.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LumenEmoteGraphic : MaskableGraphic
    {
        public string EmoteId = "wave";
        public bool ReduceMotion;
        public bool Animate = true;
        internal bool BubbleBackdrop;

        private static readonly Color Ink = new Color(.10f,.15f,.19f);
        private static readonly Color Gold = new Color(.96f,.74f,.35f);
        private static readonly Color Pale = new Color(1,.95f,.80f);
        private static readonly Color Coral = new Color(.94f,.43f,.46f);
        private static readonly Vector2[] Heart = CreateHeart();
        private static readonly Vector2[] Frame = {
            new Vector2(-.88f,-.87f),new Vector2(.88f,-.87f),new Vector2(1.02f,-.73f),new Vector2(1.02f,.77f),
            new Vector2(.88f,.91f),new Vector2(-.88f,.91f),new Vector2(-1.02f,.77f),new Vector2(-1.02f,-.73f)
        };
        private float phase, nextMeshTime;
        private bool lastMotion;
        private string lastId;
        private bool lastBackdrop;
        private Vector2 center, iconOffset;
        private float unit, cosine = 1, sine, zoom = 1;

        protected override void Awake()
        {
            base.Awake(); raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable(); raycastTarget = false;
            lastId = null; nextMeshTime = 0; SetVerticesDirty();
        }

        private void Update()
        {
            bool motion = Animate && !ReduceMotion && !WorldAnimation.Frozen;
            bool changed = lastId != EmoteId || lastMotion != motion || lastBackdrop != BubbleBackdrop;
            if (motion) phase += Mathf.Min(Time.unscaledDeltaTime,.10f);
            // UI meshes update at most thirty times per second, independent of game time scale.
            if (changed || (motion && Time.unscaledTime >= nextMeshTime))
            {
                lastId = EmoteId; lastMotion = motion; lastBackdrop = BubbleBackdrop;
                nextMeshTime = Time.unscaledTime + 1f/30f; SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            center = rect.center; unit = Mathf.Min(rect.width,rect.height)*.43f;
            TransformIcon(0,1,Vector2.zero);
            if (BubbleBackdrop)
            {
                Polygon(mesh,Frame,0,0,1,new Color(.055f,.08f,.11f,.98f));
                Triangle(mesh,new Vector2(-.19f,-.82f),new Vector2(.19f,-.82f),new Vector2(0,-1.12f),new Color(.055f,.08f,.11f,.98f));
                Polygon(mesh,Frame,0,.015f,.95f,new Color(.16f,.21f,.25f,.98f));
                Line(mesh,-.29f,.88f,.29f,.88f,.025f,new Color(.85f,.66f,.34f));
            }
            float t = Animate && !ReduceMotion && !WorldAnimation.Frozen ? phase : 0;
            float size = BubbleBackdrop ? .77f : 1;
            switch (EmoteId)
            {
                case "cheer": DrawCheer(mesh,t,size); break;
                case "heart": DrawHeart(mesh,t,size); break;
                case "laugh": DrawLaugh(mesh,t,size); break;
                case "think": DrawThink(mesh,t,size); break;
                case "gg": DrawRibbon(mesh,t,size); break;
                default: DrawWave(mesh,t,size); break;
            }
        }

        private void DrawWave(VertexHelper mesh,float t,float size)
        {
            TransformIcon(Mathf.Sin(t*7)*.16f,size,new Vector2(0,.02f));
            // Individual rounded fingers and the thumb make a hand readable at forty-four pixels.
            Finger(mesh,-.26f,.02f,-.27f,.56f,.14f);
            Finger(mesh,-.08f,.10f,-.08f,.82f,.14f);
            Finger(mesh,.10f,.10f,.12f,.76f,.14f);
            Finger(mesh,.27f,.02f,.30f,.56f,.13f);
            Finger(mesh,-.23f,-.15f,-.56f,.19f,.16f);
            Box(mesh,-.22f,-.65f,.43f,.43f,Ink);
            Box(mesh,-.16f,-.63f,.31f,.40f,Gold);
            Disc(mesh,.015f,-.13f,.38f,.39f,Ink);
            Disc(mesh,.015f,-.12f,.325f,.34f,Gold);
            Arc(mesh,-.02f,-.07f,.21f,.19f,3.7f,5.0f,.027f,new Color(.66f,.40f,.14f),5);
            Line(mesh,.48f,.57f,.62f,.68f,.035f,Gold);
            Line(mesh,.59f,.30f,.79f,.34f,.035f,Gold);
        }

        private void Finger(VertexHelper mesh,float x0,float y0,float x1,float y1,float width)
        {
            Capsule(mesh,x0,y0,x1,y1,width+.055f,Ink);
            Capsule(mesh,x0,y0,x1,y1,width,Gold);
        }

        private void DrawCheer(VertexHelper mesh,float t,float size)
        {
            TransformIcon(0,size*(1+Mathf.Sin(t*4)*.035f),Vector2.zero);
            Triangle(mesh,new Vector2(-.32f,-.32f),new Vector2(-.10f,-.35f),new Vector2(-.39f,-.90f),Gold);
            Triangle(mesh,new Vector2(.10f,-.35f),new Vector2(.32f,-.32f),new Vector2(.39f,-.90f),Gold);
            Star(mesh,0,.04f,.73f,.35f,Ink);
            Star(mesh,0,.04f,.64f,.31f,Gold);
            Spark(mesh,-.77f,.60f,.10f+.025f*Mathf.Sin(t*5),Pale);
            Spark(mesh,.78f,.69f,.12f+.025f*Mathf.Sin(t*5+1.8f),Gold);
            Spark(mesh,.78f,-.40f,.08f+.02f*Mathf.Sin(t*5+3),Pale);
        }

        private void DrawHeart(VertexHelper mesh,float t,float size)
        {
            TransformIcon(0,size*(1+Mathf.Sin(t*5)*.075f),new Vector2(0,.035f));
            Polygon(mesh,Heart,0,-.04f,.93f,Ink);
            Polygon(mesh,Heart,0,-.025f,.83f,Coral);
            Capsule(mesh,-.40f,.25f,-.28f,.36f,.065f,new Color(1,.79f,.75f));
        }

        private void DrawLaugh(VertexHelper mesh,float t,float size)
        {
            TransformIcon(Mathf.Sin(t*6)*.035f,size,new Vector2(0,Mathf.Sin(t*6)*.045f));
            Disc(mesh,0,0,.76f,.76f,Ink,24); Disc(mesh,0,.015f,.68f,.68f,Gold,24);
            Capsule(mesh,-.43f,.17f,-.29f,.29f,.065f,Ink); Capsule(mesh,-.29f,.29f,-.15f,.17f,.065f,Ink);
            Capsule(mesh,.15f,.17f,.29f,.29f,.065f,Ink); Capsule(mesh,.29f,.29f,.43f,.17f,.065f,Ink);
            Disc(mesh,0,-.23f,.31f,.255f,Ink,20);
            Box(mesh,-.205f,-.12f,.41f,.08f,Pale);
            Disc(mesh,0,-.36f,.17f,.07f,Coral);
        }

        private void DrawThink(VertexHelper mesh,float t,float size)
        {
            TransformIcon(0,size,new Vector2(-.07f,-.02f));
            Disc(mesh,-.10f,-.04f,.64f,.64f,Ink,24); Disc(mesh,-.10f,-.025f,.57f,.57f,Gold,24);
            Capsule(mesh,-.44f,.23f,-.24f,.28f,.05f,Ink);
            Capsule(mesh,.03f,.28f,.22f,.20f,.05f,Ink);
            Disc(mesh,-.31f,.10f,.05f,.065f,Ink); Disc(mesh,.12f,.07f,.05f,.065f,Ink);
            Line(mesh,-.09f,-.20f,.17f,-.15f,.045f,Ink);
            Capsule(mesh,.26f,-.49f,.04f,-.31f,.15f,Ink);
            Capsule(mesh,.24f,-.48f,.04f,-.32f,.10f,Pale);
            for(int i=0;i<3;i++)
            {
                float emphasis=.82f+.18f*Mathf.Sin(t*4-i*1.5f);
                Disc(mesh,.27f+i*.26f,.50f+i*.12f,.065f*emphasis,.065f*emphasis,Pale);
            }
        }

        private void DrawRibbon(VertexHelper mesh,float t,float size)
        {
            TransformIcon(Mathf.Sin(t*2.5f)*.025f,size,new Vector2(0,Mathf.Sin(t*3)*.025f));
            Triangle(mesh,new Vector2(-.72f,-.16f),new Vector2(-.40f,-.18f),new Vector2(-.72f,-.63f),Gold);
            Triangle(mesh,new Vector2(.40f,-.18f),new Vector2(.72f,-.16f),new Vector2(.72f,-.63f),Gold);
            Box(mesh,-.89f,-.39f,1.78f,.92f,Ink);
            Box(mesh,-.81f,-.31f,1.62f,.76f,Gold);
            for(int i=0;i<2;i++)
            {
                float x=i==0?-.34f:.34f;
                Arc(mesh,x,.075f,.235f,.235f,Mathf.PI*.23f,Mathf.PI*1.78f,.075f,Ink,12);
                Line(mesh,x+.025f,.015f,x+.225f,.015f,.075f,Ink);
                Line(mesh,x+.225f,.015f,x+.225f,-.12f,.075f,Ink);
            }
        }

        private void TransformIcon(float angle,float scale,Vector2 offset)
        { cosine=Mathf.Cos(angle);sine=Mathf.Sin(angle);zoom=scale;iconOffset=offset; }

        private void Vertex(VertexHelper mesh,float x,float y,Color fill)
        {
            float rx=(x*cosine-y*sine)*zoom+iconOffset.x, ry=(x*sine+y*cosine)*zoom+iconOffset.y;
            var vertex=UIVertex.simpleVert;
            vertex.position=new Vector3(center.x+rx*unit,center.y+ry*unit,0);
            vertex.color=fill*color; vertex.uv0=Vector2.zero; mesh.AddVert(vertex);
        }

        private void Triangle(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Color fill)
        {
            int start=mesh.currentVertCount;Vertex(mesh,a.x,a.y,fill);Vertex(mesh,b.x,b.y,fill);Vertex(mesh,c.x,c.y,fill);
            mesh.AddTriangle(start,start+1,start+2);
        }

        private void Box(VertexHelper mesh,float x,float y,float width,float height,Color fill)
        {
            int start=mesh.currentVertCount;
            Vertex(mesh,x,y,fill);Vertex(mesh,x,y+height,fill);Vertex(mesh,x+width,y+height,fill);Vertex(mesh,x+width,y,fill);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }

        private void Line(VertexHelper mesh,float x0,float y0,float x1,float y1,float width,Color fill)
        {
            Vector2 direction=new Vector2(x1-x0,y1-y0);
            if(direction.sqrMagnitude<.000001f)return;
            Vector2 n=new Vector2(-direction.y,direction.x).normalized*(width*.5f);
            int start=mesh.currentVertCount;
            Vertex(mesh,x0+n.x,y0+n.y,fill);Vertex(mesh,x1+n.x,y1+n.y,fill);Vertex(mesh,x1-n.x,y1-n.y,fill);Vertex(mesh,x0-n.x,y0-n.y,fill);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }

        private void Capsule(VertexHelper mesh,float x0,float y0,float x1,float y1,float width,Color fill)
        { Line(mesh,x0,y0,x1,y1,width,fill);Disc(mesh,x0,y0,width*.5f,width*.5f,fill,10);Disc(mesh,x1,y1,width*.5f,width*.5f,fill,10); }

        private void Disc(VertexHelper mesh,float x,float y,float rx,float ry,Color fill,int sides=16)
        {
            int start=mesh.currentVertCount;Vertex(mesh,x,y,fill);
            for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;Vertex(mesh,x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry,fill);}
            for(int i=0;i<sides;i++)mesh.AddTriangle(start,start+i+1,start+i+2);
        }

        private void Arc(VertexHelper mesh,float x,float y,float rx,float ry,float from,float to,float width,Color fill,int segments)
        {
            float oldX=x+Mathf.Cos(from)*rx,oldY=y+Mathf.Sin(from)*ry;
            for(int i=1;i<=segments;i++)
            {
                float a=Mathf.Lerp(from,to,i/(float)segments),newX=x+Mathf.Cos(a)*rx,newY=y+Mathf.Sin(a)*ry;
                Line(mesh,oldX,oldY,newX,newY,width,fill);oldX=newX;oldY=newY;
            }
        }

        private void Polygon(VertexHelper mesh,Vector2[] points,float x,float y,float scale,Color fill)
        {
            int start=mesh.currentVertCount;Vertex(mesh,x,y,fill);
            for(int i=0;i<points.Length;i++)Vertex(mesh,x+points[i].x*scale,y+points[i].y*scale,fill);
            for(int i=0;i<points.Length;i++)mesh.AddTriangle(start,start+i+1,start+(i+1)%points.Length+1);
        }

        private void Star(VertexHelper mesh,float x,float y,float outer,float inner,Color fill)
        {
            int start=mesh.currentVertCount;Vertex(mesh,x,y,fill);
            for(int i=0;i<10;i++){float a=Mathf.PI*.5f+i*Mathf.PI/5,r=i%2==0?outer:inner;Vertex(mesh,x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,fill);}
            for(int i=0;i<10;i++)mesh.AddTriangle(start,start+i+1,start+(i+1)%10+1);
        }

        private void Spark(VertexHelper mesh,float x,float y,float radius,Color fill)
        {
            Triangle(mesh,new Vector2(x-radius*.5f,y),new Vector2(x,y+radius),new Vector2(x+radius*.5f,y),fill);
            Triangle(mesh,new Vector2(x-radius*.5f,y),new Vector2(x,y-radius),new Vector2(x+radius*.5f,y),fill);
        }

        private static Vector2[] CreateHeart()
        {
            var points=new Vector2[36];
            for(int i=0;i<points.Length;i++)
            {
                float a=i*Mathf.PI*2/points.Length,s=Mathf.Sin(a);
                points[i]=new Vector2(s*s*s*.85f,(13*Mathf.Cos(a)-5*Mathf.Cos(2*a)-2*Mathf.Cos(3*a)-Mathf.Cos(4*a))/19f);
            }
            return points;
        }
    }

    /// <summary>One retained, non-interactive canvas per avatar. Avatar destruction owns all cleanup.</summary>
    public sealed class LumenEmoteBubble : MonoBehaviour
    {
        private RectTransform bubble;
        private Canvas bubbleCanvas;
        private LumenEmoteGraphic graphic;
        private Camera billboardCamera;
        private float expiresAt;

        public static void Show(Transform avatar,string emoteId,bool reduceMotion)
        {
            if(!avatar||!LumenEmotes.IsValid(emoteId))return;
            var holder=avatar.GetComponent<LumenEmoteBubble>();
            if(!holder)holder=avatar.gameObject.AddComponent<LumenEmoteBubble>();
            holder.Display(emoteId,reduceMotion);
        }

        public static void Clear(Transform avatar)
        {
            if(!avatar)return;
            var holder=avatar.GetComponent<LumenEmoteBubble>();if(holder)holder.Hide();
        }

        private void Display(string emoteId,bool reduceMotion)
        {
            if(!bubble)CreateBubble();
            graphic.EmoteId=emoteId;graphic.ReduceMotion=reduceMotion;graphic.Animate=true;
            graphic.SetVerticesDirty();expiresAt=Time.unscaledTime+4;
            bubble.gameObject.SetActive(true);enabled=true;FaceCamera();
        }

        private void CreateBubble()
        {
            var host=new GameObject("Room reaction bubble",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
            bubble=host.GetComponent<RectTransform>();bubble.SetParent(transform,false);
            bubble.localPosition=new Vector3(0,2.9f,0);bubble.localScale=Vector3.one*.01f;bubble.sizeDelta=new Vector2(88,82);
            bubbleCanvas=host.GetComponent<Canvas>();bubbleCanvas.renderMode=RenderMode.WorldSpace;bubbleCanvas.sortingOrder=80;
            var group=host.GetComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
            var art=new GameObject("Original vector reaction",typeof(RectTransform),typeof(CanvasRenderer),typeof(LumenEmoteGraphic));
            var rect=art.GetComponent<RectTransform>();rect.SetParent(bubble,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.anchoredPosition=new Vector2(0,4);rect.sizeDelta=new Vector2(80,74);
            graphic=art.GetComponent<LumenEmoteGraphic>();graphic.BubbleBackdrop=true;graphic.raycastTarget=false;graphic.color=Color.white;
        }

        private void LateUpdate()
        {
            if(!bubble){enabled=false;return;}
            if(Time.unscaledTime>=expiresAt){Hide();return;}
            FaceCamera();
        }

        private void FaceCamera()
        {
            if(!billboardCamera||!billboardCamera.isActiveAndEnabled)billboardCamera=Camera.main;
            if(!billboardCamera||!bubble)return;
            bubble.rotation=billboardCamera.transform.rotation;
            if(bubbleCanvas.worldCamera!=billboardCamera)bubbleCanvas.worldCamera=billboardCamera;
        }

        private void Hide()
        { if(bubble)bubble.gameObject.SetActive(false);expiresAt=0;enabled=false; }

        private void OnDisable()
        { if(bubble)bubble.gameObject.SetActive(false); }
    }
}
