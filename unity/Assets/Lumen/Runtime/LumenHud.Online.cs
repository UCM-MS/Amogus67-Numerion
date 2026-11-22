using System;
using System.Linq;
using Lumen.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        private bool revealCloudCode;
        private OnlineAcademyClient observedOnline;
        private Text onlinePanelStatus;

        private void ObserveOnlineStatus()
        {
            if (game.Online == null || observedOnline == game.Online) return;
            observedOnline = game.Online;
            observedOnline.StatusChanged += message => {
                if (this == null || !ModalOpen) return;
                if (FeaturePanel == "ONLINE" || FeaturePanel == "CLOUD") Put(onlinePanelStatus, message);
                if (FeaturePanel == "CLOUD") ShowCloud();
                else if (FeaturePanel == "ONLINE" && !observedOnline.Busy) ShowMultiplayer();
            };
        }

        public void ShowMultiplayer()
        {
            if (!game.Save.HasCreatedProfile) { ShowProfile(true); return; }
            Open("Spoločná matematická akadémia", "HRAŤ SPOLU / PC + WEB");
            FeaturePanel = "ONLINE"; ObserveOnlineStatus();
            var online = game.Online;
            if (online == null) { Label(body, "Online služba sa ešte pripravuje. Môžeš pokračovať v sólo svete.", 26, Ink); return; }
            onlinePanelStatus = Label(body, online.Status, 24, Purple);
            Label(body, "Vytvor miestnosť a pošli šesťmiestny kód kamarátom. Rovnaká miestnosť funguje v aplikácii aj v prehliadači.", 25, Muted);

            if (!game.SharedConnected)
            {
                var create = ButtonNode(body, "Vytvoriť spoločný svet  →", () => {
                    if (game.Connected) game.Disconnect();
                    online.CreateRoom();
                }, Lime);
                create.interactable = !online.Busy;
                var join = Card(body, Lavender);
                var roomCode = Input(join, "Kód miestnosti od kamaráta", "", false, 6);
                roomCode.onValueChanged.AddListener(value => { string upper = value.ToUpperInvariant(); if (upper != value) roomCode.SetTextWithoutNotify(upper); });
                var joinButton = ButtonNode(join, "Pripojiť sa kódom", () => {
                    if (game.Connected) game.Disconnect();
                    online.JoinRoom(roomCode.text);
                }, Mint);
                joinButton.interactable = !online.Busy;

                var server = Card(body, Cream);
                Label(server, "Server spoločného sveta", 26, Ink);
                Label(server, "Pri lokálnom spustení musí aplikácia používať rovnakú adresu servera ako prehliadač. Profil a postup sú pre každý server oddelené.", 22, Muted);
                var address = Input(server, "Adresa servera", online.BaseUrl, false, 220);
                ButtonNode(server, "Použiť túto adresu", () => { online.ConfigureOrigin(address.text); ShowMultiplayer(); }, Lavender, Ink, 23);
            }
            else
            {
                var room = online.Room;
                var info = Card(body, Mint);
                Label(info, "MIESTNOSŤ  " + room.code, 42, Ink);
                Label(info, room.players.Length + "/16 hráčov  ·  tímové majáky " + CountBeacons(room.beaconsMask) + "/6", 25, Purple);
                var code = Input(info, "Kód na pozvanie", room.code, false, 6); code.readOnly = true;
                ButtonNode(info, "Vybrať celý kód · potom Ctrl+C", () => SelectWhole(code), Cream, Ink, 22);
                ButtonNode(info, "Späť do spoločného sveta", CloseModal, Lime);
                ShowOnlineInvitation(room.battle);

                Label(body, "OBJAVITELIA V MIESTNOSTI", 28, Purple);
                foreach (var peer in room.players)
                {
                    bool self = peer.id == online.IdentityId;
                    var card = Card(body, self ? Lavender : Cream);
                    Label(card, peer.name + (self ? "  ·  TY" : ""), 28, Ink);
                    string petName = CompanionRules.Find(peer.pet)?.Name ?? "Spoločník ešte nevybraný";
                    Label(card, peer.grade + ". ročník  ·  " + petName, 23, Muted);
                    if (self) continue;
                    string targetId = peer.id;
                    var challenge = ButtonNode(card, string.IsNullOrEmpty(peer.battleId) ? "Vyzvať na pet duel" : "Hráč má rozbehnutý súboj", () => online.Challenge(targetId), Mint, Ink, 24);
                    challenge.interactable = !online.Busy && string.IsNullOrEmpty(peer.battleId) &&
                        game.Save.Companions.Owned.Count > 0 && (room.battle == null || room.battle.status != "active" && room.battle.status != "challenge");
                }
                if (room.players.Length < 2) Label(body, "Zatiaľ si tu sám. Pošli kód kamarátovi; môže hrať na PC alebo vo webovej verzii.", 24, Muted);
                ButtonNode(body, "Odísť z miestnosti a pokračovať sólo", () => { online.LeaveRoom(); ShowMultiplayer(); }, Lavender);
            }
            if (game.Save.Companions.Owned.Count == 0) MenuCard("Vybrať prvého spoločníka", "Prvý pet je zadarmo. Potom ho môžeš vziať do súboja.", ShowPets, Lavender);
            ButtonNode(body, "Spoločný profil a prenos postupu", ShowCloud, Cream, Ink, 24);
            ButtonNode(body, "OG · pôvodná webová verzia", () => Application.OpenURL(OnlineAcademyClient.ProductionOrigin + "/classic"), Cream, Muted, 22);
        }

        private void ShowOnlineInvitation(OnlineBattle battle)
        {
            if (battle == null || battle.status != "challenge" && battle.status != "active") return;
            var invitation = Card(body, Lavender);
            if (battle.status == "active")
            {
                Label(invitation, "Tvoj pet duel pokračuje", 28, Purple);
                ButtonNode(invitation, "Otvoriť arénu", () => game.ViewOnlineBattle(), Lime);
                return;
            }
            string id = battle.id;
            bool incoming = battle.side == 1;
            string otherId = incoming ? battle.challengerId : battle.opponentId;
            string opponent = game.Online.Room.players.FirstOrDefault(p => p.id == otherId)?.name ?? "Objaviteľ";
            Label(invitation, incoming ? opponent + " ťa vyzýva na pet duel!" : "Čakáme na prijatie: " + opponent, 28, Ink);
            Label(invitation, "Peti majú vo férovom online súboji rovnaký základ. Silné ťahy aktivuje matematika; rýchly výpad môžeš použiť bez otázky.", 23, Muted);
            if (incoming) ButtonNode(invitation, "Prijať výzvu", () => game.Online.AcceptBattle(id), Lime);
            ButtonNode(invitation, incoming ? "Odmietnuť výzvu" : "Zrušiť výzvu", () => game.Online.DeclineBattle(id), Cream, Ink, 23);
        }

        public void ShowCloud()
        {
            if (!game.Save.HasCreatedProfile) { ShowProfile(true); return; }
            if (FeaturePanel != "CLOUD") revealCloudCode = false;
            Open("Tvoja výprava na každom zariadení", "SPOLOČNÝ PROFIL / PC + WEB");
            FeaturePanel = "CLOUD"; ObserveOnlineStatus();
            var online = game.Online;
            if (online == null) { Label(body, "Cloudový profil sa pripravuje. Lokálne uloženie zostáva dostupné.", 26, Ink); return; }
            onlinePanelStatus = Label(body, online.Status, 24, Purple);
            var current = Card(body, Lavender);
            Label(current, game.Profile.DisplayName + " · " + CurriculumLabels.Grade(game.Profile.Grade), 30, Ink);
            Label(current, "Úroveň " + game.Profile.Progress.Level + "  ·  " + game.Save.Companions.Owned.Count + "/" + CompanionRules.Species.Length + " spoločníkov  ·  " + game.Profile.Progress.Coins + " mincí", 24, Muted);
            Label(current, online.IdentityId.Length == 0 ? "Tento lokálny profil ešte nemá cloudový kód." : "Cloudová verzia: " + online.ProfileRevision, 24, Purple);
            Label(current, "Server: " + online.BaseUrl, 21, Muted);
            var saveCloud = ButtonNode(current, online.IdentityId.Length == 0 ? "Vytvoriť cloudový profil a uložiť postup" : "Uložiť aktuálny postup do cloudu", () => online.SaveCloud(), Lime);
            saveCloud.interactable = !online.Busy && !online.CloudConflict;
            if (online.CloudConflict)
            {
                var conflict = Card(body, Mint);
                Label(conflict, "Na druhom zariadení je novší postup", 28, Ink);
                Label(conflict, "Aktuálny cloud sme neprepísali. Najprv načítaj novšiu verziu; tvoj terajší lokálny postup sa pred načítaním odloží do zálohy.", 24, Muted);
            }
            if (online.IdentityId.Length > 0)
            {
                var cloud = ButtonNode(current, "Načítať cloud · najprv zálohovať lokálny postup", () => online.LoadCloud(), Lavender, Ink, 23);
                cloud.interactable = !game.SharedConnected && !online.Busy;
                ButtonNode(current, revealCloudCode ? "Skryť súkromný prenosový kód" : "Zobraziť súkromný prenosový kód", () => { revealCloudCode = !revealCloudCode; ShowCloud(); }, Cream, Ink, 23);
                if (revealCloudCode)
                {
                    Label(current, "Tento kód sprístupňuje tvoj herný postup. Použi ho iba na svojom druhom zariadení.", 22, Purple);
                    var code = Input(current, "Súkromný kód profilu", online.PairingCode, false, 120); code.readOnly = true;
                    ButtonNode(current, "Vybrať celý kód · potom Ctrl+C", () => SelectWhole(code), Cream, Ink, 22);
                }
            }
            var pair = Card(body, Cream);
            Label(pair, "Pokračovať s profilom z druhého zariadenia", 28, Ink);
            Label(pair, "Na prvom zariadení ulož postup a zobraz súkromný kód. Tu vlož celý kód. Pred načítaním sa terajší lokálny profil zálohuje.", 24, Muted);
            var pairingCode = Input(pair, "Kód z druhého zariadenia", "", false, 120);
            pairingCode.contentType = InputField.ContentType.Password;
            var link = ButtonNode(pair, "Prepojiť a načítať môj profil", () => online.PairProfile(pairingCode.text), Mint);
            link.interactable = !game.SharedConnected && !online.Busy;
            if (game.SharedConnected) Label(pair, "Pred načítaním alebo prepojením profilu odíď zo spoločnej miestnosti.", 23, Purple);
            if (online.HasLocalBackup)
            {
                var backup = ButtonNode(body, "Obnoviť lokálnu zálohu pred posledným načítaním", () => online.RestoreLocalBackup(), Cream, Ink, 23);
                backup.interactable = !game.SharedConnected;
                Label(body, "Obnovenie zálohy zmení iba lokálny postup. Cloud sa zmení až ďalším ručným uložením.", 22, Muted);
            }
            ButtonNode(body, "Hrať spolu", ShowMultiplayer, Lavender);
            ButtonNode(body, "Späť do akadémie", CloseModal, Cream);
        }

        private static int CountBeacons(int mask)
        { int count = 0; for (int i = 0; i < 6; i++) if ((mask & (1 << i)) != 0) count++; return count; }
        private static void SelectWhole(InputField input)
        { input.Select(); input.ActivateInputField(); input.MoveTextStart(false); input.MoveTextEnd(true); }
    }
}
