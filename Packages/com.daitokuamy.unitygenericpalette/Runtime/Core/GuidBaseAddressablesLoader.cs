#if USE_ADDRESSABLES
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if USE_UNI_TASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace UnityGenericPalette {
    /// <summary>
    /// ProfileAsset GUID を使って Addressables から ProfileAsset をロードするローダー
    /// </summary>
    public sealed class GuidBaseAddressablesLoader : IPaletteProfileLoader {
        private readonly Dictionary<PaletteProfileAssetBase, AsyncOperationHandle> _loadedHandles = new();

        /// <inheritdoc/>
#if USE_UNI_TASK
        public async UniTask<TProfileAsset> LoadAsync<TProfileAsset>(
            string profileId,
            string profileGuid,
            CancellationToken cancellationToken)
#else
        public async Task<TProfileAsset> LoadAsync<TProfileAsset>(
            string profileId,
            string profileGuid,
            CancellationToken cancellationToken)
#endif
            where TProfileAsset : PaletteProfileAssetBase {
            if (string.IsNullOrEmpty(profileId)) {
                throw new ArgumentException("Profile ID must not be null or empty.", nameof(profileId));
            }

            if (string.IsNullOrEmpty(profileGuid)) {
                throw new ArgumentException("Profile GUID must not be null or empty.", nameof(profileGuid));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var loadHandle = Addressables.LoadAssetAsync<TProfileAsset>(profileGuid);
            try {
#if USE_UNI_TASK
                await loadHandle.ToUniTask(cancellationToken: cancellationToken);
                var profileAsset = loadHandle.Result;
#else
                var profileAsset = await loadHandle.Task;
                cancellationToken.ThrowIfCancellationRequested();
#endif
                if (profileAsset == null) {
                    throw new InvalidOperationException(
                        $"Addressables returned null for {typeof(TProfileAsset).Name}, " +
                        $"ProfileId '{profileId}', GUID '{profileGuid}'.");
                }

                _loadedHandles[profileAsset] = loadHandle;
                return profileAsset;
            }
            catch (OperationCanceledException) {
                if (loadHandle.IsValid()) {
                    Addressables.Release(loadHandle);
                }

                throw;
            }
            catch {
                if (loadHandle.IsValid()) {
                    Addressables.Release(loadHandle);
                }

                throw;
            }
        }

        /// <inheritdoc/>
        public void Unload(string profileId, string profileGuid, PaletteProfileAssetBase profileAsset) {
            if (string.IsNullOrEmpty(profileId)) {
                throw new ArgumentException("Profile ID must not be null or empty.", nameof(profileId));
            }

            if (string.IsNullOrEmpty(profileGuid)) {
                throw new ArgumentException("Profile GUID must not be null or empty.", nameof(profileGuid));
            }

            if (profileAsset == null ||
                !_loadedHandles.TryGetValue(profileAsset, out var loadHandle) ||
                !loadHandle.IsValid()) {
                return;
            }

            Addressables.Release(loadHandle);
            _loadedHandles.Remove(profileAsset);
        }
    }
}
#endif
