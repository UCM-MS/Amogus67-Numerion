using System;
using System.Linq;
using System.Text.Json;
using Lumen.Core;

internal static class Program
{
    private static readonly DateTime Today = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;
    private static int Main(string[] args)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine("CoreHarness only validates the curriculum. Use unity/Tests/MathHarness/generate_content.py to regenerate it with its lesson and proof data.");
            return 2;
        }

        try
        {
            var catalog = ContentCatalog.CreateDefault();
            CatalogChecks(catalog);
            IdentityChecks();
            SelectionChecks(catalog);
            RewardChecks(catalog);
            AdaptiveChecks(catalog);
            QuestChecks(catalog);
            PersistenceChecks(catalog);
            Console.WriteLine("PASS: " + assertions + " curriculum/progression assertions; " + catalog.Questions.Count + " curated questions.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Check(bool value, string label)
    {
        assertions++;
        if (!value) throw new InvalidOperationException("FAIL: " + label);
    }

    private static void CatalogChecks(ContentCatalog catalog)
    {
        Check(catalog.Questions.Count == 108, "108 authored mathematics questions");
        Check(catalog.Sections.Count == 6, "Six mathematical strands");
        Check(catalog.Sections.Select(s => s.Id).Distinct().Count() == 6, "Unique section IDs");
        Check(catalog.Questions.Select(q => q.Id).Distinct().Count() == 108, "Unique question IDs");
        Check(catalog.Questions.Select(q => q.Prompt).Distinct().Count() == 108, "Unique prompts");
        Check(catalog.Questions.All(q => q.AgeBand == AgeBand.Ages13To15), "Legacy age DTO remains coherent with grades 7–9");
        foreach (int grade in new[] { 7, 8, 9 })
        foreach (Subject subject in Enum.GetValues(typeof(Subject)))
        {
            var group = catalog.Questions.Where(q => q.Grade == grade && q.Subject == subject).ToArray();
            Check(group.Length == 6, "Six questions per grade/strand cell");
            Check(group.Select(q => q.Difficulty).OrderBy(d => d).SequenceEqual(new[] { 1, 2, 2, 3, 3, 4 }), "Introductory, intermediate and advanced difficulty progression");
            Check(group.Select(q => q.CorrectOption).Distinct().Count() == 4, "Every answer position used in each grade/strand");
            Check(group.Select(q => q.Id).OrderBy(id => id).SequenceEqual(Enumerable.Range(1, 6).Select(i => "math-g" + grade + "-s" + (int)subject + "-" + i.ToString("00"))), "Stable complete topic ID range");
            Check(catalog.Sections.Single(s => s.Subject == subject).Name == CurriculumLabels.SubjectName(subject), "Display label matches the mathematical strand");
        }
        foreach (int grade in new[] { 7, 8, 9 })
            for (int option = 0; option < 4; option++)
                Check(catalog.Questions.Count(q => q.Grade == grade && q.CorrectOption == option) == 9, "Answer positions balanced over all 36 tasks in grade " + grade);
        foreach (var q in catalog.Questions)
        {
            Check(q.Options.Length == 4 && q.Options.Distinct().Count() == 4, "Four distinct plausible options: " + q.Id);
            Check(q.Options.All(option => !string.IsNullOrWhiteSpace(option)), "No empty option: " + q.Id);
            Check(q.Explanation.Length >= 35, "Explanation present: " + q.Id);
            Check(q.Grade >= 7 && q.Grade <= 9 && q.Difficulty >= 1 && q.Difficulty <= 4, "Supported grade and difficulty: " + q.Id);
            Check(Uri.TryCreate(q.SourceUrl, UriKind.Absolute, out var source) && source.Scheme == "https" && source.Host == "www.statpedu.sk", "Mathematics curriculum source attached: " + q.Id);
            Check(catalog.Sections.Any(section => section.Id == q.SectionId && section.Subject == q.Subject), "Section link: " + q.Id);
            Check(ReferenceEquals(catalog.Find(q.Id), q), "Lookup returns same question: " + q.Id);
        }
        Check(catalog.Find(null) == null && catalog.Find("missing") == null, "Unknown lookup safe");
        bool duplicateRejected = false;
        try { catalog.Add(catalog.Questions[0]); } catch (ArgumentException) { duplicateRejected = true; }
        Check(duplicateRejected, "Catalog rejects duplicate ID");
        var fraction = catalog.Find("math-g7-s0-06");
        Check(fraction.Options[fraction.CorrectOption] == "3/8", "Exact conversion of 0.375 into lowest terms");
        var scale = catalog.Find("math-g9-s1-06");
        Check(scale.Options[scale.CorrectOption] == "9", "The ratio of shadow lengths preserves the height scale");
        var graph = catalog.Find("math-g9-s4-06");
        Check(graph.Options[graph.CorrectOption] == "7", "Substituting x = 3 into y = 2x + 1 gives y = 7");
        CatalogRejectionChecks();
    }

    private static void CatalogRejectionChecks()
    {
        var isolated = new ContentCatalog();
        bool nullRejected = false;
        try { isolated.Add(null); } catch (ArgumentException) { nullRejected = true; }
        Check(nullRejected, "Null question rejected");
        foreach (Action<Question> corrupt in new Action<Question>[] {
            q => q.Id = " ", q => q.Options = null, q => q.Options = new[] { "1", "2", "3" },
            q => q.CorrectOption = -1, q => q.CorrectOption = 4,
            q => q.Difficulty = 0, q => q.Difficulty = 5, q => q.Grade = 6, q => q.Grade = 10,
            q => q.Explanation = " " })
        {
            var question = new Question { Id = "isolated-validation", Grade = 7, Difficulty = 1,
                Options = new[] { "1", "2", "3", "4" }, Explanation = "A complete worked mathematical explanation." };
            corrupt(question); bool rejected = false;
            try { isolated.Add(question); } catch (ArgumentException) { rejected = true; }
            Check(rejected && isolated.Questions.Count == 0, "Malformed question rejected without partially adding it");
        }
    }

    private static void IdentityChecks()
    {
        Check(ProfileRules.CleanName("  Žofia   Výskumníčka  ") == "Žofia Výskumníčka", "Slovak Unicode names and collapsed spaces");
        Check(ProfileRules.CleanName("<color=red>Hráč</color>").IndexOf('<') < 0, "Rich text delimiters removed");
        Check(ProfileRules.CleanName("     ") == "Objaviteľ", "Empty nickname fallback");
        Check(ProfileRules.CleanName(new string('a', 80)).Length == 20, "Nickname bounded");
        var p = new PlayerProfile { SchemaVersion = 1, Grade = 0, AgeBand = (AgeBand)99, Appearance = new PlayerAppearance { SkinTone = -3, OutfitColor = 20, HairStyle = 8, Accessory = -8 }, Progress = null };
        ProfileRules.Normalize(p);
        Check(p.AgeBand == AgeBand.Ages13To15 && p.Grade == 7 && p.SchemaVersion == 2 && p.Progress != null, "Legacy profile migrates to grade 7 schema 2 with compatible age DTO");
        Check(p.Appearance.SkinTone == 0 && p.Appearance.OutfitColor == 7 && p.Appearance.HairStyle == 3 && p.Appearance.Accessory == 0, "Cosmetics bounded");
        foreach (int grade in new[] { 7, 8, 9 })
        {
            var valid = new PlayerProfile { Grade = grade };
            ProfileRules.Normalize(valid); Check(valid.Grade == grade, "Supported grade preserved by normalization");
        }
        foreach (int grade in new[] { int.MinValue, 6, 10, int.MaxValue })
        {
            var invalid = new PlayerProfile { Grade = grade };
            ProfileRules.Normalize(invalid); Check(invalid.Grade == 7, "Unsupported grade defaults safely");
        }
    }

    private static void SelectionChecks(ContentCatalog catalog)
    {
        var service = new LearningService(catalog);
        foreach (int grade in new[] { 7, 8, 9 })
        foreach (Subject subject in Enum.GetValues(typeof(Subject)))
        {
            var p = new PlayerProfile { Grade = grade };
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 6; i++)
            {
                var question = service.BeginQuestion(p, subject, Today);
                Check(question != null && question.Grade == grade && question.Subject == subject, "Strict grade/strand filtering");
                Check(seen.Add(question.Id), "No repeated rewarded question");
                Check(ReferenceEquals(question, service.BeginQuestion(p, subject, Today)), "Opening active question does not reroll");
                Check(service.SubmitAnswer(p, question.Id, question.CorrectOption, Today).Accepted, "Valid active answer accepted");
            }
            Check(service.BeginQuestion(p, subject, Today) == null, "Subject exhaustion explicit");
            int xp = p.Progress.XP, coins = p.Progress.Coins, petXp = p.Progress.PetXP, count = p.Progress.Attempts.Count, streak = p.Progress.Streak;
            var practice = service.BeginQuestion(p, subject, Today, true);
            var result = service.SubmitAnswer(p, practice.Id, practice.CorrectOption, Today);
            Check(result.Accepted && result.Practice, "Explicit revision available after exhaustion");
            Check(result.XpAwarded == 0 && p.Progress.XP == xp && p.Progress.Coins == coins && p.Progress.PetXP == petXp, "Practice is not an XP/currency farm");
            Check(p.Progress.Attempts.Count == count && p.Progress.Streak == streak, "Practice cannot farm quests or streaks");
        }
        var mismatch = new PlayerProfile();
        var completed = service.BeginQuestion(mismatch, Subject.History, Today);
        Check(service.SubmitAnswer(mismatch, completed.Id, completed.CorrectOption, Today).Accepted,
            "Grade seven fixture earns progress before switching");
        var original = service.BeginQuestion(mismatch, Subject.History, Today);
        int earnedXp = mismatch.Progress.XP, earnedCoins = mismatch.Progress.Coins, earnedPetXp = mismatch.Progress.PetXP;
        mismatch.Grade = 9;
        Check(!service.SubmitAnswer(mismatch, original.Id, original.CorrectOption, Today).Accepted, "Grade change invalidates old question");
        Check(mismatch.Progress.XP == earnedXp && mismatch.Progress.Coins == earnedCoins && mismatch.Progress.PetXP == earnedPetXp
            && mismatch.Progress.Attempts.Count == 1, "Rejected previous-grade answer preserves the wallet and history");
        var replacement = service.BeginQuestion(mismatch, Subject.History, Today);
        Check(replacement.Grade == 9, "Grade change replaces active question");
        Check(service.SubmitAnswer(mismatch, replacement.Id, replacement.CorrectOption, Today).Accepted
            && mismatch.Progress.Attempts.Select(attempt => attempt.Grade).SequenceEqual(new[] { 7, 9 }),
            "Solving after the grade change records one attempt in each grade");
        var ageDto = new PlayerProfile { Grade = 8 };
        var gradeQuestion = service.BeginQuestion(ageDto, Subject.Mathematics, Today);
        ageDto.AgeBand = AgeBand.Ages6To9;
        Check(service.BeginQuestion(ageDto, Subject.Mathematics, Today) == gradeQuestion && ageDto.Grade == 8,
            "Legacy age field cannot reroll or select another grade's active mathematics question");
    }

    private static void RewardChecks(ContentCatalog catalog)
    {
        var service = new LearningService(catalog);
        var p = new PlayerProfile();
        var unsolicited = catalog.Questions[40];
        Check(!service.SubmitAnswer(p, unsolicited.Id, unsolicited.CorrectOption, Today).Accepted, "Unissued answer rejected");
        var wrong = service.BeginQuestion(p, Subject.Science, Today);
        var invalid = service.SubmitAnswer(p, wrong.Id, 4, Today);
        Check(!invalid.Accepted && p.Progress.ActiveQuestionId == wrong.Id, "Invalid option does not consume active question");
        var wrongResult = service.SubmitAnswer(p, wrong.Id, (wrong.CorrectOption + 1) % 4, Today);
        Check(wrongResult.Accepted && !wrongResult.Correct, "Wrong answer evaluated");
        Check(p.Progress.XP == 0 && p.Progress.Coins == 0 && p.Progress.PetXP == 0, "Wrong answer grants no rewards");
        var duplicate = service.SubmitAnswer(p, wrong.Id, wrong.CorrectOption, Today);
        Check(!duplicate.Accepted && duplicate.AlreadyAnswered && p.Progress.XP == 0, "Retry cannot convert wrong result into reward");
        var next = service.BeginQuestion(p, Subject.Science, Today);
        var correctResult = service.SubmitAnswer(p, next.Id, next.CorrectOption, Today);
        Check(correctResult.Correct && correctResult.XpAwarded > 0 && correctResult.CoinsAwarded > 0 && correctResult.PetXpAwarded > 0, "Correct answer rewards all progression tracks");
        var second = service.BeginQuestion(p, Subject.Mathematics, Today);
        var secondResult = service.SubmitAnswer(p, second.Id, second.CorrectOption, Today);
        Check(secondResult.Streak == 2 && secondResult.XpAwarded == 8 + second.Difficulty * 5 + 2, "Streak bonus follows successful answer");
        int xp = p.Progress.XP;
        Check(!service.SubmitAnswer(p, second.Id, second.CorrectOption, Today).Accepted && p.Progress.XP == xp, "Repeated successful answer cannot duplicate rewards");
        var breakQuestion = service.BeginQuestion(p, Subject.Nature, Today);
        service.SubmitAnswer(p, breakQuestion.Id, (breakQuestion.CorrectOption + 1) % 4, Today);
        Check(p.Progress.Streak == 0 && p.Progress.BestStreak == 2, "Wrong answer breaks current streak and keeps best");
    }

    private static void AdaptiveChecks(ContentCatalog catalog)
    {
        var service = new LearningService(catalog);
        var p = new PlayerProfile();
        Check(service.BeginQuestion(p, Subject.Mathematics, Today).Difficulty == 2, "Initial difficulty medium");
        for (int i = 0; i < 2; i++)
        {
            var q = service.BeginQuestion(p, Subject.Mathematics, Today);
            service.SubmitAnswer(p, q.Id, q.CorrectOption, Today);
        }
        Check(service.GetMastery(p, Subject.Mathematics).TargetDifficulty == 3, "Two correct increase difficulty");
        Check(service.GetMastery(p, Subject.Mathematics).Score > 0, "Mastery grows with correct answers");
        var struggling = new PlayerProfile();
        for (int i = 0; i < 2; i++)
        {
            var q = service.BeginQuestion(struggling, Subject.Mathematics, Today);
            service.SubmitAnswer(struggling, q.Id, (q.CorrectOption + 1) % 4, Today);
        }
        Check(service.GetMastery(struggling, Subject.Mathematics).TargetDifficulty == 1, "Two wrong reduce difficulty");
        Check(service.GetMastery(struggling, Subject.Mathematics).Score == 0, "Mastery cannot go negative");
        Check(service.GetMastery(p, Subject.History).TargetDifficulty == 2, "Difficulty tracked separately per subject");
    }

    private static void QuestChecks(ContentCatalog catalog)
    {
        var service = new LearningService(catalog);
        var p = new PlayerProfile { Grade = 9 };
        Check(service.GetQuests(p, Today).Count == 9, "Three daily and six section quests");
        var incomplete = service.GetQuests(p, Today)[0];
        Check(!service.ClaimQuest(p, incomplete.Id, Today).Claimed, "Incomplete quest cannot be claimed");
        foreach (var subject in new[] { Subject.Mathematics, Subject.Science, Subject.Nature })
        {
            var q = service.BeginQuestion(p, subject, Today);
            service.SubmitAnswer(p, q.Id, q.CorrectOption, Today);
        }
        var daily = service.GetQuests(p, Today).Take(3).ToArray();
        Check(daily.All(q => q.Complete), "Daily exploration, streak and horizons become achievable");
        foreach (var q in daily)
        {
            int xp = p.Progress.XP, coins = p.Progress.Coins, pet = p.Progress.PetXP;
            var claim = service.ClaimQuest(p, q.Id, Today);
            Check(claim.Claimed && p.Progress.XP == xp + q.XP && p.Progress.Coins == coins + q.Coins && p.Progress.PetXP == pet + q.PetXP, "Quest applies exact advertised reward");
            Check(!service.ClaimQuest(p, q.Id, Today).Claimed && p.Progress.XP == xp + q.XP, "Quest exact-once claim");
        }
        var tomorrow = service.GetQuests(p, Today.AddDays(1)).Take(3).ToArray();
        Check(tomorrow.All(q => !q.Complete && !q.Claimed), "UTC day rollover resets daily goals");
        Check(!service.ClaimQuest(p, daily[0].Id, Today.AddDays(1)).Claimed, "Expired daily ID cannot be replayed");
        for (int i = 1; i < 4; i++)
        {
            var q = service.BeginQuestion(p, Subject.Mathematics, Today);
            service.SubmitAnswer(p, q.Id, q.CorrectOption, Today);
        }
        var section = service.GetQuests(p, Today).Single(q => q.Id == "math.g9.s0");
        Check(section.Complete && service.ClaimQuest(p, section.Id, Today).Claimed, "Four unique questions complete grade-specific topic quest");
        Check(service.CompletedSections(p) == 1, "Completed sections count");
        p.Grade = 7;
        Check(service.CompletedSections(p) == 0, "Grades have separate topic goals");
        Check(!service.GetQuests(p, Today).Single(q => q.Id == "math.g7.s0").Complete, "Grade switch does not inherit grade 9 topic completion");
        Check(!service.ClaimQuest(p, "math.g9.s0", Today).Claimed, "Another grade's quest ID cannot be claimed under the new grade");
        Check(p.Progress.Level >= 2 && p.Progress.PetLevel >= 2, "Learning and quests level up player and pet");
        Check(service.GetUnlocks(p).Contains("Bronzový odznak objaviteľa"), "Progression unlock exists");
    }

    private static void PersistenceChecks(ContentCatalog catalog)
    {
        var service = new LearningService(catalog);
        var p = new PlayerProfile { DisplayName = "Nina", Grade = 8 };
        p.Appearance.OutfitColor = 5;
        for (int i = 0; i < 3; i++)
        {
            var q = service.BeginQuestion(p, Subject.Technology, Today);
            service.SubmitAnswer(p, q.Id, q.CorrectOption, Today);
        }
        var daily = service.GetQuests(p, Today)[0];
        service.ClaimQuest(p, daily.Id, Today);
        var active = service.BeginQuestion(p, Subject.History, Today);
        var settings = new JsonSerializerOptions { IncludeFields = true };
        var json = JsonSerializer.Serialize(p, settings);
        var loaded = JsonSerializer.Deserialize<PlayerProfile>(json, settings);
        ProfileRules.Normalize(loaded);
        Check(loaded.DisplayName == "Nina" && loaded.Appearance.OutfitColor == 5 && loaded.Grade == 8 && loaded.SchemaVersion == 2 && loaded.AgeBand == AgeBand.Ages13To15, "Identity and current grade/schema roundtrip");
        Check(loaded.Progress.XP == p.Progress.XP && loaded.Progress.PetXP == p.Progress.PetXP && loaded.Progress.Attempts.Count == 3, "Progress roundtrip");
        Check(!service.ClaimQuest(loaded, daily.Id, Today).Claimed, "Claim guard persists after reload");
        Check(service.SubmitAnswer(loaded, active.Id, active.CorrectOption, Today).Accepted, "Issued question survives reload");
        int xp = loaded.Progress.XP;
        Check(!service.SubmitAnswer(loaded, active.Id, active.CorrectOption, Today).Accepted && loaded.Progress.XP == xp, "Duplicate guard survives reload");
    }
}
