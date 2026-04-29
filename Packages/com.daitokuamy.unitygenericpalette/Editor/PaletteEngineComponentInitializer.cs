using UnityEditor;
using UnityEngine;

namespace UnityGenericPalette.Editor {
    /// <summary>
    /// PaletteEngine 追加時に Project Settings の既定値を反映する
    /// </summary>
    [InitializeOnLoad]
    internal static class PaletteEngineComponentInitializer {
        private const string PaletteAssetStoragePropertyName = "_paletteAssetStorage";

        /// <summary>
        /// 静的コンストラクタ
        /// </summary>
        static PaletteEngineComponentInitializer() {
            ObjectFactory.componentWasAdded += OnComponentWasAdded;
        }

        /// <summary>
        /// Component 追加時に PaletteEngine の初期設定を行う
        /// </summary>
        /// <param name="component">追加された Component</param>
        private static void OnComponentWasAdded(Component component) {
            if (component is not PaletteEngine paletteEngine) {
                return;
            }

            ApplyProjectSettingsPaletteAssetStorageIfNeeded(paletteEngine);
        }

        /// <summary>
        /// Project Settings の PaletteAssetStorage を必要時に反映する
        /// </summary>
        /// <param name="paletteEngine">反映対象の PaletteEngine</param>
        private static void ApplyProjectSettingsPaletteAssetStorageIfNeeded(PaletteEngine paletteEngine) {
            if (paletteEngine == null) {
                return;
            }

            var paletteAssetStorage = UnityGenericPaletteProjectSettings.instance.PaletteAssetStorage;
            if (paletteAssetStorage == null) {
                return;
            }

            var serializedObject = new SerializedObject(paletteEngine);
            serializedObject.Update();

            var paletteAssetStorageProperty = serializedObject.FindProperty(PaletteAssetStoragePropertyName);
            if (paletteAssetStorageProperty == null || paletteAssetStorageProperty.objectReferenceValue != null) {
                return;
            }

            Undo.RecordObject(paletteEngine, "Assign PaletteAssetStorage");
            paletteAssetStorageProperty.objectReferenceValue = paletteAssetStorage;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(paletteEngine);
            EditorUtility.SetDirty(paletteEngine);
        }
    }
}
