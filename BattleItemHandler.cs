using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles battle item menu accessibility.
    /// Announces selected items and target selection.
    /// </summary>
    public class BattleItemHandler
    {
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
            ScreenReader.Say("Battle Items");
        }

        private void AnnounceCurrentItem(bool includePosition)
        {
            if (_cachedItemBox == null)
                return;

            string itemName = "Unknown Item";
            int cursor = _cachedItemBox.m_selectNo;

            try
            {
                var itemParam = _cachedItemBox.GetSelectItemParam();
                if (itemParam != null)
                {
                    itemName = itemParam.GetName() ?? "Unknown Item";
                }
            }
            catch { }

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
            ScreenReader.Say($"Select target: {targetName}");
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
