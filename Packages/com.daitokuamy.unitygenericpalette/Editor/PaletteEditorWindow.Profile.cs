using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// PaletteEditorWindow の Profile タブ処理
    /// </summary>
    public sealed partial class PaletteEditorWindow {
        /// <summary>
        /// Profile Body の IMGUI を描画する
        /// </summary>
        private void DrawProfileBodyIMGUI() {
            if (_paletteAssetStorage == null) {
                DrawPaletteAssetStorageMissingIMGUI();
                return;
            }

            if (_selectedPaletteAsset == null) {
                EditorGUILayout.HelpBox("Palette を選択してください。", MessageType.Info);
                return;
            }

            EnsureProfileAssetList();
            if (_profileAssetList == null) {
                return;
            }

            using var scrollViewScope = new EditorGUILayout.ScrollViewScope(_profileBodyScrollPosition);
            _profileBodyScrollPosition = scrollViewScope.scrollPosition;
            _profileAssetList.DoLayoutList();
        }

        /// <summary>
        /// ProfileAsset を生成する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        private void CreateProfileAsset(PaletteAssetBase paletteAsset) {
            var profileAssetType = GetProfileAssetType(paletteAsset);
            if (profileAssetType == null) {
                EditorUtility.DisplayDialog("Palette Editor", $"Profile asset type is not defined on {paletteAsset.GetType().Name}.", "OK");
                return;
            }

            var profileFolderPath = EnsureProfileAssetFolderPath();
            if (string.IsNullOrEmpty(profileFolderPath)) {
                EditorUtility.DisplayDialog("Palette Editor", "Storage asset must be saved before adding a profile.", "OK");
                return;
            }

            var profileId = GenerateUniqueProfileId(paletteAsset);
            var profileAsset = CreateInstance(profileAssetType) as PaletteProfileAssetBase;
            if (profileAsset == null) {
                return;
            }

            profileAsset.name = BuildProfileAssetAssetName(profileAssetType, profileId);

            var serializedObject = new SerializedObject(profileAsset);
            serializedObject.FindProperty("_profileId").stringValue = profileId;
            serializedObject.FindProperty("_sortOrder").intValue = GetNextProfileSortOrder(paletteAsset);
            serializedObject.FindProperty("_paletteAsset").objectReferenceValue = paletteAsset;
            SynchronizeProfileValuesArray(serializedObject.FindProperty("_values"), paletteAsset.Entries);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{profileFolderPath}/{profileAsset.name}.asset");
            AssetDatabase.CreateAsset(profileAsset, assetPath);
            EditorUtility.SetDirty(profileAsset);
            AssetDatabase.SaveAssets();

            _selectedProfileAsset = profileAsset;
            _selectedEntryIndex = -1;
            InvalidateProfileAssetList();
            RebuildWindow();
        }

        /// <summary>
        /// 選択中 ProfileAsset を削除する
        /// </summary>
        private void RemoveSelectedProfileAsset() {
            if (_selectedProfileAsset == null) {
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete Profile", $"Delete {_selectedProfileAsset.name}?", "Delete", "Cancel")) {
                return;
            }

            DeleteProfileAsset(_selectedProfileAsset, true);
            RebuildWindow();
        }

        /// <summary>
        /// ProfileAsset を削除する
        /// </summary>
        /// <param name="profileAsset">削除対象</param>
        /// <param name="resetSelection">選択状態をリセットするか</param>
        private void DeleteProfileAsset(PaletteProfileAssetBase profileAsset, bool resetSelection) {
            var assetPath = AssetDatabase.GetAssetPath(profileAsset);
            if (!string.IsNullOrEmpty(assetPath)) {
                AssetDatabase.DeleteAsset(assetPath);
            }
            else {
                DestroyImmediate(profileAsset, true);
            }

            if (!resetSelection) {
                return;
            }

            _selectedProfileAsset = null;
            _selectedEntryIndex = -1;
            InvalidateProfileAssetList();
        }

        /// <summary>
        /// Profile 用 ReorderableList を初期化する
        /// </summary>
        private void EnsureProfileAssetList() {
            if (_selectedPaletteAsset == null) {
                _profileAssetList = null;
                _profileAssetListPaletteAsset = null;
                _profileAssetListItems = null;
                return;
            }

            if (_profileAssetList != null && _profileAssetListPaletteAsset == _selectedPaletteAsset) {
                return;
            }

            _profileAssetListItems = GetProfileAssets(_selectedPaletteAsset);
            _profileAssetList = new ReorderableList(_profileAssetListItems, typeof(PaletteProfileAssetBase), true, true, true, true);
            _profileAssetListPaletteAsset = _selectedPaletteAsset;
            _profileAssetList.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            _profileAssetList.drawHeaderCallback = DrawProfileAssetListHeader;
            _profileAssetList.drawElementCallback = DrawProfileAssetListElement;
            _profileAssetList.onSelectCallback = OnSelectProfileAsset;
            _profileAssetList.onAddCallback = OnAddProfileAsset;
            _profileAssetList.onRemoveCallback = OnRemoveProfileAsset;
            _profileAssetList.onReorderCallback = OnReorderProfileAsset;
            _profileAssetList.index = Mathf.Clamp(_profileAssetListItems.IndexOf(_selectedProfileAsset), 0, Mathf.Max(0, _profileAssetListItems.Count - 1));
        }

        /// <summary>
        /// Profile 用 ReorderableList を無効化する
        /// </summary>
        private void InvalidateProfileAssetList() {
            _profileAssetList = null;
            _profileAssetListPaletteAsset = null;
            _profileAssetListItems = null;
        }

        /// <summary>
        /// Profile 用 ReorderableList の Header を描画する
        /// </summary>
        /// <param name="rect">描画領域</param>
        private void DrawProfileAssetListHeader(Rect rect) {
            EditorGUI.LabelField(rect, "Profile Id");
        }

        /// <summary>
        /// Profile 用 ReorderableList の要素を描画する
        /// </summary>
        /// <param name="rect">描画領域</param>
        /// <param name="index">要素 index</param>
        /// <param name="isActive">アクティブ状態</param>
        /// <param name="isFocused">フォーカス状態</param>
        private void DrawProfileAssetListElement(Rect rect, int index, bool isActive, bool isFocused) {
            if (_profileAssetListItems == null || index < 0 || index >= _profileAssetListItems.Count) {
                return;
            }

            var rowRect = new Rect(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight);
            var profileAsset = _profileAssetListItems[index];
            var serializedObject = new SerializedObject(profileAsset);
            var profileIdProperty = serializedObject.FindProperty("_profileId");
            if (profileIdProperty == null) {
                EditorGUI.LabelField(rowRect, GetProfileLabel(profileAsset));
                return;
            }

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            var profileId = EditorGUI.DelayedTextField(rowRect, profileIdProperty.stringValue);
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }

            profileIdProperty.stringValue = profileId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            RenameProfileAsset(profileAsset, profileId);
            EditorUtility.SetDirty(profileAsset);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        /// <summary>
        /// Profile 用 ReorderableList の選択変更を処理する
        /// </summary>
        /// <param name="list">対象の ReorderableList</param>
        private void OnSelectProfileAsset(ReorderableList list) {
            if (_profileAssetListItems == null || list.index < 0 || list.index >= _profileAssetListItems.Count) {
                _selectedProfileAsset = null;
                Repaint();
                return;
            }

            _selectedProfileAsset = _profileAssetListItems[list.index];
            Repaint();
        }

        /// <summary>
        /// Profile 用 ReorderableList から Profile 追加を処理する
        /// </summary>
        /// <param name="list">対象の ReorderableList</param>
        private void OnAddProfileAsset(ReorderableList list) {
            CreateProfileAsset(_selectedPaletteAsset);
        }

        /// <summary>
        /// Profile 用 ReorderableList から Profile 削除を処理する
        /// </summary>
        /// <param name="list">対象の ReorderableList</param>
        private void OnRemoveProfileAsset(ReorderableList list) {
            if (_profileAssetListItems == null || list.index < 0 || list.index >= _profileAssetListItems.Count) {
                return;
            }

            _selectedProfileAsset = _profileAssetListItems[list.index];
            RemoveSelectedProfileAsset();
        }

        /// <summary>
        /// Profile 用 ReorderableList の並び替え後処理
        /// </summary>
        /// <param name="list">対象の ReorderableList</param>
        private void OnReorderProfileAsset(ReorderableList list) {
            if (_profileAssetListItems == null) {
                return;
            }

            for (var i = 0; i < _profileAssetListItems.Count; i++) {
                var profileAsset = _profileAssetListItems[i];
                if (profileAsset == null) {
                    continue;
                }

                var serializedObject = new SerializedObject(profileAsset);
                var sortOrderProperty = serializedObject.FindProperty("_sortOrder");
                if (sortOrderProperty == null) {
                    continue;
                }

                sortOrderProperty.intValue = i;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profileAsset);
            }

            _selectedProfileAsset = list.index >= 0 && list.index < _profileAssetListItems.Count
                ? _profileAssetListItems[list.index]
                : null;
            AssetDatabase.SaveAssets();
            Repaint();
        }

        /// <summary>
        /// ProfileAsset 保存用フォルダを作成して取得する
        /// </summary>
        /// <returns>保存先フォルダパス</returns>
        private string EnsureProfileAssetFolderPath() {
            if (_paletteAssetStorage == null) {
                return null;
            }

            var storagePath = AssetDatabase.GetAssetPath(_paletteAssetStorage);
            if (string.IsNullOrEmpty(storagePath)) {
                return null;
            }

            var storageFolderPath = Path.GetDirectoryName(storagePath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(storageFolderPath)) {
                return null;
            }

            var profileFolderPath = $"{storageFolderPath}/PaletteProfiles";
            if (AssetDatabase.IsValidFolder(profileFolderPath)) {
                return profileFolderPath;
            }

            AssetDatabase.CreateFolder(storageFolderPath, "PaletteProfiles");
            return profileFolderPath;
        }

        /// <summary>
        /// ProfileAsset のアセット名を組み立てる
        /// </summary>
        /// <param name="profileAssetType">ProfileAsset の型</param>
        /// <param name="profileId">ProfileId</param>
        /// <returns>アセット名</returns>
        private string BuildProfileAssetAssetName(Type profileAssetType, string profileId) {
            var safeProfileId = SanitizeAssetFileName(profileId);
            return $"{profileAssetType.Name}_{safeProfileId}";
        }

        /// <summary>
        /// ProfileAsset のアセット名とファイル名を更新する
        /// </summary>
        /// <param name="profileAsset">対象 ProfileAsset</param>
        /// <param name="profileId">更新後の ProfileId</param>
        private void RenameProfileAsset(PaletteProfileAssetBase profileAsset, string profileId) {
            if (profileAsset == null) {
                return;
            }

            var profileFolderPath = EnsureProfileAssetFolderPath();
            if (string.IsNullOrEmpty(profileFolderPath)) {
                return;
            }

            var currentAssetPath = AssetDatabase.GetAssetPath(profileAsset);
            if (string.IsNullOrEmpty(currentAssetPath)) {
                return;
            }

            var targetAssetName = BuildProfileAssetAssetName(profileAsset.GetType(), profileId);
            var targetAssetPath = AssetDatabase.GenerateUniqueAssetPath($"{profileFolderPath}/{targetAssetName}.asset");
            if (!string.Equals(currentAssetPath, targetAssetPath, StringComparison.Ordinal)) {
                var errorMessage = AssetDatabase.MoveAsset(currentAssetPath, targetAssetPath);
                if (!string.IsNullOrEmpty(errorMessage)) {
                    Debug.LogWarning($"Failed to rename profile asset: {errorMessage}");
                    return;
                }
            }

            profileAsset.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(profileAsset));
            EditorUtility.SetDirty(profileAsset);
        }

        /// <summary>
        /// アセットファイル名として不正な文字を置換する
        /// </summary>
        /// <param name="fileName">変換対象の文字列</param>
        /// <returns>安全なファイル名</returns>
        private string SanitizeAssetFileName(string fileName) {
            if (string.IsNullOrEmpty(fileName)) {
                return string.Empty;
            }

            var sanitizedFileName = fileName;
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            for (var i = 0; i < invalidFileNameChars.Length; i++) {
                sanitizedFileName = sanitizedFileName.Replace(invalidFileNameChars[i], '_');
            }

            return sanitizedFileName;
        }
    }
}
