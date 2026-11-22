#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lumen.Core;
using Lumen.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Lumen.Editor
{
    /// <summary>Measures production screen builders with real fonts, all authored questions and uGUI layout.</summary>
    public static class UiLayoutValidation
    {
        private static int checks,screens;
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly Action NoAction=()=>{};
        private static readonly Action<int> NoAnswer=_=>{};

        [MenuItem("Numerion/Validate Responsive UI")]
        public static void Run()
        {
            checks=screens=0;
            var errors=new List<string>();
            Application.LogCallback capture=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);};
            Application.logMessageReceived+=capture;
            GameObject root=null;
            try
            {
                root=new GameObject("Numerion UI fit validation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.hideFlags=HideFlags.DontSave;
                root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
                var hud=root.AddComponent<LumenHud>();
                Set(hud,"canvas",root.GetComponent<Canvas>());
                var hudRoot=new GameObject("HUD root",typeof(RectTransform)).GetComponent<RectTransform>();hudRoot.SetParent(root.transform,false);Set(hud,"hudRoot",hudRoot);
                Font regular=Resources.Load<Font>("Fonts/BarlowSemiCondensed-Regular")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Font strong=Resources.Load<Font>("Fonts/BarlowSemiCondensed-SemiBold")??regular;
                Set(hud,"font",regular);Set(hud,"strongFont",strong);Set(hud,"headingFont",strong);
                var catalog=ContentCatalog.CreateDefault();
                var collection=new CompanionCollection {Owned=new List<CompanionProgress>{
                    new CompanionProgress{SpeciesId="ember",Nickname="Iskra"},new CompanionProgress{SpeciesId="bolt",Nickname="Volt"},new CompanionProgress{SpeciesId="astra",Nickname="Astra"}},EquippedId="ember"};
                var battle=PetBattleRules.CreatePractice(collection,"ui-layout-validation",17);
                // Window dimensions include the browser header; the Unity canvas loses 48 CSS pixels.
                foreach(var window in new[]{new Vector2(1280,720),new Vector2(1366,768),new Vector2(1024,768),new Vector2(768,1024)})
                {
                    var viewport=new Vector2(window.x,window.y-48);
                    SetViewport(hud,root,viewport,1);
                    foreach(var question in catalog.Questions)
                    {
                        var content=Open(hud,"Tematický test","MATEMATIKA / 7.–9. ROČNÍK");
                        Call(hud,"BuildStudyQuestionContent",content,question,"9. ročník · otázka 6/6 · náročnosť 4/4",.8f,NoAnswer,NoAction,NoAction);
                        AssertFits(hud,content,viewport,1,"study "+question.Id,true,false);
                        content=Open(hud,"Kolo 5 · Duel hráčov","ARÉNA / 9. ROČNÍK");
                        Call(hud,"BuildBattleFrame",content,battle,0,"VYRIEŠ ÚLOHU");
                        Call(hud,"BuildBattleQuestionContent",content,question,"Matematický útok",NoAnswer);
                        var footer=(RectTransform)Call(hud,"CSSRow",content,0f,false);
                        Call(hud,"CompactButton",footer,"Pomoc",NoAction,Color.gray,17);
                        Call(hud,"CompactButton",footer,"Vysvetlenie a priebeh",NoAction,Color.gray,17);
                        AssertFits(hud,content,viewport,1,"battle "+question.Id,true,true);
                    }
                    var actions=Open(hud,"Kolo 5 · Tréning","ARÉNA / 9. ROČNÍK");
                    Call(hud,"BuildBattleFrame",actions,battle,0,"VYBER ŤAH");
                    Call(hud,"BuildBattleActions",actions,battle,0,(Action<PetBattleActionKind,int>)((kind,slot)=>{}));
                    Call(hud,"CompactButton",actions,"Ťahy a priebeh",NoAction,Color.gray,17);
                    AssertFits(hud,actions,viewport,1,"battle actions",false,true);
                }
                // The 85% preference must not silently reduce physical touch targets below 44 CSS pixels.
                SetViewport(hud,root,new Vector2(1280,672),.85f);
                var scaled=Open(hud,"Tematický test","MATEMATIKA");
                Call(hud,"BuildStudyQuestionContent",scaled,catalog.Questions[0],"Otázka 1/6",0f,NoAnswer,NoAction,NoAction);
                AssertFits(hud,scaled,new Vector2(1280,672),.85f,"85% accessibility scale",true,false);
                ValidateOverflowFallback(hud,root,catalog.Questions[0]);
                ValidateResize(hud,root);
                Require(errors.Count==0,"No actual Unity UI errors: "+string.Join(" | ",errors));
                Debug.Log("NUMERION_UI_LAYOUT_PASSED: "+checks+" checks; "+screens+" real screen layouts; all "+catalog.Questions.Count+" questions in study and battle at 1280x720, 1366x768, 1024x768 and 768x1024 minus48 CSS header; answers visible, fighters side-by-side, 44 CSS targets, wrapped overflow safe.");
            }
            finally {if(root)Object.DestroyImmediate(root);Application.logMessageReceived-=capture;}
        }

        private static void ValidateOverflowFallback(LumenHud hud,GameObject root,Question original)
        {
            SetViewport(hud,root,new Vector2(390,796),1);
            var unusuallyLong=new Question {Prompt=string.Join(" ",Enumerable.Repeat(original.Prompt,16)),Options=original.Options};
            var content=Open(hud,"Dlhá otázka","KONTROLA POSÚVANIA");Call(hud,"BuildQuestionBlock",content,unusuallyLong,NoAnswer);Rebuild(hud,content);
            var scroll=content.GetComponentInParent<ScrollRect>();
            Require(scroll.vertical&&scroll.content.rect.height>scroll.viewport.rect.height,"Unusually long content remains available through vertical scrolling.");
            foreach(var button in Answers(content))Require(button.rect.height>=43.5f,"Overflow never shrinks an answer below its touch minimum.");
        }

        private static void ValidateResize(LumenHud hud,GameObject root)
        {
            var content=Open(hud,"Zmena šírky","REGRESIA");
            var row=(RectTransform)Call(hud,"CSSRow",content,0f,false);
            var first=((Button)Call(hud,"CompactButton",row,"Prvý ťah",NoAction,Color.gray,17)).GetComponent<RectTransform>();
            var second=((Button)Call(hud,"CompactButton",row,"Druhý ťah",NoAction,Color.gray,17)).GetComponent<RectTransform>();
            foreach(float width in new[]{360f,1000f,360f})
            {
                SetViewport(hud,root,new Vector2(width,720),1);Call(hud,"ApplyViewportLayout",true);Rebuild(hud,content);
                bool narrow=width<420;
                Require(row.GetComponents<LayoutGroup>().Length==1,"Resizing retains exactly one LayoutGroup.");
                Require(first.rect.height>=43.5f&&second.rect.height>=43.5f,"Resize preserves 44 CSS touch minimum.");
                Require(narrow?Mathf.Abs(first.anchoredPosition.x-second.anchoredPosition.x)<1:Mathf.Abs(first.anchoredPosition.y-second.anchoredPosition.y)<1,"Actions change orientation without collision.");
            }
        }

        private static void AssertFits(LumenHud hud,RectTransform content,Vector2 viewport,float cssScale,string context,bool answers,bool fighters)
        {
            Rebuild(hud,content);screens++;
            var scroll=content.GetComponentInParent<ScrollRect>();
            Require(content.rect.height<=scroll.viewport.rect.height+1,context+" fits without scrolling at "+viewport+" (content "+content.rect.height+", viewport "+scroll.viewport.rect.height+").");
            foreach(var button in content.GetComponentsInChildren<Button>())
                Require(button.GetComponent<RectTransform>().rect.height*cssScale>=43.5f,context+" retains 44 CSS target: "+button.name);
            if(answers)
            {
                var choices=Answers(content);Require(choices.Length==4,context+" has four answers.");
                foreach(var choice in choices)
                {
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,choice);
                    Require(bounds.min.y>=scroll.viewport.rect.yMin-1&&bounds.max.y<=scroll.viewport.rect.yMax+1,context+" answer is visible: "+choice.name);
                    var text=choice.GetComponentInChildren<Text>();Require(text.fontSize*cssScale>=15,context+" uses readable answer type.");
                    Require(text.preferredHeight<=text.rectTransform.rect.height+1,context+" answer text is not clipped.");
                }
            }
            if(fighters)
            {
                var cards=content.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("Fighter ",StringComparison.Ordinal)).ToArray();
                Require(cards.Length==2,context+" has two fighters.");
                Require(Mathf.Abs(cards[0].anchoredPosition.y-cards[1].anchoredPosition.y)<1,context+" fighters remain side by side.");
                var left=RectTransformUtility.CalculateRelativeRectTransformBounds(content,cards[0]);var right=RectTransformUtility.CalculateRelativeRectTransformBounds(content,cards[1]);
                Require(left.max.x<=right.min.x,context+" fighter cards do not overlap.");
            }
        }

        private static RectTransform[] Answers(RectTransform content)=>content.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Answer ",StringComparison.Ordinal)).Select(b=>b.GetComponent<RectTransform>()).ToArray();
        private static void SetViewport(LumenHud hud,GameObject root,Vector2 viewport,float scale)
        {Set(hud,"layoutPreviewSize",viewport);Set(hud,"interfaceScale",scale);root.GetComponent<RectTransform>().sizeDelta=viewport/scale;}
        private static RectTransform Open(LumenHud hud,string title,string kicker)
        {var old=(GameObject)Get(hud,"modal");if(old)Object.DestroyImmediate(old);Set(hud,"modal",null);Call(hud,"Open",title,kicker);return (RectTransform)Get(hud,"body");}
        private static void Rebuild(LumenHud hud,RectTransform content)
        {var panel=(RectTransform)Get(hud,"modalPanel");for(int pass=0;pass<2;pass++){LayoutRebuilder.ForceRebuildLayoutImmediate(panel);LayoutRebuilder.ForceRebuildLayoutImmediate(content);}}
        private static object Call(LumenHud hud,string method,params object[] args)=>typeof(LumenHud).GetMethod(method,Private).Invoke(hud,args);
        private static object Get(LumenHud hud,string field)=>typeof(LumenHud).GetField(field,Private).GetValue(hud);
        private static void Set(LumenHud hud,string field,object value)=>typeof(LumenHud).GetField(field,Private).SetValue(hud,value);
        private static void Require(bool value,string message){checks++;if(!value)throw new InvalidOperationException("Responsive UI validation: "+message);}
    }
}
#endif
