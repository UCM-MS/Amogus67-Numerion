#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Lumen.Core;
using Lumen.Runtime;
using UnityEditor;
using UnityEngine;

namespace Lumen.Editor
{
    /// <summary>Runs inside the actual Unity engine, specifically exercising JsonUtility's inline-null representation.</summary>
    public static class PersistenceValidation
    {
        private static int checks;

        [MenuItem("LUMEN/Validate JsonUtility Persistence")]
        public static void Run()
        {
            checks = 0;
            var blank = RoundTrip(new GameSave());
            SaveNormalization.Normalize(blank);
            Require(blank.PracticeBattle == null, "Fresh profile has no resumable practice battle.");
            Require(blank.ActiveStudyTest == null, "Fresh profile has no resumable study test.");
            Require(Mathf.Abs(blank.Options.MasterVolume - .8f) < .001f && Mathf.Abs(blank.Options.MusicVolume - .35f) < .001f
                && blank.Options.FieldOfView == 62 && blank.Options.InterfaceScale == 1, "Default audio and camera survive real JsonUtility round trip.");
            string legacyJson = "{\"Profile\":" + JsonUtility.ToJson(new PlayerProfile()) + "}";
            var rawLegacy = JsonUtility.FromJson<GameSave>(legacyJson);
            Debug.Log("LUMEN_PERSISTENCE_LEGACY_OPTIONS: " + (rawLegacy.Options == null ? "null" : JsonUtility.ToJson(rawLegacy.Options)));
            var legacy = SaveNormalization.NormalizeSaveJson(legacyJson);
            Require(legacy.Options.MasterVolume == .8f && legacy.Options.MusicVolume == .35f && legacy.Options.EffectsVolume == .7f
                && legacy.Options.FieldOfView == 62 && legacy.Options.InterfaceScale == 1, "Legacy missing Options receives defaults.");
            var muted = new GameSave(); muted.Options.MasterVolume = muted.Options.MusicVolume = muted.Options.EffectsVolume = 0;
            muted = SaveNormalization.NormalizeSaveJson(JsonUtility.ToJson(muted));
            Require(muted.Options.MasterVolume == 0 && muted.Options.MusicVolume == 0 && muted.Options.EffectsVolume == 0, "Explicit muted settings remain muted.");
            var nested = SaveNormalization.NormalizeSaveJson("{\"Profile\":{\"DisplayName\":\"Options\",\"Options\":{\"MasterVolume\":0}}}");
            Require(nested.Options.MasterVolume == .8f, "Nested Options key cannot masquerade as top-level saved options.");
            blank.PracticeBattle = new PetBattleState(); blank.ActiveStudyTest = new StudyTest();
            SaveNormalization.Normalize(blank);
            Require(blank.PracticeBattle == null && blank.ActiveStudyTest == null, "Explicit inline placeholder objects are absent.");

            var collection = new CompanionCollection { Owned = new List<CompanionProgress> {
                new CompanionProgress { SpeciesId = "ember", Nickname = "Test Iskra" }
            }, EquippedId = "ember" };
            var battle = PetBattleRules.CreatePractice(collection, "persistence-validation", 41);
            var liveSave = new GameSave { Companions = collection, PracticeBattle = battle };
            Require(battle.PendingActions[0] == null && battle.PendingActions[1] == null, "New live battle begins with no submitted commands.");
            string rawLiveJson = JsonUtility.ToJson(liveSave);
            Debug.Log("LUMEN_PERSISTENCE_RAW_TOJSON_LIVE: left=" + DescribeCommand(battle.PendingActions[0])
                + "; right=" + DescribeCommand(battle.PendingActions[1]) + "; jsonCharacters=" + rawLiveJson.Length);
            SaveNormalization.Normalize(liveSave);
            Require(battle.PendingActions[0] == null && battle.PendingActions[1] == null, "Live raw serialization placeholders are removed.");
            for (int saveIndex = 0; saveIndex < 3; saveIndex++)
            {
                SaveNormalization.ToJson(liveSave, saveIndex == 1);
                Require(ReferenceEquals(liveSave.PracticeBattle, battle) && battle.PendingActions[0] == null && battle.PendingActions[1] == null,
                    "Safe serializer leaves the same live battle unsubmitted after save " + saveIndex + ".");
                Require(PetBattleRules.CanUse(battle, 0, PetBattleActionKind.Strike), "Fresh practice action remains usable after live save " + saveIndex + ".");
            }
            var liveBlank = new GameSave(); SaveNormalization.ToJson(liveBlank);
            Require(liveBlank.PracticeBattle == null && liveBlank.ActiveStudyTest == null, "Safe serialization cannot leave phantom activities on the live blank profile.");
            Require(PetBattleRules.Submit(battle, 0, 1, new PetBattleCommand {
                Kind = PetBattleActionKind.Guard, Correct = true, ActionId = PetBattleRules.ActionId(battle, 0)
            }).Accepted, "Practice first committed command accepted.");
            string committedJson = SaveNormalization.ToJson(liveSave);
            Require(battle.PendingActions[0]?.ActionId == "persistence-validation:1:0" && battle.PendingActions[1] == null,
                "Safe serializer preserves a real live committed command without submitting the opponent.");
            var committedRead = SaveNormalization.NormalizeSaveJson(committedJson);
            Require(committedRead.PracticeBattle.PendingActions[0]?.ActionId == "persistence-validation:1:0" && committedRead.PracticeBattle.PendingActions[1] == null,
                "Safe serializer output reloads the real command and clears the uncommitted placeholder.");
            var saved = RoundTrip(new GameSave { Companions = collection, PracticeBattle = battle });
            SaveNormalization.Normalize(saved);
            Require(saved.PracticeBattle != null && PetBattleRules.IsValidState(saved.PracticeBattle), "Real practice state survives JsonUtility.");
            Require(saved.PracticeBattle.PendingActions[0]?.ActionId == "persistence-validation:1:0", "A real committed action survives.");
            Require(saved.PracticeBattle.PendingActions[1] == null, "Uncommitted action placeholder is cleared.");
            Require(PetBattleRules.Submit(saved.PracticeBattle, 1, 1, PetBattleRules.ChooseAi(saved.PracticeBattle, 1)).RoundResolved,
                "Restored battle can resolve the remaining action.");
            saved = RoundTrip(saved); SaveNormalization.Normalize(saved);
            Require(saved.PracticeBattle.Round == 2 && saved.PracticeBattle.PendingActions[0] == null && saved.PracticeBattle.PendingActions[1] == null,
                "Resolved round stays playable after another round trip.");
            PetBattleRules.Forfeit(saved.PracticeBattle, 1, saved.PracticeBattle.Round);
            var complete = RoundTrip(saved); SaveNormalization.Normalize(complete);
            Require(complete.PracticeBattle?.Complete == true, "Completed battle remains a real saved battle.");

            var catalog = ContentCatalog.CreateDefault(); var tests = new StudyTestService(catalog);
            var test = tests.CreateTest(new PlayerProfile { Grade = 7 }, Subject.Mathematics, 77, 6);
            var question = tests.CurrentQuestion(test);
            Require(tests.Submit(test, question.Id, question.CorrectOption).Accepted, "Study fixture has one answered question.");
            var studying = RoundTrip(new GameSave { ActiveStudyTest = test }); SaveNormalization.Normalize(studying);
            Require(studying.ActiveStudyTest != null && studying.ActiveStudyTest.NextIndex == 1, "Real study answers survive.");
            Require(tests.CurrentQuestion(studying.ActiveStudyTest) != null, "Restored study can continue.");
            studying.ActiveStudyTest.QuestionIds[0] = "missing-question"; SaveNormalization.Normalize(studying);
            Require(studying.ActiveStudyTest == null, "Study with unknown question is rejected.");

            var room = RoundTrip(new OnlineRoom { code = "ABC123", players = Array.Empty<OnlinePeer>(), battle = null });
            room = SaveNormalization.NormalizeOnlineRoom(room);
            Require(room != null && room.battle == null, "Empty online room has no phantom battle.");
            var challenge = RoundTrip(new OnlineBattle { id = "challenge-id", status = "challenge", state = null, question = null });
            challenge = SaveNormalization.NormalizeOnlineBattle(challenge);
            Require(challenge != null && challenge.state == null && challenge.question == null, "Real challenge retains identity without phantom state or question.");
            var active = RoundTrip(new OnlineBattle { id = "active-id", status = "active", state = PetBattleRules.Create("active-id", new[] { "bolt" }, new[] { "astra" }),
                question = new OnlineQuestion { id = "question-1", prompt = "2 + 2", grade = 7, options = new[] { "1", "2", "3", "4" } } });
            active = SaveNormalization.NormalizeOnlineBattle(active);
            Require(active?.state != null && active.question?.id == "question-1", "Real online state and question survive.");
            Require(active.state.PendingActions[0] == null && active.state.PendingActions[1] == null, "Redacted null online commands stay unsubmitted locally.");
            Require(SaveNormalization.NormalizeOnlineRoom(RoundTrip(new OnlineRoom())) == null, "Phantom room is absent.");
            Require(SaveNormalization.NormalizeOnlineBattle(RoundTrip(new OnlineBattle())) == null, "Phantom battle is absent.");
            Debug.Log("LUMEN_PERSISTENCE_VALIDATION_PASSED: " + checks + " actual Unity JsonUtility checks.");
        }

        private static T RoundTrip<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static string DescribeCommand(PetBattleCommand command)
            => command == null ? "null" : "object/actionId=" + (string.IsNullOrEmpty(command.ActionId) ? "EMPTY" : command.ActionId);
        private static void Require(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("JsonUtility persistence: " + message);
        }
    }
}
#endif
