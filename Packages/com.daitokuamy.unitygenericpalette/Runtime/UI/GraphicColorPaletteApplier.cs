using UnityEngine;
using UnityEngine.UI;

namespace UnityGenericPalette {
    /// <summary>
    /// Color Palette の値を uGUI の Graphic.color へ反映する Applier
    /// </summary>
    [AddComponentMenu("Unity Generic Palette/Graphic Color Palette Applier")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class GraphicColorPaletteApplier : PaletteApplierBase<ColorPaletteAsset, ColorPaletteProfileAsset, Color> {
        [SerializeField, Tooltip("色を反映する Graphic コンポーネント")]
        private Graphic _targetGraphic;

        /// <summary>
        /// 解決済みの Color を Graphic に反映する
        /// </summary>
        /// <param name="value">反映する色</param>
        protected override void ApplyValue(Color value) {
            if (_targetGraphic == null) {
                return;
            }

            _targetGraphic.color = value;
        }

        /// <summary>
        /// Inspector 更新時に参照を補完する
        /// </summary>
        protected override void OnValidateInternal() {
            AssignTargetGraphicIfNeeded();
        }

        /// <summary>
        /// コンポーネント追加時に参照を補完する
        /// </summary>
        private void Reset() {
            AssignTargetGraphicIfNeeded();
        }

        /// <summary>
        /// 参照先 Graphic が未設定なら同一 GameObject から取得する
        /// </summary>
        private void AssignTargetGraphicIfNeeded() {
            if (_targetGraphic != null) {
                return;
            }

            _targetGraphic = GetComponent<Graphic>();
        }
    }
}
