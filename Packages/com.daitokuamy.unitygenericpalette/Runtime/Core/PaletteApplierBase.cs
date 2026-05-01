using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// Palette の値をコンポーネントへ反映する MonoBehaviour の基底
    /// </summary>
    [ExecuteAlways]
    public abstract class PaletteApplierBase<TPaletteAsset, TPaletteProfileAsset, TValue> : MonoBehaviour
        where TPaletteAsset : PaletteAssetBase
        where TPaletteProfileAsset : PaletteProfileAssetBase<TPaletteAsset, TValue> {
        [SerializeField, PaletteEntryId, Tooltip("参照する Entry の ID")]
        private string _entryId;

        private IPaletteProfileContext _paletteProfileContext;

        /// <summary>参照する Entry の ID</summary>
        public string EntryId => _entryId;

        /// <summary>
        /// 有効化時の処理
        /// </summary>
        protected virtual void OnEnable() {
            if (!PaletteProfileContextResolver.TryGetCurrent(out var paletteProfileContext)) {
                return;
            }

            _paletteProfileContext = paletteProfileContext;
            _paletteProfileContext.SubscribeChangedProfile<TPaletteProfileAsset>(OnChangedProfile);
            ApplyCurrentProfileIfAvailable(_paletteProfileContext);
        }

        /// <summary>
         /// 無効化時の処理
         /// </summary>
        protected virtual void OnDisable() {
            if (_paletteProfileContext == null) {
                return;
            }

            _paletteProfileContext.UnsubscribeChangedProfile<TPaletteProfileAsset>(OnChangedProfile);
            _paletteProfileContext = null;
        }

        /// <summary>
        /// 値変更時の再反映処理
        /// </summary>
        private void OnValidate() {
            OnValidateInternal();

            if (!PaletteProfileContextResolver.TryGetCurrent(out var paletteProfileContext)) {
                return;
            }

            ApplyCurrentProfileIfAvailable(paletteProfileContext);
        }

        /// <summary>
        /// 解決済みの値を対象へ反映する
        /// </summary>
        /// <param name="value">反映する値</param>
        protected abstract void ApplyValue(TValue value);

        /// <summary>
        /// OnValidate 時の拡張処理
        /// </summary>
        protected virtual void OnValidateInternal() {
        }

        /// <summary>
        /// 現在の Profile が存在する場合に反映する
        /// </summary>
        /// <param name="paletteProfileContext">参照する ProfileContext</param>
        private void ApplyCurrentProfileIfAvailable(IPaletteProfileContext paletteProfileContext) {
            if (paletteProfileContext.TryGetCurrentPaletteProfile<TPaletteAsset, TPaletteProfileAsset>(
                out var paletteAsset,
                out var paletteProfileAsset)) {
                ApplyProfileValue(paletteAsset, paletteProfileAsset);
            }
        }

        /// <summary>
        /// プロファイル変更通知
        /// </summary>
        /// <param name="paletteProfileAsset">変更後の ProfileAsset</param>
        private void OnChangedProfile(TPaletteProfileAsset paletteProfileAsset) {
            if (_paletteProfileContext == null ||
                !_paletteProfileContext.TryGetCurrentPaletteProfile<TPaletteAsset, TPaletteProfileAsset>(
                    out var paletteAsset,
                    out var currentProfileAsset) ||
                !ReferenceEquals(currentProfileAsset, paletteProfileAsset)) {
                return;
            }

            ApplyProfileValue(paletteAsset, paletteProfileAsset);
        }

        /// <summary>
        /// 指定した PaletteAsset と ProfileAsset を使って値を反映する
        /// </summary>
        /// <param name="paletteAsset">参照する PaletteAsset</param>
        /// <param name="paletteProfileAsset">参照する ProfileAsset</param>
        private void ApplyProfileValue(TPaletteAsset paletteAsset, TPaletteProfileAsset paletteProfileAsset) {
            if (paletteAsset == null ||
                paletteProfileAsset == null ||
                string.IsNullOrEmpty(_entryId) ||
                !paletteAsset.TryGetEntryIndex(_entryId, out var entryIndex)) {
                return;
            }

            ApplyValue(paletteProfileAsset.GetValueByIndex(entryIndex));
        }
    }
}
