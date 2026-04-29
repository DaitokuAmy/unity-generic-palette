# Specs

`docs/specs` は `Unity Generic Palette` の仕様を段階的に整理していくためのディレクトリです。

このディレクトリでは、実装より先に次の内容を明確にします。

- ライブラリが解決したい問題
- Runtime と Editor の責務
- 拡張ポイントと非対応範囲
- 将来の実装で壊してはいけない前提

## 設計原則

- 構造は小さく保ち、理解コストの低い中核を優先する
- 値の切り替え概念は `Theme` に限定せず、汎用的な `Profile` として扱う
- `PaletteAsset` は `EntryId` 集合を管理するスキーマとして扱う
- `Profile` ごとの実値は別アセット化できる前提で設計する
- `PreloadedAssets` への自動登録は行わず、明示的ロードを前提とする
- Addressables は重要な利用形態として考慮するが、core は Addressables へ直接依存しない
- `Profile` 切り替え時の差し替えは `Loader` を介して行えるようにする
- ライブラリ利用者だけでなく、ライブラリ実装者が独自パレットを追加できることを重視する

## 文書一覧

- [Generic Palette Specification](./generic-palette-spec.md)

## 今後追加する想定の文書

- 具体的な Runtime API
- Editor ワークフロー
- 組み込みパレット型の一覧
- テスト方針
