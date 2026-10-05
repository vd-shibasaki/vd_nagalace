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

### 【v1.1】M2 実装メモ（2026-10-05）
- Race シーン（`Assets/NagaRace/Scenes/Race.unity`）を独立 Scene として作成（07 §3）
- 川: コース進捗を中心線（ゆるい S 字）へ写像する仮 Spline。分岐区間は分岐点・合流点で中心線へ戻る膨らみで左右に分ける。正式コースのアート時は手置き Spline へ差し替える
- 自キャラの識別: 一回り大きい体・頭上の回転マーカー・足元の黄色リング・頭上の名前ラベル・ランキング行の黄色ハイライト・ミニマップの縁取り
- カメラ: 自キャラ追従（既定）/ 集団全体 / ゴール後。カメラはレース時間で追従させ、倍速再生でも見失わない
- HUD: 順位（大）・名前・タイム・ランキング8行・ミニマップ・区間名と残り距離・速度・通知（流れに乗った／接触／姿勢崩れ／分岐／ゴール）・リザルト。セーフエリア対応
- 水しぶき: 姿勢崩れ・接触・激流/滝への進入でバースト
- 検証: PlayMode テストで全コース・全フレーム自キャラが画面内にいること（H-01）を自動確認。描画ありで実行するとスクリーンショットを保存できる
- 未対応（次工程）: 葉力ゲージ・葉脈炉 UI（M3）、耐久表示（M4）、日本語フォントの正式採用（M8。現在は組み込みフォント＋OSフォールバック）

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

### 【v1.1】M3 実装メモ（2026-10-05）
- 葉力: 基礎 0.7/秒（当初 2.5/秒では1レース3回以上使え「何回使うか」の勝負になったため削減）+ 後方ボーナス + 葉力チャンス（3〜5回、受付1.2秒、中央で押すほど多い、1回の受付で1度だけ）
- 葉脈炉: 04 §8.1 のステートマシン（Ready → Ignition → ActiveSafe → ActiveRisk(OVER FLOW) → Cooldown / Broken）。故障抽選は RiskZone 中のみ
- 調整: riskRampPerSecond 0.12 → 0.2、rampMax 1.35 → 1.3（引っ張り得が大きすぎたため）。加速レンジ [1.5, 9]（炉の始動時の伸びに加速が効くように）
- NPC / おまかせ炉: 進捗50%以降または葉力95以上で始動、AI作戦に応じ SafeDuration の 70/95/115% で解除
- 計測（標準コース、全能力60）: 炉を使わない 平均着順7.1 / 中盤以降に溜まるたび使う 3.8 / ゴール直前まで温存 5.8。超過0.5秒→2.5秒で故障 15件→193件
- 入力: Space / ゲームパッド南ボタン / 画面ボタンで炉、F / 西ボタン / 画面のチャンス枠で葉力チャンス。入力は Tick 付きで記録し、再生で同じ結果になる
- Blow / Critical の耐久ダメージ・リタイア・Lost は M4

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

### 【v1.1】M4 実装メモ（2026-10-05）
- 耐久ダメージ: 接触1 / 姿勢崩れ1 / 炉故障（Minor 2・FurnaceDamage 4・Blow 15・Critical 25）/ 危険区間の事故 20。頑丈で軽減（×1.4〜0.6）。耐久0でリタイア
- 事故: 危険度3以上の Segment で姿勢を崩したとき抽選。耐久が十分なら確率×0.2、耐久40未満なら×2（「低耐久等の条件でのみ」）
- Lost 候補は3事象のみ: 炉の Critical / 炉の Blow で耐久0 / 事故で耐久0。さらに「コース危険度3以上 or 出走時の耐久20未満」、ハードガード「危険度2以下かつ耐久40以上は0%」。妨害単独は対象外（妨害スキル自体は未実装）
- 出走前の危険度: 安全 / 注意 / 危険 / 極危険。Lost の可否は RaceSimulator と同じ判定を使い、「安全」「注意」では Lost しないことをテストで保証
- 計測（自キャラが炉を3秒超過まで引っ張る）: 危険度2コースは耐久30以上で Lost 0% / 危険度3コースで健康なら Lost 約1.7% / 耐久15・疲労80なら 15〜30%
- レース後: 耐久・最大耐久（1レースで25以上のダメージは大怪我として最大耐久が低下）・疲労（基本12 + 炉稼働秒×0.8 + 超過秒×3）・炉耐久・戦績を CharacterState へ反映。Lost は Roster で現役から外しアルバムへ
- 画面: 出走前の危険度パネル（Lost の可能性があるレースは Space / クリックで確認してから出走）、耐久ゲージ、リタイア / LOST 表示、ロスト専用の静かな画面（スキップ可）
- バランス再調整: 流速レンジ [0.93, 1.07]（事故による波乱が加わったため）

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

### 【v1.1】M5 実装メモ（2026-10-05）
- 暦: 1週4日・1季12日・1年48日。天気は世界 Seed と日付で決まり（セーブ・ロードで変わらない）、季節ごとの出現率を持つ
- ならし10種は `Assets/NagaRace/Data/Trainings/trainings.json`。効果キーは 05 §2.1 の対応表（stats.* / affinity.* / furnace.*）
- 伸び = 基礎2.4 × 重み × 成長フェーズ × 天候 × 調子 × 疲労（40超で低下、100で0.35倍）× 連続実施（1.0 / 0.85 / 0.7 / 0.55）× 伸びしろ（1 − (値/100)^1.6）× 乱数（0.7〜1.3）。端数は乱数で切り上げ
- 怪我: 耐久リスク × 4%（疲労で最大3倍）。極端な条件（耐久20未満かつ疲労70以上）の激流ならしは確認が必須で、耐久が尽きたときだけ Lost 判定
- 体質6種: 累積値60で獲得。長所と短所を持つ（例: 乾き葉 = 流速+4・炉出力+3・頑丈−4）
- 計測（15日、能力45スタート）: スピード型 流速+18 / 頑丈型 頑丈+17 / 日向ならし連打は合計+24.5（休養を挟むスピード型 +36.7 より低く、疲労90）

---

## Milestone 6 — Growth

### 作業
- Growth profile
- phases
- peak
- decline

### 完了条件
- 出走時期 / 引退時期を考える理由が生まれる

### 【v1.1】M6 実装メモ（2026-10-05）
- 成長タイプ4種（早熟・普通・晩成・持続）。各フェーズの日数は誕生時にレンジから決まる
- 育成係数: 若葉0.8 / 成長1.3 / 最盛1.0 / 紅葉0.55 / 枯葉0.25
- レース速度: 若葉0.985 / 成長0.995 / 最盛1.0 / 紅葉0.99 / 枯葉0.97（RaceEntrant.SpeedMultiplier）
- 衰え: 紅葉期は毎日30%、枯葉期は60%で流速・加速・安定のいずれかが−1
- → 「伸びる時期に育て、最盛期に勝負し、紅葉期に引退を考える」判断が生まれる

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
