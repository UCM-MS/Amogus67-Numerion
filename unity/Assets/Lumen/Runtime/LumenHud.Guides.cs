using System;
using System.Linq;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        public void ShowNpc(string id)
        {
            if (!game.Save.HasCreatedProfile) { ShowProfile(true); return; }
            string name = id == "npc-tobi" ? "Tobi" : id == "npc-ema" ? "Ema" : "Mira";
            string role = id == "npc-tobi" ? "MATEMATICKÝ MENTOR" : id == "npc-ema" ? "STRATÉGIA PET DUELU" : "SPRIEVODKYŇA AKADÉMIOU";
            Open(name + " · poďme sa porozprávať", "AKADÉMIA / " + role); FeaturePanel = "NPC";
            var pet = game.Save.Companions.Equipped;
            Label(body, "Ahoj, " + game.Profile.DisplayName + "." + (pet == null ? "" : " Vidím, že " + pet.Nickname + " ťa sprevádza!"), 31, Ink);
            if (id == "npc-tobi")
            {
                Label(body, "Nemusíš hádať. Najprv si ujasni, čo poznáš, čo hľadáš a ktorý postup sa hodí. Čo si dnes precvičíme?", 27, Muted);
                ButtonNode(body, "Vybrať učebnicu pre " + CurriculumLabels.Grade(game.Profile.Grade), () => ShowGuideTopics(id), Lime);
                ButtonNode(body, "Ako rozumne vyriešiť slovnú úlohu?", () => ShowGuideBranch(id, "reading"), Lavender);
                ButtonNode(body, "Čo keď odpoviem nesprávne?", () => ShowGuideBranch(id, "mistakes"), Cream);
                ButtonNode(body, "Učebnice a tematické testy", ShowStudy, Mint);
            }
            else if (id == "npc-ema")
            {
                Label(body, "V aréne nerozhoduje iba sila. Sleduj energiu, zásoby schopností a ďalšieho peta v zostave. Ukážem ti, ako nad tým premýšľať.", 27, Muted);
                ButtonNode(body, "Vysvetli mi prvý súboj", () => ShowGuideBranch(id, "battle"), Lime);
                ButtonNode(body, "Na čo sú typy a matematická špecializácia?", () => ShowGuideBranch(id, "affinity"), Lavender);
                ButtonNode(body, "Ako pomáhajú artefakty?", () => ShowGuideBranch(id, "artifacts"), Cream);
                ButtonNode(body, "Zostava a aréna spoločníkov", ShowArena, Mint);
            }
            else
            {
                Label(body, "Vitaj v matematickej akadémii. Môžeš objavovať vlastným tempom. Začneme ovládaním, prvým spoločníkom alebo prehľadom miest?", 27, Muted);
                ButtonNode(body, game.Save.Tutorial.Complete ? "Pripomenúť sprievodcu" : "Ukáž mi prvé kroky", ShowTutorial, Lime);
                if (!game.Save.Tutorial.Complete) ButtonNode(body, "Pokračovať so sprievodcom vo svete", () => game.ResumeTutorial(), Mint);
                ButtonNode(body, "Ako sa pohybujem a rozprávam s postavami?", () => ShowGuideBranch(id, "controls"), Lavender);
                ButtonNode(body, "Kde nájdem matematiku, petov a spoluhráčov?", () => ShowGuideBranch(id, "places"), Cream);
                ButtonNode(body, pet == null ? "Vybrať prvého spoločníka zadarmo" : "Pozrieť mojich spoločníkov", ShowPets, Mint);
            }
            ButtonNode(body, "Späť do akadémie", CloseModal, Cream, Muted, 23);
        }

        private void ShowGuideTopics(string npcId)
        {
            Open("Tobi · vyberme si učivo", "UČENIE / " + CurriculumLabels.Grade(game.Profile.Grade).ToUpperInvariant()); FeaturePanel = "NPC";
            Label(body, "Každá kapitola vysvetlí princíp, ukáže samostatný vzorový príklad a ponúkne tematický test. Tvoj ročník zostáva " + game.Profile.Grade + ".", 26, Muted);
            foreach (var section in game.Catalog.Sections)
            {
                var topic = section.Subject;
                var lesson = LessonCatalog.GetLessons(game.Profile.Grade, topic).FirstOrDefault();
                var card = Card(body, Cream);
                Label(card, section.Name, 29, Purple);
                Label(card, lesson?.Takeaway ?? section.Description, 25, Ink);
                if (lesson != null) ButtonNode(card, "Vysvetli mi postup", () => ShowLesson(lesson), Lavender, Ink, 24);
                ButtonNode(card, "Vyskúšať tematický test", () => game.BeginStudyTest(topic), Mint, Ink, 24);
            }
            ButtonNode(body, "Späť k rozhovoru", () => ShowNpc(npcId), Cream);
        }

        private void ShowGuideBranch(string id, string branch)
        {
            string name = id == "npc-tobi" ? "Tobi" : id == "npc-ema" ? "Ema" : "Mira";
            Open(name + " · mám pre teba tip", "AKADÉMIA / ROZHOVOR"); FeaturePanel = "NPC";
            if (branch == "controls")
            {
                GuideNote("Rozhliadni sa a urob prvý krok", "WASD alebo šípky ťa pohybujú, Shift zrýchli chôdzu a medzerník skočí. Kameru otáčaš pravým tlačidlom myši; kolieskom meníš vzdialenosť.");
                GuideNote("Všímaj si blízke postavy a stanice", "Priblíž sa, kým sa objaví výzva, a stlač E. Mapu otvoríš klávesom M, denník klávesom J a menu cez Esc. Na gamepade sa pohybuješ ľavou páčkou a interaguješ tlačidlom X.");
                ButtonNode(body, "Otvoriť sprievodcu", ShowTutorial, Lime);
            }
            else if (branch == "places")
            {
                GuideNote("Šesť oblastí, jedna matematika", "Pavilóny patria číslam a zlomkom, percentám a pomeru, výrazom a rovniciam, geometrii, dátam a praktickým úlohám. V učebnici nájdeš postup pre svoj ročník.");
                GuideNote("Objavuj s ostatnými", "V Hrať spolu vytvoríš miestnosť alebo zadáš kód kamaráta. Spolu môžete chodiť a rozsvecovať majáky. Aréna pridáva pet duely; knižnica sa hodí na pokojné učenie.");
                ButtonNode(body, "Mapa akadémie", ShowMap, Lavender);
                ButtonNode(body, "Otvoriť knižnicu", ShowStudy, Lime);
                ButtonNode(body, "Hrať spolu", ShowMultiplayer, Mint);
            }
            else if (branch == "reading")
            {
                GuideNote("1 · Povedz si, čo hľadáš", "Prečítaj zadanie ešte raz. Pomenuj neznámu veličinu a zapíš známe údaje aj s jednotkami. Dôležitý údaj si oddeľ od príbehu.");
                GuideNote("2 · Vyber postup", "Rozhodni, či počítaš časť celku, zmenu, pomer, neznáme číslo, rozmer alebo pravdepodobnosť. Pomôže jednoduchý náčrt alebo krátky zápis.");
                GuideNote("3 · Skontroluj výsledok", "Najprv odhadni približnú veľkosť. Po výpočte over jednotku, znamienko a to, či výsledok odpovedá na pôvodnú otázku. Až potom vyber odpoveď.");
                ButtonNode(body, "Vybrať tému s Tobim", () => ShowGuideTopics(id), Lime);
            }
            else if (branch == "mistakes")
            {
                GuideNote("Chyba ukazuje ďalší krok", "Po odpovedi dostaneš vysvetlenie. Porovnaj svoj postup s riešením a nájdi prvé miesto, kde sa líšia. Zameraj sa na dôvod, nielen na správne písmeno.");
                GuideNote("Precvičuj rozumne", "Vráť sa ku kapitole a vyrieš nový príklad. Opakovaná otázka slúži na tréning; prvá odmena sa za tú istú úlohu nepripisuje znovu. V tematickom teste si potom pozrieš celý rozbor.");
                ButtonNode(body, "Otvoriť učebnice a testy", ShowStudy, Lime);
            }
            else if (branch == "battle")
            {
                GuideNote("Začni bezpečným tréningom", "V aréne vyber najviac troch spoločníkov. Každé kolo zvolíš ťah a súper svoj ťah potvrdí tiež. Výmena, štít, liečenie a sústredenie majú odlišnú prioritu.");
                GuideNote("Vedieť počítať sa oplatí", "Správne riešenie aktivuje silnú schopnosť. Chyba spotrebuje ťah a doplní jednu energiu. Rýchly výpad spôsobí základných " + PetBattleRules.QuickStrikePower + " poškodenia bez otázky, ale neprinesie matematický artefakt ani účasť na vedomostnej odmene.");
                GuideNote("Plánuj ďalšie kolo", "Sleduj energiu a zostávajúce použitia. Štít zachytí " + PetBattleRules.GuardShield + " poškodenia v danom kole; obnova má obmedzené použitia a prestávku. Včas vymeň peta, ktorý už nemôže bezpečne pokračovať.");
                ButtonNode(body, "Otvoriť zostavu a tréning", ShowArena, Lime);
            }
            else if (branch == "affinity")
            {
                GuideNote("Typ rozhoduje o výhodnom súperovi", "Každý pet patrí do jednej z troch bojových skupín. Výhodný typ zosilní útok; nevýhodný ho oslabí. Pri karte spoločníka v aréne nájdeš jeho konkrétne výhody.");
                GuideNote("Téma je ďalšia príležitosť", "Správne vyriešená úloha zo špecializácie aktívneho peta zosilní jeho silný útok o 20 %. Tím s rôznymi špecializáciami ti dá viac možností. Slabý výpad tento bonus nepoužíva.");
                ButtonNode(body, "Porovnať moju zostavu", ShowArena, Lime);
            }
            else
            {
                GuideNote("Výbava za pochopenie", "Za nové správne matematické riešenia získavaš artefakty. Majú slot, vzácnosť, hlavnú vlastnosť, vedľajšie vlastnosti a tematický set. Vybav ich petovi, vylepši za prach alebo rozober nepotrebný kus.");
                GuideNote("Férový online duel", "Výbava v tréningu pridá najviac +20 HP a +3 sila. Online aréna začína pre oboch hráčov rovnakými základnými štatistikami. Rozhodujú ťahy a riešenie úloh.");
                ButtonNode(body, "Prezrieť artefakty", ShowArtifacts, Lime);
            }
            ButtonNode(body, "Mám ďalšiu otázku", () => ShowNpc(id), Lavender);
            ButtonNode(body, "Späť do sveta", CloseModal, Cream, Muted, 23);
        }

        private void GuideNote(string title, string text)
        {
            var card = Card(body, Cream); Label(card, title, 28, Purple); Label(card, text, 26, Ink);
        }

        public void ShowPetHelper()
        {
            string previous = FeaturePanel;
            Question question = null;
            Action back = CloseModal;
            if (previous == "BATTLE") { if (game.BattleQuestionOpen) question = game.BattleQuestion; back = ShowBattle; }
            else if (previous == "study-test") { question = game.StudyTests.CurrentQuestion(game.Save.ActiveStudyTest); back = ShowStudyTest; }
            else if (previous == "QUESTION") { question = game.CurrentQuestion; back = () => ShowQuestion(game.CurrentQuestion); }
            else if (previous == "study" || previous == "lesson") back = ShowStudy;
            var companion = game.Save.Companions.Equipped;
            Open(companion == null ? "Tvoj sprievodca učením" : companion.Nickname + " · tvoj pomocník", "SPOLOČNÍK / POMÔCKA"); FeaturePanel = "PET-HELPER";
            if (companion != null)
            {
                var hello = Row(body, 158); PetPortrait(hello, companion.SpeciesId);
                Label(hello, companion.Nickname + " ťa sprevádza v akadémii. Spolu si pripomenieme postup; odpoveď vyberieš sám.", 27, Ink);
            }
            else Label(body, "Aj ťažšia úloha sa dá rozdeliť na malé kroky. Prvého vlastného spoločníka si vyberieš zadarmo v zbierke.", 27, Ink);
            if (question != null)
            {
                Label(body, CurriculumLabels.Grade(question.Grade) + " · " + CurriculumLabels.SubjectName(question.Subject), 25, Purple);
                GuideNote("Ako na túto tému", HelperPrinciple(question.Subject));
                var lesson = LessonCatalog.GetLessons(question.Grade, question.Subject).FirstOrDefault();
                if (lesson != null) GuideNote("Pamätaj na princíp", lesson.Takeaway);
                GuideNote("Pred potvrdením", "Povedz si vlastnými slovami, čo hľadáš. Zapíš údaje a jednotky, zvoľ postup a urob odhad. Potom skontroluj výsledok. Táto pomôcka nemení tvoju odpoveď, skóre ani odmenu.");
                if (previous == "BATTLE" && game.IsOnlineBattle) Label(body, "Online kolo pokračuje podľa času servera aj počas čítania pomôcky.", 23, Purple);
            }
            else
            {
                var tutorial = game.Save.Tutorial;
                if (!tutorial.Complete)
                {
                    int step = Math.Clamp(tutorial.Step, 0, AcademyTutorial.Titles.Length - 1);
                    GuideNote("Tvoj ďalší krok · " + AcademyTutorial.Titles[step], AcademyTutorial.Instructions[step]);
                    ButtonNode(body, "Pokračovať so sprievodcom", () => game.ResumeTutorial(), Lime);
                }
                else GuideNote("Kam sa dnes vydáme?", "Vyber si kapitolu svojho ročníka, vyrieš tematický test alebo skús taktiku v tréningovej aréne. Nové správne riešenia pomáhajú rozvíjať tvoju zbierku.");
                ButtonNode(body, "Učebnice a testy", ShowStudy, Lavender);
                ButtonNode(body, "Zostava a aréna", ShowArena, Mint);
                if (companion == null) ButtonNode(body, "Vybrať spoločníka", ShowPets, Lime);
            }
            ButtonNode(body, question == null ? "Vrátiť sa" : "Späť k mojej úlohe", back, Lime);
        }

        private static string HelperPrinciple(Subject topic)
        {
            switch (topic)
            {
                case Subject.Mathematics: return "Skontroluj znamienka a poradie operácií. Pri sčítaní zlomkov zjednoť menovatele; pri násobení a delení použi ich vlastné pravidlá. Pred finálnym výberom výsledok skráť alebo odhadni.";
                case Subject.Science: return "Najprv pomenuj základ alebo celý počet dielov. Percento je stotina základu. V pomere spočítaj diely; pri mierke zjednoť jednotky. Rozlíš priamu a nepriamu úmernosť.";
                case Subject.Nature: return "Dodrž poradie operácií. Pri rovnici rob rovnakú povolenú úpravu na oboch stranách a neznámu nakoniec over dosadením. So záporným násobkom pri nerovnici sa mení smer znamienka.";
                case Subject.History: return "Nakresli si náčrt a označ známe rozmery. Rozlíš obvod, obsah, povrch a objem. Zjednoť jednotky; štvorcové a kubické jednotky prevádzaj podľa ich rozmeru.";
                case Subject.Languages: return "Najprv si ujasni, čo predstavuje jeden údaj alebo jeden možný výsledok. Pri priemere delíš súčet počtom údajov. Pri pravdepodobnosti porovnaj priaznivé a všetky rovnako možné prípady.";
                default: return "Oddeľ potrebné údaje od príbehu a napíš ich s jednotkami. Zisti, či sa pýta na časť, rozdiel, cenu alebo zmenu. Po výpočte skontroluj, či veľkosť výsledku dáva zmysel v skutočnej situácii.";
            }
        }
    }
}
