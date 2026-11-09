using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lumen.Core
{
    /// <summary>
    /// Deterministic progression used by both a Unity client and an authoritative server.
    /// Keep one authoritative profile per player. BeginQuestion must precede SubmitAnswer.
    /// UTC day keys, attempts and claimed quest IDs must be persisted with the profile.
    /// Replayed/practice answers never create currency, XP, pet XP, streak or quest progress.
    /// </summary>
    public sealed class LearningService
    {
        private readonly ContentCatalog catalog;
        public LearningService(ContentCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.catalog = catalog;
        }

        /// <summary>Returns a grade-matched mathematics question near adaptive difficulty, or null when completed.</summary>
        public Question BeginQuestion(PlayerProfile profile, Subject subject, DateTime utcNow, bool allowPractice = false)
        {
            ProfileRules.Normalize(profile);
            var progress = profile.Progress;
            var active = catalog.Find(progress.ActiveQuestionId);
            if (active != null && active.Subject == subject && active.Grade == profile.Grade)
                return active;
            var target = GetMastery(profile, subject).TargetDifficulty;
            var candidates = new List<Question>();
            var distance = int.MaxValue;
            foreach (var question in catalog.Questions)
            {
                if (question.Subject != subject || question.Grade != profile.Grade || WasAnswered(progress, question.Id)) continue;
                int nextDistance = Math.Abs(question.Difficulty - target);
                if (nextDistance < distance) { candidates.Clear(); distance = nextDistance; }
                if (nextDistance == distance) candidates.Add(question);
            }
            bool practice = candidates.Count == 0;
            if (practice && allowPractice)
                foreach (var question in catalog.Questions)
                    if (question.Subject == subject && question.Grade == profile.Grade) candidates.Add(question);
            if (candidates.Count == 0)
            {
                progress.ActiveQuestionId = "";
                progress.ActiveQuestionIsPractice = false;
                return null;
            }
            var selected = candidates[(progress.QuestionSequence & int.MaxValue) % candidates.Count];
            progress.QuestionSequence = progress.QuestionSequence == int.MaxValue ? 0 : progress.QuestionSequence + 1;
            progress.ActiveQuestionId = selected.Id;
            progress.ActiveQuestionIsPractice = practice;
            return selected;
        }

        public AnswerResult SubmitAnswer(PlayerProfile profile, string questionId, int optionIndex, DateTime utcNow)
        {
            ProfileRules.Normalize(profile);
            var progress = profile.Progress;
            var question = catalog.Find(questionId);
            var result = new AnswerResult { NewLevel = progress.Level, Streak = progress.Streak };
            if (question == null || optionIndex < 0 || optionIndex > 3)
            {
                result.Error = "Neplatná otázka alebo odpoveď.";
                return result;
            }
            bool answered = WasAnswered(progress, question.Id);
            if (question.Id != progress.ActiveQuestionId || question.Grade != profile.Grade)
            {
                result.AlreadyAnswered = answered;
                result.Error = answered ? "Táto odpoveď už bola vyhodnotená." : "Najprv si otvor túto otázku.";
                return result;
            }
            bool practice = progress.ActiveQuestionIsPractice;
            if (answered && !practice)
            {
                result.AlreadyAnswered = true;
                result.Error = "Táto odpoveď už bola vyhodnotená.";
                progress.ActiveQuestionId = "";
                return result;
            }
            result.Accepted = true;
            result.Correct = optionIndex == question.CorrectOption;
            result.Practice = practice;
            result.CorrectOption = question.CorrectOption;
            result.Explanation = question.Explanation;
            progress.ActiveQuestionId = "";
            progress.ActiveQuestionIsPractice = false;
            if (practice) return result;

            progress.Streak = result.Correct ? progress.Streak + 1 : 0;
            progress.BestStreak = Math.Max(progress.BestStreak, progress.Streak);
            if (result.Correct)
            {
                result.XpAwarded = 8 + question.Difficulty * 5 + Math.Min(progress.Streak - 1, 5) * 2;
                result.CoinsAwarded = 4 + question.Difficulty * 2;
                result.PetXpAwarded = 6 + question.Difficulty * 3;
                progress.XP += result.XpAwarded;
                progress.Coins += result.CoinsAwarded;
                progress.PetXP += result.PetXpAwarded;
            }
            var mastery = GetMastery(profile, question.Subject);
            mastery.TotalAnswers++;
            if (result.Correct)
            {
                mastery.CorrectAnswers++;
                mastery.Score = Math.Min(100, mastery.Score + 12 + question.Difficulty * 2);
                mastery.CorrectRun++;
                mastery.WrongRun = 0;
                if (mastery.CorrectRun >= 2)
                {
                    mastery.TargetDifficulty = Math.Min(4, mastery.TargetDifficulty + 1);
                    mastery.CorrectRun = 0;
                }
            }
            else
            {
                mastery.Score = Math.Max(0, mastery.Score - 6);
                mastery.WrongRun++;
                mastery.CorrectRun = 0;
                if (mastery.WrongRun >= 2)
                {
                    mastery.TargetDifficulty = Math.Max(1, mastery.TargetDifficulty - 1);
                    mastery.WrongRun = 0;
                }
            }
            progress.Attempts.Add(new QuestionAttempt
            {
                QuestionId = question.Id, Subject = question.Subject, AgeBand = question.AgeBand, Grade = question.Grade,
                Difficulty = question.Difficulty, Correct = result.Correct, UtcDay = DayKey(utcNow),
                XpAwarded = result.XpAwarded, CoinsAwarded = result.CoinsAwarded,
                PetXpAwarded = result.PetXpAwarded, StreakAfter = progress.Streak
            });
            result.Streak = progress.Streak;
            result.NewLevel = progress.Level;
            return result;
        }

        public MasteryRecord GetMastery(PlayerProfile profile, Subject subject)
        {
            ProfileRules.Normalize(profile);
            foreach (var record in profile.Progress.Mastery) if (record.Subject == subject && record.Grade == profile.Grade) return record;
            var created = new MasteryRecord { Subject = subject, Grade = profile.Grade };
            profile.Progress.Mastery.Add(created);
            return created;
        }

        /// <summary>Daily goals use UTC. Mathematics topic goals are unique to strand and grade.</summary>
        public IReadOnlyList<QuestView> GetQuests(PlayerProfile profile, DateTime utcNow)
        {
            ProfileRules.Normalize(profile);
            var day = DayKey(utcNow);
            int answered = 0, correct = 0, bestStreakToday = 0;
            var subjectsToday = new HashSet<Subject>();
            foreach (var attempt in profile.Progress.Attempts)
            {
                if (attempt.UtcDay != day) continue;
                answered++;
                if (attempt.Correct) { correct++; subjectsToday.Add(attempt.Subject); }
                bestStreakToday = Math.Max(bestStreakToday, attempt.StreakAfter);
            }
            var views = new List<QuestView>
            {
                Quest(profile, "daily." + day + ".explore", "Denný prieskum", "Vyrieš 3 nové otázky a aspoň 1 správne.",
                    correct > 0 ? answered : 0, 3, 25, 30, 10),
                Quest(profile, "daily." + day + ".streak", "Čistá séria", "Dosiahni sériu 3 správnych odpovedí.",
                    bestStreakToday, 3, 40, 35, 15),
                Quest(profile, "daily." + day + ".horizons", "Matematické obzory", "Odpovedz správne v 3 rôznych matematických oblastiach.",
                    subjectsToday.Count, 3, 45, 40, 20)
            };
            foreach (Subject subject in Enum.GetValues(typeof(Subject)))
            {
                int count = 0;
                foreach (var attempt in profile.Progress.Attempts)
                    if (attempt.Subject == subject && attempt.Grade == profile.Grade) count++;
                views.Add(Quest(profile, "math.g" + profile.Grade + ".s" + ((int)subject),
                    "Expedícia: " + CurriculumLabels.SubjectName(subject), "Dokonči 4 nové otázky tejto témy v " + profile.Grade + ". ročníku.",
                    count, 4, 60, 50, 25));
            }
            return views;
        }

        public RewardResult ClaimQuest(PlayerProfile profile, string questId, DateTime utcNow)
        {
            ProfileRules.Normalize(profile);
            foreach (var quest in GetQuests(profile, utcNow))
            {
                if (quest.Id != questId) continue;
                if (quest.Claimed) return new RewardResult { Error = "Odmena už bola prevzatá." };
                if (!quest.Complete) return new RewardResult { Error = "Výprava ešte nie je dokončená." };
                profile.Progress.ClaimedQuestIds.Add(quest.Id);
                profile.Progress.XP += quest.XP;
                profile.Progress.Coins += quest.Coins;
                profile.Progress.PetXP += quest.PetXP;
                return new RewardResult { Claimed = true, XP = quest.XP, Coins = quest.Coins, PetXP = quest.PetXP };
            }
            return new RewardResult { Error = "Výprava nie je dostupná." };
        }

        public int CompletedSections(PlayerProfile profile)
        {
            ProfileRules.Normalize(profile);
            int count = 0;
            foreach (Subject subject in Enum.GetValues(typeof(Subject)))
            {
                int answered = 0;
                foreach (var attempt in profile.Progress.Attempts)
                    if (attempt.Subject == subject && attempt.Grade == profile.Grade) answered++;
                if (answered >= 4) count++;
            }
            return count;
        }

        public string[] GetUnlocks(PlayerProfile profile)
        {
            ProfileRules.Normalize(profile);
            var unlocks = new List<string> { "Šesť matematických oblastí", "Tréning spoločníka", "Spoločná expedícia" };
            if (profile.Progress.Level >= 2) unlocks.Add("Bronzový odznak objaviteľa");
            if (profile.Progress.Level >= 4) unlocks.Add("Strieborný odznak bádateľa");
            if (profile.Progress.PetLevel >= 3) unlocks.Add("Aura skúseného spoločníka");
            if (CompletedSections(profile) >= 3) unlocks.Add("Titul Matematický bádateľ");
            if (CompletedSections(profile) >= 6) unlocks.Add("Titul Majster akadémie");
            return unlocks.ToArray();
        }

        private static QuestView Quest(PlayerProfile profile, string id, string name, string description,
            int current, int target, int xp, int coins, int petXp)
        {
            return new QuestView
            {
                Id = id, Name = name, Description = description, Current = Math.Min(target, current), Target = target,
                XP = xp, Coins = coins, PetXP = petXp, Claimed = profile.Progress.ClaimedQuestIds.Contains(id)
            };
        }

        private static bool WasAnswered(PlayerProgress progress, string id)
        {
            foreach (var attempt in progress.Attempts) if (attempt.QuestionId == id) return true;
            return false;
        }

        private static string DayKey(DateTime utcNow)
        {
            if (utcNow.Kind == DateTimeKind.Local) utcNow = utcNow.ToUniversalTime();
            return utcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }

    public static class ProfileRules
    {
        /// <summary>Normalizes cosmetic identity; never trust client-supplied progression on a server.</summary>
        public static PlayerProfile Normalize(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.DisplayName = CleanName(profile.DisplayName);
            profile.Grade = CurriculumLabels.NormalizeGrade(profile.Grade);
            profile.SchemaVersion = 2;
            // Retained for old saves and network DTOs; learning is selected by Grade.
            profile.AgeBand = AgeBand.Ages13To15;
            if (profile.Appearance == null) profile.Appearance = new PlayerAppearance();
            profile.Appearance.SkinTone = Clamp(profile.Appearance.SkinTone, 0, 7);
            profile.Appearance.OutfitColor = Clamp(profile.Appearance.OutfitColor, 0, 7);
            profile.Appearance.HairStyle = Clamp(profile.Appearance.HairStyle, 0, 3);
            profile.Appearance.Accessory = Clamp(profile.Appearance.Accessory, 0, 3);
            if (profile.Progress == null) profile.Progress = new PlayerProgress();
            var progress = profile.Progress;
            progress.XP = Math.Max(0, progress.XP);
            progress.Coins = Math.Max(0, progress.Coins);
            progress.PetXP = Math.Max(0, progress.PetXP);
            if (progress.Attempts == null) progress.Attempts = new List<QuestionAttempt>();
            if (progress.Mastery == null) progress.Mastery = new List<MasteryRecord>();
            if (progress.ClaimedQuestIds == null) progress.ClaimedQuestIds = new List<string>();
            progress.Attempts.RemoveAll(attempt => attempt == null);
            progress.Mastery.RemoveAll(mastery => mastery == null);
            foreach (var mastery in progress.Mastery)
                mastery.TargetDifficulty = Clamp(mastery.TargetDifficulty, 1, 4);
            return profile;
        }

        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Objaviteľ";
            var builder = new StringBuilder();
            bool lastSpace = true;
            foreach (char character in name.Trim())
            {
                if (builder.Length >= 20) break;
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                {
                    builder.Append(character); lastSpace = false;
                }
                else if (char.IsWhiteSpace(character) && !lastSpace)
                {
                    builder.Append(' '); lastSpace = true;
                }
            }
            return builder.Length == 0 ? "Objaviteľ" : builder.ToString().Trim();
        }

        private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
    }
}
