using System;
using Lumen.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Lumen.Networking
{
    public sealed partial class NetworkExplorer
    {
        public event Action<Question> QuestionReceived;
        public event Action<AnswerResult> QuizAnswerReceived;
        public event Action<string> QuizError;
        public int OnlineXP => onlineXP.Value;
        public int OnlineCorrect => onlineCorrect.Value;
        private readonly NetworkVariable<int> onlineXP = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> onlineCorrect = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private PlayerProfile serverLearningProfile;
        private LearningService serverLearning;
        private double lastQuizRequest = -10, lastQuizAnswer = -10;

        [Serializable]
        private sealed class PublicQuestion
        {
            public string Id;
            public int Subject;
            public int AgeBand;
            public int Grade;
            public int Difficulty;
            public string SectionId;
            public string Prompt;
            public string[] Options;
            public string SourceUrl;
        }

        [Serializable]
        private sealed class PublicAnswer
        {
            public bool Accepted, Correct, AlreadyAnswered, Practice;
            public string Explanation, Error;
            public int CorrectOption, XpAwarded, CoinsAwarded, PetXpAwarded, Streak, NewLevel;

            public PublicAnswer(AnswerResult result)
            {
                Accepted = result.Accepted; Correct = result.Correct; AlreadyAnswered = result.AlreadyAnswered;
                Practice = result.Practice; Explanation = result.Explanation; Error = result.Error;
                CorrectOption = result.CorrectOption; XpAwarded = result.XpAwarded; CoinsAwarded = result.CoinsAwarded;
                PetXpAwarded = result.PetXpAwarded; Streak = result.Streak; NewLevel = result.NewLevel;
            }
            public AnswerResult ToResult() => new AnswerResult {
                Accepted = Accepted, Correct = Correct, AlreadyAnswered = AlreadyAnswered, Practice = Practice,
                Explanation = Explanation, Error = Error, CorrectOption = CorrectOption, XpAwarded = XpAwarded,
                CoinsAwarded = CoinsAwarded, PetXpAwarded = PetXpAwarded, Streak = Streak, NewLevel = NewLevel
            };
        }

        private void InitializeQuizServer()
        {
            if (!IsServer) return;
            serverLearningProfile = new PlayerProfile();
            serverLearning = new LearningService(ContentCatalog.CreateDefault());
        }

        /// <summary>Age cohort is used privately on the authority; it is not a public NetworkVariable.</summary>
        public void BeginQuiz(Subject subject, AgeBand age, int grade = 7)
        {
            if (IsOwner && IsSpawned) BeginQuizRpc((int)subject, (int)age, grade);
        }

        public void SubmitQuizAnswer(string questionId, int option)
        {
            if (IsOwner && IsSpawned && !string.IsNullOrEmpty(questionId) && questionId.Length <= 100 && option >= 0 && option < 4)
                SubmitQuizAnswerRpc(new FixedString128Bytes(questionId), option);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void BeginQuizRpc(int subject, int ageBand, int grade, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId || serverLearning == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastQuizRequest < .5) return;
            lastQuizRequest = now;
            if (!Enum.IsDefined(typeof(Subject), subject) || !Enum.IsDefined(typeof(AgeBand), ageBand) || grade < 7 || grade > 9)
            { QuizErrorRpc("Neplatná sekcia alebo ročník. Vyber 7., 8. alebo 9. ročník ZŠ."); return; }
            serverLearningProfile.DisplayName = DisplayName;
            serverLearningProfile.AgeBand = (AgeBand)ageBand;
            serverLearningProfile.Grade = grade;
            Question question = serverLearning.BeginQuestion(serverLearningProfile, (Subject)subject, DateTime.UtcNow, true);
            if (question == null) { QuizErrorRpc("V tejto sekcii momentálne nie je dostupná otázka."); return; }
            // CorrectOption and Explanation are deliberately absent from the wire question.
            string payload = JsonUtility.ToJson(new PublicQuestion {
                Id = question.Id, Subject = (int)question.Subject, AgeBand = (int)question.AgeBand, Grade = question.Grade,
                Difficulty = question.Difficulty, SectionId = question.SectionId, Prompt = question.Prompt,
                Options = question.Options, SourceUrl = question.SourceUrl
            });
            ReceiveQuestionRpc(payload);
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void ReceiveQuestionRpc(string json)
        {
            PublicQuestion question = JsonUtility.FromJson<PublicQuestion>(json);
            QuestionReceived?.Invoke(new Question {
                Id = question.Id, Subject = (Subject)question.Subject, AgeBand = (AgeBand)question.AgeBand, Grade = question.Grade,
                Difficulty = question.Difficulty, SectionId = question.SectionId, Prompt = question.Prompt,
                Options = question.Options, SourceUrl = question.SourceUrl, CorrectOption = -1, Explanation = ""
            });
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitQuizAnswerRpc(FixedString128Bytes questionId, int option, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId || serverLearning == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastQuizAnswer < .2) return;
            lastQuizAnswer = now;
            AnswerResult answer = serverLearning.SubmitAnswer(serverLearningProfile, questionId.ToString(), option, DateTime.UtcNow);
            onlineXP.Value = serverLearningProfile.Progress.XP;
            if (answer.Accepted && answer.Correct && !answer.Practice) onlineCorrect.Value++;
            ReceiveQuizAnswerRpc(JsonUtility.ToJson(new PublicAnswer(answer)));
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void ReceiveQuizAnswerRpc(string json) => QuizAnswerReceived?.Invoke(JsonUtility.FromJson<PublicAnswer>(json).ToResult());

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void QuizErrorRpc(string message) => QuizError?.Invoke(message);
    }
}
