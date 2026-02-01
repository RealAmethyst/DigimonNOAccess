using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(DigimonNOAccess.Main), "DigimonNOAccess", "1.0.0", "Accessibility Mod")]
[assembly: MelonGame("Bandai Namco Entertainment", "Digimon World Next Order")]

namespace DigimonNOAccess
{
    public class Main : MelonMod
    {
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
        private WallDetectionHandler _wallDetectionHandler;
        private CampCommandHandler _campCommandHandler;
        private CommonSelectWindowHandler _commonSelectWindowHandler;
        private TradePanelHandler _tradePanelHandler;
        private RestaurantPanelHandler _restaurantPanelHandler;
        private TrainingPanelHandler _trainingPanelHandler;
        private ColosseumPanelHandler _colosseumPanelHandler;
        private FarmPanelHandler _farmPanelHandler;
        private HarmonyLib.Harmony _harmony;
        private bool _initialized = false;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("DigimonNOAccess initializing...");

            // Initialize debug file logger
            DebugLogger.Initialize();

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
            LoggerInstance.Msg("Harmony patches applied");

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
            _wallDetectionHandler = new WallDetectionHandler();
            _campCommandHandler = new CampCommandHandler();
            _commonSelectWindowHandler = new CommonSelectWindowHandler();
            _tradePanelHandler = new TradePanelHandler();
            _restaurantPanelHandler = new RestaurantPanelHandler();
            _trainingPanelHandler = new TrainingPanelHandler();
            _colosseumPanelHandler = new ColosseumPanelHandler();
            _farmPanelHandler = new FarmPanelHandler();

            _initialized = true;
            LoggerInstance.Msg("DigimonNOAccess initialized");
        }

        public override void OnUpdate()
        {
            if (!_initialized)
                return;

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
            _wallDetectionHandler.Update();
            _campCommandHandler.Update();
            _commonSelectWindowHandler.Update();
            _tradePanelHandler.Update();
            _restaurantPanelHandler.Update();
            _trainingPanelHandler.Update();
            _colosseumPanelHandler.Update();
            _farmPanelHandler.Update();

            // Global hotkeys
            HandleGlobalKeys();
        }

        private void HandleGlobalKeys()
        {
            // F1 = Repeat last announcement
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ScreenReader.RepeatLast();
            }

            // F2 = Announce current status
            if (Input.GetKeyDown(KeyCode.F2))
            {
                AnnounceCurrentStatus();
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
            else
            {
                ScreenReader.Say("No menu active");
            }
        }

        public override void OnApplicationQuit()
        {
            LoggerInstance.Msg("DigimonNOAccess shutdown");
            _audioNavigationHandler?.Cleanup();
            _wallDetectionHandler?.Dispose();
            ScreenReader.Shutdown();
        }
    }
}
