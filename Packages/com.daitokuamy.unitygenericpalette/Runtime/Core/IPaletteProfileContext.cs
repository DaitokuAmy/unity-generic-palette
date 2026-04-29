using System;

namespace UnityGenericPalette {
    /// <summary>
    /// 現在の Profile 状態の参照と変更通知購読を扱うコンテキスト
    /// </summary>
    public interface IPaletteProfileContext {
        /// <summary>
        /// 指定した ProfileAsset 型の変更通知を購読する
        /// </summary>
        /// <typeparam name="TProfileAsset">購読対象の ProfileAsset 型</typeparam>
        /// <param name="changedProfileAction">通知先ハンドラー</param>
        void SubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction)
            where TProfileAsset : PaletteProfileAssetBase;

        /// <summary>
        /// 指定した ProfileAsset 型の変更通知購読を解除する
        /// </summary>
        /// <typeparam name="TProfileAsset">購読解除対象の ProfileAsset 型</typeparam>
        /// <param name="changedProfileAction">解除対象のハンドラー</param>
        void UnsubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction)
            where TProfileAsset : PaletteProfileAssetBase;

        /// <summary>
        /// 指定した ProfileAsset 型に対応する現在の ProfileAsset を取得する
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="profileAsset">取得できた ProfileAsset</param>
        /// <returns>取得できた場合は true</returns>
        bool TryGetCurrentProfileAsset<TProfileAsset>(out TProfileAsset profileAsset)
            where TProfileAsset : PaletteProfileAssetBase;

        /// <summary>
        /// 指定した ProfileAsset 型に対応する現在の ProfileId を取得する
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="profileId">取得できた ProfileId</param>
        /// <returns>取得できた場合は true</returns>
        bool TryGetCurrentProfileId<TProfileAsset>(out string profileId)
            where TProfileAsset : PaletteProfileAssetBase;
    }
}
