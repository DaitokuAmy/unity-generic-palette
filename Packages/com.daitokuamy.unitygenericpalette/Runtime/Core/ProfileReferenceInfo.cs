using System;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// ProfileId とロード用 GUID の対応を表すシリアライズ情報
    /// </summary>
    [Serializable]
    public sealed class ProfileReferenceInfo {
        [SerializeField, Tooltip("対応する Profile の ID")]
        private string _profileId;
        [SerializeField, Tooltip("ProfileAsset をロードするための GUID")]
        private string _assetGuid;

        /// <summary>対応する Profile の ID</summary>
        public string ProfileId => _profileId;
        /// <summary>ProfileAsset をロードするための GUID</summary>
        public string AssetGuid => _assetGuid;
    }
}
