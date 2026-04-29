using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace UnityGenericPalette.Editor {
    /// <summary>
    /// TextStylePaletteValue の描画を提供する PropertyDrawer
    /// </summary>
    [CustomPropertyDrawer(typeof(TextStylePaletteValue))]
    public sealed class TextStylePaletteValueDrawer : PropertyDrawer {
        private const float BoxPadding = 6f;
        private const float RowSpacing = 2f;
        private const float ColumnSpacing = 6f;
        private const float FontStyleButtonSpacing = 2f;
        private const float ChildSectionIndent = 14f;
        private static readonly int MainTexShaderPropertyId = Shader.PropertyToID("_MainTex");

        private static readonly GUIContent MainSettingsLabel = new GUIContent("Main Settings");
        private static readonly GUIContent FontAssetLabel = new GUIContent("Font Asset", "The Font Asset containing the glyphs that can be rendered for this text.");
        private static readonly GUIContent MaterialPresetLabel = new GUIContent("Material Preset", "The material used for rendering. Only materials created from the Font Asset can be used.");
        private static readonly GUIContent FontStyleLabel = new GUIContent("Font Style", "Styles to apply to the text such as Bold or Italic.");
        private static readonly GUIContent FontSizeLabel = new GUIContent("Font Size", "The size the text will be rendered at in points.");
        private static readonly GUIContent AutoSizeLabel = new GUIContent("Auto Size", "Auto sizes the text to fit the available space.");
        private static readonly GUIContent AutoSizeOptionsLabel = new GUIContent("Auto Size Options", "Define the basic rules for auto-sizing text.");
        private static readonly GUIContent AutoSizeMinLabel = new GUIContent("Min", "The minimum font size.");
        private static readonly GUIContent AutoSizeMaxLabel = new GUIContent("Max", "The maximum font size.");
        private static readonly GUIContent AutoSizeWdLabel = new GUIContent("WD%", "Compresses character width up to this value before reducing font size.");
        private static readonly GUIContent AutoSizeLineLabel = new GUIContent("Line", "Negative value only. Compresses line height down to this value before reducing font size.");
        private static readonly GUIContent SpacingOptionsLabel = new GUIContent("Spacing Options (em)", "Spacing adjustments between different elements of the text.");
        private static readonly GUIContent CharacterSpacingLabel = new GUIContent("Character");
        private static readonly GUIContent WordSpacingLabel = new GUIContent("Word");
        private static readonly GUIContent LineSpacingLabel = new GUIContent("Line");
        private static readonly GUIContent ParagraphSpacingLabel = new GUIContent("Paragraph");

        private static readonly GUIContent[] FontStyleButtonLabels = {
            new("B", "Bold"),
            new("I", "Italic"),
            new("U", "Underline"),
            new("S", "Strikethrough"),
            new("ab", "Lowercase"),
            new("AB", "Uppercase"),
            new("SC", "Smallcaps"),
        };

        private static readonly FontStyles[] FontStyleFlags = {
            FontStyles.Bold,
            FontStyles.Italic,
            FontStyles.Underline,
            FontStyles.Strikethrough,
            FontStyles.LowerCase,
            FontStyles.UpperCase,
            FontStyles.SmallCaps,
        };

        /// <summary>
        /// Property GUI を描画する
        /// </summary>
        /// <param name="position">描画領域</param>
        /// <param name="property">描画対象の property</param>
        /// <param name="label">表示ラベル</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            var fontAssetProperty = property.FindPropertyRelative("_fontAsset");
            var materialPresetProperty = property.FindPropertyRelative("_materialPreset");
            var fontStyleProperty = property.FindPropertyRelative("_fontStyle");
            var enableAutoSizingProperty = property.FindPropertyRelative("_enableAutoSizing");
            var fontSizeProperty = property.FindPropertyRelative("_fontSize");
            var fontSizeMinProperty = property.FindPropertyRelative("_fontSizeMin");
            var fontSizeMaxProperty = property.FindPropertyRelative("_fontSizeMax");
            var characterWidthAdjustmentProperty = property.FindPropertyRelative("_characterWidthAdjustment");
            var lineSpacingAdjustmentProperty = property.FindPropertyRelative("_lineSpacingAdjustment");
            var characterSpacingProperty = property.FindPropertyRelative("_characterSpacing");
            var wordSpacingProperty = property.FindPropertyRelative("_wordSpacing");
            var lineSpacingProperty = property.FindPropertyRelative("_lineSpacing");
            var paragraphSpacingProperty = property.FindPropertyRelative("_paragraphSpacing");

            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            var contentRect = new Rect(
                position.x + BoxPadding,
                position.y + BoxPadding,
                position.width - (BoxPadding * 2f),
                position.height - (BoxPadding * 2f));

            var rowRect = new Rect(contentRect.x, contentRect.y, contentRect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rowRect, MainSettingsLabel, EditorStyles.boldLabel);

            rowRect = GetNextRowRect(rowRect);
            EditorGUI.PropertyField(rowRect, fontAssetProperty, FontAssetLabel);

            rowRect = GetNextRowRect(rowRect);
            DrawMaterialPresetRow(rowRect, fontAssetProperty, materialPresetProperty);

            rowRect = GetNextRowRect(rowRect);
            DrawFontStyleRow(rowRect, fontStyleProperty);

            rowRect = GetNextRowRect(rowRect);
            EditorGUI.PropertyField(rowRect, fontSizeProperty, FontSizeLabel);

            rowRect = GetNextRowRect(rowRect);
            EditorGUI.PropertyField(rowRect, enableAutoSizingProperty, AutoSizeLabel);

            if (enableAutoSizingProperty.boolValue) {
                rowRect = GetNextRowRect(rowRect);
                EditorGUI.LabelField(rowRect, AutoSizeOptionsLabel, EditorStyles.boldLabel);

                rowRect = GetNextRowRect(rowRect);
                DrawSpacingLabelRow(GetIndentedRowRect(rowRect), AutoSizeMinLabel, AutoSizeMaxLabel);

                rowRect = GetNextRowRect(rowRect);
                DrawSpacingValueRow(GetIndentedRowRect(rowRect), fontSizeMinProperty, fontSizeMaxProperty);

                rowRect = GetNextRowRect(rowRect);
                DrawSpacingLabelRow(GetIndentedRowRect(rowRect), AutoSizeWdLabel, AutoSizeLineLabel);

                rowRect = GetNextRowRect(rowRect);
                DrawSpacingValueRow(GetIndentedRowRect(rowRect), characterWidthAdjustmentProperty, lineSpacingAdjustmentProperty);
                ClampAutoSizeOptionValues(
                    fontSizeMinProperty,
                    fontSizeMaxProperty,
                    characterWidthAdjustmentProperty,
                    lineSpacingAdjustmentProperty);
            }

            rowRect = GetNextRowRect(rowRect);
            EditorGUI.LabelField(rowRect, SpacingOptionsLabel, EditorStyles.boldLabel);

            rowRect = GetNextRowRect(rowRect);
            DrawSpacingLabelRow(GetIndentedRowRect(rowRect), CharacterSpacingLabel, WordSpacingLabel);

            rowRect = GetNextRowRect(rowRect);
            DrawSpacingValueRow(GetIndentedRowRect(rowRect), characterSpacingProperty, wordSpacingProperty);

            rowRect = GetNextRowRect(rowRect);
            DrawSpacingLabelRow(GetIndentedRowRect(rowRect), LineSpacingLabel, ParagraphSpacingLabel);

            rowRect = GetNextRowRect(rowRect);
            DrawSpacingValueRow(GetIndentedRowRect(rowRect), lineSpacingProperty, paragraphSpacingProperty);

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Property の高さを取得する
        /// </summary>
        /// <param name="property">対象 property</param>
        /// <param name="label">表示ラベル</param>
        /// <returns>必要な高さ</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            var rowCount = 11;
            var enableAutoSizingProperty = property.FindPropertyRelative("_enableAutoSizing");
            if (enableAutoSizingProperty != null && enableAutoSizingProperty.boolValue) {
                rowCount += 5;
            }

            var lineHeight = EditorGUIUtility.singleLineHeight;
            return (BoxPadding * 2f) + (lineHeight * rowCount) + (RowSpacing * (rowCount - 1)) + 2f;
        }

        /// <summary>
        /// 次の行の描画領域を取得する
        /// </summary>
        /// <param name="currentRowRect">現在の行</param>
        /// <returns>次の行</returns>
        private static Rect GetNextRowRect(Rect currentRowRect) {
            return new Rect(
                currentRowRect.x,
                currentRowRect.yMax + RowSpacing,
                currentRowRect.width,
                EditorGUIUtility.singleLineHeight);
        }

        /// <summary>
        /// 1 段インデントした行の描画領域を取得する
        /// </summary>
        /// <param name="rowRect">元の行</param>
        /// <returns>インデント済みの行</returns>
        private static Rect GetIndentedRowRect(Rect rowRect) {
            return new Rect(
                rowRect.x + ChildSectionIndent,
                rowRect.y,
                Mathf.Max(0f, rowRect.width - ChildSectionIndent),
                rowRect.height);
        }

        /// <summary>
        /// FontAsset に対応する Material Preset 行を描画する
        /// </summary>
        /// <param name="position">描画領域</param>
        /// <param name="fontAssetProperty">FontAsset の property</param>
        /// <param name="materialPresetProperty">Material Preset の property</param>
        private static void DrawMaterialPresetRow(Rect position, SerializedProperty fontAssetProperty, SerializedProperty materialPresetProperty) {
            if (fontAssetProperty.objectReferenceValue is not TMP_FontAsset fontAsset) {
                EditorGUI.PropertyField(position, materialPresetProperty, MaterialPresetLabel);
                return;
            }

            var materialPresets = FindMaterialPresets(fontAsset);
            if (materialPresets.Count == 0) {
                EditorGUI.PropertyField(position, materialPresetProperty, MaterialPresetLabel);
                return;
            }

            var currentMaterial = materialPresetProperty.objectReferenceValue as Material;
            var selectedIndex = FindMaterialPresetIndex(materialPresets, currentMaterial);
            var optionLabels = BuildMaterialPresetLabels(materialPresets, currentMaterial, selectedIndex);
            if (selectedIndex < 0) {
                selectedIndex = optionLabels.Length - 1;
            }

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUI.Popup(position, MaterialPresetLabel, selectedIndex, optionLabels);
            if (EditorGUI.EndChangeCheck()) {
                materialPresetProperty.objectReferenceValue = selectedIndex >= 0 && selectedIndex < materialPresets.Count
                    ? materialPresets[selectedIndex]
                    : currentMaterial;
            }
        }

        /// <summary>
        /// FontStyle 行を描画する
        /// </summary>
        /// <param name="position">描画領域</param>
        /// <param name="fontStyleProperty">FontStyle の property</param>
        private static void DrawFontStyleRow(Rect position, SerializedProperty fontStyleProperty) {
            var prefixRect = EditorGUI.PrefixLabel(position, FontStyleLabel);
            var buttonWidth = (prefixRect.width - (FontStyleButtonSpacing * (FontStyleButtonLabels.Length - 1))) / FontStyleButtonLabels.Length;
            var buttonRect = new Rect(prefixRect.x, prefixRect.y, buttonWidth, prefixRect.height);
            var fontStyleValue = fontStyleProperty.intValue;

            for (var i = 0; i < FontStyleButtonLabels.Length; i++) {
                var style = GetButtonStyle(i, FontStyleButtonLabels.Length);
                var flagValue = (int)FontStyleFlags[i];
                var isEnabled = (fontStyleValue & flagValue) != 0;
                EditorGUI.BeginChangeCheck();
                var nextEnabled = GUI.Toggle(buttonRect, isEnabled, FontStyleButtonLabels[i], style);
                if (EditorGUI.EndChangeCheck()) {
                    fontStyleValue = nextEnabled
                        ? fontStyleValue | flagValue
                        : fontStyleValue & ~flagValue;
                }

                buttonRect.x += buttonWidth + FontStyleButtonSpacing;
            }

            fontStyleProperty.intValue = fontStyleValue;
        }

        /// <summary>
        /// Auto Size オプション値を妥当な範囲に補正する
        /// </summary>
        /// <param name="minProperty">最小値の property</param>
        /// <param name="maxProperty">最大値の property</param>
        /// <param name="widthAdjustmentProperty">WD% の property</param>
        /// <param name="lineAdjustmentProperty">Line の property</param>
        private static void ClampAutoSizeOptionValues(
            SerializedProperty minProperty,
            SerializedProperty maxProperty,
            SerializedProperty widthAdjustmentProperty,
            SerializedProperty lineAdjustmentProperty) {
            var minValue = Mathf.Max(0f, minProperty.floatValue);
            var maxValue = Mathf.Clamp(maxProperty.floatValue, 0f, 32767f);
            minProperty.floatValue = Mathf.Min(minValue, maxValue);
            maxProperty.floatValue = Mathf.Max(minProperty.floatValue, maxValue);
            widthAdjustmentProperty.floatValue = Mathf.Clamp(widthAdjustmentProperty.floatValue, 0f, 50f);
            lineAdjustmentProperty.floatValue = Mathf.Min(0f, lineAdjustmentProperty.floatValue);
        }

        /// <summary>
        /// Spacing のラベル行を描画する
        /// </summary>
        /// <param name="position">描画領域</param>
        /// <param name="leftLabel">左側ラベル</param>
        /// <param name="rightLabel">右側ラベル</param>
        private static void DrawSpacingLabelRow(
            Rect position,
            GUIContent leftLabel,
            GUIContent rightLabel) {
            var columnWidth = (position.width - ColumnSpacing) * 0.5f;
            var leftRect = new Rect(position.x, position.y, columnWidth, position.height);
            var rightRect = new Rect(position.x + columnWidth + ColumnSpacing, position.y, columnWidth, position.height);

            EditorGUI.LabelField(leftRect, leftLabel, EditorStyles.miniLabel);
            EditorGUI.LabelField(rightRect, rightLabel, EditorStyles.miniLabel);
        }

        /// <summary>
        /// Spacing の値行を描画する
        /// </summary>
        /// <param name="position">描画領域</param>
        /// <param name="leftProperty">左側 property</param>
        /// <param name="rightProperty">右側 property</param>
        private static void DrawSpacingValueRow(
            Rect position,
            SerializedProperty leftProperty,
            SerializedProperty rightProperty) {
            var columnWidth = (position.width - ColumnSpacing) * 0.5f;
            var leftRect = new Rect(position.x, position.y, columnWidth, position.height);
            var rightRect = new Rect(position.x + columnWidth + ColumnSpacing, position.y, columnWidth, position.height);

            EditorGUI.PropertyField(leftRect, leftProperty, GUIContent.none);
            EditorGUI.PropertyField(rightRect, rightProperty, GUIContent.none);
        }

        /// <summary>
        /// ラベル付きのコンパクトな float フィールドを描画する
        /// </summary>
        /// <param name="position">描画領域</param>
        /// <param name="property">対象 property</param>
        /// <param name="label">表示ラベル</param>
        private static void DrawCompactFloatField(Rect position, SerializedProperty property, GUIContent label) {
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 28f;
            EditorGUI.PropertyField(position, property, label);
            EditorGUIUtility.labelWidth = labelWidth;
        }

        /// <summary>
        /// 位置に応じたボタンスタイルを取得する
        /// </summary>
        /// <param name="index">ボタン index</param>
        /// <param name="count">ボタン数</param>
        /// <returns>使用する GUIStyle</returns>
        private static GUIStyle GetButtonStyle(int index, int count) {
            if (count == 1) {
                return EditorStyles.miniButton;
            }

            if (index == 0) {
                return EditorStyles.miniButtonLeft;
            }

            if (index == count - 1) {
                return EditorStyles.miniButtonRight;
            }

            return EditorStyles.miniButtonMid;
        }

        /// <summary>
        /// FontAsset に紐づく Material Preset 一覧を取得する
        /// </summary>
        /// <param name="fontAsset">対象 FontAsset</param>
        /// <returns>対応する Material 一覧</returns>
        private static List<Material> FindMaterialPresets(TMP_FontAsset fontAsset) {
            var materialPresets = new List<Material>();
            if (fontAsset == null || fontAsset.material == null || !fontAsset.material.HasProperty(MainTexShaderPropertyId)) {
                return materialPresets;
            }

            var baseTexture = fontAsset.material.GetTexture(MainTexShaderPropertyId);
            if (baseTexture == null) {
                return materialPresets;
            }

            AddMaterialIfMissing(materialPresets, fontAsset.material);

            var searchTokens = fontAsset.name.Split(' ');
            var searchPattern = searchTokens.Length > 0
                ? $"t:Material {searchTokens[0]}"
                : "t:Material";
            var materialAssetGuids = AssetDatabase.FindAssets(searchPattern);
            for (var i = 0; i < materialAssetGuids.Length; i++) {
                var materialPath = AssetDatabase.GUIDToAssetPath(materialAssetGuids[i]);
                var candidateMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (candidateMaterial == null || !candidateMaterial.HasProperty(MainTexShaderPropertyId)) {
                    continue;
                }

                var candidateTexture = candidateMaterial.GetTexture(MainTexShaderPropertyId);
                if (candidateTexture == null || candidateTexture.GetInstanceID() != baseTexture.GetInstanceID()) {
                    continue;
                }

                AddMaterialIfMissing(materialPresets, candidateMaterial);
            }

            return materialPresets;
        }

        /// <summary>
        /// Material 一覧に重複なく追加する
        /// </summary>
        /// <param name="materialPresets">追加先</param>
        /// <param name="material">追加対象</param>
        private static void AddMaterialIfMissing(List<Material> materialPresets, Material material) {
            if (material == null || materialPresets.Contains(material)) {
                return;
            }

            materialPresets.Add(material);
        }

        /// <summary>
        /// 現在選択中 Material の index を取得する
        /// </summary>
        /// <param name="materialPresets">候補一覧</param>
        /// <param name="currentMaterial">現在値</param>
        /// <returns>一致した index。見つからない場合は -1</returns>
        private static int FindMaterialPresetIndex(IReadOnlyList<Material> materialPresets, Material currentMaterial) {
            if (currentMaterial == null) {
                return 0;
            }

            for (var i = 0; i < materialPresets.Count; i++) {
                if (materialPresets[i] == currentMaterial) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Material Preset Popup の表示名一覧を構築する
        /// </summary>
        /// <param name="materialPresets">候補一覧</param>
        /// <param name="currentMaterial">現在値</param>
        /// <param name="selectedIndex">現在の選択 index</param>
        /// <returns>表示名一覧</returns>
        private static GUIContent[] BuildMaterialPresetLabels(IReadOnlyList<Material> materialPresets, Material currentMaterial, int selectedIndex) {
            var optionLabels = new List<GUIContent>(materialPresets.Count + 1);
            for (var i = 0; i < materialPresets.Count; i++) {
                optionLabels.Add(new GUIContent(materialPresets[i].name));
            }

            if (selectedIndex >= 0 || currentMaterial == null) {
                return optionLabels.ToArray();
            }

            optionLabels.Add(new GUIContent($"(Missing) {currentMaterial.name}", "現在の Material Preset は Font Asset に対応していない"));
            return optionLabels.ToArray();
        }
    }
}
