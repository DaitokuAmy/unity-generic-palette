using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
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
            EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("_entryId"), new GUIContent("Entry Id"));
            EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("_displayName"), new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("_description"), new GUIContent("Description"));
            if (!serializedObject.ApplyModifiedProperties()) {
                return;
            }

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

            var profileValueProperty = valuesProperty.GetArrayElementAtIndex(_selectedEntryIndex);
            var valueProperty = profileValueProperty.FindPropertyRelative("_value");

            serializedObject.Update();
            EditorGUILayout.PropertyField(valueProperty, true);
            if (serializedObject.ApplyModifiedProperties()) {
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

            var entryId = _selectedPaletteAsset.Entries[_selectedEntryIndex].EntryId;
            var sourceSerializedObject = new SerializedObject(sourceProfileAsset);
            var sourceValuesProperty = sourceSerializedObject.FindProperty("_values");
            var sourceValueIndex = FindProfileValueIndex(sourceValuesProperty, entryId, 0);
            if (!IsValidArrayIndex(sourceValuesProperty, sourceValueIndex)) {
                EditorUtility.DisplayDialog("Palette Editor", $"Profile '{GetProfileLabel(sourceProfileAsset)}' does not contain EntryId '{entryId}'.", "OK");
                return;
            }

            var targetSerializedObject = new SerializedObject(_selectedProfileAsset);
            var targetValuesProperty = targetSerializedObject.FindProperty("_values");
            var targetValueIndex = FindProfileValueIndex(targetValuesProperty, entryId, 0);
            if (!IsValidArrayIndex(targetValuesProperty, targetValueIndex)) {
                EditorUtility.DisplayDialog("Palette Editor", $"Profile '{GetProfileLabel(_selectedProfileAsset)}' does not contain EntryId '{entryId}'.", "OK");
                return;
            }

            sourceSerializedObject.Update();
            targetSerializedObject.Update();

            var sourceValueProperty = sourceValuesProperty.GetArrayElementAtIndex(sourceValueIndex).FindPropertyRelative("_value");
            var targetValueProperty = targetValuesProperty.GetArrayElementAtIndex(targetValueIndex).FindPropertyRelative("_value");
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

            _selectedPaletteAsset = paletteAsset;
            _selectedProfileAsset = null;
            _selectedEntryIndex = -1;
            InvalidatePaletteEntryList();
            InvalidateProfileAssetList();
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
                EditorUtility.SetDirty(profileAssets[i]);
            }
        }

        /// <summary>
        /// ProfileValue 配列を Entry 一覧へ同期する
        /// </summary>
        /// <param name="valuesProperty">同期対象の配列</param>
        /// <param name="entries">同期元 Entry 一覧</param>
        private void SynchronizeProfileValuesArray(SerializedProperty valuesProperty, IReadOnlyList<PaletteEntry> entries) {
            for (var i = 0; i < entries.Count; i++) {
                var entryId = entries[i].EntryId;
                var currentIndex = FindProfileValueIndex(valuesProperty, entryId, i);
                if (currentIndex < 0) {
                    valuesProperty.InsertArrayElementAtIndex(i);
                    ResetProfileValue(valuesProperty.GetArrayElementAtIndex(i));
                }
                else if (currentIndex != i) {
                    valuesProperty.MoveArrayElement(currentIndex, i);
                }

                valuesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_entryId").stringValue = entryId;
            }

            while (valuesProperty.arraySize > entries.Count) {
                valuesProperty.DeleteArrayElementAtIndex(valuesProperty.arraySize - 1);
            }
        }

        /// <summary>
        /// ProfileValue 要素を既定値へ初期化する
        /// </summary>
        /// <param name="profileValueProperty">初期化対象の要素</param>
        private void ResetProfileValue(SerializedProperty profileValueProperty) {
            profileValueProperty.FindPropertyRelative("_entryId").stringValue = string.Empty;

            var valueProperty = profileValueProperty.FindPropertyRelative("_value");
            switch (valueProperty.propertyType) {
                case SerializedPropertyType.Integer:
                    valueProperty.intValue = 0;
                    break;
                case SerializedPropertyType.Boolean:
                    valueProperty.boolValue = false;
                    break;
                case SerializedPropertyType.Float:
                    valueProperty.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    valueProperty.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    valueProperty.colorValue = default;
                    break;
                case SerializedPropertyType.ObjectReference:
                    valueProperty.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.Enum:
                    valueProperty.enumValueIndex = 0;
                    break;
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

            list.serializedProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_selectedPaletteAsset);
            SynchronizeProfileValues(_selectedPaletteAsset);
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
            EditorUtility.SetDirty(_selectedPaletteAsset);

            var profileAssets = GetProfileAssets(_selectedPaletteAsset);
            for (var i = 0; i < profileAssets.Count; i++) {
                var profileSerializedObject = new SerializedObject(profileAssets[i]);
                var valuesProperty = profileSerializedObject.FindProperty("_values");
                valuesProperty.InsertArrayElementAtIndex(insertIndex);
                valuesProperty.GetArrayElementAtIndex(insertIndex).FindPropertyRelative("_entryId").stringValue = duplicatedEntryId;
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
