# ながレース / NAGARE RACE
## Claude Code 開発開始パッケージ v1.1

v1.1（2026-10-02）: 実装前の仕様レビューを反映。指摘・決定・要確認事項は `docs/14_SPEC_REVIEW.md` を参照。

本パッケージは、Unityで「ながレース」のMVP開発を開始するための仕様一式です。

### まず読む順番

1. `CLAUDE.md`
2. `docs/01_PRODUCT_REQUIREMENTS.md`
3. `docs/02_GAME_DESIGN.md`
4. `docs/03_SCREEN_UI_SPEC.md`
5. `docs/04_RACE_SIMULATION.md`
6. `docs/05_TRAINING_INHERITANCE.md`
7. `docs/06_CHARACTER_ART_3D.md`
8. `docs/07_TECH_ARCHITECTURE.md`
9. `docs/08_DATA_MODEL_SAVE.md`
10. `docs/09_MVP_IMPLEMENTATION_PLAN.md`
11. `docs/10_ACCEPTANCE_TESTS.md`
12. `docs/14_SPEC_REVIEW.md`
13. `FIRST_PROMPT_FOR_CLAUDE.md`

## ドキュメントの正本

- 正本は `docs/01〜14_*.md` と `Assets/NagaRace/Config/*.json`（バランス値）・`Assets/NagaRace/Data/`（コース・系統定義）
- `NAGARACE_MASTER_REQUIREMENTS.md` は docs/01〜14 を連結した**生成物**。直接編集しない
  - 再生成: `powershell -File tools/build-master-doc.ps1`

## 企画の一文

**かわいい葉っぱたちを何世代も育て、自然に「ならし」、最高峰 MOST HIGHER RIVER を目指す育成レースシミュレーション。**

## コア比率

- 育成・系譜: 60%
- レース: 25%
- スローライフ / 収集 / 世界観: 15%

## MVPの最重要検証

「コンテンツ量」ではなく次の4点を検証する。

1. 自動で流れるレースを見ていて、自分の葉っぱを応援したくなるか
2. 葉脈炉を始動・解除する判断が面白いか
3. 育成した結果を次のレースで試したくなるか
4. ロストが理不尽な削除ではなく、思い出として機能するか

## 参考画像

- `art_reference/nagarace_loose_style.png`
  - キャラクターの「ゆるさ・かわいさ」はこちらを優先する。
- `art_reference/nagarace_original_board.png`
  - 世界観・企画ボードの参考。
- `art_reference/nagarace_screen_samples.png`
  - 画面構成の参考。ただしキャラの険しい表情は弱める。

## 開発環境（M0〜）

- Unity **6000.3.25f1**（6.3 LTS）。Unity Hub でこのフォルダ（リポジトリルート）を開く
- 主なパッケージ: URP / Input System / Test Framework / Newtonsoft JSON

### フォルダ

```text
Assets/NagaRace/
  Config/       バランス設定 JSON（正本）
  Data/         コース・系統定義 JSON
  Scripts/      Domain / Application / Infrastructure / Presentation（asmdef 分割）
  Editor/       デバッグシーン生成など
  Tests/EditMode/
  Scenes/Debug/RaceDebug.unity
```

### デバッグレースを見る（M1）

1. `Assets/NagaRace/Scenes/Debug/RaceDebug.unity` を開いて Play
2. 画面左上の「同Seedで再実行 / 次のSeed / コース切替 / x1〜x8」で操作
3. シーンが壊れたらメニュー `NagaRace > Setup > Create Race Debug Scene` で作り直せる

### テスト

- Editor: `Window > General > Test Runner` → EditMode → Run All
- コマンドライン:
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml
  ```
