# jk-2d-jump-action

Unity 6 で作成している 2D ジャンプアクションです。

## 環境

- Unity 6000.4.0f1
- Universal Render Pipeline (URP)
- Input System

## セットアップ

1. Git LFS をインストールし、有効にする。画像・音声などは LFS で管理している。

   ```
   git lfs install
   ```

2. リポジトリを clone し、作業ブランチ `develop` に切り替える。

   ```
   git clone <リポジトリURL>
   cd jk-2d-jump-action
   git checkout develop
   ```

3. Unity Hub で Unity 6000.4.0f1 をインストールする。
4. Unity Hub の「プロジェクトを追加」で `jk-2d-jump-action` フォルダを選び、開く。
5. 初回起動時に `Library/` が生成される。パッケージは `Packages/manifest.json` から自動で取得される。
6. シーンは `Assets/_Project/Scenes/TestArea.unity` を開いて動作を確認する。
7. スクリプトは `jk-2d-jump-action.sln` を Visual Studio または Rider で開いて編集する。

`Library/`、`Logs/`、`UserSettings/`、`Build/` は `.gitignore` で除外しているので、コミットしない。

## 主な操作

- 左右移動（スティック左右）
- しゃがみ（スティック下）
- ジャンプ
- ダッシュ
- 急降下
- ボディアタック

操作は Input Action（`Assets/InputSystem_Actions.inputactions`）で設定しています。

## フォルダ構成

- `Assets/_Project/Features/Player/` : プレイヤーの移動・ジャンプ・ダッシュ・しゃがみ・ボディアタック
- `Assets/_Project/Features/Map/` : マップ関連のスクリプト
- `Assets/_Project/Scenes/` : シーン（TestArea など）
- `Docs/` : 設計資料

## ドキュメント

- `Docs/Specs/` : 機能の仕様（ダッシュ・しゃがみ・ボディアタック・坂・動作状態）
- `Docs/Notes/` : 基本仕様やリファクタリングの検討メモ
- `Docs/DeveloperGuides/` : 開発時のルール（コードの読みやすさなど）
- `Docs/Reference/` : 参考資料
