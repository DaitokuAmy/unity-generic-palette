using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
#if USE_UNI_TASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace UnityGenericPalette {
    /// <summary>
    /// Palette ごとの Profile 状態を管理し、再反映通知を行うランタイムコンポーネント
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PaletteEngine : MonoBehaviour {
        [SerializeField, Tooltip("参照する PaletteAssetStorage")]
        private PaletteAssetStorage _paletteAssetStorage;
        [SerializeField, Tooltip("Loader を使わずに優先利用する built-in ProfileAsset 一覧")]
        private List<ScriptableObject> _builtInProfileAssets = new();

        private readonly Dictionary<PaletteAssetBase, string> _currentProfileIds = new();
        private readonly Dictionary<PaletteAssetBase, ScriptableObject> _loadedProfileAssets = new();
        private readonly Dictionary<Type, List<Delegate>> _changedProfileHandlers = new();
        private readonly HashSet<PaletteAssetBase> _loaderOwnedPaletteAssets = new();

        private IPaletteProfileLoader _paletteProfileLoader;

        /// <summary>Runtime用のシングルトンアクセス用インスタンス</summary>
        internal static PaletteEngine RuntimeInstance { get; private set; }

        /// <summary>
        /// 生成時処理
        /// </summary>
        private void Awake() {
            if (RuntimeInstance != null) {
                Destroy(gameObject);
                return;
            }

            RuntimeInstance = this;
        }

        /// <summary>
        /// 廃棄時処理
        /// </summary>
        private void OnDestroy() {
            if (RuntimeInstance == this) {
                RuntimeInstance = null;
            }
        }

        /// <summary>
        /// ProfileLoader を設定する
        /// </summary>
        /// <param name="paletteProfileLoader">設定する ProfileLoader</param>
        public void SetLoader(IPaletteProfileLoader paletteProfileLoader) {
            _paletteProfileLoader = paletteProfileLoader;
        }

        /// <summary>
        /// 指定した ProfileAsset 型の変更通知を購読する
        /// </summary>
        /// <typeparam name="TProfileAsset">購読対象の ProfileAsset 型</typeparam>
        /// <param name="changedProfileAction">通知先ハンドラー</param>
        public void SubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction)
            where TProfileAsset : ScriptableObject, IPaletteProfileAsset {
            if (changedProfileAction == null) {
                throw new ArgumentNullException(nameof(changedProfileAction));
            }

            var profileAssetType = typeof(TProfileAsset);
            if (!_changedProfileHandlers.TryGetValue(profileAssetType, out var changedProfileHandlers)) {
                changedProfileHandlers = new List<Delegate>();
                _changedProfileHandlers.Add(profileAssetType, changedProfileHandlers);
            }

            if (changedProfileHandlers.Contains(changedProfileAction)) {
                return;
            }

            changedProfileHandlers.Add(changedProfileAction);
        }

        /// <summary>
        /// 指定した ProfileAsset 型の変更通知購読を解除する
        /// </summary>
        /// <typeparam name="TProfileAsset">購読解除対象の ProfileAsset 型</typeparam>
        /// <param name="changedProfileAction">解除対象のハンドラー</param>
        public void UnsubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction)
            where TProfileAsset : ScriptableObject, IPaletteProfileAsset {
            if (changedProfileAction == null) {
                return;
            }

            var profileAssetType = typeof(TProfileAsset);
            if (!_changedProfileHandlers.TryGetValue(profileAssetType, out var changedProfileHandlers)) {
                return;
            }

            changedProfileHandlers.Remove(changedProfileAction);
            if (changedProfileHandlers.Count == 0) {
                _changedProfileHandlers.Remove(profileAssetType);
            }
        }

        /// <summary>
        /// 指定した ProfileAsset 型に対応する Profile を切り替える
        /// </summary>
        /// <typeparam name="TProfileAsset">対象の ProfileAsset 型</typeparam>
        /// <param name="profileId">切り替え先の Profile ID</param>
        /// <param name="cancellationToken">キャンセル制御に使うトークン</param>
        /// <returns>切り替え完了を表す非同期処理</returns>
#if USE_UNI_TASK
        public async UniTask ChangeProfileAsync<TProfileAsset>(string profileId, CancellationToken cancellationToken = default)
#else
        public async Task ChangeProfileAsync<TProfileAsset>(string profileId, CancellationToken cancellationToken = default)
#endif
            where TProfileAsset : ScriptableObject, IPaletteProfileAsset {
            if (string.IsNullOrEmpty(profileId)) {
                throw new ArgumentException("Profile ID must not be null or empty.", nameof(profileId));
            }

            var isLoaderOwned = false;
            if (!TryGetBuiltInProfileAsset<TProfileAsset>(profileId, out var nextProfileAsset)) {
                if (_paletteProfileLoader == null) {
                    throw new InvalidOperationException("PaletteProfileLoader is not assigned.");
                }

                nextProfileAsset = await _paletteProfileLoader.LoadAsync<TProfileAsset>(profileId, cancellationToken);
                if (nextProfileAsset == null) {
                    throw new InvalidOperationException(
                        $"PaletteProfileLoader returned null for {typeof(TProfileAsset).Name} and ProfileId '{profileId}'.");
                }

                isLoaderOwned = true;
            }

            var paletteAsset = nextProfileAsset.PaletteAssetBase;
            if (paletteAsset == null) {
                throw new InvalidOperationException(
                    $"{typeof(TProfileAsset).Name} has no PaletteAsset reference for ProfileId '{profileId}'.");
            }

            if (_currentProfileIds.TryGetValue(paletteAsset, out var currentProfileId) &&
                _loadedProfileAssets.TryGetValue(paletteAsset, out var currentLoadedProfileAsset) &&
                currentProfileId == profileId &&
                ReferenceEquals(currentLoadedProfileAsset, nextProfileAsset)) {
                return;
            }

            UnloadLoaderOwnedProfileAsset(paletteAsset, nextProfileAsset);
            _currentProfileIds[paletteAsset] = profileId;
            _loadedProfileAssets[paletteAsset] = nextProfileAsset;
            if (isLoaderOwned) {
                _loaderOwnedPaletteAssets.Add(paletteAsset);
            }
            else {
                _loaderOwnedPaletteAssets.Remove(paletteAsset);
            }

            RaiseChangedProfile(nextProfileAsset);
        }

        /// <summary>
        /// 指定した PaletteAsset に対応する現在の Profile ID を取得する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="profileId">取得できた Profile ID</param>
        /// <returns>取得できた場合は true</returns>
        public bool TryGetCurrentProfileId(PaletteAssetBase paletteAsset, out string profileId) {
            if (paletteAsset == null) {
                profileId = default;
                return false;
            }

            return _currentProfileIds.TryGetValue(paletteAsset, out profileId);
        }

        /// <summary>
        /// 指定した ProfileAsset 型に対応する現在のロード済み ProfileAsset を取得する
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="profileAsset">取得できた ProfileAsset</param>
        /// <returns>取得できた場合は true</returns>
        public bool TryGetCurrentProfileAsset<TProfileAsset>(out TProfileAsset profileAsset)
            where TProfileAsset : ScriptableObject, IPaletteProfileAsset {
            foreach (var loadedProfileAsset in _loadedProfileAssets.Values) {
                if (loadedProfileAsset is TProfileAsset typedProfileAsset) {
                    profileAsset = typedProfileAsset;
                    return true;
                }
            }

            profileAsset = null;
            return false;
        }

        /// <summary>
        /// 指定した PaletteAsset に対応するロード済み ProfileAsset を取得する
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="profileAsset">取得できた ProfileAsset</param>
        /// <returns>取得できた場合は true</returns>
        public bool TryGetLoadedProfileAsset<TProfileAsset>(PaletteAssetBase paletteAsset, out TProfileAsset profileAsset)
            where TProfileAsset : ScriptableObject, IPaletteProfileAsset {
            if (paletteAsset != null &&
                _loadedProfileAssets.TryGetValue(paletteAsset, out var loadedProfileAsset) &&
                loadedProfileAsset is TProfileAsset typedProfileAsset) {
                profileAsset = typedProfileAsset;
                return true;
            }

            profileAsset = null;
            return false;
        }

        /// <summary>
        /// 指定した ProfileAsset の変更通知を発火する
        /// </summary>
        /// <typeparam name="TProfileAsset">通知対象の ProfileAsset 型</typeparam>
        /// <param name="profileAsset">通知対象の ProfileAsset</param>
        private void RaiseChangedProfile<TProfileAsset>(TProfileAsset profileAsset)
            where TProfileAsset : ScriptableObject, IPaletteProfileAsset {
            if (!_changedProfileHandlers.TryGetValue(typeof(TProfileAsset), out var changedProfileHandlers)) {
                return;
            }

            for (var i = 0; i < changedProfileHandlers.Count; i++) {
                if (changedProfileHandlers[i] is Action<TProfileAsset> changedProfileHandler) {
                    changedProfileHandler.Invoke(profileAsset);
                }
            }
        }

        /// <summary>
        /// Loader 管理下の既存 ProfileAsset を解放する
        /// </summary>
        /// <param name="paletteAsset">解放対象を保持している PaletteAsset</param>
        /// <param name="nextProfileAsset">これから設定する ProfileAsset</param>
        /// <exception cref="InvalidOperationException">解放が必要なのに Loader が未設定の場合</exception>
        private void UnloadLoaderOwnedProfileAsset(PaletteAssetBase paletteAsset, ScriptableObject nextProfileAsset) {
            if (!_loadedProfileAssets.TryGetValue(paletteAsset, out var loadedProfileAsset)) {
                return;
            }

            if (!_loaderOwnedPaletteAssets.Contains(paletteAsset)) {
                return;
            }

            if (ReferenceEquals(loadedProfileAsset, nextProfileAsset)) {
                return;
            }

            if (_paletteProfileLoader == null) {
                throw new InvalidOperationException("PaletteProfileLoader is not assigned while unloading a previously loaded ProfileAsset.");
            }

            _paletteProfileLoader.Unload(loadedProfileAsset);
            _loadedProfileAssets.Remove(paletteAsset);
            _loaderOwnedPaletteAssets.Remove(paletteAsset);
        }

        /// <summary>
        /// built-in ProfileAsset 一覧から一致するアセットを取得
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="profileId">対象の Profile ID</param>
        /// <param name="profileAsset">取得できた ProfileAsset</param>
        /// <returns>取得できた場合は true</returns>
        private bool TryGetBuiltInProfileAsset<TProfileAsset>(string profileId, out TProfileAsset profileAsset)
            where TProfileAsset : ScriptableObject, IPaletteProfileAsset {
            for (var i = 0; i < _builtInProfileAssets.Count; i++) {
                var builtInProfileAsset = _builtInProfileAssets[i];
                if (builtInProfileAsset is not TProfileAsset typedProfileAsset) {
                    continue;
                }

                if (typedProfileAsset.ProfileId != profileId) {
                    continue;
                }

                profileAsset = typedProfileAsset;
                return true;
            }

            profileAsset = null;
            return false;
        }
    }
}
