using System;
using TMPro;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// TextMeshPro 用のテキストスタイル値
    /// </summary>
    [Serializable]
    public struct TextStylePaletteValue {
        [SerializeField, Tooltip("適用する Font Asset")]
        private TMP_FontAsset _fontAsset;
        [SerializeField, Tooltip("適用する Material Preset")]
        private Material _materialPreset;
        [SerializeField, Tooltip("適用する Font Style")]
        private FontStyles _fontStyle;
        [SerializeField, Tooltip("Auto Size を有効にするか")]
        private bool _enableAutoSizing;
        [SerializeField, Tooltip("適用する Font Size")]
        private float _fontSize;
        [SerializeField, Tooltip("Auto Size 時の最小 Font Size")]
        private float _fontSizeMin;
        [SerializeField, Tooltip("Auto Size 時の最大 Font Size")]
        private float _fontSizeMax;
        [SerializeField, Tooltip("Auto Size 時に許容する最大 Character Width 縮小率")]
        private float _characterWidthAdjustment;
        [SerializeField, Tooltip("Auto Size 時に許容する最大 Line Spacing 調整量")]
        private float _lineSpacingAdjustment;
        [SerializeField, Tooltip("適用する Character Spacing")]
        private float _characterSpacing;
        [SerializeField, Tooltip("適用する Word Spacing")]
        private float _wordSpacing;
        [SerializeField, Tooltip("適用する Line Spacing")]
        private float _lineSpacing;
        [SerializeField, Tooltip("適用する Paragraph Spacing")]
        private float _paragraphSpacing;

        /// <summary>適用する Font Asset</summary>
        public TMP_FontAsset FontAsset => _fontAsset;
        /// <summary>適用する Material Preset</summary>
        public Material MaterialPreset => _materialPreset;
        /// <summary>適用する Font Style</summary>
        public FontStyles FontStyle => _fontStyle;
        /// <summary>Auto Size を有効にするか</summary>
        public bool EnableAutoSizing => _enableAutoSizing;
        /// <summary>適用する Font Size</summary>
        public float FontSize => _fontSize;
        /// <summary>Auto Size 時の最小 Font Size</summary>
        public float FontSizeMin => _fontSizeMin;
        /// <summary>Auto Size 時の最大 Font Size</summary>
        public float FontSizeMax => _fontSizeMax;
        /// <summary>Auto Size 時に許容する最大 Character Width 縮小率</summary>
        public float CharacterWidthAdjustment => _characterWidthAdjustment;
        /// <summary>Auto Size 時に許容する最大 Line Spacing 調整量</summary>
        public float LineSpacingAdjustment => _lineSpacingAdjustment;
        /// <summary>適用する Character Spacing</summary>
        public float CharacterSpacing => _characterSpacing;
        /// <summary>適用する Word Spacing</summary>
        public float WordSpacing => _wordSpacing;
        /// <summary>適用する Line Spacing</summary>
        public float LineSpacing => _lineSpacing;
        /// <summary>適用する Paragraph Spacing</summary>
        public float ParagraphSpacing => _paragraphSpacing;
    }
}
