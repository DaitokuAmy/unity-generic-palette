using System.Collections;
using System.Collections.Generic;
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
        var tasks = new List<UniTask>();
        tasks.Add(PaletteEngine.ChangeProfileAsync<ColorPaletteProfileAsset>("Default"));
        yield return UniTask.WhenAll(tasks).ToCoroutine();
    }
}