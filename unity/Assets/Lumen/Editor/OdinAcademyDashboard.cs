#if UNITY_EDITOR && ODIN_INSPECTOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lumen.Core;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Lumen.Editor
{
    /// <summary>Optional authoring dashboard. Odin is required only in the Unity Editor.</summary>
    public sealed class OdinAcademyDashboard : OdinEditorWindow
    {
        private const string ExportPath = "Assets/Lumen/Resources/Content/curriculum.json";
        private ContentCatalog catalog;
        private List<QuestionRow> visibleQuestions;
        private string validationReport = "Katalóg ešte nebol overený v tomto okne.";

        [MenuItem("LUMEN/Academy Dashboard", false, 1)]
        private static void OpenDashboard()
        {
            var window = GetWindow<OdinAcademyDashboard>("LUMEN Academy");
            window.minSize = new Vector2(1040, 660);
            window.Show();
        }

        private ContentCatalog Catalog => catalog ?? (catalog = ContentCatalog.CreateDefault());
        private bool EditorBusy => EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode;

        [ShowInInspector, PropertyOrder(-100), Title("LUMEN · Academy Dashboard", "Autorský prehľad obsahu a nástroje projektu")]
        [InfoBox("Toto je nástroj autora v Unity Editore. Zobrazuje aj správne odpovede; do hráčskeho rozhrania sa nepridáva.")]
        [DisplayAsString, LabelText("Katalóg")]
        private string Summary => Catalog.Questions.Count + " otázok  ·  " + Catalog.Sections.Count +
            " sekcií  ·  4 vekové kategórie  ·  4 stupne náročnosti";

        [ShowInInspector, PropertyOrder(-99), DisplayAsString, LabelText("Pokrytie vekov")]
        private string AgeSummary => string.Join("  |  ", Enum.GetValues(typeof(AgeBand)).Cast<AgeBand>()
            .Select(age => CurriculumLabels.Age(age) + ": " + Catalog.Questions.Count(q => q.AgeBand == age)));

        [ShowInInspector, PropertyOrder(-90), HorizontalGroup("Filters/Age"), LabelText("Všetky veky")]
        [OnValueChanged(nameof(RefreshView))]
        public bool AllAges = true;

        [ShowInInspector, PropertyOrder(-89), HorizontalGroup("Filters/Age"), LabelText("Veková kategória")]
        [DisableIf(nameof(AllAges)), OnValueChanged(nameof(RefreshView))]
        public AgeBand Age = AgeBand.Ages10To12;

        [ShowInInspector, PropertyOrder(-88), HorizontalGroup("Filters/Subject"), LabelText("Všetky sekcie")]
        [OnValueChanged(nameof(RefreshView))]
        public bool AllSubjects = true;

        [ShowInInspector, PropertyOrder(-87), HorizontalGroup("Filters/Subject"), LabelText("Sekcia")]
        [DisableIf(nameof(AllSubjects)), OnValueChanged(nameof(RefreshView))]
        public Subject SubjectFilter = Subject.Mathematics;

        [ShowInInspector, PropertyOrder(-86), DisplayAsString, LabelText("Zobrazené otázky")]
        private string FilterSummary => Questions.Count + " / " + Catalog.Questions.Count;

        [Button("Overiť celý katalóg", ButtonSizes.Large), PropertyOrder(-70)]
        [HorizontalGroup("ContentActions"), GUIColor(.82f, .94f, .42f)]
        public void ValidateCatalog()
        {
            var errors = Validate(Catalog);
            validationReport = errors.Count == 0
                ? "KATALÓG V PORIADKU: 96 jedinečných otázok, 4 veky × 6 sekcií × 4 náročnosti. " +
                  "Každá otázka má 4 rôzne odpovede, platnú správnu možnosť, vysvetlenie a existujúcu sekciu."
                : "CHYBY KATALÓGU (" + errors.Count + "):\n" + string.Join("\n", errors);
            if (errors.Count == 0) Debug.Log("LUMEN_ODIN_CATALOG_VALID: " + validationReport);
            else Debug.LogError("LUMEN_ODIN_CATALOG_INVALID: " + validationReport);
            Repaint();
        }

        [Button("Exportovať curriculum.json", ButtonSizes.Large), PropertyOrder(-69)]
        [HorizontalGroup("ContentActions"), DisableIf(nameof(EditorBusy))]
        public void ExportCurriculum()
        {
            var errors = Validate(Catalog);
            if (errors.Count != 0)
            {
                validationReport = "Export sa neuskutočnil. Najprv oprav chyby katalógu:\n" + string.Join("\n", errors);
                Debug.LogError(validationReport); Repaint(); return;
            }
            var snapshot = new CurriculumExport
            {
                SchemaVersion = 1, QuestionCount = Catalog.Questions.Count,
                Sections = Catalog.Sections.ToArray(), Questions = Catalog.Questions.ToArray()
            };
            string path = Path.Combine(Application.dataPath, "Lumen/Resources/Content/curriculum.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ExportPath, ImportAssetOptions.ForceUpdate);
            validationReport = "Exportovaných " + snapshot.QuestionCount + " otázok do " + ExportPath + ".";
            Debug.Log("LUMEN_ODIN_CONTENT_EXPORTED: " + path); Repaint();
        }

        [ShowInInspector, PropertyOrder(-60), ReadOnly, MultiLineProperty(3), LabelText("Výsledok kontroly / exportu")]
        private string ValidationReport => validationReport;

        [Button("Prepare Academy", ButtonSizes.Medium), PropertyOrder(-50)]
        [HorizontalGroup("ProjectActions"), DisableIf(nameof(EditorBusy))]
        public void PrepareAcademy() => LumenProjectBuilder.Prepare();

        [Button("Validate Academy", ButtonSizes.Medium), PropertyOrder(-49)]
        [HorizontalGroup("ProjectActions"), DisableIf(nameof(EditorBusy))]
        public void ValidateAcademy() => LumenProjectBuilder.Validate();

        [Button("Build Windows Player", ButtonSizes.Medium), PropertyOrder(-48)]
        [HorizontalGroup("ProjectActions"), DisableIf(nameof(EditorBusy))]
        public void BuildWindowsPlayer() => LumenProjectBuilder.BuildWindows();

        [ShowInInspector, PropertyOrder(0), LabelText("Otázky · autorský pohľad")]
        [TableList(IsReadOnly = true, AlwaysExpanded = true, ShowPaging = true,
            NumberOfItemsPerPage = 12, DrawScrollView = true, MaxScrollViewHeight = 720,
            DefaultMinColumnWidth = 90, ShowIndexLabels = true)]
        public List<QuestionRow> Questions
        {
            get
            {
                if (visibleQuestions == null) RefreshView();
                return visibleQuestions;
            }
        }

        private void RefreshView()
        {
            visibleQuestions = Catalog.Questions.Where(q => (AllAges || q.AgeBand == Age) &&
                (AllSubjects || q.Subject == SubjectFilter))
                .OrderBy(q => q.AgeBand).ThenBy(q => q.Subject).ThenBy(q => q.Difficulty)
                .Select(q => new QuestionRow(q)).ToList();
            Repaint();
        }

        /// <summary>Checks the complete catalog, independently of visible filters.</summary>
        public static List<string> Validate(ContentCatalog content)
        {
            var errors = new List<string>();
            if (content == null) { errors.Add("Katalóg neexistuje."); return errors; }
            if (content.Questions.Count != 96) errors.Add("Očakáva sa 96 otázok, nájdených " + content.Questions.Count + ".");
            if (content.Sections.Count != 6) errors.Add("Očakáva sa 6 výskumných sekcií.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var prompts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var question in content.Questions)
            {
                if (question == null) { errors.Add("Prázdna položka otázky."); continue; }
                string id = question.Id ?? "<bez ID>";
                if (string.IsNullOrWhiteSpace(question.Id) || !ids.Add(question.Id)) errors.Add(id + ": chýbajúce alebo duplicitné ID.");
                if (string.IsNullOrWhiteSpace(question.Prompt) || !prompts.Add(question.Prompt)) errors.Add(id + ": prázdne alebo duplicitné zadanie.");
                if (!Enum.IsDefined(typeof(AgeBand), question.AgeBand)) errors.Add(id + ": neplatná veková kategória.");
                if (!Enum.IsDefined(typeof(Subject), question.Subject)) errors.Add(id + ": neplatná sekcia.");
                if (question.Difficulty < 1 || question.Difficulty > 4) errors.Add(id + ": náročnosť musí byť 1 až 4.");
                if (question.Options == null || question.Options.Length != 4)
                    errors.Add(id + ": otázka musí obsahovať štyri možnosti.");
                else
                {
                    if (question.Options.Any(string.IsNullOrWhiteSpace)) errors.Add(id + ": prázdna možnosť odpovede.");
                    if (question.Options.Distinct(StringComparer.Ordinal).Count() != 4) errors.Add(id + ": možnosti musia byť rozdielne.");
                    if (question.CorrectOption < 0 || question.CorrectOption >= question.Options.Length) errors.Add(id + ": nesprávny index správnej odpovede.");
                }
                if (string.IsNullOrWhiteSpace(question.Explanation)) errors.Add(id + ": chýba vysvetlenie.");
                if (!content.Sections.Any(s => s != null && s.Id == question.SectionId && s.Subject == question.Subject))
                    errors.Add(id + ": odkaz na neexistujúcu sekciu.");
            }
            foreach (AgeBand age in Enum.GetValues(typeof(AgeBand)))
            foreach (Subject subject in Enum.GetValues(typeof(Subject)))
            {
                var cell = content.Questions.Where(q => q != null && q.AgeBand == age && q.Subject == subject).ToArray();
                string label = CurriculumLabels.Age(age) + " / " + CurriculumLabels.SubjectName(subject);
                if (cell.Length != 4) errors.Add(label + ": očakávajú sa 4 otázky, nájdených " + cell.Length + ".");
                if (!cell.Select(q => q.Difficulty).OrderBy(d => d).SequenceEqual(new[] { 1, 2, 3, 4 }))
                    errors.Add(label + ": chýba úplné pokrytie náročností 1, 2, 3, 4.");
            }
            return errors;
        }

        [Serializable]
        public sealed class QuestionRow
        {
            [ReadOnly, TableColumnWidth(100), LabelText("ID")]
            public string Id;
            [ReadOnly, TableColumnWidth(90), LabelText("Vek")]
            public string Age;
            [ReadOnly, TableColumnWidth(145), LabelText("Sekcia")]
            public string Subject;
            [ReadOnly, TableColumnWidth(65), LabelText("Úroveň")]
            public int Difficulty;
            [ReadOnly, MultiLineProperty(3), TableColumnWidth(270), LabelText("Zadanie")]
            public string Prompt;
            [ReadOnly, MultiLineProperty(4), TableColumnWidth(260), LabelText("Možnosti")]
            public string Options;
            [ReadOnly, MultiLineProperty(2), TableColumnWidth(180), LabelText("Správna odpoveď")]
            public string CorrectAnswer;
            [ReadOnly, MultiLineProperty(4), TableColumnWidth(310), LabelText("Vysvetlenie")]
            public string Explanation;

            public QuestionRow(Question question)
            {
                Id = question.Id; Age = CurriculumLabels.Age(question.AgeBand);
                Subject = CurriculumLabels.SubjectName(question.Subject); Difficulty = question.Difficulty;
                Prompt = question.Prompt;
                Options = string.Join("\n", question.Options.Select((option, index) => ((char)('A' + index)) + ") " + option));
                CorrectAnswer = question.CorrectOption >= 0 && question.CorrectOption < question.Options.Length
                    ? ((char)('A' + question.CorrectOption)) + ") " + question.Options[question.CorrectOption] : "NEPLATNÁ";
                Explanation = question.Explanation;
            }
        }

        [Serializable]
        private sealed class CurriculumExport
        {
            public int SchemaVersion;
            public int QuestionCount;
            public Section[] Sections;
            public Question[] Questions;
        }
    }
}
#endif
