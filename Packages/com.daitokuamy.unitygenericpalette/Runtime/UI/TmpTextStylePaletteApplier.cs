using TMPro;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// TextStyle Palette の値を TMP_Text へ反映する Applier
    /// </summary>
    [AddComponentMenu("Unity Generic Palette/TMP Text Style Palette Applier")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TmpTextStylePaletteApplier : PaletteApplierBase<TextStylePaletteAsset, TextStylePaletteProfileAsset, TextStylePaletteValue> {
        /// <summary>
        /// Palette からの適用を無視する TextStyle 項目
        /// </summary>
        [System.Flags]
        private enum IgnoreStyleMask {
            None = 0,
            Material = 1 << 0,
            FontStyle = 1 << 1,
            SpacingOptions = 1 << 2,
        }

        [SerializeField, Tooltip("テキストスタイルを反映する TMP_Text コンポーネント")]
        private TMP_Text _targetText;
        [SerializeField, Tooltip("Palette から適用しない TextStyle 項目")]
        private IgnoreStyleMask _ignoreStyleMask;

        /// <summary>
        /// 解決済みの TextStyle を TMP_Text に反映する
        /// </summary>
        /// <param name="value">反映するテキストスタイル</param>
        protected override void ApplyValue(TextStylePaletteValue value) {
            if (_targetText == null) {
                return;
            }

            _targetText.font = value.FontAsset;
            if (!IsIgnored(IgnoreStyleMask.Material)) {
                _targetText.fontSharedMaterial = ResolveMaterialPreset(value);
            }
            if (!IsIgnored(IgnoreStyleMask.FontStyle)) {
                _targetText.fontStyle = value.FontStyle;
            }
            _targetText.enableAutoSizing = value.EnableAutoSizing;
            _targetText.fontSize = value.FontSize;
            _targetText.fontSizeMin = value.FontSizeMin;
            _targetText.fontSizeMax = value.FontSizeMax;
            if (!IsIgnored(IgnoreStyleMask.SpacingOptions)) {
                _targetText.characterWidthAdjustment = value.CharacterWidthAdjustment;
                _targetText.lineSpacingAdjustment = value.LineSpacingAdjustment;
                _targetText.characterSpacing = value.CharacterSpacing;
                _targetText.wordSpacing = value.WordSpacing;
                _targetText.lineSpacing = value.LineSpacing;
                _targetText.paragraphSpacing = value.ParagraphSpacing;
            }
            _targetText.UpdateMeshPadding();
            _targetText.ForceMeshUpdate();
        }

        /// <summary>
        /// 指定した TextStyle 項目を無視する設定か判定する
        /// </summary>
        /// <param name="ignoreStyleMask">判定対象の項目</param>
        /// <returns>無視する設定なら true</returns>
        private bool IsIgnored(IgnoreStyleMask ignoreStyleMask) {
            return (_ignoreStyleMask & ignoreStyleMask) != 0;
        }

        /// <summary>
        /// 適用対象の Material Preset を解決する
        /// </summary>
        /// <param name="value">参照する TextStyle</param>
        /// <returns>反映に使う Material</returns>
        private static Material ResolveMaterialPreset(TextStylePaletteValue value) {
            if (value.MaterialPreset != null) {
                return value.MaterialPreset;
            }

            return value.FontAsset != null ? value.FontAsset.material : null;
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
        /// 参照先 TMP_Text が未設定なら同一 GameObject から取得する
        /// </summary>
        private void AssignTargetTextIfNeeded() {
            if (_targetText != null) {
                return;
            }

            _targetText = GetComponent<TMP_Text>();
        }
    }
}
