using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumen.Core
{
    public enum PetBattleActionKind { Strike, Guard, Focus, Heal, Switch, QuickStrike }
    public enum PetBattlePhase { Choosing, Complete }

    [Serializable]
    public sealed class PetBattleFighter
    {
        public string SpeciesId;
        public string Nickname;
        public int HP = 100;
        public int MaxHP = 100;
        public int AttackBonus;
        public int Energy = 4;
        public int StrikePP = 12;
        public int FocusPP = 4;
        public int HealPP = 2;
        public int HealCooldown;
        public int Shield;
        public bool Focused;
        public bool KnockedOut => HP <= 0;
    }

    [Serializable]
    public sealed class PetBattleTeam
    {
        public string Name;
        public PetBattleFighter[] Pets = Array.Empty<PetBattleFighter>();
        public int Active;
        public PetBattleFighter Fighter => Pets != null && Active >= 0 && Active < Pets.Length ? Pets[Active] : null;
    }

    [Serializable]
    public sealed class PetBattleCommand
    {
        public PetBattleActionKind Kind;
        public int TargetSlot;
        /// <summary>The caller must establish correctness with its authoritative question service.</summary>
        public bool Correct;
        /// <summary>Authoritatively assigned question Subject (0..5); -1 means no question/topic bonus.</summary>
        public int Topic = -1;
        public string ActionId;
    }

    [Serializable]
    public sealed class PetBattleState
    {
        public string Id;
        public int Seed;
        public int Round = 1;
        public int RoundsResolved;
        public PetBattlePhase Phase;
        /// <summary>-2 while playing; -1 for a draw; otherwise the winning side, 0 or 1.</summary>
        public int Winner = -2;
        public bool IsPractice;
        public PetBattleTeam[] Teams = new PetBattleTeam[2];
        public PetBattleCommand[] PendingActions = new PetBattleCommand[2];
        public bool[] RewardClaimed = new bool[2];
        public int[] CorrectAnswers = new int[2];
        public string[] Log = Array.Empty<string>();
        public bool Complete => Phase == PetBattlePhase.Complete;
    }

    [Serializable]
    public sealed class PetBattleResult
    {
        public bool Accepted;
        public bool Duplicate;
        public string Error = "";
        public bool RoundResolved;
        public bool Complete;
        public int Round;
        public int Winner = -2;
    }

    [Serializable]
    public sealed class PetBattleReward
    {
        public bool Claimed;
        public int XP;
        public int Coins;
        public int PetXP;
        public string Error = "";
    }

    public sealed class PetBattleMove
    {
        public PetBattleActionKind Kind;
        public string Name;
        public string Description;
        public int EnergyCost;
        public int MaxPP;
        public int Power;
        public int Priority;
    }

    /// <summary>
    /// Shared, engine-independent duel rules. Base HP, energy and damage are equalized. Optional
    /// validated loadouts have explicit small caps; collection XP never directly changes damage. Integer arithmetic is mirrored by
    /// the online server. State/commands use public fields for Unity JsonUtility serialization.
    /// </summary>
    public static class PetBattleRules
    {
        public const int MaxHP = 100;
        public const int MaxEnergy = 6;
        public const int MaxRounds = 100;
        public const int StrikePower = 26;
        public const int GuardShield = 14;
        public const int HealPower = 30;
        public const int QuickStrikePower = 8;
        public const int TopicBonusPercent = 120;

        public static Subject GetTopicAffinity(string speciesId)
        {
            switch (speciesId)
            {
                case "ember": return Subject.Technology;
                case "bolt": case "rovnik": return Subject.Nature;
                case "astra": case "geora": case "prizma": return Subject.History;
                case "percenta": case "ratio": return Subject.Science;
                case "datax": return Subject.Languages;
                default: return Subject.Mathematics;
            }
        }

        public static bool RequiresAnswer(PetBattleActionKind kind) => kind != PetBattleActionKind.QuickStrike;

        /// <summary>Checks persisted state shape before a serializer-created placeholder reaches gameplay.</summary>
        public static bool IsValidState(PetBattleState state) => Valid(state);

        /// <summary>Apply a server-validated loadout before the first submitted action. Idempotent, capped and per pet.</summary>
        public static bool SetLoadoutBonus(PetBattleState state, int side, int slot, int hpBonus, int attackBonus)
        {
            if (!Valid(state) || state.Complete || state.Round != 1 || state.RoundsResolved != 0 || side < 0 || side > 1
                || slot < 0 || slot >= state.Teams[side].Pets.Length || state.PendingActions.Any(p => p != null)) return false;
            var pet = state.Teams[side].Pets[slot];
            pet.MaxHP = MaxHP + Math.Clamp(hpBonus, 0, 20); pet.HP = pet.MaxHP;
            pet.AttackBonus = Math.Clamp(attackBonus, 0, 3); return true;
        }

        public static PetBattleState CreatePractice(CompanionCollection owned, string battleId, int seed = 1)
        {
            if (owned?.Owned == null) return null;
            var valid = owned.Owned.Where(p => p != null && CompanionRules.Find(p.SpeciesId) != null)
                .GroupBy(p => p.SpeciesId).Select(g => g.First()).ToList();
            if (valid.Count == 0) return null;
            valid = valid.OrderByDescending(p => p.SpeciesId == owned.EquippedId).Take(3).ToList();
            var right = new string[valid.Count];
            int offset = (int)((uint)seed % (uint)CompanionRules.Species.Length);
            for (int i = 0; i < right.Length; i++) right[i] = CompanionRules.Species[(offset + i) % CompanionRules.Species.Length].Id;
            var state = Create(battleId, valid.Select(p => p.SpeciesId).ToArray(), right, seed);
            state.IsPractice = true;
            state.Teams[0].Name = "Tvoj tím";
            state.Teams[1].Name = "Tréner akadémie";
            for (int i = 0; i < valid.Count; i++)
                state.Teams[0].Pets[i].Nickname = CleanNickname(valid[i].Nickname, valid[i].SpeciesId);
            return state;
        }

        public static PetBattleState Create(string battleId, string[] leftSpecies, string[] rightSpecies, int seed = 1)
        {
            if (string.IsNullOrWhiteSpace(battleId) || battleId.Length > 128)
                throw new ArgumentException("A battle requires a stable identifier up to 128 characters.");
            var state = new PetBattleState { Id = battleId, Seed = seed };
            state.Teams[0] = Team("Tím 1", leftSpecies);
            state.Teams[1] = Team("Tím 2", rightSpecies);
            state.Log = new[] { "Začína priateľský súboj. Správna matematická odpoveď aktivuje zvolený ťah." };
            return state;
        }

        public static string ActionId(PetBattleState state, int side) => state.Id + ":" + state.Round + ":" + side;
        public static string MoveLabel(PetBattleActionKind kind, string speciesId = null) => GetMove(kind, speciesId).Name;
        public static string MoveDescription(PetBattleActionKind kind, string speciesId = null) => GetMove(kind, speciesId).Description;

        public static PetBattleMove GetMove(PetBattleActionKind kind, string speciesId = null)
        {
            switch (kind)
            {
                case PetBattleActionKind.Strike:
                    return Move(kind, SignatureName(speciesId),
                        "Útok 26. Typová výhoda ±20 %, správna úloha zo špecializácie +20 %; sústredenie posilní ďalší útok o 50 %.", 2, 12, StrikePower, 1);
                case PetBattleActionKind.Guard:
                    return Move(kind, "Ochranný štít", "Zablokuje 14 poškodenia v tomto kole a obnoví 2 energie. Obrana má prednosť pred útokom.", 0, 0, GuardShield, 3);
                case PetBattleActionKind.Focus:
                    return Move(kind, "Sústredenie", "Ďalší úspešný útok bude o 50 % silnejší. Účinok zostáva aj po vystriedaní.", 1, 4, 0, 2);
                case PetBattleActionKind.Heal:
                    return Move(kind, "Obnova", "Obnoví 30 HP. Dve použitia na spoločníka; potom treba počkať dve celé kolá.", 3, 2, HealPower, 2);
                case PetBattleActionKind.Switch:
                    return Move(kind, "Vystriedať", "Vymeň aktívneho spoločníka. Výmena prebehne pred útokmi a spotrebuje tento ťah.", 0, 0, 0, 4);
                case PetBattleActionKind.QuickStrike:
                    return Move(kind, "Rýchly výpad", "Slabý zásah bez otázky: sila 8, ktorú môže zachytiť štít. Nezískava vedomostné body ani nevyužíva bonus výbavy či sústredenia.", 0, 0, QuickStrikePower, 1);
                default: return Move(kind, "Neznámy ťah", "Tento ťah nie je dostupný.", 0, 0, 0, 0);
            }
        }

        public static string AvailabilityError(PetBattleState state, int side, PetBattleActionKind kind, int targetSlot = 0)
        {
            if (!Valid(state) || side < 0 || side > 1) return "Neplatný stav súboja.";
            if (state.Complete) return "Súboj sa už skončil.";
            if (state.PendingActions[side] != null) return "Tvoj ťah je už potvrdený.";
            var team = state.Teams[side]; var pet = team.Fighter;
            if (pet == null || pet.KnockedOut) return "Spoločník nemôže pokračovať.";
            if (kind < PetBattleActionKind.Strike || kind > PetBattleActionKind.QuickStrike) return "Neznámy ťah.";
            if (kind == PetBattleActionKind.Switch)
                return targetSlot < 0 || targetSlot >= team.Pets.Length || targetSlot == team.Active || team.Pets[targetSlot].KnockedOut
                    ? "Vyber iného zdravého spoločníka." : "";
            var move = GetMove(kind, pet.SpeciesId);
            if (pet.Energy < move.EnergyCost) return "Nedostatok energie. Zvoľ ochranný štít.";
            if (kind == PetBattleActionKind.Strike && pet.StrikePP <= 0) return "Výpady sú vyčerpané. Vystriedaj spoločníka.";
            if (kind == PetBattleActionKind.Focus && (pet.FocusPP <= 0 || pet.Focused)) return "Sústredenie už pôsobí alebo je vyčerpané.";
            if (kind == PetBattleActionKind.Heal && (pet.HealPP <= 0 || pet.HealCooldown > 0 || pet.HP >= pet.MaxHP)) return "Obnova ešte nie je dostupná.";
            return "";
        }

        public static bool CanUse(PetBattleState state, int side, PetBattleActionKind kind, int targetSlot = 0)
            => AvailabilityError(state, side, kind, targetSlot).Length == 0;

        public static PetBattleResult Submit(PetBattleState state, int side, int expectedRound, PetBattleCommand command)
        {
            if (!Valid(state) || side < 0 || side > 1 || command == null) return Rejected(state, "Neplatný ťah.");
            if (state.Complete) return Rejected(state, "Súboj sa už skončil.");
            if (expectedRound != state.Round || command.ActionId != ActionId(state, side)) return Rejected(state, "Tento ťah patrí do iného kola.", true);
            if (state.PendingActions[side] != null) return Rejected(state, "Tvoj ťah je už potvrdený.", true);
            if (command.Topic < -1 || command.Topic > 5) return Rejected(state, "Neplatná téma otázky.");
            string error = AvailabilityError(state, side, command.Kind, command.TargetSlot);
            if (error.Length != 0) return Rejected(state, error);
            // Copy the command so a caller cannot mutate an already committed move.
            state.PendingActions[side] = new PetBattleCommand {
                Kind = command.Kind, TargetSlot = command.TargetSlot, Correct = command.Correct, Topic = command.Topic, ActionId = command.ActionId
            };
            bool resolved = state.PendingActions[0] != null && state.PendingActions[1] != null;
            if (resolved) Resolve(state);
            return new PetBattleResult { Accepted = true, RoundResolved = resolved, Complete = state.Complete, Round = state.Round, Winner = state.Winner };
        }

        public static PetBattleCommand ChooseAi(PetBattleState state, int side)
        {
            if (!Valid(state) || side < 0 || side > 1 || state.Complete || state.PendingActions[side] != null) return null;
            var team = state.Teams[side]; var fighter = team.Fighter;
            PetBattleActionKind kind = PetBattleActionKind.Strike; int slot = 0;
            if (fighter.HP <= 45 && CanUse(state, side, PetBattleActionKind.Heal)) kind = PetBattleActionKind.Heal;
            else if (fighter.StrikePP <= 0)
            {
                int next = Array.FindIndex(team.Pets, p => p != fighter && !p.KnockedOut && p.StrikePP > 0);
                if (next >= 0) { kind = PetBattleActionKind.Switch; slot = next; }
                else kind = PetBattleActionKind.QuickStrike;
            }
            else if (fighter.Energy < 2) kind = PetBattleActionKind.Guard;
            else if (CanUse(state, side, PetBattleActionKind.Focus) && ((state.Round + side + (state.Seed & 7)) % 6 == 0)) kind = PetBattleActionKind.Focus;
            return new PetBattleCommand { Kind = kind, TargetSlot = slot, Correct = true, ActionId = ActionId(state, side) };
        }

        public static bool Forfeit(PetBattleState state, int side, int expectedRound)
        {
            if (!Valid(state) || state.Complete || side < 0 || side > 1 || state.Round != expectedRound) return false;
            state.Phase = PetBattlePhase.Complete; state.Winner = 1 - side;
            state.PendingActions = new PetBattleCommand[2];
            state.Log = new[] { state.Teams[side].Name + " ukončil súboj." };
            return true;
        }

        public static PetBattleReward ClaimVictory(PetBattleState state, int side)
        {
            if (!Valid(state) || side < 0 || side > 1 || !state.Complete || state.Winner != side)
                return new PetBattleReward { Error = "Odmena patrí víťazovi dokončeného súboja." };
            if (state.RewardClaimed[side]) return new PetBattleReward { Error = "Odmena už bola prevzatá." };
            // A forfeit with no mathematical participation does not earn progress.
            if (state.CorrectAnswers[side] <= 0) return new PetBattleReward { Error = "Odmena vyžaduje aspoň jednu správnu odpoveď." };
            state.RewardClaimed[side] = true;
            return new PetBattleReward { Claimed = true, XP = 60, Coins = 100, PetXP = 40 };
        }

        /// <summary>120 = advantage, 80 = disadvantage, 100 = neutral.</summary>
        public static int Effectiveness(string attacker, string defender)
        {
            var left = CompanionRules.Find(attacker); var right = CompanionRules.Find(defender);
            if (left == null || right == null || left.BattleAffinity == right.BattleAffinity) return 100;
            return left.BattleAffinity == 0 && right.BattleAffinity == 2 || left.BattleAffinity == 2 && right.BattleAffinity == 1 || left.BattleAffinity == 1 && right.BattleAffinity == 0 ? 120 : 80;
        }

        public static string AdvantageDescription(string speciesId)
        {
            var species = CompanionRules.Find(speciesId); int affinity = species?.BattleAffinity ?? 0;
            return affinity == 0 ? "Početné typy majú výhodu proti priestorovým; algebraické typy majú výhodu proti početným."
                : affinity == 1 ? "Algebraické typy majú výhodu proti početným; priestorové typy majú výhodu proti algebraickým."
                : "Priestorové typy majú výhodu proti algebraickým; početné typy majú výhodu proti priestorovým.";
        }

        private static void Resolve(PetBattleState state)
        {
            var log = new List<string> { "Kolo " + state.Round };
            int firstPriority = GetMove(state.PendingActions[0].Kind).Priority;
            int secondPriority = GetMove(state.PendingActions[1].Kind).Priority;
            int first = firstPriority == secondPriority ? (int)(((uint)state.Seed + (uint)state.Round) & 1u) : firstPriority > secondPriority ? 0 : 1;
            Apply(state, first, log); Apply(state, 1 - first, log);
            for (int side = 0; side < 2; side++)
            {
                var team = state.Teams[side];
                foreach (var pet in team.Pets)
                {
                    pet.Shield = 0;
                    pet.HealCooldown = Math.Max(0, pet.HealCooldown - 1);
                }
                if (!team.Fighter.KnockedOut) team.Fighter.Energy = Math.Min(MaxEnergy, team.Fighter.Energy + 1);
                else
                {
                    int next = Array.FindIndex(team.Pets, p => !p.KnockedOut);
                    if (next >= 0) { team.Active = next; log.Add(team.Name + ": na scénu prichádza " + team.Fighter.Nickname + "."); }
                }
            }
            state.RoundsResolved++;
            bool leftAlive = state.Teams[0].Pets.Any(p => !p.KnockedOut);
            bool rightAlive = state.Teams[1].Pets.Any(p => !p.KnockedOut);
            if (!leftAlive || !rightAlive || state.Round >= MaxRounds)
            {
                state.Phase = PetBattlePhase.Complete;
                state.Winner = leftAlive && !rightAlive ? 0 : !leftAlive && rightAlive ? 1 : -1;
                log.Add(state.Winner == -1 ? "Priateľská remíza." : "Víťaz: " + state.Teams[state.Winner].Name + ".");
            }
            else state.Round++;
            state.PendingActions = new PetBattleCommand[2]; state.Log = log.ToArray();
        }

        private static void Apply(PetBattleState state, int side, List<string> log)
        {
            var command = state.PendingActions[side]; var team = state.Teams[side]; var pet = team.Fighter;
            if (pet.KnockedOut) { log.Add(pet.Nickname + " už nemôže vykonať ťah."); return; }
            if (RequiresAnswer(command.Kind) && !command.Correct)
            {
                pet.Energy = Math.Min(MaxEnergy, pet.Energy + 1);
                log.Add(pet.Nickname + ": nesprávna odpoveď, ťah sa neaktivoval. Pozri si vysvetlenie a skús ďalšiu úlohu.");
                return;
            }
            if (RequiresAnswer(command.Kind)) state.CorrectAnswers[side]++;
            if (command.Kind == PetBattleActionKind.Switch)
            {
                team.Active = command.TargetSlot;
                log.Add(team.Name + " povoláva " + team.Fighter.Nickname + "."); return;
            }
            pet.Energy -= GetMove(command.Kind, pet.SpeciesId).EnergyCost;
            switch (command.Kind)
            {
                case PetBattleActionKind.Guard:
                    pet.Shield = GuardShield; pet.Energy = Math.Min(MaxEnergy, pet.Energy + 2);
                    log.Add(pet.Nickname + " vytvára ochranný štít a dopĺňa energiu."); break;
                case PetBattleActionKind.Focus:
                    pet.FocusPP--; pet.Focused = true; log.Add(pet.Nickname + " sa sústredí na ďalší výpad."); break;
                case PetBattleActionKind.Heal:
                    pet.HealPP--; pet.HealCooldown = 3; int restored = Math.Min(HealPower, pet.MaxHP - pet.HP); pet.HP += restored;
                    log.Add(pet.Nickname + " obnovuje " + restored + " HP."); break;
                case PetBattleActionKind.Strike:
                    pet.StrikePP--; var defender = state.Teams[1 - side].Fighter;
                    int effectiveness = Effectiveness(pet.SpeciesId, defender.SpeciesId);
                    int damage = (StrikePower + pet.AttackBonus) * effectiveness / 100;
                    bool topicBonus = command.Topic == (int)GetTopicAffinity(pet.SpeciesId);
                    if (topicBonus) damage = damage * TopicBonusPercent / 100;
                    if (pet.Focused) { damage = damage * 150 / 100; pet.Focused = false; }
                    damage = Math.Max(0, damage - defender.Shield); defender.Shield = 0;
                    defender.HP = Math.Max(0, defender.HP - damage);
                    log.Add(pet.Nickname + " používa " + MoveLabel(command.Kind, pet.SpeciesId) + ": " + damage + " poškodenia."
                        + (effectiveness > 100 ? " Typová výhoda!" : effectiveness < 100 ? " Menší účinok proti tomuto typu." : "")
                        + (topicBonus ? " Bonus matematickej špecializácie!" : ""));
                    if (defender.KnockedOut) log.Add(defender.Nickname + " si potrebuje oddýchnuť.");
                    break;
                case PetBattleActionKind.QuickStrike:
                    var quickTarget = state.Teams[1 - side].Fighter;
                    int quickDamage = Math.Max(0, QuickStrikePower - quickTarget.Shield); quickTarget.Shield = 0;
                    quickTarget.HP = Math.Max(0, quickTarget.HP - quickDamage);
                    log.Add(pet.Nickname + " používa rýchly výpad: " + quickDamage + " poškodenia bez otázky.");
                    if (quickTarget.KnockedOut) log.Add(quickTarget.Nickname + " si potrebuje oddýchnuť.");
                    break;
            }
        }

        private static PetBattleTeam Team(string name, string[] species)
        {
            if (species == null || species.Length < 1 || species.Length > 3 || species.Distinct().Count() != species.Length || species.Any(s => CompanionRules.Find(s) == null))
                throw new ArgumentException("A party requires one to three distinct, known companion species.");
            return new PetBattleTeam { Name = name, Pets = species.Select(s => new PetBattleFighter { SpeciesId = s, Nickname = CompanionRules.Find(s).Name }).ToArray() };
        }

        private static PetBattleMove Move(PetBattleActionKind kind, string name, string description, int cost, int pp, int power, int priority)
            => new PetBattleMove { Kind = kind, Name = name, Description = description, EnergyCost = cost, MaxPP = pp, Power = power, Priority = priority };

        private static string CleanNickname(string nickname, string speciesId)
            => string.IsNullOrWhiteSpace(nickname) ? CompanionRules.Find(speciesId).Name : ProfileRules.CleanName(nickname);

        private static string SignatureName(string speciesId)
        {
            switch (speciesId)
            {
                case "ember": return "Iskrivý výpad";
                case "bolt": return "Voltový impulz";
                case "astra": return "Hviezdny let";
                case "zlomko": return "Zlomková vlna";
                case "percenta": return "Stotinový záblesk";
                case "rovnik": return "Rovnovážny úder";
                case "geora": return "Trojuholníkový náraz";
                case "prizma": return "Priestorový lúč";
                case "datax": return "Dátová lavína";
                case "ratio": return "Pomerný šprint";
                default: return "Výpad";
            }
        }

        private static PetBattleResult Rejected(PetBattleState state, string error, bool duplicate = false)
            => new PetBattleResult { Error = error, Duplicate = duplicate, Round = state?.Round ?? 0, Complete = state?.Complete ?? false, Winner = state?.Winner ?? -2 };

        private static bool Valid(PetBattleState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.Id) || state.Round < 1 || state.Round > MaxRounds
                || state.Teams == null || state.Teams.Length != 2 || state.PendingActions == null || state.PendingActions.Length != 2
                || state.RewardClaimed == null || state.RewardClaimed.Length != 2 || state.CorrectAnswers == null || state.CorrectAnswers.Length != 2) return false;
            foreach (var team in state.Teams)
            {
                if (team?.Pets == null || team.Pets.Length < 1 || team.Pets.Length > 3 || team.Active < 0 || team.Active >= team.Pets.Length) return false;
                foreach (var pet in team.Pets)
                    if (pet == null || CompanionRules.Find(pet.SpeciesId) == null || pet.MaxHP < MaxHP || pet.MaxHP > MaxHP + 20 || pet.AttackBonus < 0 || pet.AttackBonus > 3 || pet.HP < 0 || pet.HP > pet.MaxHP || pet.Energy < 0 || pet.Energy > MaxEnergy
                        || pet.StrikePP < 0 || pet.StrikePP > 12 || pet.FocusPP < 0 || pet.FocusPP > 4 || pet.HealPP < 0 || pet.HealPP > 2 || pet.HealCooldown < 0 || pet.HealCooldown > 3
                        || pet.Shield < 0 || pet.Shield > GuardShield) return false;
            }
            return true;
        }
    }
}
