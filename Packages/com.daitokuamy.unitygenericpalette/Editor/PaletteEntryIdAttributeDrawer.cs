using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// PaletteEntryIdAttribute の描画を提供する PropertyDrawer
    /// </summary>
    [CustomPropertyDrawer(typeof(PaletteEntryIdAttribute))]
    public sealed class PaletteEntryIdAttributeDrawer : PropertyDrawer {
        private static readonly GUIContent NoneOptionLabel = new GUIContent("(None)", "EntryId を未選択にする");

        /// <summary>
        /// Property GUI を描画する
        /// </summary>
        /// <param name="position">描画領域</param>
        /// <param name="property">描画対象の property</param>
        /// <param name="label">表示ラベル</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.String) {
                EditorGUI.LabelField(position, label, new GUIContent("Use with string."));
                EditorGUI.EndProperty();
                return;
            }

            if (!TryBuildOptions(property, out var optionLabels, out var optionValues, out var currentIndex)) {
                EditorGUI.PropertyField(position, property, label);
                EditorGUI.EndProperty();
                return;
            }

            var displayedOptions = optionLabels.ToArray();

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var selectedIndex = EditorGUI.Popup(position, label, currentIndex, displayedOptions);
            if (EditorGUI.EndChangeCheck() && selectedIndex >= 0 && selectedIndex < optionValues.Count) {
                property.stringValue = optionValues[selectedIndex];
            }
            EditorGUI.showMixedValue = false;

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Popup に表示する候補一覧を構築する
        /// </summary>
        /// <param name="property">描画対象の property</param>
        /// <param name="optionLabels">表示ラベル一覧</param>
        /// <param name="optionValues">対応する EntryId 一覧</param>
        /// <param name="currentIndex">現在値に対応する index</param>
        /// <returns>候補化できた場合は true</returns>
        private static bool TryBuildOptions(
            SerializedProperty property,
            out List<GUIContent> optionLabels,
            out List<string> optionValues,
            out int currentIndex) {
            optionLabels = new List<GUIContent> {
                NoneOptionLabel,
            };
            optionValues = new List<string> {
                string.Empty,
            };

            var paletteAsset = ResolvePaletteAsset(property);
            if (paletteAsset == null) {
                currentIndex = 0;
                return false;
            }

            for (var i = 0; i < paletteAsset.Entries.Count; i++) {
                var paletteEntry = paletteAsset.Entries[i];
                if (paletteEntry == null || string.IsNullOrEmpty(paletteEntry.EntryId)) {
                    continue;
                }

                optionLabels.Add(new GUIContent(GetEntryLabel(paletteEntry), paletteEntry.Description));
                optionValues.Add(paletteEntry.EntryId);
            }

            var currentValue = property.stringValue;
            currentIndex = optionValues.IndexOf(currentValue);
            if (currentIndex >= 0) {
                return true;
            }

            if (!string.IsNullOrEmpty(currentValue)) {
                optionLabels.Add(new GUIContent($"(Missing) {currentValue}", "現在の EntryId は PaletteAsset に存在しない"));
                optionValues.Add(currentValue);
                currentIndex = optionValues.Count - 1;
                return true;
            }

            currentIndex = 0;
            return true;
        }

        /// <summary>
        /// Property に対応する PaletteAsset を解決する
        /// </summary>
        /// <param name="property">対象 property</param>
        /// <returns>対応する PaletteAsset。解決できない場合は null</returns>
        private static PaletteAssetBase ResolvePaletteAsset(SerializedProperty property) {
            if (property?.serializedObject?.targetObject == null) {
                return null;
            }

            var targetType = property.serializedObject.targetObject.GetType();
            var paletteAssetType = ResolvePaletteAssetType(targetType);
            if (paletteAssetType == null) {
                return null;
            }

            var paletteAssetStorage = UnityGenericPaletteProjectSettings.instance.PaletteAssetStorage;
            if (paletteAssetStorage == null) {
                return null;
            }

            for (var i = 0; i < paletteAssetStorage.PaletteAssets.Count; i++) {
                var paletteAsset = paletteAssetStorage.PaletteAssets[i];
                if (paletteAsset != null && paletteAssetType.IsInstanceOfType(paletteAsset)) {
                    return paletteAsset;
                }
            }

            return null;
        }

        /// <summary>
        /// PaletteApplierBase から PaletteAsset 型を取得する
        /// </summary>
        /// <param name="targetType">探索対象の型</param>
        /// <returns>対応する PaletteAsset 型。見つからない場合は null</returns>
        private static Type ResolvePaletteAssetType(Type targetType) {
            while (targetType != null) {
                if (targetType.IsGenericType &&
                    targetType.GetGenericTypeDefinition() == typeof(PaletteApplierBase<,,>)) {
                    return targetType.GetGenericArguments()[0];
                }

                targetType = targetType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Entry の表示ラベルを取得する
        /// </summary>
        /// <param name="paletteEntry">対象 Entry</param>
        /// <returns>表示ラベル</returns>
        private static string GetEntryLabel(PaletteEntry paletteEntry) {
            if (paletteEntry == null) {
                return "(Null Entry)";
            }

            if (!string.IsNullOrEmpty(paletteEntry.DisplayName)) {
                return $"{paletteEntry.DisplayName} ({paletteEntry.EntryId})";
            }

            return paletteEntry.EntryId;
        }
    }
}
