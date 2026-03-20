using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    public class CardGalleryHandler : IAccessibilityHandler
    {
        private const string LogTag = "[CardGallery]";

        private uCardGalleryPanel _panel;
        private bool _wasOpen;
        private string _lastName = "";

        public int Priority => 55;

        public bool IsOpen()
        {
            if (_panel == null)
                _panel = Object.FindObjectOfType<uCardGalleryPanel>();

            if (_panel == null)
                return false;

            try
            {
                return _panel.gameObject != null && _panel.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        public void Update()
        {
            bool isOpen = IsOpen();

            if (isOpen && !_wasOpen)
            {
                OnOpen();
            }
            else if (!isOpen && _wasOpen)
            {
                OnClose();
            }
            else if (isOpen)
            {
                CheckCursorChange();
            }

            _wasOpen = isOpen;
        }

        private void OnOpen()
        {
            _lastName = "";

            string collected = GetCollectionProgress();
            string cardText = GetCardText();
            string currentName = GetCurrentName();

            string header = string.IsNullOrEmpty(collected)
                ? "Card Gallery"
                : $"Card Gallery, {collected}";

            ScreenReader.Say(header);

            if (!string.IsNullOrEmpty(currentName))
            {
                ScreenReader.SayQueued(cardText);
                _lastName = currentName;
            }

            DebugLogger.Log($"{LogTag} Opened, {collected}");
        }

        private void OnClose()
        {
            _lastName = "";
            DebugLogger.Log($"{LogTag} Closed");
        }

        private void CheckCursorChange()
        {
            string currentName = GetCurrentName();

            if (!string.IsNullOrEmpty(currentName) && currentName != _lastName)
            {
                _lastName = currentName;
                string cardText = GetCardText();
                ScreenReader.Say(cardText);
                DebugLogger.Log($"{LogTag} Cursor: {cardText}");
            }
        }

        private string GetCurrentName()
        {
            try
            {
                var nameText = _panel?.m_CardNameName;
                if (nameText != null)
                    return nameText.text ?? "";
            }
            catch { }
            return "";
        }

        private string GetCardText()
        {
            try
            {
                var parts = new System.Collections.Generic.List<string>();

                var nameText = _panel?.m_CardNameName;
                if (nameText != null && !string.IsNullOrEmpty(nameText.text))
                    parts.Add(TextUtilities.StripRichTextTags(nameText.text));

                var numberText = _panel?.m_CardNameNumber;
                string numStr = numberText?.text;
                int total = GetTotalCards();
                int cardNum = -1;

                if (!string.IsNullOrEmpty(numStr) && int.TryParse(numStr, out cardNum))
                {
                    if (total > 0)
                        parts.Add($"{cardNum} of {total}");
                    else
                        parts.Add($"Number {cardNum}");
                }

                if (cardNum > 0)
                {
                    try
                    {
                        var cardData = ParameterDigimonCardData.GetParam(cardNum);
                        if (cardData != null)
                        {
                            string rarity = ((ParameterDigimonCardData.RarityIndex)cardData.m_rarity).ToString();
                            parts.Add($"Rarity {rarity}");
                        }
                    }
                    catch { }
                }

                if (parts.Count > 0)
                    return string.Join(", ", parts);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error: {ex.Message}");
            }

            return "Card";
        }

        private int GetTotalCards()
        {
            try
            {
                var max = _panel?.m_PossessionMax;
                if (max != null && !string.IsNullOrEmpty(max.text) &&
                    int.TryParse(max.text, out int total))
                    return total;
            }
            catch { }
            return 0;
        }

        private string GetCollectionProgress()
        {
            try
            {
                var current = _panel?.m_PossessionCurrent;
                var max = _panel?.m_PossessionMax;

                if (current != null && max != null &&
                    !string.IsNullOrEmpty(current.text) && !string.IsNullOrEmpty(max.text))
                {
                    string curStr = int.TryParse(current.text, out int curNum) ? curNum.ToString() : current.text;
                    string maxStr = int.TryParse(max.text, out int maxNum) ? maxNum.ToString() : max.text;
                    return $"Collected {curStr} of {maxStr}";
                }
            }
            catch { }
            return "";
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            string cardText = GetCardText();
            string collected = GetCollectionProgress();

            string announcement = string.IsNullOrEmpty(collected)
                ? $"Card Gallery, {cardText}"
                : $"Card Gallery, {collected}, {cardText}";
            ScreenReader.Say(announcement);
        }
    }
}
