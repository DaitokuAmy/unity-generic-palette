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

        private IPaletteProfileContext _subscribedPaletteProfileContext;

        /// <summary>参照する Entry の ID</summary>
        public string EntryId => _entryId;

        /// <summary>
        /// 有効化時の処理
        /// </summary>
        protected virtual void OnEnable() {
            if (!PaletteProfileContextResolver.TryGetCurrent(out var paletteProfileContext)) {
                return;
            }

            _subscribedPaletteProfileContext = paletteProfileContext;
            _subscribedPaletteProfileContext.SubscribeChangedProfile<TPaletteProfileAsset>(OnChangedProfile);
            ApplyCurrentProfileIfAvailable(_subscribedPaletteProfileContext);
        }

        /// <summary>
         /// 無効化時の処理
         /// </summary>
        protected virtual void OnDisable() {
            if (_subscribedPaletteProfileContext == null) {
                return;
            }

            _subscribedPaletteProfileContext.UnsubscribeChangedProfile<TPaletteProfileAsset>(OnChangedProfile);
            _subscribedPaletteProfileContext = null;
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
            if (paletteProfileContext.TryGetCurrentProfileAsset<TPaletteProfileAsset>(out var paletteProfileAsset)) {
                OnChangedProfile(paletteProfileAsset);
            }
        }

        /// <summary>
        /// プロファイル変更通知
        /// </summary>
        /// <param name="paletteProfileAsset">変更後の ProfileAsset</param>
        private void OnChangedProfile(TPaletteProfileAsset paletteProfileAsset) {
            if (paletteProfileAsset == null || string.IsNullOrEmpty(_entryId)) {
                return;
            }

            var value = paletteProfileAsset.GetValueById(_entryId);
            ApplyValue(value);
        }
    }
}
