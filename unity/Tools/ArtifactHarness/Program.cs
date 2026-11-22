using System;
using System.Collections.Generic;
using System.Linq;
using Lumen.Core;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string message)
    { assertions++; if (!value) throw new InvalidOperationException(message); }
    private static ArtifactItem Item(string id, ArtifactSlot slot = ArtifactSlot.Charm, int rarity = 1)
        => new ArtifactItem { Id = id, Slot = slot, Rarity = rarity, Set = Subject.Mathematics, MainStat = ArtifactStat.Vitality, MainValue = rarity * 2,
            Substats = new[] { new ArtifactSubstat { Stat = ArtifactStat.Power, Value = 1 } } };

    public static void Main()
    {
        var inventory = new ArtifactInventory();
        var first = ArtifactRules.RewardCorrect(inventory, "math-g7-s0-01:first", Subject.Mathematics, 7);
        Check(first.Awarded && first.Item != null && inventory.Items.Count == 1 && inventory.Dust == 2, "correct receipt grants deterministic artifact and dust");
        int dust = inventory.Dust, sequence = inventory.DropSequence;
        Check(!ArtifactRules.RewardCorrect(inventory, "math-g7-s0-01:first", Subject.Mathematics, 7).Awarded
            && inventory.Dust == dust && inventory.DropSequence == sequence && inventory.Items.Count == 1, "same receipt cannot generate duplicate loot");
        Check(!ArtifactRules.RewardCorrect(inventory, "", Subject.Mathematics, 7).Awarded, "empty receipt rejected");
        Check(!ArtifactRules.RewardCorrect(inventory, new string('x', 201), Subject.Mathematics, 7).Awarded, "oversized receipt rejected");
        var copy = new ArtifactInventory(); var second = ArtifactRules.RewardCorrect(copy, "math-g7-s0-01:first", Subject.Mathematics, 7);
        Check(second.Item.Id == first.Item.Id && second.Item.Rarity == first.Item.Rarity && second.Item.Slot == first.Item.Slot && second.Item.MainStat == first.Item.MainStat, "identical receipt and sequence reproduce loot");
        var epic = new ArtifactInventory { PityEpic = 11 };
        Check(ArtifactRules.RewardCorrect(epic, "pity-epic", Subject.Nature, 8).Item.Rarity >= 3 && epic.PityEpic == 0, "twelfth eligible answer guarantees at least epic");
        var legendary = new ArtifactInventory { PityLegendary = 39 };
        Check(ArtifactRules.RewardCorrect(legendary, "pity-legendary", Subject.History, 9).Item.Rarity == 4
            && legendary.PityLegendary == 0 && legendary.PityEpic == 0, "fortieth eligible answer guarantees legendary and resets pity");
        for (int i = 0; i < 550; i++)
        {
            var drop = ArtifactRules.RewardCorrect(inventory, "progress-" + i, (Subject)(i % 6), 7 + i % 3);
            Check(drop.Awarded && inventory.Items.Count <= ArtifactRules.MaximumItems, "drop capacity remains bounded");
            Check(inventory.Items.Select(a => a.Id).Distinct().Count() == inventory.Items.Count, "inventory IDs stay unique");
            Check(inventory.PityEpic >= 0 && inventory.PityEpic < 12 && inventory.PityLegendary >= 0 && inventory.PityLegendary < 40, "pity guarantees remain bounded across full sequence");
        }
        Check(inventory.Items.Count == ArtifactRules.MaximumItems && inventory.ClaimedKeys.Count == 551, "inventory cap preserves receipt replay ledger");

        var collection = new CompanionCollection(); CompanionRules.ClaimStarter(collection, "ember");
        var equipment = new ArtifactInventory { Dust = 10000 };
        equipment.Items.Add(Item("a", ArtifactSlot.Charm, 4)); equipment.Items.Add(Item("b", ArtifactSlot.Charm, 3));
        Check(!ArtifactRules.Equip(equipment, "a", "bolt", collection), "unowned pet cannot equip artifact");
        Check(!ArtifactRules.Equip(equipment, "missing", "ember", collection), "missing artifact cannot equip");
        Check(ArtifactRules.Equip(equipment, "a", "ember", collection) && !ArtifactRules.Salvage(equipment, "a"), "equipped artifact protected from salvage");
        Check(ArtifactRules.Equip(equipment, "b", "ember", collection) && equipment.Items.First(i => i.Id == "a").EquippedTo == null
            && equipment.Items.First(i => i.Id == "b").EquippedTo == "ember", "new item replaces same-slot equipment without duplication");
        dust = equipment.Dust;
        Check(ArtifactRules.Upgrade(equipment, "a") && equipment.Dust == dust - 3 && equipment.Items.First(i => i.Id == "a").Level == 1, "upgrade charges exact increasing cost");
        var upgraded = equipment.Items.First(i => i.Id == "a");
        while (ArtifactRules.Upgrade(equipment, "a")) { }
        Check(upgraded.Level == 12 && upgraded.MainValue == 20 && !ArtifactRules.Upgrade(equipment, "a"), "legendary reaches finite level12 cap");
        dust = equipment.Dust;
        Check(ArtifactRules.Salvage(equipment, "a") && equipment.Dust == dust + 28 && !equipment.Items.Any(i => i.Id == "a"), "salvage returns rarity and investment dust exactly once");
        Check(!ArtifactRules.Salvage(equipment, "a"), "salvage replay cannot duplicate dust");
        var poor = new ArtifactInventory(); poor.Items.Add(Item("poor"));
        Check(!ArtifactRules.Upgrade(poor, "poor") && poor.Items[0].Level == 0 && poor.Dust == 0, "insufficient dust leaves item unchanged");

        var fullSet = new ArtifactInventory();
        foreach (ArtifactSlot slot in Enum.GetValues(typeof(ArtifactSlot)))
        {
            var item = Item("set-" + slot, slot, 4); item.Level = 12; item.EquippedTo = "ember";
            item.Substats = new[] { new ArtifactSubstat { Stat = ArtifactStat.Power, Value = 12 }, new ArtifactSubstat { Stat = ArtifactStat.Insight, Value = 12 } };
            fullSet.Items.Add(item);
        }
        var bonus = ArtifactRules.Bonus(fullSet, "ember");
        Check(bonus.HP == 20 && bonus.Attack == 3 && bonus.SetPieces == 4, "four-piece set and substats cannot exceed combat caps");
        var battle = PetBattleRules.Create("artifact-combat", new[] { "ember" }, new[] { "bolt" });
        Check(PetBattleRules.SetLoadoutBonus(battle, 0, 0, bonus.HP, bonus.Attack) && battle.Teams[0].Fighter.MaxHP == 120 && battle.Teams[0].Fighter.AttackBonus == 3, "artifact bonus integrates shared battle API");

        // Defensive save normalization must not invent a bonus for unequipped items.
        var malformed = new ArtifactInventory {
            Items = new List<ArtifactItem> { null, Item("same"), Item("same"), Item("invalid") },
            ClaimedKeys = new List<string> { "duplicate", "duplicate", null, "" }, Dust = -9, PityEpic = 99, PityLegendary = 99
        };
        malformed.Items[3].Set = (Subject)900; malformed.Items[3].Slot = (ArtifactSlot)900;
        malformed.Items[3].MainStat = (ArtifactStat)900; malformed.Items[3].Rarity = 900; malformed.Items[3].Level = 900;
        malformed.Items[3].EquippedTo = "unknown";
        ArtifactRules.Normalize(malformed);
        Check(malformed.Items.Count == 2 && malformed.ClaimedKeys.Count == 1 && malformed.Dust == 0, "malformed save deduplicates items and receipt keys");
        Check(malformed.Items[1].Rarity == 4 && malformed.Items[1].Level == 12 && malformed.Items[1].EquippedTo == null, "malformed enum and progression values clamped");
        var noPetBonus = ArtifactRules.Bonus(malformed, null);
        Check(noPetBonus.HP == 0 && noPetBonus.Attack == 0, "unequipped inventory never grants a null-pet combat bonus");
        collection.Owned.Add(new CompanionProgress { SpeciesId = "unknown" });
        Check(!ArtifactRules.Equip(malformed, "invalid", "unknown", collection), "malformed save cannot equip to unknown species");
        var emptySave = new ArtifactInventory { Items = null };
        Check(!ArtifactRules.Salvage(emptySave, "missing"), "salvage handles incomplete saved inventory");
        var boundedSubstat = new ArtifactInventory { Dust = 100 };
        var capItem = Item("substat-cap", ArtifactSlot.Core, 4); capItem.Level = 2; capItem.Substats[0].Value = 12; boundedSubstat.Items.Add(capItem);
        Check(ArtifactRules.Upgrade(boundedSubstat, capItem.Id) && capItem.Substats[0].Value <= 12, "upgrade never leaves a substat above the cap");
        Console.WriteLine("Artifact harness: " + assertions + " assertions passed.");
    }
}
