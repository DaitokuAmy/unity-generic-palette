using System.Collections.Generic;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// Palette の EntryId 集合と型情報を管理するアセットの基底
    /// </summary>
    public abstract class PaletteAssetBase : ScriptableObject {
        [SerializeField, Tooltip("Palette に含まれる Entry 定義一覧")]
        private List<PaletteEntry> _entries = new();
        [SerializeField, Tooltip("初期化時に適用する既定 Profile の ID")]
        private string _defaultProfileId;

        /// <summary>Entry 一覧</summary>
        public IReadOnlyList<PaletteEntry> Entries => _entries;
        /// <summary>初期化時に適用する既定 Profile の ID</summary>
        public string DefaultProfileId => _defaultProfileId;
    }
}
