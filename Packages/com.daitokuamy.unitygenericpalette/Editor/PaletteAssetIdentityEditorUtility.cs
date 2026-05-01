using System;
using UnityEditor;
using UnityEngine;

namespace UnityGenericPalette.Editor {
    /// <summary>
    /// PaletteAsset と ProfileAsset の識別子同期を扱う
    /// </summary>
    [InitializeOnLoad]
    internal static class PaletteAssetIdentityEditorUtility {
        private const string PaletteGuidPropertyName = "_paletteGuid";
        private const string PaletteLocalFileIdPropertyName = "_paletteLocalFileId";

        private static bool s_isSynchronizationScheduled;

        /// <summary>
        /// 静的コンストラクタ
        /// </summary>
        static PaletteAssetIdentityEditorUtility() {
            EditorApplication.projectChanged += RequestSynchronizeAllAssetIdentities;
            RequestSynchronizeAllAssetIdentities();
        }

        /// <summary>
        /// すべての識別子同期を予約する
        /// </summary>
        internal static void RequestSynchronizeAllAssetIdentities() {
            if (s_isSynchronizationScheduled) {
                return;
            }

            s_isSynchronizationScheduled = true;
            EditorApplication.delayCall += SynchronizeAllAssetIdentities;
        }

        /// <summary>
        /// PaletteAsset に識別子を設定する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>変更があった場合は true</returns>
        internal static bool EnsurePaletteAssetGuid(PaletteAssetBase paletteAsset) {
            return TryGetPaletteIdentity(paletteAsset, out var paletteGuid, out var paletteLocalFileId) &&
                ApplyPaletteIdentity(new SerializedObject(paletteAsset), paletteGuid, paletteLocalFileId);
        }

        /// <summary>
        /// ProfileAsset に Palette 識別子を同期する
        /// </summary>
        /// <param name="profileAsset">対象の ProfileAsset</param>
        /// <returns>変更があった場合は true</returns>
        internal static bool SynchronizeProfileAssetPaletteGuid(PaletteProfileAssetBase profileAsset) {
            if (profileAsset == null || !TryGetPaletteAsset(profileAsset, out var paletteAsset)) {
                return false;
            }

            return ApplyPaletteIdentity(
                new SerializedObject(profileAsset),
                paletteAsset.PaletteGuid,
                paletteAsset.PaletteLocalFileId);
        }

        /// <summary>
        /// ProfileAsset に対応する PaletteAsset を取得する
        /// </summary>
        /// <param name="profileAsset">対象の ProfileAsset</param>
        /// <param name="paletteAsset">取得できた PaletteAsset</param>
        /// <returns>取得できた場合は true</returns>
        internal static bool TryGetPaletteAsset(PaletteProfileAssetBase profileAsset, out PaletteAssetBase paletteAsset) {
            paletteAsset = profileAsset == null
                ? null
                : LoadPaletteAsset(profileAsset.PaletteGuid, profileAsset.PaletteLocalFileId) ??
                    LoadPaletteAsset(profileAsset.PaletteGuid, profileAsset.GetType());
            return paletteAsset != null;
        }

        /// <summary>
        /// ProfileAsset が指定した PaletteAsset に属するか判定する
        /// </summary>
        /// <param name="profileAsset">対象の ProfileAsset</param>
        /// <param name="paletteAsset">比較対象の PaletteAsset</param>
        /// <returns>属する場合は true</returns>
        internal static bool IsAssignedToPalette(PaletteProfileAssetBase profileAsset, PaletteAssetBase paletteAsset) {
            return profileAsset != null &&
                paletteAsset != null &&
                !string.IsNullOrEmpty(profileAsset.PaletteGuid) &&
                paletteAsset.HasPaletteIdentity(profileAsset.PaletteGuid, profileAsset.PaletteLocalFileId);
        }

        /// <summary>
        /// すべての PaletteAsset / ProfileAsset の識別子を同期する
        /// </summary>
        private static void SynchronizeAllAssetIdentities() {
            s_isSynchronizationScheduled = false;

            var isChanged = false;
            isChanged |= SynchronizeAssetType<PaletteAssetBase>(EnsurePaletteAssetGuid);
            isChanged |= SynchronizeAssetType<PaletteProfileAssetBase>(SynchronizeProfileAssetPaletteGuid);

            if (isChanged) {
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// 指定した型のアセット一式を同期する
        /// </summary>
        /// <typeparam name="TAsset">同期対象のアセット型</typeparam>
        /// <param name="synchronize">同期処理</param>
        /// <returns>変更があった場合は true</returns>
        private static bool SynchronizeAssetType<TAsset>(Func<TAsset, bool> synchronize)
            where TAsset : UnityEngine.Object {
            var isChanged = false;
            var assetTypes = TypeCache.GetTypesDerivedFrom<TAsset>();
            for (var i = 0; i < assetTypes.Count; i++) {
                var guids = AssetDatabase.FindAssets($"t:{assetTypes[i].Name}");
                for (var guidIndex = 0; guidIndex < guids.Length; guidIndex++) {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    for (var assetIndex = 0; assetIndex < assets.Length; assetIndex++) {
                        if (assets[assetIndex] is TAsset typedAsset && synchronize(typedAsset)) {
                            isChanged = true;
                        }
                    }
                }
            }

            return isChanged;
        }

        /// <summary>
        /// シリアライズオブジェクトへ Palette 識別子を反映する
        /// </summary>
        /// <param name="serializedObject">反映対象</param>
        /// <param name="paletteGuid">PaletteGuid</param>
        /// <param name="paletteLocalFileId">PaletteLocalFileId</param>
        /// <returns>変更があった場合は true</returns>
        private static bool ApplyPaletteIdentity(
            SerializedObject serializedObject,
            string paletteGuid,
            long paletteLocalFileId) {
            var paletteGuidProperty = serializedObject.FindProperty(PaletteGuidPropertyName);
            var paletteLocalFileIdProperty = serializedObject.FindProperty(PaletteLocalFileIdPropertyName);
            if (paletteGuidProperty == null || paletteLocalFileIdProperty == null) {
                return false;
            }

            if (paletteGuidProperty.stringValue == paletteGuid &&
                paletteLocalFileIdProperty.longValue == paletteLocalFileId) {
                return false;
            }

            serializedObject.Update();
            paletteGuidProperty.stringValue = paletteGuid;
            paletteLocalFileIdProperty.longValue = paletteLocalFileId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(serializedObject.targetObject);
            return true;
        }

        /// <summary>
        /// PaletteAsset から識別子を取得する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="paletteGuid">取得できた PaletteGuid</param>
        /// <param name="paletteLocalFileId">取得できた PaletteLocalFileId</param>
        /// <returns>取得できた場合は true</returns>
        private static bool TryGetPaletteIdentity(
            PaletteAssetBase paletteAsset,
            out string paletteGuid,
            out long paletteLocalFileId) {
            paletteGuid = null;
            paletteLocalFileId = 0;
            return paletteAsset != null &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    paletteAsset,
                    out paletteGuid,
                    out paletteLocalFileId) &&
                !string.IsNullOrEmpty(paletteGuid) &&
                paletteLocalFileId != 0;
        }

        /// <summary>
        /// 識別子から PaletteAsset を読み込む
        /// </summary>
        /// <param name="paletteGuid">読み込み対象の PaletteGuid</param>
        /// <param name="paletteLocalFileId">読み込み対象の PaletteLocalFileId</param>
        /// <returns>読み込めた PaletteAsset。見つからない場合は null</returns>
        private static PaletteAssetBase LoadPaletteAsset(string paletteGuid, long paletteLocalFileId) {
            if (string.IsNullOrEmpty(paletteGuid) || paletteLocalFileId == 0) {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(paletteGuid);
            if (string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < assets.Length; i++) {
                if (assets[i] is not PaletteAssetBase paletteAsset) {
                    continue;
                }

                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        paletteAsset,
                        out _,
                        out var localFileId) &&
                    localFileId == paletteLocalFileId) {
                    return paletteAsset;
                }
            }

            return null;
        }

        /// <summary>
        /// ProfileAsset 型に対応する PaletteAsset を読み込む
        /// </summary>
        /// <param name="paletteGuid">読み込み対象の PaletteGuid</param>
        /// <param name="profileAssetType">対応する ProfileAsset 型</param>
        /// <returns>読み込めた PaletteAsset。見つからない場合は null</returns>
        private static PaletteAssetBase LoadPaletteAsset(string paletteGuid, Type profileAssetType) {
            if (string.IsNullOrEmpty(paletteGuid) || profileAssetType == null) {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(paletteGuid);
            if (string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            PaletteAssetBase matchedPaletteAsset = null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < assets.Length; i++) {
                if (assets[i] is not PaletteAssetBase paletteAsset) {
                    continue;
                }

                var profileAssetAttribute = Attribute.GetCustomAttribute(
                    paletteAsset.GetType(),
                    typeof(PaletteProfileAssetAttribute)) as PaletteProfileAssetAttribute;
                if (profileAssetAttribute?.ProfileAssetType != profileAssetType) {
                    continue;
                }

                if (matchedPaletteAsset != null) {
                    return null;
                }

                matchedPaletteAsset = paletteAsset;
            }

            return matchedPaletteAsset;
        }
    }
}
