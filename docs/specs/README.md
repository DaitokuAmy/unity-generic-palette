# Specs

`docs/specs` は `Unity Generic Palette` の実装方針と現在仕様を整理するためのディレクトリです。

このディレクトリでは、次の観点を明文化します。

- Runtime と Editor の責務分担
- `Palette` / `Profile` / `EntryId` のデータモデル
- `Loader` による外部ロードの前提
- 実装者が壊してはいけない整合性ルール

## 設計原則

- `PaletteAsset` は値の入れ物ではなく、`EntryId` 集合を定義するスキーマとして扱う
- `Profile` ごとの実値は `PaletteProfileAsset` に分離し、必要に応じて動的ロードできるようにする
- Core は Addressables へ直接依存せず、`IPaletteProfileLoader` を介して外部ロードを差し替え可能にする
- Editor はアセット生成だけでなく、`ProfileId -> GUID` 対応表の同期と検証も担当する
- 利用者向けの組み込みパレットと、独自パレット拡張の両方を成立させる

## 文書一覧

- [Generic Palette Specification](./generic-palette-spec.md)

## 補足

- 利用者向けセットアップはリポジトリ直下の `README.md` を参照
- このディレクトリの内容は、将来の API 追加に合わせて随時更新する

<!-- TODO: Add a small architecture diagram PNG that shows Storage, PaletteAsset, PaletteProfileAsset, Engine, and Loader relationships. -->
