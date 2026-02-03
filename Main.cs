using HarmonyLib;
using MelonLoader;
using UnityEngine;
using System.IO;

[assembly: MelonInfo(typeof(DigimonNOAccess.Main), "DigimonNOAccess", "1.0.0", "Accessibility Mod")]
[assembly: MelonGame("Bandai Namco Entertainment", "Digimon World Next Order")]

namespace DigimonNOAccess
{
    public class Main : MelonMod
    {
        private static string _modFolderPath;
        private TitleMenuHandler _titleMenuHandler;
        private OptionsMenuHandler _optionsMenuHandler;
        private NameEntryHandler _nameEntryHandler;
        private DialogHandler _dialogHandler;
        private MessageWindowHandler _messageWindowHandler;
        private DifficultyDialogHandler _difficultyDialogHandler;
        private CharaSelectHandler _charaSelectHandler;
        private DigiEggHandler _digiEggHandler;
        private GenealogyHandler _genealogyHandler;
        private DialogChoiceHandler _dialogChoiceHandler;
        private CommonYesNoHandler _commonYesNoHandler;
        private AudioNavigationHandler _audioNavigationHandler;
        private CampCommandHandler _campCommandHandler;
        private CommonSelectWindowHandler _commonSelectWindowHandler;
        private TradePanelHandler _tradePanelHandler;
        private RestaurantPanelHandler _restaurantPanelHandler;
        private TrainingPanelHandler _trainingPanelHandler;
        private ColosseumPanelHandler _colosseumPanelHandler;
        private FarmPanelHandler _farmPanelHandler;
        private SavePanelHandler _savePanelHandler;
        private FieldItemPanelHandler _fieldItemPanelHandler;
        private StoragePanelHandler _storagePanelHandler;
        private MapPanelHandler _mapPanelHandler;
        private PartnerPanelHandler _partnerPanelHandler;
        private ItemPickPanelHandler _itemPickPanelHandler;
        private MailPanelHandler _mailPanelHandler;
        private DigiviceTopPanelHandler _digiviceTopPanelHandler;
        private ZonePanelHandler _zonePanelHandler;
        private FieldHudHandler _fieldHudHandler;
        private CarePanelHandler _carePanelHandler;
        private BattleHudHandler _battleHudHandler;
        private BattleOrderRingHandler _battleOrderRingHandler;
        private BattleItemHandler _battleItemHandler;
        private BattleDialogHandler _battleDialogHandler;
        private BattleTacticsHandler _battleTacticsHandler;
        private BattleResultHandler _battleResultHandler;
        private CommonMessageMonitor _commonMessageMonitor;
        private HarmonyLib.Harmony _harmony;
        private bool _initialized = false;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("DigimonNOAccess initializing...");

            // Set up mod folder path for config files
            _modFolderPath = Path.GetDirectoryName(MelonAssembly.Location);

            // Initialize debug file logger
            DebugLogger.Initialize();

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
            LoggerInstance.Msg("Dialog text patches applied");

            // Apply gamepad input injection patch for PlayStation controller support
            // Apply if SDL3 is available (even if controller not connected yet)
            if (ModInputManager.IsUsingSDL2)
            {
                GamepadInputPatch.Apply(_harmony);
                LoggerInstance.Msg("Gamepad input injection enabled - SDL3 will provide controller input to game");
            }
            else
            {
                LoggerInstance.Msg("SDL3 not available - using game's default input (Xbox controllers via Steam)");
            }

            // Create handlers
            _titleMenuHandler = new TitleMenuHandler();
            _optionsMenuHandler = new OptionsMenuHandler();
            _nameEntryHandler = new NameEntryHandler();
            _dialogHandler = new DialogHandler();
            _messageWindowHandler = new MessageWindowHandler();
            _difficultyDialogHandler = new DifficultyDialogHandler();
            _charaSelectHandler = new CharaSelectHandler();
            _digiEggHandler = new DigiEggHandler();
            _genealogyHandler = new GenealogyHandler();
            _dialogChoiceHandler = new DialogChoiceHandler();
            _commonYesNoHandler = new CommonYesNoHandler();
            _audioNavigationHandler = new AudioNavigationHandler();
            _campCommandHandler = new CampCommandHandler();
            _commonSelectWindowHandler = new CommonSelectWindowHandler();
            _tradePanelHandler = new TradePanelHandler();
            _restaurantPanelHandler = new RestaurantPanelHandler();
            _trainingPanelHandler = new TrainingPanelHandler();
            _colosseumPanelHandler = new ColosseumPanelHandler();
            _farmPanelHandler = new FarmPanelHandler();
            _savePanelHandler = new SavePanelHandler();
            _fieldItemPanelHandler = new FieldItemPanelHandler();
            _storagePanelHandler = new StoragePanelHandler();
            _mapPanelHandler = new MapPanelHandler();
            _partnerPanelHandler = new PartnerPanelHandler();
            _itemPickPanelHandler = new ItemPickPanelHandler();
            _mailPanelHandler = new MailPanelHandler();
            _digiviceTopPanelHandler = new DigiviceTopPanelHandler();
            _zonePanelHandler = new ZonePanelHandler();
            _fieldHudHandler = new FieldHudHandler();
            _carePanelHandler = new CarePanelHandler();
            _battleHudHandler = new BattleHudHandler();
            _battleOrderRingHandler = new BattleOrderRingHandler();
            _battleItemHandler = new BattleItemHandler();
            _battleDialogHandler = new BattleDialogHandler();
            _battleTacticsHandler = new BattleTacticsHandler();
            _battleResultHandler = new BattleResultHandler();
            _commonMessageMonitor = new CommonMessageMonitor();

            _initialized = true;
            LoggerInstance.Msg("DigimonNOAccess initialized");
        }

        public override void OnUpdate()
        {
            if (!_initialized)
                return;

            // Update the input manager first (tracks button states)
            ModInputManager.Update();

            // Update all handlers
            _titleMenuHandler.Update();
            _optionsMenuHandler.Update();
            _nameEntryHandler.Update();
            _dialogHandler.Update();
            _messageWindowHandler.Update();
            _difficultyDialogHandler.Update();
            _charaSelectHandler.Update();
            _digiEggHandler.Update();
            _genealogyHandler.Update();
            _dialogChoiceHandler.Update();
            _commonYesNoHandler.Update();
            _audioNavigationHandler.Update();
            _campCommandHandler.Update();
            _commonSelectWindowHandler.Update();
            _tradePanelHandler.Update();
            _restaurantPanelHandler.Update();
            _trainingPanelHandler.Update();
            _colosseumPanelHandler.Update();
            _farmPanelHandler.Update();
            _savePanelHandler.Update();
            _fieldItemPanelHandler.Update();
            _storagePanelHandler.Update();
            _mapPanelHandler.Update();
            _partnerPanelHandler.Update();
            _itemPickPanelHandler.Update();
            _mailPanelHandler.Update();
            _digiviceTopPanelHandler.Update();
            _zonePanelHandler.Update();
            _fieldHudHandler.Update();
            _carePanelHandler.Update();
            _battleHudHandler.Update();
            _battleOrderRingHandler.Update();
            _battleItemHandler.Update();
            _battleDialogHandler.Update();
            _battleTacticsHandler.Update();
            _battleResultHandler.Update();
            _commonMessageMonitor.Update();

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
            // Check handlers in priority order (most specific first)
            if (_commonYesNoHandler.IsOpen())
            {
                _commonYesNoHandler.AnnounceStatus();
            }
            else if (_dialogChoiceHandler.IsChoicesActive())
            {
                _dialogChoiceHandler.AnnounceStatus();
            }
            else if (_dialogHandler.IsOpen())
            {
                _dialogHandler.AnnounceStatus();
            }
            else if (_difficultyDialogHandler.IsOpen())
            {
                _difficultyDialogHandler.AnnounceStatus();
            }
            else if (_charaSelectHandler.IsOpen())
            {
                _charaSelectHandler.AnnounceStatus();
            }
            else if (_genealogyHandler.IsOpen())
            {
                _genealogyHandler.AnnounceStatus();
            }
            else if (_digiEggHandler.IsOpen())
            {
                _digiEggHandler.AnnounceStatus();
            }
            else if (_nameEntryHandler.IsOpen())
            {
                _nameEntryHandler.AnnounceStatus();
            }
            else if (_optionsMenuHandler.IsOpen())
            {
                _optionsMenuHandler.AnnounceStatus();
            }
            else if (_titleMenuHandler.IsOpen())
            {
                _titleMenuHandler.AnnounceStatus();
            }
            else if (_messageWindowHandler.IsOpen())
            {
                _messageWindowHandler.AnnounceStatus();
            }
            else if (_campCommandHandler.IsOpen())
            {
                _campCommandHandler.AnnounceStatus();
            }
            else if (_commonSelectWindowHandler.IsOpen())
            {
                _commonSelectWindowHandler.AnnounceStatus();
            }
            else if (_tradePanelHandler.IsOpen())
            {
                _tradePanelHandler.AnnounceStatus();
            }
            else if (_restaurantPanelHandler.IsOpen())
            {
                _restaurantPanelHandler.AnnounceStatus();
            }
            else if (_trainingPanelHandler.IsOpen())
            {
                _trainingPanelHandler.AnnounceStatus();
            }
            else if (_colosseumPanelHandler.IsOpen())
            {
                _colosseumPanelHandler.AnnounceStatus();
            }
            else if (_farmPanelHandler.IsOpen())
            {
                _farmPanelHandler.AnnounceStatus();
            }
            else if (_savePanelHandler.IsOpen())
            {
                _savePanelHandler.AnnounceStatus();
            }
            else if (_carePanelHandler.IsOpen())
            {
                _carePanelHandler.AnnounceStatus();
            }
            else if (_fieldItemPanelHandler.IsOpen())
            {
                _fieldItemPanelHandler.AnnounceStatus();
            }
            else if (_storagePanelHandler.IsOpen())
            {
                _storagePanelHandler.AnnounceStatus();
            }
            else if (_mapPanelHandler.IsOpen())
            {
                _mapPanelHandler.AnnounceStatus();
            }
            else if (_partnerPanelHandler.IsOpen())
            {
                _partnerPanelHandler.AnnounceStatus();
            }
            else if (_itemPickPanelHandler.IsOpen())
            {
                _itemPickPanelHandler.AnnounceStatus();
            }
            else if (_mailPanelHandler.IsOpen())
            {
                _mailPanelHandler.AnnounceStatus();
            }
            else if (_digiviceTopPanelHandler.IsOpen())
            {
                _digiviceTopPanelHandler.AnnounceStatus();
            }
            else if (_battleDialogHandler.IsActive())
            {
                ScreenReader.Say("Battle dialog");
            }
            else if (_battleItemHandler.IsActive())
            {
                ScreenReader.Say("Battle items");
            }
            else if (_battleOrderRingHandler.IsActive())
            {
                ScreenReader.Say("Order Ring");
            }
            else if (_battleTacticsHandler.IsActive())
            {
                _battleTacticsHandler.AnnounceStatus();
            }
            else if (_battleResultHandler.IsActive())
            {
                _battleResultHandler.AnnounceStatus();
            }
            else if (_battleHudHandler.IsActive())
            {
                ScreenReader.Say("In battle. Hold RB plus D-pad for Partner 1, LB plus D-pad for Partner 2");
            }
            else
            {
                ScreenReader.Say("No menu active");
            }
        }

        public override void OnApplicationQuit()
        {
            LoggerInstance.Msg("DigimonNOAccess shutdown");
            _audioNavigationHandler?.Cleanup();
            SDL2Controller.Shutdown();
            ScreenReader.Shutdown();
        }
    }
}
