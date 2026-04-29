using System.Collections.Generic;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// Palette の EntryId 集合と型情報を管理するアセットの基底
    /// </summary>
    public abstract class PaletteAssetBase : ScriptableObject {
        [SerializeField, Tooltip("Palette に含まれる Entry 定義一覧")]
        private List<PaletteEntry> _entries = new();

        /// <summary>Entry 一覧</summary>
        public IReadOnlyList<PaletteEntry> Entries => _entries;
    }
}
