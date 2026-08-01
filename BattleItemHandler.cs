using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles battle item menu accessibility.
    /// Announces selected items and target selection.
    /// </summary>
    public class BattleItemHandler : IAccessibilityHandler
    {
        public int Priority => 82;

        /// <summary>
        /// IAccessibilityHandler.IsOpen() - delegates to IsActive().
        /// </summary>
        public bool IsOpen() => IsActive();

        public void AnnounceStatus()
        {
            ScreenReader.Say(GetHeadlineText("Battle items"));
        }

        private uBattlePanelItemBox _cachedItemBox;
        private int _lastSelectNo = -1;
        private bool _wasActive = false;
        private bool _wasSelectingTarget = false;
        private MainGameManager.ORDER_UNIT _lastTarget;

        public void Update()
        {
            // Check if battle is active first
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
            {
                ResetState();
                return;
            }

            // Get item box
            var itemBox = battlePanel.m_itemBox;
            if (itemBox == null || !itemBox.gameObject.activeInHierarchy || !itemBox.m_isVisible)
            {
                if (_wasActive)
                {
                    ResetState();
                }
                return;
            }

            _cachedItemBox = itemBox;

            // Panel just opened
            if (!_wasActive)
            {
                _wasActive = true;
                _lastSelectNo = itemBox.m_selectNo;
                _wasSelectingTarget = itemBox.m_isSelectTarget;
                _lastTarget = itemBox.m_target;

                if (_wasSelectingTarget)
                {
                    AnnounceTargetSelection();
                }
                else
                {
                    AnnounceMenuItem();
                    AnnounceCurrentItem(true);
                }
                return;
            }

            // Check if we just entered target selection mode
            if (itemBox.m_isSelectTarget && !_wasSelectingTarget)
            {
                _wasSelectingTarget = true;
                _lastTarget = itemBox.m_target;
                AnnounceTargetSelection();
                return;
            }

            // Check if we just exited target selection mode
            if (!itemBox.m_isSelectTarget && _wasSelectingTarget)
            {
                _wasSelectingTarget = false;
                AnnounceCurrentItem(false);
                return;
            }

            // In target selection mode - check for target change
            if (itemBox.m_isSelectTarget)
            {
                if (itemBox.m_target != _lastTarget)
                {
                    _lastTarget = itemBox.m_target;
                    AnnounceTargetSelection();
                }
                return;
            }

            // In item selection mode - check for cursor change
            if (itemBox.m_selectNo != _lastSelectNo)
            {
                _lastSelectNo = itemBox.m_selectNo;
                AnnounceCurrentItem(false);
            }
        }

        private void ResetState()
        {
            _cachedItemBox = null;
            _lastSelectNo = -1;
            _wasActive = false;
            _wasSelectingTarget = false;
        }

        private void AnnounceMenuItem()
        {
            ScreenReader.Say(GetHeadlineText("Battle Items"));
        }

        private void AnnounceCurrentItem(bool includePosition)
        {
            if (_cachedItemBox == null)
                return;

            string itemName = GetSelectedItemName();
            int cursor = _cachedItemBox.m_selectNo;

            if (includePosition)
            {
                ScreenReader.Say($"{itemName}, item {cursor + 1}");
            }
            else
            {
                ScreenReader.Say(itemName);
            }
        }

        private void AnnounceTargetSelection()
        {
            if (_cachedItemBox == null)
                return;

            string targetName = GetTargetName(_cachedItemBox.m_target);
            ScreenReader.Say($"{GetCaptionText("Select target")}: {targetName}");
        }

        private string GetHeadlineText(string fallback)
        {
            try
            {
                if (_cachedItemBox == null)
                {
                    DebugLogger.Log("[BattleItemHandler] Headline: cached item box was null");
                    return fallback;
                }

                var headlineText = _cachedItemBox.m_MainHeadLineText;
                if (headlineText == null)
                {
                    DebugLogger.Log("[BattleItemHandler] Headline: m_MainHeadLineText was null");
                    return fallback;
                }

                string headline = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(headlineText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(headline) || TextUtilities.IsPlaceholderText(headline))
                {
                    DebugLogger.Log($"[BattleItemHandler] Headline: m_MainHeadLineText.text unusable: {TextUtilities.DescribeUnusable(headline)}");
                    return fallback;
                }

                return headline;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleItemHandler] Headline read failed: {ex.Message}");
                return fallback;
            }
        }

        private string GetSelectedItemName()
        {
            const string fallback = "Unknown Item";

            try
            {
                if (_cachedItemBox == null)
                {
                    DebugLogger.Log("[BattleItemHandler] Item name: cached item box was null");
                    return fallback;
                }

                var itemParam = _cachedItemBox.GetSelectItemParam();
                if (itemParam != null)
                {
                    string itemName = TextUtilities.StripRichTextTags(itemParam.GetName())?.Trim();
                    if (!string.IsNullOrWhiteSpace(itemName))
                        return itemName;

                    DebugLogger.Log("[BattleItemHandler] Item name: ParameterItemData.GetName returned empty");
                }
                else
                {
                    DebugLogger.Log("[BattleItemHandler] Item name: GetSelectItemParam returned null");
                }

                int logicalIndex = _cachedItemBox.m_selectNo;
                var itemParts = _cachedItemBox.GetSelectItemParts(logicalIndex);
                if (itemParts == null)
                {
                    DebugLogger.Log($"[BattleItemHandler] Item name: GetSelectItemParts({logicalIndex}) returned null");
                    return fallback;
                }

                var nameText = itemParts.m_name;
                if (nameText == null)
                {
                    DebugLogger.Log("[BattleItemHandler] Item name: selected uItemParts.m_name was null");
                    return fallback;
                }

                string renderedName = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(nameText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(renderedName) || TextUtilities.IsPlaceholderText(renderedName))
                {
                    DebugLogger.Log($"[BattleItemHandler] Item name: selected uItemParts.m_name.text unusable: {TextUtilities.DescribeUnusable(renderedName)}");
                    return fallback;
                }

                return renderedName;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleItemHandler] Item name read failed: {ex.Message}");
                return fallback;
            }
        }

        private string GetCaptionText(string fallback)
        {
            try
            {
                if (_cachedItemBox == null)
                {
                    DebugLogger.Log("[BattleItemHandler] Target caption: cached item box was null");
                    return fallback;
                }

                var captionText = _cachedItemBox.m_CaptionText;
                if (captionText == null)
                {
                    DebugLogger.Log("[BattleItemHandler] Target caption: m_CaptionText was null");
                    return fallback;
                }

                string caption = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(captionText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(caption) || TextUtilities.IsPlaceholderText(caption))
                {
                    DebugLogger.Log($"[BattleItemHandler] Target caption: m_CaptionText.text unusable: {TextUtilities.DescribeUnusable(caption)}");
                    return fallback;
                }

                return caption;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleItemHandler] Target caption read failed: {ex.Message}");
                return fallback;
            }
        }

        private string GetTargetName(MainGameManager.ORDER_UNIT target)
        {
            return target switch
            {
                MainGameManager.ORDER_UNIT.Partner00 => "Partner 1",
                MainGameManager.ORDER_UNIT.Partner01 => "Partner 2",
                MainGameManager.ORDER_UNIT.PartnerAll => "Both Partners",
                MainGameManager.ORDER_UNIT.Non => "No Target",
                _ => "Unknown"
            };
        }

        public bool IsActive()
        {
            return _wasActive && _cachedItemBox != null;
        }
    }
}
