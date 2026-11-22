using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web;
using Lumen.Core;

internal static class Program
{
    private static int assertions;
    private static readonly JsonSerializerOptions Wire = new JsonSerializerOptions {
        IncludeFields = true, IgnoreReadOnlyProperties = true, WriteIndented = true
    };
    private static void Check(bool value, string message)
    { assertions++; if (!value) throw new InvalidOperationException(message); }
    private static void Throws(Action action, string message)
    { bool threw = false; try { action(); } catch (ArgumentException) { threw = true; } Check(threw, message); }
    private static PetBattleCommand Command(PetBattleState state, int side, PetBattleActionKind kind, bool correct = true, int slot = 0)
        => new PetBattleCommand { ActionId = PetBattleRules.ActionId(state, side), Kind = kind, Correct = correct, TargetSlot = slot };
    private static PetBattleState Match(string left = "ember", string right = "ember", int seed = 1)
        => PetBattleRules.Create("harness-match", new[] { left }, new[] { right }, seed);
    private static PetBattleResult Round(PetBattleState state, PetBattleActionKind left, PetBattleActionKind right, bool leftCorrect = true, bool rightCorrect = true, int leftSlot = 0, int rightSlot = 0)
    {
        int round = state.Round;
        var a = PetBattleRules.Submit(state, 0, round, Command(state, 0, left, leftCorrect, leftSlot));
        Check(a.Accepted && !a.RoundResolved, "first side commits without resolving");
        var b = PetBattleRules.Submit(state, 1, round, Command(state, 1, right, rightCorrect, rightSlot));
        Check(b.Accepted && b.RoundResolved, "second side resolves exactly one round");
        return b;
    }

    private static void CollectionAndMoves()
    {
        Check(CompanionRules.Species.Length == 10 && CompanionRules.Species.Select(p => p.Id).Distinct().Count() == 10, "ten unique companions");
        Check(CompanionRules.Species.Select(p => p.Visual).Distinct().Count() == 10, "ten distinct visual identifiers");
        Check(CompanionRules.Species.All(p => !string.IsNullOrWhiteSpace(p.Specialty)), "every species has a math specialty");
        var owned = new CompanionCollection();
        CompanionRules.ClaimStarter(owned, "ember"); owned.Eggs = 10;
        for (int i = 0; i < 9; i++)
        {
            var hatch = CompanionRules.Hatch(owned, out bool duplicate);
            Check(hatch != null && !duplicate, "all ten species collected before duplicate " + i);
        }
        Check(owned.Owned.Count == 10, "full ten species collection");
        Check(CompanionRules.Hatch(owned, out bool repeat) != null && repeat && owned.Owned.Count == 10, "duplicate after full collection grants XP");
        owned.EquippedId = "prizma"; owned.Owned.First(p => p.SpeciesId == "prizma").Nickname = "Priestor";
        var practice = PetBattleRules.CreatePractice(owned, "practice", 9);
        // Equipped pets must be considered before the three-pet party cap.
        Check(practice != null && practice.Teams[0].Pets.Length == 3 && practice.Teams[0].Fighter.SpeciesId == "prizma", "equipped species leads capped party");
        Check(practice.Teams[0].Fighter.Nickname == "Priestor", "battle nickname preserved");
        Check(practice.Teams.All(t => t.Pets.All(p => p.HP == 100 && p.MaxHP == 100 && p.AttackBonus == 0)), "equalized defaults regardless of collection XP");
        Check(PetBattleRules.CreatePractice(new CompanionCollection(), "empty") == null, "empty collection cannot enter practice");
        Throws(() => PetBattleRules.Create("bad", new[] { "unknown" }, new[] { "ember" }), "invalid species rejected");
        Throws(() => PetBattleRules.Create("bad", new[] { "ember", "ember" }, new[] { "ember" }), "duplicate party species rejected");
        Throws(() => PetBattleRules.Create("", new[] { "ember" }, new[] { "ember" }), "empty battle identifier rejected");
        Check(CompanionRules.Species.Select(p => PetBattleRules.MoveLabel(PetBattleActionKind.Strike, p.Id)).Distinct().Count() == 10, "ten original signature moves");
        foreach (PetBattleActionKind kind in Enum.GetValues(typeof(PetBattleActionKind)))
            Check(!string.IsNullOrWhiteSpace(PetBattleRules.GetMove(kind).Description), "move has UI description " + kind);
        Check(!PetBattleRules.RequiresAnswer(PetBattleActionKind.QuickStrike) && PetBattleRules.RequiresAnswer(PetBattleActionKind.Strike), "only weak strike bypasses question");
    }

    private static void AttacksAndResources()
    {
        var guarded = Match(); Round(guarded, PetBattleActionKind.Strike, PetBattleActionKind.Guard);
        Check(guarded.Teams[1].Fighter.HP == 88, "guard absorbs fourteen before strike");
        Check(guarded.Teams[0].Fighter.Energy == 3 && guarded.Teams[0].Fighter.StrikePP == 11, "strike consumes energy and PP then regenerates one");
        Check(guarded.Teams[1].Fighter.Energy == 6 && guarded.Teams[1].Fighter.Shield == 0, "guard energy capped and shield expires");
        var advantage = Match("ember", "astra"); Round(advantage, PetBattleActionKind.Strike, PetBattleActionKind.Strike, true, false);
        Check(advantage.Teams[1].Fighter.HP == 69 && advantage.Teams[0].Fighter.HP == 100, "advantage integer floor gives31 and incorrect attack misses");
        var disadvantage = Match("astra", "ember"); Round(disadvantage, PetBattleActionKind.Strike, PetBattleActionKind.Guard, true, false);
        Check(disadvantage.Teams[1].Fighter.HP == 80, "disadvantage integer floor gives20");
        Check(PetBattleRules.Effectiveness("zlomko", "geora") == 120 && PetBattleRules.Effectiveness("percenta", "ratio") == 120 && PetBattleRules.Effectiveness("prizma", "rovnik") == 120, "all ten pets use documented affinity cycle");
        Check(PetBattleRules.Effectiveness("ratio", "datax") == 100, "same affinity is neutral");
        var topic = Match("rovnik", "rovnik");
        var topicCommand = Command(topic, 0, PetBattleActionKind.Strike); topicCommand.Topic = (int)Subject.Nature;
        Check(PetBattleRules.Submit(topic, 0, 1, topicCommand).Accepted, "math topic strike commits");
        Check(PetBattleRules.Submit(topic, 1, 1, Command(topic, 1, PetBattleActionKind.Guard, false)).RoundResolved && topic.Teams[1].Fighter.HP == 69, "matching topic grants integer31damage");
        Check(CompanionRules.Species.Select(p => (int)PetBattleRules.GetTopicAffinity(p.Id)).Distinct().Count() == 6, "pet specialties cover all six math topics");
        var wrong = Match(); Round(wrong, PetBattleActionKind.Strike, PetBattleActionKind.Guard, false, true);
        Check(wrong.Teams[1].Fighter.HP == 100 && wrong.Teams[0].Fighter.Energy == 6 && wrong.Teams[0].Fighter.StrikePP == 12, "incorrect answer cannot deal damage or consume PP");
        Check(wrong.CorrectAnswers[0] == 0, "incorrect answer does not earn learning participation");
        var focus = Match(); Round(focus, PetBattleActionKind.Focus, PetBattleActionKind.Guard);
        Check(focus.Teams[0].Fighter.Focused && focus.Teams[0].Fighter.FocusPP == 3, "focus persists and spends one PP");
        Check(!PetBattleRules.CanUse(focus, 0, PetBattleActionKind.Focus), "focus cannot stack");
        Round(focus, PetBattleActionKind.Strike, PetBattleActionKind.Strike, true, false);
        Check(focus.Teams[1].Fighter.HP == 61 && !focus.Teams[0].Fighter.Focused, "focused strike deals39 and consumes focus");
        var quick = Match(); quick.Teams[0].Fighter.Focused = true;
        Round(quick, PetBattleActionKind.QuickStrike, PetBattleActionKind.QuickStrike, false, false);
        Check(quick.Teams.All(t => t.Fighter.HP == 92 && t.Fighter.StrikePP == 12), "both weak attacks hit without questions and preserve PP");
        Check(quick.CorrectAnswers.All(a => a == 0) && quick.Teams[0].Fighter.Focused, "weak attack earns no learning counter and preserves focus");
        var recovery = Match(); recovery.Teams[0].Fighter.HP = 40;
        Round(recovery, PetBattleActionKind.Heal, PetBattleActionKind.Guard);
        Check(recovery.Teams[0].Fighter.HP == 70 && recovery.Teams[0].Fighter.HealPP == 1 && recovery.Teams[0].Fighter.HealCooldown == 2, "heal restores30 and leaves two blocked rounds");
        Check(!PetBattleRules.CanUse(recovery, 0, PetBattleActionKind.Heal), "heal blocked during cooldown");
        Round(recovery, PetBattleActionKind.Guard, PetBattleActionKind.Guard);
        Check(recovery.Teams[0].Fighter.HealCooldown == 1, "first cooldown round");
        Round(recovery, PetBattleActionKind.Guard, PetBattleActionKind.Guard);
        Check(PetBattleRules.CanUse(recovery, 0, PetBattleActionKind.Heal), "heal returns after two full rounds");
        Round(recovery, PetBattleActionKind.Heal, PetBattleActionKind.Guard);
        Check(recovery.Teams[0].Fighter.HP == 100 && recovery.Teams[0].Fighter.HealPP == 0, "heal capped and PP finite");
    }

    private static void ReplaySwitchAndKnockout()
    {
        var state = Match(); int round = state.Round;
        var command = Command(state, 0, PetBattleActionKind.Strike);
        Check(PetBattleRules.Submit(state, 0, round, command).Accepted, "first submission accepted");
        command.Kind = PetBattleActionKind.Heal; command.Correct = false;
        Check(state.PendingActions[0].Kind == PetBattleActionKind.Strike && state.PendingActions[0].Correct, "committed command is isolated from caller mutation");
        var duplicate = PetBattleRules.Submit(state, 0, round, Command(state, 0, PetBattleActionKind.Strike));
        Check(!duplicate.Accepted && duplicate.Duplicate && state.Round == round && state.Teams[0].Fighter.StrikePP == 12, "pending duplicate cannot consume or resolve twice");
        Check(PetBattleRules.Submit(state, 1, round, Command(state, 1, PetBattleActionKind.Guard)).RoundResolved, "second player resolves committed round");
        Check(!PetBattleRules.Submit(state, 0, round, new PetBattleCommand { ActionId = state.Id + ":1:0" }).Accepted, "stale round replay rejected");
        Check(!PetBattleRules.Submit(state, 0, state.Round, new PetBattleCommand { ActionId = PetBattleRules.ActionId(state, 1) }).Accepted, "other side turn token rejected");
        Check(!PetBattleRules.Submit(state, -1, state.Round, command).Accepted, "invalid side rejected");
        Check(!PetBattleRules.Submit(state, 0, state.Round, null).Accepted, "null command rejected");
        Check(!PetBattleRules.Submit(state, 0, state.Round, Command(state, 0, (PetBattleActionKind)99)).Accepted, "invalid move rejected");
        state.Teams[0].Fighter.Energy = 0;
        Check(!PetBattleRules.CanUse(state, 0, PetBattleActionKind.Strike) && PetBattleRules.CanUse(state, 0, PetBattleActionKind.QuickStrike), "weak fallback prevents energy deadlock");
        var swap = PetBattleRules.Create("swap", new[] { "ember", "bolt" }, new[] { "astra" });
        Round(swap, PetBattleActionKind.Switch, PetBattleActionKind.Strike, true, true, 1);
        Check(swap.Teams[0].Active == 1 && swap.Teams[0].Pets[0].HP == 100 && swap.Teams[0].Pets[1].HP == 69, "switch priority redirects attack to incoming pet");
        Check(!PetBattleRules.CanUse(swap, 0, PetBattleActionKind.Switch, 1) && !PetBattleRules.CanUse(swap, 0, PetBattleActionKind.Switch, 5), "invalid switches rejected");
        var knockout = PetBattleRules.Create("ko", new[] { "ember" }, new[] { "astra", "bolt" }, 1);
        knockout.Teams[1].Pets[0].HP = 10;
        Round(knockout, PetBattleActionKind.Strike, PetBattleActionKind.Strike);
        Check(knockout.Teams[1].Pets[0].HP == 0 && knockout.Teams[1].Active == 1 && knockout.Teams[0].Fighter.HP == 100, "KO cancels outgoing move and promotes living reserve");
        knockout.Teams[1].Fighter.HP = 1;
        Round(knockout, PetBattleActionKind.Strike, PetBattleActionKind.Guard, true, false);
        Check(knockout.Complete && knockout.Winner == 0, "last KO ends battle");
        var reward = PetBattleRules.ClaimVictory(knockout, 0);
        Check(reward.Claimed && reward.XP == 60 && reward.Coins == 100 && reward.PetXP == 40, "victory reward has explicit values");
        Check(!PetBattleRules.ClaimVictory(knockout, 0).Claimed && !PetBattleRules.ClaimVictory(knockout, 1).Claimed, "duplicate and losing reward claims rejected");
        Check(!PetBattleRules.Submit(knockout, 0, knockout.Round, Command(knockout, 0, PetBattleActionKind.QuickStrike)).Accepted, "finished duel rejects moves");
        var quickOnly = Match(); quickOnly.Teams[1].Fighter.HP = 1;
        Round(quickOnly, PetBattleActionKind.QuickStrike, PetBattleActionKind.Guard, false, false);
        Check(quickOnly.Winner == 0 && !PetBattleRules.ClaimVictory(quickOnly, 0).Claimed, "quick-only victory cannot generate learning rewards");
        var forfeited = Match();
        Check(PetBattleRules.Forfeit(forfeited, 1, forfeited.Round) && forfeited.Winner == 0, "forfeit gives opposite side victory");
        Check(!PetBattleRules.ClaimVictory(forfeited, 0).Claimed && !PetBattleRules.Forfeit(forfeited, 0, forfeited.Round), "zero-answer forfeit cannot farm reward or re-forfeit");
    }

    private static void LoadoutsSerializationAndAi()
    {
        var gear = Match();
        Check(PetBattleRules.SetLoadoutBonus(gear, 0, 0, 999, 999), "validated bonus applied before play");
        Check(gear.Teams[0].Fighter.MaxHP == 120 && gear.Teams[0].Fighter.HP == 120 && gear.Teams[0].Fighter.AttackBonus == 3, "bonus cap enforces120HP29attack");
        Check(PetBattleRules.SetLoadoutBonus(gear, 0, 0, 999, 999) && gear.Teams[0].Fighter.MaxHP == 120, "loadout bonus idempotent");
        Round(gear, PetBattleActionKind.Strike, PetBattleActionKind.Guard, true, false);
        Check(gear.Teams[1].Fighter.HP == 71 && !PetBattleRules.SetLoadoutBonus(gear, 0, 0, 1, 1), "math attack uses capped bonus and loadout cannot change mid-battle");
        var restored = JsonSerializer.Deserialize<PetBattleState>(JsonSerializer.Serialize(gear, Wire), Wire);
        Check(restored != null && restored.Round == gear.Round && restored.Teams[0].Fighter.MaxHP == 120, "field-only battle state survives serialization");
        Round(restored, PetBattleActionKind.QuickStrike, PetBattleActionKind.Guard, false, false);
        Check(restored.Teams[1].Fighter.HP == 63, "weak attack ignores loadout attack bonus after restore");
        var draw = Match();
        while (!draw.Complete) Round(draw, PetBattleActionKind.Guard, PetBattleActionKind.Guard);
        Check(draw.Winner == -1 && draw.RoundsResolved == 100 && !PetBattleRules.ClaimVictory(draw, 0).Claimed, "stalemate ends at100 rounds without reward");
        for (int seed = 0; seed < 40; seed++)
        {
            var match = PetBattleRules.Create("ai-" + seed, new[] { "ember", "prizma", "percenta" }, new[] { "astra", "datax", "rovnik" }, seed);
            while (!match.Complete)
            {
                int round = match.Round;
                var left = PetBattleRules.ChooseAi(match, 0); var right = PetBattleRules.ChooseAi(match, 1);
                Check(left != null && right != null && PetBattleRules.Submit(match, 0, round, left).Accepted, "AI supplies valid left action seed" + seed);
                Check(PetBattleRules.Submit(match, 1, round, right).RoundResolved, "AI supplies valid right action seed" + seed);
                Check(match.Teams.All(t => t.Pets.All(p => p.HP >= 0 && p.HP <= p.MaxHP && p.Energy >= 0 && p.Energy <= 6 && p.StrikePP >= 0 && p.HealPP >= 0 && p.FocusPP >= 0)), "resources stay bounded during complete AI battles");
            }
            Check(match.RoundsResolved <= 100, "AI battle terminates seed" + seed);
        }
    }

    private static void Export(string path)
    {
        var json = new {
            schemaVersion = 1,
            scope = "Matematika pre 7.–9. ročník ZŠ; engine-independent companion and duel data",
            species = CompanionRules.Species.Select(p => new {
                id = p.Id, name = p.Name, description = p.Description, mathTopic = p.Specialty,
                visualId = p.Visual, battleAffinity = p.BattleAffinity,
                mathTopicId = (int)PetBattleRules.GetTopicAffinity(p.Id),
                signatureMove = PetBattleRules.MoveLabel(PetBattleActionKind.Strike, p.Id)
            }).ToArray(),
            affinities = new[] { new { id = 0, name = "Početný", strongAgainst = 2 }, new { id = 1, name = "Algebraický", strongAgainst = 0 }, new { id = 2, name = "Priestorový", strongAgainst = 1 } },
            rules = new {
                partyLimit = 3, baseMaxHP = 100, startingEnergy = 4, maxEnergy = 6, maxRounds = 100,
                strikePower = 26, quickStrikePower = 8, guardShield = 14, guardEnergy = 2,
                healPower = 30, healCooldownOnUse = 3, endRoundCooldownReduction = 1,
                endRoundEnergy = 1, shieldsExpireAtRoundEnd = true, focusedAttackPercent = 150,
                advantagePercent = 120, disadvantagePercent = 80, matchingMathTopicPercent = 120, loadoutHPBonusCap = 20, loadoutAttackBonusCap = 3,
                priorityTieFirstSide = "(unsigned(seed) + unsigned(round)) & 1",
                damageRounding = "floor((26 + attackBonus) * effectivenessPercent / 100); then floor(damage * 120 / 100) if math topic matches pet; then floor(damage * 150 / 100) if focused; then max(0, damage - shield)",
                actionToken = "battleId:round:side", rewardNeedsCorrectMath = true,
                victoryReward = new { xp = 60, coins = 100, petXP = 40 },
                moves = Enum.GetValues(typeof(PetBattleActionKind)).Cast<PetBattleActionKind>().Select(kind => {
                    var move = PetBattleRules.GetMove(kind);
                    return new { id = (int)kind, key = kind.ToString(), name = move.Name, description = move.Description,
                        energyCost = move.EnergyCost, maxPP = move.MaxPP, power = move.Power, priority = move.Priority,
                        requiresMathAnswer = PetBattleRules.RequiresAnswer(kind) };
                }).ToArray()
            }
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine("Exported engine-independent pet/rule catalog: " + Path.GetFullPath(path));
    }

    private static void ExportFixtures(string path)
    {
        var vectors = new List<object>();
        Action<string, PetBattleState, PetBattleCommand, PetBattleCommand> add = (label, state, left, right) => {
            var initial = JsonSerializer.SerializeToElement(state, Wire);
            var leftCopy = JsonSerializer.SerializeToElement(left, Wire);
            var rightCopy = JsonSerializer.SerializeToElement(right, Wire);
            int round = state.Round;
            if (!PetBattleRules.Submit(state, 0, round, left).Accepted || !PetBattleRules.Submit(state, 1, round, right).RoundResolved)
                throw new InvalidOperationException("Fixture action was not valid: " + label);
            vectors.Add(new { label, initial, left = leftCopy, right = rightCopy, expected = JsonSerializer.SerializeToElement(state, Wire) });
        };
        for (int seed = 1; seed <= 3; seed++)
        {
            var state = PetBattleRules.Create("conformance-ai-" + seed, new[] { "ember", "prizma", "percenta" }, new[] { "astra", "datax", "rovnik" }, seed);
            if (seed == 3) PetBattleRules.SetLoadoutBonus(state, 0, 0, 20, 3);
            while (!state.Complete)
            {
                var left = PetBattleRules.ChooseAi(state, 0); var right = PetBattleRules.ChooseAi(state, 1);
                left.Topic = (int)PetBattleRules.GetTopicAffinity(state.Teams[0].Fighter.SpeciesId);
                right.Topic = state.Round % 3 == 0 ? (int)PetBattleRules.GetTopicAffinity(state.Teams[1].Fighter.SpeciesId) : -1;
                if (state.Round % 7 == 0) left.Correct = false;
                add("ai-" + seed + "-round-" + state.Round, state, left, right);
            }
        }
        var swap = PetBattleRules.Create("conformance-swap", new[] { "ember", "bolt", "ratio" }, new[] { "astra", "rovnik", "geora" }, 1);
        add("switch-before-attack", swap, Command(swap, 0, PetBattleActionKind.Switch, true, 1), Command(swap, 1, PetBattleActionKind.Strike));
        add("incorrect-switch-stays", swap, Command(swap, 0, PetBattleActionKind.Switch, false, 2), Command(swap, 1, PetBattleActionKind.Guard));
        add("quick-vs-guard", swap, Command(swap, 0, PetBattleActionKind.QuickStrike, false), Command(swap, 1, PetBattleActionKind.Guard));
        add("focus-vs-quick", swap, Command(swap, 0, PetBattleActionKind.Focus), Command(swap, 1, PetBattleActionKind.QuickStrike, false));
        add("focus-preserved-by-quick", swap, Command(swap, 0, PetBattleActionKind.QuickStrike, false), Command(swap, 1, PetBattleActionKind.Guard, false));
        var focused = Command(swap, 0, PetBattleActionKind.Strike); focused.Topic = (int)PetBattleRules.GetTopicAffinity("bolt");
        add("type-topic-focus-integer-floors", swap, focused, Command(swap, 1, PetBattleActionKind.Guard));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, JsonSerializer.Serialize(new { schemaVersion = 1, vectors }, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine("Exported " + vectors.Count + " server conformance vectors: " + Path.GetFullPath(path));
    }

    public static void Main(string[] args)
    {
        CollectionAndMoves(); AttacksAndResources(); ReplaySwitchAndKnockout(); LoadoutsSerializationAndAi();
        Console.WriteLine("Pet battle harness: " + assertions + " assertions passed.");
        if (args.Length == 2 && args[0] == "--export") Export(args[1]);
        if (args.Length == 2 && args[0] == "--fixtures") ExportFixtures(args[1]);
    }
}
