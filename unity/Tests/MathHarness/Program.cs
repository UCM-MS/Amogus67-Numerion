using System;
using System.Collections.Generic;
using System.Linq;
using Lumen.Core;

internal static class Program
{
    private static int checks;
    private static readonly DateTime Now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static string Snapshot(PlayerProgress p) => p.XP + ":" + p.Coins + ":" + p.PetXP + ":" + p.Attempts.Count;

    public static void Main()
    {
        var catalog = ContentCatalog.CreateDefault(); var learning = new LearningService(catalog);
        Check(catalog.Questions.Count == 144 && catalog.Sections.Count == 6, "144 mathematics challenges and six strands");
        Check(catalog.Questions.Select(q => q.Id).Distinct().Count() == 144, "unique stable question IDs");
        Check(catalog.Questions.All(q => q.Id.StartsWith("math-g") && q.Grade >= 7 && q.Grade <= 9), "all questions belong to the new math curriculum");
        Check(LessonCatalog.All.Length == 18, "eighteen textbook chapters");
        foreach (var lesson in LessonCatalog.All)
        {
            Check(lesson.Explanation.Length > 280 && lesson.WorkedExample.Length > 90 && lesson.Takeaway.Length > 35,
                "substantive lesson and worked example: " + lesson.Id);
            Check(LessonCatalog.GetLessons(lesson.Grade, lesson.Subject).Count == 1, "lesson is discoverable by grade and strand");
        }

        foreach (int grade in new[] { 7, 8, 9 })
        {
            Check(catalog.Questions.Count(q => q.Grade == grade) == 48, "48 questions per grade");
            for (int i = 0; i < 4; i++) Check(catalog.Questions.Count(q => q.Grade == grade && q.CorrectOption == i) == 12, "balanced answer positions");
            foreach (Subject strand in Enum.GetValues(typeof(Subject)))
            {
                var pool = catalog.Questions.Where(q => q.Grade == grade && q.Subject == strand).ToArray();
                Check(pool.Length == 8 && pool.Select(q => q.Difficulty).Distinct().Count() == 4, "topic has eight questions across four difficulty levels");
                var p = new PlayerProfile { Grade = grade };
                var seen = new HashSet<string>();
                for (int n = 0; n < 8; n++)
                {
                    var q = learning.BeginQuestion(p, strand, Now);
                    Check(q != null && q.Grade == grade && q.Subject == strand && seen.Add(q.Id), "adaptive selection stays in grade/topic without repeating");
                    var outside = catalog.Questions.First(x => x.Id != q.Id);
                    Check(!learning.SubmitAnswer(p, outside.Id, outside.CorrectOption, Now).Accepted, "unopened question rejected");
                    var answer = learning.SubmitAnswer(p, q.Id, q.CorrectOption, Now);
                    Check(answer.Accepted && answer.Correct && answer.XpAwarded > 0 && !string.IsNullOrEmpty(answer.Explanation), "first answer earns explained reward");
                    var before = Snapshot(p.Progress);
                    Check(!learning.SubmitAnswer(p, q.Id, q.CorrectOption, Now).Accepted && Snapshot(p.Progress) == before, "answer replay cannot mint rewards");
                }
                Check(learning.BeginQuestion(p, strand, Now) == null, "completed topic has no new questions");
                var practice = learning.BeginQuestion(p, strand, Now, true); var beforePractice = Snapshot(p.Progress);
                var practiceAnswer = learning.SubmitAnswer(p, practice.Id, practice.CorrectOption, Now);
                Check(practiceAnswer.Accepted && practiceAnswer.Practice && Snapshot(p.Progress) == beforePractice, "practice provides explanation but no XP/currency/attempt farming");
                var quest = learning.GetQuests(p, Now).First(q => q.Id == "math.g" + grade + ".s" + (int)strand);
                Check(quest.Complete && learning.ClaimQuest(p, quest.Id, Now).Claimed, "grade/topic expedition reward available");
                Check(!learning.ClaimQuest(p, quest.Id, Now).Claimed, "topic expedition reward exactly once");
                int mastery = learning.GetMastery(p, strand).TotalAnswers;
                p.Grade = grade == 9 ? 7 : grade + 1;
                Check(learning.GetMastery(p, strand).TotalAnswers == 0 && mastery == 8, "mastery is independent between grades");

                // A real content upgrade must keep completed saves useful without
                // reopening their old rewards or losing the six historical IDs.
                var imported = new PlayerProfile { Grade = grade, SchemaVersion = 2 };
                imported.Progress.XP = 410; imported.Progress.Coins = 217;
                string questId = "math.g" + grade + ".s" + (int)strand;
                imported.Progress.ClaimedQuestIds.Add(questId);
                var originalIds = Enumerable.Range(1, 6).Select(i => "math-g" + grade + "-s" + (int)strand + "-" + i.ToString("00")).ToArray();
                foreach (string id in originalIds)
                    imported.Progress.Attempts.Add(new QuestionAttempt { QuestionId = id, Grade = grade, Subject = strand, Correct = true });
                var newIds = new HashSet<string>();
                for (int i = 0; i < 2; i++)
                {
                    var added = learning.BeginQuestion(imported, strand, Now);
                    Check(added != null && (added.Id.EndsWith("-07") || added.Id.EndsWith("-08")) && newIds.Add(added.Id),
                        "completed legacy topic receives only distinct added tasks");
                    Check(learning.SubmitAnswer(imported, added.Id, added.CorrectOption, Now).Correct,
                        "new task remains rewardable after importing original six answers");
                }
                Check(learning.BeginQuestion(imported, strand, Now) == null && imported.Progress.Attempts.Count == 8,
                    "expanded pool completes after two added answers without replaying original work");
                Check(originalIds.All(id => imported.Progress.Attempts.Count(a => a.QuestionId == id) == 1)
                    && imported.Progress.XP > 410 && imported.Progress.Coins > 217,
                    "upgrade preserves historical answers and existing balances");
                var importedBalance = Snapshot(imported.Progress);
                Check(!learning.ClaimQuest(imported, questId, Now).Claimed && Snapshot(imported.Progress) == importedBalance,
                    "catalog expansion cannot reopen an already claimed topic reward");
            }
        }

        var legacy = new PlayerProfile { Grade = 0, SchemaVersion = 1 };
        legacy.Progress.XP = 73; legacy.Progress.Coins = 52;
        legacy.Progress.Attempts.Add(new QuestionAttempt { QuestionId = "old-question", Subject = Subject.Mathematics, Grade = 0 });
        ProfileRules.Normalize(legacy);
        Check(legacy.Grade == 7 && legacy.SchemaVersion == 2 && legacy.Progress.XP == 73 && legacy.Progress.Coins == 52,
            "legacy migration defaults grade while preserving earned progression");
        Check(learning.GetQuests(legacy, Now).First(q => q.Id == "math.g7.s0").Current == 0, "old non-math attempts do not complete new grade quests");
        var switchProfile = new PlayerProfile { Grade = 7 };
        var oldActive = learning.BeginQuestion(switchProfile, Subject.Mathematics, Now);
        switchProfile.Grade = 9;
        Check(!learning.SubmitAnswer(switchProfile, oldActive.Id, oldActive.CorrectOption, Now).Accepted, "grade switch invalidates an old active answer");
        Check(learning.BeginQuestion(switchProfile, Subject.Mathematics, Now).Grade == 9, "new grade selects its own content");

        var pendingLegacy = new PlayerProfile { Grade = 8 };
        foreach (int index in Enumerable.Range(1, 5))
            pendingLegacy.Progress.Attempts.Add(new QuestionAttempt { QuestionId = "math-g8-s2-" + index.ToString("00"),
                Grade = 8, Subject = Subject.Nature, Correct = true });
        pendingLegacy.Progress.ActiveQuestionId = "math-g8-s2-06";
        var pendingQuestion = learning.BeginQuestion(pendingLegacy, Subject.Nature, Now);
        Check(pendingQuestion.Id == "math-g8-s2-06", "an unanswered original active question survives the bank expansion");
        Check(learning.SubmitAnswer(pendingLegacy, pendingQuestion.Id, pendingQuestion.CorrectOption, Now).Accepted,
            "pending original question remains answerable under its stable mapping");
        Check(learning.BeginQuestion(pendingLegacy, Subject.Nature, Now).Id.EndsWith("-07"),
            "finishing the pending original question unlocks the new multistep content");

        var tests = new StudyTestService(catalog); var profile = new PlayerProfile { Grade = 8 };
        var test = tests.CreateTest(profile, Subject.History, 42);
        var same = tests.CreateTest(profile, Subject.History, 42);
        Check(test.QuestionIds.SequenceEqual(same.QuestionIds) && test.Id != same.Id, "seed reproduces order but sessions have separate IDs");
        Check(!tests.Results(test).Completed && tests.Results(test).WrongAnswers.Length == 0, "unfinished test does not expose a final answer review");
        Check(!tests.Submit(test, test.QuestionIds[5], 0).Accepted, "out-of-order test answer rejected");
        Check(!tests.Submit(test, "unknown", 0).Accepted && !tests.Submit(test, test.QuestionIds[0], 4).Accepted, "invalid test inputs rejected");
        for (int i = 0; i < 6; i++)
        {
            var q = tests.CurrentQuestion(test); int choice = i == 2 ? (q.CorrectOption + 1) % 4 : q.CorrectOption;
            Check(tests.Submit(test, q.Id, choice).Accepted, "ordered test answer accepted");
            Check(tests.Submit(test, q.Id, choice).AlreadyAnswered, "test replay rejected");
        }
        var result = tests.Results(test);
        Check(result.Completed && result.CorrectCount == 5 && result.Percent == 83 && result.Passed, "computed test result uses submitted answers");
        Check(result.WrongAnswers.Length == 1 && result.WrongAnswers[0].Explanation.Length > 35
            && result.WrongAnswers[0].CorrectAnswer != result.WrongAnswers[0].SelectedAnswer, "wrong answer review includes solution and selected answer");
        Check(tests.ClaimReward(profile, test).Claimed, "first passed full test earns reward");
        var after = Snapshot(profile.Progress);
        Check(!tests.ClaimReward(profile, test).Claimed && Snapshot(profile.Progress) == after, "test reward replay cannot change balance");
        var repeat = tests.CreateTest(profile, Subject.History, 99);
        Complete(tests, repeat, true);
        Check(!tests.ClaimReward(profile, repeat).Claimed && Snapshot(profile.Progress) == after, "new seed/session cannot farm grade/topic reward");
        var failed = tests.CreateTest(profile, Subject.Languages, 10); Complete(tests, failed, false);
        Check(!tests.Results(failed).Passed && !tests.ClaimReward(profile, failed).Claimed, "failed test gives no pass reward");
        var shortTest = tests.CreateTest(profile, Subject.Nature, 3, 1); Complete(tests, shortTest, true);
        Check(!tests.ClaimReward(profile, shortTest).Claimed, "single-question practice cannot claim full-test reward");
        var malformed = tests.CreateTest(profile, Subject.Nature, 4); malformed.QuestionIds[1] = malformed.QuestionIds[0];
        Check(tests.CurrentQuestion(malformed) == null && !tests.Results(malformed).Completed, "duplicate test question IDs rejected");
        var invalidAnswer = tests.CreateTest(profile, Subject.Nature, 4); invalidAnswer.Answers[0] = 5;
        Check(!tests.Results(invalidAnswer).Completed && tests.CurrentQuestion(invalidAnswer) == null, "tampered option index rejected before review indexing");
        var another = tests.CreateTest(profile, Subject.Nature, 8); Complete(tests, another, true); profile.Grade = 7;
        Check(!tests.ClaimReward(profile, another).Claimed, "test cannot award under the wrong grade");
        profile.Grade = 8; Check(tests.ClaimReward(profile, another).Claimed, "returning to test grade permits its legitimate first pass reward");

        var oldStudy = new StudyTest { Id = "saved-before-numerion-expansion", Grade = 7, Subject = Subject.Mathematics,
            QuestionIds = Enumerable.Range(1, 6).Select(i => "math-g7-s0-" + i.ToString("00")).ToArray(),
            Answers = Enumerable.Repeat(-1, 6).ToArray() };
        Check(tests.CurrentQuestion(oldStudy)?.Id == "math-g7-s0-01", "saved six-question test remains valid in the expanded eight-question pool");
        Complete(tests, oldStudy, true);
        Check(tests.Results(oldStudy).CorrectCount == 6 && tests.Results(oldStudy).Percent == 100,
            "saved original test grades against unchanged historical question values");
        var oldStudyProfile = new PlayerProfile { Grade = 7 };
        Check(tests.ClaimReward(oldStudyProfile, oldStudy).Claimed, "completed pre-expansion test still receives its legitimate first reward");
        var oldRewardBalance = Snapshot(oldStudyProfile.Progress);
        var replacement = tests.CreateTest(oldStudyProfile, Subject.Mathematics, 91); Complete(tests, replacement, true);
        Check(!tests.ClaimReward(oldStudyProfile, replacement).Claimed && Snapshot(oldStudyProfile.Progress) == oldRewardBalance,
            "added question IDs cannot farm the old grade-topic test reward");
        var sampled = new HashSet<string>();
        for (int seed = 1; seed <= 32; seed++)
        {
            var sample = tests.CreateTest(new PlayerProfile { Grade = 9 }, Subject.History, seed);
            Check(sample.QuestionIds.Length == 6 && sample.QuestionIds.Distinct().Count() == 6,
                "default topic tests remain six unique questions after bank expansion");
            foreach (string id in sample.QuestionIds) sampled.Add(id);
        }
        Check(sampled.Count == 8 && sampled.Contains("math-g9-s3-07") && sampled.Contains("math-g9-s3-08"),
            "seeded topic tests can draw both added questions as well as all original ones");
        var boundaryProfile = new PlayerProfile { Grade = 9 };
        var boundary = tests.CreateTest(boundaryProfile, Subject.Science, 13);
        for (int i = 0; i < 6; i++)
        {
            var q = tests.CurrentQuestion(boundary);
            Check(tests.Submit(boundary, q.Id, i < 4 ? q.CorrectOption : (q.CorrectOption + 1) % 4).Accepted,
                "boundary test records four correct and two incorrect answers");
        }
        Check(tests.Results(boundary).Percent == 66 && !tests.Results(boundary).Passed && !tests.ClaimReward(boundaryProfile, boundary).Claimed,
            "four of six remains below the unchanged seventy-percent reward threshold");
        Console.WriteLine("NUMERION_MATH_HARNESS_PASS: " + checks + " assertions; 144 questions, 18 unchanged lessons, saved-topic/test migration, adaptive grades and anti-replay rewards.");
    }

    private static void Complete(StudyTestService tests, StudyTest test, bool correct)
    {
        while (!test.Completed)
        {
            var q = tests.CurrentQuestion(test);
            var answer = tests.Submit(test, q.Id, correct ? q.CorrectOption : (q.CorrectOption + 1) % 4);
            Check(answer.Accepted, "helper completes a valid ordered test");
        }
    }
}
