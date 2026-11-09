using System;
using Lumen.Core;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string label) { assertions++; if (!value) throw new Exception(label); }
    public static void Main()
    {
        var c = new CompanionCollection();
        Check(CompanionRules.ClaimStarter(c, "unknown") == null, "invalid starter rejected");
        var fox = CompanionRules.ClaimStarter(c, "ember");
        Check(fox != null && c.Owned.Count == 1, "starter owned");
        Check(CompanionRules.ClaimStarter(c, "astra") == null, "second free starter rejected");
        Check(!CompanionRules.Equip(c, "astra") && CompanionRules.Equip(c, "ember"), "equip ownership enforced");
        Check(!CompanionRules.RewardCorrectAnswer(c, 12), "first correct towards egg");
        Check(!CompanionRules.RewardCorrectAnswer(c, 12), "second correct towards egg");
        Check(CompanionRules.RewardCorrectAnswer(c, 12) && c.Eggs == 1 && c.CorrectTowardsEgg == 0, "third correct earns egg");
        Check(fox.XP == 36 && fox.Bond == 3, "equipped companion gains XP and bond");
        var robot = CompanionRules.Hatch(c, out bool duplicate);
        Check(!duplicate && robot.SpeciesId == "bolt" && c.Eggs == 0, "first missing species hatched");
        Check(CompanionRules.Hatch(c, out duplicate) == null, "empty hatch rejected");
        c.Eggs = 9; var owl = CompanionRules.Hatch(c, out duplicate);
        Check(!duplicate && owl.SpeciesId == "astra" && c.Owned.Count == 3, "collection guaranteed before duplicates");
        for (int i = 0; i < CompanionRules.Species.Length - 3; i++)
        {
            var missing = CompanionRules.Hatch(c, out duplicate);
            Check(missing != null && !duplicate, "every new companion is guaranteed before repeats");
        }
        var repeat = CompanionRules.Hatch(c, out duplicate);
        Check(duplicate && repeat.XP >= 120 && c.Owned.Count == 10, "duplicate converts to XP after full ten-pet collection");
        fox.XP = -50; fox.Gear = 2; c.Eggs = -9; c.EquippedId = "invalid"; c.Owned.Add(new CompanionProgress { SpeciesId = "bad" });
        CompanionRules.Normalize(c);
        Check(fox.XP == 0 && fox.Gear == 0 && c.Eggs == 0 && c.Equipped == null && c.Owned.Count == 10, "save values normalized");
        fox.XP = 200; fox.Gear = 2; CompanionRules.Normalize(c); Check(fox.Gear == 1, "gear level gate");
        var guide = new AcademyTutorial(); Check(guide.Step == 0 && !guide.Complete, "tutorial initial step");
        guide.Walked = guide.Jumped = guide.OpenedQuiz = guide.AnsweredCorrectly = guide.ClaimedPet = guide.EquippedPet = true;
        Check(guide.Step == 6 && guide.Complete && !guide.RewardClaimed, "tutorial actions complete independently from one-time reward");
        CheckArtifacts();
        CheckBattleRound();
        Console.WriteLine("Companions, tutorial, artifacts and battle: " + assertions + " assertions passed.");
    }

    private static void CheckArtifacts()
    {
        var inventory = new ArtifactInventory();
        var first = ArtifactRules.RewardCorrect(inventory, "first-correct-answer", Subject.Mathematics, 7);
        Check(first.Awarded && first.Item != null && inventory.Items.Count == 1, "a new answer can award one artifact");
        var replay = ArtifactRules.RewardCorrect(inventory, "first-correct-answer", Subject.Mathematics, 7);
        Check(!replay.Awarded && inventory.Items.Count == 1 && inventory.Dust == first.Dust, "answer replay does not award gear or dust");
    }

    private static void CheckBattleRound()
    {
        var battle = PetBattleRules.Create("week-one-round", new[] { "ember" }, new[] { "zlomko" });
        int round = battle.Round;
        var quick = new PetBattleCommand { Kind = PetBattleActionKind.QuickStrike, ActionId = PetBattleRules.ActionId(battle, 0) };
        var wrong = new PetBattleCommand { Kind = PetBattleActionKind.Strike, ActionId = PetBattleRules.ActionId(battle, 1) };
        var first = PetBattleRules.Submit(battle, 0, round, quick);
        Check(first.Accepted && !first.RoundResolved, "the round waits for both moves");
        Check(!PetBattleRules.Submit(battle, 0, round, quick).Accepted, "the same side cannot submit twice");
        Check(PetBattleRules.Submit(battle, 1, round, wrong).RoundResolved && battle.Round == round + 1, "both moves resolve exactly one round");
        var player = battle.Teams[0].Fighter;
        var opponent = battle.Teams[1].Fighter;
        Check(player.HP == player.MaxHP && opponent.HP == opponent.MaxHP - PetBattleRules.QuickStrikePower,
            "a wrong answer cannot trigger a strike, while the small no-question move still works");
        Check(battle.CorrectAnswers[0] == 0 && battle.CorrectAnswers[1] == 0, "a no-question move does not grant learning credit");
    }
}
