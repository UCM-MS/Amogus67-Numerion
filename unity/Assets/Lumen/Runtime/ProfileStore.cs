using System;
using System.Collections.Generic;
using System.IO;
using Lumen.Core;
using UnityEngine;

namespace Lumen.Runtime
{
    [Serializable]
    public sealed class GameSave
    {
        public PlayerProfile Profile = new PlayerProfile();
        public List<string> Collected = new List<string>();
        public List<string> Purchases = new List<string>();
        public float ParkourBest;
        public bool HasCreatedProfile;
        public int FrameLimit = -1;
        public bool VSync;
        public int TextureSize = 4096;
        public CompanionCollection Companions = new CompanionCollection();
        public AcademyTutorial Tutorial = new AcademyTutorial();
        public GameOptions Options = new GameOptions();
        public ArtifactInventory Artifacts = new ArtifactInventory();
        public List<string> BattleParty = new List<string>();
        public PetBattleState PracticeBattle;
        public int PracticeBattleSequence;
        public StudyTest ActiveStudyTest;
    }

    public static class ProfileStore
    {
        public static string ProfileSuffix
        {
            get
            {
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-lumen-profile-suffix");
                if (index < 0 || index + 1 >= args.Length) return "";
                var result = new System.Text.StringBuilder(32);
                foreach (char c in args[index + 1]) { if (char.IsLetterOrDigit(c) || c == '-' || c == '_') result.Append(c); if (result.Length == 32) break; }
                return result.ToString();
            }
        }
        public static string SavePath
        {
            get
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-lumen-profile-directory");
                string directory = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : Application.persistentDataPath;
                if (ProfileSuffix.Length > 0) directory = Path.Combine(directory, "profiles", ProfileSuffix);
                return Path.Combine(directory, "lumen-native-v1.json");
            }
        }
        public static GameSave Load()
        {
            foreach (string path in new[] { SavePath, SavePath + ".backup" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var save = SaveNormalization.NormalizeSaveJson(File.ReadAllText(path));
                    if (save == null || save.Profile == null) continue;
                    ProfileRules.Normalize(save.Profile);
                    save.Collected ??= new List<string>(); save.Purchases ??= new List<string>();
                    save.Companions ??= new CompanionCollection(); save.Tutorial ??= new AcademyTutorial();
                    CompanionRules.Normalize(save.Companions);
                    save.Artifacts ??= new ArtifactInventory(); ArtifactRules.Normalize(save.Artifacts);
                    save.BattleParty ??= new List<string>();
                    if (float.IsNaN(save.Tutorial.Distance) || float.IsInfinity(save.Tutorial.Distance)) save.Tutorial.Distance = 0;
                    save.Tutorial.Distance = Mathf.Clamp(save.Tutorial.Distance, 0, 8);
                    save.TextureSize = save.TextureSize >= 4096 ? 4096 : 2048;
                    return save;
                }
                catch (Exception e) when (e is IOException || e is ArgumentException) { Debug.LogWarning("Zálohu sa nepodarilo načítať: " + e.Message); }
            }
            return new GameSave();
        }
        public static void Save(GameSave save)
        {
            if (save == null) return;
            SaveNormalization.Normalize(save);
            ProfileRules.Normalize(save.Profile);
            save.Companions ??= new CompanionCollection(); save.Tutorial ??= new AcademyTutorial();
            CompanionRules.Normalize(save.Companions);
            save.Artifacts ??= new ArtifactInventory(); ArtifactRules.Normalize(save.Artifacts);
            save.BattleParty ??= new List<string>();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, SaveNormalization.ToJson(save, true));
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, SavePath + ".backup");
                else File.Move(temp, SavePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { Debug.LogWarning("Uloženie hry: " + e.Message); }
        }
    }
}
