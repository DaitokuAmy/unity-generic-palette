using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
namespace UnityGenericPalette.Editor {
    /// <summary>
    /// PaletteEditorWindow の Profile タブ処理
    /// </summary>
    public sealed partial class PaletteEditorWindow {
        /// <summary>
        /// Profile Body の Gui を描画する
        /// </summary>
        private void DrawProfileBodyGui() {
            if (_paletteAssetStorage == null) {
                DrawPaletteAssetStorageMissingGui();
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
            CreateProfileAsset(paletteAsset, GenerateUniqueProfileId(paletteAsset), false);
        }

        /// <summary>
        /// ProfileAsset を生成する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="profileId">生成する ProfileId</param>
        /// <param name="setAsDefaultProfile">生成後に既定 Profile として登録するか</param>
        /// <returns>生成した ProfileAsset。生成できなかった場合は null</returns>
        private PaletteProfileAssetBase CreateProfileAsset(PaletteAssetBase paletteAsset, string profileId, bool setAsDefaultProfile) {
            var profileAssetType = GetProfileAssetType(paletteAsset);
            if (profileAssetType == null) {
                EditorUtility.DisplayDialog("Palette Editor", $"Profile asset type is not defined on {paletteAsset.GetType().Name}.", "OK");
                return null;
            }

            var profileFolderPath = EnsureProfileAssetFolderPath();
            if (string.IsNullOrEmpty(profileFolderPath)) {
                EditorUtility.DisplayDialog("Palette Editor", "Storage asset must be saved before adding a profile.", "OK");
                return null;
            }

            PaletteAssetIdentityEditorUtility.EnsurePaletteAssetGuid(paletteAsset);

            var profileAsset = CreateInstance(profileAssetType) as PaletteProfileAssetBase;
            if (profileAsset == null) {
                return null;
            }

            profileAsset.name = BuildProfileAssetAssetName(profileAssetType, profileId);

            var serializedObject = new SerializedObject(profileAsset);
            serializedObject.FindProperty("_profileId").stringValue = profileId;
            serializedObject.FindProperty("_sortOrder").intValue = GetNextProfileSortOrder(paletteAsset);
            serializedObject.FindProperty("_paletteGuid").stringValue = paletteAsset.PaletteGuid;
            serializedObject.FindProperty("_paletteLocalFileId").longValue = paletteAsset.PaletteLocalFileId;
            SynchronizeProfileValuesArray(serializedObject.FindProperty("_values"), paletteAsset.Entries);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{profileFolderPath}/{profileAsset.name}.asset");
            AssetDatabase.CreateAsset(profileAsset, assetPath);
            EditorUtility.SetDirty(profileAsset);
            PaletteProfileReferenceEditorUtility.SynchronizePaletteAssetProfileReferences(paletteAsset, false, false);
            if (setAsDefaultProfile) {
                SetDefaultProfileId(paletteAsset, profileId);
            }

            AssetDatabase.SaveAssets();

            _selectedProfileAsset = profileAsset;
            _selectedEntryIndex = -1;
            InvalidateProfileAssetList();
            RebuildWindow();
            return profileAsset;
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
            PaletteAssetIdentityEditorUtility.TryGetPaletteAsset(profileAsset, out var paletteAsset);
            ClearDefaultProfileIdIfMatched(paletteAsset, profileAsset.ProfileId);
            PaletteEditorProfileContext.Instance.ClearCurrentProfileIfMatched(profileAsset);

            var assetPath = AssetDatabase.GetAssetPath(profileAsset);
            if (!string.IsNullOrEmpty(assetPath)) {
                AssetDatabase.DeleteAsset(assetPath);
            }
            else {
                DestroyImmediate(profileAsset, true);
            }

            if (paletteAsset != null) {
                PaletteProfileReferenceEditorUtility.SynchronizePaletteAssetProfileReferences(paletteAsset, false, false);
            }
            AssetDatabase.SaveAssets();

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
            var defaultLabelRect = new Rect(rect.xMax - 88f, rect.y, 88f, rect.height);
            EditorGUI.LabelField(defaultLabelRect, "Default");
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

            var profileAsset = _profileAssetListItems[index];
            var isDefaultProfile = _selectedPaletteAsset != null &&
                profileAsset != null &&
                profileAsset.ProfileId == _selectedPaletteAsset.DefaultProfileId;
            var rowRect = new Rect(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight);
            var profileIdRect = new Rect(rowRect.x, rowRect.y, rowRect.width - 96f, rowRect.height);
            var defaultButtonRect = new Rect(rowRect.xMax - 88f, rowRect.y, 88f, rowRect.height);
            var serializedObject = new SerializedObject(profileAsset);
            var profileIdProperty = serializedObject.FindProperty("_profileId");
            if (profileIdProperty == null) {
                EditorGUI.LabelField(profileIdRect, GetProfileLabel(profileAsset));
                DrawDefaultProfileButton(defaultButtonRect, profileAsset, isDefaultProfile);
                return;
            }

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            var previousProfileId = profileIdProperty.stringValue;
            var profileId = EditorGUI.DelayedTextField(profileIdRect, previousProfileId);
            var changedProfileId = EditorGUI.EndChangeCheck();
            DrawDefaultProfileButton(defaultButtonRect, profileAsset, isDefaultProfile);
            if (!changedProfileId) {
                return;
            }

            profileIdProperty.stringValue = profileId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            RenameProfileAsset(profileAsset, profileId);
            if (PaletteAssetIdentityEditorUtility.TryGetPaletteAsset(profileAsset, out var paletteAsset)) {
                UpdateDefaultProfileIdIfMatched(paletteAsset, previousProfileId, profileId);
                PaletteProfileReferenceEditorUtility.SynchronizePaletteAssetProfileReferences(paletteAsset, false, false);
            }
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

        /// <summary>
        /// PaletteAsset の既定 ProfileId を設定する
        /// </summary>
        /// <param name="paletteAsset">設定対象の PaletteAsset</param>
        /// <param name="defaultProfileId">設定する ProfileId</param>
        private void SetDefaultProfileId(PaletteAssetBase paletteAsset, string defaultProfileId) {
            if (paletteAsset == null) {
                return;
            }

            var serializedObject = new SerializedObject(paletteAsset);
            var defaultProfileIdProperty = serializedObject.FindProperty("_defaultProfileId");
            if (defaultProfileIdProperty == null) {
                return;
            }

            serializedObject.Update();
            defaultProfileIdProperty.stringValue = defaultProfileId ?? string.Empty;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(paletteAsset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 既定 ProfileId が一致する場合だけ更新する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="previousProfileId">更新前の ProfileId</param>
        /// <param name="nextProfileId">更新後の ProfileId</param>
        private void UpdateDefaultProfileIdIfMatched(PaletteAssetBase paletteAsset, string previousProfileId, string nextProfileId) {
            if (paletteAsset == null || paletteAsset.DefaultProfileId != previousProfileId) {
                return;
            }

            SetDefaultProfileId(paletteAsset, nextProfileId);
        }

        /// <summary>
        /// 既定 ProfileId が一致する場合だけ解除する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="profileId">解除条件となる ProfileId</param>
        private void ClearDefaultProfileIdIfMatched(PaletteAssetBase paletteAsset, string profileId) {
            if (paletteAsset == null || paletteAsset.DefaultProfileId != profileId) {
                return;
            }

            SetDefaultProfileId(paletteAsset, string.Empty);
        }

        /// <summary>
        /// 既定 Profile 設定ボタンを描画する
        /// </summary>
        /// <param name="buttonRect">描画領域</param>
        /// <param name="profileAsset">対象の ProfileAsset</param>
        /// <param name="isDefaultProfile">既定 Profile かどうか</param>
        private void DrawDefaultProfileButton(Rect buttonRect, PaletteProfileAssetBase profileAsset, bool isDefaultProfile) {
            var buttonContent = isDefaultProfile
                ? new GUIContent("Default", "This profile is currently used as the default.")
                : new GUIContent("Set Default", "Use this profile as the default.");
            var previousBackgroundColor = GUI.backgroundColor;
            if (isDefaultProfile) {
                GUI.backgroundColor = new Color(0.24f, 0.62f, 0.98f, 1f);
            }

            var clicked = GUI.Button(buttonRect, buttonContent, EditorStyles.miniButton);
            GUI.backgroundColor = previousBackgroundColor;
            if (!clicked) {
                return;
            }

            ToggleDefaultProfile(profileAsset, isDefaultProfile);
        }

        /// <summary>
        /// 既定 Profile 設定を切り替える
        /// </summary>
        /// <param name="profileAsset">対象の ProfileAsset</param>
        /// <param name="isDefaultProfile">既定 Profile かどうか</param>
        private void ToggleDefaultProfile(PaletteProfileAssetBase profileAsset, bool isDefaultProfile) {
            if (profileAsset == null ||
                !PaletteAssetIdentityEditorUtility.TryGetPaletteAsset(profileAsset, out var paletteAsset)) {
                return;
            }

            if (!isDefaultProfile) {
                SetDefaultProfileId(paletteAsset, profileAsset.ProfileId);
                SetCurrentEditorProfile(profileAsset);
            }

            Repaint();
        }
    }
}
