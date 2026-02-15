using HarmonyLib;
using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

[assembly: MelonInfo(typeof(DigimonNOAccess.Main), "DigimonNOAccess", "1.0.0", "Accessibility Mod")]
[assembly: MelonGame("Bandai Namco Entertainment", "Digimon World Next Order")]

namespace DigimonNOAccess
{
    public class Main : MelonMod
    {
        private static string _modFolderPath;

        /// <summary>
        /// All handlers in the mod, sorted by Priority (lowest first).
        /// Used for Update() calls and AnnounceCurrentStatus() iteration.
        /// </summary>
        private List<IAccessibilityHandler> _handlers;

        // Special handler references needed for cross-handler interactions and cleanup
        private EvolutionHandler _evolutionHandler;
        private NavigationListHandler _navigationListHandler;
        private AudioNavigationHandler _audioNavigationHandler;

        private HarmonyLib.Harmony _harmony;
        private bool _initialized = false;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("DigimonNOAccess initializing...");

            // Set up mod folder path for config files
            _modFolderPath = Path.GetDirectoryName(MelonAssembly.Location);

            // Initialize debug file logger
            DebugLogger.Initialize();

            // Initialize Steam Audio HRTF (before any PositionalAudio instances are created)
            SteamAudioManager.Initialize();

            // Initialize shared audio output (single WaveOutEvent for all positional audio)
            AudioOutputMixer.Initialize();

            // Initialize the input manager with config
            ModInputManager.Initialize(_modFolderPath);
            LoggerInstance.Msg($"Input manager initialized, config at: {_modFolderPath}");

            // Initialize screen reader
            if (ScreenReader.Initialize())
            {
                LoggerInstance.Msg("Screen reader initialized successfully");
            }
            else
            {
                LoggerInstance.Warning("Screen reader not available - announcements will be logged only");
            }

            // Initialize Harmony patches for immediate text interception
            _harmony = new HarmonyLib.Harmony("com.digimonoaccess.patches");
            DialogTextPatch.Apply(_harmony);
            BattleDamagePopPatch.Apply(_harmony);
            LoggerInstance.Msg("Dialog and battle patches applied");

            // Apply gamepad input injection patch for PlayStation controller support
            // Apply if SDL3 is available (even if controller not connected yet)
            if (ModInputManager.IsUsingSDL)
            {
                GamepadInputPatch.Apply(_harmony);
                LoggerInstance.Msg("Gamepad input injection enabled - SDL3 will provide controller input to game");
            }
            else
            {
                LoggerInstance.Msg("SDL3 not available - using game's default input (Xbox controllers via Steam)");
            }

            // Create special handlers that need direct references
            _evolutionHandler = new EvolutionHandler();
            _navigationListHandler = new NavigationListHandler();
            _audioNavigationHandler = new AudioNavigationHandler();

            // Create all handlers and add to registry
            _handlers = new List<IAccessibilityHandler>
            {
                new ModSettingsHandler(),
                new CommonYesNoHandler(),
                new DialogChoiceHandler(),
                new DifficultyDialogHandler(),
                new AgreeWindowHandler(),
                new NameEntryHandler(),
                new DialogHandler(),
                new TitleMenuHandler(),
                new CommonSelectWindowHandler(),
                new OptionsMenuHandler(),
                new CharaSelectHandler(),
                new DigiEggHandler(),
                new GenealogyHandler(),
                new MessageWindowHandler(),
                new TrainingBonusHandler(),
                new TrainingResultHandler(),
                new DigiviceTopPanelHandler(),
                new CampCommandHandler(),
                new TrainingPanelHandler(),
                new FieldItemPanelHandler(),
                new SavePanelHandler(),
                new ColosseumPanelHandler(),
                new IjigenBoxPanelHandler(),
                new FarmPanelHandler(),
                new RestaurantPanelHandler(),
                new TradePanelHandler(),
                new ShopHandler(),
                new EducationPanelHandler(),
                new CarePanelHandler(),
                new StoragePanelHandler(),
                new MapPanelHandler(),
                new LibraryHandler(),
                new MailPanelHandler(),
                new PartnerPanelHandler(),
                new TamerPanelHandler(),
                _evolutionHandler,
                new ZonePanelHandler(),
                new BattleDialogHandler(),
                new BattleItemHandler(),
                new BattleOrderRingHandler(),
                new BattleTacticsHandler(),
                new BattleResultHandler(),
                new BattleHudHandler(),
                new BattleMonitorHandler(),
                new ItemPickPanelHandler(),
                _audioNavigationHandler,
                new FieldHudHandler(),
                _navigationListHandler,
            };

            // Sort by Priority (lowest first) for consistent Update and AnnounceStatus ordering
            _handlers.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            _initialized = true;
            LoggerInstance.Msg("DigimonNOAccess initialized");
        }

        public override void OnUpdate()
        {
            if (!_initialized)
                return;

            // Update the input manager first (tracks button states)
            ModInputManager.Update();

            // Cross-handler data flow: NavigationList needs to know if evolution is active
            _navigationListHandler.SetEvolutionActive(_evolutionHandler.IsActive());

            // Update all handlers (isolated: one handler's exception won't break others)
            for (int i = 0; i < _handlers.Count; i++)
            {
                try
                {
                    _handlers[i].Update();
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Error($"[Main] Handler update error ({_handlers[i].GetType().Name}): {ex.Message}");
                }
            }

            // Global hotkeys
            HandleGlobalKeys();
        }

        private void HandleGlobalKeys()
        {
            // Use the new input manager for configurable hotkeys
            if (ModInputManager.IsActionTriggered("RepeatLast"))
            {
                ScreenReader.RepeatLast();
            }

            if (ModInputManager.IsActionTriggered("AnnounceStatus"))
            {
                AnnounceCurrentStatus();
            }

            if (ModInputManager.IsActionTriggered("ToggleVoicedText"))
            {
                DialogTextPatch.AlwaysReadText = !DialogTextPatch.AlwaysReadText;
                string state = DialogTextPatch.AlwaysReadText ? "on" : "off";
                ScreenReader.Say($"Read voiced text: {state}");
            }

            if (ModInputManager.IsActionTriggered("CompassDirection"))
            {
                string direction = _audioNavigationHandler?.GetCameraCompassDirection() ?? "unknown";
                ScreenReader.Say($"Facing {direction}");
            }

            // F8 = Reload hotkey config (always F8, not configurable)
            if (Input.GetKeyDown(KeyCode.F8))
            {
                ModInputManager.ReloadConfig();
            }

            // F9 = Toggle input debug mode (logs all button presses)
            if (Input.GetKeyDown(KeyCode.F9))
            {
                GamepadInputPatch.DebugMode = !GamepadInputPatch.DebugMode;
                string state = GamepadInputPatch.DebugMode ? "on" : "off";
                ScreenReader.Say($"Input debug mode: {state}");
            }
        }

        private void AnnounceCurrentStatus()
        {
            // Iterate handlers in priority order (lowest Priority first = most specific).
            // The first handler that reports IsOpen() wins and announces.
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i].IsOpen())
                {
                    _handlers[i].AnnounceStatus();
                    return;
                }
            }

            // No handler claimed ownership
            ScreenReader.Say("No menu active");
        }

        public override void OnApplicationQuit()
        {
            LoggerInstance.Msg("DigimonNOAccess shutdown");
            _audioNavigationHandler?.Cleanup();
            AudioOutputMixer.Shutdown();
            SteamAudioManager.Shutdown();
            BattleAudioCues.Shutdown();
            SDLController.Shutdown();
            ScreenReader.Shutdown();
        }
    }
}
