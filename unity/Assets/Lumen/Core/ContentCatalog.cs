using System;
using System.Collections.Generic;

namespace Lumen.Core
{
    /// <summary>Code-native catalog; constructing it never requires Unity or a JSON parser.</summary>
    public sealed class ContentCatalog
    {
        public readonly List<Question> Questions = new List<Question>();
        public readonly List<Section> Sections = new List<Section>();
        private readonly Dictionary<string, Question> byId = new Dictionary<string, Question>(StringComparer.Ordinal);

        public static ContentCatalog CreateDefault()
        {
            var catalog = new ContentCatalog();
            foreach (Subject subject in Enum.GetValues(typeof(Subject)))
                catalog.Sections.Add(new Section
                {
                    Id = SectionId(subject), Subject = subject, Name = CurriculumLabels.SubjectName(subject),
                    Description = SectionDescription(subject), UnlockLevel = 1
                });
            QuestionBank.Populate(catalog);
            return catalog;
        }

        public Question Find(string id)
        {
            Question result;
            return id != null && byId.TryGetValue(id, out result) ? result : null;
        }

        public void Add(Question question)
        {
            if (question == null || string.IsNullOrWhiteSpace(question.Id) || byId.ContainsKey(question.Id))
                throw new ArgumentException("Question identifier must be unique.");
            if (question.Options == null || question.Options.Length != 4 || question.CorrectOption < 0 || question.CorrectOption > 3)
                throw new ArgumentException("A question requires four options and a valid correct option.");
            if (question.Difficulty < 1 || question.Difficulty > 4)
                throw new ArgumentException("Difficulty must be in range 1 to 4.");
            if (question.Grade < 7 || question.Grade > 9)
                throw new ArgumentException("A mathematics question must belong to grade 7, 8 or 9.");
            if (string.IsNullOrWhiteSpace(question.Explanation))
                throw new ArgumentException("A question requires an explanation of its solution.");
            Questions.Add(question);
            byId.Add(question.Id, question);
        }

        public static string SectionId(Subject subject) { return "section-" + ((int)subject).ToString(); }

        private static string SectionDescription(Subject subject)
        {
            switch (subject)
            {
                case Subject.Mathematics: return "Zlomky, celé čísla, racionálne čísla, mocniny a odmocniny.";
                case Subject.Science: return "Percentá, pomer, mierka, priama a nepriama úmernosť.";
                case Subject.Nature: return "Poradie operácií, výrazy s premennou, lineárne rovnice a nerovnice.";
                case Subject.History: return "Obvody, obsahy, objemy, Pytagorova veta a priestorová predstavivosť.";
                case Subject.Languages: return "Kombinatorika, priemer, pravdepodobnosť, štatistika a grafy.";
                default: return "Slovné úlohy, jednotky, rozpočty a matematické rozhodovanie v živote.";
            }
        }
    }
}
