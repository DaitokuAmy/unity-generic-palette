# Generic Palette Specification

## Scope

この文書は、`Unity Generic Palette` のデータ仕様を定義する。

現時点では次を対象にする。

- アセットの責務分割
- `Profile` 切り替え時に必要な保存情報
- `Loader` を介したアセット差し替えの前提
- 整合性を守るための不変条件

現時点では次を対象外とする。

- ランタイム API の最終形
- 値変更通知の詳細仕様
- Binding の反映タイミング
- 非同期ロード制御の詳細

## 目的

本仕様では、次の条件を満たす設計を定義する。

- 構造が過度に分割されず、追いやすいこと
- `Theme` 専用ではなく、ローカライズやイベント切り替えにも流用できること
- `PaletteAsset` ごとに `EntryId` 集合が安定していること
- `Profile` ごとの実データを必要に応じて別アセット化できること
- `PreloadedAssets` に依存せず、明示的なロードフローを採用できること
- `Loader` を差し替えることで Addressables を含む複数運用へ適応できること

## 用語

| 用語 | 意味 |
| --- | --- |
| PaletteAssetStorage | 全 Palette 情報と Profile 情報を管理するルートアセット |
| PaletteAsset | 1 種類の Palette を表すアセット。`EntryId` 集合とメタ情報を持つ |
| PaletteEntry | `PaletteAsset` に属する要素 1 件分の定義 |
| EntryId | `PaletteAsset` 内で各要素を一意に識別する安定 ID |
| Profile | `Theme`、言語、イベント状態などを表現する切り替え単位 |
| PaletteProfileAsset | ある `PaletteAsset` の、ある `Profile` における値集合を保持する別アセット |
| EntryValue | `EntryId x Profile` の交点にある実値 |
| Loader | `Profile` 切り替え時に対応アセットを解決する差し替え可能な仕組み |
| Loader Key | `Loader` がアセット解決に使う識別子 |

## 基本方針

### 1. `PaletteAsset` はスキーマとして扱う

`PaletteAsset` の責務は、値を大量に抱えることではなく、その Palette に属する `EntryId` 集合とメタ情報を安定して保持することである。

ここで重要なのは次の点である。

- 同じ `PaletteAsset` に対して、どの `Profile` を選んでも `EntryId` 集合は変わらない
- rename や表示名変更と、内部識別子である `EntryId` は分けて扱う
- Profile ごとの差し替えは、`EntryId` 集合を保ったまま値だけを切り替える

### 2. `Profile` ごとの実値は別アセット化できるようにする

`Profile` を多く持つ場合に全データを常時オンメモリにしないため、`Profile` ごとの値は `PaletteAsset` 本体ではなく別アセットとして保持できる構造を採用する。

これにより、次を狙う。

- `PaletteAssetStorage` と `PaletteAsset` は軽量に保つ
- 必要な `Profile` の実値だけをロードできる
- テーマ用途とローカライズ用途でロード粒度を調整できる

### 3. `Profile` 切り替えは `Loader` を経由する

`PaletteAssetStorage` の `Profile` 切り替え時は、登録された `Loader` に対して各 `PaletteAsset` の差し替え候補を問い合わせる前提とする。

このとき、アセット解決のために使うキーは固定せず、少なくとも次を選択肢に含める。

- `GUID`
- `AssetPath`
- `CustomString`

どのキー方式を採用する場合でも、ランタイムでは `AssetDatabase` を使わずに済むよう、必要な文字列はシリアライズ済みの形で保持する。

## 想定パッケージ構成

```text
Runtime/
  Core/
  Binding/
Editor/
Tests/
```

各ディレクトリの責務は次のとおり。

- `Runtime/Core`
  データモデル、Storage、Profile 切り替え、Loader 連携の中核
- `Runtime/Binding`
  解決済みの値をコンポーネントへ反映する仕組み
- `Editor`
  アセット作成、整合性維持、Profile データ編集、Loader キー更新補助
- `Tests`
  データ整合性と Profile 切り替え前提の検証

## データモデル

### PaletteAssetStorage

`PaletteAssetStorage` はルートアセットであり、全体の参照関係と切り替え情報を管理する。

最低限、次の情報を持つ。

- Storage ID
- Display Name
- `Profile` 定義一覧
- 登録済み `PaletteAsset` 一覧
- 初期 Active Profile
- `Loader` 解決に必要なキー情報

`PaletteAssetStorage` 自体は、全 `Profile` の全値を内包する前提にはしない。

### PaletteAsset

`PaletteAsset` は `PaletteAssetStorage` に属する 1 つの Palette を表す。

最低限、次の情報を持つ。

- Palette ID
- Display Name
- 値型情報
- `PaletteEntry` 一覧

`PaletteAsset` の主責務は `EntryId` 集合の保持であり、`Profile` ごとの大量データを直接持つ責務は負わない。

### PaletteEntry

`PaletteEntry` は `PaletteAsset` に属する要素 1 件分の定義である。

最低限、次の情報を持つ。

- `EntryId`
- Display Name
- 任意の説明

必要であれば、将来的に次のメタ情報を持てるようにしてよい。

- 並び順
- 廃止フラグ
- 検索用タグ

### ProfileDefinition

`Profile` 定義は `PaletteAssetStorage` に集約する。

最低限、次の情報を持つ。

- Profile ID
- Display Name
- 任意の説明

`Profile` は用途を限定しない。
`Dark`、`Light`、`Ja`、`En`、`EventA` のような異なる意味の切り替えを同じ概念で扱える。

### PaletteProfileAsset

`PaletteProfileAsset` は、ある `PaletteAsset` に対する、ある `Profile` の値集合を保持するアセットである。

最低限、次の情報を持つ。

- 対象 `PaletteAsset` の識別情報
- 対象 `Profile` の識別情報
- `EntryValue` 配列

`PaletteProfileAsset` は、`PaletteAsset` と同じ `EntryId` 集合を前提とする。
そのため、`EntryValue` の対応は `EntryId` 文字列の重複保持ではなく、`PaletteAsset` 側の定義順に合わせた配列対応を基本とする。

つまり次の対応を前提とする。

- `PaletteAsset.Entries[i]`
- `PaletteProfileAsset.Values[i]`

### EntryValue

`EntryValue` は値そのものを保持する構造体またはクラスである。

型は Palette の種類ごとに異なるため、実体はジェネリックな仕組みまたは派生型で扱う。

重要なのは、`EntryValue` 自体が `EntryId` を主キーとして持つことではなく、`PaletteAsset` の定義順に従って対応付けられることである。

## Loader キー仕様

### 目的

`PaletteAssetStorage` の `Profile` 切り替え時、各 `PaletteAsset` に対して「どのアセットをロードするべきか」を `Loader` へ問い合わせるための識別情報が必要になる。

この識別情報を `Loader Key` と呼ぶ。

### Key の種類

少なくとも次の 3 種類をサポート対象とする。

- `Guid`
- `AssetPath`
- `CustomString`

### Key の保持方法

`GUID` と `AssetPath` は Editor 上では自動取得できるが、ランタイムでは `AssetDatabase` に依存できない。

そのため、`PaletteAssetStorage` またはその配下の登録情報には、実際に `Loader` へ渡す文字列をシリアライズ済みで保持する。

想定する保持項目は次のとおり。

- Key Mode
- Serialized Loader Key
- 必要であれば補助メタデータ

### Key の適用単位

Key 指定は少なくとも `PaletteAsset` 単位で行える必要がある。

必要に応じて次の拡張を許容する。

- Storage 全体の既定 Key Mode
- `PaletteAsset` ごとの override
- `Profile` ごとの追加 suffix や naming rule

ただし、v1 では naming rule を複雑化しすぎず、`Loader` 実装側で解決できる範囲を優先する。

## Profile 切り替え時のデータフロー

データ仕様としては、`Profile` 切り替え時に次の情報がそろっていることを保証する。

1. `PaletteAssetStorage` が次の Active Profile を決定する
2. 各 `PaletteAsset` について `Loader Key` を取得する
3. `Loader` が `Profile` と `Loader Key` を使って対象アセットを解決する
4. 解決結果として、対応する `PaletteProfileAsset` または同等のデータソースを得る
5. 以降のランタイム層が解決済みデータを反映する

この文書では 5 の詳細は定義しない。

## 不変条件

データ整合性のため、少なくとも次を保証する。

### Storage 単位

- `PaletteAssetStorage` 内で Palette ID は重複しない
- `PaletteAssetStorage` 内で Profile ID は重複しない

### PaletteAsset 単位

- 1 つの `PaletteAsset` 内で `EntryId` は重複しない
- `EntryId` は表示名変更とは独立して安定している

### PaletteProfileAsset 単位

- 対象 `PaletteAsset` が一意に定まる
- 対象 `Profile` が一意に定まる
- `Values.Length == PaletteAsset.Entries.Length` を満たす
- `Values[i]` は `PaletteAsset.Entries[i]` に対応する

### 編集操作時

`PaletteAsset` 側で Entry の追加、削除、並び替えが行われた場合、関連するすべての `PaletteProfileAsset` は同じ変更を反映して整合を維持しなければならない。

この同期は Editor 側の責務として扱う。

## Editor 仕様

Editor は最低限次の作業を支援する。

- `PaletteAssetStorage` の作成
- Profile の追加、削除、並び替え
- `PaletteAsset` の追加、削除
- `PaletteEntry` の追加、削除、並び替え
- `PaletteProfileAsset` の作成と編集
- `Loader Key` の更新補助
- 整合性 validation

特に重要なのは、`PaletteAsset` の変更時に `PaletteProfileAsset` 群の整合性を壊さないことだが、これを手作業で維持させないことである。

## Runtime との接続に関する前提

ランタイム API 自体は別途定義するが、データ仕様としては次を前提にする。

- `PaletteAssetStorage` が Active Profile を持つ
- Active Profile の変更が発生したら `Loader` 経由でデータ再解決が走る
- 解決済みの Profile データを、ランタイム層が参照または反映する

この前提により、ランタイム反映方式はあとから差し替えられる。

## 初期スコープ

v1 で最低限含めたい要素は次のとおり。

- `PaletteAssetStorage`
- `PaletteAsset`
- `PaletteEntry`
- `ProfileDefinition`
- `PaletteProfileAsset`
- `Loader Key` 情報
- Editor 上の整合性維持

## 将来拡張

将来的に検討するが v1 では見送る項目を明記する。

- 複数 Profile の同時合成
- Profile 継承
- Entry 単位の部分ロード
- Profile Asset の分割キャッシュ戦略
- コード生成による型安全アクセス
- リモート設定や live update 連携

## 採用判断の要約

この設計では、`PaletteAsset` を「`EntryId` 集合を管理するスキーマ」として扱い、`Profile` ごとの実値を別アセットへ分離する。

その結果、次を同時に満たしやすくなる。

- `EntryId` の安定性を保ちやすい
- Profile 数が増えても本体アセットを重くしにくい
- `Loader` による差し替え戦略を持ち込みやすい
- Addressables を含む複数のロード方針へ寄せやすい
