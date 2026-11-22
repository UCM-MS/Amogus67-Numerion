using System;
using System.Collections.Generic;
using System.Linq;
using Lumen.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        public void ShowStudy()
        {
            Open("Učebnice a tematické testy", "MATEMATIKA / KNIŽNICA"); FeaturePanel = "study";
            Label(body, "Pochop postup. Vyskúšaj príklad. Potom si over, čo už dokážeš sám.", 28, Ink);
            var grades = Row(body, 76);
            for (int value = 7; value <= 9; value++)
            {
                int grade = value;
                ButtonNode(grades, CurriculumLabels.Grade(grade), () => { game.SetStudyGrade(grade); ShowStudy(); },
                    game.Profile.Grade == grade ? Lavender : Color.white, Ink, 25);
            }
            var active = game.Save.ActiveStudyTest;
            if (active != null)
            {
                var resume = Card(body, Mint);
                Label(resume, active.Completed ? "Tvoj posledný test" : "Rozpracovaný test", 26, Purple);
                Label(resume, CurriculumLabels.Grade(active.Grade) + " · " + CurriculumLabels.SubjectName(active.Subject)
                    + " · " + active.NextIndex + "/" + active.QuestionIds.Length + " odpovedí", 24, Ink);
                ButtonNode(resume, active.Completed ? "Výsledky a vysvetlenia" : "Pokračovať v teste", () =>
                { game.SetStudyGrade(active.Grade); if (active.Completed) ShowStudyResults(); else ShowStudyTest(); }, Lime);
            }
            foreach (var section in game.Catalog.Sections)
            {
                Subject topic = section.Subject;
                var lessons = LessonCatalog.GetLessons(game.Profile.Grade, topic);
                var lesson = lessons.FirstOrDefault();
                var card = Card(body, Cream);
                Label(card, section.Name, 30, Ink);
                Label(card, lesson == null ? section.Description : lesson.Title + "\n" + lesson.Takeaway, 24, Muted);
                bool passed = game.Profile.Progress.ClaimedQuestIds.Contains("study.pass.g" + game.Profile.Grade + ".s" + (int)topic);
                Label(card, passed ? "✓ Prvý tematický test zvládnutý · ďalšie pokusy sú precvičovanie" : "6 otázok · prvá úspešnosť aspoň 70 %: +50 XP, +40 mincí, +25 XP spoločníka", 22, passed ? Purple : Muted);
                if (lesson != null) ButtonNode(card, "Otvoriť učebnicu  →", () => ShowLesson(lesson), Lavender, Ink, 25);
                ButtonNode(card, "Tematický test  →", () => game.BeginStudyTest(topic), Lime, Ink, 25);
                ButtonNode(card, "Voľné precvičovanie", () => game.BeginQuiz(topic), Mint, Ink, 23);
            }
            Label(body, "Otázky sú určené na učenie, nie na časový tlak. Texty a výsledky môžeš kedykoľvek posúvať; test sa priebežne ukladá.", 23, Muted);
        }

        public void ShowLesson(Lesson lesson)
        {
            if (lesson == null) { ShowStudy(); return; }
            Open(lesson.Title, "UČEBNICA / " + CurriculumLabels.Grade(lesson.Grade).ToUpperInvariant()); FeaturePanel = "lesson";
            Label(body, CurriculumLabels.SubjectName(lesson.Subject), 26, Purple);
            var theory = Card(body, Cream);
            Label(theory, "POCHOP PRINCÍP", 24, Purple);
            Label(theory, lesson.Explanation, 27, Ink);
            var example = Card(body, Lavender);
            Label(example, "VZOROVÝ PRÍKLAD KROK ZA KROKOM", 24, Purple);
            Label(example, lesson.WorkedExample, 29, Ink);
            var note = Card(body, Mint);
            Label(note, "ZAPAMÄTAJ SI", 24, Purple);
            Label(note, lesson.Takeaway, 27, Ink);
            var current = game.Save.ActiveStudyTest;
            if (current != null && !current.Completed && current.Grade == lesson.Grade && current.Subject == lesson.Subject)
                ButtonNode(body, "Pokračovať v rozpracovanom teste  →", () => { game.SetStudyGrade(lesson.Grade); ShowStudyTest(); }, Lime);
            ButtonNode(body, "Overím si to · tematický test  →", () => { game.SetStudyGrade(lesson.Grade); game.BeginStudyTest(lesson.Subject); }, Lime);
            ButtonNode(body, "Späť do knižnice", ShowStudy, Lavender);
            Label(body, "Tematický rozsah vychádza zo štandardu matematiky pre ZŠ. Rozloženie učiva môže škola upraviť vo svojom programe.", 21, Muted);
        }

        public void ShowStudyReplacePrompt(Subject requested)
        {
            var existing = game.Save.ActiveStudyTest;
            if (existing == null || existing.Completed) { game.BeginStudyTest(requested, true); return; }
            Open("Pokračovať alebo začať nový test?", "UČENIE / ROZPRACOVANÝ TEST"); FeaturePanel = "study";
            Label(body, "Máš rozpracovaný test: " + CurriculumLabels.SubjectName(existing.Subject) + " · " + CurriculumLabels.Grade(existing.Grade)
                + " · " + existing.NextIndex + "/" + existing.QuestionIds.Length + " odpovedí.", 28, Ink);
            Label(body, "Pri novom teste sa rozpracované odpovede nahradia. Tvoj získaný postup, peti a odmeny zostanú uložené.", 25, Muted);
            ButtonNode(body, "Pokračovať v rozpracovanom teste", () => { game.SetStudyGrade(existing.Grade); ShowStudyTest(); }, Lime);
            ButtonNode(body, "Začať nový · " + CurriculumLabels.SubjectName(requested), () => game.BeginStudyTest(requested, true), Lavender);
            ButtonNode(body, "Späť do knižnice", ShowStudy, Cream);
        }

        public void ShowStudyTest()
        {
            var test = game.Save.ActiveStudyTest;
            if (test == null) { ShowStudy(); return; }
            if (test.Completed) { ShowStudyResults(); return; }
            Open("Tematický test", "MATEMATIKA / " + CurriculumLabels.SubjectName(test.Subject).ToUpperInvariant()); FeaturePanel = "study-test";
            if (game.Profile.Grade != test.Grade)
            {
                Label(body, "Rozpracovaný test patrí do " + CurriculumLabels.Grade(test.Grade) + ". Pokračovať môžeš po návrate na tento ročník.", 28, Ink);
                ButtonNode(body, "Pokračovať · " + CurriculumLabels.Grade(test.Grade), () => { game.SetStudyGrade(test.Grade); ShowStudyTest(); }, Lime);
                ButtonNode(body, "Späť do knižnice", ShowStudy, Lavender); return;
            }
            var question = game.StudyTests.CurrentQuestion(test);
            if (question == null)
            {
                Label(body, "Rozpracovaný test sa nedá načítať. V knižnici môžeš začať nový; tvoj doterajší postup zostáva uložený.", 27, Ink);
                ButtonNode(body, "Nový test tejto témy", () => game.BeginStudyTest(test.Subject, true), Lime);
                ButtonNode(body, "Späť do knižnice", ShowStudy, Lavender); return;
            }
            Label(body, CurriculumLabels.Grade(test.Grade) + " · otázka " + (test.NextIndex + 1) + "/" + test.QuestionIds.Length + " · náročnosť " + question.Difficulty + "/4", 24, Purple);
            ProgressBar(body, test.NextIndex / (float)test.QuestionIds.Length, Purple);
            Label(body, question.Prompt, 34, Ink);
            if (game.Save.Options.PetHelp) ButtonNode(body, "Pomoc od spoločníka", ShowPetHelper, Mint, Ink, 23);
            var answers = new List<Button>();
            for (int i = 0; i < question.Options.Length; i++)
            {
                int choice = i;
                var answer = ButtonNode(body, ((char)('A' + i)) + "  " + question.Options[i], () =>
                { foreach (var b in answers) b.interactable = false; game.SubmitStudyAnswer(choice); }, i % 2 == 0 ? Lavender : Mint, Ink, 28);
                answers.Add(answer);
            }
            Label(body, "Bez časového limitu. Po odpovedi uvidíš vysvetlenie a na konci rozbor chýb. Za nové správne riešenie môžeš získať artefakt iba raz.", 23, Muted);
            ButtonNode(body, "Odložiť test a otvoriť knižnicu", ShowStudy, Cream, Ink, 23);
        }

        public void ShowStudyFeedback(Question question, StudyAnswerResult answer)
        {
            Open(answer.Accepted ? answer.Correct ? "Správne. Rozumieš tomu!" : "Chyba je ďalší krok k pochopeniu." : "Odpoveď sa neuložila", "TEST / VYSVETLENIE"); FeaturePanel = "study-feedback";
            if (!answer.Accepted)
            {
                Label(body, answer.Error, 28, Ink); ButtonNode(body, "Vrátiť sa k otázke", ShowStudyTest, Lime); return;
            }
            Label(body, question.Prompt, 28, Ink);
            var solved = Card(body, answer.Correct ? Mint : Lavender);
            Label(solved, "Správna odpoveď: " + question.Options[answer.CorrectOption], 31, Purple);
            Label(solved, answer.Explanation, 29, Ink);
            var lesson = LessonCatalog.GetLessons(question.Grade, question.Subject).FirstOrDefault();
            if (lesson != null) ButtonNode(body, "Potrebujem postup z učebnice", () => ShowLesson(lesson), Cream, Ink, 24);
            ButtonNode(body, answer.Completed ? "Zobraziť výsledok testu  →" : "Ďalšia otázka  →", answer.Completed ? (Action)ShowStudyResults : ShowStudyTest, Lime, Ink, 28);
        }

        public void ShowStudyResults()
        {
            var test = game.Save.ActiveStudyTest;
            if (test == null) { ShowStudy(); return; }
            var results = game.StudyTests.Results(test);
            if (!results.Completed)
            {
                if (!test.Completed) { ShowStudyTest(); return; }
                Open("Test sa nedá vyhodnotiť", "MATEMATIKA / TEST"); FeaturePanel = "study";
                Label(body, "Uložený test neobsahuje platnú postupnosť odpovedí. Tvoj ostatný postup zostáva uložený; môžeš začať nový test.", 27, Ink);
                ButtonNode(body, "Nový test tejto témy", () => game.BeginStudyTest(test.Subject, true), Lime);
                ButtonNode(body, "Späť do knižnice", ShowStudy, Lavender); return;
            }
            game.RecordStudyCompletion();
            Open(results.Passed ? "Tému máš v rukách!" : "Pozri sa na postup a skús to znovu.", "TEST / VÝSLEDKY A ROZBOR"); FeaturePanel = "study-results";
            var score = Card(body, results.Passed ? Mint : Lavender);
            Label(score, results.Percent + " %", 60, results.Passed ? Purple : Ink);
            Label(score, results.CorrectCount + " zo " + results.TotalCount + " správne · " + CurriculumLabels.Grade(test.Grade) + " · " + CurriculumLabels.SubjectName(test.Subject), 28, Ink);
            ProgressBar(score, results.Percent / 100f, Purple);
            Label(score, "Hranica zvládnutia: 70 %. Výsledok pomáha vybrať, čo precvičiť ďalej.", 23, Muted);
            Label(body, game.StudyRewardMessage, 25, Purple);
            if (results.WrongAnswers.Length == 0) Label(body, "✓ Všetky odpovede sú správne. Skús výzvu inej témy alebo ročníka.", 27, Purple);
            else
            {
                Label(body, "TVOJE CHYBY · VYSVETLENÉ RIEŠENIA", 27, Ink);
                foreach (var review in results.WrongAnswers)
                {
                    var card = Card(body, Cream);
                    Label(card, review.Prompt, 28, Ink);
                    Label(card, "Tvoja odpoveď: " + review.SelectedAnswer, 24, Muted);
                    Label(card, "Správne: " + review.CorrectAnswer, 27, Purple);
                    Label(card, review.Explanation, 26, Ink);
                }
            }
            ButtonNode(body, "Zopakovať test s novým poradím", () => { game.SetStudyGrade(test.Grade); game.BeginStudyTest(test.Subject, true); }, Lime);
            var lesson = LessonCatalog.GetLessons(test.Grade, test.Subject).FirstOrDefault();
            if (lesson != null) ButtonNode(body, "Otvoriť učebnicu tejto témy", () => ShowLesson(lesson), Lavender);
            ButtonNode(body, "Vybrať ďalšiu tému", ShowStudy, Cream);
            ButtonNode(body, "Späť do sveta", CloseModal, Cream);
        }
    }
}
