using UnityEngine;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        private int expeditionSelection;

        public void ShowExpeditions()
        {
            if(!game.Save.HasCreatedProfile){ShowProfile(true);return;}
            Open("Výpravy","PORTÁLY / MATEMATICKÉ OSTROVY");FeaturePanel="EXPEDITIONS";
            int active=game.ActiveExpeditionIndex;
            expeditionSelection=Mathf.Clamp(expeditionSelection,0,game.Expeditions.Count-1);
            Label(body,"Vyber oblasť. Na každom ostrove nájdeš model učiva, učebnicu a tematický test.",25,Muted);
            var regions=CardRow(body);
            foreach(var destination in game.Expeditions)
            {
                int index=destination.Index;
                bool selected=index==expeditionSelection;
                var card=Card(regions,selected?Selected:Cream);
                Label(card,(index+1).ToString("00")+"  /  "+(active==index?"SI V OBLASTI":"PORTÁL PRIPRAVENÝ"),20,Muted);
                Label(card,destination.Name,30,Ink);
                Label(card,destination.Description,23,Muted);
                Rule(card,Frame);
                Label(card,"CIEĽ VÝPRAVY",20,Muted);
                Label(card,destination.Objective,24,Ink);
                var mastery=game.Learning.GetMastery(game.Profile,destination.Subject);
                Label(card,"Zvládnutie témy "+mastery.Score+" %",23,Muted);
                ProgressBar(card,mastery.Score/100f,Purple);
                ButtonNode(card,selected?"✓  VYBRANÁ OBLASŤ":"Vybrať oblasť",()=>{expeditionSelection=index;ShowExpeditions();},selected?Selected:Lavender,Ink,24);
            }
            var chosen=game.Expeditions[expeditionSelection];
            var actions=Row(body,72);
            ButtonNode(actions,active==chosen.Index?"Pokračovať v oblasti  →":"Vstúpiť: "+chosen.Name+"  →",()=>{if(game.ActiveExpeditionIndex==chosen.Index)CloseModal();else game.StartExpedition(chosen.Index);},Lime);
            if(active>=0)ButtonNode(actions,"Návrat do akadémie",game.ReturnFromExpedition,Lavender);
            else ButtonNode(actions,"Späť do akadémie",CloseModal,Lavender);
            Label(body,"Učivo sa riadi tvojím ročníkom. Výsledky testov a získané odmeny zostávajú uložené aj po návrate.",22,Muted);
        }
    }
}
