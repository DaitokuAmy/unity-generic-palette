#if UNITY_EDITOR
using System;
#endif

namespace UnityGenericPalette {
    /// <summary>
    /// 実行状況に応じて利用可能な ProfileContext を解決する
    /// </summary>
    public static class PaletteProfileContextResolver {
#if UNITY_EDITOR
        private static Func<IPaletteProfileContext> s_editorContextProvider;
#endif

        /// <summary>
        /// 現在利用可能な ProfileContext を取得する
        /// </summary>
        /// <param name="paletteProfileContext">取得できた ProfileContext</param>
        /// <returns>取得できた場合は true</returns>
        public static bool TryGetCurrent(out IPaletteProfileContext paletteProfileContext) {
            if (PaletteEngine.RuntimeInstance != null) {
                paletteProfileContext = PaletteEngine.RuntimeInstance;
                return true;
            }

#if UNITY_EDITOR
            if (s_editorContextProvider != null) {
                paletteProfileContext = s_editorContextProvider.Invoke();
                return paletteProfileContext != null;
            }
#endif

            paletteProfileContext = null;
            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 用 ProfileContext の解決処理を設定する
        /// </summary>
        /// <param name="editorContextProvider">Editor 用 ProfileContext の解決処理</param>
        public static void SetEditorContextProvider(Func<IPaletteProfileContext> editorContextProvider) {
            s_editorContextProvider = editorContextProvider;
        }
#endif
    }
}
