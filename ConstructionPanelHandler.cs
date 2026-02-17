using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the construction/builder panel (uConstructionPanel).
    /// Covers the Grade Up menu (facility upgrades) and the Material donation menu.
    /// </summary>
    public class ConstructionPanelHandler : IAccessibilityHandler
    {
        private const string LogTag = "[Construction]";
        public int Priority => 57;

        private uConstructionPanel _panel;
        private bool _wasActive;

        // Grade Up panel tracking
        private int _lastBlockIndex = -1;
        private int _lastContentIndex = -1;
        private uConstructionPanelGradeUp.State _lastGradeUpState = uConstructionPanelGradeUp.State.None;

        // Material panel tracking
        private int _lastKindIndex = -1;
        private int _lastMaterialIndex = -1;

        // Parent state tracking
        private uConstructionPanel.State _lastParentState = uConstructionPanel.State.None;

        // Debug: throttle per-frame logging
        private float _lastDebugLogTime;

        public bool IsOpen()
        {
            try
            {
                if (_panel == null)
                    _panel = Object.FindObjectOfType<uConstructionPanel>();
                if (_panel == null) return false;
                var state = _panel.state;
                // Panel is active in GradeUpMain, MaterialMain, or Wait (transitional state)
                return state == uConstructionPanel.State.GradeUpMain ||
                       state == uConstructionPanel.State.MaterialMain ||
                       state == uConstructionPanel.State.Wait;
            }
            catch
            {
                return false;
            }
        }

        public void AnnounceStatus() { }

        public void Update()
        {
            bool isActive = IsOpen();
            if (isActive && !_wasActive)
                OnOpen();
            else if (!isActive && _wasActive)
                OnClose();
            else if (isActive)
                OnUpdate();
            _wasActive = isActive;
        }

        private void OnOpen()
        {
            ResetTracking();
            var state = _panel.state;
            _lastParentState = state;
            DebugLogger.Log($"{LogTag} Panel opened, parent state={state}");

            // Panel may open in Wait before transitioning to GradeUpMain/MaterialMain.
            // The actual sub-panel determines what to announce.
            if (state == uConstructionPanel.State.GradeUpMain)
                AnnounceGradeUpOpen();
            else if (state == uConstructionPanel.State.MaterialMain)
                AnnounceMaterialOpen();
            else if (state == uConstructionPanel.State.Wait)
            {
                // Detect which sub-panel is active by checking which one exists
                if (_panel.m_gradeUpPanel != null)
                {
                    _lastParentState = uConstructionPanel.State.GradeUpMain;
                    AnnounceGradeUpOpen();
                }
                else if (_panel.m_materialPanel != null)
                {
                    _lastParentState = uConstructionPanel.State.MaterialMain;
                    AnnounceMaterialOpen();
                }
            }
        }

        private void OnClose()
        {
            DebugLogger.Log($"{LogTag} Panel closed");
            ResetTracking();
            _panel = null;
        }

        private void OnUpdate()
        {
            try
            {
                var state = _panel.state;

                // Detect parent state change (switching between grade up and material)
                // Wait is transitional - don't react to it, just update _lastParentState
                if (state != _lastParentState)
                {
                    DebugLogger.Log($"{LogTag} Parent state changed: {_lastParentState} -> {state}");
                    if (state == uConstructionPanel.State.GradeUpMain)
                    {
                        // Only announce if truly switching from material to grade up
                        if (_lastParentState == uConstructionPanel.State.MaterialMain)
                        {
                            ResetGradeUpTracking();
                            AnnounceGradeUpOpen();
                        }
                    }
                    else if (state == uConstructionPanel.State.MaterialMain)
                    {
                        if (_lastParentState == uConstructionPanel.State.GradeUpMain)
                        {
                            ResetMaterialTracking();
                            AnnounceMaterialOpen();
                        }
                    }
                    _lastParentState = state;
                }

                // Route updates to the active sub-panel
                // Use _lastParentState to determine which panel, since Wait is transitional
                if (_lastParentState == uConstructionPanel.State.GradeUpMain ||
                    (_lastParentState == uConstructionPanel.State.Wait && _panel.m_gradeUpPanel != null))
                    UpdateGradeUp();
                else if (_lastParentState == uConstructionPanel.State.MaterialMain)
                    UpdateMaterial();
            }
            catch { }
        }

        // ---- Grade Up Panel ----

        private void AnnounceGradeUpOpen()
        {
            try
            {
                var gradeUp = _panel.m_gradeUpPanel;
                if (gradeUp == null)
                {
                    DebugLogger.Log($"{LogTag} m_gradeUpPanel is null");
                    return;
                }

                var gradeState = gradeUp.m_state;
                _lastGradeUpState = gradeState;
                DebugLogger.Log($"{LogTag} GradeUp sub-state: {gradeState}");

                // Log cursor objects
                var blockCursor = gradeUp.m_constructionBlockCursor;
                var contentCursor = gradeUp.m_constructionContentCursor;
                DebugLogger.Log($"{LogTag} blockCursor null={blockCursor == null}, contentCursor null={contentCursor == null}");

                int blockIdx = 0;
                int contentIdx = 0;
                if (blockCursor != null)
                {
                    blockIdx = blockCursor.index;
                    DebugLogger.Log($"{LogTag} blockCursor.index={blockIdx}, min={blockCursor.m_min}, max={blockCursor.m_max}");
                }
                if (contentCursor != null)
                {
                    contentIdx = contentCursor.index;
                    DebugLogger.Log($"{LogTag} contentCursor.index={contentIdx}, min={contentCursor.m_min}, max={contentCursor.m_max}");
                }

                _lastBlockIndex = blockIdx;
                _lastContentIndex = contentIdx;

                // Log available blocks and contents
                var blocks = gradeUp.m_constructionBlocks;
                var contents = gradeUp.m_constructionContents;
                DebugLogger.Log($"{LogTag} blocks null={blocks == null}, count={blocks?.Length ?? 0}");
                DebugLogger.Log($"{LogTag} contents null={contents == null}, count={contents?.Length ?? 0}");

                if (blocks != null)
                {
                    for (int i = 0; i < blocks.Length; i++)
                    {
                        string txt = "null";
                        try { txt = blocks[i]?.m_textUI?.text ?? "empty"; } catch { }
                        DebugLogger.Log($"{LogTag}   block[{i}]: {txt}");
                    }
                }

                if (contents != null)
                {
                    for (int i = 0; i < contents.Length; i++)
                    {
                        string txt = "null";
                        try { txt = contents[i]?.m_nameUI?.text ?? "empty"; } catch { }
                        DebugLogger.Log($"{LogTag}   content[{i}]: {txt}");
                    }
                }

                string blockName = GetBlockName(blockIdx);
                string contentName = GetContentName(contentIdx);

                string announcement = "Construction, Grade Up";
                if (!string.IsNullOrEmpty(blockName))
                    announcement += $". {blockName}";
                if (!string.IsNullOrEmpty(contentName))
                    announcement += $". {contentName}";

                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} Announced open: {announcement}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} AnnounceGradeUpOpen error: {ex.Message}");
            }
        }

        private void UpdateGradeUp()
        {
            try
            {
                var gradeUp = _panel.m_gradeUpPanel;
                if (gradeUp == null) return;

                var gradeState = gradeUp.m_state;
                if (gradeState != _lastGradeUpState)
                {
                    _lastGradeUpState = gradeState;
                    DebugLogger.Log($"{LogTag} GradeUp state: {gradeState}");
                }

                // Skip cursor tracking in terminal states
                if (gradeState == uConstructionPanelGradeUp.State.None ||
                    gradeState == uConstructionPanelGradeUp.State.Close ||
                    gradeState == uConstructionPanelGradeUp.State.GradeUpConfirm ||
                    gradeState == uConstructionPanelGradeUp.State.GradeUpConfirmWait)
                    return;

                int blockIdx = gradeUp.m_constructionBlockCursor?.index ?? 0;
                int contentIdx = gradeUp.m_constructionContentCursor?.index ?? 0;

                // Periodic debug logging of cursor values
                float now = Time.time;
                if (now - _lastDebugLogTime > 2f)
                {
                    DebugLogger.Log($"{LogTag} [tick] block={blockIdx} (last={_lastBlockIndex}), content={contentIdx} (last={_lastContentIndex})");
                    _lastDebugLogTime = now;
                }

                bool blockChanged = blockIdx != _lastBlockIndex;
                bool contentChanged = contentIdx != _lastContentIndex;

                if (!blockChanged && !contentChanged)
                    return;

                DebugLogger.Log($"{LogTag} Cursor changed: block {_lastBlockIndex}->{blockIdx}, content {_lastContentIndex}->{contentIdx}");
                _lastBlockIndex = blockIdx;
                _lastContentIndex = contentIdx;

                string contentName = GetContentName(contentIdx);
                string materials = GetMaterialRequirements();
                string term = GetConstructionTerm();

                string announcement = "";
                if (blockChanged)
                {
                    string blockName = GetBlockName(blockIdx);
                    announcement = blockName ?? "";
                    if (!string.IsNullOrEmpty(contentName))
                        announcement += $". {contentName}";
                }
                else
                {
                    announcement = contentName ?? "";
                }

                if (!string.IsNullOrEmpty(materials))
                    announcement += $". {materials}";
                if (!string.IsNullOrEmpty(term))
                    announcement += $". {term}";

                if (!string.IsNullOrEmpty(announcement))
                {
                    ScreenReader.Say(announcement);
                    DebugLogger.Log($"{LogTag} Announced: {announcement}");
                }
            }
            catch { }
        }

        private string GetBlockName(int index)
        {
            try
            {
                var blocks = _panel.m_gradeUpPanel?.m_constructionBlocks;
                if (blocks == null || index < 0 || index >= blocks.Length) return null;
                return TextUtilities.StripRichTextTags(blocks[index]?.m_textUI?.text);
            }
            catch { return null; }
        }

        private string GetContentName(int index)
        {
            try
            {
                var contents = _panel.m_gradeUpPanel?.m_constructionContents;
                if (contents == null || index < 0 || index >= contents.Length) return null;
                return TextUtilities.StripRichTextTags(contents[index]?.m_nameUI?.text);
            }
            catch { return null; }
        }

        private string GetMaterialRequirements()
        {
            try
            {
                var materials = _panel.m_gradeUpPanel?.m_constructionMaterials;
                if (materials == null) return null;

                var parts = new System.Collections.Generic.List<string>();
                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (mat == null) continue;

                    string name = TextUtilities.StripRichTextTags(mat.m_name?.text);
                    if (string.IsNullOrEmpty(name)) continue;

                    string have = mat.m_currentNum?.text ?? "0";
                    string need = mat.m_needNum?.text ?? "0";
                    parts.Add($"{name}: {have}/{need}");
                }

                return parts.Count > 0 ? string.Join(", ", parts) : null;
            }
            catch { return null; }
        }

        private string GetConstructionTerm()
        {
            try
            {
                var term = _panel.m_gradeUpPanel?.m_constructionTerm;
                if (term == null) return null;

                string day = term.m_dayUI?.text;
                string time = term.m_timeUI?.text;

                if (!string.IsNullOrEmpty(day) || !string.IsNullOrEmpty(time))
                    return $"Term: {day ?? ""} {time ?? ""}".Trim();
                return null;
            }
            catch { return null; }
        }

        // ---- Material Panel ----

        private void AnnounceMaterialOpen()
        {
            try
            {
                var matPanel = _panel.m_materialPanel;
                if (matPanel == null)
                {
                    DebugLogger.Log($"{LogTag} m_materialPanel is null");
                    return;
                }

                DebugLogger.Log($"{LogTag} Material sub-state: {matPanel.m_state}");

                var kindCursor = matPanel.m_materialKindCursor;
                var contentCursor = matPanel.m_materialContentCursor;
                DebugLogger.Log($"{LogTag} kindCursor null={kindCursor == null}, contentCursor null={contentCursor == null}");

                int kindIdx = 0;
                int matIdx = 0;
                if (kindCursor != null)
                {
                    kindIdx = kindCursor.index;
                    DebugLogger.Log($"{LogTag} kindCursor.index={kindIdx}, min={kindCursor.m_min}, max={kindCursor.m_max}");
                }
                if (contentCursor != null)
                {
                    matIdx = contentCursor.index;
                    DebugLogger.Log($"{LogTag} contentCursor.index={matIdx}, min={contentCursor.m_min}, max={contentCursor.m_max}");
                }

                _lastKindIndex = kindIdx;
                _lastMaterialIndex = matIdx;

                string matName = GetMaterialName(matIdx);
                string quantity = GetMaterialQuantity(matIdx);

                string announcement = "Construction, Material Donation";
                if (!string.IsNullOrEmpty(matName))
                {
                    announcement += $". {matName}";
                    if (!string.IsNullOrEmpty(quantity))
                        announcement += $", have {quantity}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} Announced material open: {announcement}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} AnnounceMaterialOpen error: {ex.Message}");
            }
        }

        private void UpdateMaterial()
        {
            try
            {
                var matPanel = _panel.m_materialPanel;
                if (matPanel == null) return;

                if (matPanel.m_state != uConstructionPanelMaterial.State.Main)
                    return;

                int kindIdx = matPanel.m_materialKindCursor?.index ?? 0;
                int matIdx = matPanel.m_materialContentCursor?.index ?? 0;

                // Periodic debug logging
                float now = Time.time;
                if (now - _lastDebugLogTime > 2f)
                {
                    DebugLogger.Log($"{LogTag} [tick] kind={kindIdx} (last={_lastKindIndex}), mat={matIdx} (last={_lastMaterialIndex})");
                    _lastDebugLogTime = now;
                }

                bool kindChanged = kindIdx != _lastKindIndex;
                bool matChanged = matIdx != _lastMaterialIndex;

                if (!kindChanged && !matChanged)
                    return;

                DebugLogger.Log($"{LogTag} Material cursor changed: kind {_lastKindIndex}->{kindIdx}, mat {_lastMaterialIndex}->{matIdx}");
                _lastKindIndex = kindIdx;
                _lastMaterialIndex = matIdx;

                string matName = GetMaterialName(matIdx);
                string quantity = GetMaterialQuantity(matIdx);

                string announcement = "";
                if (!string.IsNullOrEmpty(matName))
                {
                    announcement = matName;
                    if (!string.IsNullOrEmpty(quantity))
                        announcement += $", have {quantity}";
                }

                if (!string.IsNullOrEmpty(announcement))
                {
                    ScreenReader.Say(announcement);
                    DebugLogger.Log($"{LogTag} Announced: {announcement}");
                }
            }
            catch { }
        }

        private string GetMaterialName(int index)
        {
            try
            {
                var contents = _panel.m_materialPanel?.m_materialContents;
                if (contents == null || index < 0 || index >= contents.Length) return null;
                return TextUtilities.StripRichTextTags(contents[index]?.m_nameUI?.text);
            }
            catch { return null; }
        }

        private string GetMaterialQuantity(int index)
        {
            try
            {
                var nums = _panel.m_materialPanel?.m_materialNums;
                if (nums == null || index < 0 || index >= nums.Length) return null;
                return nums[index]?.m_numUI?.text;
            }
            catch { return null; }
        }

        private string GetMaterialDescription()
        {
            try
            {
                return TextUtilities.StripRichTextTags(
                    _panel.m_materialPanel?.m_materialDescription?.m_descriptionUI?.text);
            }
            catch { return null; }
        }

        // ---- Helpers ----

        private void ResetTracking()
        {
            ResetGradeUpTracking();
            ResetMaterialTracking();
            _lastParentState = uConstructionPanel.State.None;
        }

        private void ResetGradeUpTracking()
        {
            _lastBlockIndex = -1;
            _lastContentIndex = -1;
            _lastGradeUpState = uConstructionPanelGradeUp.State.None;
        }

        private void ResetMaterialTracking()
        {
            _lastKindIndex = -1;
            _lastMaterialIndex = -1;
        }
    }
}
