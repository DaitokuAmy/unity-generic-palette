using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityGenericPalette.Editor {
    /// <summary>
    /// PaletteEditorWindow の Palette タブ処理
    /// </summary>
    public sealed partial class PaletteEditorWindow {
        /// <summary>
        /// Palette Body の Gui を描画する
        /// </summary>
        private void DrawPaletteBodyGui() {
            if (_paletteAssetStorage == null) {
                DrawPaletteAssetStorageMissingGui();
                return;
            }

            if (_selectedPaletteAsset == null) {
                EditorGUILayout.HelpBox("Palette を選択してください。", MessageType.Info);
                return;
            }

            if (_selectedProfileAsset == null) {
                EditorGUILayout.HelpBox("Profile を作成して Header から選択してください。", MessageType.Info);
                return;
            }

            EnsurePaletteEntryList();
            if (_paletteEntryList == null) {
                return;
            }

            using var scrollViewScope = new EditorGUILayout.ScrollViewScope(_paletteBodyScrollPosition);
            _paletteBodyScrollPosition = scrollViewScope.scrollPosition;

            var paletteEntryList = _paletteEntryList;
            var serializedProperty = paletteEntryList.serializedProperty;
            if (serializedProperty == null) {
                return;
            }

            var serializedObject = serializedProperty.serializedObject;
            if (serializedObject == null) {
                return;
            }

            serializedObject.Update();
            paletteEntryList.DoLayoutList();

            if (_paletteEntryList != paletteEntryList || paletteEntryList.serializedProperty == null) {
                return;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Palette Inspector の Gui を描画する
        /// </summary>
        private void DrawPaletteInspectorGui() {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                GUILayout.Label("Inspector", EditorStyles.boldLabel);
                EditorGUILayout.Space(4f);

                if (_selectedPaletteAsset == null) {
                    EditorGUILayout.HelpBox("Palette を選択してください。", MessageType.Info);
                    return;
                }

                if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _selectedPaletteAsset.Entries.Count) {
                    EditorGUILayout.HelpBox("Entry を選択してください。", MessageType.Info);
                    return;
                }

                DrawSelectedEntryInspectorGui();

                EditorGUILayout.Space(8f);

                if (_selectedProfileAsset == null) {
                    EditorGUILayout.HelpBox("Profile を Header から選択してください。", MessageType.Info);
                    return;
                }

                DrawSelectedCellInspectorGui();
            }
        }

        /// <summary>
        /// 選択中 Entry の Inspector を描画する
        /// </summary>
        private void DrawSelectedEntryInspectorGui() {
            GUILayout.Label("Entry", EditorStyles.miniBoldLabel);

            var serializedObject = new SerializedObject(_selectedPaletteAsset);
            var entriesProperty = serializedObject.FindProperty("_entries");
            if (!IsValidArrayIndex(entriesProperty, _selectedEntryIndex)) {
                return;
            }

            serializedObject.Update();
            var entryProperty = entriesProperty.GetArrayElementAtIndex(_selectedEntryIndex);
            var entryIdProperty = entryProperty.FindPropertyRelative("_entryId");
            EditorGUILayout.PropertyField(entryIdProperty, new GUIContent("Entry Id"));
            EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("_displayName"), new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("_description"), new GUIContent("Description"));
            if (!serializedObject.ApplyModifiedProperties()) {
                return;
            }

            _selectedPaletteAsset.InvalidateEntryIndexCache();
            EditorUtility.SetDirty(_selectedPaletteAsset);
            SynchronizeProfileValues(_selectedPaletteAsset);
        }

        /// <summary>
        /// 選択中セルの Inspector を描画する
        /// </summary>
        private void DrawSelectedCellInspectorGui() {
            GUILayout.Label("Profile Value", EditorStyles.miniBoldLabel);

            var serializedObject = new SerializedObject(_selectedProfileAsset);
            var valuesProperty = serializedObject.FindProperty("_values");
            if (!IsValidArrayIndex(valuesProperty, _selectedEntryIndex)) {
                EditorGUILayout.HelpBox("選択セルの値が見つかりません。", MessageType.Warning);
                return;
            }

            var valueProperty = valuesProperty.GetArrayElementAtIndex(_selectedEntryIndex);

            serializedObject.Update();
            EditorGUILayout.PropertyField(valueProperty, true);
            if (serializedObject.ApplyModifiedProperties()) {
                _selectedProfileAsset.InvalidateCache();
                EditorUtility.SetDirty(_selectedProfileAsset);
                PaletteEditorProfileContext.Instance.NotifyProfileChanged(_selectedProfileAsset);
            }

            EditorGUILayout.Space(6f);
            DrawCopyProfileValueButtonGui();
        }

        /// <summary>
        /// 他の Profile から値をコピーするボタンを描画する
        /// </summary>
        private void DrawCopyProfileValueButtonGui() {
            var hasCopySource = false;
            var profileAssets = GetProfileAssets(_selectedPaletteAsset);
            for (var i = 0; i < profileAssets.Count; i++) {
                if (profileAssets[i] == null || profileAssets[i] == _selectedProfileAsset) {
                    continue;
                }

                hasCopySource = true;
                break;
            }

            using (new EditorGUI.DisabledScope(!hasCopySource)) {
                if (GUILayout.Button("Copy From...")) {
                    ShowCopyProfileValueMenu();
                }
            }
        }

        /// <summary>
        /// 他の Profile から値をコピーするメニューを表示する
        /// </summary>
        private void ShowCopyProfileValueMenu() {
            var profileAssets = GetProfileAssets(_selectedPaletteAsset);
            var menu = new GenericMenu();
            var hasItem = false;

            for (var i = 0; i < profileAssets.Count; i++) {
                var profileAsset = profileAssets[i];
                if (profileAsset == null || profileAsset == _selectedProfileAsset) {
                    continue;
                }

                hasItem = true;
                menu.AddItem(new GUIContent(GetProfileLabel(profileAsset)), false, () => CopyProfileValueFrom(profileAsset));
            }

            if (!hasItem) {
                menu.AddDisabledItem(new GUIContent("No profile available"));
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// 指定した Profile から現在の値をコピーする
        /// </summary>
        /// <param name="sourceProfileAsset">コピー元 ProfileAsset</param>
        private void CopyProfileValueFrom(PaletteProfileAssetBase sourceProfileAsset) {
            if (_selectedPaletteAsset == null || _selectedProfileAsset == null || sourceProfileAsset == null) {
                return;
            }

            if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _selectedPaletteAsset.Entries.Count) {
                return;
            }

            var sourceSerializedObject = new SerializedObject(sourceProfileAsset);
            var sourceValuesProperty = sourceSerializedObject.FindProperty("_values");
            if (!IsValidArrayIndex(sourceValuesProperty, _selectedEntryIndex)) {
                EditorUtility.DisplayDialog("Palette Editor", $"Profile '{GetProfileLabel(sourceProfileAsset)}' does not contain the selected entry index.", "OK");
                return;
            }

            var targetSerializedObject = new SerializedObject(_selectedProfileAsset);
            var targetValuesProperty = targetSerializedObject.FindProperty("_values");
            if (!IsValidArrayIndex(targetValuesProperty, _selectedEntryIndex)) {
                EditorUtility.DisplayDialog("Palette Editor", $"Profile '{GetProfileLabel(_selectedProfileAsset)}' does not contain the selected entry index.", "OK");
                return;
            }

            sourceSerializedObject.Update();
            targetSerializedObject.Update();

            var sourceValueProperty = sourceValuesProperty.GetArrayElementAtIndex(_selectedEntryIndex);
            var targetValueProperty = targetValuesProperty.GetArrayElementAtIndex(_selectedEntryIndex);
            CopySerializedPropertyValue(sourceValueProperty, targetValueProperty);

            targetSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_selectedProfileAsset);
            PaletteEditorProfileContext.Instance.NotifyProfileChanged(_selectedProfileAsset);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        /// <summary>
        /// SerializedProperty の値をコピーする
        /// </summary>
        /// <param name="sourceProperty">コピー元</param>
        /// <param name="destinationProperty">コピー先</param>
        private void CopySerializedPropertyValue(SerializedProperty sourceProperty, SerializedProperty destinationProperty) {
            if (sourceProperty == null || destinationProperty == null) {
                return;
            }

            if (sourceProperty.isArray && sourceProperty.propertyType != SerializedPropertyType.String) {
                destinationProperty.arraySize = sourceProperty.arraySize;
                for (var i = 0; i < sourceProperty.arraySize; i++) {
                    CopySerializedPropertyValue(sourceProperty.GetArrayElementAtIndex(i), destinationProperty.GetArrayElementAtIndex(i));
                }

                return;
            }

            switch (sourceProperty.propertyType) {
                case SerializedPropertyType.Integer:
                    destinationProperty.intValue = sourceProperty.intValue;
                    return;
                case SerializedPropertyType.Boolean:
                    destinationProperty.boolValue = sourceProperty.boolValue;
                    return;
                case SerializedPropertyType.Float:
                    destinationProperty.floatValue = sourceProperty.floatValue;
                    return;
                case SerializedPropertyType.String:
                    destinationProperty.stringValue = sourceProperty.stringValue;
                    return;
                case SerializedPropertyType.Color:
                    destinationProperty.colorValue = sourceProperty.colorValue;
                    return;
                case SerializedPropertyType.ObjectReference:
                    destinationProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                    return;
                case SerializedPropertyType.LayerMask:
                    destinationProperty.intValue = sourceProperty.intValue;
                    return;
                case SerializedPropertyType.Enum:
                    destinationProperty.enumValueIndex = sourceProperty.enumValueIndex;
                    return;
                case SerializedPropertyType.Vector2:
                    destinationProperty.vector2Value = sourceProperty.vector2Value;
                    return;
                case SerializedPropertyType.Vector3:
                    destinationProperty.vector3Value = sourceProperty.vector3Value;
                    return;
                case SerializedPropertyType.Vector4:
                    destinationProperty.vector4Value = sourceProperty.vector4Value;
                    return;
                case SerializedPropertyType.Rect:
                    destinationProperty.rectValue = sourceProperty.rectValue;
                    return;
                case SerializedPropertyType.ArraySize:
                    destinationProperty.intValue = sourceProperty.intValue;
                    return;
                case SerializedPropertyType.Character:
                    destinationProperty.intValue = sourceProperty.intValue;
                    return;
                case SerializedPropertyType.AnimationCurve:
                    destinationProperty.animationCurveValue = sourceProperty.animationCurveValue;
                    return;
                case SerializedPropertyType.Bounds:
                    destinationProperty.boundsValue = sourceProperty.boundsValue;
                    return;
                case SerializedPropertyType.Gradient:
                    destinationProperty.gradientValue = sourceProperty.gradientValue;
                    return;
                case SerializedPropertyType.Quaternion:
                    destinationProperty.quaternionValue = sourceProperty.quaternionValue;
                    return;
                case SerializedPropertyType.ExposedReference:
                    destinationProperty.exposedReferenceValue = sourceProperty.exposedReferenceValue;
                    return;
                case SerializedPropertyType.FixedBufferSize:
                    destinationProperty.intValue = sourceProperty.intValue;
                    return;
                case SerializedPropertyType.Vector2Int:
                    destinationProperty.vector2IntValue = sourceProperty.vector2IntValue;
                    return;
                case SerializedPropertyType.Vector3Int:
                    destinationProperty.vector3IntValue = sourceProperty.vector3IntValue;
                    return;
                case SerializedPropertyType.RectInt:
                    destinationProperty.rectIntValue = sourceProperty.rectIntValue;
                    return;
                case SerializedPropertyType.BoundsInt:
                    destinationProperty.boundsIntValue = sourceProperty.boundsIntValue;
                    return;
                case SerializedPropertyType.ManagedReference:
                    destinationProperty.managedReferenceValue = sourceProperty.managedReferenceValue;
                    return;
                case SerializedPropertyType.Hash128:
                    destinationProperty.hash128Value = sourceProperty.hash128Value;
                    return;
                case SerializedPropertyType.Generic:
                    CopyGenericPropertyValue(sourceProperty, destinationProperty);
                    return;
                default:
                    return;
            }
        }

        /// <summary>
        /// Generic な SerializedProperty の値を再帰的にコピーする
        /// </summary>
        /// <param name="sourceProperty">コピー元</param>
        /// <param name="destinationProperty">コピー先</param>
        private void CopyGenericPropertyValue(SerializedProperty sourceProperty, SerializedProperty destinationProperty) {
            var sourceIterator = sourceProperty.Copy();
            var sourceEndProperty = sourceIterator.GetEndProperty();
            var enterChildren = true;
            while (sourceIterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(sourceIterator, sourceEndProperty)) {
                enterChildren = false;

                var relativePropertyPath = sourceIterator.propertyPath.Substring(sourceProperty.propertyPath.Length + 1);
                var destinationChildProperty = destinationProperty.FindPropertyRelative(relativePropertyPath);
                if (destinationChildProperty == null) {
                    continue;
                }

                CopySerializedPropertyValue(sourceIterator, destinationChildProperty);
            }
        }

        /// <summary>
        /// Palette 追加メニューを表示する
        /// </summary>
        private void ShowAddPaletteMenu() {
            var existingPaletteTypes = new HashSet<Type>();
            var paletteAssets = GetPaletteAssets();
            for (var i = 0; i < paletteAssets.Count; i++) {
                existingPaletteTypes.Add(paletteAssets[i].GetType());
            }

            var menu = new GenericMenu();
            var paletteAssetTypes = TypeCache.GetTypesDerivedFrom<PaletteAssetBase>();
            var hasItem = false;
            for (var i = 0; i < paletteAssetTypes.Count; i++) {
                var paletteAssetType = paletteAssetTypes[i];
                if (paletteAssetType.IsAbstract || existingPaletteTypes.Contains(paletteAssetType)) {
                    continue;
                }

                hasItem = true;
                menu.AddItem(new GUIContent(paletteAssetType.Name), false, () => CreatePaletteAsset(paletteAssetType));
            }

            if (!hasItem) {
                menu.AddDisabledItem(new GUIContent("No palette type available"));
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// PaletteAsset を生成する
        /// </summary>
        /// <param name="paletteAssetType">生成する型</param>
        private void CreatePaletteAsset(Type paletteAssetType) {
            if (_paletteAssetStorage == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_paletteAssetStorage))) {
                EditorUtility.DisplayDialog("Palette Editor", "Storage asset must be saved before adding a palette.", "OK");
                return;
            }

            var paletteAsset = CreateInstance(paletteAssetType) as PaletteAssetBase;
            if (paletteAsset == null) {
                return;
            }

            paletteAsset.name = paletteAssetType.Name;
            AssetDatabase.AddObjectToAsset(paletteAsset, _paletteAssetStorage);
            AddPaletteReference(paletteAsset);
            EditorUtility.SetDirty(paletteAsset);
            EditorUtility.SetDirty(_paletteAssetStorage);
            AssetDatabase.SaveAssets();
            PaletteAssetIdentityEditorUtility.EnsurePaletteAssetGuid(paletteAsset);
            AssetDatabase.SaveAssets();

            _selectedPaletteAsset = paletteAsset;
            _selectedEntryIndex = -1;
            InvalidatePaletteEntryList();
            InvalidateProfileAssetList();
            _selectedProfileAsset = CreateProfileAsset(paletteAsset, "Default", true);
            RebuildWindow();
        }

        /// <summary>
        /// Storage に PaletteAsset 参照を追加する
        /// </summary>
        /// <param name="paletteAsset">追加対象の PaletteAsset</param>
        private void AddPaletteReference(PaletteAssetBase paletteAsset) {
            if (_paletteAssetStorage == null || paletteAsset == null) {
                return;
            }

            var paletteAssets = GetPaletteAssetList(_paletteAssetStorage);
            if (paletteAssets == null || paletteAssets.Contains(paletteAsset)) {
                return;
            }

            Undo.RecordObject(_paletteAssetStorage, "Add Palette");
            paletteAssets.Add(paletteAsset);
            EditorUtility.SetDirty(_paletteAssetStorage);
        }

        /// <summary>
        /// 選択中 PaletteAsset を削除する
        /// </summary>
        private void RemoveSelectedPalette() {
            if (_selectedPaletteAsset == null) {
                return;
            }

            var paletteAsset = _selectedPaletteAsset;
            if (!EditorUtility.DisplayDialog("Remove Palette", $"Remove {paletteAsset.GetType().Name} and related profiles?", "Remove", "Cancel")) {
                return;
            }

            RemovePaletteAsset(paletteAsset);
        }

        /// <summary>
        /// 選択中 PaletteAsset を削除する
        /// </summary>
        /// <param name="paletteAsset">削除対象の PaletteAsset</param>
        private void RemovePaletteAsset(PaletteAssetBase paletteAsset) {
            if (paletteAsset == null || _paletteAssetStorage == null) {
                return;
            }

            var profileAssets = GetProfileAssets(paletteAsset);
            for (var i = 0; i < profileAssets.Count; i++) {
                DeleteProfileAsset(profileAssets[i], false);
            }

            RemovePaletteReference(paletteAsset);
            EditorUtility.SetDirty(_paletteAssetStorage);
            Undo.DestroyObjectImmediate(paletteAsset);

            _selectedPaletteAsset = null;
            _selectedProfileAsset = null;
            _selectedEntryIndex = -1;
            InvalidatePaletteEntryList();
            InvalidateProfileAssetList();
            RebuildWindow();
        }

        /// <summary>
        /// Storage から PaletteAsset 参照を削除する
        /// </summary>
        /// <param name="paletteAsset">削除対象の PaletteAsset</param>
        private void RemovePaletteReference(PaletteAssetBase paletteAsset) {
            if (_paletteAssetStorage == null || paletteAsset == null) {
                return;
            }

            var paletteAssets = GetPaletteAssetList(_paletteAssetStorage);
            if (paletteAssets == null) {
                return;
            }

            Undo.RecordObject(_paletteAssetStorage, "Remove Palette");
            paletteAssets.Remove(paletteAsset);
        }

        /// <summary>
        /// PaletteAssetStorage が保持する PaletteAsset 一覧を取得する
        /// </summary>
        /// <param name="paletteAssetStorage">参照対象の Storage</param>
        /// <returns>PaletteAsset 一覧</returns>
        private List<PaletteAssetBase> GetPaletteAssetList(PaletteAssetStorage paletteAssetStorage) {
            if (paletteAssetStorage == null) {
                return null;
            }

            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var paletteAssetsField = typeof(PaletteAssetStorage).GetField("_paletteAssets", bindingFlags);
            return paletteAssetsField?.GetValue(paletteAssetStorage) as List<PaletteAssetBase>;
        }

        /// <summary>
        /// Entry を追加する
        /// </summary>
        private void AddEntryToSelectedPalette() {
            if (_selectedPaletteAsset == null) {
                return;
            }

            var serializedObject = new SerializedObject(_selectedPaletteAsset);
            var entriesProperty = serializedObject.FindProperty("_entries");
            var insertIndex = entriesProperty.arraySize;
            entriesProperty.InsertArrayElementAtIndex(insertIndex);

            var entryProperty = entriesProperty.GetArrayElementAtIndex(insertIndex);
            entryProperty.FindPropertyRelative("_entryId").stringValue = GenerateUniqueEntryId(_selectedPaletteAsset);
            entryProperty.FindPropertyRelative("_displayName").stringValue = $"Entry {insertIndex + 1}";
            entryProperty.FindPropertyRelative("_description").stringValue = string.Empty;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            _selectedPaletteAsset.InvalidateEntryIndexCache();
            EditorUtility.SetDirty(_selectedPaletteAsset);
            SynchronizeProfileValues(_selectedPaletteAsset);

            _selectedEntryIndex = insertIndex;
            InvalidatePaletteEntryList();
            RebuildWindow();
        }

        /// <summary>
        /// 選択中 Entry を削除する
        /// </summary>
        private void RemoveSelectedEntry() {
            if (_selectedPaletteAsset == null || _selectedEntryIndex < 0) {
                return;
            }

            var serializedObject = new SerializedObject(_selectedPaletteAsset);
            var entriesProperty = serializedObject.FindProperty("_entries");
            if (!IsValidArrayIndex(entriesProperty, _selectedEntryIndex)) {
                return;
            }

            entriesProperty.DeleteArrayElementAtIndex(_selectedEntryIndex);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            _selectedPaletteAsset.InvalidateEntryIndexCache();
            EditorUtility.SetDirty(_selectedPaletteAsset);
            SynchronizeProfileValues(_selectedPaletteAsset);

            _selectedEntryIndex = -1;
            InvalidatePaletteEntryList();
            RebuildWindow();
        }

        /// <summary>
        /// Palette に対応する ProfileValue 配列を同期する
        /// </summary>
        /// <param name="paletteAsset">同期対象の PaletteAsset</param>
        private void SynchronizeProfileValues(PaletteAssetBase paletteAsset) {
            var profileAssets = GetProfileAssets(paletteAsset);
            for (var i = 0; i < profileAssets.Count; i++) {
                var serializedObject = new SerializedObject(profileAssets[i]);
                SynchronizeProfileValuesArray(serializedObject.FindProperty("_values"), paletteAsset.Entries);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                profileAssets[i].InvalidateCache();
                EditorUtility.SetDirty(profileAssets[i]);
            }
        }

        /// <summary>
        /// Palette と ProfileValue 配列の不整合があれば同期する
        /// </summary>
        /// <param name="paletteAsset">確認対象の PaletteAsset</param>
        private void EnsureProfileValuesSynchronized(PaletteAssetBase paletteAsset) {
            if (paletteAsset == null) {
                return;
            }

            var profileAssets = GetProfileAssets(paletteAsset);
            for (var i = 0; i < profileAssets.Count; i++) {
                var serializedObject = new SerializedObject(profileAssets[i]);
                var valuesProperty = serializedObject.FindProperty("_values");
                if (IsProfileValuesArraySynchronized(valuesProperty, paletteAsset.Entries)) {
                    continue;
                }

                SynchronizeProfileValues(paletteAsset);
                return;
            }
        }

        /// <summary>
        /// ProfileValue 配列を Entry 一覧へ同期する
        /// </summary>
        /// <param name="valuesProperty">同期対象の配列</param>
        /// <param name="entries">同期元 Entry 一覧</param>
        private void SynchronizeProfileValuesArray(SerializedProperty valuesProperty, IReadOnlyList<PaletteEntry> entries) {
            while (valuesProperty.arraySize < entries.Count) {
                var insertIndex = valuesProperty.arraySize;
                valuesProperty.InsertArrayElementAtIndex(insertIndex);
                ResetProfileValue(valuesProperty.GetArrayElementAtIndex(insertIndex));
            }

            while (valuesProperty.arraySize > entries.Count) {
                valuesProperty.DeleteArrayElementAtIndex(valuesProperty.arraySize - 1);
            }
        }

        /// <summary>
        /// ProfileValue 配列が Entry 一覧と一致しているか判定する
        /// </summary>
        /// <param name="valuesProperty">判定対象の配列</param>
        /// <param name="entries">比較元 Entry 一覧</param>
        /// <returns>一致している場合は true</returns>
        private bool IsProfileValuesArraySynchronized(SerializedProperty valuesProperty, IReadOnlyList<PaletteEntry> entries) {
            return valuesProperty != null && entries != null && valuesProperty.arraySize == entries.Count;
        }

        /// <summary>
        /// Palette Entry の並び替えに合わせて ProfileValue 配列も並び替える
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="previousEntryIds">並び替え前の EntryId 一覧</param>
        /// <param name="nextEntryIds">並び替え後の EntryId 一覧</param>
        private void ReorderProfileValues(PaletteAssetBase paletteAsset, IReadOnlyList<string> previousEntryIds, IReadOnlyList<string> nextEntryIds) {
            if (paletteAsset == null ||
                previousEntryIds == null ||
                nextEntryIds == null ||
                previousEntryIds.Count != nextEntryIds.Count) {
                SynchronizeProfileValues(paletteAsset);
                return;
            }

            var previousIndexByEntryId = new Dictionary<string, int>(previousEntryIds.Count);
            for (var i = 0; i < previousEntryIds.Count; i++) {
                if (string.IsNullOrEmpty(previousEntryIds[i]) || previousIndexByEntryId.ContainsKey(previousEntryIds[i])) {
                    SynchronizeProfileValues(paletteAsset);
                    return;
                }

                previousIndexByEntryId.Add(previousEntryIds[i], i);
            }

            var reorderedIndices = new int[nextEntryIds.Count];
            for (var i = 0; i < nextEntryIds.Count; i++) {
                if (!previousIndexByEntryId.TryGetValue(nextEntryIds[i], out reorderedIndices[i])) {
                    SynchronizeProfileValues(paletteAsset);
                    return;
                }
            }

            var profileAssets = GetProfileAssets(paletteAsset);
            for (var i = 0; i < profileAssets.Count; i++) {
                var serializedObject = new SerializedObject(profileAssets[i]);
                var valuesProperty = serializedObject.FindProperty("_values");
                if (!IsValidArrayIndex(valuesProperty, previousEntryIds.Count - 1)) {
                    SynchronizeProfileValues(paletteAsset);
                    return;
                }

                ReorderProfileValuesArray(valuesProperty, reorderedIndices);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                profileAssets[i].InvalidateCache();
                EditorUtility.SetDirty(profileAssets[i]);
            }
        }

        /// <summary>
        /// ProfileValue 配列を指定した index 順へ並び替える
        /// </summary>
        /// <param name="valuesProperty">並び替え対象の配列</param>
        /// <param name="sourceIndicesByDestinationIndex">各 destination index に対応する source index</param>
        private void ReorderProfileValuesArray(SerializedProperty valuesProperty, IReadOnlyList<int> sourceIndicesByDestinationIndex) {
            var originalSize = valuesProperty.arraySize;
            for (var i = 0; i < originalSize; i++) {
                valuesProperty.InsertArrayElementAtIndex(valuesProperty.arraySize);
            }

            var backupStartIndex = originalSize;
            for (var i = 0; i < originalSize; i++) {
                var sourceProperty = valuesProperty.GetArrayElementAtIndex(i);
                var backupProperty = valuesProperty.GetArrayElementAtIndex(backupStartIndex + i);
                CopySerializedPropertyValue(sourceProperty, backupProperty);
            }

            for (var i = 0; i < sourceIndicesByDestinationIndex.Count; i++) {
                var sourceProperty = valuesProperty.GetArrayElementAtIndex(backupStartIndex + sourceIndicesByDestinationIndex[i]);
                var destinationProperty = valuesProperty.GetArrayElementAtIndex(i);
                CopySerializedPropertyValue(sourceProperty, destinationProperty);
            }

            while (valuesProperty.arraySize > originalSize) {
                valuesProperty.DeleteArrayElementAtIndex(valuesProperty.arraySize - 1);
            }
        }

        /// <summary>
        /// Entry 一覧から EntryId の配列を取得する
        /// </summary>
        /// <param name="entries">対象 Entry 一覧</param>
        /// <returns>EntryId の一覧</returns>
        private List<string> GetPaletteEntryIds(IReadOnlyList<PaletteEntry> entries) {
            var entryIds = new List<string>(entries?.Count ?? 0);
            if (entries == null) {
                return entryIds;
            }

            for (var i = 0; i < entries.Count; i++) {
                entryIds.Add(entries[i]?.EntryId ?? string.Empty);
            }

            return entryIds;
        }

        /// <summary>
        /// SerializedProperty の Entry 配列から EntryId の配列を取得する
        /// </summary>
        /// <param name="entriesProperty">対象 Entry 配列</param>
        /// <returns>EntryId の一覧</returns>
        private List<string> GetPaletteEntryIds(SerializedProperty entriesProperty) {
            var entryIds = new List<string>(entriesProperty?.arraySize ?? 0);
            if (entriesProperty == null) {
                return entryIds;
            }

            for (var i = 0; i < entriesProperty.arraySize; i++) {
                var entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                var entryIdProperty = entryProperty.FindPropertyRelative("_entryId");
                entryIds.Add(entryIdProperty != null ? entryIdProperty.stringValue : string.Empty);
            }

            return entryIds;
        }

        /// <summary>
        /// ProfileValue 要素を既定値へ初期化する
        /// </summary>
        /// <param name="valueProperty">初期化対象の要素</param>
        private void ResetProfileValue(SerializedProperty valueProperty) {
            ResetSerializedPropertyValue(valueProperty);
        }

        /// <summary>
        /// SerializedProperty を既定値へ初期化する
        /// </summary>
        /// <param name="property">初期化対象</param>
        private void ResetSerializedPropertyValue(SerializedProperty property) {
            if (property == null) {
                return;
            }

            if (property.isArray && property.propertyType != SerializedPropertyType.String) {
                property.arraySize = 0;
                return;
            }

            switch (property.propertyType) {
                case SerializedPropertyType.Integer:
                    property.intValue = 0;
                    return;
                case SerializedPropertyType.Boolean:
                    property.boolValue = false;
                    return;
                case SerializedPropertyType.Float:
                    property.floatValue = 0f;
                    return;
                case SerializedPropertyType.String:
                    property.stringValue = string.Empty;
                    return;
                case SerializedPropertyType.Color:
                    property.colorValue = default;
                    return;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    return;
                case SerializedPropertyType.LayerMask:
                    property.intValue = 0;
                    return;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = 0;
                    return;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = default;
                    return;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = default;
                    return;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = default;
                    return;
                case SerializedPropertyType.Rect:
                    property.rectValue = default;
                    return;
                case SerializedPropertyType.ArraySize:
                    property.intValue = 0;
                    return;
                case SerializedPropertyType.Character:
                    property.intValue = 0;
                    return;
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = new AnimationCurve();
                    return;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = default;
                    return;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = default;
                    return;
                case SerializedPropertyType.ExposedReference:
                    property.exposedReferenceValue = null;
                    return;
                case SerializedPropertyType.FixedBufferSize:
                    property.intValue = 0;
                    return;
                case SerializedPropertyType.Vector2Int:
                    property.vector2IntValue = default;
                    return;
                case SerializedPropertyType.Vector3Int:
                    property.vector3IntValue = default;
                    return;
                case SerializedPropertyType.RectInt:
                    property.rectIntValue = default;
                    return;
                case SerializedPropertyType.BoundsInt:
                    property.boundsIntValue = default;
                    return;
                case SerializedPropertyType.ManagedReference:
                    property.managedReferenceValue = null;
                    return;
                case SerializedPropertyType.Hash128:
                    property.hash128Value = default;
                    return;
                case SerializedPropertyType.Generic:
                    ResetGenericPropertyValue(property);
                    return;
                default:
                    return;
            }
        }

        /// <summary>
        /// Generic な SerializedProperty を再帰的に既定値へ初期化する
        /// </summary>
        /// <param name="property">初期化対象</param>
        private void ResetGenericPropertyValue(SerializedProperty property) {
            var iterator = property.Copy();
            var endProperty = iterator.GetEndProperty();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty)) {
                enterChildren = false;

                var relativePropertyPath = iterator.propertyPath.Substring(property.propertyPath.Length + 1);
                var childProperty = property.FindPropertyRelative(relativePropertyPath);
                if (childProperty == null) {
                    continue;
                }

                ResetSerializedPropertyValue(childProperty);
            }
        }

        /// <summary>
        /// Palette 用 ReorderableList を初期化する
        /// </summary>
        private void EnsurePaletteEntryList() {
            if (_selectedPaletteAsset == null) {
                _paletteEntryList = null;
                _paletteEntryListPaletteAsset = null;
                return;
            }

            if (_paletteEntryList != null && _paletteEntryListPaletteAsset == _selectedPaletteAsset) {
                return;
            }

            var serializedObject = new SerializedObject(_selectedPaletteAsset);
            var entriesProperty = serializedObject.FindProperty("_entries");
            _paletteEntryList = new ReorderableList(serializedObject, entriesProperty, true, true, true, true);
            _paletteEntryListPaletteAsset = _selectedPaletteAsset;
            _paletteEntryList.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            _paletteEntryList.drawHeaderCallback = DrawPaletteEntryListHeader;
            _paletteEntryList.drawElementCallback = DrawPaletteEntryListElement;
            _paletteEntryList.onSelectCallback = OnSelectPaletteEntry;
            _paletteEntryList.onAddDropdownCallback = OnAddPaletteEntryDropdown;
            _paletteEntryList.onRemoveCallback = OnRemovePaletteEntry;
            _paletteEntryList.onReorderCallback = OnReorderPaletteEntry;
            _paletteEntryList.index = Mathf.Clamp(_selectedEntryIndex, 0, Mathf.Max(0, _selectedPaletteAsset.Entries.Count - 1));
        }

        /// <summary>
        /// Palette 用 ReorderableList を無効化する
        /// </summary>
        private void InvalidatePaletteEntryList() {
            _paletteEntryList = null;
            _paletteEntryListPaletteAsset = null;
        }

        /// <summary>
        /// Palette 用 ReorderableList の Header を描画する
        /// </summary>
        /// <param name="rect">描画領域</param>
        private void DrawPaletteEntryListHeader(Rect rect) {
            EditorGUI.LabelField(rect, "Entry");
            var applyLabelRect = new Rect(rect.xMax - 56f, rect.y, 56f, rect.height);
            EditorGUI.LabelField(applyLabelRect, "Apply");
        }

        /// <summary>
        /// Palette 用 ReorderableList の要素を描画する
        /// </summary>
        /// <param name="rect">描画領域</param>
        /// <param name="index">要素 index</param>
        /// <param name="isActive">アクティブ状態</param>
        /// <param name="isFocused">フォーカス状態</param>
        private void DrawPaletteEntryListElement(Rect rect, int index, bool isActive, bool isFocused) {
            if (_selectedPaletteAsset == null || index < 0 || index >= _selectedPaletteAsset.Entries.Count) {
                return;
            }

            var rowRect = new Rect(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight);
            var labelRect = new Rect(rowRect.x, rowRect.y, rowRect.width - 64f, rowRect.height);
            var applyButtonRect = new Rect(rowRect.xMax - 56f, rowRect.y, 56f, rowRect.height);
            var paletteEntry = _selectedPaletteAsset.Entries[index];
            EditorGUI.LabelField(labelRect, GetEntryLabel(paletteEntry));

            using (new EditorGUI.DisabledScope(!CanApplyPaletteEntry())) {
                if (GUI.Button(applyButtonRect, "Apply", EditorStyles.miniButton)) {
                    ApplyPaletteEntryToSelection(paletteEntry, applyButtonRect);
                }
            }

            HandlePaletteEntryContextMenu(rowRect, index, paletteEntry);
        }

        /// <summary>
        /// Palette 用 ReorderableList の選択変更を処理する
        /// </summary>
        /// <param name="list">対象の ReorderableList</param>
        private void OnSelectPaletteEntry(ReorderableList list) {
            _selectedEntryIndex = list.index;
            Repaint();
        }

        /// <summary>
        /// Palette Entry 行の右クリックメニューを処理する
        /// </summary>
        /// <param name="rowRect">対象行の描画領域</param>
        /// <param name="index">対象 Entry index</param>
        /// <param name="paletteEntry">対象 Entry</param>
        private void HandlePaletteEntryContextMenu(Rect rowRect, int index, PaletteEntry paletteEntry) {
            var currentEvent = Event.current;
            if (currentEvent == null ||
                currentEvent.type != EventType.ContextClick ||
                !rowRect.Contains(currentEvent.mousePosition)) {
                return;
            }

            _selectedEntryIndex = index;
            if (_paletteEntryList != null) {
                _paletteEntryList.index = index;
            }

            ShowPaletteEntryContextMenu(paletteEntry);
            currentEvent.Use();
            Repaint();
        }

        /// <summary>
        /// Palette Entry 用の右クリックメニューを表示する
        /// </summary>
        /// <param name="paletteEntry">対象 Entry</param>
        private void ShowPaletteEntryContextMenu(PaletteEntry paletteEntry) {
            var menu = new GenericMenu();
            if (_selectedPaletteAsset != null && paletteEntry != null && !string.IsNullOrEmpty(paletteEntry.EntryId)) {
                menu.AddItem(new GUIContent("Select target components"), false, () => SelectTargetComponentsForEntry(paletteEntry));
            }
            else {
                menu.AddDisabledItem(new GUIContent("Select target components"));
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// Palette 用 ReorderableList の追加メニューを表示する
        /// </summary>
        /// <param name="buttonRect">ボタン位置</param>
        /// <param name="list">対象の ReorderableList</param>
        private void OnAddPaletteEntryDropdown(Rect buttonRect, ReorderableList list) {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Entry"), false, AddEntryToSelectedPalette);

            if (_selectedEntryIndex >= 0) {
                menu.AddItem(new GUIContent("Duplicate Selected Entry"), false, DuplicateSelectedEntry);
            }
            else {
                menu.AddDisabledItem(new GUIContent("Duplicate Selected Entry"));
            }

            menu.DropDown(buttonRect);
        }

        /// <summary>
        /// Palette 用 ReorderableList から Entry 削除を処理する
        /// </summary>
        /// <param name="list">対象の ReorderableList</param>
        private void OnRemovePaletteEntry(ReorderableList list) {
            _selectedEntryIndex = list.index;
            RemoveSelectedEntry();
        }

        /// <summary>
        /// Palette 用 ReorderableList の並び替え後処理
        /// </summary>
        /// <param name="list">対象の ReorderableList</param>
        private void OnReorderPaletteEntry(ReorderableList list) {
            if (_selectedPaletteAsset == null) {
                return;
            }

            var previousEntryIds = GetPaletteEntryIds(_selectedPaletteAsset.Entries);
            var nextEntryIds = GetPaletteEntryIds(list.serializedProperty);
            list.serializedProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            _selectedPaletteAsset.InvalidateEntryIndexCache();
            EditorUtility.SetDirty(_selectedPaletteAsset);
            ReorderProfileValues(_selectedPaletteAsset, previousEntryIds, nextEntryIds);
            _selectedEntryIndex = list.index;
            Repaint();
        }

        /// <summary>
        /// 選択中 Entry を複製する
        /// </summary>
        private void DuplicateSelectedEntry() {
            if (_selectedPaletteAsset == null || _selectedEntryIndex < 0 || _selectedEntryIndex >= _selectedPaletteAsset.Entries.Count) {
                return;
            }

            var serializedObject = new SerializedObject(_selectedPaletteAsset);
            var entriesProperty = serializedObject.FindProperty("_entries");
            var sourceIndex = _selectedEntryIndex;
            var insertIndex = sourceIndex + 1;
            var sourceEntryProperty = entriesProperty.GetArrayElementAtIndex(sourceIndex);
            var sourceDisplayName = sourceEntryProperty.FindPropertyRelative("_displayName").stringValue;
            var sourceDescription = sourceEntryProperty.FindPropertyRelative("_description").stringValue;
            var duplicatedEntryId = GenerateUniqueEntryId(_selectedPaletteAsset);

            entriesProperty.InsertArrayElementAtIndex(insertIndex);
            var duplicatedEntryProperty = entriesProperty.GetArrayElementAtIndex(insertIndex);
            duplicatedEntryProperty.FindPropertyRelative("_entryId").stringValue = duplicatedEntryId;
            duplicatedEntryProperty.FindPropertyRelative("_displayName").stringValue = sourceDisplayName;
            duplicatedEntryProperty.FindPropertyRelative("_description").stringValue = sourceDescription;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            _selectedPaletteAsset.InvalidateEntryIndexCache();
            EditorUtility.SetDirty(_selectedPaletteAsset);

            var profileAssets = GetProfileAssets(_selectedPaletteAsset);
            for (var i = 0; i < profileAssets.Count; i++) {
                var profileSerializedObject = new SerializedObject(profileAssets[i]);
                var valuesProperty = profileSerializedObject.FindProperty("_values");
                valuesProperty.InsertArrayElementAtIndex(insertIndex);
                profileSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profileAssets[i]);
            }

            _selectedEntryIndex = insertIndex;
            InvalidatePaletteEntryList();
            RebuildWindow();
        }

        /// <summary>
        /// 現在の選択へ Entry を適用できるか判定する
        /// </summary>
        /// <returns>適用可能な場合は true</returns>
        private bool CanApplyPaletteEntry() {
            return _selectedPaletteAsset != null && Selection.gameObjects.Length > 0;
        }

        /// <summary>
        /// 現在のステージで指定 Entry を参照している GameObject を一括選択する
        /// </summary>
        /// <param name="paletteEntry">対象 Entry</param>
        private void SelectTargetComponentsForEntry(PaletteEntry paletteEntry) {
            if (_selectedPaletteAsset == null || paletteEntry == null || string.IsNullOrEmpty(paletteEntry.EntryId)) {
                return;
            }

            var applierTypes = GetMatchingApplierTypes(_selectedPaletteAsset);
            if (applierTypes.Count == 0) {
                return;
            }

            var currentStage = StageUtility.GetCurrentStageHandle();
            var matchedGameObjects = new List<GameObject>();
            var matchedInstanceIds = new HashSet<int>();
            for (var i = 0; i < applierTypes.Count; i++) {
                CollectGameObjectsUsingEntry(applierTypes[i], paletteEntry.EntryId, currentStage, matchedGameObjects, matchedInstanceIds);
            }

            if (matchedGameObjects.Count == 0) {
                return;
            }

            matchedGameObjects.Sort(CompareHierarchyPath);
            Selection.objects = matchedGameObjects.ToArray();
            EditorGUIUtility.PingObject(matchedGameObjects[0]);
        }

        /// <summary>
        /// 選択中 GameObject へ Entry を適用する
        /// </summary>
        /// <param name="paletteEntry">適用する Entry</param>
        /// <param name="buttonRect">ボタン位置</param>
        private void ApplyPaletteEntryToSelection(PaletteEntry paletteEntry, Rect buttonRect) {
            if (_selectedPaletteAsset == null || paletteEntry == null || string.IsNullOrEmpty(paletteEntry.EntryId)) {
                return;
            }

            var applierTypes = GetMatchingApplierTypes(_selectedPaletteAsset);
            if (applierTypes.Count == 0) {
                EditorUtility.DisplayDialog(
                    "Palette Editor",
                    $"No applier type was found for {_selectedPaletteAsset.GetType().Name}.",
                    "OK");
                return;
            }

            if (applierTypes.Count == 1) {
                ApplyPaletteEntryToSelection(applierTypes[0], paletteEntry.EntryId);
                return;
            }

            var menu = new GenericMenu();
            for (var i = 0; i < applierTypes.Count; i++) {
                var applierType = applierTypes[i];
                menu.AddItem(
                    new GUIContent(applierType.Name),
                    false,
                    () => ApplyPaletteEntryToSelection(applierType, paletteEntry.EntryId));
            }

            menu.DropDown(buttonRect);
        }

        /// <summary>
        /// 指定した Applier 型で選択中 GameObject へ EntryId を反映する
        /// </summary>
        /// <param name="applierType">適用する Applier 型</param>
        /// <param name="entryId">設定する EntryId</param>
        private void ApplyPaletteEntryToSelection(Type applierType, string entryId) {
            if (applierType == null || string.IsNullOrEmpty(entryId)) {
                return;
            }

            var selectedGameObjects = Selection.gameObjects;
            if (selectedGameObjects == null || selectedGameObjects.Length == 0) {
                return;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Apply {applierType.Name}");

            for (var i = 0; i < selectedGameObjects.Length; i++) {
                var gameObject = selectedGameObjects[i];
                if (gameObject == null) {
                    continue;
                }

                ApplyPaletteEntryToGameObject(gameObject, applierType, entryId);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// 指定した GameObject へ EntryId を反映する
        /// </summary>
        /// <param name="gameObject">適用先</param>
        /// <param name="applierType">適用する Applier 型</param>
        /// <param name="entryId">設定する EntryId</param>
        private void ApplyPaletteEntryToGameObject(GameObject gameObject, Type applierType, string entryId) {
            if (gameObject == null || applierType == null || string.IsNullOrEmpty(entryId)) {
                return;
            }

            var applierComponent = gameObject.GetComponent(applierType);
            if (applierComponent == null) {
                applierComponent = Undo.AddComponent(gameObject, applierType);
            }

            if (applierComponent == null) {
                return;
            }

            Undo.RecordObject(applierComponent, $"Apply {applierType.Name}");
            var serializedObject = new SerializedObject(applierComponent);
            var entryIdProperty = serializedObject.FindProperty("_entryId");
            if (entryIdProperty == null) {
                return;
            }

            serializedObject.Update();
            entryIdProperty.stringValue = entryId;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(applierComponent);
        }

        /// <summary>
        /// 指定した Applier 型から対象 Entry を参照している GameObject を収集する
        /// </summary>
        /// <param name="applierType">対象の Applier 型</param>
        /// <param name="entryId">対象 EntryId</param>
        /// <param name="currentStage">現在のステージ</param>
        /// <param name="matchedGameObjects">収集先リスト</param>
        /// <param name="matchedInstanceIds">重複除外用の instanceId 集合</param>
        private void CollectGameObjectsUsingEntry(
            Type applierType,
            string entryId,
            StageHandle currentStage,
            List<GameObject> matchedGameObjects,
            HashSet<int> matchedInstanceIds) {
            var applierObjects = Resources.FindObjectsOfTypeAll(applierType);
            for (var i = 0; i < applierObjects.Length; i++) {
                if (applierObjects[i] is not Component applierComponent) {
                    continue;
                }

                var gameObject = applierComponent.gameObject;
                if (gameObject == null ||
                    EditorUtility.IsPersistent(applierComponent) ||
                    EditorUtility.IsPersistent(gameObject) ||
                    (applierComponent.hideFlags & HideFlags.HideInHierarchy) != 0 ||
                    (gameObject.hideFlags & HideFlags.HideInHierarchy) != 0 ||
                    !StageUtility.GetStageHandle(gameObject).Equals(currentStage) ||
                    !HasPaletteEntryId(applierComponent, entryId)) {
                    continue;
                }

                if (!matchedInstanceIds.Add(gameObject.GetInstanceID())) {
                    continue;
                }

                matchedGameObjects.Add(gameObject);
            }
        }

        /// <summary>
        /// Applier コンポーネントが指定した EntryId を参照しているか判定する
        /// </summary>
        /// <param name="applierComponent">対象コンポーネント</param>
        /// <param name="entryId">確認する EntryId</param>
        /// <returns>一致する場合は true</returns>
        private bool HasPaletteEntryId(Component applierComponent, string entryId) {
            if (applierComponent == null || string.IsNullOrEmpty(entryId)) {
                return false;
            }

            var serializedObject = new SerializedObject(applierComponent);
            serializedObject.Update();
            var entryIdProperty = serializedObject.FindProperty("_entryId");
            return entryIdProperty != null && entryIdProperty.stringValue == entryId;
        }

        /// <summary>
        /// Hierarchy 上のパス文字列で GameObject を比較する
        /// </summary>
        /// <param name="left">左側の GameObject</param>
        /// <param name="right">右側の GameObject</param>
        /// <returns>比較結果</returns>
        private int CompareHierarchyPath(GameObject left, GameObject right) {
            return string.Compare(GetHierarchyPath(left), GetHierarchyPath(right), StringComparison.Ordinal);
        }

        /// <summary>
        /// GameObject の Hierarchy パス文字列を取得する
        /// </summary>
        /// <param name="gameObject">対象 GameObject</param>
        /// <returns>Hierarchy パス</returns>
        private string GetHierarchyPath(GameObject gameObject) {
            if (gameObject == null) {
                return string.Empty;
            }

            var path = gameObject.name;
            var current = gameObject.transform.parent;
            while (current != null) {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        /// <summary>
        /// Palette に対応する Applier 型一覧を取得する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>一致した Applier 型一覧</returns>
        private List<Type> GetMatchingApplierTypes(PaletteAssetBase paletteAsset) {
            var applierTypes = new List<Type>();
            if (paletteAsset == null) {
                return applierTypes;
            }

            var derivedTypes = TypeCache.GetTypesDerivedFrom<MonoBehaviour>();
            for (var i = 0; i < derivedTypes.Count; i++) {
                var applierType = derivedTypes[i];
                if (applierType.IsAbstract) {
                    continue;
                }

                if (!TryGetPaletteApplierTypeArguments(applierType, out var targetPaletteAssetType)) {
                    continue;
                }

                if (targetPaletteAssetType != paletteAsset.GetType()) {
                    continue;
                }

                applierTypes.Add(applierType);
            }

            applierTypes.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return applierTypes;
        }

        /// <summary>
        /// Applier 型から対応する PaletteAsset 型を取得する
        /// </summary>
        /// <param name="applierType">対象の Applier 型</param>
        /// <param name="paletteAssetType">取得できた PaletteAsset 型</param>
        /// <returns>取得できた場合は true</returns>
        private bool TryGetPaletteApplierTypeArguments(Type applierType, out Type paletteAssetType) {
            paletteAssetType = null;
            if (applierType == null) {
                return false;
            }

            var currentType = applierType;
            while (currentType != null && currentType != typeof(MonoBehaviour)) {
                if (currentType.IsGenericType &&
                    currentType.GetGenericTypeDefinition() == typeof(PaletteApplierBase<,,>)) {
                    var genericArguments = currentType.GetGenericArguments();
                    if (genericArguments.Length > 0) {
                        paletteAssetType = genericArguments[0];
                        return paletteAssetType != null;
                    }

                    return false;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }
    }
}
