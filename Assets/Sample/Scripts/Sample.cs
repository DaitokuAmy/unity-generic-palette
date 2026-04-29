using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityGenericPalette;

/// <summary>
/// サンプルクラス
/// </summary>
public class Sample : MonoBehaviour {
    [Serializable]
    private sealed class ThemaInfo {
        public string ProfileId;
        public Button Button;
    }

    [Serializable]
    private sealed class TextStyleInfo {
        public string ProfileId;
        public Button Button;
    }

    [SerializeField, Tooltip("テーマ情報リスト")]
    private ThemaInfo[] _themeInfos;
    
    [SerializeField, Tooltip("テキストスタイル情報リスト")]
    private TextStyleInfo[] _textStyleInfos;

    /// <summary>
    /// 開始処理
    /// </summary>
    private IEnumerator Start() {
        PaletteEngine.SetLoader(new GuidBaseAddressablesLoader());
        yield return PaletteEngine.InitializeAsync().ToCoroutine();
    }

    /// <summary>
    /// アクティブ時処理
    /// </summary>
    private void OnEnable() {
        for (var i = 0; i < _themeInfos.Length; i++) {
            var info = _themeInfos[i];
            if (info.Button == null) {
                continue;
            }

            var id = info.ProfileId;
            info.Button.onClick.AddListener(() => {
                PaletteEngine.ChangeProfileAsync<ColorPaletteProfileAsset>(id).Forget();
            });
        }

        for (var i = 0; i < _textStyleInfos.Length; i++) {
            var info = _textStyleInfos[i];
            if (info.Button == null) {
                continue;
            }
            
            var id = info.ProfileId;
            info.Button.onClick.AddListener(() => {
                PaletteEngine.ChangeProfileAsync<TextStylePaletteProfileAsset>(id).Forget();
            });
        }
    }

    /// <summary>
    /// 非アクティブ時処理
    /// </summary>
    private void OnDisable() {
        for (var i = 0; i < _themeInfos.Length; i++) {
            var info = _themeInfos[i];
            if (info.Button == null) {
                continue;
            }

            info.Button.onClick.RemoveAllListeners();
        }

        for (var i = 0; i < _textStyleInfos.Length; i++) {
            var info = _textStyleInfos[i];
            if (info.Button == null) {
                continue;
            }
            
            info.Button.onClick.RemoveAllListeners();
        }
    }
}