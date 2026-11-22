using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Runtime
{
    /// <summary>Opt-in evidence from the real Unity HTTP SDK, never enabled by normal gameplay.</summary>
    public sealed class SharedOnlineSmokeCheck : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public int SchemaVersion = 1, ElapsedSeconds, PlayerCount, Round, RoundsResolved, OwnSide = -1, WinnerSide = -2, OwnCorrectAnswers, MathSubmissions;
            public string Role, Phase = "WAIT_GAME", RoomCode = "", OwnName = "", ServerOrigin = "", BattleId = "", Outcome = "", Error = "";
            public bool Connected, LiveRoomTwoPeers, SawPeerMovement, MathRound, BattleResult, Passed, TimedOut;
            public string[] PeerNames = Array.Empty<string>();
            public string EvidenceKind = "Actual UnityWebRequest SDK execution; this file does not verify rendered UI.";
        }
        private Evidence evidence;
        private LumenGame game;
        private string output, requestedRoom, previousJson = "";
        private readonly Dictionary<string, Vector3> firstPositions = new Dictionary<string, Vector3>();
        private bool quitWhenDone, finished;
        private float started, deadline, nextAction, nextHeartbeat;
        private int previousFrameRate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-lumen-online-smoke");
            if (index < 0) return;
            var role = index + 1 < args.Length ? args[index + 1].ToLowerInvariant() : "";
            var checker = new GameObject("LUMEN Shared Online Smoke Check").AddComponent<SharedOnlineSmokeCheck>();
            checker.Initialize(args, role);
        }
        private static string Option(string[] args, string key, string fallback = "")
        { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        private void Initialize(string[] args, string role)
        {
            evidence = new Evidence { Role = role };
            output = Path.GetFullPath(Option(args, "-lumen-online-smoke-output", Path.Combine(Application.persistentDataPath, "shared-online-" + role + ".json")));
            requestedRoom = Option(args, "-lumen-online-smoke-room").Trim().ToUpperInvariant();
            quitWhenDone = args.Contains("-lumen-online-smoke-quit"); started = Time.realtimeSinceStartup;
            float timeout = 180; float.TryParse(Option(args, "-lumen-online-smoke-timeout", "180"), NumberStyles.Float, CultureInfo.InvariantCulture, out timeout);
            deadline = started + Mathf.Clamp(timeout > 0 ? timeout : 180, 30, 1800);
            previousFrameRate = Application.targetFrameRate; Application.targetFrameRate = 30;
            Publish(true);
            if (role != "host" && role != "client") { Fail("Use -lumen-online-smoke host or client."); return; }
            if (ProfileStore.ProfileSuffix.Length == 0 && !args.Contains("-lumen-profile-directory")) { Fail("Use an isolated -lumen-profile-directory or -lumen-profile-suffix for this QA flow."); return; }
            if (role == "client" && requestedRoom.Length != 6) { Fail("Client mode needs -lumen-online-smoke-room with a six-character code."); return; }
            StartCoroutine(Run());
        }
        private IEnumerator Run()
        {
            while (game == null || game.Online == null)
            {
                game = FindAnyObjectByType<LumenGame>();
                if (Time.realtimeSinceStartup >= deadline) { Timeout(); yield break; }
                yield return null;
            }
            Application.targetFrameRate = 30;
            if (!game.Save.HasCreatedProfile || game.BattleParty().Length == 0) { Fail("The isolated QA profile must already have a created player and at least one owned pet."); yield break; }
            evidence.OwnName = game.Profile.DisplayName; evidence.ServerOrigin = game.Online.BaseUrl;
            game.Hud?.CloseModal();
            evidence.Phase = evidence.Role == "host" ? "CREATING_ROOM" : "JOINING_ROOM"; Publish(true);
            if (evidence.Role == "host") game.Online.CreateRoom(); else game.Online.JoinRoom(requestedRoom);
            float firstRequest = Time.realtimeSinceStartup;
            while (!finished)
            {
                if (Time.realtimeSinceStartup >= deadline) { Timeout(); yield break; }
                var online = game.Online;
                evidence.Connected = online.InRoom;
                if (online.Room == null)
                {
                    if (!online.Busy && Time.realtimeSinceStartup - firstRequest > 12) { Fail(online.LastError.Length > 0 ? online.LastError : "The SDK did not enter the requested room."); yield break; }
                    Publish(); yield return null; continue;
                }
                var room = online.Room;
                evidence.RoomCode = room.code; evidence.PlayerCount = room.players?.Length ?? 0;
                evidence.PeerNames = (room.players ?? Array.Empty<OnlinePeer>()).Select(p => p.name).ToArray();
                if (evidence.PlayerCount >= 2) evidence.LiveRoomTwoPeers = true;
                foreach (var peer in room.players ?? Array.Empty<OnlinePeer>())
                {
                    if (peer.id == online.IdentityId) continue;
                    var p = new Vector3(peer.x, peer.y, peer.z);
                    if (!firstPositions.TryGetValue(peer.id, out var initial)) firstPositions[peer.id] = p;
                    else if (Vector3.Distance(initial, p) >= .75f) evidence.SawPeerMovement = true;
                }
                var battle = online.Battle;
                if (battle == null) evidence.Phase = evidence.LiveRoomTwoPeers ? "LIVE_PEERS_WAITING_CHALLENGE" : "ROOM_READY_WAITING_WEB_PEER";
                else
                {
                    evidence.BattleId = battle.id; evidence.OwnSide = battle.side; evidence.Round = battle.round;
                    if (battle.status == "challenge")
                    {
                        evidence.Phase = battle.side == 1 ? "ACCEPTING_WEB_CHALLENGE" : "WAITING_CHALLENGE_ACCEPTANCE";
                        if (battle.side == 1 && !online.Busy && Time.realtimeSinceStartup >= nextAction) { nextAction = Time.realtimeSinceStartup + 1; online.AcceptBattle(battle.id); }
                    }
                    if (battle.state != null && battle.state.CorrectAnswers != null && battle.side >= 0 && battle.side < battle.state.CorrectAnswers.Length)
                    {
                        evidence.RoundsResolved = battle.state.RoundsResolved; evidence.OwnCorrectAnswers = battle.state.CorrectAnswers[battle.side];
                        evidence.MathRound |= evidence.RoundsResolved > 0 && evidence.OwnCorrectAnswers > 0;
                    }
                    if (battle.status == "active" && battle.state != null)
                    {
                        evidence.Phase = "BATTLE_ACTIVE";
                        bool submitted = battle.submitted != null && battle.side < battle.submitted.Length && battle.submitted[battle.side];
                        if (!submitted && !online.Busy && Time.realtimeSinceStartup >= nextAction)
                        {
                            var kind = PetBattleRules.CanUse(battle.state, battle.side, PetBattleActionKind.Strike) ? PetBattleActionKind.Strike : PetBattleActionKind.QuickStrike;
                            int answer = -1;
                            if (kind != PetBattleActionKind.QuickStrike)
                            {
                                var wire = battle.question;
                                if (wire == null) { Publish(); yield return null; continue; }
                                string questionId = wire.id; int separator = questionId.LastIndexOf(':');
                                var definition = game.Catalog.Find(separator > 0 ? questionId.Substring(0, separator) : questionId);
                                if (definition == null || wire.options == null) { Fail("The SDK question does not exist in the authored local catalog."); yield break; }
                                answer = Array.IndexOf(wire.options, definition.Options[definition.CorrectOption]);
                                if (answer < 0) { Fail("Shuffled SDK options do not match the authored answer."); yield break; }
                                evidence.MathSubmissions++;
                            }
                            nextAction = Time.realtimeSinceStartup + 1;
                            online.SubmitBattle(battle.round, kind, 0, answer, battle.question?.id ?? "");
                        }
                    }
                    if (battle.status == "finished" && battle.state != null && battle.state.Complete)
                    {
                        evidence.BattleResult = true; evidence.WinnerSide = battle.state.Winner;
                        evidence.Outcome = battle.state.Winner < 0 ? "draw" : battle.state.Winner == battle.side ? "won" : "lost";
                        evidence.Phase = "BATTLE_FINISHED";
                    }
                }
                if (evidence.LiveRoomTwoPeers && evidence.SawPeerMovement && evidence.MathRound && evidence.BattleResult)
                { evidence.Passed = true; evidence.Phase = "PASS"; finished = true; Publish(true); Debug.Log("LUMEN_SHARED_ONLINE_SMOKE_PASS: " + evidence.RoomCode); if (quitWhenDone) StartCoroutine(QuitAfterLeave(0)); yield break; }
                Publish(); yield return null;
            }
        }
        private void Timeout()
        { evidence.TimedOut = true; Fail("Timed out before all live-peer, movement, mathematical-round and completed-battle observations were collected."); }
        private void Fail(string reason)
        {
            finished = true; evidence.Error = reason; evidence.Phase = evidence.TimedOut ? "TIMEOUT" : "FAIL";
            Publish(true); Debug.LogError("LUMEN_SHARED_ONLINE_SMOKE_FAIL: " + reason);
            if (quitWhenDone) StartCoroutine(QuitAfterLeave(1));
        }
        private void Publish(bool force = false)
        {
            if (evidence == null || string.IsNullOrEmpty(output)) return;
            string comparison = JsonUtility.ToJson(evidence);
            if (!force && comparison == previousJson && Time.realtimeSinceStartup < nextHeartbeat) return;
            evidence.ElapsedSeconds = (int)(Time.realtimeSinceStartup - started); previousJson = JsonUtility.ToJson(evidence); nextHeartbeat = Time.realtimeSinceStartup + 10;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)); string temporary = output + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(evidence, true), new UTF8Encoding(false));
                if (File.Exists(output)) File.Replace(temporary, output, null); else File.Move(temporary, output);
            }
            catch (IOException e) { Debug.LogWarning("Shared online QA evidence write failed: " + e.Message); }
            catch (UnauthorizedAccessException e) { Debug.LogWarning("Shared online QA evidence path denied: " + e.Message); }
        }
        private IEnumerator QuitAfterLeave(int exitCode)
        {
            if (game?.Online != null && game.Online.InRoom)
            {
                game.Online.LeaveRoom(); float stop = Time.realtimeSinceStartup + 5;
                while (game.Online.Busy && Time.realtimeSinceStartup < stop) yield return null;
            }
            Application.Quit(exitCode);
        }
        private void OnApplicationQuit()
        { if (game != null && game.Online != null && game.Online.InRoom) game.Online.LeaveRoom(); }
        private void OnDestroy() { Application.targetFrameRate = previousFrameRate; }
    }
}
