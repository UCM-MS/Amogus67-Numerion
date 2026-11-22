using System;
using System.Collections.Generic;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Runtime
{
    /// <summary>Unity inline class serialization may replace null with an empty object; identity and shape determine presence.</summary>
    public static class SaveNormalization
    {
        private static ContentCatalog studyCatalog;

        public static void Normalize(GameSave save)
        {
            if (save == null) return;
            save.Options ??= new GameOptions(); save.Options.Normalize();
            save.PracticeBattle = NormalizeBattleState(save.PracticeBattle);
            if (!IsValidStudyTest(save.ActiveStudyTest)) save.ActiveStudyTest = null;
        }

        /// <summary>JsonUtility may materialize inline-null fields on the live object as well as in JSON.</summary>
        public static string ToJson(GameSave save, bool pretty = false)
        {
            if (save == null) return "";
            Normalize(save);
            try { return JsonUtility.ToJson(save, pretty); }
            finally { Normalize(save); }
        }

        /// <summary>Missing or null legacy Options mean defaults; an explicit object preserves legitimate muted values.</summary>
        public static GameSave NormalizeSaveJson(string json)
        {
            var save = JsonUtility.FromJson<GameSave>(json);
            if (save == null) return null;
            if (!HasTopLevelObject(json, "Options")) save.Options = new GameOptions();
            Normalize(save);
            return save;
        }

        private static bool HasTopLevelObject(string json, string property)
        {
            int depth = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '{' || ch == '[') { depth++; continue; }
                if (ch == '}' || ch == ']') { depth--; continue; }
                if (ch != '"') continue;
                int start = ++i; bool escaped = false;
                for (; i < json.Length; i++)
                {
                    if (json[i] == '\\') { escaped = true; i++; continue; }
                    if (json[i] == '"') break;
                }
                if (depth != 1 || escaped || i - start != property.Length || string.CompareOrdinal(json, start, property, 0, property.Length) != 0) continue;
                int next = i + 1; while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (next >= json.Length || json[next] != ':') continue;
                next++; while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                return next < json.Length && json[next] == '{';
            }
            return false;
        }

        public static PetBattleState NormalizeBattleState(PetBattleState battle)
        {
            if (!PetBattleRules.IsValidState(battle)) return null;
            for (int side = 0; side < battle.PendingActions.Length; side++)
                if (battle.PendingActions[side] != null && string.IsNullOrWhiteSpace(battle.PendingActions[side].ActionId))
                    battle.PendingActions[side] = null;
            return battle;
        }

        public static OnlineRoom NormalizeOnlineRoom(OnlineRoom room)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.code)) return null;
            room.battle = NormalizeOnlineBattle(room.battle);
            return room;
        }

        public static OnlineBattle NormalizeOnlineBattle(OnlineBattle battle)
        {
            if (battle == null || string.IsNullOrWhiteSpace(battle.id) || string.IsNullOrWhiteSpace(battle.status)) return null;
            battle.state = NormalizeBattleState(battle.state);
            if (battle.question != null && string.IsNullOrWhiteSpace(battle.question.id)) battle.question = null;
            return battle;
        }

        public static bool IsValidStudyTest(StudyTest test)
        {
            if (test == null || string.IsNullOrWhiteSpace(test.Id) || test.Grade < 7 || test.Grade > 9
                || test.QuestionIds == null || test.Answers == null || test.QuestionIds.Length == 0
                || test.QuestionIds.Length > 24 || test.Answers.Length != test.QuestionIds.Length) return false;
            studyCatalog ??= ContentCatalog.CreateDefault();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool unanswered = false;
            for (int i = 0; i < test.QuestionIds.Length; i++)
            {
                var question = studyCatalog.Find(test.QuestionIds[i]);
                if (question == null || question.Grade != test.Grade || question.Subject != test.Subject
                    || !seen.Add(question.Id) || test.Answers[i] < -1 || test.Answers[i] > 3) return false;
                if (test.Answers[i] < 0) unanswered = true;
                else if (unanswered) return false;
            }
            return true;
        }
    }
}
