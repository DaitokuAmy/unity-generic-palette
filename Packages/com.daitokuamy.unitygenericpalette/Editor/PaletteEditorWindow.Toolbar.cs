using UnityEditor;
using UnityEngine;
namespace UnityGenericPalette.Editor {
    /// <summary>
    /// PaletteEditorWindow のヘッダー描画
    /// </summary>
    public sealed partial class PaletteEditorWindow {
        /// <summary>
        /// タブ用 Toolbar を描画する
        /// </summary>
        private void DrawTabToolbarGui() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUILayout.Space(4f);

                var nextTabMode = (TabMode)GUILayout.Toolbar((int)_tabMode, TabNames, EditorStyles.toolbarButton, GUILayout.Width(160f));
                if (nextTabMode == _tabMode) {
                    GUILayout.FlexibleSpace();
                    return;
                }

                _tabMode = nextTabMode;
                RebuildWindow();
                GUIUtility.ExitGUI();
            }
        }

        /// <summary>
        /// Palette タブの Header を描画する
        /// </summary>
        private void DrawPaletteHeaderGui() {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                    DrawPalettePopupGui();

                    using (new EditorGUI.DisabledScope(_paletteAssetStorage == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_paletteAssetStorage)))) {
                        if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(HeaderButtonWidth))) {
                            ShowAddPaletteMenu();
                        }
                    }

                    using (new EditorGUI.DisabledScope(_selectedPaletteAsset == null)) {
                        if (GUILayout.Button("Remove", EditorStyles.toolbarButton, GUILayout.Width(HeaderRemoveButtonWidth))) {
                            RemoveSelectedPalette();
                            GUIUtility.ExitGUI();
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                    DrawProfilePopupGui();
                    DrawSelectedProfileDefaultGui();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>
        /// Profile タブの Header を描画する
        /// </summary>
        private void DrawProfileHeaderGui() {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                    DrawPalettePopupGui();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>
        /// Palette 選択 Popup を描画する
        /// </summary>
        private void DrawPalettePopupGui() {
            var paletteAssets = GetPaletteAssets();
            GUILayout.Label("Palette", GUILayout.Width(HeaderLabelWidth));

            if (paletteAssets.Count == 0) {
                EditorGUILayout.LabelField("No palette asset", GUILayout.Width(HeaderPopupWidth));
                return;
            }

            var paletteNames = new string[paletteAssets.Count];
            var selectedIndex = 0;
            for (var i = 0; i < paletteAssets.Count; i++) {
                paletteNames[i] = GetPaletteLabel(paletteAssets[i]);
                if (paletteAssets[i] == _selectedPaletteAsset) {
                    selectedIndex = i;
                }
            }

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(selectedIndex, paletteNames, EditorStyles.toolbarPopup, GUILayout.Width(HeaderPopupWidth));
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }

            _selectedPaletteAsset = paletteAssets[selectedIndex];
            _selectedProfileAsset = null;
            _selectedEntryIndex = -1;
            RebuildWindow();
            GUIUtility.ExitGUI();
        }

        /// <summary>
        /// Profile 選択 Popup を描画する
        /// </summary>
        private void DrawProfilePopupGui() {
            var profileAssets = GetProfileAssets(_selectedPaletteAsset);
            GUILayout.Label("Profile", GUILayout.Width(HeaderLabelWidth));

            if (profileAssets.Count == 0) {
                EditorGUILayout.LabelField("No profile asset", GUILayout.Width(HeaderPopupWidth));
                return;
            }

            var profileNames = new string[profileAssets.Count];
            var selectedIndex = 0;
            for (var i = 0; i < profileAssets.Count; i++) {
                profileNames[i] = GetProfileLabel(profileAssets[i]);
                if (profileAssets[i] == _selectedProfileAsset) {
                    selectedIndex = i;
                }
            }

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(selectedIndex, profileNames, EditorStyles.toolbarPopup, GUILayout.Width(HeaderPopupWidth));
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }

            _selectedProfileAsset = profileAssets[selectedIndex];
            SetCurrentEditorProfile(_selectedProfileAsset, false);
            RebuildWindow();
            GUIUtility.ExitGUI();
        }

        /// <summary>
        /// 選択中 Profile の既定状態を表示し、必要に応じて既定へ設定する
        /// </summary>
        private void DrawSelectedProfileDefaultGui() {
            var isDefaultProfile = _selectedPaletteAsset != null &&
                _selectedProfileAsset != null &&
                _selectedPaletteAsset.DefaultProfileId == _selectedProfileAsset.ProfileId;

            using (new EditorGUI.DisabledScope(_selectedPaletteAsset == null || _selectedProfileAsset == null || isDefaultProfile)) {
                if (GUILayout.Button(isDefaultProfile ? "Default" : "Set Default", EditorStyles.toolbarButton, GUILayout.Width(HeaderButtonWidth))) {
                    ToggleDefaultProfile(_selectedProfileAsset, false);
                }
            }
        }
    }
}
