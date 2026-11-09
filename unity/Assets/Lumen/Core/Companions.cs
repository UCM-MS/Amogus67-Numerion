using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumen.Core
{
    [Serializable]
    public sealed class CompanionProgress
    {
        public string SpeciesId;
        public string Nickname;
        public int XP;
        public int Bond;
        public int Gear;
        public int Level => 1 + Math.Max(0, XP) / 100;
    }

    [Serializable]
    public sealed class CompanionCollection
    {
        public List<CompanionProgress> Owned = new List<CompanionProgress>();
        public string EquippedId;
        public int Eggs;
        public int CorrectTowardsEgg;
        public int HatchSequence;
        public bool StarterClaimed;
        public CompanionProgress Equipped => Owned?.FirstOrDefault(p => p.SpeciesId == EquippedId);
    }

    public sealed class CompanionSpecies
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string Specialty { get; }
        public int Visual { get; }
        public int BattleAffinity { get; }
        public CompanionSpecies(string id, string name, string description, string specialty, int visual, int battleAffinity = 0)
        { Id = id; Name = name; Description = description; Specialty = specialty; Visual = visual; BattleAffinity = battleAffinity; }
    }

    /// <summary>Deterministic collection rules: a first collection completes before duplicate eggs award training XP.</summary>
    public static class CompanionRules
    {
        public const int EggCost = 200;
        public const int TrainingCost = 75;
        public static readonly CompanionSpecies[] Species = {
            new CompanionSpecies("ember", "Iskra", "Zvedavá líška s huňatým chvostom. Každý nový chodník je pre ňu dobrodružstvo.", "Slovné úlohy a matematika v praxi", 1, 0),
            new CompanionSpecies("bolt", "Volt", "Malý výskumný robot so svietiacou anténou. Miluje techniku a logické výzvy.", "Výrazy a rovnice", 2, 1),
            new CompanionSpecies("astra", "Astra", "Nočná sova s mäkkými krídlami. Do denníka si zapisuje každú novú vedomosť.", "Geometria a priestor", 3, 2),
            new CompanionSpecies("zlomko", "Zlomko", "Veselý slizový spoločník s deleným kryštálom. Z častí vždy zloží celok.", "Zlomky a racionálne čísla", 4, 0),
            new CompanionSpecies("percenta", "Percenta", "Malá dračica s mincovými šupinami. Každú výpravu si prepočíta na sto dielikov.", "Percentá a finančné úlohy", 5, 1),
            new CompanionSpecies("rovnik", "Rovnik", "Rohatý strážca rovnováhy. Na oboch stranách svojej váhy udržuje rovnakú silu.", "Lineárne rovnice a nerovnice", 6, 1),
            new CompanionSpecies("geora", "Geora", "Krištáľová korytnačka s trojuholníkovým pancierom. Pozná najkratšie chodníky akadémie.", "Uhly a Pytagorova veta", 7, 2),
            new CompanionSpecies("prizma", "Prizma", "Lietajúci kryštálový mantik s hranatými krídlami. V každom telese objaví nový priestor.", "Telesá, objem a povrch", 8, 2),
            new CompanionSpecies("datax", "Datax", "Zvedavý mechanický chrobák. Zbiera čísla, kreslí grafy a hľadá medzi nimi súvislosti.", "Štatistika a pravdepodobnosť", 9, 0),
            new CompanionSpecies("ratio", "Ratio", "Rýchly zajačí kartograf s kompasom. Každú trasu zmenší do dokonalej mierky.", "Pomer, mierka a úmernosť", 10, 0)
        };
        public static CompanionSpecies Find(string id) => Species.FirstOrDefault(p => p.Id == id);
        public static void Normalize(CompanionCollection collection)
        {
            collection.Owned ??= new List<CompanionProgress>();
            collection.Owned = collection.Owned.Where(p => p != null && Find(p.SpeciesId) != null)
                .GroupBy(p => p.SpeciesId).Select(g => g.First()).ToList();
            foreach (var pet in collection.Owned)
            {
                pet.XP = Math.Clamp(pet.XP, 0, 1000000); pet.Bond = Math.Clamp(pet.Bond, 0, 10000);
                pet.Nickname = string.IsNullOrWhiteSpace(pet.Nickname) ? Find(pet.SpeciesId).Name : ProfileRules.CleanName(pet.Nickname);
                pet.Gear = Math.Clamp(pet.Gear, 0, pet.Level >= 5 ? 2 : pet.Level >= 3 ? 1 : 0);
            }
            collection.Eggs = Math.Clamp(collection.Eggs, 0, 999); collection.CorrectTowardsEgg = Math.Clamp(collection.CorrectTowardsEgg, 0, 2);
            collection.HatchSequence = Math.Clamp(collection.HatchSequence, 0, 1000000);
            if (collection.Owned.Count > 0) collection.StarterClaimed = true;
            if (collection.Equipped == null) collection.EquippedId = null;
        }
        public static CompanionProgress ClaimStarter(CompanionCollection collection, string speciesId)
        {
            Normalize(collection);
            if (collection.StarterClaimed || Find(speciesId) == null) return null;
            var pet = NewPet(speciesId); collection.Owned.Add(pet); collection.StarterClaimed = true; return pet;
        }
        public static bool Equip(CompanionCollection collection, string speciesId)
        {
            if (!collection.Owned.Any(p => p.SpeciesId == speciesId)) return false;
            collection.EquippedId = speciesId; return true;
        }
        public static CompanionProgress Hatch(CompanionCollection collection, out bool duplicate)
        {
            Normalize(collection); duplicate = false;
            if (collection.Eggs <= 0 || !collection.StarterClaimed) return null;
            var species = Species.FirstOrDefault(s => !collection.Owned.Any(p => p.SpeciesId == s.Id))
                ?? Species[collection.HatchSequence % Species.Length];
            collection.Eggs--; collection.HatchSequence++;
            var pet = collection.Owned.FirstOrDefault(p => p.SpeciesId == species.Id);
            duplicate = pet != null;
            if (pet == null) { pet = NewPet(species.Id); collection.Owned.Add(pet); }
            else pet.XP = Math.Min(1000000, pet.XP + 120);
            return pet;
        }
        public static bool RewardCorrectAnswer(CompanionCollection collection, int petXP)
        {
            if (collection.Equipped != null) { collection.Equipped.XP = Math.Min(1000000, collection.Equipped.XP + Math.Max(0, petXP)); collection.Equipped.Bond = Math.Min(10000, collection.Equipped.Bond + 1); }
            collection.CorrectTowardsEgg++;
            if (collection.CorrectTowardsEgg < 3) return false;
            collection.CorrectTowardsEgg = 0; collection.Eggs = Math.Min(999, collection.Eggs + 1); return true;
        }
        private static CompanionProgress NewPet(string id) => new CompanionProgress { SpeciesId = id, Nickname = Find(id).Name };
    }

    [Serializable]
    public sealed class AcademyTutorial
    {
        public bool Active;
        public bool Walked;
        public bool Jumped;
        public bool OpenedQuiz;
        public bool AnsweredCorrectly;
        public bool ClaimedPet;
        public bool EquippedPet;
        public bool RewardClaimed;
        public float Distance;
        public int Step => !Walked ? 0 : !Jumped ? 1 : !OpenedQuiz ? 2 : !AnsweredCorrectly ? 3 : !ClaimedPet ? 4 : !EquippedPet ? 5 : 6;
        public bool Complete => Step == 6;
        public static readonly string[] Titles = { "Rozhýb svoj svet", "Vyskoč k objavom", "Prvá výskumná výzva", "Vedomosti sú tvoja sila", "Vyber si parťáka", "Spoločne na výpravu", "Vitaj v akadémii!" };
        public static readonly string[] Instructions = {
            "Prejdi 8 metrov. WASD alebo šípky ovládajú pohyb; pravé tlačidlo myši otáča kameru.",
            "Stlač medzerník a vyskoč. Na ovládači použi A; v PC VR tiež A.",
            "Otvor mapu klávesom M a vyber sekciu. Pri kiosku môžeš použiť E.",
            "Odpovedz správne na jednu otázku. Každá odpoveď má vysvetlenie, takže sa môžeš poučiť aj z chyby.",
            "Otvor Spoločníkov a vyber si prvého parťáka zadarmo. Každý má vlastný vzhľad a postup.",
            "V zbierke stlač Vziať na výpravu. Parťák ťa bude sprevádzať a získavať skúsenosti.",
            "Zvládol si pohyb, vedomosti aj spoločníkov. Začni objavovať šesť ostrovov." };
    }
}
