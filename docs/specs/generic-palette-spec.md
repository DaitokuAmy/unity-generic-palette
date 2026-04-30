# Generic Palette Specification

## Scope

この文書は、`Unity Generic Palette` の現行実装に対応するデータ仕様と責務分担を定義する。

対象に含めるもの:

- `PaletteAssetStorage` / `PaletteAsset` / `PaletteProfileAsset` の構造
- `ProfileId` と `GUID` の対応情報
- `PaletteEngine` による `Profile` 切り替えフロー
- `IPaletteProfileLoader` と Addressables 連携の前提
- Editor による同期・検証・既定 Profile 運用

対象に含めないもの:

- 個別 `Applier` の見た目や UI デザイン
- 各値型に対する演出的な適用ルール
- Addressables Group 構成の詳細ベストプラクティス
- 利用側アプリケーションの状態管理設計

## Goals

本仕様は次の条件を満たすことを目的とする。

- `EntryId` を軸に、`Palette` ごとの値参照を安定させる
- `Profile` の切り替えを値型ごとに一元管理できる
- 組み込み Profile と外部ロード Profile を同じ API で扱える
- ランタイムでは `AssetDatabase` に依存しない
- Editor で `ProfileId -> GUID` の不整合を検出し、自動同期できる

## Terms

| Term | Meaning |
| --- | --- |
| PaletteAssetStorage | 登録済み `PaletteAsset` 一覧を保持するルートアセット |
| PaletteAsset | 1 種類の Palette を表すスキーマアセット |
| PaletteEntry | `PaletteAsset` に含まれる 1 件のエントリ定義 |
| EntryId | `PaletteAsset` 内で要素を一意に識別する安定 ID |
| PaletteProfileAsset | 特定 `PaletteAsset` に対する特定 `Profile` の値集合アセット |
| ProfileId | `PaletteProfileAsset` を論理的に識別する ID |
| ProfileReferenceInfo | `ProfileId` と `ProfileAsset GUID` の対応情報 |
| Included ProfileAsset | `PaletteEngine` が Loader を使わず優先利用する組み込み Profile アセット |
| Loader | 外部ソースから `PaletteProfileAsset` をロード・解放する仕組み |

## Current Package Structure

```text
Runtime/
  Color/
  Core/
  Gradient/
  Text/
  UI/
Editor/
docs/specs/
```

- `Runtime/Core`
  - コアデータ型、`PaletteEngine`、`Loader` 契約、`Profile` コンテキストを提供する
- `Runtime/Color`
  - `Color` 用の組み込みパレット型を提供する
- `Runtime/Gradient`
  - `Gradient` 用の組み込みパレット型を提供する
- `Runtime/Text`
  - `TMP` / `Legacy UI.Text` 向けテキストスタイル値を提供する
- `Runtime/UI`
  - 組み込み `Applier` を提供する
- `Editor`
  - `PaletteEditorWindow`、Project Settings、参照表同期、値編集 UI を提供する

## Built-in Palette Types

現行の組み込み型は次のとおり。

| Palette Type | Profile Value Type | Built-in Applier |
| --- | --- | --- |
| `ColorPaletteAsset` | `UnityEngine.Color` | `GraphicColorPaletteApplier` |
| `TextStylePaletteAsset` | `TextStylePaletteValue` | `TmpTextStylePaletteApplier` |
| `LegacyTextStylePaletteAsset` | `LegacyTextStylePaletteValue` | `LegacyTextStylePaletteApplier` |
| `GradientPaletteAsset` | `UnityEngine.Gradient` | なし |

`Gradient` は値型として利用可能だが、現時点では組み込み `Applier` を持たない。

## Data Model

### PaletteAssetStorage

`PaletteAssetStorage` は `PaletteAsset` の登録一覧を保持するルートアセットである。

保持項目:

- `PaletteAssets`

責務:

- `PaletteEngine.InitializeAsync()` が初期化対象の `PaletteAsset` 一覧を列挙するための起点となる
- Editor から作成される `PaletteAsset` の登録先となる

### PaletteAssetBase

`PaletteAssetBase` はすべての Palette 型の共通基底である。

保持項目:

- `Entries`
- `DefaultProfileId`
- `ProfileReferences`

責務:

- `EntryId` 集合の保持
- `EntryId -> index` キャッシュ
- `ProfileId -> AssetGuid` キャッシュ

仕様:

- `Entries[i]` と `PaletteProfileAsset.Values[i]` は同じ index で対応する
- `DefaultProfileId` はこの Palette の初期適用候補を表す
- `ProfileReferences` は `ProfileId` と `GUID` のペアをシリアライズ保持する

### PaletteEntry

`PaletteEntry` は `PaletteAsset` に属する 1 件のエントリ定義である。

保持項目:

- `EntryId`
- `DisplayName`
- `Description`

仕様:

- `EntryId` は値適用の主キーであり、表示名変更とは独立して扱う
- 同一 `PaletteAsset` 内で `EntryId` は重複してはならない

### PaletteProfileAssetBase

`PaletteProfileAssetBase` はすべての Profile アセットの共通基底である。

保持項目:

- `ProfileId`
- `SortOrder`

`PaletteProfileAssetBase<TPaletteAsset, TValue>` は追加で次を持つ。

- 対象 `PaletteAsset`
- `Values`

仕様:

- `Values.Length` は対応する `PaletteAsset.Entries.Count` と一致している必要がある
- `Values[i]` は `PaletteAsset.Entries[i]` に対応する
- 値参照は `EntryId` 文字列を各要素に重複保持せず、index ベースで解決する

### ProfileReferenceInfo

`ProfileReferenceInfo` は `PaletteAssetBase.ProfileReferences` の要素である。

保持項目:

- `ProfileId`
- `AssetGuid`

用途:

- ランタイムで `AssetDatabase` を使わず、`Loader` に対して GUID を渡す
- Editor で `PaletteProfileAsset` の生成・リネーム・削除・`.meta` 再生成に追従する

## Runtime Flow

### Initialization

`PaletteEngine.InitializeAsync()` は `PaletteAssetStorage` を列挙し、`DefaultProfileId` が設定された Palette を初期化する。

制約:

- 同一 `ProfileAsset` 型に対して複数の `DefaultProfileId` を同時に初期化することはできない
- `InitializeAsync()` は `ProfileAsset` 型ごとに 1 つの既定 Profile だけを許容する

### Profile Change

`PaletteEngine.ChangeProfileAsync<TProfileAsset>(profileId)` の動作は次のとおり。

1. `profileId` の妥当性を検証する
2. `Included ProfileAsset` 一覧から一致する `ProfileId` を探す
3. 見つからない場合は `PaletteAssetBase.ProfileReferences` から `profileGuid` を解決する
4. `IPaletteProfileLoader.LoadAsync(profileId, profileGuid, assetName, cancellationToken)` を呼ぶ
5. 解決された `PaletteProfileAsset` を current として保持する
6. 購読中 `Applier` へ変更通知を発火する

補足:

- Loader が返した `PaletteProfileAsset` は、解決元 `PaletteAsset` と一致しなければならない
- Loader 管理下の Profile は、次の Profile 適用時に `Unload` 対象となる
- 同一 `ProfileAsset` 型に対する同時切り替えはサポートしない

## Loader Contract

### IPaletteProfileLoader

`IPaletteProfileLoader` は次の契約を持つ。

- `LoadAsync<TProfileAsset>(string profileId, string profileGuid, string assetName, CancellationToken cancellationToken)`
- `Unload(string profileId, string profileGuid, string assetName, PaletteProfileAssetBase profileAsset)`

意図:

- `profileId` は論理的識別子として保持する
- `profileGuid` はロード手段向けの物理キーとして使う
- `assetName` はライブラリ内部の命名規則を Loader 側へ持ち込まないための補助情報として使う
- `Unload` 時も同じ `profileId` / `profileGuid` / `assetName` を受け取り、Loader 側で解放文脈を参照できる

### GuidBaseAddressablesLoader

`GuidBaseAddressablesLoader` は `#if USE_ADDRESSABLES` 時のみ有効な組み込み Loader である。

仕様:

- `Addressables.LoadAssetAsync<TProfileAsset>(profileGuid)` でロードする
- ロード成功時は `AsyncOperationHandle` を内部保持する
- `Unload` 時に `Addressables.Release(handle)` を呼ぶ

利用前提:

- Addressables の Catalog で GUID を解決可能にしておく必要がある
- 代表的には `Include GUID in Catalog` を有効化した運用を想定する

## Editor Behavior

### Palette Editor

`PaletteEditorWindow` は次の用途を担当する。

- `PaletteAssetStorage` の作成・選択
- `PaletteAsset` の作成・削除
- `PaletteProfileAsset` の作成・削除・並び替え
- `Entry` と `Profile Value` の編集
- `DefaultProfileId` の設定

### Default Reflection Rule

Editor 上で preview 用 current profile が切り替わるのは、`Default` 設定時のみとする。

現在の仕様:

- Profile リスト選択だけでは current profile は切り替えない
- Profile Popup 選択だけでは current profile は切り替えない
- Profile 作成直後も自動反映しない
- `Default` を設定したときだけ current profile を更新する

### Profile Reference Synchronization

`PaletteProfileReferenceEditorUtility` は `ProfileReferences` を同期・検証する。

同期タイミング:

- Editor 起動時
- Project 変更時
- Profile 作成時
- Profile リネーム時
- Profile 削除時

検出対象:

- 空エントリ
- 空 `ProfileId`
- 空 `GUID`
- 重複 `ProfileId`
- `GUID` の不一致
- 孤立した `DefaultProfileId`

方針:

- 実在する `PaletteProfileAsset` を正とし、`PaletteAssetBase.ProfileReferences` を再構築する
- 不整合が見つかった場合は警告ログを出しつつ、自動修復を試みる

### Profile Value Copy

Profile 値のコピー機能は `SerializedProperty` ベースで動作し、`Gradient` を含む組み込み値型の複製に対応する。

## Invariants

### PaletteAsset

- 同一 `PaletteAsset` 内で `EntryId` は重複しない
- 同一 `PaletteAsset` 内で `ProfileReferences.ProfileId` は重複しない
- `DefaultProfileId` が設定されている場合、その `ProfileId` に対応する `PaletteProfileAsset` が存在するのが望ましい

### PaletteProfileAsset

- `PaletteAssetBase` が null であってはならない
- `ProfileId` は空であってはならない
- `Values.Length == PaletteAsset.Entries.Length`

### PaletteEngine

- 同一 `ProfileAsset` 型に対する切り替え要求は逐次実行でなければならない
- Loader ベース切り替えでは、対象 `ProfileAsset` 型に対する `PaletteAsset` が一意に決まる必要がある
- `InitializeAsync()` では同一 `ProfileAsset` 型の既定 Profile 多重初期化を許容しない

## Extensibility

独自パレットを追加する場合は、少なくとも次を満たす。

1. `PaletteAssetBase` 派生型を作る
2. `PaletteProfileAssetAttribute(typeof(YourProfileAsset))` を付ける
3. `PaletteProfileAssetBase<TPaletteAsset, TValue>` 派生型を作る
4. 必要であれば `PropertyDrawer` と `Applier` を追加する

`GradientPaletteAsset` は、組み込み `Applier` を持たない拡張例として位置付けられる。

## Out of Scope

この文書では次を確定しない。

- `Gradient` 用組み込み `Applier`
- `Profile` の表示名や説明を別定義で持つ仕組み
- Storage レベルの複雑な naming rule
- Addressables Group 自動生成

<!-- TODO: Add a short GIF showing that Profile list selection alone does not change preview, and Default button does. -->
<!-- TODO: Add a PNG showing the ProfileReferenceInfo synchronization warning example after a .meta regeneration. -->
