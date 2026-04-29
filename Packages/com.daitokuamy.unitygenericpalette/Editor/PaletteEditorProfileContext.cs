using System;
using System.Collections.Generic;
using UnityEditor;

namespace UnityGenericPalette.Editor {
    /// <summary>
    /// Edit Mode 用の Profile 状態を管理し、再反映通知を行うコンテキスト
    /// </summary>
    [InitializeOnLoad]
    internal sealed class PaletteEditorProfileContext : IPaletteProfileContext {
        private static readonly PaletteEditorProfileContext InstanceValue = new();
        private readonly Dictionary<PaletteAssetBase, PaletteProfileAssetBase> _currentProfileAssets = new();
        private readonly Dictionary<Type, List<Delegate>> _changedProfileHandlers = new();

        /// <summary>共有インスタンス</summary>
        internal static PaletteEditorProfileContext Instance => InstanceValue;

        /// <summary>
        /// 静的コンストラクタ
        /// </summary>
        static PaletteEditorProfileContext() {
            PaletteProfileContextResolver.SetEditorContextProvider(GetCurrentContext);
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        /// <inheritdoc/>
        void IPaletteProfileContext.SubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction) {
            SubscribeChangedProfile(changedProfileAction);
        }

        /// <inheritdoc/>
        void IPaletteProfileContext.UnsubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction) {
            UnsubscribeChangedProfile(changedProfileAction);
        }

        /// <inheritdoc/>
        bool IPaletteProfileContext.TryGetCurrentProfileAsset<TProfileAsset>(out TProfileAsset profileAsset) {
            return TryGetCurrentProfileAsset(out profileAsset);
        }

        /// <inheritdoc/>
        bool IPaletteProfileContext.TryGetCurrentProfileId<TProfileAsset>(out string profileId) {
            return TryGetCurrentProfileId<TProfileAsset>(out profileId);
        }

        /// <summary>
        /// Preview 中の ProfileAsset を設定する
        /// </summary>
        /// <param name="profileAsset">設定する ProfileAsset</param>
        internal void SetCurrentProfile(PaletteProfileAssetBase profileAsset) {
            SetCurrentProfile(profileAsset, true);
        }

        /// <summary>
        /// Preview 中の ProfileAsset を設定する
        /// </summary>
        /// <param name="profileAsset">設定する ProfileAsset</param>
        /// <param name="notifyChangedProfile">変更通知を発火するか</param>
        internal void SetCurrentProfile(PaletteProfileAssetBase profileAsset, bool notifyChangedProfile) {
            if (profileAsset == null) {
                throw new ArgumentNullException(nameof(profileAsset));
            }

            var paletteAsset = profileAsset.PaletteAssetBase;
            if (paletteAsset == null) {
                throw new InvalidOperationException($"{profileAsset.GetType().Name} has no PaletteAsset reference.");
            }

            _currentProfileAssets[paletteAsset] = profileAsset;
            if (notifyChangedProfile) {
                RaiseChangedProfile(profileAsset);
            }
        }

        /// <summary>
        /// Preview 中の ProfileAsset 変更を通知する
        /// </summary>
        /// <param name="profileAsset">通知対象の ProfileAsset</param>
        internal void NotifyProfileChanged(PaletteProfileAssetBase profileAsset) {
            if (profileAsset == null) {
                return;
            }

            var paletteAsset = profileAsset.PaletteAssetBase;
            if (paletteAsset == null) {
                return;
            }

            if (_currentProfileAssets.TryGetValue(paletteAsset, out var currentProfileAsset)) {
                if (!ReferenceEquals(currentProfileAsset, profileAsset)) {
                    return;
                }
            }
            else if (paletteAsset.DefaultProfileId != profileAsset.ProfileId) {
                return;
            }

            RaiseChangedProfile(profileAsset);
        }

        /// <summary>
        /// Preview 中の ProfileAsset を解除する
        /// </summary>
        /// <param name="paletteAsset">解除対象の PaletteAsset</param>
        internal void ClearCurrentProfile(PaletteAssetBase paletteAsset) {
            if (paletteAsset == null) {
                return;
            }

            _currentProfileAssets.Remove(paletteAsset);
        }

        /// <summary>
        /// 指定した ProfileAsset が current と一致する場合だけ解除する
        /// </summary>
        /// <param name="profileAsset">解除対象の ProfileAsset</param>
        internal void ClearCurrentProfileIfMatched(PaletteProfileAssetBase profileAsset) {
            if (profileAsset == null) {
                return;
            }

            var paletteAsset = profileAsset.PaletteAssetBase;
            if (paletteAsset == null ||
                !_currentProfileAssets.TryGetValue(paletteAsset, out var currentProfileAsset) ||
                !ReferenceEquals(currentProfileAsset, profileAsset)) {
                return;
            }

            _currentProfileAssets.Remove(paletteAsset);
        }

        /// <summary>
        /// 指定した ProfileAsset 型の変更通知を購読する
        /// </summary>
        /// <typeparam name="TProfileAsset">購読対象の ProfileAsset 型</typeparam>
        /// <param name="changedProfileAction">通知先ハンドラー</param>
        private void SubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction)
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
        private void UnsubscribeChangedProfile<TProfileAsset>(Action<TProfileAsset> changedProfileAction)
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
        /// 指定した ProfileAsset 型に対応する現在の ProfileAsset を取得する
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="profileAsset">取得できた ProfileAsset</param>
        /// <returns>取得できた場合は true</returns>
        private bool TryGetCurrentProfileAsset<TProfileAsset>(out TProfileAsset profileAsset)
            where TProfileAsset : PaletteProfileAssetBase {
            foreach (var currentProfileAsset in _currentProfileAssets.Values) {
                if (currentProfileAsset is TProfileAsset typedProfileAsset) {
                    profileAsset = typedProfileAsset;
                    return true;
                }
            }

            if (TryGetDefaultProfileAsset(out profileAsset)) {
                return true;
            }

            profileAsset = null;
            return false;
        }

        /// <summary>
        /// 指定した ProfileAsset 型に対応する現在の ProfileId を取得する
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="profileId">取得できた ProfileId</param>
        /// <returns>取得できた場合は true</returns>
        private bool TryGetCurrentProfileId<TProfileAsset>(out string profileId)
            where TProfileAsset : PaletteProfileAssetBase {
            if (TryGetCurrentProfileAsset(out TProfileAsset profileAsset)) {
                profileId = profileAsset.ProfileId;
                return !string.IsNullOrEmpty(profileId);
            }

            profileId = null;
            return false;
        }

        /// <summary>
        /// 現在利用可能な Edit Mode 用コンテキストを取得する
        /// </summary>
        /// <returns>利用可能な Edit Mode 用コンテキスト。利用できない場合は null</returns>
        private static IPaletteProfileContext GetCurrentContext() {
            return EditorApplication.isPlaying ? null : InstanceValue;
        }

        /// <summary>
        /// Undo / Redo 後に current profile の再反映を通知する
        /// </summary>
        private static void OnUndoRedoPerformed() {
            InstanceValue.NotifyCurrentProfilesChanged();
        }

        /// <summary>
        /// 指定した ProfileAsset の変更通知を発火する
        /// </summary>
        /// <param name="profileAsset">通知対象の ProfileAsset</param>
        private void RaiseChangedProfile(PaletteProfileAssetBase profileAsset) {
            if (profileAsset == null ||
                !_changedProfileHandlers.TryGetValue(profileAsset.GetType(), out var changedProfileHandlers)) {
                return;
            }

            for (var i = 0; i < changedProfileHandlers.Count; i++) {
                changedProfileHandlers[i].DynamicInvoke(profileAsset);
            }
        }

        /// <summary>
        /// 現在 preview 中の ProfileAsset すべての変更通知を再発火する
        /// </summary>
        private void NotifyCurrentProfilesChanged() {
            if (_currentProfileAssets.Count == 0) {
                return;
            }

            foreach (var currentProfileAsset in _currentProfileAssets.Values) {
                if (currentProfileAsset == null) {
                    continue;
                }

                currentProfileAsset.InvalidateCache();
                RaiseChangedProfile(currentProfileAsset);
            }
        }

        /// <summary>
        /// 指定した型に対応する既定 ProfileAsset を取得する
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="profileAsset">取得できた ProfileAsset</param>
        /// <returns>取得できた場合は true</returns>
        private bool TryGetDefaultProfileAsset<TProfileAsset>(out TProfileAsset profileAsset)
            where TProfileAsset : PaletteProfileAssetBase {
            var paletteAssetStorage = UnityGenericPaletteProjectSettings.instance.PaletteAssetStorage;
            if (paletteAssetStorage == null) {
                profileAsset = null;
                return false;
            }

            for (var i = 0; i < paletteAssetStorage.PaletteAssets.Count; i++) {
                var paletteAsset = paletteAssetStorage.PaletteAssets[i];
                if (paletteAsset == null || string.IsNullOrEmpty(paletteAsset.DefaultProfileId)) {
                    continue;
                }

                if (TryResolveProfileAsset(paletteAsset, paletteAsset.DefaultProfileId, out profileAsset)) {
                    return true;
                }
            }

            profileAsset = null;
            return false;
        }

        /// <summary>
        /// PaletteAsset と ProfileId から ProfileAsset を解決する
        /// </summary>
        /// <typeparam name="TProfileAsset">取得対象の ProfileAsset 型</typeparam>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="profileId">対象の ProfileId</param>
        /// <param name="profileAsset">取得できた ProfileAsset</param>
        /// <returns>取得できた場合は true</returns>
        private bool TryResolveProfileAsset<TProfileAsset>(PaletteAssetBase paletteAsset, string profileId, out TProfileAsset profileAsset)
            where TProfileAsset : PaletteProfileAssetBase {
            if (paletteAsset == null || string.IsNullOrEmpty(profileId)) {
                profileAsset = null;
                return false;
            }

            if (paletteAsset.TryGetProfileAssetGuid(profileId, out var profileGuid)) {
                var profileAssetPath = AssetDatabase.GUIDToAssetPath(profileGuid);
                var referencedProfileAsset = AssetDatabase.LoadAssetAtPath<TProfileAsset>(profileAssetPath);
                if (referencedProfileAsset != null &&
                    referencedProfileAsset.PaletteAssetBase == paletteAsset &&
                    referencedProfileAsset.ProfileId == profileId) {
                    profileAsset = referencedProfileAsset;
                    return true;
                }
            }

            var guids = AssetDatabase.FindAssets($"t:{typeof(TProfileAsset).Name}");
            for (var i = 0; i < guids.Length; i++) {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var candidateProfileAsset = AssetDatabase.LoadAssetAtPath<TProfileAsset>(assetPath);
                if (candidateProfileAsset == null ||
                    candidateProfileAsset.PaletteAssetBase != paletteAsset ||
                    candidateProfileAsset.ProfileId != profileId) {
                    continue;
                }

                profileAsset = candidateProfileAsset;
                return true;
            }

            profileAsset = null;
            return false;
        }
    }
}
