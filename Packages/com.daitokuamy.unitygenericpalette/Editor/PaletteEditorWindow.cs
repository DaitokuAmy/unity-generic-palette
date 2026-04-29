using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityGenericPalette {
    /// <summary>
    /// PaletteAssetStorage と関連アセットを編集する EditorWindow
    /// </summary>
    public sealed partial class PaletteEditorWindow : EditorWindow {
        private const float InspectorWidth = 320f;
        private const float HeaderLabelWidth = 52f;
        private const float HeaderPopupWidth = 240f;
        private const float HeaderButtonWidth = 90f;
        private const float HeaderRemoveButtonWidth = 100f;
        private static readonly string[] TabNames = { "Palette", "Profile" };

        private enum TabMode {
            Palette = 0,
            Profile = 1,
        }

        [SerializeField]
        private PaletteAssetStorage _paletteAssetStorage;
        [SerializeField]
        private TabMode _tabMode;
        [SerializeField]
        private PaletteAssetBase _selectedPaletteAsset;
        [SerializeField]
        private PaletteProfileAssetBase _selectedProfileAsset;
        [SerializeField]
        private int _selectedEntryIndex = -1;
        [SerializeField]
        private Vector2 _paletteBodyScrollPosition;
        [SerializeField]
        private Vector2 _profileBodyScrollPosition;

        private VisualElement _headerContainer;
        private VisualElement _bodyContainer;
        private VisualElement _tabToolbarContainer;
        private ReorderableList _paletteEntryList;
        private PaletteAssetBase _paletteEntryListPaletteAsset;
        private ReorderableList _profileAssetList;
        private PaletteAssetBase _profileAssetListPaletteAsset;
        private List<PaletteProfileAssetBase> _profileAssetListItems;

        /// <summary>
        /// Window を開く
        /// </summary>
        [MenuItem("Window/Unity Generic Palette/Palette Editor")]
        public static void Open() {
            var window = GetWindow<PaletteEditorWindow>();
            window.titleContent = new GUIContent("Palette Editor");
            window.minSize = new Vector2(960f, 540f);
            window.Show();
        }

        /// <summary>
        /// UI を構築する
        /// </summary>
        public void CreateGUI() {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            _tabToolbarContainer = CreateToolbarContainer(DrawTabToolbarIMGUI);
            rootVisualElement.Add(_tabToolbarContainer);

            _headerContainer = new VisualElement();
            _headerContainer.style.marginLeft = 8f;
            _headerContainer.style.marginRight = 8f;
            _headerContainer.style.marginTop = 8f;
            _headerContainer.style.marginBottom = 8f;
            rootVisualElement.Add(_headerContainer);

            _bodyContainer = new VisualElement();
            _bodyContainer.style.flexGrow = 1f;
            _bodyContainer.style.paddingLeft = 8f;
            _bodyContainer.style.paddingRight = 8f;
            _bodyContainer.style.paddingBottom = 8f;
            rootVisualElement.Add(_bodyContainer);

            RebuildWindow();
        }

        /// <summary>
        /// Window 全体を再構築する
        /// </summary>
        private void RebuildWindow() {
            if (_tabToolbarContainer == null || _headerContainer == null || _bodyContainer == null) {
                return;
            }

            ValidateSelection();
            _headerContainer.Clear();
            _bodyContainer.Clear();

            if (_paletteAssetStorage == null) {
                _tabToolbarContainer.style.display = DisplayStyle.None;
                _bodyContainer.Add(CreateStorageMissingBody());
                return;
            }

            _tabToolbarContainer.style.display = DisplayStyle.Flex;

            if (_tabMode == TabMode.Palette) {
                BuildPaletteTab();
                return;
            }

            BuildProfileTab();
        }

        /// <summary>
        /// Palette タブを構築する
        /// </summary>
        private void BuildPaletteTab() {
            _headerContainer.Add(CreateToolbarContainer(DrawPaletteHeaderIMGUI));

            var splitView = new TwoPaneSplitView(0, position.width - InspectorWidth - 32f, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            splitView.Add(CreatePaletteBody());
            splitView.Add(CreatePaletteInspector());
            _bodyContainer.Add(splitView);
        }

        /// <summary>
        /// Profile タブを構築する
        /// </summary>
        private void BuildProfileTab() {
            _headerContainer.Add(CreateToolbarContainer(DrawProfileHeaderIMGUI));
            _bodyContainer.Add(CreateProfileBody());
        }

        /// <summary>
        /// Palette Body を生成する
        /// </summary>
        /// <returns>生成した要素</returns>
        private VisualElement CreatePaletteBody() {
            var container = new VisualElement();
            container.style.flexGrow = 1f;

            var body = new IMGUIContainer(DrawPaletteBodyIMGUI);
            body.style.flexGrow = 1f;
            container.Add(body);
            return container;
        }

        /// <summary>
        /// Palette Inspector を生成する
        /// </summary>
        /// <returns>生成した要素</returns>
        private VisualElement CreatePaletteInspector() {
            var scrollView = CreateInspectorScrollView();
            var inspector = new IMGUIContainer(DrawPaletteInspectorIMGUI);
            inspector.style.flexGrow = 1f;
            scrollView.Add(inspector);
            return scrollView;
        }

        /// <summary>
        /// Profile Body を生成する
        /// </summary>
        /// <returns>生成した要素</returns>
        private VisualElement CreateProfileBody() {
            var container = new VisualElement();
            container.style.flexGrow = 1f;

            var body = new IMGUIContainer(DrawProfileBodyIMGUI);
            body.style.flexGrow = 1f;
            container.Add(body);
            return container;
        }

        /// <summary>
        /// IMGUI ベースの Toolbar コンテナを生成する
        /// </summary>
        /// <param name="onGUIHandler">描画ハンドラー</param>
        /// <returns>生成した要素</returns>
        private VisualElement CreateToolbarContainer(Action onGUIHandler) {
            var container = new IMGUIContainer(() => onGUIHandler?.Invoke());
            container.style.flexShrink = 0f;
            return container;
        }

        /// <summary>
        /// Inspector 用 ScrollView を生成する
        /// </summary>
        /// <returns>生成した ScrollView</returns>
        private ScrollView CreateInspectorScrollView() {
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.width = InspectorWidth;
            scrollView.style.flexShrink = 0f;
            scrollView.style.flexGrow = 0f;
            return scrollView;
        }

        /// <summary>
        /// Storage 未設定時の Body を生成する
        /// </summary>
        /// <returns>生成した要素</returns>
        private VisualElement CreateStorageMissingBody() {
            var container = new VisualElement();
            container.style.flexGrow = 1f;

            var body = new IMGUIContainer(DrawPaletteAssetStorageMissingIMGUI);
            body.style.flexGrow = 1f;
            container.Add(body);
            return container;
        }
    }
}
