using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the fishing minigame.
    /// Announces lure selection, fishing states, bite detection, and catch results.
    /// </summary>
    public class FishingHandler : IAccessibilityHandler
    {
        public int Priority => 30;

        private const string LogTag = "[Fishing]";

        // Step enum values from uFishingPanel.Step
        private const int STEP_LURE_SELECT = 0;
        private const int STEP_FISHING_START = 1;
        private const int STEP_WAIT_HIT = 2;
        private const int STEP_HIT_GAME = 3;
        private const int STEP_WAIT_FADE = 4;
        private const int STEP_RESULT = 5;
        private const int STEP_TUTORIAL00 = 6;
        private const int STEP_TUTORIAL01 = 7;
        private const int STEP_FIRST_SUCCESS = 8;

        private uFishingPanel _panel;
        private bool _wasActive;
        private int _lastStep = -1;
        private int _lastLureSelect = -1;
        private string _lastResultText = "";
        private int _pendingOpenFrames = -1;
        private bool _pendingHitCheck;

        public bool IsOpen()
        {
            try
            {
                _panel = uFishingPanel.m_instance;
                if (_panel == null) return false;
                if (_panel.gameObject == null) return false;
                if (!_panel.gameObject.activeInHierarchy) return false;

                int step = _panel.m_step;
                // m_step is -1 or similar when panel exists but fishing isn't active
                return step >= STEP_LURE_SELECT;
            }
            catch
            {
                _panel = null;
                return false;
            }
        }

        public void Update()
        {
            bool active = IsOpen();

            if (active && !_wasActive)
            {
                OnOpen();
            }
            else if (!active && _wasActive)
            {
                OnClose();
            }
            else if (active)
            {
                OnUpdate();
            }

            _wasActive = active;
        }

        public void AnnounceStatus()
        {
            if (!IsOpen()) return;

            int step = _panel.m_step;
            switch (step)
            {
                case STEP_LURE_SELECT:
                    AnnounceLureSelection();
                    break;
                case STEP_FISHING_START:
                    ScreenReader.Say("Casting");
                    break;
                case STEP_WAIT_HIT:
                    ScreenReader.Say("Waiting for bite");
                    break;
                case STEP_HIT_GAME:
                    if (_panel.isHit)
                        ScreenReader.Say("Fish on the line!");
                    else
                        ScreenReader.Say("No bite");
                    break;
                case STEP_RESULT:
                case STEP_FIRST_SUCCESS:
                    AnnounceResult();
                    break;
                default:
                    ScreenReader.Say("Fishing");
                    break;
            }
        }

        private void OnOpen()
        {
            _lastStep = -1;
            _lastLureSelect = -1;
            _lastResultText = "";
            _pendingOpenFrames = 0;
            DebugLogger.Log($"{LogTag} Opened");
        }

        private void OnClose()
        {
            _panel = null;
            _lastStep = -1;
            _lastLureSelect = -1;
            _lastResultText = "";
            _pendingOpenFrames = -1;
            _pendingHitCheck = false;
            DebugLogger.Log($"{LogTag} Closed");
        }

        private void OnUpdate()
        {
            if (_panel == null) return;

            // Delayed first announcement (lure data needs a frame to populate)
            if (_pendingOpenFrames >= 0)
            {
                _pendingOpenFrames++;
                if (_pendingOpenFrames >= 2)
                {
                    _pendingOpenFrames = -1;
                    int step = _panel.m_step;
                    HandleStepChange(step);
                    _lastStep = step;
                }
                return;
            }

            try
            {
                int step = _panel.m_step;

                // Deferred hit check: if still in HitGame after 1 frame, it's a real bite
                if (_pendingHitCheck)
                {
                    _pendingHitCheck = false;
                    if (step == STEP_HIT_GAME)
                    {
                        ScreenReader.Say("Fish on the line! Press now!");
                        DebugLogger.Log($"{LogTag} Confirmed real bite");
                    }
                }

                if (step != _lastStep)
                {
                    HandleStepChange(step);
                    _lastStep = step;
                }
                else if (step == STEP_LURE_SELECT)
                {
                    CheckLureChange();
                }
                else if (step == STEP_RESULT || step == STEP_FIRST_SUCCESS)
                {
                    CheckResultText();
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"{LogTag} Update error: {ex.Message}");
            }
        }

        private void HandleStepChange(int step)
        {
            DebugLogger.Log($"{LogTag} Step changed to {step}");

            switch (step)
            {
                case STEP_LURE_SELECT:
                    _lastLureSelect = -1;
                    AnnounceLureSelection();
                    break;

                case STEP_FISHING_START:
                    ScreenReader.Say("Casting");
                    break;

                case STEP_WAIT_HIT:
                    ScreenReader.Say("Waiting for bite");
                    break;

                case STEP_HIT_GAME:
                    // Don't announce immediately - failures flash through HitGame in ~17ms.
                    // Wait a frame; if still in HitGame, it's a real bite.
                    _pendingHitCheck = true;
                    break;

                case STEP_RESULT:
                case STEP_FIRST_SUCCESS:
                    _lastResultText = "";
                    // Result text may take a frame to populate, checked in OnUpdate
                    break;

                case STEP_WAIT_FADE:
                    // Transition, nothing to announce
                    break;

                case STEP_TUTORIAL00:
                case STEP_TUTORIAL01:
                    ScreenReader.Say("Fishing tutorial");
                    break;
            }
        }

        private void AnnounceLureSelection()
        {
            try
            {
                var lure = _panel.m_lure;
                if (lure == null) return;

                int cursor = lure.m_selectNo;
                int total = lure.m_selectMax;
                string lureName = GetLureName(cursor);

                string announcement = AnnouncementBuilder.MenuOpen("Select Lure", lureName, cursor, total);
                ScreenReader.Say(announcement);
                _lastLureSelect = cursor;

                DebugLogger.Log($"{LogTag} Lure: {lureName}, {cursor + 1} of {total}");
            }
            catch (System.Exception ex)
            {
                ScreenReader.Say("Select Lure");
                DebugLogger.Warning($"{LogTag} Lure announce error: {ex.Message}");
            }
        }

        private void CheckLureChange()
        {
            try
            {
                var lure = _panel.m_lure;
                if (lure == null) return;

                int cursor = lure.m_selectNo;
                if (cursor != _lastLureSelect)
                {
                    string lureName = GetLureName(cursor);
                    int total = lure.m_selectMax;
                    ScreenReader.Say($"{lureName}, {cursor + 1} of {total}");
                    _lastLureSelect = cursor;
                    DebugLogger.Log($"{LogTag} Lure changed: {lureName}, {cursor + 1} of {total}");
                }
            }
            catch { }
        }

        private string GetLureName(int index)
        {
            try
            {
                var lure = _panel.m_lure;
                if (lure?.m_luaName != null && index >= 0 && index < lure.m_luaName.Length)
                {
                    var textComp = lure.m_luaName[index];
                    if (textComp != null)
                    {
                        string text = textComp.text;
                        if (!string.IsNullOrEmpty(text))
                            return TextUtilities.StripRichTextTags(text).Trim();
                    }
                }
            }
            catch { }
            return AnnouncementBuilder.FallbackItem("Lure", index);
        }

        private void CheckResultText()
        {
            try
            {
                var result = _panel.m_result;
                if (result?.m_text == null) return;

                string text = result.m_text.text;
                if (string.IsNullOrEmpty(text)) return;

                text = TextUtilities.CleanText(text).Trim();
                if (text != _lastResultText && !string.IsNullOrEmpty(text))
                {
                    _lastResultText = text;
                    ScreenReader.Say(text);
                    DebugLogger.Log($"{LogTag} Result: {text}");
                }
            }
            catch { }
        }

        private void AnnounceResult()
        {
            try
            {
                // Try result panel text first
                var result = _panel.m_result;
                if (result?.m_text != null)
                {
                    string text = result.m_text.text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        ScreenReader.Say(TextUtilities.CleanText(text).Trim());
                        return;
                    }
                }

                // Fallback: read item data
                int itemId = _panel.m_hitItem;
                int quantity = _panel.m_hitNum;
                if (itemId > 0)
                {
                    string itemName = "item";
                    try
                    {
                        var param = ParameterItemData.GetParam((uint)itemId);
                        if (param != null)
                            itemName = param.GetName() ?? "item";
                    }
                    catch { }

                    ScreenReader.Say(quantity > 1
                        ? $"Caught {quantity} {itemName}"
                        : $"Caught {itemName}");
                }
                else
                {
                    ScreenReader.Say("Fishing complete");
                }
            }
            catch
            {
                ScreenReader.Say("Fishing complete");
            }
        }
    }
}
