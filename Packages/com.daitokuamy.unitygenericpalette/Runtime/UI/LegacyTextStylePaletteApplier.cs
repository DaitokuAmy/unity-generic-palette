using UnityEngine;
using UnityEngine.UI;

namespace UnityGenericPalette {
    /// <summary>
    /// LegacyTextStyle Palette の値を uGUI の Text へ反映する Applier
    /// </summary>
    [AddComponentMenu("Unity Generic Palette/Legacy Text Style Palette Applier")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class LegacyTextStylePaletteApplier : PaletteApplierBase<LegacyTextStylePaletteAsset, LegacyTextStylePaletteProfileAsset, LegacyTextStylePaletteValue> {
        /// <summary>
        /// Palette からの適用を無視する LegacyTextStyle 項目
        /// </summary>
        [System.Flags]
        private enum IgnoreStyleMask {
            None = 0,
            FontStyle = 1 << 0,
            LineSpacing = 1 << 1,
        }

        [SerializeField, Tooltip("テキストスタイルを反映する Text コンポーネント")]
        private Text _targetText;
        [SerializeField, Tooltip("Palette から適用しない LegacyTextStyle 項目")]
        private IgnoreStyleMask _ignoreStyleMask;

        /// <summary>
        /// 解決済みの LegacyTextStyle を Text に反映する
        /// </summary>
        /// <param name="value">反映するテキストスタイル</param>
        protected override void ApplyValue(LegacyTextStylePaletteValue value) {
            if (_targetText == null) {
                return;
            }

            _targetText.font = value.Font;
            if (!IsIgnored(IgnoreStyleMask.FontStyle)) {
                _targetText.fontStyle = value.FontStyle;
            }
            _targetText.fontSize = value.FontSize;
            if (!IsIgnored(IgnoreStyleMask.LineSpacing)) {
                _targetText.lineSpacing = value.LineSpacing;
            }
            _targetText.SetAllDirty();
        }

        /// <summary>
        /// 指定した LegacyTextStyle 項目を無視する設定か判定する
        /// </summary>
        /// <param name="ignoreStyleMask">判定対象の項目</param>
        /// <returns>無視する設定なら true</returns>
        private bool IsIgnored(IgnoreStyleMask ignoreStyleMask) {
            return (_ignoreStyleMask & ignoreStyleMask) != 0;
        }

        /// <summary>
        /// Inspector 更新時に参照を補完する
        /// </summary>
        protected override void OnValidateInternal() {
            AssignTargetTextIfNeeded();
        }

        /// <summary>
        /// コンポーネント追加時に参照を補完する
        /// </summary>
        private void Reset() {
            AssignTargetTextIfNeeded();
        }

        /// <summary>
        /// 参照先 Text が未設定なら同一 GameObject から取得する
        /// </summary>
        private void AssignTargetTextIfNeeded() {
            if (_targetText != null) {
                return;
            }

            _targetText = GetComponent<Text>();
        }
    }
}
