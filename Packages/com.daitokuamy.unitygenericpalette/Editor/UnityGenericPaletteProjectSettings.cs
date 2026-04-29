using UnityEditor;
using UnityEngine;
namespace UnityGenericPalette.Editor {
    /// <summary>
    /// Unity Generic Palette の Project Settings を保持する
    /// </summary>
    [FilePath("ProjectSettings/UnityGenericPaletteSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class UnityGenericPaletteProjectSettings : ScriptableSingleton<UnityGenericPaletteProjectSettings> {
        [SerializeField, Tooltip("Editor 既定で使用する PaletteAssetStorage の GUID")]
        private string _paletteAssetStorageGuid;

        /// <summary>Editor 既定で使用する PaletteAssetStorage</summary>
        public PaletteAssetStorage PaletteAssetStorage {
            get {
                if (string.IsNullOrEmpty(_paletteAssetStorageGuid)) {
                    return null;
                }

                var assetPath = AssetDatabase.GUIDToAssetPath(_paletteAssetStorageGuid);
                return string.IsNullOrEmpty(assetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<PaletteAssetStorage>(assetPath);
            }
        }

        /// <summary>
        /// 既定の PaletteAssetStorage を更新する
        /// </summary>
        /// <param name="paletteAssetStorage">設定する PaletteAssetStorage</param>
        public void SetPaletteAssetStorage(PaletteAssetStorage paletteAssetStorage) {
            var assetPath = paletteAssetStorage != null
                ? AssetDatabase.GetAssetPath(paletteAssetStorage)
                : string.Empty;
            _paletteAssetStorageGuid = string.IsNullOrEmpty(assetPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(assetPath);
            Save(true);
        }
    }
}
