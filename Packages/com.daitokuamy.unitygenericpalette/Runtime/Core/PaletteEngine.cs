using System;
using System.Collections.Generic;
using System.Reflection;
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
    public sealed class PaletteEngine : MonoBehaviour, IPaletteProfileContext {
        private static readonly MethodInfo ChangeProfileByTypeAsyncMethod = typeof(PaletteEngine)
            .GetMethod(nameof(ChangeProfileByTypeAsyncInternal), BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField, Tooltip("参照する PaletteAssetStorage")]
        private PaletteAssetStorage _paletteAssetStorage;
        [SerializeField, Tooltip("シーン遷移時に GameObject を破棄しないか")]
        private bool _dontDestroyOnLoad;
        [SerializeField, Tooltip("Loader を使わずに優先利用する Included ProfileAsset 一覧")]
        private List<PaletteProfileAssetBase> _includedProfileAssets = new();

        private readonly Dictionary<PaletteAssetBase, string> _currentProfileIds = new();
        private readonly Dictionary<PaletteAssetBase, PaletteProfileAssetBase> _loadedProfileAssets = new();
        private readonly Dictionary<Type, List<Delegate>> _changedProfileHandlers = new();
        private readonly HashSet<Type> _changingProfileAssetTypes = new();
        private readonly HashSet<PaletteAssetBase> _loaderOwnedPaletteAssets = new();

        private IPaletteProfileLoader _paletteProfileLoader;

        /// <summary>Runtime用のシングルトンアクセス用インスタンス</summary>
        internal static PaletteEngine RuntimeInstance { get; private set; }

        /// <summary>
        /// ProfileLoader を設定する
        /// </summary>
        /// <param name="paletteProfileLoader">設定する ProfileLoader</param>
        public static void SetLoader(IPaletteProfileLoader paletteProfileLoader) {
            GetRequiredRuntimeInstance().SetLoaderInternal(paletteProfileLoader);
        }

        /// <summary>
        /// PaletteAsset に設定された既定 Profile を初期化する
        /// </summary>
        /// <param name="cancellationToken">キャンセル制御に使うトークン</param>
#if USE_UNI_TASK
        public static UniTask InitializeAsync(CancellationToken cancellationToken = default)
#else
        public static Task InitializeAsync(CancellationToken cancellationToken = default)
#endif
        {
            return GetRequiredRuntimeInstance().InitializeAsyncInternal(cancellationToken);
        }

        void IPaletteProfileContext.SubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction) {
            SubscribeChangedProfile(changedProfileAction);
        }

        void IPaletteProfileContext.UnsubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction) {
            UnsubscribeChangedProfile(changedProfileAction);
        }

        bool IPaletteProfileContext.TryGetCurrentProfileAsset<TProfileAsset>(out TProfileAsset profileAsset) {
            return TryGetCurrentProfileAsset(out profileAsset);
        }

        /// <summary>
        /// 指定した ProfileAsset 型に対応する Profile を切り替える
        /// </summary>
        /// <typeparam name="TProfileAsset">対象の ProfileAsset 型</typeparam>
        /// <param name="profileId">切り替え先の Profile ID</param>
        /// <param name="cancellationToken">キャンセル制御に使うトークン</param>
        /// <returns>切り替え完了を表す非同期処理</returns>
#if USE_UNI_TASK
        public static UniTask ChangeProfileAsync<TProfileAsset>(string profileId, CancellationToken cancellationToken = default)
#else
        public static Task ChangeProfileAsync<TProfileAsset>(string profileId, CancellationToken cancellationToken = default)
#endif
            where TProfileAsset : PaletteProfileAssetBase {
            return GetRequiredRuntimeInstance().ChangeProfileAsyncInternal<TProfileAsset>(profileId, cancellationToken);
        }

        /// <summary>
        /// 指定した ProfileAsset 型の変更通知を購読する
        /// </summary>
        /// <typeparam name="TProfileAsset">購読対象の ProfileAsset 型</typeparam>
        /// <param name="changedProfileAction">通知先ハンドラー</param>
        internal void SubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction)
            where TProfileAsset : PaletteProfileAssetBase {
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
        internal void UnsubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction)
            where TProfileAsset : PaletteProfileAssetBase {
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
        /// 指定した ProfileAsset 型に対応する現在のロード済み ProfileAsset を取得する
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="profileAsset">取得できた ProfileAsset</param>
        /// <returns>取得できた場合は true</returns>
        internal bool TryGetCurrentProfileAsset<TProfileAsset>(out TProfileAsset profileAsset)
            where TProfileAsset : PaletteProfileAssetBase {
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
        /// ProfileLoader を設定する
        /// </summary>
        /// <param name="paletteProfileLoader">設定する ProfileLoader</param>
        private void SetLoaderInternal(IPaletteProfileLoader paletteProfileLoader) {
            _paletteProfileLoader = paletteProfileLoader;
        }

        /// <summary>
        /// PaletteAsset に設定された既定 Profile を初期化する
        /// </summary>
        /// <param name="cancellationToken">キャンセル制御に使うトークン</param>
#if USE_UNI_TASK
        private async UniTask InitializeAsyncInternal(CancellationToken cancellationToken = default)
#else
        private async Task InitializeAsyncInternal(CancellationToken cancellationToken = default)
#endif
        {
            if (_paletteAssetStorage == null) {
                return;
            }

            var initializedProfileAssetTypes = new HashSet<Type>();
#if USE_UNI_TASK
            var initializeTasks = new List<UniTask>();
#else
            var initializeTasks = new List<Task>();
#endif

            for (var i = 0; i < _paletteAssetStorage.PaletteAssets.Count; i++) {
                var paletteAsset = _paletteAssetStorage.PaletteAssets[i];
                if (paletteAsset == null || string.IsNullOrEmpty(paletteAsset.DefaultProfileId)) {
                    continue;
                }

                var profileAssetType = GetProfileAssetType(paletteAsset);
                if (profileAssetType == null) {
                    throw new InvalidOperationException(
                        $"Profile asset type is not defined on {paletteAsset.GetType().Name}.");
                }

                if (!initializedProfileAssetTypes.Add(profileAssetType)) {
                    throw new InvalidOperationException(
                        $"Multiple default profiles are configured for {profileAssetType.Name}. " +
                        "InitializeAsync supports only one default profile per profile asset type.");
                }

                initializeTasks.Add(ChangeProfileByTypeAsync(profileAssetType, paletteAsset.DefaultProfileId, cancellationToken));
            }

            if (initializeTasks.Count == 0) {
                return;
            }

#if USE_UNI_TASK
            await UniTask.WhenAll(initializeTasks);
#else
            await Task.WhenAll(initializeTasks);
#endif
        }

        /// <summary>
        /// 指定した ProfileAsset 型に対応する Profile を切り替える
        /// </summary>
        /// <typeparam name="TProfileAsset">対象の ProfileAsset 型</typeparam>
        /// <param name="profileId">切り替え先の Profile ID</param>
        /// <param name="cancellationToken">キャンセル制御に使うトークン</param>
        /// <returns>切り替え完了を表す非同期処理</returns>
#if USE_UNI_TASK
        private async UniTask ChangeProfileAsyncInternal<TProfileAsset>(string profileId, CancellationToken cancellationToken = default)
#else
        private async Task ChangeProfileAsyncInternal<TProfileAsset>(string profileId, CancellationToken cancellationToken = default)
#endif
            where TProfileAsset : PaletteProfileAssetBase {
            if (string.IsNullOrEmpty(profileId)) {
                throw new ArgumentException("Profile ID must not be null or empty.", nameof(profileId));
            }

            BeginProfileChangeRequest<TProfileAsset>();
            try {
                var isLoaderOwned = false;
                if (!TryGetIncludedProfileAsset<TProfileAsset>(profileId, out var nextProfileAsset)) {
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
            finally {
                EndProfileChangeRequest<TProfileAsset>();
            }
        }

        /// <summary>
        /// 指定した ProfileAsset 型に対応する Profile を切り替える
        /// </summary>
        /// <typeparam name="TProfileAsset">対象の ProfileAsset 型</typeparam>
        /// <param name="profileId">切り替え先の Profile ID</param>
        /// <param name="cancellationToken">キャンセル制御に使うトークン</param>
#if USE_UNI_TASK
        private UniTask ChangeProfileByTypeAsyncInternal<TProfileAsset>(string profileId, CancellationToken cancellationToken = default)
#else
        private Task ChangeProfileByTypeAsyncInternal<TProfileAsset>(string profileId, CancellationToken cancellationToken = default)
#endif
            where TProfileAsset : PaletteProfileAssetBase {
            return ChangeProfileAsyncInternal<TProfileAsset>(profileId, cancellationToken);
        }

        /// <summary>
        /// Runtime 用インスタンスを取得する
        /// </summary>
        /// <returns>現在の Runtime 用インスタンス</returns>
        /// <exception cref="InvalidOperationException">Runtime 用インスタンスが存在しない場合</exception>
        private static PaletteEngine GetRequiredRuntimeInstance() {
            if (RuntimeInstance == null) {
                throw new InvalidOperationException("PaletteEngine RuntimeInstance is not available.");
            }

            return RuntimeInstance;
        }

        /// <summary>
        /// ProfileAsset 型を指定して Profile を切り替える
        /// </summary>
        /// <param name="profileAssetType">対象の ProfileAsset 型</param>
        /// <param name="profileId">切り替え先の Profile ID</param>
        /// <param name="cancellationToken">キャンセル制御に使うトークン</param>
#if USE_UNI_TASK
        private async UniTask ChangeProfileByTypeAsync(Type profileAssetType, string profileId, CancellationToken cancellationToken = default)
#else
        private async Task ChangeProfileByTypeAsync(Type profileAssetType, string profileId, CancellationToken cancellationToken = default)
#endif
        {
            if (profileAssetType == null) {
                throw new ArgumentNullException(nameof(profileAssetType));
            }

            if (!typeof(PaletteProfileAssetBase).IsAssignableFrom(profileAssetType)) {
                throw new ArgumentException(
                    "Profile asset type must inherit from PaletteProfileAssetBase.",
                    nameof(profileAssetType));
            }

            if (ChangeProfileByTypeAsyncMethod == null) {
                throw new InvalidOperationException("ChangeProfileByTypeAsyncInternal method is not available.");
            }

            var genericMethod = ChangeProfileByTypeAsyncMethod.MakeGenericMethod(profileAssetType);
#if USE_UNI_TASK
            var changeProfileTask = (UniTask)genericMethod.Invoke(this, new object[] { profileId, cancellationToken });
            await changeProfileTask;
#else
            var changeProfileTask = (Task)genericMethod.Invoke(this, new object[] { profileId, cancellationToken });
            await changeProfileTask;
#endif
        }

        /// <summary>
        /// Profile 変更要求の開始を記録する
        /// </summary>
        /// <typeparam name="TProfileAsset">変更対象の ProfileAsset 型</typeparam>
        /// <exception cref="InvalidOperationException">同一型の Profile 変更が進行中の場合</exception>
        private void BeginProfileChangeRequest<TProfileAsset>()
            where TProfileAsset : PaletteProfileAssetBase {
            var profileAssetType = typeof(TProfileAsset);
            if (_changingProfileAssetTypes.Contains(profileAssetType)) {
                throw new InvalidOperationException(
                    $"Concurrent profile changes are not supported for {profileAssetType.Name}. Await the current change before starting another.");
            }

            _changingProfileAssetTypes.Add(profileAssetType);
        }

        /// <summary>
        /// Profile 変更要求の進行状態を解除する
        /// </summary>
        /// <typeparam name="TProfileAsset">変更対象の ProfileAsset 型</typeparam>
        private void EndProfileChangeRequest<TProfileAsset>()
            where TProfileAsset : PaletteProfileAssetBase {
            _changingProfileAssetTypes.Remove(typeof(TProfileAsset));
        }

        /// <summary>
        /// 指定した ProfileAsset の変更通知を発火する
        /// </summary>
        /// <typeparam name="TProfileAsset">通知対象の ProfileAsset 型</typeparam>
        /// <param name="profileAsset">通知対象の ProfileAsset</param>
        private void RaiseChangedProfile<TProfileAsset>(TProfileAsset profileAsset)
            where TProfileAsset : PaletteProfileAssetBase {
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
        private void UnloadLoaderOwnedProfileAsset(PaletteAssetBase paletteAsset, PaletteProfileAssetBase nextProfileAsset) {
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
        /// Included ProfileAsset 一覧から一致するアセットを取得
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="profileId">対象の Profile ID</param>
        /// <param name="profileAsset">取得できた ProfileAsset</param>
        /// <returns>取得できた場合は true</returns>
        private bool TryGetIncludedProfileAsset<TProfileAsset>(string profileId, out TProfileAsset profileAsset)
            where TProfileAsset : PaletteProfileAssetBase {
            for (var i = 0; i < _includedProfileAssets.Count; i++) {
                var includedProfileAsset = _includedProfileAssets[i];
                if (includedProfileAsset is not TProfileAsset typedProfileAsset) {
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

        /// <summary>
        /// PaletteAsset に対応する ProfileAsset 型を取得する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>対応する ProfileAsset 型。見つからない場合は null</returns>
        private Type GetProfileAssetType(PaletteAssetBase paletteAsset) {
            if (paletteAsset == null) {
                return null;
            }

            var profileAssetAttribute = Attribute.GetCustomAttribute(
                paletteAsset.GetType(),
                typeof(PaletteProfileAssetAttribute)) as PaletteProfileAssetAttribute;
            return profileAssetAttribute?.ProfileAssetType;
        }

        /// <summary>
        /// 生成時処理
        /// </summary>
        private void Awake() {
            if (RuntimeInstance != null) {
                Destroy(gameObject);
                return;
            }

            RuntimeInstance = this;
            if (_dontDestroyOnLoad) {
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// 廃棄時処理
        /// </summary>
        private void OnDestroy() {
            if (RuntimeInstance == this) {
                RuntimeInstance = null;
            }
        }
    }
}
