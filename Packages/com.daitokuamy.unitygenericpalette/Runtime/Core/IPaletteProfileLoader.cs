using System;
using System.Threading;
using UnityEngine;
#if USE_UNI_TASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace UnityGenericPalette {
    /// <summary>
    /// ProfileAsset のロードと解放を扱うローダー
    /// </summary>
    public interface IPaletteProfileLoader {
        /// <summary>
        /// 指定した ProfileAsset 型と Profile に対応する ProfileAsset を非同期でロードする
        /// </summary>
        /// <typeparam name="TProfileAsset">ロードする ProfileAsset の型</typeparam>
        /// <param name="profileId">ロード対象の Profile ID</param>
        /// <param name="cancellationToken">キャンセル制御に使うトークン</param>
        /// <returns>ロードされた ProfileAsset を返す非同期処理</returns>
#if USE_UNI_TASK
        UniTask<TProfileAsset> LoadAsync<TProfileAsset>(string profileId, CancellationToken cancellationToken)
#else
        Task<TProfileAsset> LoadAsync<TProfileAsset>(string profileId, CancellationToken cancellationToken)
#endif
            where TProfileAsset : ScriptableObject, IPaletteProfileAsset;

        /// <summary>
        /// ロード済みの ProfileAsset を解放する
        /// </summary>
        /// <param name="profileAsset">解放対象の ProfileAsset</param>
        void Unload(ScriptableObject profileAsset);
    }
}
