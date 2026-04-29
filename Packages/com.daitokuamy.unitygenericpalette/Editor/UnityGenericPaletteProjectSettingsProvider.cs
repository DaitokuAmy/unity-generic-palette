using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// Unity Generic Palette の Project Settings UI を提供する
    /// </summary>
    public static class UnityGenericPaletteProjectSettingsProvider {
        /// <summary>
        /// Project Settings Provider を生成する
        /// </summary>
        /// <returns>生成した SettingsProvider</returns>
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() {
            return new SettingsProvider("Project/Unity Generic Palette", SettingsScope.Project) {
                label = "Unity Generic Palette",
                guiHandler = DrawSettingsGui,
                keywords = new HashSet<string> {
                    "Unity Generic Palette",
                    "Palette",
                    "Storage",
                },
            };
        }

        /// <summary>
        /// Project Settings の GUI を描画する
        /// </summary>
        /// <param name="searchContext">検索文字列</param>
        private static void DrawSettingsGui(string searchContext) {
            var projectSettings = UnityGenericPaletteProjectSettings.instance;

            EditorGUI.BeginChangeCheck();
            var paletteAssetStorage = (PaletteAssetStorage)EditorGUILayout.ObjectField(
                new GUIContent("Palette Asset Storage"),
                projectSettings.PaletteAssetStorage,
                typeof(PaletteAssetStorage),
                false);
            if (EditorGUI.EndChangeCheck()) {
                projectSettings.SetPaletteAssetStorage(paletteAssetStorage);
            }

            if (projectSettings.PaletteAssetStorage == null) {
                EditorGUILayout.HelpBox("PaletteEditorWindow で既定利用する PaletteAssetStorage を設定できます。", MessageType.Info);

                if (GUILayout.Button("Create Storage")) {
                    CreatePaletteAssetStorage(projectSettings);
                }
            }
        }

        /// <summary>
        /// PaletteAssetStorage を作成して Project Settings へ設定する
        /// </summary>
        /// <param name="projectSettings">設定対象の Project Settings</param>
        private static void CreatePaletteAssetStorage(UnityGenericPaletteProjectSettings projectSettings) {
            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Create PaletteAssetStorage",
                "PaletteAssetStorage",
                "asset",
                "Select a location for the PaletteAssetStorage asset.");
            if (string.IsNullOrEmpty(assetPath)) {
                return;
            }

            var paletteAssetStorage = ScriptableObject.CreateInstance<PaletteAssetStorage>();
            AssetDatabase.CreateAsset(paletteAssetStorage, assetPath);
            AssetDatabase.SaveAssets();
            projectSettings.SetPaletteAssetStorage(paletteAssetStorage);
        }
    }
}
