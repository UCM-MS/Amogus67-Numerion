using System;
using System.Collections;
using System.Linq;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Networking
{
    /// <summary>Opt-in native process integration probe. Does nothing without -lumen-smoke host/client.</summary>
    public sealed class NativeMultiplayerSmoke : MonoBehaviour
    {
        private string role;
        private bool failed, finished;
        private float hardDeadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-lumen-smoke");
            if (index < 0 || index + 1 >= args.Length) return;
            string selected = args[index + 1];
            if (selected != "host" && selected != "client") return;
            var runner = new GameObject("LUMEN native multiplayer smoke").AddComponent<NativeMultiplayerSmoke>();
            runner.role = selected;
        }

        private void Update()
        {
            if (!finished && !failed && hardDeadline > 0 && Time.realtimeSinceStartup > hardDeadline)
                Fail("Integration probe exceeded its 55 second deadline.");
        }

        private IEnumerator Start()
        {
            hardDeadline = Time.realtimeSinceStartup + 55f;
            yield return WaitFor(() => NetworkSession.Instance != null && NetworkSession.Instance.LocalPlayer != null, 20, "local player spawn");
            if (failed) yield break;
            NetworkSession session = NetworkSession.Instance;
            NetworkExplorer local = session.LocalPlayer;
            if (!Check(session.TickRate == 60 && Mathf.Abs(Time.fixedDeltaTime - 1f / 60f) < .00001f, "actual network and physics configuration are 60 Hz")) yield break;
            yield return new WaitForSecondsRealtime(.7f);
            string ownName = role == "host" ? "SmokeHost" : "SmokeGuest";
            var ownLook = role == "host" ? new NetworkAppearance(2, 5, 1, 2) : new NetworkAppearance(4, 1, 2, 3);
            session.SendAppearance(ownName, ownLook);
            yield return WaitFor(() => NetworkExplorer.All.Count == 2 && NetworkExplorer.All.Any(p => p.DisplayName == "SmokeHost")
                && NetworkExplorer.All.Any(p => p.DisplayName == "SmokeGuest"), 20, "two named network peers");
            if (failed) yield break;
            NetworkExplorer remote = NetworkExplorer.All.First(p => !p.IsOwner);
            NetworkAppearance expectedRemote = role == "host" ? new NetworkAppearance(4, 1, 2, 3) : new NetworkAppearance(2, 5, 1, 2);
            yield return WaitFor(() => remote.Appearance.Equals(expectedRemote), 5, "remote appearance replication");
            if (failed) yield break;

            Vector3 start = local.transform.position, remoteStart = remote.transform.position;
            uint tickStart = local.AuthoritativeTick;
            float begin = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - begin < 1.25f)
            {
                local.SetInput(Vector2.up, role == "host" ? 90f : 270f, false);
                yield return new WaitForFixedUpdate();
            }
            local.SetInput(Vector2.zero, 0, false);
            yield return new WaitForSecondsRealtime(.5f);
            float elapsed = Time.realtimeSinceStartup - begin;
            float measuredTickRate = unchecked(local.AuthoritativeTick - tickStart) / elapsed;
            if (!Check(Vector2.Distance(new Vector2(start.x, start.z), new Vector2(local.transform.position.x, local.transform.position.z)) > 2f,
                "owned character moved through the authoritative simulation")) yield break;
            if (!Check(Vector3.Distance(remoteStart, remote.transform.position) > 1f, "remote walking replicated to this process")) yield break;
            if (!Check(measuredTickRate > 40f && measuredTickRate < 80f, "measured authoritative ticks remain near 60 Hz")) yield break;
            Debug.Log($"LUMEN_SMOKE_TICK_RATE: {role} {measuredTickRate:0.00} Hz");

            Question question = null; AnswerResult answer = null;
            local.QuestionReceived += value => question = value;
            local.QuizAnswerReceived += value => answer = value;
            local.QuizError += message => Fail("Quiz RPC error: " + message);
            local.BeginQuiz(Subject.Technology, AgeBand.Ages13To15);
            yield return WaitFor(() => question != null, 5, "server-selected advanced question");
            if (failed) yield break;
            if (!Check(question.AgeBand == AgeBand.Ages13To15 && question.Options.Length == 4 && question.CorrectOption == -1,
                "age cohort and question transmitted without an answer key")) yield break;
            Question canonical = ContentCatalog.CreateDefault().Find(question.Id);
            if (!Check(canonical != null, "server question exists in the build curriculum")) yield break;
            local.SubmitQuizAnswer(question.Id, canonical.CorrectOption);
            yield return WaitFor(() => answer != null, 5, "authoritative quiz evaluation");
            if (failed) yield break;
            if (!Check(answer.Accepted && answer.Correct && answer.XpAwarded > 0, "server accepted a correct answer and awarded XP")) yield break;
            yield return WaitFor(() => local.OnlineXP == answer.XpAwarded, 5, "session XP replication");
            if (failed) yield break;
            int earned = local.OnlineXP;
            yield return new WaitForSecondsRealtime(.3f);
            answer = null;
            local.SubmitQuizAnswer(question.Id, canonical.CorrectOption);
            yield return WaitFor(() => answer != null, 5, "duplicate answer rejection");
            if (failed) yield break;
            if (!Check(!answer.Accepted && answer.AlreadyAnswered && local.OnlineXP == earned,
                "duplicate answer did not mint a second reward")) yield break;
            if (session.BeaconPositions.Count > 0)
            {
                int farBeacon = Enumerable.Range(0, session.BeaconPositions.Count)
                    .OrderByDescending(i => Vector3.Distance(local.transform.position, session.BeaconPositions[i])).First();
                ulong maskBefore = local.SharedBeaconsMask;
                local.TryCollectBeacon(farBeacon);
                yield return new WaitForSecondsRealtime(.35f);
                if (!Check(local.SharedBeaconsMask == maskBefore && local.TeamShards == 0,
                    "server rejected a beacon collection from outside its range")) yield break;
            }
            if (session.CoursePositions.Count > 1)
            {
                local.StartCourse();
                yield return WaitFor(() => local.CourseActive && local.CourseCheckpoint == 0, 5, "server course start");
                if (failed) yield break;
                yield return new WaitForSecondsRealtime(.85f);
                Vector3 firstCheckpoint = session.CoursePositions[0];
                if (!Check(Vector3.Distance(local.transform.position, firstCheckpoint) < 1f, "server teleported only to the registered course start")) yield break;
                local.ReturnToCheckpoint(session.CoursePositions.Count - 1);
                yield return new WaitForSecondsRealtime(.85f);
                if (!Check(local.CourseCheckpoint == 0 && Vector3.Distance(local.transform.position, firstCheckpoint) < 1f,
                    "server rejected a teleport to an unvisited checkpoint")) yield break;
                local.ResetToSpawn();
                yield return WaitFor(() => !local.CourseActive && Vector3.Distance(local.transform.position, session.SpawnOrigin) < 1f,
                    5, "server-owned return to spawn");
                if (failed) yield break;
            }
            yield return WaitFor(() => remote.OnlineCorrect == 1, 5, "other player completed its server quiz");
            if (failed) yield break;
            if (role == "client")
            {
                yield return new WaitForSecondsRealtime(.8f);
                session.Disconnect();
                yield return new WaitForSecondsRealtime(.3f);
            }
            else
            {
                yield return WaitFor(() => NetworkExplorer.All.Count == 1, 8, "client disconnect propagated to host");
                if (failed) yield break;
                if (!Check(session.Manager.IsListening && session.LocalPlayer == local, "host remained healthy after peer disconnect")) yield break;
                session.Disconnect();
            }
            finished = true;
            Debug.Log("LUMEN_SMOKE_PASS: " + role + " · names, appearance, walking, ticks, quiz, duplicate rewards, beacon range, checkpoints, disconnect");
            Application.Quit(0);
        }

        private IEnumerator WaitFor(Func<bool> condition, float seconds, string label)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!failed && !condition() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!failed && !condition()) Fail("Timed out waiting for " + label + ".");
        }

        private bool Check(bool condition, string label)
        {
            if (!condition) Fail(label);
            else Debug.Log("LUMEN_SMOKE_CHECK: " + role + " · " + label);
            return condition;
        }

        private void Fail(string message)
        {
            if (failed || finished) return;
            failed = true;
            Debug.LogError("LUMEN_SMOKE_FAIL: " + role + " · " + message);
            Application.Quit(1);
        }
    }
}
