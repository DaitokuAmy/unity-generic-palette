using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityGenericPalette.Editor {
    /// <summary>
    /// PaletteAsset が保持する Profile 参照情報の同期と検証を扱う
    /// </summary>
    [InitializeOnLoad]
    internal static class PaletteProfileReferenceEditorUtility {
        private const string ProfileReferencesPropertyName = "_profileReferences";
        private const string ProfileIdPropertyName = "_profileId";
        private const string AssetGuidPropertyName = "_assetGuid";

        private static bool s_isSynchronizationScheduled;

        /// <summary>
        /// 静的コンストラクタ
        /// </summary>
        static PaletteProfileReferenceEditorUtility() {
            EditorApplication.projectChanged += OnProjectChanged;
            RequestSynchronizeAllPaletteAssetProfileReferences();
        }

        /// <summary>
        /// 単一 PaletteAsset の Profile 参照情報を同期する
        /// </summary>
        /// <param name="paletteAsset">同期対象の PaletteAsset</param>
        /// <param name="saveAssets">同期後に保存するか</param>
        /// <param name="logIssues">検出した不整合をログ出力するか</param>
        internal static void SynchronizePaletteAssetProfileReferences(
            PaletteAssetBase paletteAsset,
            bool saveAssets = true,
            bool logIssues = true) {
            if (!TryBuildExpectedProfileReferences(paletteAsset, out var expectedReferences, out var issues)) {
                return;
            }

            if (!ApplyProfileReferencesIfNeeded(paletteAsset, expectedReferences)) {
                if (logIssues) {
                    LogIssuesIfNeeded(paletteAsset, issues);
                }
                return;
            }

            EditorUtility.SetDirty(paletteAsset);
            if (saveAssets) {
                AssetDatabase.SaveAssets();
            }

            if (logIssues) {
                LogIssuesIfNeeded(paletteAsset, issues);
            }
        }

        /// <summary>
        /// すべての PaletteAsset の Profile 参照情報同期を予約する
        /// </summary>
        internal static void RequestSynchronizeAllPaletteAssetProfileReferences() {
            if (s_isSynchronizationScheduled) {
                return;
            }

            s_isSynchronizationScheduled = true;
            EditorApplication.delayCall += SynchronizeAllPaletteAssetProfileReferences;
        }

        /// <summary>
        /// Project 変更時に同期予約する
        /// </summary>
        private static void OnProjectChanged() {
            RequestSynchronizeAllPaletteAssetProfileReferences();
        }

        /// <summary>
        /// すべての PaletteAsset の Profile 参照情報を同期する
        /// </summary>
        private static void SynchronizeAllPaletteAssetProfileReferences() {
            s_isSynchronizationScheduled = false;

            var paletteAssets = CollectPaletteAssets();
            var changed = false;
            for (var i = 0; i < paletteAssets.Count; i++) {
                var paletteAsset = paletteAssets[i];
                if (!TryBuildExpectedProfileReferences(paletteAsset, out var expectedReferences, out var issues)) {
                    continue;
                }

                if (ApplyProfileReferencesIfNeeded(paletteAsset, expectedReferences)) {
                    changed = true;
                    EditorUtility.SetDirty(paletteAsset);
                }

                LogIssuesIfNeeded(paletteAsset, issues);
            }

            if (changed) {
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// 同期対象の PaletteAsset 一覧を収集する
        /// </summary>
        /// <returns>PaletteAsset 一覧</returns>
        private static List<PaletteAssetBase> CollectPaletteAssets() {
            var paletteAssets = new List<PaletteAssetBase>();
            var knownPaletteAssets = new HashSet<PaletteAssetBase>();
            var storageGuids = AssetDatabase.FindAssets($"t:{nameof(PaletteAssetStorage)}");
            for (var i = 0; i < storageGuids.Length; i++) {
                var storageAssetPath = AssetDatabase.GUIDToAssetPath(storageGuids[i]);
                var paletteAssetStorage = AssetDatabase.LoadAssetAtPath<PaletteAssetStorage>(storageAssetPath);
                if (paletteAssetStorage == null) {
                    continue;
                }

                for (var paletteIndex = 0; paletteIndex < paletteAssetStorage.PaletteAssets.Count; paletteIndex++) {
                    var paletteAsset = paletteAssetStorage.PaletteAssets[paletteIndex];
                    if (paletteAsset == null || !knownPaletteAssets.Add(paletteAsset)) {
                        continue;
                    }

                    paletteAssets.Add(paletteAsset);
                }
            }

            return paletteAssets;
        }

        /// <summary>
        /// PaletteAsset に対して期待される Profile 参照一覧を構築する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="expectedReferences">期待される参照一覧</param>
        /// <param name="issues">検出した不整合一覧</param>
        /// <returns>構築できた場合は true</returns>
        private static bool TryBuildExpectedProfileReferences(
            PaletteAssetBase paletteAsset,
            out List<ProfileReferenceRecord> expectedReferences,
            out List<string> issues) {
            expectedReferences = new List<ProfileReferenceRecord>();
            issues = new List<string>();
            if (paletteAsset == null) {
                return false;
            }

            var profileAssetType = GetProfileAssetType(paletteAsset);
            if (profileAssetType == null) {
                return false;
            }

            var currentReferences = BuildCurrentProfileReferenceMap(paletteAsset, issues);
            var discoveredReferences = BuildDiscoveredProfileReferenceMap(paletteAsset, profileAssetType, issues);
            foreach (var discoveredReference in discoveredReferences.Values) {
                expectedReferences.Add(discoveredReference);
            }

            expectedReferences.Sort(ProfileReferenceRecord.Compare);
            DetectReferenceDifferences(currentReferences, discoveredReferences, issues);
            ValidateDefaultProfileReference(paletteAsset, discoveredReferences, issues);
            return true;
        }

        /// <summary>
        /// 現在シリアライズされている Profile 参照一覧を辞書化する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="issues">検出した不整合一覧</param>
        /// <returns>ProfileId をキーにした辞書</returns>
        private static Dictionary<string, ProfileReferenceRecord> BuildCurrentProfileReferenceMap(
            PaletteAssetBase paletteAsset,
            List<string> issues) {
            var currentReferences = new Dictionary<string, ProfileReferenceRecord>(StringComparer.Ordinal);
            for (var i = 0; i < paletteAsset.ProfileReferences.Count; i++) {
                var profileReference = paletteAsset.ProfileReferences[i];
                if (profileReference == null) {
                    issues.Add("Removed an empty serialized profile reference entry.");
                    continue;
                }

                if (string.IsNullOrEmpty(profileReference.ProfileId)) {
                    issues.Add("Removed a profile reference entry with an empty ProfileId.");
                    continue;
                }

                if (string.IsNullOrEmpty(profileReference.AssetGuid)) {
                    issues.Add($"Removed the '{profileReference.ProfileId}' reference because its GUID was empty.");
                    continue;
                }

                var currentReference = new ProfileReferenceRecord(profileReference.ProfileId, profileReference.AssetGuid);
                if (!currentReferences.TryAdd(currentReference.ProfileId, currentReference)) {
                    issues.Add($"Removed duplicated serialized profile references for '{currentReference.ProfileId}'.");
                }
            }

            return currentReferences;
        }

        /// <summary>
        /// 実在する ProfileAsset 一覧から期待される Profile 参照辞書を構築する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="profileAssetType">対応する ProfileAsset 型</param>
        /// <param name="issues">検出した不整合一覧</param>
        /// <returns>ProfileId をキーにした辞書</returns>
        private static Dictionary<string, ProfileReferenceRecord> BuildDiscoveredProfileReferenceMap(
            PaletteAssetBase paletteAsset,
            Type profileAssetType,
            List<string> issues) {
            var discoveredReferences = new Dictionary<string, ProfileReferenceRecord>(StringComparer.Ordinal);
            var profileAssetGuids = AssetDatabase.FindAssets($"t:{profileAssetType.Name}");
            for (var i = 0; i < profileAssetGuids.Length; i++) {
                var profileAssetPath = AssetDatabase.GUIDToAssetPath(profileAssetGuids[i]);
                var profileAsset = AssetDatabase.LoadAssetAtPath(profileAssetPath, profileAssetType) as PaletteProfileAssetBase;
                if (profileAsset == null || profileAsset.PaletteAssetBase != paletteAsset) {
                    continue;
                }

                if (string.IsNullOrEmpty(profileAsset.ProfileId)) {
                    issues.Add($"Skipped '{profileAsset.name}' because ProfileId was empty.");
                    continue;
                }

                var discoveredReference = new ProfileReferenceRecord(profileAsset.ProfileId, profileAssetGuids[i]);
                if (!discoveredReferences.TryAdd(discoveredReference.ProfileId, discoveredReference)) {
                    issues.Add(
                        $"Detected multiple ProfileAssets with ProfileId '{discoveredReference.ProfileId}'. " +
                        "Only the first asset was kept in the reference table.");
                }
            }

            return discoveredReferences;
        }

        /// <summary>
        /// 現在の参照一覧と期待される参照一覧との差分を検出する
        /// </summary>
        /// <param name="currentReferences">現在の参照一覧</param>
        /// <param name="expectedReferences">期待される参照一覧</param>
        /// <param name="issues">検出した不整合一覧</param>
        private static void DetectReferenceDifferences(
            Dictionary<string, ProfileReferenceRecord> currentReferences,
            Dictionary<string, ProfileReferenceRecord> expectedReferences,
            List<string> issues) {
            foreach (var expectedReference in expectedReferences) {
                if (!currentReferences.TryGetValue(expectedReference.Key, out var currentReference)) {
                    issues.Add($"Added a missing reference for ProfileId '{expectedReference.Key}'.");
                    continue;
                }

                if (currentReference.AssetGuid != expectedReference.Value.AssetGuid) {
                    issues.Add(
                        $"Updated the GUID for ProfileId '{expectedReference.Key}' from '{currentReference.AssetGuid}' " +
                        $"to '{expectedReference.Value.AssetGuid}'.");
                }
            }

            foreach (var currentReference in currentReferences) {
                if (!expectedReferences.ContainsKey(currentReference.Key)) {
                    issues.Add($"Removed the stale reference for ProfileId '{currentReference.Key}'.");
                }
            }
        }

        /// <summary>
        /// 既定 ProfileId が実在する ProfileAsset を参照しているか検証する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="expectedReferences">期待される参照一覧</param>
        /// <param name="issues">検出した不整合一覧</param>
        private static void ValidateDefaultProfileReference(
            PaletteAssetBase paletteAsset,
            Dictionary<string, ProfileReferenceRecord> expectedReferences,
            List<string> issues) {
            if (paletteAsset == null ||
                string.IsNullOrEmpty(paletteAsset.DefaultProfileId) ||
                expectedReferences.ContainsKey(paletteAsset.DefaultProfileId)) {
                return;
            }

            issues.Add(
                $"DefaultProfileId '{paletteAsset.DefaultProfileId}' does not have a matching ProfileAsset.");
        }

        /// <summary>
        /// 期待される Profile 参照一覧を PaletteAsset へ反映する
        /// </summary>
        /// <param name="paletteAsset">反映対象の PaletteAsset</param>
        /// <param name="expectedReferences">期待される参照一覧</param>
        /// <returns>変更した場合は true</returns>
        private static bool ApplyProfileReferencesIfNeeded(
            PaletteAssetBase paletteAsset,
            List<ProfileReferenceRecord> expectedReferences) {
            if (!HasProfileReferenceDifferences(paletteAsset.ProfileReferences, expectedReferences)) {
                return false;
            }

            var serializedObject = new SerializedObject(paletteAsset);
            var profileReferencesProperty = serializedObject.FindProperty(ProfileReferencesPropertyName);
            if (profileReferencesProperty == null || !profileReferencesProperty.isArray) {
                return false;
            }

            serializedObject.Update();
            profileReferencesProperty.arraySize = expectedReferences.Count;
            for (var i = 0; i < expectedReferences.Count; i++) {
                var profileReferenceProperty = profileReferencesProperty.GetArrayElementAtIndex(i);
                profileReferenceProperty.FindPropertyRelative(ProfileIdPropertyName).stringValue = expectedReferences[i].ProfileId;
                profileReferenceProperty.FindPropertyRelative(AssetGuidPropertyName).stringValue = expectedReferences[i].AssetGuid;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            paletteAsset.InvalidateProfileReferenceCache();
            return true;
        }

        /// <summary>
        /// 現在のシリアライズ内容と期待される参照一覧に差分があるか判定する
        /// </summary>
        /// <param name="currentReferences">現在のシリアライズ内容</param>
        /// <param name="expectedReferences">期待される参照一覧</param>
        /// <returns>差分がある場合は true</returns>
        private static bool HasProfileReferenceDifferences(
            IReadOnlyList<ProfileReferenceInfo> currentReferences,
            IReadOnlyList<ProfileReferenceRecord> expectedReferences) {
            if (currentReferences.Count != expectedReferences.Count) {
                return true;
            }

            for (var i = 0; i < currentReferences.Count; i++) {
                var currentReference = currentReferences[i];
                var expectedReference = expectedReferences[i];
                if (currentReference == null ||
                    currentReference.ProfileId != expectedReference.ProfileId ||
                    currentReference.AssetGuid != expectedReference.AssetGuid) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// PaletteAsset に定義された ProfileAsset 型を取得する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <returns>対応する ProfileAsset 型</returns>
        private static Type GetProfileAssetType(PaletteAssetBase paletteAsset) {
            if (paletteAsset == null) {
                return null;
            }

            var profileAssetAttribute = Attribute.GetCustomAttribute(
                paletteAsset.GetType(),
                typeof(PaletteProfileAssetAttribute)) as PaletteProfileAssetAttribute;
            return profileAssetAttribute?.ProfileAssetType;
        }

        /// <summary>
        /// 同期中に検出した不整合をログ出力する
        /// </summary>
        /// <param name="paletteAsset">対象の PaletteAsset</param>
        /// <param name="issues">検出した不整合一覧</param>
        private static void LogIssuesIfNeeded(PaletteAssetBase paletteAsset, List<string> issues) {
            if (paletteAsset == null || issues == null || issues.Count == 0) {
                return;
            }

            Debug.LogWarning(
                $"Synchronized profile references for {paletteAsset.GetType().Name}:\n- {string.Join("\n- ", issues)}",
                paletteAsset);
        }

        /// <summary>
        /// 同期対象の Profile 参照情報を表す一時レコード
        /// </summary>
        private struct ProfileReferenceRecord {
            /// <summary>
            /// 一時レコードを生成する
            /// </summary>
            /// <param name="profileId">ProfileId</param>
            /// <param name="assetGuid">AssetGuid</param>
            public ProfileReferenceRecord(string profileId, string assetGuid) {
                ProfileId = profileId;
                AssetGuid = assetGuid;
            }

            /// <summary>ProfileId</summary>
            public string ProfileId { get; }
            /// <summary>AssetGuid</summary>
            public string AssetGuid { get; }

            /// <summary>
            /// 並び順を比較する
            /// </summary>
            /// <param name="left">左側のレコード</param>
            /// <param name="right">右側のレコード</param>
            /// <returns>比較結果</returns>
            public static int Compare(ProfileReferenceRecord left, ProfileReferenceRecord right) {
                return string.Compare(left.ProfileId, right.ProfileId, StringComparison.Ordinal);
            }
        }
    }
}
