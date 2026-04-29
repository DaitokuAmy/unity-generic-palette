using System;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// Profile アセット内の 1 エントリ分の値
    /// </summary>
    /// <typeparam name="TValue">保持する値の型</typeparam>
    [Serializable]
    public sealed class PaletteProfileValue<TValue> {
        [SerializeField, Tooltip("対応する Entry の ID")]
        private string _entryId;
        [SerializeField, Tooltip("EntryId に対応する値")]
        private TValue _value;

        /// <summary>対応する Entry の ID</summary>
        public string EntryId => _entryId;
        /// <summary>EntryId に対応する値</summary>
        public TValue Value => _value;
    }
}
