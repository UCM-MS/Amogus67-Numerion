using System;
using System.Collections.Generic;

namespace Lumen.Core
{
    [Serializable]
    public sealed class StudyTest
    {
        public string Id;
        public int Grade;
        public Subject Subject;
        public string[] QuestionIds = Array.Empty<string>();
        public int[] Answers = Array.Empty<int>();
        public bool RewardClaimed;
        public int NextIndex
        {
            get { for (int i = 0; i < Answers.Length; i++) if (Answers[i] < 0) return i; return Answers.Length; }
        }
        public bool Completed => QuestionIds.Length > 0 && Answers.Length == QuestionIds.Length && NextIndex == QuestionIds.Length;
    }

    public sealed class StudyAnswerResult
    {
        public bool Accepted;
        public bool AlreadyAnswered;
        public bool Correct;
        public bool Completed;
        public int CorrectOption;
        public string Explanation = "";
        public string Error = "";
    }

    public sealed class StudyReview
    {
        public string QuestionId;
        public string Prompt;
        public string SelectedAnswer;
        public string CorrectAnswer;
        public string Explanation;
    }

    public sealed class StudyTestResults
    {
        public bool Completed;
        public int Grade;
        public Subject Subject;
        public int CorrectCount;
        public int TotalCount;
        public int Percent;
        public bool Passed;
        public StudyReview[] WrongAnswers = Array.Empty<StudyReview>();
        public string Error = "";
    }

    /// <summary>Topic tests with ordered immutable submissions and one first-pass reward per grade/topic.
    /// Public DTOs may be saved, but authoritative online servers must retain their own session state.</summary>
    public sealed class StudyTestService
    {
        private readonly ContentCatalog catalog;
        public StudyTestService(ContentCatalog catalog) => this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        public StudyTest CreateTest(PlayerProfile profile, Subject topic, int seed, int count = 6)
        {
            ProfileRules.Normalize(profile);
            var questions = new List<Question>();
            foreach (var q in catalog.Questions) if (q.Grade == profile.Grade && q.Subject == topic) questions.Add(q);
            if (questions.Count == 0) throw new ArgumentException("Téma nemá otázky pre zvolený ročník.", nameof(topic));
            count = Math.Max(1, Math.Min(questions.Count, count));
            uint random = unchecked((uint)seed); if (random == 0) random = 0x9e3779b9;
            for (int i = questions.Count - 1; i > 0; i--)
            {
                random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                int j = (int)(random % (uint)(i + 1)); var swap = questions[i]; questions[i] = questions[j]; questions[j] = swap;
            }
            var test = new StudyTest { Id = Guid.NewGuid().ToString("N"), Grade = profile.Grade, Subject = topic,
                QuestionIds = new string[count], Answers = new int[count] };
            for (int i = 0; i < count; i++) { test.QuestionIds[i] = questions[i].Id; test.Answers[i] = -1; }
            return test;
        }

        public Question CurrentQuestion(StudyTest test)
        {
            if (!IsValid(test) || test.Completed) return null;
            return catalog.Find(test.QuestionIds[test.NextIndex]);
        }

        public StudyAnswerResult Submit(StudyTest test, string questionId, int option)
        {
            var result = new StudyAnswerResult();
            if (!IsValid(test) || option < 0 || option > 3) { result.Error = "Neplatný test alebo odpoveď."; return result; }
            int index = Array.IndexOf(test.QuestionIds, questionId);
            if (index < 0) { result.Error = "Otázka nepatrí do testu."; return result; }
            if (test.Answers[index] >= 0) { result.AlreadyAnswered = true; result.Error = "Odpoveď už bola vyhodnotená."; return result; }
            if (index != test.NextIndex) { result.Error = "Najprv dokonči aktuálnu otázku."; return result; }
            var q = catalog.Find(questionId);
            test.Answers[index] = option;
            result.Accepted = true; result.Correct = option == q.CorrectOption; result.CorrectOption = q.CorrectOption;
            result.Explanation = q.Explanation; result.Completed = test.Completed;
            return result;
        }

        public StudyTestResults Results(StudyTest test)
        {
            var result = new StudyTestResults();
            if (!IsValid(test)) { result.Error = "Neplatný test."; return result; }
            result.Grade = test.Grade; result.Subject = test.Subject; result.TotalCount = test.QuestionIds.Length;
            result.Completed = test.Completed;
            if (!result.Completed) { result.Error = "Najprv dokonči všetky otázky testu."; return result; }
            var wrong = new List<StudyReview>();
            for (int i = 0; i < test.QuestionIds.Length; i++)
            {
                var q = catalog.Find(test.QuestionIds[i]);
                if (test.Answers[i] == q.CorrectOption) { result.CorrectCount++; continue; }
                wrong.Add(new StudyReview { QuestionId = q.Id, Prompt = q.Prompt, SelectedAnswer = q.Options[test.Answers[i]],
                    CorrectAnswer = q.Options[q.CorrectOption], Explanation = q.Explanation });
            }
            result.Percent = result.CorrectCount * 100 / result.TotalCount;
            result.Passed = result.Percent >= 70; result.WrongAnswers = wrong.ToArray();
            return result;
        }

        public RewardResult ClaimReward(PlayerProfile profile, StudyTest test, bool applyBalances = true)
        {
            ProfileRules.Normalize(profile);
            var results = Results(test);
            if (!results.Completed || !results.Passed) return new RewardResult { Error = "Na odmenu treba dokončiť test s úspešnosťou aspoň 70 %." };
            if (results.TotalCount < 6) return new RewardResult { Error = "Krátky test je precvičovanie. Odmenu ponúka celý šesťotázkový test." };
            if (profile.Grade != test.Grade) return new RewardResult { Error = "Vráť sa na ročník, v ktorom si test začal." };
            string claim = "study.pass.g" + test.Grade + ".s" + (int)test.Subject;
            if (test.RewardClaimed || profile.Progress.ClaimedQuestIds.Contains(claim))
                return new RewardResult { Error = "Prvú odmenu za tento ročník a tému už máš. Ďalšie testy sú precvičovanie." };
            // Commit the durable claim before mutating balances; a recreated session cannot farm the reward.
            profile.Progress.ClaimedQuestIds.Add(claim); test.RewardClaimed = true;
            const int xp = 50, coins = 40, petXP = 25;
            if (applyBalances) { profile.Progress.XP += xp; profile.Progress.Coins += coins; profile.Progress.PetXP += petXP; }
            return new RewardResult { Claimed = true, XP = xp, Coins = coins, PetXP = petXP };
        }

        private bool IsValid(StudyTest test)
        {
            if (test == null || string.IsNullOrEmpty(test.Id) || test.Grade < 7 || test.Grade > 9 || test.QuestionIds == null || test.Answers == null
                || test.QuestionIds.Length == 0 || test.QuestionIds.Length != test.Answers.Length || test.QuestionIds.Length > 24) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            bool unanswered = false;
            for (int i = 0; i < test.QuestionIds.Length; i++)
            {
                var q = catalog.Find(test.QuestionIds[i]);
                if (q == null || q.Grade != test.Grade || q.Subject != test.Subject || !ids.Add(q.Id) || test.Answers[i] < -1 || test.Answers[i] > 3) return false;
                if (test.Answers[i] < 0) unanswered = true;
                else if (unanswered) return false;
            }
            return true;
        }
    }
}
