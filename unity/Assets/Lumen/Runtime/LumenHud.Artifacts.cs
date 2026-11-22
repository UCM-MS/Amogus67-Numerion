using System.Linq;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        private string artifactPet = "";
        private int artifactPage;
        public void ShowArtifacts()
        {
            var inventory = game.Save.Artifacts; ArtifactRules.Normalize(inventory);
            Open("Artefakty vedomostí", "VÝBAVA / ODMENY ZA RIEŠENIA"); FeaturePanel = "ARTIFACTS";
            Label(body, "Každé nové správne riešenie prinesie artefakt a 2 jednotky prachu. Opakovaná odpoveď na tú istú úlohu odmenu nevytvorí. Online kolá majú samostatné odmeny.", 25, Ink);
            Label(body, "Štyri pozície · hlavná vlastnosť a vedľajšie vlastnosti · bonus za 2 a 4 kusy rovnakého setu. Epický alebo lepší najneskôr do 12 odmien, legendárny najneskôr do 40. Žiadne platené žrebovanie.", 24, Muted);
            Label(body, inventory.Items.Count + "/200 artefaktov · " + inventory.Dust + " prachu · epický za najviac " + (12 - inventory.PityEpic) + " · legendárny za najviac " + (40 - inventory.PityLegendary), 25, Purple);
            var owned = game.Save.Companions.Owned;
            if (!owned.Any(p => p.SpeciesId == artifactPet)) artifactPet = owned.FirstOrDefault()?.SpeciesId ?? "";
            foreach (var pet in owned)
            {
                string id = pet.SpeciesId;
                ButtonNode(body, (artifactPet == id ? "✓  " : "") + pet.Nickname + " · " + ArtifactRules.Bonus(inventory, id).Description, () => { artifactPet = id; ShowArtifacts(); }, artifactPet == id ? Mint : Cream, Ink, 24);
            }
            Label(body, "Vitalita a ochrana zvyšujú HP; sila a poznanie pridajú 1 k útoku za každé 4 body vlastnosti. Set: 2 kusy +5 HP, 4 kusy navyše +2 sila. Spolu najviac +20 HP a +3 sila v tréningu. Online súboje používajú vyrovnané štatistiky.", 23, Muted);
            if (inventory.Items.Count == 0) ButtonNode(body, "Získať prvý artefakt · otvoriť učebnice", ShowStudy, Lime);
            var sorted = inventory.Items.OrderByDescending(i => i.EquippedTo == artifactPet && !string.IsNullOrEmpty(artifactPet)).ThenByDescending(i => i.Rarity).ThenByDescending(i => i.Level).ToArray();
            const int perPage = 12; int pages = System.Math.Max(1, (sorted.Length + perPage - 1) / perPage); artifactPage = Mathf.Clamp(artifactPage, 0, pages - 1);
            if (pages > 1) { var row = Row(body, 70); ButtonNode(row, "←", () => { artifactPage--; ShowArtifacts(); }, Lavender); Label(row, (artifactPage + 1) + "/" + pages, 24, Ink); ButtonNode(row, "→", () => { artifactPage++; ShowArtifacts(); }, Lavender); }
            RectTransform artifactRow=null;int artifactIndex=0;
            foreach (var item in sorted.Skip(artifactPage * perPage).Take(perPage))
            {
                if(artifactIndex++%2==0)artifactRow=CardRow(body);
                string id = item.Id; var card = Card(artifactRow, Cream);
                Label(card, ArtifactRules.RarityName(item.Rarity) + " · " + ArtifactRules.Name(item) + "  +" + item.Level, 27, Ink);
                Label(card, ArtifactRules.StatName(item.MainStat) + " +" + item.MainValue + "\n" + string.Join(" · ", item.Substats.Select(s => ArtifactRules.StatName(s.Stat) + " +" + s.Value)), 24, Purple);
                Label(card, string.IsNullOrEmpty(item.EquippedTo) ? "Voľný artefakt" : "Nosí: " + (owned.FirstOrDefault(p => p.SpeciesId == item.EquippedTo)?.Nickname ?? item.EquippedTo), 22, Muted);
                if (!string.IsNullOrEmpty(artifactPet)) ButtonNode(card, item.EquippedTo == artifactPet ? "Odložiť artefakt" : "Nasadiť vybranému spoločníkovi", () => { if (item.EquippedTo == artifactPet) item.EquippedTo = null; else ArtifactRules.Equip(inventory, id, artifactPet, game.Save.Companions); ProfileStore.Save(game.Save); ShowArtifacts(); }, Mint, Ink, 23);
                if (item.Level < item.Rarity * 3) { int cost = (item.Level + 1) * 3; var b = ButtonNode(card, "Vylepšiť · " + cost + " prachu", () => { ArtifactRules.Upgrade(inventory, id); ProfileStore.Save(game.Save); ShowArtifacts(); }, Lavender, Ink, 23); b.interactable = inventory.Dust >= cost; }
                if (string.IsNullOrEmpty(item.EquippedTo)) ButtonNode(card, "Rozobrať na " + (item.Rarity * 4 + item.Level) + " prachu", () => { ArtifactRules.Salvage(inventory, id); ProfileStore.Save(game.Save); ShowArtifacts(); }, Color.white, Muted, 22);
            }
            ButtonNode(body, "Aréna spoločníkov", ShowArena, Lime);
        }
    }
}
