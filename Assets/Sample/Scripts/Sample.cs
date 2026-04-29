using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityGenericPalette;

/// <summary>
/// サンプルクラス
/// </summary>
public class Sample : MonoBehaviour {
    /// <summary>
    /// 開始処理
    /// </summary>
    private IEnumerator Start() {
        PaletteEngine.SetLoader(new GuidBaseAddressablesLoader());
        yield return PaletteEngine.InitializeAsync().ToCoroutine();
    }
}