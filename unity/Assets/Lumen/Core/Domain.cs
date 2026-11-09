using System;
using System.Collections.Generic;

namespace Lumen.Core
{
    public enum AgeBand { Ages6To9, Ages10To12, Ages13To15, Ages16Plus }
    public enum Subject { Mathematics, Science, Nature, History, Languages, Technology }

    /// <summary>Public fields intentionally support Unity JsonUtility and server serializers.</summary>
    [Serializable]
    public sealed class PlayerAppearance
    {
        public int SkinTone;
        public int OutfitColor;
        public int HairStyle;
        public int Accessory;
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public int SchemaVersion = 2;
        public string DisplayName = "Objaviteľ";
        public AgeBand AgeBand = AgeBand.Ages10To12;
        public int Grade = 7;
        public PlayerAppearance Appearance = new PlayerAppearance();
        public PlayerProgress Progress = new PlayerProgress();
    }

    [Serializable]
    public sealed class PlayerProgress
    {
        public int XP;
        public int Coins;
        public int PetXP;
        public int Streak;
        public int BestStreak;
        public List<QuestionAttempt> Attempts = new List<QuestionAttempt>();
        public List<MasteryRecord> Mastery = new List<MasteryRecord>();
        public List<string> ClaimedQuestIds = new List<string>();
        public string ActiveQuestionId = "";
        public bool ActiveQuestionIsPractice;
        public int QuestionSequence;
        public int Level { get { return 1 + XP / 150; } }
        public int PetLevel { get { return 1 + PetXP / 80; } }
    }

    [Serializable]
    public sealed class MasteryRecord
    {
        public Subject Subject;
        public int Grade;
        public int CorrectAnswers;
        public int TotalAnswers;
        public int Score;
        public int TargetDifficulty = 2;
        public int CorrectRun;
        public int WrongRun;
    }

    [Serializable]
    public sealed class QuestionAttempt
    {
        public string QuestionId = "";
        public Subject Subject;
        public AgeBand AgeBand;
        public int Grade;
        public int Difficulty;
        public bool Correct;
        public string UtcDay = "";
        public int XpAwarded;
        public int CoinsAwarded;
        public int PetXpAwarded;
        public int StreakAfter;
    }

    [Serializable]
    public sealed class Question
    {
        public string Id = "";
        public Subject Subject;
        public AgeBand AgeBand;
        public int Grade;
        public int Difficulty;
        public string SectionId = "";
        public string Prompt = "";
        public string[] Options = new string[0];
        public int CorrectOption;
        public string Explanation = "";
        public string SourceUrl = "";
    }

    [Serializable]
    public sealed class Section
    {
        public string Id = "";
        public Subject Subject;
        public string Name = "";
        public string Description = "";
        public int UnlockLevel = 1;
    }

    public sealed class AnswerResult
    {
        public bool Accepted;
        public bool Correct;
        public bool AlreadyAnswered;
        public bool Practice;
        public string Explanation = "";
        public string Error = "";
        public int CorrectOption;
        public int XpAwarded;
        public int CoinsAwarded;
        public int PetXpAwarded;
        public int Streak;
        public int NewLevel;
    }

    public sealed class RewardResult
    {
        public bool Claimed;
        public string Error = "";
        public int XP;
        public int Coins;
        public int PetXP;
    }

    public sealed class QuestView
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public int Current;
        public int Target;
        public int XP;
        public int Coins;
        public int PetXP;
        public bool Claimed;
        public bool Complete { get { return Current >= Target; } }
    }

    public static class CurriculumLabels
    {
        public static int NormalizeGrade(int grade) => grade >= 7 && grade <= 9 ? grade : 7;
        public static string Grade(int grade) => NormalizeGrade(grade).ToString() + ". ročník ZŠ";
        public static string Age(AgeBand age)
        {
            switch (age)
            {
                case AgeBand.Ages6To9: return "6 – 9 rokov";
                case AgeBand.Ages10To12: return "10 – 12 rokov";
                case AgeBand.Ages13To15: return "13 – 15 rokov";
                default: return "16+ rokov";
            }
        }

        public static string SubjectName(Subject subject)
        {
            switch (subject)
            {
                case Subject.Mathematics: return "Čísla a zlomky";
                case Subject.Science: return "Percentá a pomer";
                case Subject.Nature: return "Výrazy a rovnice";
                case Subject.History: return "Geometria";
                case Subject.Languages: return "Dáta a pravdepodobnosť";
                default: return "Matematika v praxi";
            }
        }
    }
}
