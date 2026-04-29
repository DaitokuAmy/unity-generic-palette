using System.Collections.Generic;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// Palette と Profile の索引を管理するルートアセット
    /// </summary>
    [CreateAssetMenu(fileName = "PaletteAssetStorage", menuName = "Unity Generic Palette/Palette Asset Storage")]
    public class PaletteAssetStorage : ScriptableObject {
        [SerializeField, Tooltip("Storage に登録されている Palette 一覧")]
        private List<PaletteAssetBase> _paletteAssets = new();
        
        /// <summary>登録済み Palette 一覧</summary>
        public IReadOnlyList<PaletteAssetBase> PaletteAssets => _paletteAssets;
    }
}
