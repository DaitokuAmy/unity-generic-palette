using UnityEditor;
using UnityEngine;

namespace UnityGenericPalette.Editor {
    /// <summary>
    /// LegacyTextStylePaletteValue の描画を提供する PropertyDrawer
    /// </summary>
    [CustomPropertyDrawer(typeof(LegacyTextStylePaletteValue))]
    public sealed class LegacyTextStylePaletteValueDrawer : PropertyDrawer {
        private const float BoxPadding = 6f;
        private const float RowSpacing = 2f;

        private static readonly GUIContent MainSettingsLabel = new("Main Settings");
        private static readonly GUIContent FontLabel = new("Font");
        private static readonly GUIContent FontStyleLabel = new("Font Style");
        private static readonly GUIContent FontSizeLabel = new("Font Size");
        private static readonly GUIContent LineSpacingLabel = new("Line Spacing");

        /// <summary>
        /// Property GUI を描画する
        /// </summary>
        /// <param name="position">描画領域</param>
        /// <param name="property">描画対象の property</param>
        /// <param name="label">表示ラベル</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            var fontProperty = property.FindPropertyRelative("_font");
            var fontStyleProperty = property.FindPropertyRelative("_fontStyle");
            var fontSizeProperty = property.FindPropertyRelative("_fontSize");
            var lineSpacingProperty = property.FindPropertyRelative("_lineSpacing");

            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            var contentRect = new Rect(
                position.x + BoxPadding,
                position.y + BoxPadding,
                position.width - (BoxPadding * 2f),
                position.height - (BoxPadding * 2f));

            var rowRect = new Rect(contentRect.x, contentRect.y, contentRect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rowRect, MainSettingsLabel, EditorStyles.boldLabel);

            rowRect = GetNextRowRect(rowRect);
            EditorGUI.PropertyField(rowRect, fontProperty, FontLabel);

            rowRect = GetNextRowRect(rowRect);
            EditorGUI.PropertyField(rowRect, fontStyleProperty, FontStyleLabel);

            rowRect = GetNextRowRect(rowRect);
            EditorGUI.PropertyField(rowRect, fontSizeProperty, FontSizeLabel);

            rowRect = GetNextRowRect(rowRect);
            EditorGUI.PropertyField(rowRect, lineSpacingProperty, LineSpacingLabel);

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Property の高さを取得する
        /// </summary>
        /// <param name="property">対象 property</param>
        /// <param name="label">表示ラベル</param>
        /// <returns>必要な高さ</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            var rowCount = 5;
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
    }
}
