# 09. MVP実装計画

> **v1.1 改訂（2026-10-02 仕様レビュー）** M0/M1 の作業・完了条件を具体化。追記箇所には【v1.1】を付ける。

## Milestone 0 — Project Bootstrap

### 作業
- Unity 6.3 LTSで3D URP Project
- Git初期化
- Assembly Definition
- Input System
- 基本Folder
- EditMode Test設定
- Debug Scene

### 完了条件
- 空Sceneが実行可能
- Test Runnerが動く
- Domain assemblyがPresentationに依存しない

### 【v1.1】補足
- エディタは 6000.3.x（6.3 LTS）。`ProjectVersion.txt` をコミットする
- Unity 用 `.gitignore`（Library / Temp / Logs / UserSettings 等を除外）と `.gitattributes`（画像・FBX は Git LFS）を用意
- パッケージ: Input System / URP / Test Framework / `com.unity.nuget.newtonsoft-json`
- asmdef は 07 §4 の参照関係どおり。Domain は `noEngineReferences: true`
- `config/game_balance_defaults.json` を `Assets/NagaRace/Config/` へ移動（M0 実施済み。正本は Assets 側のみ）

---

## Milestone 1 — Race Core

### 作業
- CharacterStats
- RaceCourse
- FlowSegment
- Racer
- RaceSimulator
- Seed RNG
- 8 racer simulation
- 簡易順位

### 表示
カプセル / 色板でよい。

### 完了条件
- 8体がゴール
- Seed固定で同結果
- 能力差が長期的に順位へ影響

### 【v1.1】補足
作業に追加:
- 固定Tick（20Hz）の RaceSimulator と Tick番号ベースのイベント列
- IRandomProvider + PCG32 + サブストリーム派生（04 §1.2）
- FlowGraph のバリデーション（DAG・合流・見せ場の有無）と progress 自動計算（04 §2）
- 横位置（lateral）・詰まり・簡易接触（04 §5〜6）。妨害は含めない
- 性格による進路選択差（慎重 / 標準 / 勝負師 の3種で可）
- RaceConditions（天候・水量）を入力として受ける（補正は設定値のみ）
- テスト用固定8体 + Seed生成NPC
- テストコース 3本（直線基調 / 分岐あり / 激流・狭所あり）を JSON で用意
- Debug表示: Gizmo または簡易 OnGUI で各出走者の位置・順位・Segment を表示

完了条件に追加:
- 10 の A-01〜A-06 が EditMode テストで通る
- 葉脈炉・葉力消費・耐久ダメージ・Lost は M1 では実装しない（M3 / M4）。ただし RacerState にフィールドの置き場は用意してよい

---

## Milestone 2 — Race View

### 作業
- Spline river
- RacerView
- カメラ
- HUD
- ミニマップ簡易
- Splash placeholder

### 完了条件
「自分のキャラがどれか」「何位か」が常に分かる。

---

## Milestone 3 — 葉力 / 葉脈炉

### 作業
- LeafPower
- Furnace state machine
- Safe
- Risk
- OverFlow
- Blow
- Player Input
- AI炉使用（NPC）

### 完了条件
- いつ発動するかで順位が変わる
- 早すぎ / 遅すぎの違いがある
- 限界まで引っ張る緊張感がある

---

## Milestone 4 — Durability / Lost

### 作業
- Durability
- Fatigue
- damage
- retirement
- lost evaluation
- album record

### 完了条件
- 危険表示あり
- 低危険レースで不条理ロストしない
- ロストキャラがアルバムに残る

【v1.1】アルバム画面は Phase2（03 §1）。M4 では「アルバム用の記録データが保存され、現役一覧から外れる」ことを完了条件とする。

---

## Milestone 5 — Training

### 作業
- Game calendar
- Weather
- Training definitions
- training resolution
- condition
- fatigue
- body trait accumulation

### 完了条件
- 10〜20ターン育成するとキャラの方向性に差が出る
- 同じコマンド連打だけが最適にならない

---

## Milestone 6 — Growth

### 作業
- Growth profile
- phases
- peak
- decline

### 完了条件
- 出走時期 / 引退時期を考える理由が生まれる

---

## Milestone 7 — Parent Tree / Grafting

### 作業
- Lineage
- ParentTree
- factors
- grafting
- 3 offspring candidates
- mutation

### 完了条件
- 親の特徴を感じる
- しかし完全コピーではない
- 「次はこの組み合わせ」と考えたくなる

【v1.1】接ぎ木画面は Phase2（03 §1）。M7 はロジックと EditMode テスト、Debug Balance 画面からの実行で確認する。

---

## Milestone 8 — UI Loop

### 作業
- Home
- Character
- Training
- Calendar
- Entry
- Race
- Result
- Lost / Retirement 演出（【v1.1】Album 画面は Phase2 のため除外）

### 完了条件
新規ゲームから20ターン以上遊べる。

---

## Milestone 9 — Art Vertical Slice

正式キャラ:
- モミジ
- イチョウ
- 悪葉っぱ

正式コース:
- 小川1本

### 完了条件
ゲームの最終イメージを判断できる。

---

## Milestone 10 — Internal Alpha

### 作業
- balance
- save migration
- logs
- crash-safe
- performance
- tutorial

### 完了条件
第三者へビルドを渡して説明なしで遊べる。

---

# Claudeへの重要指示

一度に全Milestoneを実装しない。

各Milestone:
1. Plan
2. 実装
3. Tests
4. Editor確認方法
5. ユーザー確認
6. 次Milestone

の順。
