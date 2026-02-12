using Il2Cpp;
using System;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Centralized service for checking player and game state.
    /// Consolidates duplicated state checks from AudioNavigationHandler,
    /// NavigationListHandler, and FieldHudHandler into a single source of truth.
    /// All methods are static for easy access from any handler.
    /// </summary>
    public static class GameStateService
    {
        /// <summary>
        /// Check if the battle panel instance exists and is enabled.
        /// Lightweight check for whether a battle UI is currently showing.
        /// </summary>
        public static bool IsInBattle()
        {
            try
            {
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel != null && battlePanel.m_enabled)
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if we're in any battle phase, including the result screen.
        /// Uses multiple detection methods to catch tutorial battles and battle dialogs.
        /// Also checks MainGameBattle's internal step to catch the transition gap
        /// between battle ending and result panel appearing.
        /// </summary>
        public static bool IsInBattlePhase()
        {
            try
            {
                // FIRST: Check MainGameComponent's current step - most reliable for battle detection
                var mgc = MainGameComponent.m_instance;
                if (mgc != null)
                {
                    var curStep = mgc.m_CurStep;

                    // If we're in Battle mode at all, check the battle's internal step
                    if (curStep == Il2CppMainGame.STEP.Battle)
                    {
                        // Check MainGameBattle's step for Win/Lose/Escape states
                        // These occur BEFORE the result panel is shown
                        try
                        {
                            var stepProc = mgc.m_StepProc;
                            if (stepProc != null && stepProc.Length > 1)
                            {
                                var battleIF = stepProc[1]; // Index 1 = Battle
                                if (battleIF != null)
                                {
                                    var mainGameBattle = battleIF.TryCast<MainGameBattle>();
                                    if (mainGameBattle != null)
                                    {
                                        var stepProg = mainGameBattle.m_Step;
                                        if (stepProg != null)
                                        {
                                            var battleStep = stepProg.step;
                                            // Any step in battle means we're still in battle
                                            // Win, Lose, Miracle, Escape, Break = battle ended but transitioning
                                            if (battleStep != MainGameBattle.STEP.Idle)
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }

                        // Even if we couldn't check internal step, we're in Battle mode
                        return true;
                    }
                }

                // Check the battle panel
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel != null)
                {
                    // Battle UI is active
                    if (battlePanel.m_enabled)
                    {
                        return true;
                    }

                    // Check if result panel is showing (victory/defeat screen)
                    // This is shown AFTER battle panel is disabled, player still can't move
                    // ONLY use m_enabled - gameObject.activeInHierarchy is unreliable
                    var resultPanel = battlePanel.m_result;
                    if (resultPanel != null)
                    {
                        try
                        {
                            if (resultPanel.m_enabled)
                            {
                                return true;
                            }
                        }
                        catch { }
                    }
                }

                // Check if any partner is in battle action state
                // This catches tutorial battles even when battle panel is hidden for dialogs
                var partnerCtrls = UnityEngine.Object.FindObjectsOfType<PartnerCtrl>();
                foreach (var partner in partnerCtrls)
                {
                    if (partner != null && partner.gameObject.activeInHierarchy)
                    {
                        var state = partner.actionState;
                        if (state == UnitCtrlBase.ActionState.ActionState_Battle)
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if the game is paused (m_isPause on MainGameManager).
        /// </summary>
        public static bool IsGamePaused()
        {
            try
            {
                var mgm = MainGameManager.m_instance;
                if (mgm != null && mgm.m_isPause)
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if the player is sleeping (Care > Sleep action).
        /// MainGameComponent.m_CurStep transitions to STEP.Sleep during the
        /// sleep animation and stat recovery sequence (after the care panel closes).
        /// </summary>
        public static bool IsPlayerSleeping()
        {
            try
            {
                var mgc = MainGameComponent.m_instance;
                if (mgc != null && mgc.m_CurStep == Il2CppMainGame.STEP.Sleep)
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if the player is in an event/cutscene action state.
        /// </summary>
        public static bool IsInEvent()
        {
            try
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                if (player != null)
                {
                    return player.actionState == UnitCtrlBase.ActionState.ActionState_Event;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if the player just lost a battle (death/game over recovery).
        /// During this time the player is returned to overworld but cannot move.
        ///
        /// Key: Check MainGameField.m_Step.step == RestartLose or RestartEscape,
        /// which is the definitive indicator that the game is in the death recovery sequence.
        /// </summary>
        public static bool IsInDeathRecovery()
        {
            try
            {
                var mgc = MainGameComponent.m_instance;
                if (mgc == null)
                    return false;

                // Only check when we're supposed to be in field mode
                var curStep = mgc.m_CurStep;
                if (curStep != Il2CppMainGame.STEP.Field)
                    return false;

                // Check MainGameField's internal step state
                var stepProc = mgc.m_StepProc;
                if (stepProc == null || stepProc.Length == 0)
                    return false;

                var fieldIF = stepProc[0]; // Index 0 = Field
                if (fieldIF == null)
                    return false;

                // Try to cast to MainGameField and check m_Step
                var mainGameField = fieldIF.TryCast<MainGameField>();
                if (mainGameField == null)
                    return false;

                var stepProg = mainGameField.m_Step;
                if (stepProg == null)
                    return false;

                var fieldStep = stepProg.step;
                // RestartLose = death recovery, RestartEscape = escape recovery
                if (fieldStep == MainGameField.STEP.RestartLose ||
                    fieldStep == MainGameField.STEP.RestartEscape)
                {
                    DebugLogger.Log($"[GameState] Death recovery detected: {fieldStep}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log but don't block - if we can't determine, assume not in recovery
                DebugLogger.Log($"[GameState] IsInDeathRecovery error (ignoring): {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Check if player action state indicates they cannot move.
        /// Includes death states, events, battles, damage, and recovery sequences.
        /// </summary>
        public static bool IsPlayerInNonControllableState()
        {
            try
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                if (player == null)
                    return true; // No player = not controllable

                var actionState = player.actionState;

                // Check for states where player cannot freely move
                switch (actionState)
                {
                    case UnitCtrlBase.ActionState.ActionState_Event:
                    case UnitCtrlBase.ActionState.ActionState_Battle:
                    case UnitCtrlBase.ActionState.ActionState_Dead:
                    case UnitCtrlBase.ActionState.ActionState_DeadGataway:
                    case UnitCtrlBase.ActionState.ActionState_LiquidCrystallization:
                    case UnitCtrlBase.ActionState.ActionState_Damage:
                    case UnitCtrlBase.ActionState.ActionState_DownDamage:
                    case UnitCtrlBase.ActionState.ActionState_BlowDamage:
                    case UnitCtrlBase.ActionState.ActionState_Getup:
                        return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if player action state indicates they cannot move.
        /// Uses the provided PlayerCtrl reference to avoid redundant FindObjectOfType calls.
        /// Includes death states, events, battles, damage, and recovery sequences.
        /// </summary>
        public static bool IsPlayerInNonControllableState(PlayerCtrl player)
        {
            try
            {
                if (player == null)
                    return true; // No player = not controllable

                var actionState = player.actionState;

                // Check for states where player cannot freely move
                switch (actionState)
                {
                    case UnitCtrlBase.ActionState.ActionState_Event:
                    case UnitCtrlBase.ActionState.ActionState_Battle:
                    case UnitCtrlBase.ActionState.ActionState_Dead:
                    case UnitCtrlBase.ActionState.ActionState_DeadGataway:
                    case UnitCtrlBase.ActionState.ActionState_LiquidCrystallization:
                    case UnitCtrlBase.ActionState.ActionState_Damage:
                    case UnitCtrlBase.ActionState.ActionState_DownDamage:
                    case UnitCtrlBase.ActionState.ActionState_BlowDamage:
                    case UnitCtrlBase.ActionState.ActionState_Getup:
                        return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Composite check: player is controllable when not in battle, not paused,
        /// not in event, not in death recovery, and not in a non-controllable action state.
        /// Does NOT check menus - use IsPlayerInControl() for that.
        /// </summary>
        public static bool IsPlayerControllable()
        {
            try
            {
                if (IsGamePaused())
                    return false;

                if (IsInBattlePhase())
                    return false;

                if (IsInDeathRecovery())
                    return false;

                if (IsPlayerInNonControllableState())
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the player is in the field and controllable.
        /// Verifies MainGameComponent.m_CurStep == Field, player is not in
        /// a non-controllable action state, and the game is not paused.
        /// Does NOT check for menus or evolution (callers handle those separately).
        /// </summary>
        public static bool IsPlayerInField()
        {
            try
            {
                // Check MainGameComponent step - must be in Field mode
                var mgc = MainGameComponent.m_instance;
                if (mgc == null) return false;

                if (mgc.m_CurStep != Il2CppMainGame.STEP.Field)
                    return false;

                // Check player exists and is in a controllable action state
                var player = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                if (player == null) return false;

                var actionState = player.actionState;
                switch (actionState)
                {
                    case UnitCtrlBase.ActionState.ActionState_Event:
                    case UnitCtrlBase.ActionState.ActionState_Battle:
                    case UnitCtrlBase.ActionState.ActionState_Dead:
                    case UnitCtrlBase.ActionState.ActionState_DeadGataway:
                    case UnitCtrlBase.ActionState.ActionState_LiquidCrystallization:
                    case UnitCtrlBase.ActionState.ActionState_Damage:
                    case UnitCtrlBase.ActionState.ActionState_DownDamage:
                    case UnitCtrlBase.ActionState.ActionState_BlowDamage:
                    case UnitCtrlBase.ActionState.ActionState_Getup:
                        return false;
                }

                // Check game is not paused
                if (IsGamePaused())
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Overload that accepts a cached PlayerCtrl reference to avoid FindObjectOfType calls.
        /// Check if the player is in the field and controllable.
        /// </summary>
        public static bool IsPlayerInField(PlayerCtrl player)
        {
            try
            {
                // Check MainGameComponent step - must be in Field mode
                var mgc = MainGameComponent.m_instance;
                if (mgc == null) return false;

                if (mgc.m_CurStep != Il2CppMainGame.STEP.Field)
                    return false;

                // Check player exists and is in a controllable action state
                if (player == null) return false;

                var actionState = player.actionState;
                switch (actionState)
                {
                    case UnitCtrlBase.ActionState.ActionState_Event:
                    case UnitCtrlBase.ActionState.ActionState_Battle:
                    case UnitCtrlBase.ActionState.ActionState_Dead:
                    case UnitCtrlBase.ActionState.ActionState_DeadGataway:
                    case UnitCtrlBase.ActionState.ActionState_LiquidCrystallization:
                    case UnitCtrlBase.ActionState.ActionState_Damage:
                    case UnitCtrlBase.ActionState.ActionState_DownDamage:
                    case UnitCtrlBase.ActionState.ActionState_BlowDamage:
                    case UnitCtrlBase.ActionState.ActionState_Getup:
                        return false;
                }

                // Check game is not paused
                if (IsGamePaused())
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if any menu is open that blocks player control.
        /// Checks Digivice, care, save, training, training bonus, training result,
        /// and evolution panels.
        ///
        /// Note: Training result cooldown is NOT handled here since it requires
        /// instance-level timing state. Callers that need that behavior (e.g.,
        /// AudioNavigationHandler) should handle it separately.
        /// </summary>
        public static bool IsMenuOpen()
        {
            try
            {
                var mgm = MainGameManager.m_instance;
                if (mgm != null)
                {
                    // Care menu check - reliable state-based detection
                    var careUI = mgm.careUI;
                    if (careUI != null && careUI.m_state != uCarePanel.State.None)
                        return true;
                }

                // Digivice panel check - has static instance with m_enabled flag
                var digivicePanel = uDigivicePanel.m_instance;
                if (digivicePanel != null && digivicePanel.m_enabled)
                    return true;

                // Save panel check - use state-based detection via uSavePanelCommand
                var savePanel = UnityEngine.Object.FindObjectOfType<uSavePanelCommand>();
                if (savePanel != null)
                {
                    var state = savePanel.m_State;
                    if (state == uSavePanelCommand.State.MAIN ||
                        state == uSavePanelCommand.State.SAVE_CHECK ||
                        state == uSavePanelCommand.State.LOAD_CHECK)
                        return true;
                }

                // Training panel check - main training selection
                var trainingPanel = UnityEngine.Object.FindObjectOfType<uTrainingPanelCommand>();
                if (trainingPanel != null && trainingPanel.gameObject.activeInHierarchy)
                {
                    var state = trainingPanel.m_state;
                    if (state != uTrainingPanelCommand.State.None && state != uTrainingPanelCommand.State.Close)
                        return true;
                }

                // Training bonus roulette panel check
                var bonusPanel = UnityEngine.Object.FindObjectOfType<uTrainingPanelBonus>();
                if (bonusPanel != null && bonusPanel.gameObject.activeInHierarchy)
                {
                    var state = bonusPanel.m_state;
                    if (state != uTrainingPanelBonus.State.None)
                        return true;
                }

                // Training result panel check
                var resultPanel = UnityEngine.Object.FindObjectOfType<uTrainingPanelResult>();
                if (resultPanel != null && resultPanel.gameObject.activeInHierarchy)
                    return true;

                // Evolution/Digivolution check
                var evolution = UnityEngine.Object.FindObjectOfType<EvolutionBase>();
                if (evolution != null && evolution.gameObject.activeInHierarchy)
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the defeated enemy's GameObject if the last battle was won.
        /// Uses MainGameBattle.m_lastEnemy which is set during battle and persists
        /// after returning to the field. Returns null if not a win or unavailable.
        /// </summary>
        public static GameObject GetLastDefeatedEnemyObject()
        {
            try
            {
                var mgc = MainGameComponent.m_instance;
                if (mgc == null || mgc.battleResult != MainGameComponent.BATTLE_RESULT.Win)
                    return null;

                var stepProc = mgc.m_StepProc;
                if (stepProc == null || stepProc.Length <= 1)
                    return null;

                var battleIF = stepProc[1]; // Index 1 = Battle
                if (battleIF == null)
                    return null;

                var battle = battleIF.TryCast<MainGameBattle>();
                var lastEnemy = battle?.m_lastEnemy;
                return lastEnemy?.gameObject;
            }
            catch { return null; }
        }

        /// <summary>
        /// Check if we're currently in a tutorial or event-scripted sequence.
        /// Checks MainGameManager.eventScene which holds a TalkMain reference
        /// that persists during event-triggered battles (tutorials, story events).
        /// Falls back to MainGameComponent.m_tutorialScene for non-battle tutorial phases.
        /// </summary>
        public static bool IsInTutorial()
        {
            try
            {
                // Primary: MainGameManager.eventScene persists during event-triggered battles
                var mgm = MainGameManager.m_instance;
                if (mgm != null)
                {
                    var evtScene = mgm.eventScene;
                    if (evtScene != null)
                        return true;
                }

                // Fallback: m_tutorialScene on MainGameComponent (set during non-battle tutorial phases)
                var mgc = MainGameComponent.m_instance;
                if (mgc != null)
                {
                    var tutScene = mgc.m_tutorialScene;
                    if (tutScene != null)
                        return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if the player is in the field and has basic control (not in battle,
        /// not in a non-controllable action state). Used by FieldHudHandler for
        /// determining when to respond to status queries.
        /// Lighter check than full IsPlayerInField - does not check pause or game step.
        /// </summary>
        public static bool IsPlayerInFieldControl()
        {
            // Check if battle is active
            if (IsInBattle())
                return false;

            // Check player action state - exclude states where we shouldn't respond
            try
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                if (player != null)
                {
                    var state = player.actionState;
                    if (state == PlayerCtrl.ActionState.ActionState_Event ||
                        state == PlayerCtrl.ActionState.ActionState_Battle ||
                        state == PlayerCtrl.ActionState.ActionState_Dead ||
                        state == PlayerCtrl.ActionState.ActionState_DeadGataway ||
                        state == PlayerCtrl.ActionState.ActionState_LiquidCrystallization)
                    {
                        return false;
                    }
                }
            }
            catch { }

            return true;
        }
    }
}
