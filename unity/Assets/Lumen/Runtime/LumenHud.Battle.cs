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
            Open("Aréna spoločníkov", "LUMEN / MATEMATICKÉ DUELY"); FeaturePanel = "ARENA";
            Label(body, "Zostav trojicu. Vyber učivo. Premysli ďalší ťah.", 32, Ink);
            Label(body, "Obaja hráči si zvolia ťah súčasne. Silné schopnosti aktivuje správne vyriešená úloha; slabý Rýchly výpad funguje bez otázky. Energia, zásoby schopností a výmena petov rozhodujú o taktike.", 25, Muted);
            var row = Row(body, 78);
            ButtonNode(row, PetBattleRules.IsValidState(game.Save.PracticeBattle) && !game.Save.PracticeBattle.Complete ? "Pokračovať v tréningu" : "Tréning proti akadémii", game.StartPracticeBattle, Lime);
            ButtonNode(row, "Súboj s hráčom / miestnosť", ShowMultiplayer, Lavender);
            if (game.Online?.Battle != null) ButtonNode(body, "Otvoriť online súboj", game.ViewOnlineBattle, Mint);
            Label(body, "TVOJA ZOSTAVA · najviac 3 spoločníci", 25, Purple);
            var party = game.BattleParty();
            if (game.Save.Companions.Owned.Count == 0) ButtonNode(body, "Vybrať prvého spoločníka zadarmo", ShowPets, Lime);
            foreach (var pet in game.Save.Companions.Owned)
            {
                var species = CompanionRules.Find(pet.SpeciesId); if (species == null) continue;
                var card = Card(body, party.Contains(pet.SpeciesId) ? Lavender : Cream);
                PetPortrait(card, species.Id);
                Label(card, (party.Contains(pet.SpeciesId) ? "●  " : "○  ") + pet.Nickname + "  ·  " + species.Name, 29, Ink);
                Label(card, species.Specialty + "\n" + PetBattleRules.AdvantageDescription(species.Id), 23, Muted);
                Label(card, ArtifactRules.Bonus(game.Save.Artifacts, species.Id).Description, 23, Purple);
                string id = species.Id;
                ButtonNode(card, party.Contains(id) ? "Odobrať zo zostavy" : "Zaradiť do zostavy", () => game.ToggleBattleParty(id), Color.white, Ink, 24);
            }
            Label(body, "UČIVO PRE TRÉNING", 25, Purple);
            foreach (Subject topic in Enum.GetValues(typeof(Subject)))
            {
                Subject selected = topic;
                ButtonNode(body, (game.BattleTopic == topic ? "✓  " : "") + CurriculumLabels.SubjectName(topic), () => { game.BattleTopic = selected; ShowArena(); }, game.BattleTopic == topic ? Mint : Cream, Ink, 24);
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
            var teams = Row(body, 410);
            BattleTeamCard(teams, state.Teams[side], "TVOJ SPOLOČNÍK", Lavender);
            BattleTeamCard(teams, state.Teams[opponent], "SÚPER", Mint);
            if (!string.IsNullOrEmpty(game.BattleExplanation)) Label(body, game.BattleExplanation, 25, Purple);
            if (state.Complete)
            {
                Label(body, state.Winner == side ? "Víťazstvo!" : state.Winner < 0 ? "Remíza" : "Tentoraz vyhral súper. Skús inú taktiku.", 36, Ink);
                Label(body, "Správne riešenia: " + state.CorrectAnswers[side] + " · odohrané kolá: " + state.RoundsResolved, 25, Muted);
                ButtonNode(body, "Späť do arény", ShowArena, Lime);
                if (!game.IsOnlineBattle) ButtonNode(body, "Nový tréning", game.StartPracticeBattle, Lavender);
            }
            else if (game.IsOnlineBattle && (game.Online.Busy || (online.submitted != null && online.submitted.Length > side && online.submitted[side])))
            {
                Label(body, "Ťah je odoslaný. Čakáme na súpera…", 29, Purple);
                Label(body, "Výsledok kola vyhodnotí spoločný server po oboch ťahoch.", 24, Muted);
            }
            else if (game.BattleQuestionOpen && game.BattleQuestion != null)
            {
                var question = game.BattleQuestion;
                var card = Card(body, Cream);
                Label(card, PetBattleRules.MoveLabel(game.PendingBattleMove, state.Teams[side].Fighter.SpeciesId), 27, Purple);
                Label(card, question.Prompt, 32, Ink);
                if (game.Save.Options.PetHelp) ButtonNode(card, "Pomoc od spoločníka", ShowPetHelper, Mint, Ink, 23);
                for (int i = 0; i < question.Options.Length; i++)
                {
                    int option = i;
                    ButtonNode(card, ((char)('A' + i)) + "   " + question.Options[i], () => game.SubmitBattleAnswer(option), Lavender, Ink, 27);
                }
                Label(card, "Správne riešenie aktivuje schopnosť. Chyba spotrebuje ťah, ale prinesie vysvetlenie a jednu energiu.", 23, Muted);
            }
            else
            {
                Label(body, "ČO SPRAVÍ " + state.Teams[side].Fighter.Nickname.ToUpperInvariant() + "?", 28, Ink);
                foreach (var kind in new[] { PetBattleActionKind.Strike, PetBattleActionKind.Guard, PetBattleActionKind.Focus, PetBattleActionKind.Heal, PetBattleActionKind.QuickStrike })
                {
                    var action = kind; var move = PetBattleRules.GetMove(kind, state.Teams[side].Fighter.SpeciesId);
                    string error = PetBattleRules.AvailabilityError(state, side, kind);
                    var button = ButtonNode(body, move.Name + " · " + (kind == PetBattleActionKind.QuickStrike ? "bez otázky" : move.EnergyCost + " energie + úloha") + "\n" + move.Description + (string.IsNullOrEmpty(error) ? "" : "\n" + error), () => game.SelectBattleMove(action), kind == PetBattleActionKind.QuickStrike ? Mint : Lavender, Ink, 24);
                    button.interactable = string.IsNullOrEmpty(error);
                }
                var pets = state.Teams[side].Pets;
                for (int i = 0; i < pets.Length; i++) if (i != state.Teams[side].Active)
                {
                    int slot = i; var pet = pets[i];
                    var b = ButtonNode(body, "Vymeniť za " + pet.Nickname + " · " + pet.HP + "/" + pet.MaxHP + " HP · vyžaduje úlohu", () => game.SelectBattleMove(PetBattleActionKind.Switch, slot), Cream, Ink, 23);
                    b.interactable = PetBattleRules.CanUse(state, side, PetBattleActionKind.Switch, slot);
                }
            }
            if (state.Log?.Length > 0) { Label(body, "PRIEBEH SÚBOJA", 23, Purple); foreach (var line in state.Log.Skip(Math.Max(0, state.Log.Length - 8))) Label(body, line, 23, Muted); }
            if (!state.Complete) ButtonNode(body, "Vzdať súboj…", () => { Open("Vzdať tento súboj?", "ARÉNA"); FeaturePanel = "BATTLE"; Label(body, "Súper získa výhru. Rozpracovaný tréning sa ukončí.", 27, Ink); ButtonNode(body, "Vzdať", game.EndBattle, Lavender); ButtonNode(body, "Pokračovať v súboji", ShowBattle, Lime); }, Cream, Muted, 23);
        }
        private void BattleTeamCard(Transform parent, PetBattleTeam team, string caption, Color color)
        {
            var card = Card(parent, color); var p = team.Fighter; if (p == null) return;
            Label(card, caption + " · " + team.Name, 22, Purple);
            PetPortrait(card, p.SpeciesId, false, 130);
            Label(card, p.Nickname + " · " + (CompanionRules.Find(p.SpeciesId)?.Name ?? p.SpeciesId), 30, Ink);
            ProgressBar(card, p.HP / (float)Math.Max(1, p.MaxHP), p.HP < 30 ? new Color(.9f,.5f,.4f) : new Color(.4f,.7f,.45f));
            Label(card, p.HP + "/" + p.MaxHP + " HP · energia " + p.Energy + "/6 · štít " + p.Shield + (p.Focused ? " · SÚSTREDENÝ" : ""), 23, Ink);
            Label(card, "Útok " + p.StrikePP + "/12 · sústredenie " + p.FocusPP + "/4 · liečenie " + p.HealPP + "/2", 22, Muted);
        }
    }
}
