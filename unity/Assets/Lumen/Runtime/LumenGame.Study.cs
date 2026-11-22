using System;
using Lumen.Core;

namespace Lumen.Runtime
{
    public sealed partial class LumenGame
    {
        private StudyTestService studyTests;
        public StudyTestService StudyTests => studyTests ??= new StudyTestService(Catalog);
        public StudyAnswerResult LastStudyAnswer { get; private set; }
        public string StudyRewardMessage { get; private set; } = "";

        public void SetStudyGrade(int grade)
        {
            grade = CurriculumLabels.NormalizeGrade(grade);
            if (Profile.Grade == grade) return;
            Profile.Progress.ActiveQuestionId = ""; Profile.Progress.ActiveQuestionIsPractice = false;
            CurrentQuestion = null;
            var look = Profile.Appearance;
            ApplyProfile(Profile.DisplayName, AgeBand.Ages13To15, look.SkinTone, look.OutfitColor, look.HairStyle, look.Accessory, grade);
            Notify("Matematika · " + CurriculumLabels.Grade(grade) + ". Tvoja postava a zbierka zostávajú zachované.");
        }

        public void BeginStudyTest(Subject topic, bool replaceCurrent = false)
        {
            if (!replaceCurrent && Save.ActiveStudyTest != null && !Save.ActiveStudyTest.Completed)
            {
                Hud.ShowStudyReplacePrompt(topic); return;
            }
            int seed = unchecked((int)DateTime.UtcNow.Ticks);
            Save.ActiveStudyTest = StudyTests.CreateTest(Profile, topic, seed, 6);
            LastStudyAnswer = null; StudyRewardMessage = "";
            ProfileStore.Save(Save); Hud.ShowStudyTest();
        }

        public void SubmitStudyAnswer(int option)
        {
            var test = Save.ActiveStudyTest;
            if (test == null) { Hud.ShowStudy(); return; }
            if (test.Grade != Profile.Grade)
            { Notify("Tento test patrí do " + test.Grade + ". ročníka. Najprv sa vráť na jeho ročník."); Hud.ShowStudyTest(); return; }
            var question = StudyTests.CurrentQuestion(test);
            if (question == null) { Hud.ShowStudyResults(); return; }
            LastStudyAnswer = StudyTests.Submit(test, question.Id, option);
            if (LastStudyAnswer.Accepted) Audio?.Answer(LastStudyAnswer.Correct);
            if (LastStudyAnswer.Accepted && LastStudyAnswer.Correct)
                GrantArtifact("question:" + question.Id, question.Subject);
            if (test.Completed) RecordStudyCompletion();
            ProfileStore.Save(Save); Hud.ShowStudyFeedback(question, LastStudyAnswer);
        }

        public void RecordStudyCompletion()
        {
            var test = Save.ActiveStudyTest;
            if (test == null || !test.Completed) return;
            // The core service commits the durable first-pass claim, while the shared runtime
            // applies all balances AND the equipped companion's progression through one receipt.
            var reward = StudyTests.ClaimReward(Profile, test, false);
            if (reward.Claimed)
            {
                ApplyProgressReward("study:" + test.Id, reward.XP, reward.Coins, reward.PetXP);
                StudyRewardMessage = "+" + reward.XP + " XP · +" + reward.Coins + " mincí · +" + reward.PetXP + " XP spoločníka";
                Notify("Tematický test zvládnutý! " + StudyRewardMessage);
            }
            else if (test.RewardClaimed)
                StudyRewardMessage = "Odmena za prvé zvládnutie tejto témy je uložená v tvojom profile.";
            else StudyRewardMessage = reward.Error;
            ProfileStore.Save(Save);
        }
    }
}
