using System;
using System.Collections.Generic;
using System.Linq;
using Lumen.Core;
using Lumen.Networking;
using Lumen.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using RenderMode = Lumen.Graphics.RenderMode;

namespace Lumen.Runtime
{
    /// <summary>Native, resolution-independent uGUI. Every menu action calls the real game service.</summary>
    public sealed partial class LumenHud : MonoBehaviour
    {
        public bool ModalOpen => modal != null && modal.activeSelf;
        private LumenGame game;
        private Canvas canvas;
        private Font font;
        private Sprite rounded;
        private Texture2D roundedTexture;
        private GameObject modal;
        private RectTransform body;
        private RectTransform hudRoot;
        private GameObject tutorialCard;
        private Text tutorialTitle, tutorialText;
        private Button tutorialAction;
        private bool titleOpen;
        private Text coins, level, xp, objective, status, toast, target, liveNetwork, liveRoster, liveVR;
        private Button vrStart, vrStop;
        private Image xpFill;
        private Button interact;
        private GameObject toastCard, targetCard;
        private float nextRefresh;
        private bool profileRequired;
        private static readonly Color Cream = new Color(.925f, .895f, .82f);
        private static readonly Color Ink = new Color(.075f, .12f, .17f);
        private static readonly Color Muted = new Color(.32f, .36f, .36f);
        private static readonly Color Purple = new Color(.18f, .39f, .40f);
        private static readonly Color Lavender = new Color(.81f, .79f, .72f);
        private static readonly Color Lime = new Color(.75f, .61f, .36f);
        private static readonly Color Mint = new Color(.76f, .83f, .78f);

        public void Initialize(LumenGame owner)
        {
            game = owner;
            font = Resources.Load<Font>("Fonts/BarlowSemiCondensed-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            strongFont = Resources.Load<Font>("Fonts/BarlowSemiCondensed-SemiBold") ?? font;
            headingFont = Resources.Load<Font>("Fonts/CormorantSC-SemiBold") ?? strongFont;
            CreateRoundedSprite();
            var root = new GameObject("LUMEN · Native interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderModeCanvas(); canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("LUMEN · Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            BuildHud(); ApplyViewportLayout(true); Refresh();
        }

        private static UnityEngine.RenderMode RenderModeCanvas() => UnityEngine.RenderMode.ScreenSpaceOverlay;


        private void BuildHud()
        {
            hudRoot = new GameObject("Adventure HUD · safe area", typeof(RectTransform)).GetComponent<RectTransform>();
            hudRoot.SetParent(canvas.transform, false); Stretch(hudRoot, Vector2.zero, Vector2.one);
            hudBrand=HudBlock("Academy signature",SoftInk);
            Label(hudBrand,"L U M E N",28,Chalk);
            hudStats=HudBlock("Progress strip",SoftInk);
            var statsRow=Row(hudStats,32);
            coins=Label(statsRow,"0 M",22,Chalk);coins.fontSize=22;
            level=Label(statsRow,"ÚR. 1",22,Brass);level.fontSize=22;
            xp=Label(statsRow,"0/150 XP",22,Chalk);xp.fontSize=22;
            var track=ImageNode("Experience",hudStats,new Color(.4f,.45f,.45f,.5f));SetHeight(track.rectTransform,3);track.raycastTarget=false;
            xpFill=ImageNode("Experience fill",track.transform,Brass);xpFill.raycastTarget=false;
            xpFill.type=Image.Type.Filled;xpFill.fillMethod=Image.FillMethod.Horizontal;Stretch(xpFill.rectTransform,Vector2.zero,Vector2.one);
            hudQuest=HudBlock("Current objective",SoftInk);
            objective=Label(hudQuest,"Preskúmaj akadémiu",25,Chalk);objective.fontSize=25;
            status=Label(hudQuest,"Sólo svet",20,new Color(.73f,.77f,.74f));status.fontSize=20;
            hudControls=HudBlock("Keyboard hints",new Color(.075f,.12f,.17f,.78f));
            var hint=Label(hudControls,"WASD pohyb    E interakcia    M atlas",20,Chalk);hint.fontSize=20;
            hudMenu=ButtonNode(hudRoot,"PONUKA",ToggleMenu,Lime,Ink,24).GetComponent<RectTransform>();
            hudQuickbar=new GameObject("Field shortcuts",typeof(RectTransform),typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();hudQuickbar.SetParent(hudRoot,false);
            var quick=hudQuickbar.GetComponent<HorizontalLayoutGroup>();quick.spacing=2;quick.childControlWidth=true;quick.childControlHeight=true;quick.childForceExpandWidth=true;
            ButtonNode(hudQuickbar,"Atlas",ShowMap,SoftInk,Chalk,23);
            ButtonNode(hudQuickbar,"Učebnica",ShowStudy,SoftInk,Chalk,23);
            ButtonNode(hudQuickbar,"Spoločníci",ShowPets,SoftInk,Chalk,23);
            hudInteraction=HudBlock("Nearby interaction",SoftInk);targetCard=hudInteraction.gameObject;
            var interactionRow=Row(hudInteraction,50);
            target=Label(interactionRow,"Priblíž sa ku kiosku",24,Chalk);
            interact=ButtonNode(interactionRow,"E · Objaviť",()=>game.Interact(),Lime,Ink,24);SetWidth(interact.GetComponent<RectTransform>(),190);
            hudNotification=HudBlock("Discovery notification",Cream);toastCard=hudNotification.gameObject;
            toast=Label(hudNotification,"",24,Ink);toast.alignment=TextAnchor.MiddleCenter;
            hudGuide=HudBlock("Onboarding objective",Cream);tutorialCard=hudGuide.gameObject;
            tutorialTitle=Label(hudGuide,"TVOJE PRVÉ OBJAVY",23,Purple);
            tutorialText=Label(hudGuide,"",22,Ink);tutorialText.gameObject.SetActive(false);
            tutorialAction=ButtonNode(hudGuide,"Pokračovať",TutorialAction,Lime,Ink,23);
            tutorialSkip=ButtonNode(hudGuide,"Odložiť sprievodcu",()=>game.SuspendTutorial(),Lavender,Ink,20);
            tutorialSkip.GetComponent<LumenButtonLayout>().MinimumHeight=44;
            SetHeight(tutorialSkip.GetComponent<RectTransform>(),44);
        }

        public void Refresh()
        {
            if (game == null) return;
            ApplyViewportLayout();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .12f;
            var p = game.Profile.Progress;
            hudRoot.gameObject.SetActive(!titleOpen && !ModalOpen);
            Put(coins, p.Coins.ToString("N0") + " M");
            Put(level, "ÚR. " + p.Level);
            Put(xp, (p.XP % 150) + "/150 XP");
            xpFill.fillAmount = (p.XP % 150) / 150f;
            var next = game.Learning.GetQuests(game.Profile, DateTime.UtcNow).FirstOrDefault(q => !q.Claimed);
            Put(objective, game.CourseActive ? $"Dráha · {game.CourseSeconds:0.0} s · bod {game.CourseCheckpoint + 1}" : next == null ? "Všetky výpravy splnené" : next.Name + "  " + next.Current + "/" + next.Target);
            Put(status, game.SharedConnected ? "Miestnosť " + game.Online.RoomCode + " · " + game.Online.Room.players.Length + " hráčov" : game.Connected ? game.Session.PlayerCount + " hráčov · LAN" : "Sólo svet · " + CurriculumLabels.Grade(game.Profile.Grade));
            Put(toast, game.Toast); toastCard.SetActive(!string.IsNullOrEmpty(game.Toast) && !ModalOpen);
            bool hasTarget = game.Target != null && !ModalOpen;
            targetCard.SetActive(hasTarget);
            if (hasTarget) Put(target, game.Target.Prompt ?? "Objav nové vedomosti");
            if (liveNetwork != null) Put(liveNetwork, NetworkSummary());
            if (liveRoster != null) Put(liveRoster, game.SharedConnected ? string.Join("\n", game.Online.Room.players.Select(e => e.name + (e.id == game.Online.IdentityId ? "  ·  ty" : ""))) : string.Join("\n", NetworkExplorer.All.Where(e => e != null).Select(e => e.DisplayName + (e.IsOwner ? "  ·  ty" : ""))));
            if (liveVR != null) Put(liveVR, game.VR != null ? game.VR.Status : "VR nie je dostupné v tomto spustení.");
            if (vrStart != null) vrStart.interactable = game.VR != null && !game.VR.Active && !game.VR.Busy;
            if (vrStop != null) vrStop.interactable = game.VR != null && game.VR.Active && !game.VR.Busy;
            tutorialCard.SetActive(game.Save.Tutorial.Active && !game.Save.Tutorial.Complete && !ModalOpen && (!CompactViewport || string.IsNullOrEmpty(game.Toast)));
            if (tutorialCard.activeSelf)
            {
                int step = game.Save.Tutorial.Step;
                Put(tutorialTitle, (step + 1) + "/6  ·  " + AcademyTutorial.Titles[step]);
                Put(tutorialText, AcademyTutorial.Instructions[step]);
                Put(ButtonText(tutorialAction), step < 2 ? "Zobraziť ovládanie" : step < 4 ? "Otvoriť výskumné sekcie" : "Otvoriť spoločníkov");
            }
        }

        private void TutorialAction() { if (game.Save.Tutorial.Step < 2) ShowTutorial(); else if (game.Save.Tutorial.Step < 4) ShowMap(); else ShowPets(); }
        public void ToggleMenu() { if (titleOpen) game.EnterAcademy(); else if (ModalOpen) CloseModal(); else ShowMenu(); }

        public void CloseModal()
        {
            if (profileRequired) { game.Notify("Vyber meno a ročník pre svoju výpravu."); return; }
            if (modal != null && !game.Save.HasCreatedProfile) { ShowTitle(); return; }
            if (modal != null) { modal.SetActive(false); Destroy(modal); }
            modal = null; body = null; liveNetwork = null; liveRoster = null; liveVR = null; vrStart = null; vrStop = null;
            titleOpen = false; hudRoot.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(null);
            nextRefresh = 0;
            FeaturePanel = "";
            ProfileStore.Save(game.Save);
        }


        private void ShowMenu() => ShowMenuGroup(menuSection);

        private void ShowMenuGroup(int section)
        {
            menuSection=Mathf.Clamp(section,0,3);
            Open("Poľný denník","LUMEN / AKADÉMIA");windowKind=WindowKind.Menu;FeaturePanel="MENU";
            var tabs=new GameObject("Journal sections",typeof(RectTransform),typeof(GridLayoutGroup),typeof(LayoutElement),typeof(LumenMenuTabLayout)).GetComponent<RectTransform>();tabs.SetParent(body,false);
            var grid=tabs.GetComponent<GridLayoutGroup>();grid.spacing=new Vector2(10,10);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;
            SetHeight(tabs,64);
            string[] names={"VÝPRAVA","SPOLOČNÍCI","SPOLOČNE","MOŽNOSTI"};
            for(int i=0;i<names.Length;i++) {int selected=i;ButtonNode(tabs,names[i],()=>ShowMenuGroup(selected),i==menuSection?Lime:Lavender,Ink,22);}
            Rule(body,Frame);
            switch(menuSection)
            {
                case 0:
                    MenuEntry("Atlas akadémie","Šesť oblastí matematiky a miesta, ktoré už poznáš.",ShowMap);
                    MenuEntry("Učebnice a testy","Postupy, riešené príklady a test z vybranej témy.",ShowStudy);
                    MenuEntry("Výpravy a denník","Ciele, odmeny a tvoj postup v matematike.",ShowQuests);
                    MenuEntry("Zvládnuté učivo","Prehľad tém, séria a odomknuté tituly.",ShowJournal);
                    MenuEntry("Sprievodca","Ovládanie a prvé kroky v akadémii.",ShowTutorial);
                    break;
                case 1:
                    MenuEntry("Moji spoločníci","Kolekcia, tréning a vybavený pet.",ShowPets);
                    MenuEntry("Súbojová aréna","Zostav tím a vyzvi trénera alebo spoluhráča.",ShowArena);
                    MenuEntry("Artefakty a výbava","Odmeny za správne riešenia, sety a vylepšenia.",ShowArtifacts);
                    MenuEntry("Pomoc spoločníka","Nápoveda k učivu a odporúčaný ďalší krok.",ShowPetHelper);
                    break;
                case 2:
                    MenuEntry("Hrať s priateľmi","Spoločný svet pre aplikáciu aj prehliadač.",ShowMultiplayer);
                    MenuEntry("Preniesť postup","Prepoj profil na druhom zariadení.",ShowCloud);
                    MenuEntry("Moja postava",game.Profile.DisplayName+" · "+CurriculumLabels.Grade(game.Profile.Grade),()=>ShowProfile(false));
                    break;
                default:
                    MenuEntry("Nastavenia hry","Zvuk, kamera, čitateľnosť a pomoc spoločníka.",ShowSettings);
                    MenuEntry("Obraz a výkon","Kvalita, rozlíšenie a snímková frekvencia.",ShowGraphics);
                    MenuEntry("Virtuálna realita","Voliteľný PC OpenXR režim.",ShowVR);
                    MenuEntry("Hlavná ponuka","Návrat na úvodnú obrazovku akadémie.",ShowTitle);
#if !UNITY_WEBGL
                    if(!Application.isMobilePlatform) MenuEntry("Ukončiť hru","Tvoj postup sa pred odchodom uloží.",()=>{ProfileStore.Save(game.Save);Application.Quit();});
#endif
                    break;
            }
            NavigationLink(body,"Späť do sveta",CloseModal,true);
            ApplyViewportLayout(true);
        }


        public void ShowTitle()
        {
            Open("L U M E N","AKADÉMIA / MATEMATIKA 7.–9. ROČNÍKA");
            windowKind=WindowKind.Title;FeaturePanel="TITLE";titleOpen=true;hudRoot.gameObject.SetActive(false);
            profileRequired=!game.Save.HasCreatedProfile;
            modal.GetComponent<Image>().color=new Color(.025f,.055f,.075f,.16f);
            modalPanel.GetComponent<Image>().color=SoftInk;
            modalHeading.color=Chalk;modalHeading.fontSize=64;modalHeading.font=ChooseFont(modalHeading.text,headingFont);
            modalKicker.color=Brass;modalClose.gameObject.SetActive(false);
            Label(body,"Matematika v živom svete.",30,Chalk);
            Label(body,"Objavuj akadémiu, trénuj spoločníkov a vyhraj vedomosťami.",25,new Color(.73f,.77f,.74f));
            Rule(body,Brass);
            NavigationLink(body,game.Save.HasCreatedProfile?"Pokračovať vo výprave  →":"Vstúpiť do akadémie  →",()=>game.EnterAcademy(),true,true);
            NavigationLink(body,"Učebnice a testy",ShowStudy,false,true);
            NavigationLink(body,"Spoločníci a aréna",ShowArena,false,true);
            NavigationLink(body,"Hrať s priateľmi",ShowMultiplayer,false,true);
            NavigationLink(body,"Moja postava",()=>ShowProfile(!game.Save.HasCreatedProfile),false,true);
            NavigationLink(body,"Nastavenia",ShowSettings,false,true);
            Label(body,"Postup sa ukladá automaticky.",21,new Color(.67f,.72f,.70f));
            var player=ImageNode("Explorer identity",modalSafeRoot,SoftInk);titlePlayerCard=player.gameObject;
            Anchor(player.rectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-70,-70),new Vector2(340,172));
            var identity=Stack(player.rectTransform,22,8);
            Label(identity,game.Save.HasCreatedProfile?"TVOJA VÝPRAVA":"NOVÝ OBJAVITEĽ",20,Brass);
            Label(identity,game.Save.HasCreatedProfile?game.Profile.DisplayName:"Vitaj v akadémii",30,Chalk);
            Label(identity,CurriculumLabels.Grade(game.Profile.Grade)+" · úroveň "+game.Profile.Progress.Level,23,Chalk);
            ApplyViewportLayout(true);
        }

        public void ShowTutorial()
        {
            Open("Tvoje prvé objavy", "SPRIEVODCA / AKADÉMIA");
            Label(body, "Ovládanie: WASD alebo šípky = pohyb · medzerník = skok · Shift = beh · pravé tlačidlo myši = kamera · E = kiosk · M = mapa · Esc = menu.", 25, Ink);
            for (int i = 0; i < 6; i++)
            {
                var card = Card(body, i < game.Save.Tutorial.Step ? Mint : Cream);
                Label(card, (i < game.Save.Tutorial.Step ? "✓  " : (i + 1) + ".  ") + AcademyTutorial.Titles[i], 27, Purple);
                Label(card, AcademyTutorial.Instructions[i], 23, Muted);
            }
            Label(body, game.Save.Tutorial.RewardClaimed ? "Úvod už máš dokončený. Odmena je uložená v tvojom profile." : "Za dokončenie: +120 mincí, +20 XP a jedno vajíčko. Postup sa zachová aj po vypnutí hry.", 24, Purple);
            if (!game.Save.Tutorial.Complete) ButtonNode(body, "Pokračovať so sprievodcom  →", () => game.ResumeTutorial(), Lime);
            else
            {
                ButtonNode(body, "Späť do sveta", () => game.EnterAcademy(), Lime);
                ButtonNode(body, "Zopakovať sprievodcu", () => game.RestartTutorial(), Lavender);
            }
        }

        public void ShowProfile(bool initial)
        {
            Open(initial ? "Tvoja výprava sa začína." : "Tvoja postava", "OBJAVITEĽ / PROFIL");
            profileRequired = initial && !game.Save.HasCreatedProfile;
            Label(body, "Meno vidia aj ostatní hráči. Ročník vyberá matematické učivo; postavu a spoločníkov si ponecháš aj pri jeho zmene.", 24, Muted);
            var name = Input(body, "Meno objaviteľa", game.Profile.DisplayName, false, 20);
            int grade = CurriculumLabels.NormalizeGrade(game.Profile.Grade);
            Label(body, "Matematika · ročník základnej školy", 26, Ink);
            var gradeRow = Row(body, 76);
            var gradeButtons = new List<Button>();
            for (int option = 7; option <= 9; option++)
            {
                int chosen = option;
                var button = ButtonNode(gradeRow, CurriculumLabels.Grade(chosen), () =>
                {
                    grade = chosen;
                    for (int i = 0; i < gradeButtons.Count; i++) Tint(gradeButtons[i], i + 7 == grade ? Lavender : Color.white);
                }, chosen == grade ? Lavender : Color.white, Ink, 24);
                gradeButtons.Add(button);
            }
            Label(body, "Každý ročník má šesť matematických oblastí, učebnicu s príkladmi a vlastné tematické testy.", 22, Muted);
            int skin = game.Profile.Appearance.SkinTone, outfit = game.Profile.Appearance.OutfitColor;
            int hair = game.Profile.Appearance.HairStyle, accessory = game.Profile.Appearance.Accessory;
            Palette(body, "Odtieň pokožky", LumenMaterials.SkinColors, skin, value => skin = value);
            Palette(body, "Farba oblečenia", LumenMaterials.OutfitColors, outfit, value => outfit = value);
            Label(body, "Účes", 26, Ink);
            var hairButton = ButtonNode(body, HairLabel(hair), null, Lavender);
            hairButton.onClick.AddListener(() => { hair = (hair + 1) % 4; ButtonText(hairButton).text = HairLabel(hair); });
            Label(body, "Doplnok", 26, Ink);
            var accessoryButton = ButtonNode(body, AccessoryLabel(accessory), null, Lavender);
            accessoryButton.onClick.AddListener(() => { accessory = (accessory + 1) % 4; ButtonText(accessoryButton).text = AccessoryLabel(accessory); });
            ButtonNode(body, initial ? "Ideme objavovať  →" : "Uložiť postavu", () =>
            {
                game.ApplyProfile(name.text, AgeBand.Ages13To15, skin, outfit, hair, accessory, grade);
                profileRequired = false; CloseModal(); if (initial) game.EnterAcademy(true);
            }, Lime);
        }

        public void ShowQuestion(Question question)
        {
            Open("Výskumná výzva", "VEDOMOSTI / " + CurriculumLabels.SubjectName(game.CurrentSubject).ToUpperInvariant());
            FeaturePanel = "QUESTION";
            if (question == null)
            {
                Label(body, "V tejto sekcii už máš všetky nové otázky hotové.", 30, Ink);
                ButtonNode(body, "Vybrať ďalšiu sekciu", ShowMap, Lime); return;
            }
            Label(body, CurriculumLabels.Grade(question.Grade) + "  ·  náročnosť " + question.Difficulty + "/4" + (game.OnlineQuestion ? "  ·  overuje server" : ""), 22, Purple);
            Label(body, question.Prompt, 34, Ink);
            if (game.Save.Options.PetHelp) ButtonNode(body, "Pomoc od spoločníka", ShowPetHelper, Mint, Ink, 23);
            var answerButtons = new List<Button>();
            for (int i = 0; i < question.Options.Length; i++)
            {
                int answer = i;
                var button = ButtonNode(body, ((char)('A' + i)) + "   " + question.Options[i], () =>
                {
                    foreach (var b in answerButtons) b.interactable = false;
                    game.Answer(answer);
                }, i % 2 == 0 ? Lavender : Mint, Ink, 26);
                answerButtons.Add(button);
            }
            Label(body, "Premysli si postup. Po odpovedi uvidíš vysvetlenie a správne riešenie.", 22, Muted);
        }

        public void ShowAnswer(AnswerResult answer)
        {
            Open(answer.Accepted ? answer.Correct ? "Výborne. Objav je tvoj!" : "Každý pokus učí." : "Odpoveď sa neuložila", "VÝSLEDOK / VÝSKUM");
            if (!answer.Accepted)
            {
                Label(body, answer.Error, 28, Ink);
                ButtonNode(body, "Ďalšia výzva", () => game.BeginQuiz(game.CurrentSubject), Lime); return;
            }
            if (game.CurrentQuestion != null && answer.CorrectOption >= 0 && answer.CorrectOption < game.CurrentQuestion.Options.Length)
                Label(body, "Správna odpoveď: " + game.CurrentQuestion.Options[answer.CorrectOption], 30, Purple);
            Label(body, answer.Explanation, 28, Ink);
            Label(body, answer.Practice ? "Opakovanie · vedomosti si precvičuješ bez opätovného získania odmeny."
                : answer.Correct ? $"+{answer.XpAwarded} XP  ·  +{answer.CoinsAwarded} mincí  ·  +{answer.PetXpAwarded} XP spoločníka"
                : "Tentoraz bez XP. Pozri si vysvetlenie a skús ďalšiu otázku.", 24, Muted);
            if (answer.Streak > 1 && !answer.Practice) Label(body, "Séria správnych odpovedí: " + answer.Streak, 26, Purple);
            ButtonNode(body, "Ďalšia výzva  →", () => game.BeginQuiz(game.CurrentSubject), Lime);
            ButtonNode(body, "Preskúmať ďalšiu sekciu", ShowMap, Lavender);
            ButtonNode(body, "Vrátiť sa na ostrov", CloseModal, Cream);
        }

        public void ShowWaiting(string message)
        {
            Open("Spojenie s akadémiou", "MULTIPLAYER / VÝSKUM");
            Label(body, message, 30, Ink);
            liveNetwork = Label(body, NetworkSummary(), 24, Muted);
            ButtonNode(body, "Vrátiť sa na ostrov", CloseModal, Lavender);
        }

        public void ShowMap()
        {
            Open("Ostrov možností", "MAPA / VÝSKUMNÉ SEKCIE");
            Label(body, "Vyraz ku kioskom na ostrove alebo začni vybranú výzvu priamo tu. Obsah sa prispôsobí tvojmu veku.", 24, Muted);
            foreach (var section in game.Catalog.Sections)
            {
                Subject subject = section.Subject;
                var station = game.World.Stations.FirstOrDefault(s => s.Mode == "quiz" && LumenGame.ParseSubject(s.Subject) == subject);
                string location = station == null ? "" : $"  ·  ostrov [{station.transform.position.x:0}, {station.transform.position.z:0}]";
                var mastery = game.Learning.GetMastery(game.Profile, subject);
                MenuCard(section.Name, section.Description + "\nZvládnutie " + mastery.Score + "%" + location, () => game.BeginQuiz(subject), Lavender);
            }
            MenuCard("Nebeská dráha", game.Save.ParkourBest > 0 ? $"Najlepší čas {game.Save.ParkourBest:0.0} s · skoky a kontrolné body" : "Prekonaj plošiny po poradí · prvé dokončenie +100 mincí", () => game.StartCourse(), Lime);
            if (game.CourseActive) ButtonNode(body, "Vrátiť na kontrolný bod " + (game.CourseCheckpoint + 1), () => { game.CourseRespawn(); CloseModal(); }, Lavender);
        }

        public void ShowJournal()
        {
            Open("Tvoje poznanie rastie.", "DENNÍK / ZVLÁDNUTIE TÉM");
            var p = game.Profile.Progress;
            Label(body, game.Profile.DisplayName + " · úroveň " + p.Level + " · " + p.XP + " XP", 30, Ink);
            Label(body, "Najlepšia séria " + p.BestStreak + "  ·  nové vyriešené otázky " + p.Attempts.Count + "  ·  dokončené sekcie " + game.Learning.CompletedSections(game.Profile) + "/6", 24, Muted);
            foreach (Subject subject in Enum.GetValues(typeof(Subject)))
            {
                var m = game.Learning.GetMastery(game.Profile, subject);
                var card = Card(body, Cream);
                Label(card, CurriculumLabels.SubjectName(subject), 28, Ink);
                Label(card, $"Zvládnutie {m.Score}%  ·  správne {m.CorrectAnswers}/{m.TotalAnswers}  ·  ďalšia náročnosť {m.TargetDifficulty}/4", 24, Muted);
                ProgressBar(card, m.Score / 100f, Purple);
            }
            Label(body, "Odomknuté objavy", 30, Ink);
            foreach (string unlock in game.Learning.GetUnlocks(game.Profile)) Label(body, "✓  " + unlock, 24, Purple);
        }

        public void ShowPets()
        {
            Open("Malí parťáci. Veľké objavy.", "SPOLOČNÍCI / ZBIERKA A LIAHNUTIE");
            var collection = game.Save.Companions;
            Label(body, "Zbierka " + collection.Owned.Count + "/" + CompanionRules.Species.Length + "  ·  " + collection.Eggs + " vajíčok  ·  " + game.Profile.Progress.Coins + " mincí", 28, Purple);
            if (!collection.StarterClaimed) Label(body, "Vyber si prvého spoločníka zadarmo. Ďalších objavíš vo vajíčkach. Pri prvých liahnutiach vždy získaš druh, ktorý ešte nemáš.", 25, Ink);
            foreach (var species in CompanionRules.Species)
            {
                string speciesId = species.Id;
                var owned = collection.Owned.FirstOrDefault(p => p.SpeciesId == speciesId);
                bool equipped = collection.EquippedId == speciesId;
                var card = Card(body, equipped ? Mint : owned != null ? Lavender : Cream);
                PetPortrait(card, speciesId, owned == null);
                Label(card, (owned?.Nickname ?? species.Name) + (equipped ? "  ·  NA VÝPRAVE" : owned == null ? "  ·  NEOBJAVENÝ" : "  ·  V ZBIERKE"), 30, Ink);
                Label(card, species.Specialty + "\n" + species.Description, 24, Muted);
                if (owned == null)
                {
                    if (!collection.StarterClaimed) ButtonNode(card, "Vybrať " + species.Name + "  ·  zadarmo", () => game.ClaimStarter(speciesId), Lime);
                    continue;
                }
                Label(card, "Úroveň " + owned.Level + "  ·  " + (owned.XP % 100) + "/100 XP  ·  puto " + owned.Bond, 25, Purple);
                ProgressBar(card, (owned.XP % 100) / 100f, Purple);
                if (!equipped) ButtonNode(card, "Vziať na výpravu", () => game.EquipCompanion(speciesId), Lime);
                else
                {
                    var train = ButtonNode(card, "Trénovať  ·  75 mincí  →  +60 XP", () => game.TrainPet(), Lime);
                    train.interactable = game.Profile.Progress.Coins >= CompanionRules.TrainingCost;
                    var gearRow = Row(card, 78);
                    for (int i = 0; i < 3; i++)
                    {
                        int chosen = i; int needed = i == 1 ? 3 : i == 2 ? 5 : 1;
                        var gear = ButtonNode(gearRow, (owned.Gear == i ? "✓  " : "") + new[] { "Bez doplnku", "Šatka · ú. 3", "Korunka · ú. 5" }[i], () => game.SetCompanionGear(chosen), Cream, Ink, 22);
                        gear.interactable = owned.Level >= needed;
                    }
                    Label(card, "Puto a XP získava za nové správne odpovede, keď je na výprave. Šatka sa odomkne na 3. a korunka na 5. úrovni.", 22, Muted);
                }
            }
            if (collection.StarterClaimed)
            {
                var eggs = Card(body, Lavender);
                Label(eggs, "LIAHEŇ OBJAVOV", 27, Purple);
                Label(eggs, "Vajíčko za každé 3 nové správne odpovede · teraz " + collection.CorrectTowardsEgg + "/3. Vajíčko získaš aj za prvé dokončenie dráhy a za úvod. Po dokončení zbierky duplicitný druh dá +120 XP.", 24, Ink);
                var hatch = ButtonNode(eggs, "Vyliahnuť vajíčko  ·  máš " + collection.Eggs, () => game.HatchCompanion(), Lime); hatch.interactable = collection.Eggs > 0;
                var buy = ButtonNode(eggs, "Vajíčko za 200 herných mincí", () => game.BuyCompanionEgg(), Cream); buy.interactable = game.Profile.Progress.Coins >= CompanionRules.EggCost;
            }
            ButtonNode(body, "Späť na výpravu", CloseModal, Lime);
        }

        public void ShowHatchResult(CompanionProgress pet, bool duplicate)
        {
            Open(duplicate ? "Tvoje puto zosilnelo!" : "Nový objav v zbierke!", "LIAHEŇ / " + CompanionRules.Find(pet.SpeciesId).Name.ToUpperInvariant());
            Label(body, pet.Nickname, 52, Purple);
            Label(body, CompanionRules.Find(pet.SpeciesId).Description, 29, Ink);
            Label(body, duplicate ? "+120 XP pre spoločníka · aktuálna úroveň " + pet.Level : "Tento spoločník teraz patrí do tvojej zbierky. Vezmi ho preskúmať akadémiu.", 26, Muted);
            ButtonNode(body, "Vziať na výpravu", () => game.EquipCompanion(pet.SpeciesId), Lime);
            ButtonNode(body, "Otvoriť zbierku", ShowPets, Lavender);
        }

        public void ShowQuests()
        {
            Open("Každý objav sa počíta.", "VÝPRAVY / ODMENY");
            Label(body, "Denné výpravy sa obnovujú o polnoci UTC. Sekcie dokončuješ osobitne pre svoju vekovú kategóriu.", 24, Muted);
            foreach (var quest in game.Learning.GetQuests(game.Profile, DateTime.UtcNow))
            {
                var card = Card(body, quest.Claimed ? Mint : Cream);
                Label(card, quest.Name + "  " + quest.Current + "/" + quest.Target, 28, Ink);
                Label(card, quest.Description, 24, Muted);
                ProgressBar(card, (float)quest.Current / quest.Target, quest.Claimed ? new Color(.3f,.65f,.4f) : Purple);
                string id = quest.Id;
                var button = ButtonNode(card, quest.Claimed ? "Odmena prevzatá" : $"Prevziať +{quest.XP} XP · +{quest.Coins} mincí · +{quest.PetXP} XP peta", () => game.Claim(id), quest.Complete ? Lime : Lavender, Ink, 24);
                button.interactable = quest.Complete && !quest.Claimed;
            }
        }

        private void ShowLegacyLAN()
        {
            Open("Objavujte spolu.", "MULTIPLAYER / SPOLOČNÝ OSTROV");
            liveNetwork = Label(body, NetworkSummary(), 28, Purple);
            Label(body, "Stretnite sa na rovnakom ostrove, zbierajte spoločné majáky a riešte otázky overované hostiteľom. Mená a vzhľad postáv sa prenášajú ostatným.", 26, Ink);
            if (game.Session.State != SessionState.Offline)
            {
                var roster = Card(body, Mint);
                Label(roster, "Hráči na ostrove", 28, Ink);
                liveRoster = Label(roster, string.Join("\n", NetworkExplorer.All.Where(e => e != null).Select(e => e.DisplayName + (e.IsOwner ? "  ·  ty" : ""))), 24, Ink);
                var leave = ButtonNode(body, "Odpojiť a pokračovať sólo", () => { game.Disconnect(); ShowMultiplayer(); }, Lavender);
                leave.interactable = game.Session.State != SessionState.Disconnecting;
                ButtonNode(body, "Vrátiť sa na ostrov", CloseModal, Lime);
                return;
            }
            var address = Input(body, "IP adresa alebo názov hostiteľa", "127.0.0.1", false, 253);
            var port = Input(body, "UDP port", NetworkSession.DefaultPort.ToString(), true, 5);
            var error = Label(body, "V lokálnej sieti zadaj IP adresu počítača hostiteľa.", 22, Muted);
            Action<bool> connect = host =>
            {
                if (!ushort.TryParse(port.text, out ushort parsed) || parsed == 0)
                { error.text = "Zadaj platný port od 1 do 65535."; return; }
                bool started = host ? game.Session.StartHost(game.Profile.DisplayName, game.NetworkLook(), parsed)
                    : game.Session.Join(address.text, game.Profile.DisplayName, game.NetworkLook(), parsed);
                error.text = game.Session.Status;
                if (started) ShowMultiplayer();
            };
            ButtonNode(body, "Vytvoriť spoločný ostrov", () => connect(true), Lime);
            ButtonNode(body, "Pripojiť sa k hostiteľovi", () => connect(false), Lavender);
            Label(body, "Priame spojenie používa UDP, predvolene port 7777. Pre prístup cez internet potrebuje hostiteľ dostupnú adresu a povolený príslušný port; automatické vyhľadávanie ani Relay nie sú súčasťou tohto spojenia.", 22, Muted);
        }

        public void ShowGraphics()
        {
            Open("Svet vo väčšom detaile.", "GRAFIKA / NATÍVNY HDRP");
            Label(body, SystemInfo.graphicsDeviceName + "  ·  " + SystemInfo.graphicsDeviceType + "  ·  " + SystemInfo.graphicsMemorySize + " MB", 24, Purple);
            Label(body, game.Graphics.LastStatus, 26, Ink);
            bool ray = game.Graphics.RayTracingAvailable(out string reason);
            Label(body, reason, 22, Muted);
            var message = Label(body, "Rasterizácia ponúka plynulé hranie. Ray tracing pridáva DXR odrazy a globálne osvetlenie; path tracing je statický fotorežim.", 24, Muted);
            Action<RenderMode> select = mode =>
            {
                bool changed = game.Graphics.TrySetMode(mode, out string result);
                message.text = result; game.Notify(result);
                if (changed) CloseModal();
            };
            ButtonNode(body, "HDRP rasterizácia", () => select(RenderMode.Raster), Lavender);
            var rt = ButtonNode(body, "Ray tracing · odrazy a osvetlenie", () => select(RenderMode.RayTracing), Lavender); rt.interactable = ray;
            var pt = ButtonNode(body, "Path tracing · fotorežim · 256 vzoriek", () => select(RenderMode.PathTracing), Lavender); pt.interactable = ray;
            if (game.PhotoMode) ButtonNode(body, "Ukončiť fotorežim", () => { game.Graphics.ExitPhotoMode(out string result); game.Notify(result); CloseModal(); }, Lime);

            Label(body, "Výkon a kvalita", 30, Ink);
            int[] caps = { -1, 60, 120, 144, 165, 240 };
            int limit = game.Save.FrameLimit;
            var cap = ButtonNode(body, FrameLabel(limit), null, Cream);
            cap.onClick.AddListener(() =>
            {
                int index = Array.IndexOf(caps, limit); limit = caps[(index + 1) % caps.Length];
                ButtonText(cap).text = FrameLabel(limit);
            });
            var vsync = CheckBox(body, "VSync · synchronizovať s obnovovaním monitora", game.Save.VSync);
            int textureSize = game.Save.TextureSize;
            var textures = ButtonNode(body, TextureLabel(textureSize), null, Cream);
            textures.onClick.AddListener(() => { textureSize = textureSize >= 4096 ? 2048 : 4096; ButtonText(textures).text = TextureLabel(textureSize); });
            Label(body, "4K textúry sú plné zdrojové mapy. Rozlíšenie obrazu a limit FPS sú samostatné nastavenia. VRR (G-SYNC/FreeSync) závisí aj od monitora, systému a ovládača.", 22, Muted);
            ButtonNode(body, "Použiť a uložiť nastavenia", () =>
            {
                game.Save.FrameLimit = limit; game.Save.VSync = vsync.isOn; game.Save.TextureSize = textureSize;
                NativePerformance.Apply(game.Save); ProfileStore.Save(game.Save);
                game.Notify("Nastavenia uložené · " + FrameLabel(limit) + " · " + TextureLabel(textureSize));
                ShowGraphics();
            }, Lime);
            Label(body, $"Aktuálny obraz: {Screen.width} × {Screen.height}  ·  monitor {Screen.currentResolution.refreshRateRatio.value:0.#} Hz", 24, Ink);
            var displayModes = Screen.resolutions.Where(r => r.width >= 1280).GroupBy(r => new { r.width, r.height })
                .Select(g => g.OrderByDescending(r => r.refreshRateRatio.value).First()).OrderByDescending(r => r.width * r.height).Take(5).ToArray();
            foreach (var resolution in displayModes)
            {
                Resolution selected = resolution;
                ButtonNode(body, $"Obraz {selected.width} × {selected.height} · {selected.refreshRateRatio.value:0.#} Hz", () =>
                {
                    Screen.SetResolution(selected.width, selected.height, FullScreenMode.FullScreenWindow, selected.refreshRateRatio);
                    game.Notify("Požadované rozlíšenie " + selected.width + " × " + selected.height);
                }, Cream, Ink, 24);
            }
        }

        public void ShowVR()
        {
            Open("Vstúp priamo do akadémie.", "VIRTUÁLNA REALITA / PC OPENXR");
            liveVR = Label(body, game.VR != null ? game.VR.Status : "VR nie je dostupné v tomto spustení.", 26, Ink);
            Label(body, "Pripoj PC OpenXR headset a nastav jeho aktívny runtime. Režim sa zapne až po rozpoznaní bežiaceho displeja a sledovanej polohy headsetu.", 24, Muted);
            vrStart = ButtonNode(body, "Spustiť PC VR", () => game.VR?.StartXR(), Lime);
            vrStop = ButtonNode(body, "Ukončiť VR a hrať na monitore", () => { game.VR?.StopXR(); ShowVR(); }, Lavender);
            vrStart.interactable = game.VR != null && !game.VR.Active && !game.VR.Busy;
            vrStop.interactable = game.VR != null && game.VR.Active && !game.VR.Busy;
            Label(body, "OVLÁDANIE", 26, Purple);
            Label(body, "Ľavá páčka: pohyb podľa smeru pohľadu. Pravá páčka: otočenie po 30°. A: skok. B alebo tlačidlo menu: otvorenie ponuky. Pravý spúšťač: interakcia pri stanici alebo výber tlačidla lúčom v ponuke. Pravá páčka v ponuke: posúvanie obsahu.", 24, Ink);
            Label(body, "Meno a adresu hostiteľa môžeš zadať klávesnicou počítača; virtuálna klávesnica zatiaľ nie je súčasťou hry. Profil si môžeš pripraviť pred vstupom do VR.", 22, Muted);
            Label(body, "Pre Windows PC VR cez OpenXR vrátane kompatibilného headsetu pripojeného k počítaču. Vo VR je dostupná rasterizácia; DXR a fotorežim si môžeš vybrať po návrate na monitor.", 22, Muted);
        }

        private string NetworkSummary()
        {
            if (game.Online != null && (game.SharedConnected || game.Online.Busy)) return game.Online.Status + "\n" + (game.SharedConnected ? "Miestnosť " + game.Online.RoomCode + " · " + game.Online.Room.players.Length + "/16 hráčov · PC + web" : "Pripájanie…");
            if (game.Session == null) return "Sólo svet";
            return game.Session.Status + $"\n{game.Session.PlayerCount}/{NetworkSession.MaxPlayers} hráčov  ·  {game.Session.TickRate} Hz  ·  RTT {game.Session.PingMs} ms";
        }


        private void Open(string title, string eyebrow)
        {
            FeaturePanel="";windowKind=WindowKind.Page;titleOpen=false;hudRoot.gameObject.SetActive(false);
            if(modal!=null){modal.SetActive(false);Destroy(modal);}
            titlePlayerCard=null;liveNetwork=null;liveRoster=null;liveVR=null;vrStart=null;vrStop=null;profileRequired=false;
            modal=new GameObject("LUMEN · "+title,typeof(RectTransform),typeof(Image));modal.transform.SetParent(canvas.transform,false);
            modal.GetComponent<Image>().color=new Color(.035f,.065f,.08f,.72f);Stretch(modal.GetComponent<RectTransform>(),Vector2.zero,Vector2.one);
            modalSafeRoot=new GameObject("Window safe area",typeof(RectTransform)).GetComponent<RectTransform>();modalSafeRoot.SetParent(modal.transform,false);Stretch(modalSafeRoot,Vector2.zero,Vector2.one);
            var panel=ImageNode("Atlas page",modalSafeRoot,Cream);modalPanel=panel.rectTransform;
            var frame=panel.gameObject.AddComponent<Outline>();frame.effectColor=Brass;frame.effectDistance=new Vector2(1,-1);frame.useGraphicAlpha=true;
            var stack=Stack(modalPanel,32,16);
            modalKicker=Label(stack,eyebrow,20,Purple);
            var header=Row(stack,70);
            modalHeading=Label(header,title,38,Ink);modalHeading.font=ChooseFont(title,headingFont);
            modalClose=ButtonNode(header,"Späť",CloseModal,Lavender,Ink,22);SetWidth(modalClose.GetComponent<RectTransform>(),CompactViewport?112:100);
            Rule(stack,Brass);
            var scrollObject=new GameObject("Scrollable content",typeof(RectTransform),typeof(ScrollRect));scrollObject.transform.SetParent(stack,false);
            var flex=scrollObject.AddComponent<LayoutElement>();flex.flexibleHeight=1;flex.minHeight=60;
            var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D),typeof(Image));viewport.transform.SetParent(scrollObject.transform,false);
            viewport.GetComponent<Image>().color=Color.clear;
            var viewportRect=viewport.GetComponent<RectTransform>();Stretch(viewportRect,Vector2.zero,Vector2.one);
            var content=new GameObject("Content",typeof(RectTransform));content.transform.SetParent(viewport.transform,false);
            body=content.GetComponent<RectTransform>();body.anchorMin=new Vector2(0,1);body.anchorMax=Vector2.one;body.pivot=new Vector2(.5f,1);body.sizeDelta=Vector2.zero;
            Stack(body,2,14);content.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=scrollObject.GetComponent<ScrollRect>();scroll.viewport=viewportRect;scroll.content=body;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=44;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;nextRefresh=0;
            ApplyViewportLayout(true);
        }

        private void MenuCard(string title, string description, Action action, Color color)
        {
            var card = Card(body, color);
            var button = ButtonNode(card, title + "  →", action, color, Ink, 28);
            Label(card, description, 24, Muted);
        }

        private RectTransform FixedCard(string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var image = ImageNode(name, hudRoot, color); Anchor(image.rectTransform, anchor, pivot, position, size);
            return Stack(image.rectTransform, 16, 4);
        }

        private RectTransform Card(Transform parent, Color color)
        {
            var image = ImageNode("Card", parent, color); var result = Stack(image.rectTransform, 20, 12);
            return result;
        }

        private RectTransform Stack(RectTransform rect, int padding, float spacing)
        {
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>(); group.padding = new RectOffset(padding,padding,padding,padding);
            group.spacing = spacing; group.childControlWidth = true; group.childControlHeight = true; group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            return rect;
        }

        private RectTransform Row(Transform parent, float height)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup)); row.transform.SetParent(parent, false);
            var group = row.GetComponent<HorizontalLayoutGroup>(); group.spacing = 12; group.childControlWidth = true; group.childControlHeight = true;
            group.childForceExpandWidth = false; group.childForceExpandHeight = true; group.childAlignment = TextAnchor.MiddleLeft;
            var rect = row.GetComponent<RectTransform>(); SetHeight(rect, height); row.AddComponent<LumenAdaptiveRow>().BaseHeight=height; return rect;
        }

        private Text Label(Transform parent, string value, int size, Color color)
        {
            var obj = new GameObject("Text", typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>(); text.font = ChooseFont(value,font); text.fontSize = CompactViewport ? Mathf.Max(30,size) : size; text.color = color; text.text = value ?? "";
            text.supportRichText = false; text.raycastTarget = false; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.lineSpacing = 1.08f;
            var element = obj.AddComponent<LayoutElement>(); element.minHeight = text.fontSize * 1.3f; element.flexibleWidth = 1;
            return text;
        }

        private Button ButtonNode(Transform parent, string label, Action action, Color background, Color? foreground = null, int fontSize = 26)
        {
            var image = ImageNode("Button · " + label, parent, background);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.92f,.88f,.77f); colors.pressedColor = new Color(.74f,.72f,.64f);
            colors.selectedColor = new Color(.95f,.86f,.65f); colors.disabledColor = new Color(.64f,.64f,.60f,.65f); colors.fadeDuration = .09f; button.colors = colors;
            if (action != null) button.onClick.AddListener(() => { game.Audio?.Click(); action(); });
            var text = Label(image.transform, label, fontSize, foreground ?? Ink); text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one); text.rectTransform.offsetMin = new Vector2(18,10); text.rectTransform.offsetMax = new Vector2(-18,-10);
            var element = image.gameObject.AddComponent<LayoutElement>(); element.minHeight = CompactViewport ? 92 : 64; element.flexibleWidth = 1;
            var buttonLayout=image.gameObject.AddComponent<LumenButtonLayout>();buttonLayout.Caption=text;buttonLayout.MinimumHeight=CompactViewport?92:64;
            return button;
        }

        private InputField Input(Transform parent, string label, string value, bool number, int limit)
        {
            Label(parent, label, 24, Ink);
            var image = ImageNode("Input · " + label, parent, Color.white);
            var field = image.gameObject.AddComponent<InputField>(); field.targetGraphic = image;
            var text = Label(image.transform, "", 28, Ink); Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            text.rectTransform.offsetMin = new Vector2(18,8); text.rectTransform.offsetMax = new Vector2(-18,-8);
            field.textComponent = text; field.characterLimit = limit; field.lineType = InputField.LineType.SingleLine;
            field.contentType = number ? InputField.ContentType.IntegerNumber : InputField.ContentType.Standard;
            field.text = value; field.selectionColor = new Color(.18f,.39f,.40f,.3f);
            var element = image.gameObject.AddComponent<LayoutElement>(); element.minHeight = 74; element.preferredHeight = 78;
            return field;
        }

        private Toggle CheckBox(Transform parent, string label, bool initial)
        {
            var row = Row(parent, 72);
            var square = ImageNode("Toggle", row, Color.white); SetWidth(square.rectTransform, 60);
            var check = ImageNode("Check", square.transform, Purple); Stretch(check.rectTransform, new Vector2(.20f,.20f), new Vector2(.8f,.8f));
            var toggle = square.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = square; toggle.graphic = check; toggle.isOn = initial;
            Label(row, label, 24, Ink);
            return toggle;
        }

        private void Palette(Transform parent, string label, Color[] colors, int current, Action<int> change)
        {
            Label(parent, label, 26, Ink); var row = Row(parent, 74); var buttons = new List<Button>();
            for (int i = 0; i < colors.Length; i++)
            {
                int chosen = i;
                var button = ButtonNode(row, i == current ? "✓" : "", () =>
                {
                    for (int k = 0; k < buttons.Count; k++) ButtonText(buttons[k]).text = k == chosen ? "✓" : "";
                    change(chosen);
                }, colors[i], colors[i].grayscale > .55f ? Ink : Color.white, 30);
                buttons.Add(button);
            }
        }

        private void ProgressBar(Transform parent, float value, Color color)
        {
            var track = ImageNode("Progress", parent, Lavender); SetHeight(track.rectTransform, 14);
            var fill = ImageNode("Progress fill", track.transform, color); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = Mathf.Clamp01(value);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
        }

        private Image ImageNode(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>(); image.sprite = rounded; image.type = Image.Type.Sliced; image.color = color;
            return image;
        }

        private void CreateRoundedSprite()
        {
            const int size = 64; const float radius = 2;
            roundedTexture = new Texture2D(size,size,TextureFormat.RGBA32,false) { name = "LUMEN UI corners", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius));
                float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius));
                float distance = Mathf.Sqrt(Mathf.Max(0,dx) * Mathf.Max(0,dx) + Mathf.Max(0,dy) * Mathf.Max(0,dy));
                pixels[y*size+x] = new Color(1,1,1,Mathf.Clamp01(radius - distance + .5f));
            }
            roundedTexture.SetPixels(pixels); roundedTexture.Apply(false,true);
            rounded = Sprite.Create(roundedTexture, new Rect(0,0,size,size), new Vector2(.5f,.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius,radius,radius,radius));
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        { rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static void SetHeight(RectTransform rect, float height)
        { var layout = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); layout.minHeight = height; layout.preferredHeight = height; layout.flexibleHeight = 0; }
        private static void SetWidth(RectTransform rect, float width)
        { var layout = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); layout.minWidth = width; layout.preferredWidth = width; layout.flexibleWidth = 0; }
        private static void Put(Text text, string value) { if (text != null && text.text != value) { text.font=ChooseFont(value,text.font);text.text=value??""; } }
        private static Text ButtonText(Button button) => button.GetComponentInChildren<Text>();
        private static void Tint(Button button, Color color) => button.GetComponent<Image>().color = color;
        private static string HairLabel(int value) { return new[] { "Účes: krátky s ofinou", "Účes: rozstrapatený", "Účes: dlhý s drdolom", "Účes: bez vlasov" }[Mathf.Clamp(value,0,3)] + "  ·  zmeniť →"; }
        private static string AccessoryLabel(int value) { return new[] { "Doplnok: bez doplnku", "Doplnok: čiapka", "Doplnok: batoh", "Doplnok: akademický klobúk" }[Mathf.Clamp(value,0,3)] + "  ·  zmeniť →"; }
        private static string FrameLabel(int value) => value <= 0 ? "Limit FPS: odomknutý" : "Limit FPS: " + value;
        private static string TextureLabel(int value) => value >= 4096 ? "Textúry: 4K · plný detail" : "Textúry: 2K · úspora pamäte";
        private void OnDestroy() { if (rounded != null) Destroy(rounded); if (roundedTexture != null) Destroy(roundedTexture); }
    }

    /// <summary>Answers grow with wrapped text; lengthy advanced options never overlap adjacent rows.</summary>
    internal sealed class LumenButtonLayout : MonoBehaviour, ILayoutElement
    {
        public Text Caption;
        public float MinimumHeight=64;
        public void CalculateLayoutInputHorizontal() { }
        public float maxWidth => -1;
        public float maxHeight => -1;
        public void CalculateLayoutInputVertical() { }
        public float minWidth => -1;
        public float preferredWidth => -1;
        public float flexibleWidth => -1;
        public float minHeight => MinimumHeight;
        public float preferredHeight => Mathf.Max(MinimumHeight, Caption != null ? Caption.preferredHeight + 24 : MinimumHeight);
        public float flexibleHeight => -1;
        public int layoutPriority => 1;
    }
}
