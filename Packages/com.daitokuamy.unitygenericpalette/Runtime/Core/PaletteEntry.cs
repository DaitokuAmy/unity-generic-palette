using System;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// Palette 内の 1 エントリを表す定義
    /// </summary>
    [Serializable]
    public sealed class PaletteEntry {
        [SerializeField, Tooltip("Entry を一意に識別する ID")]
        private string _entryId;
        [SerializeField, Tooltip("Entry の表示名")]
        private string _displayName;
        [SerializeField, Tooltip("Entry の補足説明")]
        private string _description;

        /// <summary>Entry の ID</summary>
        public string EntryId => _entryId;
        /// <summary>表示名</summary>
        public string DisplayName => _displayName;
        /// <summary>説明文</summary>
        public string Description => _description;
    }
}
