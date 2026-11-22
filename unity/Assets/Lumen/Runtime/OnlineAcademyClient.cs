using System;
using System.Collections;
using System.Net;
using System.Text;
using Lumen.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Lumen.Runtime
{
    [Serializable] public sealed class OnlinePresence
    {
        public string name, pet;
        public int skin, outfit, hair, accessory, petGear, grade = 7;
        public float x, y, z, yaw;
        public string[] party = Array.Empty<string>();
    }
    [Serializable] public sealed class OnlinePeer
    {
        public string id, name, pet, battleId;
        public int skin, outfit, hair, accessory, petGear, grade, courseCheckpoint = -1;
        public float x, y, z, yaw;
        public long seenAt;
    }
    [Serializable] public sealed class OnlineArtifactReward { public string id; public int topic, grade; }
    [Serializable] public sealed class OnlineQuestion
    {
        public string id, prompt, topic;
        public int grade, subject;
        public string[] options = Array.Empty<string>();
    }
    [Serializable] public sealed class OnlineBattle
    {
        public string id, status, challengerId, opponentId, explanation, rewardId;
        public int side, round, revision, correctOption = -1, rewardXP, rewardCoins, rewardPetXP;
        public long deadline;
        public bool[] submitted = new bool[2];
        public PetBattleState state;
        public OnlineQuestion question;
        public OnlineArtifactReward[] artifactRewards = Array.Empty<OnlineArtifactReward>();
    }
    [Serializable] public sealed class OnlineRoom
    {
        public string code;
        public int revision, beaconsMask, teamShards;
        public long serverTime;
        public OnlinePeer[] players = Array.Empty<OnlinePeer>();
        public OnlineBattle battle;
    }

    /// <summary>
    /// Same HTTPS protocol on Windows and WebGL. Snapshots arrive at 5 Hz; presentation
    /// interpolates remote players independently of rendering. No browser UDP or hidden host.
    /// Cloud writes use a revision so opening another device cannot silently overwrite progress.
    /// </summary>
    public sealed class OnlineAcademyClient : MonoBehaviour
    {
        public const string ProductionOrigin = "https://legacy-numerion.example.invalid";
        public const string LocalOrigin = "http://127.0.0.1:4174";
        public string BaseUrl = LocalOrigin;
        public string IdentityId { get; private set; } = "";
        public string PairingCode => identityToken;
        public bool HasLocalBackup => PlayerPrefs.HasKey(StorageKey + "-before-cloud");
        public int ProfileRevision { get; private set; }
        public bool CloudConflict { get; private set; }
        public bool Busy => commandsInFlight > 0 || profileInFlight || creatingIdentity;
        public bool InRoom => Room != null;
        public string RoomCode => Room?.code ?? "";
        public OnlineRoom Room { get; private set; }
        public OnlineBattle Battle => Room?.battle;
        public string Status { get; private set; } = "Spoločný svet · PC aj prehliadač";
        public string LastError { get; private set; } = "";
        public event Action<string> StatusChanged;
        public event Action<OnlineRoom> RoomChanged;
        public event Action<OnlineBattle> BattleChanged;
        public event Action<GameSave> ProfileLoaded;

        private const string TokenKey = "lumen-online-identity-v1";
        private string StorageKey => "lumen-online-v2-" + OriginKey(BaseUrl) + (ProfileStore.ProfileSuffix.Length > 0 ? "-" + ProfileStore.ProfileSuffix : "");
        private string identityToken = "", battleSignature = "", lastCloudJson = "";
        private Func<OnlinePresence> presence;
        private Func<GameSave> save;
        private float nextPulse, nextCloudCheck;
        private bool pulseInFlight, profileInFlight, creatingIdentity, relocating;
        private int sequence, failures, commandsInFlight, generation;

        [Serializable] private sealed class RequestBody
        {
            public string code, targetId, battleId, identityToken, saveJson, questionId, actionId, destination;
            public int profileRevision, sequence, round, kind, targetSlot, optionIndex, checkpoint, beaconIndex;
            public OnlinePresence presence;
        }
        [Serializable] private sealed class ResponseBody
        {
            public bool ok;
            public string error, message, identityToken, identityId, saveJson;
            public int profileRevision;
            public OnlineRoom room;
            public OnlineBattle battle;
        }

        private void Awake()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            string rememberedOrigin = PlayerPrefs.GetString("lumen-server-origin-v1-" + ProfileStore.ProfileSuffix, LocalOrigin);
            if (ValidOrigin(rememberedOrigin)) BaseUrl = rememberedOrigin;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var page) &&
                (page.Scheme == "http" || page.Scheme == "https")) BaseUrl = page.GetLeftPart(UriPartial.Authority);
#endif
            var args = Environment.GetCommandLineArgs(); int argument = Array.IndexOf(args, "-lumen-online-origin");
            if (argument >= 0 && argument + 1 < args.Length && ValidOrigin(args[argument + 1])) BaseUrl = args[argument + 1].TrimEnd('/');
            LoadIdentityForOrigin();
        }
        public void Initialize(Func<OnlinePresence> presenceProvider, Func<GameSave> saveProvider)
        { presence = presenceProvider; save = saveProvider; nextCloudCheck = Time.unscaledTime + 30; }

        private void Update()
        {
            if (Room != null && !pulseInFlight && !relocating && Time.unscaledTime >= nextPulse)
            {
                nextPulse = Time.unscaledTime + (failures > 0 ? Mathf.Min(4, failures) : .2f);
                StartCoroutine(Pulse());
            }
            if (identityToken.Length > 0 && !profileInFlight && !CloudConflict && Time.unscaledTime >= nextCloudCheck)
            {
                nextCloudCheck = Time.unscaledTime + 30;
                var current = save?.Invoke();
                if (current != null && lastCloudJson.Length > 0 && SaveNormalization.ToJson(current) != lastCloudJson) SaveCloud();
            }
        }

        public void CreateRoom() => CommandAfterIdentity("rooms/create", () => new RequestBody { presence = Presence() });
        public void JoinRoom(string code) => CommandAfterIdentity("rooms/join", () => new RequestBody { code = (code ?? "").Trim().ToUpperInvariant(), presence = Presence() });
        public void LeaveRoom()
        {
            if (Room == null) return;
            string code = Room.code; Room = null; generation++; pulseInFlight = false;
            RoomChanged?.Invoke(null); BattleChanged?.Invoke(null); battleSignature = "";
            StartCoroutine(Command("rooms/leave", new RequestBody { code = code }, false));
            SetStatus("Pokračuješ v sólo svete. Cloudový profil zostáva prepojený.");
        }
        public void Challenge(string targetId) => RoomCommand("battle/challenge", new RequestBody { targetId = targetId });
        public void AcceptBattle(string id) => RoomCommand("battle/accept", new RequestBody { battleId = id });
        public void DeclineBattle(string id) => RoomCommand("battle/decline", new RequestBody { battleId = id });
        public void ForfeitBattle() { if (Battle != null) RoomCommand("battle/forfeit", new RequestBody { battleId = Battle.id }); }
        public void Respawn(string destination, int checkpoint = 0)
        { if (Room != null && !relocating) StartCoroutine(Relocate(destination, checkpoint)); }
        private IEnumerator Relocate(string destination, int checkpoint)
        {
            relocating = true;
            while (pulseInFlight && Room != null) yield return null;
            if (Room != null) yield return Command("rooms/respawn", new RequestBody { code = Room.code, destination = destination, checkpoint = checkpoint });
            relocating = false; nextPulse = Time.unscaledTime + .2f;
        }
        public void CollectBeacon(int index) => RoomCommand("rooms/beacon", new RequestBody { beaconIndex = index });
        public void SubmitBattle(int expectedRound, PetBattleActionKind kind, int targetSlot, int optionIndex, string questionId)
        {
            if (Battle == null) return;
            RoomCommand("battle/action", new RequestBody { battleId = Battle.id, round = expectedRound,
                kind = (int)kind, targetSlot = targetSlot, optionIndex = optionIndex, questionId = questionId,
                actionId = Battle.id + ":" + expectedRound + ":" + Battle.side });
        }
        public void SaveCloud()
        {
            if (profileInFlight || CloudConflict) { if (CloudConflict) SetStatus("Cloud má novšiu verziu. Najprv načítaj cloud alebo obnov spojenie."); return; }
            StartCoroutine(SaveProfile());
        }
        public void LoadCloud()
        {
            if (InRoom) { SetStatus("Pred načítaním cloudového profilu odíď z miestnosti."); return; }
            if (!profileInFlight && !Busy) StartCoroutine(LoadProfile(identityToken, false));
        }
        public void PairProfile(string code)
        {
            if (profileInFlight || Room != null || Busy) { SetStatus("Pred prepojením profilu odíď zo spoločného sveta a počkaj na dokončenie operácie."); return; }
            var token = (code ?? "").Trim();
            if (IdentityFromToken(token).Length == 0) { SetStatus("Vlož celý súkromný kód z druhého zariadenia."); return; }
            StartCoroutine(LoadProfile(token, true));
        }
        public bool ConfigureOrigin(string origin)
        {
            if (InRoom || Busy || profileInFlight || creatingIdentity) { SetStatus("Pred zmenou servera odíď z miestnosti a počkaj na dokončenie ukladania."); return false; }
            var selected = (origin ?? "").Trim().TrimEnd('/');
            if (!ValidOrigin(selected)) { SetStatus("Použi HTTPS alebo adresu lokálneho servera vrátane portu."); return false; }
            BaseUrl = selected; generation++; sequence = 0; lastCloudJson = ""; CloudConflict = false; LoadIdentityForOrigin();
#if !UNITY_WEBGL || UNITY_EDITOR
            PlayerPrefs.SetString("lumen-server-origin-v1-" + ProfileStore.ProfileSuffix, selected); PlayerPrefs.Save();
#endif
            SetStatus("Vybraný server: " + selected + ". Profil je uložený oddelene pre každý server."); return true;
        }
        public void RestoreLocalBackup()
        {
            if (InRoom || profileInFlight) { SetStatus("Pred obnovou zálohy odíď z miestnosti."); return; }
            try {
                var local = SaveNormalization.NormalizeSaveJson(PlayerPrefs.GetString(StorageKey + "-before-cloud", ""));
                if (local?.Profile == null) { SetStatus("Lokálna záloha nie je dostupná."); return; }
                lastCloudJson = ""; CloudConflict = false; ProfileLoaded?.Invoke(local);
                SetStatus("Lokálna záloha obnovená. Cloud zmeníš až ručným uložením postupu.");
            } catch (ArgumentException) { SetStatus("Záloha sa nepodarila načítať."); }
        }

        private OnlinePresence Presence() => presence?.Invoke() ?? new OnlinePresence { name = "Objaviteľ", z = 12 };
        private void RoomCommand(string path, RequestBody body)
        {
            if (Room == null || Busy) return;
            body.code = Room.code; StartCoroutine(Command(path, body));
        }
        private void CommandAfterIdentity(string path, Func<RequestBody> body)
        { if (!Busy && !creatingIdentity) StartCoroutine(WithIdentity(path, body)); }
        private IEnumerator WithIdentity(string path, Func<RequestBody> body)
        {
            yield return EnsureIdentity();
            if (identityToken.Length > 0) yield return Command(path, body());
        }
        private IEnumerator EnsureIdentity()
        {
            if (identityToken.Length > 0) yield break;
            if (creatingIdentity) yield break;
            creatingIdentity = true; var local = save?.Invoke();
            yield return Send("profile/create", new RequestBody { saveJson = local == null ? "" : SaveNormalization.ToJson(local) }, response => {
                SetIdentity(response.identityToken, response.identityId, response.profileRevision);
                lastCloudJson = local == null ? "" : SaveNormalization.ToJson(local);
                SetStatus("Cloudový profil pripravený. Súkromný kód prenesie postup na druhé zariadenie.");
            }, null, "");
            creatingIdentity = false;
        }
        private IEnumerator Pulse()
        {
            if (Room == null) yield break;
            pulseInFlight = true; int requestGeneration = generation; string code = Room.code;
            yield return Send("rooms/pulse", new RequestBody { code = code, sequence = ++sequence, presence = Presence() }, response => {
                if (requestGeneration != generation || Room == null || Room.code != code) return;
                bool recovered = failures > 0; failures = 0; AcceptRoom(response.room);
                if (recovered) SetStatus("Spojenie so spoločným svetom obnovené.");
            }, (status, error) => {
                if (requestGeneration != generation) return;
                failures++;
                if (status == 401 || status == 403 || status == 404 || status == 410) {
                    Room = null; RoomChanged?.Invoke(null); BattleChanged?.Invoke(null); battleSignature = "";
                }
                if (failures == 1 || Room == null) SetStatus(error);
            });
            if (requestGeneration == generation) pulseInFlight = false;
        }
        private IEnumerator Command(string path, RequestBody body, bool acceptRoom = true)
        {
            commandsInFlight++; int requestGeneration = generation;
            yield return Send(path, body, response => {
                if (requestGeneration != generation) return;
                if (acceptRoom && response.room != null) { AcceptRoom(response.room); nextPulse = Time.unscaledTime + .2f; }
                if (!string.IsNullOrEmpty(response.message)) SetStatus(response.message);
            });
            commandsInFlight--;
            StatusChanged?.Invoke(Status);
        }
        private void AcceptRoom(OnlineRoom room)
        {
            room = SaveNormalization.NormalizeOnlineRoom(room);
            if (room == null) return;
            if (Room != null && Room.code == room.code && room.serverTime < Room.serverTime) return;
            Room = room; RoomChanged?.Invoke(room);
            string signature = room.battle == null ? "" : room.battle.id + ":" + room.battle.revision + ":" + room.battle.status;
            if (signature != battleSignature) { battleSignature = signature; BattleChanged?.Invoke(room.battle); }
        }
        private IEnumerator SaveProfile()
        {
            profileInFlight = true; yield return EnsureIdentity();
            var local = save?.Invoke(); string json = local == null ? "" : SaveNormalization.ToJson(local);
            if (identityToken.Length > 0 && local != null) yield return Send("profile/save", new RequestBody { profileRevision = ProfileRevision, saveJson = json }, response => {
                ProfileRevision = response.profileRevision; RememberRevision(); lastCloudJson = json; CloudConflict = false;
                SetStatus("Postup uložený v cloude · verzia " + ProfileRevision + ".");
            }, (status, message) => { CloudConflict = status == 409; SetStatus(message); });
            profileInFlight = false;
            StatusChanged?.Invoke(Status);
        }
        private IEnumerator LoadProfile(string token, bool pairing)
        {
            if (token.Length == 0) { SetStatus("Najprv vytvor cloudový profil alebo vlož kód z druhého zariadenia."); yield break; }
            profileInFlight = true;
            yield return Send("profile/load", new RequestBody(), response => {
                try {
                    var loaded = SaveNormalization.NormalizeSaveJson(response.saveJson);
                    if (loaded?.Profile == null) { SetStatus("Cloudový profil nemá platný formát."); return; }
                    var before = save?.Invoke();
                    string backup = before == null ? "" : SaveNormalization.ToJson(before);
                    SetIdentity(token, response.identityId, response.profileRevision); CloudConflict = false;
                    if (backup.Length > 0) { PlayerPrefs.SetString(StorageKey + "-before-cloud", backup); PlayerPrefs.Save(); }
                    lastCloudJson = response.saveJson; ProfileLoaded?.Invoke(loaded);
                    SetStatus(pairing ? "Profil prepojený. Rovnaký postup môžeš používať v aplikácii aj na webe." : "Novšia cloudová verzia načítaná.");
                } catch (ArgumentException) { SetStatus("Cloudový profil sa nepodarilo načítať."); }
            }, null, token);
            profileInFlight = false;
            StatusChanged?.Invoke(Status);
        }
        private IEnumerator Send(string path, RequestBody body, Action<ResponseBody> success, Action<long, string> failure = null, string tokenOverride = null)
        {
            if (!ValidOrigin(BaseUrl)) { SetStatus("Adresa online služby musí používať HTTPS alebo lokálny vývojový server."); failure?.Invoke(0, Status); yield break; }
            using (var request = new UnityWebRequest(BaseUrl.TrimEnd('/') + "/api/unity/" + path, "POST")) {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 8;
                request.SetRequestHeader("Content-Type", "application/json");
                string token = tokenOverride ?? identityToken;
                if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
                yield return request.SendWebRequest();
                ResponseBody response = null;
                try { if (request.downloadHandler != null) response = JsonUtility.FromJson<ResponseBody>(request.downloadHandler.text); } catch (ArgumentException) { }
                if (request.result != UnityWebRequest.Result.Success || response == null || !response.ok) {
                    LastError = !string.IsNullOrEmpty(response?.message) ? response.message : "Spojenie sa nepodarilo. Skontroluj internet; sólo hranie zostáva dostupné.";
                    if (failure != null) failure(request.responseCode, LastError); else SetStatus(LastError);
                    yield break;
                }
                LastError = ""; success?.Invoke(response);
            }
        }
        private void SetIdentity(string token, string id, int revision)
        {
            identityToken = token ?? ""; IdentityId = id ?? IdentityFromToken(identityToken); ProfileRevision = revision;
            PlayerPrefs.SetString(StorageKey, identityToken); RememberRevision();
        }
        private void RememberRevision() { PlayerPrefs.SetInt(StorageKey + "-revision", ProfileRevision); PlayerPrefs.Save(); }
        private void LoadIdentityForOrigin()
        {
            identityToken = PlayerPrefs.GetString(StorageKey, ""); ProfileRevision = PlayerPrefs.GetInt(StorageKey + "-revision", 0);
            if (identityToken.Length == 0 && BaseUrl.TrimEnd('/') == ProductionOrigin && ProfileStore.ProfileSuffix.Length == 0) {
                identityToken = PlayerPrefs.GetString(TokenKey, ""); ProfileRevision = PlayerPrefs.GetInt(TokenKey + "-revision", 0);
            }
            IdentityId = IdentityFromToken(identityToken);
        }
        private static string OriginKey(string origin)
        { uint value = 2166136261; foreach (char c in (origin ?? "").ToLowerInvariant().TrimEnd('/')) { value ^= c; value *= 16777619; } return value.ToString("x8"); }
        private void SetStatus(string message) { Status = message ?? ""; StatusChanged?.Invoke(Status); }
        private static string IdentityFromToken(string token)
        { if (string.IsNullOrEmpty(token)) return ""; int dot = token.IndexOf('.'); return dot == 36 && token.Length == 101 ? token.Substring(0, dot) : ""; }
        private static bool ValidOrigin(string origin)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/") return false;
            if (uri.Scheme == "https") return true;
            if (uri.Scheme != "http") return false;
            if (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1") return true;
            if (!IPAddress.TryParse(uri.Host, out var address)) return false;
            var bytes = address.GetAddressBytes();
            return bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);
        }
        private void OnDestroy() { generation++; }
    }
}
