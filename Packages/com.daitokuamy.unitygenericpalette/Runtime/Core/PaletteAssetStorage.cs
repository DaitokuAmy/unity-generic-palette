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

        private Dictionary<string, PaletteAssetBase> _paletteAssetIdentityCache;
        
        /// <summary>登録済み Palette 一覧</summary>
        public IReadOnlyList<PaletteAssetBase> PaletteAssets => _paletteAssets;

        /// <summary>
        /// 指定した Palette 識別子に対応する PaletteAsset を取得する
        /// </summary>
        /// <param name="paletteGuid">取得対象の PaletteGuid</param>
        /// <param name="paletteLocalFileId">取得対象の PaletteLocalFileId</param>
        /// <param name="paletteAsset">取得できた PaletteAsset</param>
        /// <returns>取得できた場合は true</returns>
        public bool TryGetPaletteAsset(string paletteGuid, long paletteLocalFileId, out PaletteAssetBase paletteAsset) {
            if (string.IsNullOrEmpty(paletteGuid) || paletteLocalFileId == 0) {
                paletteAsset = null;
                return false;
            }

            EnsurePaletteAssetIdentityCache();
            return _paletteAssetIdentityCache.TryGetValue(BuildPaletteIdentityKey(paletteGuid, paletteLocalFileId), out paletteAsset);
        }

        /// <summary>
        /// Palette 識別子から PaletteAsset を引くキャッシュを初期化する
        /// </summary>
        private void EnsurePaletteAssetIdentityCache() {
            if (_paletteAssetIdentityCache != null) {
                return;
            }

            _paletteAssetIdentityCache = new Dictionary<string, PaletteAssetBase>(_paletteAssets.Count);
            for (var i = 0; i < _paletteAssets.Count; i++) {
                var paletteAsset = _paletteAssets[i];
                if (paletteAsset == null ||
                    string.IsNullOrEmpty(paletteAsset.PaletteGuid) ||
                    paletteAsset.PaletteLocalFileId == 0) {
                    continue;
                }

                _paletteAssetIdentityCache.TryAdd(
                    BuildPaletteIdentityKey(paletteAsset.PaletteGuid, paletteAsset.PaletteLocalFileId),
                    paletteAsset);
            }
        }

        /// <summary>
        /// Palette 識別子キャッシュ用キーを組み立てる
        /// </summary>
        /// <param name="paletteGuid">PaletteGuid</param>
        /// <param name="paletteLocalFileId">PaletteLocalFileId</param>
        /// <returns>キャッシュ用キー</returns>
        private static string BuildPaletteIdentityKey(string paletteGuid, long paletteLocalFileId) {
            return $"{paletteGuid}:{paletteLocalFileId}";
        }

        /// <summary>
        /// デシリアライズ後のキャッシュを無効化する
        /// </summary>
        private void OnValidate() {
            _paletteAssetIdentityCache = null;
        }
    }
}
