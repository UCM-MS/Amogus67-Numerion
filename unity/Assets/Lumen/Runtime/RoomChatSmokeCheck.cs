using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Lumen.Runtime
{
    /// <summary>Explicit QA only: exercises the real Unity chat SDK with an independent room peer.</summary>
    public sealed class RoomChatSmokeCheck : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public int SchemaVersion = 1, ElapsedSeconds, PlayerCount, MessageCount;
            public string Phase = "WAIT_GAME", RoomCode = "", OwnName = "Numerion QA", PeerName = "", ServerOrigin = "";
            public string RunId, SDKText, PeerText, Error = "";
            public bool Connected, HistoryReady, SDKTextQueued, SDKWaveQueued, SelfTextEcho, SelfWaveEcho;
            public bool IndependentPeerPresent, PeerTextSeen, PeerHeartSeen, Passed, TimedOut;
            public long SelfTextId, SelfWaveId, PeerTextId, PeerHeartId;
            public string EvidenceKind = "Actual UnityWebRequest chat SDK and independent room-peer messages; no rendered UI claim.";
        }
        private Evidence evidence;
        private LumenGame game;
        private string output, previousJson = "", peerId = "";
        private readonly HashSet<string> seenPeerIds = new HashSet<string>();
        private float started, deadline, nextHeartbeat, waveAfter;
        private bool quitWhenDone, finished;
        private int previousFrameRate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("-lumen-chat-smoke")) return;
            var checker = new GameObject("Numerion Room Chat SDK Smoke Check").AddComponent<RoomChatSmokeCheck>();
            checker.Initialize(args);
        }
        private static string Option(string[] args, string key)
        { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : ""; }
        private static bool InArtifacts(string path)
        { return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)); }
        private void Initialize(string[] args)
        {
            string runId = Guid.NewGuid().ToString("N").Substring(0, 12);
            evidence = new Evidence { RunId = runId, SDKText = "Numerion native SDK chat QA " + runId, PeerText = "Numerion HTTP peer chat QA " + runId };
            quitWhenDone = args.Contains("-lumen-chat-smoke-quit"); started = Time.realtimeSinceStartup;
            previousFrameRate = Application.targetFrameRate; Application.targetFrameRate = 30;
            int timeout; if (!int.TryParse(Option(args, "-lumen-chat-smoke-timeout"), NumberStyles.Integer, CultureInfo.InvariantCulture, out timeout)) timeout = 180;
            deadline = started + Mathf.Clamp(timeout, 30, 240);
            try
            {
                string requestedOutput = Option(args, "-lumen-chat-smoke-output"), directory = Option(args, "-lumen-profile-directory");
                if (string.IsNullOrWhiteSpace(requestedOutput) || string.IsNullOrWhiteSpace(directory) || ProfileStore.ProfileSuffix.Length == 0)
                { Fail("Specify an isolated profile directory, profile suffix, and chat smoke output path."); return; }
                string resolvedOutput = Path.GetFullPath(requestedOutput), resolvedDirectory = Path.GetFullPath(directory);
                if (!InArtifacts(resolvedOutput) || !InArtifacts(resolvedDirectory))
                { Fail("Chat QA output and isolated profile directory must be under an Artifacts folder."); return; }
                output = resolvedOutput;
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is NotSupportedException)
            { Fail("The isolated QA paths are invalid."); return; }
            Publish(true);
            if (args.Contains("-lumen-online-smoke") || args.Contains("-lumen-smoke"))
            { Fail("Run this chat QA component independently of other automatic smoke flows."); return; }
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
            if (!game.Save.HasCreatedProfile) { Fail("Prepare a created profile in the explicit isolated QA directory first."); yield break; }
            if (game.Online.InRoom) { Fail("The isolated QA client must start outside a room."); yield break; }
            game.Profile.DisplayName = evidence.OwnName; // Only the explicitly isolated QA save is loaded.
            evidence.ServerOrigin = game.Online.BaseUrl; game.Hud?.CloseModal();
            evidence.Phase = "CREATING_ROOM"; Publish(true); game.Online.CreateRoom();
            float firstRequest = Time.realtimeSinceStartup;
            while (!finished)
            {
                if (Time.realtimeSinceStartup >= deadline) { Timeout(); yield break; }
                var online = game.Online; evidence.Connected = online.InRoom;
                if (!online.InRoom)
                {
                    if (!online.Busy && Time.realtimeSinceStartup - firstRequest > 12)
                    { Fail("The native SDK could not enter its chat QA room."); yield break; }
                    Publish(); yield return null; continue;
                }
                if (evidence.RoomCode.Length > 0 && evidence.RoomCode != online.RoomCode)
                { Fail("The QA client unexpectedly changed rooms."); yield break; }
                evidence.RoomCode = online.RoomCode; evidence.PlayerCount = online.Room.players?.Length ?? 0;
                evidence.HistoryReady = online.ChatHistoryReady; evidence.MessageCount = online.RoomMessages.Length;
                foreach (var peer in online.Room.players ?? Array.Empty<OnlinePeer>())
                    if (peer.id != online.IdentityId) seenPeerIds.Add(peer.id);
                if (online.ChatHistoryReady && !evidence.SDKTextQueued && !online.Busy && !online.ChatSending)
                {
                    evidence.SDKTextQueued = true; online.SendChat(evidence.SDKText);
                    evidence.Phase = "WAIT_SELF_TEXT_ECHO";
                }
                foreach (var message in online.RoomMessages)
                {
                    if (message.authorId == online.IdentityId)
                    {
                        if (message.text == evidence.SDKText && !evidence.SelfTextEcho)
                        { evidence.SelfTextEcho = true; evidence.SelfTextId = message.id; waveAfter = Time.realtimeSinceStartup + 1.2f; }
                        if (evidence.SDKWaveQueued && message.emoteId == "wave" && message.id > evidence.SelfTextId)
                        { evidence.SelfWaveEcho = true; evidence.SelfWaveId = message.id; }
                    }
                    else if (message.text == evidence.PeerText && peerId.Length == 0)
                    { peerId = message.authorId; evidence.PeerName = message.authorName; evidence.PeerTextSeen = true; evidence.PeerTextId = message.id; }
                }
                if (peerId.Length > 0)
                {
                    evidence.IndependentPeerPresent |= seenPeerIds.Contains(peerId);
                    foreach (var message in online.RoomMessages)
                        if (message.authorId == peerId && message.emoteId == "heart" && message.id > evidence.PeerTextId)
                        { evidence.PeerHeartSeen = true; evidence.PeerHeartId = message.id; }
                }
                if (evidence.SelfTextEcho && !evidence.SDKWaveQueued && Time.realtimeSinceStartup >= waveAfter && !online.Busy && !online.ChatSending)
                { evidence.SDKWaveQueued = true; online.SendEmote("wave"); evidence.Phase = "WAIT_SELF_WAVE_ECHO"; }
                if (evidence.SelfWaveEcho) evidence.Phase = "WAIT_INDEPENDENT_PEER_TEXT_AND_HEART";
                if (evidence.HistoryReady && evidence.SelfTextEcho && evidence.SelfWaveEcho && evidence.IndependentPeerPresent && evidence.PeerTextSeen && evidence.PeerHeartSeen)
                {
                    evidence.Passed = true; evidence.Phase = "PASS"; finished = true; Publish(true);
                    Debug.Log("NUMERION_ROOM_CHAT_SDK_SMOKE_PASS");
                    if (quitWhenDone) StartCoroutine(QuitAfterLeave(0)); yield break;
                }
                Publish(); yield return null;
            }
        }
        private void Timeout()
        { evidence.TimedOut = true; Fail("Timed out before native text/wave echoes and an independent peer's text/heart were all observed."); }
        private void Fail(string reason)
        {
            finished = true; evidence.Error = reason; evidence.Phase = evidence.TimedOut ? "TIMEOUT" : "FAIL";
            Publish(true); Debug.LogError("NUMERION_ROOM_CHAT_SDK_SMOKE_FAIL: " + reason);
            if (quitWhenDone) StartCoroutine(QuitAfterLeave(1));
        }
        private void Publish(bool force = false)
        {
            if (evidence == null || string.IsNullOrEmpty(output)) return;
            string comparison = JsonUtility.ToJson(evidence);
            if (!force && comparison == previousJson && Time.realtimeSinceStartup < nextHeartbeat) return;
            evidence.ElapsedSeconds = (int)(Time.realtimeSinceStartup - started); previousJson = JsonUtility.ToJson(evidence);
            nextHeartbeat = Time.realtimeSinceStartup + 5;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)); string temporary = output + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(evidence, true), new UTF8Encoding(false));
                if (File.Exists(output)) File.Replace(temporary, output, null); else File.Move(temporary, output);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { Debug.LogWarning("Chat QA evidence could not be written."); }
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
        { if (game?.Online != null && game.Online.InRoom) game.Online.LeaveRoom(); }
        private void OnDestroy() { Application.targetFrameRate = previousFrameRate; }
    }
}
