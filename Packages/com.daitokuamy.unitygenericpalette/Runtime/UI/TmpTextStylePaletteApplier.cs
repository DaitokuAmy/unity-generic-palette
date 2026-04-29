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
        [SerializeField, Tooltip("テキストスタイルを反映する TMP_Text コンポーネント")]
        private TMP_Text _targetText;

        /// <summary>
        /// 解決済みの TextStyle を TMP_Text に反映する
        /// </summary>
        /// <param name="value">反映するテキストスタイル</param>
        protected override void ApplyValue(TextStylePaletteValue value) {
            if (_targetText == null) {
                return;
            }

            var materialPreset = ResolveMaterialPreset(value);
            _targetText.font = value.FontAsset;
            _targetText.fontSharedMaterial = materialPreset;
            _targetText.fontStyle = value.FontStyle;
            _targetText.enableAutoSizing = value.EnableAutoSizing;
            _targetText.fontSize = value.FontSize;
            _targetText.fontSizeMin = value.FontSizeMin;
            _targetText.fontSizeMax = value.FontSizeMax;
            _targetText.characterWidthAdjustment = value.CharacterWidthAdjustment;
            _targetText.lineSpacingAdjustment = value.LineSpacingAdjustment;
            _targetText.characterSpacing = value.CharacterSpacing;
            _targetText.wordSpacing = value.WordSpacing;
            _targetText.lineSpacing = value.LineSpacing;
            _targetText.paragraphSpacing = value.ParagraphSpacing;
            _targetText.UpdateMeshPadding();
            _targetText.ForceMeshUpdate();
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
