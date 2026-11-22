using System;
using System.Collections.Generic;
using System.Linq;
using Lumen.Core;
using Lumen.World;
using UnityEngine;

namespace Lumen.Runtime
{
    public sealed partial class LumenGame
    {
        public OnlineAcademyClient Online { get; private set; }
        public bool SharedConnected => Online != null && Online.InRoom;
        public Subject BattleTopic = Subject.Mathematics;
        public Question BattleQuestion { get; private set; }
        public PetBattleActionKind PendingBattleMove { get; private set; }
        public int PendingBattleSlot { get; private set; }
        public bool BattleQuestionOpen { get; private set; }
        public string BattleExplanation { get; private set; } = "";
        private bool preferPractice;
        public PetBattleState BattleState => SaveNormalization.NormalizeBattleState(IsOnlineBattle ? Online.Battle.state : Save.PracticeBattle);
        public bool IsOnlineBattle => !preferPractice && Online?.Battle?.state != null && (Online.Battle.status == "active" || Online.Battle.status == "finished");
        public int BattleSide => IsOnlineBattle ? Online.Battle.side : 0;
        private readonly Dictionary<string, AvatarVisual> sharedAvatars = new Dictionary<string, AvatarVisual>();
        private readonly Dictionary<string, OnlinePeer> sharedTargets = new Dictionary<string, OnlinePeer>();
        private readonly Dictionary<string, string> sharedLooks = new Dictionary<string, string>();
        private string sharedRoster = "";
        private float pendingRespawnUntil;
        private Vector3 pendingRespawnPosition;
        private int questionRound;

        private void InitializeSharedAcademy()
        {
            Save.Artifacts ??= new ArtifactInventory(); Save.BattleParty ??= new List<string>(); ArtifactRules.Normalize(Save.Artifacts);
            Online = gameObject.AddComponent<OnlineAcademyClient>(); Online.Initialize(Presence, () => Save);
            Online.StatusChanged += message => {
                Notify(message);
                if (!Online.Busy) {
                    if (Hud.FeaturePanel == "CLOUD") Hud.ShowCloud();
                    else if (Hud.FeaturePanel == "BATTLE") Hud.ShowBattle();
                }
            };
            Online.RoomChanged += ReceiveSharedRoom; Online.BattleChanged += ReceiveSharedBattle;
            Online.RoomMessagesChanged += ReceiveRoomChat;
            Online.ProfileLoaded += loaded => {
                ProfileStore.Save(Save); // Keep the previous local version recoverable in the normal backup.
                int frames = Save.FrameLimit, textures = Save.TextureSize; bool vsync = Save.VSync; var deviceOptions = Save.Options;
                Save = loaded; ProfileRules.Normalize(Profile);
                SaveNormalization.Normalize(Save);
                Save.Companions ??= new CompanionCollection(); Save.Artifacts ??= new ArtifactInventory();
                Save.Collected ??= new List<string>(); Save.Purchases ??= new List<string>(); Save.BattleParty ??= new List<string>(); Save.Tutorial ??= new AcademyTutorial();
                CompanionRules.Normalize(Save.Companions); ArtifactRules.Normalize(Save.Artifacts);
                Save.FrameLimit = frames; Save.TextureSize = textures; Save.VSync = vsync; Save.Options = deviceOptions ?? new GameOptions(); ApplyUserOptions();
                offlineAvatar.SetName(Profile.DisplayName); RefreshCompanion(); ProfileStore.Save(Save);
                foreach (var item in world.Collectibles.Where(i => !i.IsBeacon)) item.SetCollected(Save.Collected.Contains(item.Id));
                if (Hud.FeaturePanel == "CLOUD") Hud.ShowCloud();
            };
        }
        public string[] BattleParty()
        {
            var owned = Save.Companions.Owned;
            var selected = Save.BattleParty.Where(id => owned.Any(p => p.SpeciesId == id)).Distinct().Take(3).ToArray();
            if (selected.Length > 0) return selected;
            return owned.OrderByDescending(p => p.SpeciesId == Save.Companions.EquippedId).Take(3).Select(p => p.SpeciesId).ToArray();
        }
        public void ToggleBattleParty(string id)
        {
            if (Online?.Battle?.status == "active") { Notify("Zostavu zmeníš po skončení súboja."); return; }
            if (!Save.Companions.Owned.Any(p => p.SpeciesId == id)) return;
            if (Save.BattleParty.Count == 0) Save.BattleParty = BattleParty().ToList();
            if (Save.BattleParty.Contains(id)) { if (Save.BattleParty.Count > 1) Save.BattleParty.Remove(id); }
            else if (Save.BattleParty.Count < 3) Save.BattleParty.Add(id);
            else { Notify("V aréne môžeš mať najviac troch spoločníkov."); return; }
            ProfileStore.Save(Save); Hud.ShowArena();
        }
        private OnlinePresence Presence()
        {
            var a = Profile.Appearance; var p = PlayerPosition;
            return new OnlinePresence { name = Profile.DisplayName, skin = a.SkinTone, outfit = a.OutfitColor, hair = a.HairStyle,
                accessory = a.Accessory, pet = Save.Companions.EquippedId ?? "", petGear = Save.Companions.Equipped?.Gear ?? 0,
                grade = Profile.Grade, x = p.x, y = p.y, z = p.z, yaw = offline != null ? offline.transform.eulerAngles.y : yaw, party = BattleParty() };
        }
        private void ReceiveSharedRoom(OnlineRoom room)
        {
            ResetRoomChatContext(room);
            sharedTargets.Clear();
            if (room != null) foreach (var peer in room.players ?? Array.Empty<OnlinePeer>()) {
                if (peer.id == Online.IdentityId) {
                    Vector3 approved = new Vector3(peer.x, peer.y, peer.z);
                    if (Time.unscaledTime < pendingRespawnUntil) {
                        if (Vector3.Distance(approved, pendingRespawnPosition) < 2) pendingRespawnUntil = 0;
                    } else if (!Connected && Vector3.Distance(PlayerPosition, approved) > 2.5f) TeleportOffline(approved);
                    continue;
                }
                sharedTargets[peer.id] = peer;
                if (!sharedAvatars.TryGetValue(peer.id, out var avatar)) {
                    avatar = AvatarFactory.Create(peer.name); avatar.transform.position = new Vector3(peer.x, peer.y, peer.z);
                    sharedAvatars[peer.id] = avatar;
                }
                string look = peer.name + ":" + peer.skin + ":" + peer.outfit + ":" + peer.hair + ":" + peer.accessory + ":" + peer.pet + ":" + peer.petGear;
                if (!sharedLooks.TryGetValue(peer.id, out var previous) || previous != look) {
                    var species = CompanionRules.Find(peer.pet);
                    avatar.SetName(peer.name); avatar.SetNameVisible(Save.Options.ShowNames); avatar.ApplyAppearance(new AvatarAppearance { SkinIndex = peer.skin, ColorIndex = peer.outfit,
                        Hair = peer.hair, Hat = peer.accessory == 3 ? 2 : peer.accessory == 1 ? 1 : 0, Backpack = peer.accessory == 2 ? 1 : 0,
                        Pet = species?.Visual ?? 0, PetGear = peer.petGear }); sharedLooks[peer.id] = look;
                }
            }
            foreach (string id in sharedAvatars.Keys.Where(id => !sharedTargets.ContainsKey(id)).ToArray()) {
                if (sharedAvatars[id] != null) Destroy(sharedAvatars[id].gameObject); sharedAvatars.Remove(id); sharedLooks.Remove(id);
            }
            string roster = room == null ? "" : string.Join("|", room.players.Select(p => p.id));
            if (roster != sharedRoster) { sharedRoster = roster; if (Hud.FeaturePanel == "ONLINE") Hud.ShowMultiplayer(); }
        }
        private void UpdateSharedAcademy(float dt)
        {
            foreach (var pair in sharedAvatars) if (pair.Value != null && sharedTargets.TryGetValue(pair.Key, out var target)) {
                Vector3 goal = new Vector3(target.x, target.y, target.z); bool moving = (goal - pair.Value.transform.position).sqrMagnitude > .0025f;
                pair.Value.transform.position = Vector3.Lerp(pair.Value.transform.position, goal, 1 - Mathf.Exp(-12 * dt));
                pair.Value.transform.rotation = Quaternion.Slerp(pair.Value.transform.rotation, Quaternion.Euler(0, target.yaw, 0), 1 - Mathf.Exp(-12 * dt));
                if (!PhotoMode) pair.Value.Tick(dt, moving);
            }
        }
        private void ReceiveSharedBattle(OnlineBattle battle)
        {
            if (battle != null) {
                if (battle.status == "active" || battle.status == "challenge") preferPractice = false;
                foreach (var drop in battle.artifactRewards ?? Array.Empty<OnlineArtifactReward>()) GrantArtifact(drop.id, (Subject)Math.Clamp(drop.topic, 0, 5), drop.grade);
                if (!string.IsNullOrEmpty(battle.rewardId)) ApplyProgressReward(battle.rewardId, battle.rewardXP, battle.rewardCoins, battle.rewardPetXP);
                if (battle.status == "challenge" && battle.opponentId == Online.IdentityId) Notify("Nová výzva do arény. Otvor Arénu spoločníkov a prijmi súboj.");
                if (!string.IsNullOrEmpty(battle.explanation)) BattleExplanation = battle.explanation;
            }
            bool keepQuestion = BattleQuestionOpen && battle?.status == "active" && battle.round == questionRound
                && battle.question?.id == BattleQuestion?.Id && !(battle.submitted?.ElementAtOrDefault(battle.side) ?? false);
            if (!keepQuestion) BattleQuestionOpen = false;
            if (Hud.FeaturePanel == "BATTLE" || Hud.FeaturePanel == "ARENA") Hud.ShowBattle();
            else if (Hud.FeaturePanel == "ONLINE") Hud.ShowMultiplayer();
        }
        public void RequestSharedRespawn(string destination, int checkpoint, Vector3 position)
        {
            if (SharedConnected) { pendingRespawnPosition = position; pendingRespawnUntil = Time.unscaledTime + 4; Online.Respawn(destination, checkpoint); }
            TeleportOffline(position);
        }
        public void StartPracticeBattle()
        {
            if (BattleParty().Length == 0) { Notify("Najprv si vyber prvého spoločníka zadarmo."); Hud.ShowPets(); return; }
            if (Online?.Battle?.status == "active") { Hud.ShowBattle(); return; }
            if (!PetBattleRules.IsValidState(Save.PracticeBattle) || Save.PracticeBattle.Complete) {
                Save.PracticeBattleSequence++;
                var selected = new CompanionCollection { Owned = BattleParty().Select(id => Save.Companions.Owned.First(p => p.SpeciesId == id)).ToList(), EquippedId = Save.Companions.EquippedId };
                Save.PracticeBattle = PetBattleRules.CreatePractice(selected, "practice-" + Guid.NewGuid().ToString("N"), Save.PracticeBattleSequence);
                for (int i = 0; i < Save.PracticeBattle.Teams[0].Pets.Length; i++) {
                    var bonus = ArtifactRules.Bonus(Save.Artifacts, Save.PracticeBattle.Teams[0].Pets[i].SpeciesId);
                    PetBattleRules.SetLoadoutBonus(Save.PracticeBattle, 0, i, bonus.HP, bonus.Attack);
                }
            }
            preferPractice = true; BattleQuestionOpen = false; BattleExplanation = ""; ProfileStore.Save(Save); Hud.ShowBattle();
        }
        public void ViewOnlineBattle() { preferPractice = false; BattleQuestionOpen = false; Hud.ShowBattle(); }
        public void SelectBattleMove(PetBattleActionKind kind, int slot = 0)
        {
            var state = BattleState; int side = BattleSide;
            if (state == null || !PetBattleRules.CanUse(state, side, kind, slot)) { Notify(PetBattleRules.AvailabilityError(state, side, kind, slot)); return; }
            PendingBattleMove = kind; PendingBattleSlot = slot;
            if (!PetBattleRules.RequiresAnswer(kind)) { SubmitBattleAnswer(-1); return; }
            if (IsOnlineBattle) {
                var q = Online.Battle.question; if (q == null) { Notify("Čakáme na matematickú úlohu zo servera."); return; }
                BattleQuestion = new Question { Id = q.id, Grade = q.grade, Subject = (Subject)Math.Clamp(q.subject, 0, 5), Prompt = q.prompt, Options = q.options, CorrectOption = -1 };
            } else BattleQuestion = Learning.BeginQuestion(Profile, BattleTopic, DateTime.UtcNow, true);
            questionRound = state.Round; BattleQuestionOpen = BattleQuestion != null; Hud.ShowBattle();
        }
        public void SubmitBattleAnswer(int answer)
        {
            var state = BattleState; if (state == null || state.Complete) return;
            if (IsOnlineBattle) {
                if (Online.Busy) { Notify("Počkaj na dokončenie predchádzajúcej akcie a potvrď odpoveď znova."); return; }
                Online.SubmitBattle(state.Round, PendingBattleMove, PendingBattleSlot, answer, Online.Battle.question?.id ?? "");
                BattleQuestionOpen = false; Hud.ShowBattle(); return;
            }
            bool correct = false;
            if (PetBattleRules.RequiresAnswer(PendingBattleMove)) {
                if (!BattleQuestionOpen || BattleQuestion == null || answer < 0 || answer >= BattleQuestion.Options.Length) return;
                CurrentSubject = BattleQuestion.Subject;
                var result = Learning.SubmitAnswer(Profile, BattleQuestion.Id, answer, DateTime.UtcNow);
                if (!result.Accepted) { Notify(result.Error); return; }
                correct = result.Correct; BattleExplanation = (correct ? "Správne. " : "Správne riešenie: " + BattleQuestion.Options[result.CorrectOption] + ". ") + result.Explanation;
                RewardCompanionAnswer(result);
            } else BattleExplanation = "Rýchly výpad je slabý garantovaný útok bez otázky. Nezískava artefakt ani vedomostnú odmenu.";
            int round = state.Round;
            var command = new PetBattleCommand { Kind = PendingBattleMove, TargetSlot = PendingBattleSlot, Correct = correct,
                Topic = BattleQuestion == null ? -1 : (int)BattleQuestion.Subject, ActionId = PetBattleRules.ActionId(state, 0) };
            var accepted = PetBattleRules.Submit(state, 0, round, command);
            if (!accepted.Accepted) { Notify(accepted.Error); return; }
            var ai = PetBattleRules.ChooseAi(state, 1); if (ai != null) {
                ai.Correct = ((state.Seed * 17 + round * 11) & 15) > 2; ai.Topic = (int)PetBattleRules.GetTopicAffinity(state.Teams[1].Fighter.SpeciesId);
                PetBattleRules.Submit(state, 1, round, ai);
            }
            if (state.Complete) { var reward = PetBattleRules.ClaimVictory(state, 0); if (reward.Claimed) ApplyProgressReward("battle:" + state.Id, reward.XP, reward.Coins, reward.PetXP); }
            BattleQuestionOpen = false; ProfileStore.Save(Save); Hud.ShowBattle();
        }
        public void EndBattle()
        {
            if (IsOnlineBattle) Online.ForfeitBattle();
            else if (Save.PracticeBattle != null) { PetBattleRules.Forfeit(Save.PracticeBattle, 0, Save.PracticeBattle.Round); ProfileStore.Save(Save); }
            BattleQuestionOpen = false; Hud.ShowArena();
        }
        public void ApplyProgressReward(string receipt, int xp, int coins, int petXP)
        {
            if (string.IsNullOrEmpty(receipt) || Save.Purchases.Contains(receipt)) return;
            Save.Purchases.Add(receipt); Profile.Progress.XP += Math.Max(0, xp); Profile.Progress.Coins += Math.Max(0, coins); Profile.Progress.PetXP += Math.Max(0, petXP);
            if (Save.Companions.Equipped != null) Save.Companions.Equipped.XP += Math.Max(0, petXP);
            RefreshCompanion(); ProfileStore.Save(Save);
        }
        public void GrantArtifact(string receipt, Subject topic, int grade = 0)
        {
            if (receipt == "question:" || string.IsNullOrEmpty(receipt)) return;
            var drop = ArtifactRules.RewardCorrect(Save.Artifacts, receipt, topic, grade == 0 ? Profile.Grade : grade);
            if (drop.Awarded) { Notify(drop.Message); ProfileStore.Save(Save); }
        }
    }
}
