using Newtonsoft.Json;
using System;
using System.IO;

namespace DigimonNOAccess
{
    /// <summary>
    /// Centralized settings storage with JSON persistence.
    /// All configurable mod values are exposed as static properties.
    /// Call Initialize() early in Main.OnInitializeMelon() before any handler reads settings.
    /// </summary>
    public static class ModSettings
    {
        private static string _settingsPath;

        // --- General ---
        private static bool _readVoicedText = false;
        public static bool ReadVoicedText
        {
            get => _readVoicedText;
            set
            {
                _readVoicedText = value;
                DialogTextPatch.AlwaysReadText = value;
            }
        }

        // --- Audio Navigation: Detection Ranges ---
        public static float ItemRange { get; set; } = 80f;
        public static float NpcRange { get; set; } = 80f;
        public static float EnemyRange { get; set; } = 100f;
        public static float TransitionRange { get; set; } = 60f;
        public static float FacilityRange { get; set; } = 80f;

        // --- Audio Navigation: Base Volumes ---
        public static float NearestVolume { get; set; } = 0.8f;
        public static float BackgroundVolume { get; set; } = 0.15f;

        // --- Audio Navigation: Per-type Volumes (0.0 to 1.0) ---
        public static float ItemVolume { get; set; } = 1.0f;
        public static float NpcVolume { get; set; } = 1.0f;
        public static float EnemyVolume { get; set; } = 1.0f;
        public static float TransitionVolume { get; set; } = 1.0f;
        public static float FacilityVolume { get; set; } = 1.0f;

        // --- Audio Navigation: Per-type Enable/Disable ---
        public static bool ItemsEnabled { get; set; } = true;
        public static bool NpcsEnabled { get; set; } = true;
        public static bool EnemiesEnabled { get; set; } = true;
        public static bool TransitionsEnabled { get; set; } = true;
        public static bool FacilitiesEnabled { get; set; } = true;

        // --- Audio Navigation: Max Simultaneous Sounds ---
        public static int MaxItemSounds { get; set; } = 3;
        public static int MaxNpcSounds { get; set; } = 3;
        public static int MaxEnemySounds { get; set; } = 3;
        public static int MaxTransitionSounds { get; set; } = 2;
        public static int MaxFacilitySounds { get; set; } = 2;

        // --- Gameplay: Care Mechanics ---
        public static bool DisableHunger { get; set; } = false;
        public static bool DisableToilet { get; set; } = false;
        public static bool DisableFatigue { get; set; } = false;
        public static bool DisableSickness { get; set; } = false;

        public static void Initialize(string modFolderPath)
        {
            _settingsPath = Path.Combine(modFolderPath, "settings.json");
            Load();
            DebugLogger.Log($"[ModSettings] Initialized, path: {_settingsPath}");
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    DebugLogger.Log("[ModSettings] No settings file found, using defaults");
                    Save();
                    return;
                }

                string json = File.ReadAllText(_settingsPath);
                var data = JsonConvert.DeserializeObject<SettingsData>(json);
                if (data == null)
                {
                    DebugLogger.Warning("[ModSettings] Failed to deserialize settings, using defaults");
                    return;
                }

                ApplyFromData(data);
                DebugLogger.Log("[ModSettings] Settings loaded successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[ModSettings] Error loading settings: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                var data = CreateData();
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[ModSettings] Error saving settings: {ex.Message}");
            }
        }

        private static void ApplyFromData(SettingsData data)
        {
            ReadVoicedText = data.ReadVoicedText;

            ItemRange = data.ItemRange;
            NpcRange = data.NpcRange;
            EnemyRange = data.EnemyRange;
            TransitionRange = data.TransitionRange;
            FacilityRange = data.FacilityRange;

            NearestVolume = data.NearestVolume;
            BackgroundVolume = data.BackgroundVolume;

            ItemVolume = data.ItemVolume;
            NpcVolume = data.NpcVolume;
            EnemyVolume = data.EnemyVolume;
            TransitionVolume = data.TransitionVolume;
            FacilityVolume = data.FacilityVolume;

            ItemsEnabled = data.ItemsEnabled;
            NpcsEnabled = data.NpcsEnabled;
            EnemiesEnabled = data.EnemiesEnabled;
            TransitionsEnabled = data.TransitionsEnabled;
            FacilitiesEnabled = data.FacilitiesEnabled;

            MaxItemSounds = data.MaxItemSounds;
            MaxNpcSounds = data.MaxNpcSounds;
            MaxEnemySounds = data.MaxEnemySounds;
            MaxTransitionSounds = data.MaxTransitionSounds;
            MaxFacilitySounds = data.MaxFacilitySounds;

            DisableHunger = data.DisableHunger;
            DisableToilet = data.DisableToilet;
            DisableFatigue = data.DisableFatigue;
            DisableSickness = data.DisableSickness;
        }

        private static SettingsData CreateData()
        {
            return new SettingsData
            {
                ReadVoicedText = ReadVoicedText,

                ItemRange = ItemRange,
                NpcRange = NpcRange,
                EnemyRange = EnemyRange,
                TransitionRange = TransitionRange,
                FacilityRange = FacilityRange,

                NearestVolume = NearestVolume,
                BackgroundVolume = BackgroundVolume,

                ItemVolume = ItemVolume,
                NpcVolume = NpcVolume,
                EnemyVolume = EnemyVolume,
                TransitionVolume = TransitionVolume,
                FacilityVolume = FacilityVolume,

                ItemsEnabled = ItemsEnabled,
                NpcsEnabled = NpcsEnabled,
                EnemiesEnabled = EnemiesEnabled,
                TransitionsEnabled = TransitionsEnabled,
                FacilitiesEnabled = FacilitiesEnabled,

                MaxItemSounds = MaxItemSounds,
                MaxNpcSounds = MaxNpcSounds,
                MaxEnemySounds = MaxEnemySounds,
                MaxTransitionSounds = MaxTransitionSounds,
                MaxFacilitySounds = MaxFacilitySounds,

                DisableHunger = DisableHunger,
                DisableToilet = DisableToilet,
                DisableFatigue = DisableFatigue,
                DisableSickness = DisableSickness,
            };
        }

        private class SettingsData
        {
            public bool ReadVoicedText { get; set; } = false;

            public float ItemRange { get; set; } = 80f;
            public float NpcRange { get; set; } = 80f;
            public float EnemyRange { get; set; } = 100f;
            public float TransitionRange { get; set; } = 60f;
            public float FacilityRange { get; set; } = 80f;

            public float NearestVolume { get; set; } = 0.8f;
            public float BackgroundVolume { get; set; } = 0.15f;

            public float ItemVolume { get; set; } = 1.0f;
            public float NpcVolume { get; set; } = 1.0f;
            public float EnemyVolume { get; set; } = 1.0f;
            public float TransitionVolume { get; set; } = 1.0f;
            public float FacilityVolume { get; set; } = 1.0f;

            public bool ItemsEnabled { get; set; } = true;
            public bool NpcsEnabled { get; set; } = true;
            public bool EnemiesEnabled { get; set; } = true;
            public bool TransitionsEnabled { get; set; } = true;
            public bool FacilitiesEnabled { get; set; } = true;

            public int MaxItemSounds { get; set; } = 3;
            public int MaxNpcSounds { get; set; } = 3;
            public int MaxEnemySounds { get; set; } = 3;
            public int MaxTransitionSounds { get; set; } = 2;
            public int MaxFacilitySounds { get; set; } = 2;

            public bool DisableHunger { get; set; } = false;
            public bool DisableToilet { get; set; } = false;
            public bool DisableFatigue { get; set; } = false;
            public bool DisableSickness { get; set; } = false;
        }
    }
}
