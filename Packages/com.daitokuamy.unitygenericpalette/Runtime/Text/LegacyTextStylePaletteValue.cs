using System;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// uGUI Text 用のテキストスタイル値
    /// </summary>
    [Serializable]
    public struct LegacyTextStylePaletteValue {
        [SerializeField, Tooltip("適用する Font")]
        private Font _font;
        [SerializeField, Tooltip("適用する Font Style")]
        private FontStyle _fontStyle;
        [SerializeField, Tooltip("適用する Font Size")]
        private int _fontSize;
        [SerializeField, Tooltip("適用する Line Spacing")]
        private float _lineSpacing;

        /// <summary>適用する Font</summary>
        public Font Font => _font;
        /// <summary>適用する Font Style</summary>
        public FontStyle FontStyle => _fontStyle;
        /// <summary>適用する Font Size</summary>
        public int FontSize => _fontSize;
        /// <summary>適用する Line Spacing</summary>
        public float LineSpacing => _lineSpacing;
    }
}
