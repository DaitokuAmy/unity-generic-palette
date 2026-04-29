using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// Palette の値をコンポーネントへ反映する MonoBehaviour の基底
    /// </summary>
    public abstract class PaletteApplierBase<TPaletteAsset, TPaletteProfileAsset, TValue> : MonoBehaviour
        where TPaletteAsset : PaletteAssetBase
        where TPaletteProfileAsset : PaletteProfileAssetBase<TPaletteAsset, TValue> {
        [SerializeField, PaletteEntryId, Tooltip("参照する Entry の安定 ID")]
        private string _entryId;

        /// <summary>参照する Entry の安定 ID</summary>
        public string EntryId => _entryId;

        /// <summary>
        /// 有効化時の処理
        /// </summary>
        protected virtual void OnEnable() {
            var paletteEngine = PaletteEngine.RuntimeInstance;
            if (paletteEngine == null) {
                return;
            }

            paletteEngine.SubscribeChangedProfile<TPaletteProfileAsset>(OnChangedProfile);
            ApplyCurrentProfileIfAvailable(paletteEngine);
        }

        /// <summary>
         /// 無効化時の処理
         /// </summary>
        protected virtual void OnDisable() {
            var paletteEngine = PaletteEngine.RuntimeInstance;
            if (paletteEngine == null) {
                return;
            }

            paletteEngine.UnsubscribeChangedProfile<TPaletteProfileAsset>(OnChangedProfile);
        }

        /// <summary>
        /// 値変更時の再反映処理
        /// </summary>
        private void OnValidate() {
            OnValidateInternal();

            var paletteEngine = PaletteEngine.RuntimeInstance;
            if (paletteEngine == null) {
                return;
            }

            ApplyCurrentProfileIfAvailable(paletteEngine);
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
        /// <param name="paletteEngine">参照する PaletteEngine</param>
        private void ApplyCurrentProfileIfAvailable(PaletteEngine paletteEngine) {
            if (paletteEngine.TryGetCurrentProfileAsset<TPaletteProfileAsset>(out var paletteProfileAsset)) {
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
