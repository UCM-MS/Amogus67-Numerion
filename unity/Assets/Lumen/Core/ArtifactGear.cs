using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumen.Core
{
    public enum ArtifactSlot { Charm, Crown, Core, Band }
    public enum ArtifactStat { Vitality, Power, Protection, Insight }
    [Serializable] public sealed class ArtifactSubstat { public ArtifactStat Stat; public int Value; }
    [Serializable] public sealed class ArtifactItem
    {
        public string Id, EquippedTo;
        public Subject Set;
        public ArtifactSlot Slot;
        public ArtifactStat MainStat;
        public int Rarity = 1, Level, MainValue;
        public ArtifactSubstat[] Substats = Array.Empty<ArtifactSubstat>();
    }
    [Serializable] public sealed class ArtifactInventory
    {
        public List<ArtifactItem> Items = new List<ArtifactItem>();
        public List<string> ClaimedKeys = new List<string>();
        public int DropSequence, PityEpic, PityLegendary, Dust;
    }
    public sealed class ArtifactDrop { public bool Awarded; public ArtifactItem Item; public int Dust; public string Message = ""; }
    public sealed class ArtifactBonus { public int HP, Attack, SetPieces; public string Description = ""; }

    /// <summary>Original learning-earned loot. No money, purchase currency or paid random draws.</summary>
    public static class ArtifactRules
    {
        public const int MaximumItems = 200;
        public static string RarityName(int rarity) => new[] { "", "Bežný", "Vzácny", "Epický", "Legendárny" }[Math.Clamp(rarity, 1, 4)];
        public static string SlotName(ArtifactSlot slot) => new[] { "Talizman", "Čelenka", "Jadro", "Náramok" }[(int)slot];
        public static string StatName(ArtifactStat stat) => new[] { "Vitalita", "Sila", "Ochrana", "Poznanie" }[(int)stat];
        public static string SetName(Subject set) => new[] { "Zlomkový prieskumník", "Majster pomeru", "Riešiteľ rovníc", "Strážca geometrie", "Pozorovateľ dát", "Praktický objaviteľ" }[Math.Clamp((int)set, 0, 5)];
        public static string Name(ArtifactItem item) => SlotName(item.Slot) + " · " + SetName(item.Set);
        public static void Normalize(ArtifactInventory inventory)
        {
            inventory.Items ??= new List<ArtifactItem>(); inventory.ClaimedKeys ??= new List<string>();
            inventory.Items = inventory.Items.Where(i => i != null && !string.IsNullOrWhiteSpace(i.Id)).GroupBy(i => i.Id).Select(g => g.First()).Take(MaximumItems).ToList();
            inventory.ClaimedKeys = inventory.ClaimedKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
            inventory.DropSequence = Math.Clamp(inventory.DropSequence, 0, 1000000);
            inventory.Dust = Math.Clamp(inventory.Dust, 0, 1000000);
            inventory.PityEpic = Math.Clamp(inventory.PityEpic, 0, 11); inventory.PityLegendary = Math.Clamp(inventory.PityLegendary, 0, 39);
            var slots = new HashSet<string>();
            foreach (var i in inventory.Items)
            {
                i.Set = (Subject)Math.Clamp((int)i.Set, 0, 5); i.Slot = (ArtifactSlot)Math.Clamp((int)i.Slot, 0, 3);
                i.MainStat = (ArtifactStat)Math.Clamp((int)i.MainStat, 0, 3); i.Rarity = Math.Clamp(i.Rarity, 1, 4); i.Level = Math.Clamp(i.Level, 0, i.Rarity * 3);
                i.MainValue = 2 * i.Rarity + i.Level;
                i.Substats = (i.Substats ?? Array.Empty<ArtifactSubstat>()).Where(s => s != null && (int)s.Stat >= 0 && (int)s.Stat < 4 && s.Stat != i.MainStat)
                    .GroupBy(s => s.Stat).Select(g => g.First()).Take(3).ToArray();
                foreach (var s in i.Substats) s.Value = Math.Clamp(s.Value, 1, 12);
                if (CompanionRules.Find(i.EquippedTo) == null || !slots.Add(i.EquippedTo + ":" + i.Slot)) i.EquippedTo = null;
            }
        }
        public static ArtifactDrop RewardCorrect(ArtifactInventory inventory, string receipt, Subject topic, int grade)
        {
            Normalize(inventory);
            if (string.IsNullOrWhiteSpace(receipt) || receipt.Length > 200 || inventory.ClaimedKeys.Contains(receipt)) return new ArtifactDrop();
            inventory.ClaimedKeys.Add(receipt); inventory.DropSequence++; inventory.PityEpic++; inventory.PityLegendary++; inventory.Dust += 2;
            uint seed = Hash(receipt + ":" + grade); int roll = (int)(seed % 100);
            int rarity = inventory.PityLegendary >= 40 || roll == 0 ? 4 : inventory.PityEpic >= 12 || roll < 8 ? 3 : roll < 32 ? 2 : 1;
            if (rarity >= 3) inventory.PityEpic = 0; if (rarity == 4) inventory.PityLegendary = 0;
            var item = new ArtifactItem { Id = "artifact-" + seed.ToString("x8") + "-" + inventory.DropSequence,
                Set = (Subject)Math.Clamp((int)topic, 0, 5), Slot = (ArtifactSlot)((seed >> 9) % 4), Rarity = rarity,
                MainStat = (ArtifactStat)((seed >> 13) % 4), MainValue = rarity * 2 };
            var stats = Enumerable.Range(0, 4).Select(s => (ArtifactStat)s).Where(s => s != item.MainStat).ToArray();
            item.Substats = Enumerable.Range(0, Math.Min(3, rarity)).Select(s => new ArtifactSubstat { Stat = stats[(s + (int)((seed >> 17) % 3)) % 3], Value = 1 + (int)((seed >> (s * 3)) % (uint)(rarity + 1)) }).ToArray();
            if (inventory.Items.Count >= MaximumItems)
            {
                var scrap = inventory.Items.Where(i => string.IsNullOrEmpty(i.EquippedTo)).OrderBy(i => i.Rarity).ThenBy(i => i.Level).FirstOrDefault();
                if (scrap == null) { inventory.Dust += rarity * 4; return new ArtifactDrop { Awarded = true, Dust = 2 + rarity * 4, Message = "Plná výbava · odmena sa premenila na prach." }; }
                inventory.Items.Remove(scrap); inventory.Dust += scrap.Rarity * 4;
            }
            inventory.Items.Add(item);
            return new ArtifactDrop { Awarded = true, Item = item, Dust = 2, Message = RarityName(rarity) + " artefakt · " + Name(item) };
        }
        public static bool Equip(ArtifactInventory inventory, string itemId, string species, CompanionCollection collection)
        {
            Normalize(inventory); var item = inventory.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null || CompanionRules.Find(species) == null || collection?.Owned == null || !collection.Owned.Any(p => p != null && p.SpeciesId == species)) return false;
            foreach (var i in inventory.Items.Where(i => i.EquippedTo == species && i.Slot == item.Slot)) i.EquippedTo = null;
            item.EquippedTo = species; return true;
        }
        public static bool Upgrade(ArtifactInventory inventory, string id)
        {
            Normalize(inventory); var item = inventory.Items.FirstOrDefault(i => i.Id == id);
            if (item == null || item.Level >= item.Rarity * 3) return false;
            int cost = (item.Level + 1) * 3;
            if (inventory.Dust < cost) return false;
            inventory.Dust -= cost; item.Level++; item.MainValue = item.Rarity * 2 + item.Level;
            if (item.Level % 3 == 0 && item.Substats.Length > 0)
            {
                var substat = item.Substats[(item.Level / 3 - 1) % item.Substats.Length];
                substat.Value = Math.Min(12, substat.Value + 1);
            }
            return true;
        }
        public static bool Salvage(ArtifactInventory inventory, string id)
        {
            Normalize(inventory);
            var item = inventory.Items.FirstOrDefault(i => i.Id == id);
            if (item == null || !string.IsNullOrEmpty(item.EquippedTo)) return false;
            inventory.Items.Remove(item); inventory.Dust += item.Rarity * 4 + item.Level; return true;
        }
        public static ArtifactBonus Bonus(ArtifactInventory inventory, string species)
        {
            Normalize(inventory);
            if (CompanionRules.Find(species) == null) return new ArtifactBonus { Description = "+0 HP · +0 sila v tréningu" };
            var items = inventory.Items.Where(i => i.EquippedTo == species).ToArray();
            int hp = 0, atk = 0;
            foreach (var i in items) {
                Add(i.MainStat, i.MainValue, ref hp, ref atk);
                foreach (var s in i.Substats) Add(s.Stat, s.Value, ref hp, ref atk);
            }
            int set = items.GroupBy(i => i.Set).Select(g => g.Count()).DefaultIfEmpty(0).Max();
            if (set >= 2) hp += 5; if (set >= 4) atk += 2;
            hp = Math.Clamp(hp, 0, 20); atk = Math.Clamp(atk, 0, 3);
            return new ArtifactBonus { HP = hp, Attack = atk, SetPieces = set, Description = "+" + hp + " HP · +" + atk + " sila v tréningu" + (set >= 2 ? " · " + set + "/4 set" : "") };
        }
        private static void Add(ArtifactStat stat, int value, ref int hp, ref int attack)
        { if (stat == ArtifactStat.Vitality || stat == ArtifactStat.Protection) hp += value; else attack += value / 4; }
        private static uint Hash(string value) { unchecked { uint hash = 2166136261; foreach (char c in value) { hash ^= c; hash *= 16777619; } return hash; } }
    }
}
