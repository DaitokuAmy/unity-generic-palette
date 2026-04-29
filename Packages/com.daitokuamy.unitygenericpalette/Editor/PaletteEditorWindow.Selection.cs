using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// PaletteEditorWindow の選択状態と共通ヘルパー
    /// </summary>
    public sealed partial class PaletteEditorWindow {
        /// <summary>
        /// 選択状態を補正する
        /// </summary>
        private void ValidateSelection() {
            ApplyProjectSettingsPaletteAssetStorageIfNeeded();

            var paletteAssets = GetPaletteAssets();
            if (paletteAssets.Count == 0) {
                _selectedPaletteAsset = null;
                _selectedProfileAsset = null;
                _selectedEntryIndex = -1;
                InvalidatePaletteEntryList();
                return;
            }

            if (_selectedPaletteAsset == null || !paletteAssets.Contains(_selectedPaletteAsset)) {
                _selectedPaletteAsset = paletteAssets[0];
                _selectedProfileAsset = null;
                _selectedEntryIndex = -1;
                InvalidatePaletteEntryList();
            }

            if (_selectedEntryIndex >= _selectedPaletteAsset.Entries.Count) {
                _selectedEntryIndex = -1;
            }

            var profileAssets = GetProfileAssets(_selectedPaletteAsset);
            if (profileAssets.Count == 0) {
                _selectedProfileAsset = null;
                return;
            }

            if (_selectedProfileAsset == null || !profileAssets.Contains(_selectedProfileAsset)) {
                _selectedProfileAsset = profileAssets[0];
            }
        }

        /// <summary>
        /// PaletteAsset 一覧を取得する
        /// </summary>
        /// <returns>PaletteAsset 一覧</returns>
        private List<PaletteAssetBase> GetPaletteAssets() {
            var paletteAssets = new List<PaletteAssetBase>();
            if (_paletteAssetStorage == null) {
                return paletteAssets;
            }

            for (var i = 0; i < _paletteAssetStorage.PaletteAssets.Count; i++) {
                var paletteAsset = _paletteAssetStorage.PaletteAssets[i];
                if (paletteAsset == null) {
                    continue;
                }

                paletteAssets.Add(paletteAsset);
            }

            return paletteAssets;
        }

        /// <summary>
        /// Palette に対応する ProfileAsset 一覧を取得する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>ProfileAsset 一覧</returns>
        private List<ScriptableObject> GetProfileAssets(PaletteAssetBase paletteAsset) {
            var profileAssets = new List<ScriptableObject>();
            if (paletteAsset == null) {
                return profileAssets;
            }

            var profileAssetType = GetProfileAssetType(paletteAsset);
            if (profileAssetType == null) {
                return profileAssets;
            }

            var guids = AssetDatabase.FindAssets($"t:{profileAssetType.Name}");
            for (var i = 0; i < guids.Length; i++) {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var profileAsset = AssetDatabase.LoadAssetAtPath(assetPath, profileAssetType) as ScriptableObject;
                if (profileAsset is not IPaletteProfileAsset paletteProfileAsset) {
                    continue;
                }

                if (paletteProfileAsset.PaletteAssetBase != paletteAsset) {
                    continue;
                }

                profileAssets.Add(profileAsset);
            }

            profileAssets.Sort(CompareProfileAsset);
            return profileAssets;
        }

        /// <summary>
        /// ProfileAsset 型を取得する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>対応する ProfileAsset 型</returns>
        private Type GetProfileAssetType(PaletteAssetBase paletteAsset) {
            if (paletteAsset == null) {
                return null;
            }

            var attribute = Attribute.GetCustomAttribute(paletteAsset.GetType(), typeof(PaletteProfileAssetAttribute)) as PaletteProfileAssetAttribute;
            return attribute?.ProfileAssetType;
        }

        /// <summary>
        /// Palette 表示ラベルを取得する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>表示ラベル</returns>
        private string GetPaletteLabel(PaletteAssetBase paletteAsset) {
            return paletteAsset == null ? "No palette asset" : paletteAsset.GetType().Name;
        }

        /// <summary>
        /// Entry 表示ラベルを取得する
        /// </summary>
        /// <param name="paletteEntry">対象 Entry</param>
        /// <returns>表示ラベル</returns>
        private string GetEntryLabel(PaletteEntry paletteEntry) {
            if (paletteEntry == null) {
                return "(Null Entry)";
            }

            if (!string.IsNullOrEmpty(paletteEntry.DisplayName)) {
                return $"{paletteEntry.DisplayName} ({paletteEntry.EntryId})";
            }

            return paletteEntry.EntryId;
        }

        /// <summary>
        /// Profile 表示ラベルを取得する
        /// </summary>
        /// <param name="profileAsset">対象 ProfileAsset</param>
        /// <returns>表示ラベル</returns>
        private string GetProfileLabel(ScriptableObject profileAsset) {
            if (profileAsset is not IPaletteProfileAsset paletteProfileAsset) {
                return profileAsset != null ? profileAsset.name : "(Null Profile)";
            }

            return string.IsNullOrEmpty(paletteProfileAsset.ProfileId) ? profileAsset.name : paletteProfileAsset.ProfileId;
        }

        /// <summary>
        /// ProfileAsset のソート順を比較する
        /// </summary>
        /// <param name="left">左側のアセット</param>
        /// <param name="right">右側のアセット</param>
        /// <returns>比較結果</returns>
        private int CompareProfileAsset(ScriptableObject left, ScriptableObject right) {
            var leftSortOrder = GetProfileSortOrder(left);
            var rightSortOrder = GetProfileSortOrder(right);
            var compareSortOrder = leftSortOrder.CompareTo(rightSortOrder);
            if (compareSortOrder != 0) {
                return compareSortOrder;
            }

            var leftProfileId = left is IPaletteProfileAsset leftProfileAsset ? leftProfileAsset.ProfileId : string.Empty;
            var rightProfileId = right is IPaletteProfileAsset rightProfileAsset ? rightProfileAsset.ProfileId : string.Empty;
            return string.Compare(leftProfileId, rightProfileId, StringComparison.Ordinal);
        }

        /// <summary>
        /// ProfileAsset の並び順を取得する
        /// </summary>
        /// <param name="profileAsset">対象 ProfileAsset</param>
        /// <returns>並び順</returns>
        private int GetProfileSortOrder(ScriptableObject profileAsset) {
            if (profileAsset == null) {
                return int.MaxValue;
            }

            var serializedObject = new SerializedObject(profileAsset);
            var sortOrderProperty = serializedObject.FindProperty("_sortOrder");
            return sortOrderProperty != null ? sortOrderProperty.intValue : int.MaxValue;
        }

        /// <summary>
        /// 新規 EntryId を生成する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>未使用の EntryId</returns>
        private string GenerateUniqueEntryId(PaletteAssetBase paletteAsset) {
            var entryIds = new HashSet<string>();
            for (var i = 0; i < paletteAsset.Entries.Count; i++) {
                entryIds.Add(paletteAsset.Entries[i].EntryId);
            }

            var index = 1;
            while (true) {
                var entryId = $"entry{index}";
                if (!entryIds.Contains(entryId)) {
                    return entryId;
                }

                index++;
            }
        }

        /// <summary>
        /// 新規 ProfileId を生成する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>未使用の ProfileId</returns>
        private string GenerateUniqueProfileId(PaletteAssetBase paletteAsset) {
            var profileAssets = GetProfileAssets(paletteAsset);
            var profileIds = new HashSet<string>();
            for (var i = 0; i < profileAssets.Count; i++) {
                if (profileAssets[i] is not IPaletteProfileAsset paletteProfileAsset) {
                    continue;
                }

                profileIds.Add(paletteProfileAsset.ProfileId);
            }

            var index = 1;
            while (true) {
                var profileId = $"profile{index}";
                if (!profileIds.Contains(profileId)) {
                    return profileId;
                }

                index++;
            }
        }

        /// <summary>
        /// 新規 Profile 用の並び順を生成する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>未使用の並び順</returns>
        private int GetNextProfileSortOrder(PaletteAssetBase paletteAsset) {
            var profileAssets = GetProfileAssets(paletteAsset);
            if (profileAssets.Count == 0) {
                return 0;
            }

            var maxSortOrder = -1;
            for (var i = 0; i < profileAssets.Count; i++) {
                maxSortOrder = Mathf.Max(maxSortOrder, GetProfileSortOrder(profileAssets[i]));
            }

            return maxSortOrder + 1;
        }

        /// <summary>
        /// ProfileValue 配列の中から一致する EntryId の位置を検索する
        /// </summary>
        /// <param name="valuesProperty">検索対象の配列</param>
        /// <param name="entryId">検索する EntryId</param>
        /// <param name="startIndex">検索開始 index</param>
        /// <returns>見つかった index。見つからない場合は -1</returns>
        private int FindProfileValueIndex(SerializedProperty valuesProperty, string entryId, int startIndex) {
            for (var i = startIndex; i < valuesProperty.arraySize; i++) {
                var currentEntryId = valuesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_entryId").stringValue;
                if (currentEntryId == entryId) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 配列 index の妥当性を判定する
        /// </summary>
        /// <param name="arrayProperty">対象の配列 property</param>
        /// <param name="index">確認対象 index</param>
        /// <returns>有効な場合は true</returns>
        private bool IsValidArrayIndex(SerializedProperty arrayProperty, int index) {
            return arrayProperty != null && arrayProperty.isArray && index >= 0 && index < arrayProperty.arraySize;
        }

        /// <summary>
        /// Project Settings に設定された PaletteAssetStorage を必要時に反映する
        /// </summary>
        private void ApplyProjectSettingsPaletteAssetStorageIfNeeded() {
            if (_paletteAssetStorage != null) {
                return;
            }

            var paletteAssetStorage = UnityGenericPaletteProjectSettings.instance.PaletteAssetStorage;
            if (paletteAssetStorage == null) {
                return;
            }

            _paletteAssetStorage = paletteAssetStorage;
        }

        /// <summary>
        /// PaletteAssetStorage 未設定時の案内 UI を描画する
        /// </summary>
        private void DrawPaletteAssetStorageMissingIMGUI() {
            EditorGUILayout.HelpBox("Project Settings で PaletteAssetStorage を設定してください。", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Open Project Settings")) {
                    SettingsService.OpenProjectSettings("Project/Unity Generic Palette");
                }

                if (GUILayout.Button("Create Storage")) {
                    CreateAndAssignPaletteAssetStorage();
                    GUIUtility.ExitGUI();
                }
            }
        }

        /// <summary>
        /// PaletteAssetStorage を新規作成して Project Settings へ割り当てる
        /// </summary>
        private void CreateAndAssignPaletteAssetStorage() {
            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Create PaletteAssetStorage",
                "PaletteAssetStorage",
                "asset",
                "Select a location for the PaletteAssetStorage asset.");
            if (string.IsNullOrEmpty(assetPath)) {
                return;
            }

            var paletteAssetStorage = ScriptableObject.CreateInstance<PaletteAssetStorage>();
            AssetDatabase.CreateAsset(paletteAssetStorage, assetPath);
            AssetDatabase.SaveAssets();

            UnityGenericPaletteProjectSettings.instance.SetPaletteAssetStorage(paletteAssetStorage);
            _paletteAssetStorage = paletteAssetStorage;
            _selectedPaletteAsset = null;
            _selectedProfileAsset = null;
            _selectedEntryIndex = -1;
            RebuildWindow();
        }
    }
}
