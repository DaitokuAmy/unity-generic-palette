# Unity Generic Palette

`Unity Generic Palette` は、`Color` や `TextStyle` のような共通設定を `Palette` と `Profile` で管理し、UI やコンポーネントへ再利用可能な形で適用するためのライブラリです。

`Theme` 切り替えだけでなく、言語差し替え、イベント状態差し替え、Addressables を使った遅延ロードにも対応できる構成を目指しています。

<!-- TODO: この段落の直下にヒーロー GIF を追加する。
初見の人がスクロールせずに「Profile を切り替えると複数の共通設定がまとめて変わるライブラリ」だと理解できる短いループを目指す。
長さの目安は 6-10 秒。
含めたい内容:
1. SampleScene を開いた状態を見せる
2. Editor またはランタイム UI から Profile を切り替える
3. 複数の UI 要素で Color と TextStyle が同時に変わる様子を見せる
4. 可能なら言語切り替えのような非ビジュアル寄りユースケースも一瞬だけ含める
目的は、本文を読まなくても「Profile 単位で共通設定が連動する」ことが伝わること。 -->

## Features

- `PaletteAsset` に `EntryId` 集合を定義し、値の参照先を安定化できる
- `PaletteProfileAsset` で `Profile` ごとの実値を分離できる
- `PaletteEngine` が `Profile` 切り替えと再反映通知を一元管理する
- `Included ProfileAsset` と外部 `Loader` の両方を同じ API で扱える
- `ProfileId -> GUID` の対応表を Editor が自動同期する
- Addressables 利用時は `GuidBaseAddressablesLoader` を使って GUID ベースでロードできる
- 組み込みの `Color` / `TMP TextStyle` / `Legacy TextStyle` / `Gradient` パレット型を用意している
- 組み込み `Applier` は `Graphic.color`、`TMP_Text`、`UnityEngine.UI.Text` に対応している

## Package

- Package name: `com.daitokuamy.unitygenericpalette`
- Current version: `0.9.0`
- Unity: `6000.0` 以降

## Built-in Palette Types

| Palette Type | Value Type | Built-in Applier |
| --- | --- | --- |
| `ColorPaletteAsset` | `UnityEngine.Color` | `GraphicColorPaletteApplier` |
| `TextStylePaletteAsset` | `TextStylePaletteValue` | `TmpTextStylePaletteApplier` |
| `LegacyTextStylePaletteAsset` | `LegacyTextStylePaletteValue` | `LegacyTextStylePaletteApplier` |
| `GradientPaletteAsset` | `UnityEngine.Gradient` | なし |

`Gradient` はパレット化できますが、現時点では組み込み `Applier` は提供していません。

## Installation

### Install via Package Manager

Unity の `Window > Package Manager` を開き、`Add package from git URL...` から次を指定します。

```text
https://github.com/DaitokuAmy/unity-generic-palette.git?path=/Packages/com.daitokuamy.unitygenericpalette
```

タグを固定したい場合は末尾にバージョンを付けます。

```text
https://github.com/DaitokuAmy/unity-generic-palette.git?path=/Packages/com.daitokuamy.unitygenericpalette#0.9.0
```

### Install via manifest.json

```json
{
  "dependencies": {
    "com.daitokuamy.unitygenericpalette": "https://github.com/DaitokuAmy/unity-generic-palette.git?path=/Packages/com.daitokuamy.unitygenericpalette"
  }
}
```

## Quick Start

### 1. Create `PaletteAssetStorage`

Project Settings の `Project/Unity Generic Palette` から `Palette Asset Storage` を作成または設定します。

作成されるルートアセット:

- `PaletteAssetStorage`

このアセットは利用する `PaletteAsset` 一覧を保持します。

### 2. Create palettes and profiles in `PaletteEditorWindow`

`PaletteEditorWindow` から次を行います。

- `PaletteAsset` の追加
- `Entry` の追加
- `PaletteProfileAsset` の追加
- `Profile Value` の編集
- `Default` の設定

重要な Editor 仕様:

- Profile リストを選択しただけでは preview は切り替わりません
- Profile Popup を選択しただけでも preview は切り替わりません
- `Default` を設定したときだけ preview 用 current profile が更新されます

<!-- TODO: このセクション用に手順 GIF を追加する。
宣伝用ではなく、ミニ手順書として見えることを優先する。
速いカット割りは避け、次の操作を順番に撮る:
1. Project Settings を開いて `PaletteAssetStorage` を設定する
2. `PaletteEditorWindow` を開く
3. Palette を追加する
4. Entry を 1-2 個追加する
5. Profile を 2 つ作成する
6. 片方の Profile の値を編集する
7. `Set Default` を押した瞬間にだけ preview が切り替わることを見せる -->

### 3. Place `PaletteEngine` in the scene

シーンに `PaletteEngine` を 1 つ配置し、次を設定します。

- `Palette Asset Storage`
- 必要なら `Dont Destroy On Load`
- 必要なら `Included ProfileAssets`

`Included ProfileAssets` に入れた Profile は Loader より優先されます。

### 4. Initialize at runtime

最小構成では、`Start` などから `PaletteEngine.InitializeAsync()` を呼びます。

`UniTask` 利用時の例:

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityGenericPalette;

public sealed class PaletteBootstrap : MonoBehaviour {
    private void Start() {
        InitializeAsync().Forget();
    }

    private async UniTaskVoid InitializeAsync() {
        await PaletteEngine.InitializeAsync();
    }
}
```

`DefaultProfileId` が設定された Palette は、この初期化時に反映されます。

### 5. Add appliers

対象コンポーネントに組み込み `Applier` を追加し、`EntryId` を設定します。

- `GraphicColorPaletteApplier`
- `TmpTextStylePaletteApplier`
- `LegacyTextStylePaletteApplier`

`PaletteApplierBase` は現在の `Profile` を購読し、対応する `EntryId` の値を受け取ると `ApplyValue` を呼びます。

## Runtime API

主な API は次のとおりです。

- `PaletteEngine.InitializeAsync()`
- `PaletteEngine.SetLoader(IPaletteProfileLoader loader)`
- `PaletteEngine.ChangeProfileAsync<TProfileAsset>(string profileId)`

例:

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityGenericPalette;

public sealed class LocaleSwitcher : MonoBehaviour {
    public async UniTask SwitchToJapaneseAsync() {
        await PaletteEngine.ChangeProfileAsync<TextStylePaletteProfileAsset>("Japanese");
    }
}
```

## Addressables Integration

Addressables を使う場合は、`GuidBaseAddressablesLoader` を利用できます。

前提:

- `com.unity.addressables` が入っていること
- Addressables Catalog から GUID で引けること

サンプル:

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityGenericPalette;

public sealed class PaletteBootstrap : MonoBehaviour {
    private void Start() {
        InitializeAsync().Forget();
    }

    private async UniTaskVoid InitializeAsync() {
        PaletteEngine.SetLoader(new GuidBaseAddressablesLoader());
        await PaletteEngine.InitializeAsync();
    }
}
```

`GuidBaseAddressablesLoader` は `PaletteAssetBase` にシリアライズされた `ProfileId -> GUID` 対応表を使って `Addressables.LoadAssetAsync<TProfileAsset>(guid)` を呼びます。

<!-- TODO: Addressables 設定用の画像を追加する。
1 枚の注釈付きロング画像でも、複数ステップ PNG でもよい。
最低限、次を含める:
1. どの ProfileAsset を Addressable にする必要があるか
2. GUID ベースでロードする前提であること
3. `GuidBaseAddressablesLoader` を bootstrap コードのどこで設定するか
4. 実行順が `SetLoader` -> `InitializeAsync` -> `ChangeProfileAsync` であること
複数画像に分ける場合は `readme-addressables-step1.png` のような名前に揃える。 -->

## Loader and GUID Synchronization

Editor は `PaletteProfileAsset` の作成・リネーム・削除・Project 変更時に `ProfileId -> GUID` 対応表を自動同期します。

同期対象:

- 欠損した参照
- 重複 `ProfileId`
- 古い GUID
- 空 `GUID`
- 孤立した `DefaultProfileId`

この対応表は、ランタイムが `AssetDatabase` なしで Loader に GUID を渡すために使われます。

## Extending with Custom Palettes

独自パレットを追加したい場合は、次の 2 型を作ります。

1. `PaletteAssetBase` 派生型
2. `PaletteProfileAssetBase<TPaletteAsset, TValue>` 派生型

`PaletteAsset` 側には、対応する Profile 型を示す属性を付けます。

```csharp
using UnityEngine;

namespace UnityGenericPalette {
    [PaletteProfileAsset(typeof(MyPaletteProfileAsset))]
    public sealed class MyPaletteAsset : PaletteAssetBase {
    }

    public sealed class MyPaletteProfileAsset : PaletteProfileAssetBase<MyPaletteAsset, MyValueType> {
    }
}
```

必要に応じて追加するもの:

- `PropertyDrawer`
- `Applier`
- 独自 `Loader`

## Sample

サンプルアセットは次にあります。

- Scene: `Assets/Sample/Scenes/SampleScene.unity`
- Script: `Assets/Sample/Scripts/Sample.cs`
- Palette assets: `Assets/Sample/UnityGenericPalette`

サンプルでは `GuidBaseAddressablesLoader` を設定して `PaletteEngine.InitializeAsync()` を呼ぶ流れを確認できます。

<!-- TODO: Sample セクション用に「どこから見ればよいか」が分かる画像を追加する。
スクリーンショットでも GIF でもよい。
入れたい内容:
1. `PaletteEngine` を選択した Hierarchy
2. `Palette Asset Storage` と `Included ProfileAssets` が見える Inspector
3. `Assets/Sample/UnityGenericPalette` が見える Project ウィンドウ
README の手順とサンプル同梱アセットの対応が一目で分かることを目的にする。 -->

## Repository Layout

```text
Packages/com.daitokuamy.unitygenericpalette/
  Editor/
  Runtime/
Assets/Sample/
docs/specs/
```

- `Packages/com.daitokuamy.unitygenericpalette`
  - 配布対象の UPM パッケージ本体
- `Assets/Sample`
  - 動作確認用のサンプルアセット
- `docs/specs`
  - 実装寄りの仕様整理ドキュメント

## Limitations

- `Gradient` 用の組み込み `Applier` はまだありません
- Addressables Group の自動構成は行いません
- `Profile` の表示名や説明を別定義で持つ仕組みはまだありません

## License

MIT License
