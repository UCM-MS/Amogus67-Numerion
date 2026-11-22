using System;
using System.Linq;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        public string FeaturePanel { get; private set; } = "";

        public void ShowArena()
        {
            Open("Aréna spoločníkov", "NUMERION / MATEMATICKÉ DUELY"); FeaturePanel = "ARENA";
            Label(body, "Zostava a tréning", 30, Ink);
            Label(body, "Obaja hráči si zvolia ťah súčasne. Silné schopnosti aktivuje správne vyriešená úloha; slabý Rýchly výpad funguje bez otázky. Energia, zásoby schopností a výmena petov rozhodujú o taktike.", 25, Muted);
            var party = game.BattleParty();
            if (party.Length == 0)
            {
                var firstCompanion = Card(body, Mint);
                Label(firstCompanion, "Tvoja zostava sa práve začína.", 29, Ink);
                Label(firstCompanion, "Na prvý tréning stačí jeden spoločník. Vyber si ho zadarmo v zbierke a potom sa vráť do arény.", 25, Muted);
                ButtonNode(firstCompanion, "Vybrať prvého spoločníka zadarmo", ShowPets, Lime);
            }
            var row = Row(body, 78);
            if (party.Length > 0) ButtonNode(row, PetBattleRules.IsValidState(game.Save.PracticeBattle) && !game.Save.PracticeBattle.Complete ? "Pokračovať v tréningu" : "Tréning proti akadémii", game.StartPracticeBattle, Lime);
            ButtonNode(row, "Súboj s hráčom / miestnosť", ShowMultiplayer, Lavender);
            if (game.Online?.Battle != null) ButtonNode(body, "Otvoriť online súboj", game.ViewOnlineBattle, Mint);
            if (party.Length > 0) Label(body, "TVOJA ZOSTAVA · " + party.Length + " z 3 miest · na tréning stačí jeden", 25, Purple);
            RectTransform partyRow=null;int partyIndex=0;
            foreach (var pet in game.Save.Companions.Owned)
            {
                var species = CompanionRules.Find(pet.SpeciesId); if (species == null) continue;
                if(partyIndex++%2==0)partyRow=CardRow(body);
                var card = Card(partyRow, party.Contains(pet.SpeciesId) ? Selected : Cream);
                PetPortrait(card, species.Id,false,72);
                Label(card, (party.Contains(pet.SpeciesId) ? "●  " : "○  ") + pet.Nickname + "  ·  " + species.Name, 29, Ink);
                Label(card, species.Specialty + "\n" + PetBattleRules.AdvantageDescription(species.Id), 23, Muted);
                Label(card, ArtifactRules.Bonus(game.Save.Artifacts, species.Id).Description, 23, Purple);
                string id = species.Id;
                ButtonNode(card, party.Contains(id) ? "Odobrať zo zostavy" : "Zaradiť do zostavy", () => game.ToggleBattleParty(id), Color.white, Ink, 24);
            }
            Label(body, "UČIVO PRE TRÉNING", 25, Purple);
            RectTransform topicRow=null;int topicIndex=0;
            foreach (Subject topic in Enum.GetValues(typeof(Subject)))
            {
                Subject selected = topic;
                if(topicIndex++%2==0)topicRow=Row(body,64);
                ButtonNode(topicRow, (game.BattleTopic == topic ? "✓  " : "") + CurriculumLabels.SubjectName(topic), () => { game.BattleTopic = selected; ShowArena(); }, game.BattleTopic == topic ? Selected : Lavender, Ink, 24);
            }
            Label(body, "Tréning: artefakty pridajú najviac +20 HP a +3 k sile. Online aréna má rovnaké základné štatistiky pre oboch hráčov. Výhra po aspoň jednej správnej odpovedi: 60 XP, 100 mincí, 40 XP spoločníka; každú odmenu získaš raz.", 23, Muted);
            ButtonNode(body, "Výbava a artefakty", ShowArtifacts, Lavender);
        }

        public void ShowBattle()
        {
            var online = game.Online?.Battle;
            if (online != null && online.status == "challenge")
            {
                Open("Výzva do arény", "DUEL / POZVÁNKA"); FeaturePanel = "BATTLE";
                Label(body, online.opponentId == game.Online.IdentityId ? "Spoluhráč ťa vyzval na matematický súboj." : "Výzva bola odoslaná. Čakáme na prijatie spoluhráčom.", 29, Ink);
                if (online.opponentId == game.Online.IdentityId) ButtonNode(body, "Prijať súboj", () => game.Online.AcceptBattle(online.id), Lime);
                ButtonNode(body, "Odmietnuť / zrušiť výzvu", () => game.Online.DeclineBattle(online.id), Lavender); return;
            }
            var state = game.BattleState;
            if (!PetBattleRules.IsValidState(state)) { ShowArena(); return; }
            int side = game.BattleSide, opponent = 1 - side;
            Open("Kolo " + state.Round + " · " + (game.IsOnlineBattle ? "Duel hráčov" : "Tréningový duel"), "ARÉNA / " + CurriculumLabels.Grade(game.Profile.Grade).ToUpperInvariant()); FeaturePanel = "BATTLE";
            bool waitingForOpponent=game.IsOnlineBattle&&(game.Online.Busy||(online.submitted!=null&&online.submitted.Length>side&&online.submitted[side]));
            string phase=state.Complete?"SÚBOJ UKONČENÝ":waitingForOpponent?"ŤAH ODOSLANÝ":game.BattleQuestionOpen?"VYRIEŠ ÚLOHU":"VYBER ŤAH";
            BuildBattleFrame(body,state,side,phase);
            if (state.Complete)
            {
                CompactLabel(body,state.Winner==side?"Víťazstvo!":state.Winner<0?"Remíza":"Vyhral súper",22,Ink);
                CompactLabel(body,"Správne riešenia: "+state.CorrectAnswers[side]+" · kolá: "+state.RoundsResolved,16,Muted);
                var actions=CSSRow(body);
                CompactButton(actions,"Späť do arény",ShowArena,Lime);
                if(!game.IsOnlineBattle)CompactButton(actions,"Nový tréning",game.StartPracticeBattle,Lavender);
            }
            else if (waitingForOpponent)
            {
                CompactLabel(body,"Tvoj ťah je potvrdený. Čakáme na súpera…",18,Muted);
            }
            else if (game.BattleQuestionOpen && game.BattleQuestion != null)
            {
                BuildBattleQuestionContent(body,game.BattleQuestion,PetBattleRules.MoveLabel(game.PendingBattleMove,state.Teams[side].Fighter.SpeciesId),SubmitBattleAnswerFromUI);
            }
            else
            {
                BuildBattleActions(body,state,side,(kind,slot)=>game.SelectBattleMove(kind,slot));
            }
            var footer=CSSRow(body);
            if(game.BattleQuestionOpen&&game.Save.Options.PetHelp)CompactButton(footer,"Pomoc",ShowPetHelper,Mint);
            CompactButton(footer,string.IsNullOrEmpty(game.BattleExplanation)?"Ťahy a priebeh":"Vysvetlenie a priebeh",ShowBattleDetails,string.IsNullOrEmpty(game.BattleExplanation)?Lavender:Selected);
        }

        private void BuildBattleFrame(Transform parent,PetBattleState state,int side,string phase)
        {
            CompactLabel(parent,"KOLO "+state.Round+" · "+phase,17,Ink);
            var teams=CSSRow(parent,0,true);teams.name="Battle faceoff";
            BattleTeamCard(teams,state.Teams[side],"TY",Lavender);
            var versus=CompactLabel(teams,"VS",13,Muted);versus.alignment=TextAnchor.MiddleCenter;SetWidth(versus.rectTransform,22);
            BattleTeamCard(teams,state.Teams[1-side],"SÚPER",Mint);
        }

        private void BuildBattleQuestionContent(Transform parent,Question question,string move,Action<int> answer)
        {
            CompactLabel(parent,move+" · správna odpoveď aktivuje ťah",14,Muted);
            BuildQuestionBlock(parent,question,answer);
        }

        private void SubmitBattleAnswerFromUI(int option)
        {
            game.SubmitBattleAnswer(option);
            // A busy online request can reject submission before accepting the answer; allow the same retry.
            if(game.BattleQuestionOpen&&body)foreach(var button in body.GetComponentsInChildren<UnityEngine.UI.Button>())
                if(button.name.StartsWith("Answer ",StringComparison.Ordinal))button.interactable=true;
        }

        private void BuildBattleActions(Transform parent,PetBattleState state,int side,Action<PetBattleActionKind,int> choose)
        {
            RectTransform row=null;int index=0;
            foreach(var kind in new[]{PetBattleActionKind.Strike,PetBattleActionKind.Guard,PetBattleActionKind.Focus,PetBattleActionKind.Heal,PetBattleActionKind.QuickStrike})
            {
                var action=kind;var move=PetBattleRules.GetMove(kind,state.Teams[side].Fighter.SpeciesId);
                string error=PetBattleRules.AvailabilityError(state,side,kind);
                string cost=kind==PetBattleActionKind.QuickStrike?"bez úlohy":move.EnergyCost+" E";
                if(!string.IsNullOrEmpty(error))cost=error.Contains("energie")?"chýba energia":"nedostupné";
                if(index++%2==0)row=CSSRow(parent);
                var button=CompactButton(row,move.Name+" · "+cost,()=>choose(action,0),kind==PetBattleActionKind.Strike?Lime:Lavender);
                button.interactable=string.IsNullOrEmpty(error);
            }
            var pets=state.Teams[side].Pets;row=null;index=0;
            for(int i=0;i<pets.Length;i++)if(i!=state.Teams[side].Active)
            {
                int slot=i;var pet=pets[i];if(index++%2==0)row=CSSRow(parent);
                var button=CompactButton(row,"Vymeniť: "+pet.Nickname+" · "+pet.HP+" HP",()=>choose(PetBattleActionKind.Switch,slot),Cream,16);
                button.interactable=PetBattleRules.CanUse(state,side,PetBattleActionKind.Switch,slot);
            }
        }

        private void BattleTeamCard(Transform parent,PetBattleTeam team,string caption,Color color)
        {
            var card=CompactCard(parent,color);card.name="Fighter "+caption;
            var p=team.Fighter;if(p==null)return;
            var identity=CSSRow(card,0,true);
            if(UiViewportSize.x>=520){var portrait=PetPortrait(identity,p.SpeciesId,false,64);SetWidth(portrait,64);}
            var names=TextColumn(identity);
            CompactLabel(names,caption,12,Muted);CompactLabel(names,p.Nickname,18,Ink);
            CompactLabel(card,p.HP+"/"+p.MaxHP+" HP · energia "+p.Energy+"/6",16,Ink);
            var hp=ImageNode("HP",card,Lavender);SetHeight(hp.rectTransform,6);
            var fill=ImageNode("HP fill",hp.transform,p.HP*3<p.MaxHP?new Color(.88f,.39f,.33f):new Color(.55f,.73f,.58f));
            fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fill.fillAmount=p.HP/(float)Math.Max(1,p.MaxHP);Stretch(fill.rectTransform,Vector2.zero,Vector2.one);
            var energy=CSSRow(card,4,true);
            for(int i=0;i<6;i++){var pip=ImageNode("Energy "+(i+1),energy,i<p.Energy?Brass:Lavender);var sizing=pip.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();sizing.flexibleWidth=1;sizing.minHeight=4;}
            if(p.Shield>0||p.Focused)CompactLabel(card,(p.Shield>0?"Štít "+p.Shield:"")+(p.Focused?" · sústredený":""),13,Muted);
        }

        private void ShowBattleDetails()
        {
            var state=game.BattleState;if(!PetBattleRules.IsValidState(state)){ShowArena();return;}
            int side=game.BattleSide;Open("Ťahy a priebeh","ARÉNA / PODROBNOSTI");FeaturePanel="BATTLE-DETAILS";
            CompactButton(body,"Späť do súboja",ShowBattle,Lime);
            if(!string.IsNullOrEmpty(game.BattleExplanation))CompactLabel(body,game.BattleExplanation,18,Ink);
            var pet=state.Teams[side].Fighter;
            CompactLabel(body,"Použitia: útok "+pet.StrikePP+"/12 · sústredenie "+pet.FocusPP+"/4 · liečenie "+pet.HealPP+"/2",16,Muted);
            foreach(var kind in new[]{PetBattleActionKind.Strike,PetBattleActionKind.Guard,PetBattleActionKind.Focus,PetBattleActionKind.Heal,PetBattleActionKind.QuickStrike})
            {
                var move=PetBattleRules.GetMove(kind,pet.SpeciesId);var card=CompactCard(body,Cream);
                CompactLabel(card,move.Name+" · "+move.EnergyCost+" energie",18,Ink);CompactLabel(card,move.Description,16,Muted);
                string error=PetBattleRules.AvailabilityError(state,side,kind);if(!string.IsNullOrEmpty(error))CompactLabel(card,error,15,Muted);
            }
            if(state.Log?.Length>0)foreach(var line in state.Log.Skip(Math.Max(0,state.Log.Length-8)))CompactLabel(body,line,16,Muted);
            if(!state.Complete)CompactButton(body,"Vzdať súboj…",()=>{Open("Vzdať súboj?","ARÉNA");FeaturePanel="BATTLE-FORFEIT";CompactLabel(body,"Súper získa výhru.",18,Ink);CompactButton(body,"Vzdať",game.EndBattle,Lavender);CompactButton(body,"Pokračovať",ShowBattle,Lime);},Cream);
        }
    }
}
