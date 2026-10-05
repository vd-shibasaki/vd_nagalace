# 04. レースシミュレーション仕様

> **v1.1 改訂（2026-10-02 仕様レビュー）**
> 実装に必要な単位・Tick・乱数・横位置（Lane）・進捗・能力の役割・葉脈炉ステートマシン・Lost判定条件を追記した。
> 追記箇所には【v1.1】を付ける。数値はすべて暫定値で `Assets/NagaRace/Config/game_balance_defaults.json` から調整する。
> レビュー経緯は `14_SPEC_REVIEW.md` を参照。

## 1. 基本

- 出走: 8体
- 基本時間: 45〜90秒（通常大会）。【v1.1】最高峰 MOST HIGHER RIVER のみ 90〜110秒を許容（葉しょう記録 01:42.186 と整合させるため）。大会ごとに `targetDurationSeconds` を持つ
- 直接ステアリングなし
- 内部シミュレーションTickと描画を分離
- Seed付き疑似乱数
- 同Seed・同初期状態・同プレイヤー入力なら同結果を目指す
- 【v1.1】プレイヤーの自キャラは1レースにつき1体（MVP）。残り7体はNPC（§13）

### 1.1 単位とTick【v1.1】

| 項目 | 定義 |
|---|---|
| 距離 | ゲーム内メートル（m）。標準コース 1,200〜2,400m |
| 速度 | m/s。全体平均の目安 約26m/s（1,200m≒45秒、2,400m≒90秒） |
| シミュレーションTick | 固定 20Hz（dt = 0.05秒）。`race.tickRate` で変更可 |
| 時間の記録 | イベント・プレイヤー入力は **Tick番号（int）** で記録する。秒は表示用の換算値 |
| ゴールタイム | ゴールを跨いだTick内で線形補間し、ミリ秒精度で算出 |

描画側はTick間を補間して表示する。Domain は `Time.deltaTime` を参照しない。

### 1.2 決定性と乱数【v1.1】

- 乱数は Domain 内の `IRandomProvider` 経由のみ。実装は自前の Seeded PRNG（PCG32 等）とし、`System.Random` / `UnityEngine.Random` は使わない（実装差・バージョン差を避けるため）
- `raceSeed` から用途別・出走者別のサブストリームを派生させる（例: SplitMix64 で `raceSeed ⊕ hash(entrantIndex, streamId)`）
  - streamId: `Route` / `Movement` / `Contact` / `Furnace` / `Event`
  - 目的: プレイヤーの炉操作の有無で、他の出走者の進路抽選が連鎖的に全部変わることを防ぐ（「早すぎ / 遅すぎ」の比較を公平にする）
- 再現性の保証範囲: **同一ビルド・同一プラットフォーム**で同Seed・同入力 → 同結果
- クロスプラットフォームでの完全一致（浮動小数点差）は非同期PvP着手時に再評価する（固定小数点化 or サーバ検証）。MVPでは保証しない
- レースTick中の出走者の更新順は `entrantIndex` 昇順で固定する

## 2. コース内部表現

川をNavMesh任せにせず、
**Flow Graph + Spline** で表現する。

### FlowSegment

```text
id
length              // m
baseFlowSpeed       // m/s（目安 16〜36）
waterDepth          // 0〜1（浮力の効き）
roughness           // 0〜1（荒れ。浮力・安定で軽減）
danger              // 1〜4（安全/注意/危険/極危険）
width               // m（横位置の余裕、接触頻度に影響）
tags[]
nextSegments[]
progressStart       // 【v1.1】コース全体の進捗 0〜1（自動計算）
progressEnd         // 【v1.1】
```

### コースグラフの制約【v1.1】

- 開始Segment・ゴールSegmentはそれぞれ1つ
- 分岐は必ずどこかで合流する（行き止まり禁止、ループ禁止 = DAG）
- 分岐した各経路は同じ進捗区間を覆う。`progressStart/End` は分岐点・合流点の進捗から、経路内の距離比で線形補間して自動計算する
- 各コースは「見せ場」となる分岐 または `rapid / waterfall / whirlpool / narrow` Segmentを最低1つ含む（02 §14）。コース読込時にバリデーションする

### タグ例
- calm
- rapid
- whirlpool
- narrow
- waterfall
- mud
- shortcut
- collision_heavy

### 2.1 天候・水量【v1.1】

レース入力として `RaceConditions { weather, waterLevel }` を受け取る（CLAUDE.md §7「天候」を満たすため）。

| 入力 | 値 | 影響（暫定） |
|---|---|---|
| waterLevel | Low / Normal / High | baseFlowSpeed ×0.92 / ×1.0 / ×1.08、High は roughness +0.15 |
| weather | Sunny / Cloudy / Rain / HeavyRain / Wind | HeavyRain は全Segmentの danger +1（上限4）、特性・適性の天候補正トリガー |

M1 では入力を受け取り、補正係数はすべて設定値（既定 1.0 近傍）で動けばよい。

## 3. 進路選択

各キャラは分岐時にスコアリング。

概念式:

```text
routeScore =
  expectedSpeed
+ suitability
+ personalityBonus
+ positionStrategy
- dangerAversion
- collisionRisk
+ randomNoise
```

【v1.1】`randomNoise` の大きさは **判断** が高いほど小さい（`route.noiseByJudgement` = [低判断時, 高判断時]）。
判断が高い個体ほど「その個体にとって本当に速いルート」を選べる。

### 性格
慎重:
dangerAversion 高。

勝負師:
shortcut 加点。

悪葉っぱ:
opponentDensity の高いルートを好む場合あり。

【v1.1】性格（Personality）は特性（Trait）とは別の値として1個体に1つ持つ（02 §5.1）。
レース前にプレイヤーが選ぶ **AI作戦（安全/標準/攻める）** は性格の上に重ねる補正で、dangerAversion と葉脈炉の引っ張り具合（NPC/おまかせ時）に効く。

## 4. 移動速度

初期概念式:

```text
effectiveSpeed =
 baseCourseFlow
 * characterFlowRate
 * courseSuitability
 * conditionModifier
 * fatigueModifier
 * statusModifier
 + accelerationEffect
 + boostEffect
```

数値は `game_balance_defaults.json` で調整。

### 4.1 計算手順（実装用）【v1.1】

```text
targetSpeed =
  segment.baseFlowSpeed * waterLevelMod * laneFlowMod
  * flowRate(流速)                 // 例 0.90〜1.10 を線形補間
  * affinityMod(タグ・距離・天候)   // 例 0.95〜1.05
  * conditionMod(調子)
  * fatigueMod(疲労)
  * roughnessMod(荒れ, 浮力)        // 1 - roughness * lerp(0.15, 0.03, 浮力)
  * statusMod(減速・スタン等)
  * furnaceMod(葉脈炉)             // §8
  * jitter(Segment毎の微小ゆらぎ)

currentSpeed は targetSpeed へ accelRate(加速) で近づく（m/s²）。
減速は即時〜高速。加速は能力依存。
```

「+ accelerationEffect」は、接触・Segment切替・スタン後の **復帰の速さ** として表現する（加算値ではなく追従速度）。

### 4.2 主能力の役割【v1.1】

| 能力 | id | レース内での役割 |
|---|---|---|
| 流速 | flowSpeed | targetSpeed の基礎倍率 |
| 加速 | acceleration | currentSpeed の追従速度（m/s²）。分岐後・接触後・炉始動時の伸び |
| 浮力 | buoyancy | roughness / waterDepth による減速の軽減 |
| 安定 | stability | 接触時の減速量・スタン時間・姿勢崩れ確率の軽減 |
| 頑丈 | toughness | 受けるダメージ（耐久減少量）の軽減 ※旧称「耐久」。02 §4 参照 |
| 判断 | judgement | ルート選択ノイズの軽減、詰まり回避（横移動開始）の早さ |

全能力が「どこかで効く」ことを M1 のテストで確認する。

## 5. 追い抜き

完全物理接触だけに依存しない。

- Lane offset
- Character radius
- Overlap score
- Speed difference

を使い、見た目は物理的に接触しても内部結果は制御可能にする。

### 5.1 横位置（Flow Lane）モデル【v1.1】

- 各出走者は `lateral`（-1.0〜+1.0 の連続値。0 = 川の中央）を持つ
- 実際の横幅は `segment.width` で換算する。narrow は幅が狭く、同じ lateral 差でも接触しやすい
- 流れは中央が速い（`laneFlowMod`: 中央 +3%、端 -3% 程度の暫定値）
- 前方 `blockDistance` 以内・横方向で重なる相手がいると、速度は相手の速度で頭打ち（詰まり）
- 詰まった個体は 判断 に応じた反応時間後、空いている側へ `targetLateral` を移す（AIが Flow Lane を選ぶ）
- 横移動速度は能力（加速・安定）で差を付ける
- スタート時の初期 lateral は枠順で等間隔に配置。**枠順による有利不利は統計的に出ないこと**（10 A-03）

## 6. 接触

接触結果:
- 軽い減速
- 姿勢崩れ
- 流路変更
- 耐久微減

重大事故は危険Segment + 高速 + 低耐久等の条件でのみ発生。

【v1.1】接触は「詰まり状態で速度差が閾値以上」または「横移動中に重なった」場合に判定する。
減速量・スタン時間は 安定、耐久減少は 頑丈 で軽減する。通常の接触単独で Critical / Lost は発生しない。

## 7. 葉力

葉力ゲージ:
0〜100。

獲得:
- 良い流れ
- タイミング入力
- 特性
- 後方ボーナス等

MVP:
タップ連打競争にしない。

入力イベント例:
「葉力チャンス」が3〜5回発生。
短いタップ / スワイプで追加取得。

何もしなくても基礎量は溜まる。

【v1.1】暫定値: 基礎 2.5/秒 → 何もしなくても約20秒で始動ラインの50に届く。
後方ボーナスは順位5〜8位で加算（追い上げの機会を作る）。葉力チャンスは M3 で実装し、M1 では基礎獲得のみ。

## 8. 葉脈炉

### 起動条件
- 葉力 >= 50（仮）
- 故障していない
- クールダウン外

### 起動
葉力を初期消費し、継続中も消費。

### 効果
- 速度
- 加速
- 接触耐性
- 一部妨害耐性

### 臨界
キャラごとにSafeDuration。

例:
`Safe 3.5 sec`

SafeDurationを超えるとRiskZone。

RiskZone:
毎Tick故障リスクが増加。

概念:

```text
risk =
 baseOverrunRisk
 * overrunSeconds
 * damageModifier
 * furnaceFragility
 * fatigueModifier
```

乱数により結果:
- Safe
- MinorDamage
- FurnaceDamage
- Blow
- Critical

### 8.1 ステートマシン【v1.1】

```text
Ready ──(始動入力 & 葉力>=50)──> Ignition ──(起動ラグ経過)──> Active.Safe
Active.Safe ──(経過 >= SafeDuration)──> Active.Risk   ← UI表示「OVER FLOW」
Active.* ──(解除入力 or 葉力0)──> Cooldown ──(冷却時間経過)──> Ready
Active.Risk ──(故障抽選に当選)──> Failure(重さ別) ──> Cooldown or Broken
Broken: このレース中は再始動不可
```

- **OVER FLOW** は「RiskZone に入っている状態」の表示名とする（独立した結果ではない）
- 葉力が0になったら自動解除（故障ではない）
- 故障抽選は Active.Risk 中のみ。Safe中に解除すれば絶対に故障しない（10 B-02）

### 8.2 炉能力の変換（暫定）【v1.1】

| 炉能力 | id | 変換 |
|---|---|---|
| 出力 | output | 速度ブースト量 +20%〜+36% |
| 起動 | startup | 起動ラグ 0.6→0.2秒、始動時の葉力消費 20→10 |
| 臨界 | criticalDuration | SafeDuration 2.5→4.5秒（50で3.5秒） |
| 冷却 | cooling | 解除後のクールダウン 14→7秒 |
| 炉耐久 | durability | furnaceFragility 1.5→0.6 |

能力値0〜100の線形補間。範囲はすべて設定ファイルで持つ。

### 8.3 「長く引っ張るほど強い」【v1.1】

ブースト量は経過時間で増える（ramp）。

```text
boost = outputBoost * ramp(t)
ramp: 始動時 0.7 → SafeDuration到達時 1.2 → RiskZone中も緩やかに増加し上限 1.35
```

→ 安全圏ギリギリで止めるか、RiskZoneへ踏み込んで伸ばすか、の判断が生まれる。

### 8.4 故障抽選と重さ【v1.1】

```text
hazardPerSecond = riskRampPerSecond * overrunSeconds
                  * furnaceFragility * damageModifier * fatigueModifier
Tickごとの発生確率 = hazardPerSecond * dt
```

- damageModifier: 耐久（現在値/最大値）が低いほど上昇
- fatigueModifier: 疲労が50を超えると上昇
- 発生時の重さ（Minor / FurnaceDamage / Blow / Critical）は重み抽選。overrunSeconds が長いほど重い側へ寄る

結果の名称は以下に統一する（旧 §9 の Minor / Blow / Critical と同じものを指す）。

| 結果 | 内容 |
|---|---|
| MinorDamage | 炉停止・一時減速。クールダウン後に再始動可 |
| FurnaceDamage | 炉停止。このレース中は再始動不可（Broken）。炉耐久にレース後ダメージ |
| Blow | 大失速・耐久大幅減。耐久0ならリタイア |
| Critical | Lost判定（§9.1）へ。条件を満たさなければ Blow として扱う |

### 8.5 おまかせ炉【v1.1】

- レース前の設定（既定OFF）。ON の場合、自キャラの葉脈炉を NPC と同じ炉AI が操作する（§13）
- レース中にプレイヤーが始動・解除を入力した時点で、そのレースは手動に切り替わり、以後AIは操作しない
- 手動切替はプレイヤー入力として Replay に記録する（Tick番号）
- 炉AI の判断は Seed とレース状態だけで決まり、再現性を保つ

## 9. ブロー

### Minor
- 速度低下
- 炉停止

### Blow
- 大失速
- 耐久大幅減
- リタイア可能性

### Critical
ロスト判定候補。

ただし:
- 耐久十分
- 低危険コース

ではCriticalを極めて低くする。

### 9.1 Lost判定条件（確定ルール）【v1.1】

「低リスク行動で突然ロストさせない」をコード上のガードとして実装する。

Lost判定に進むのは、次の **すべて** を満たす場合のみ。

1. 葉脈炉の Critical、またはコース上の重大事故（danger 3 以上のSegment）が発生した
2. かつ、次のいずれか
   - コース危険度（最大Segment danger、天候補正込み）が 3（危険）以上
   - 耐久が危険帯（`durability.damagedMin` 未満 = 20未満）
3. かつ、原因が妨害スキル単独ではない

満たさない場合は Blow に降格する。判定に進んでも確率（`lost.chanceOnCandidate`）であり確定ではない。

**ハードガード:** コース危険度 2 以下 かつ 耐久40以上 の場合、Lost確率は常に0（10 C-01）。

### 9.2 出走前の危険度表示【v1.1】

出走前に 安全 / 注意 / 危険 / 極危険 の4段階を表示する。算出入力はコース危険度・天候・耐久帯・疲労・調子。
表示が「安全」「注意」のレースでは、§9.1 により Lost が発生しないことを保証する。

## 10. 妨害

悪葉っぱAI / スキル。

### 体当たり
対象:
隣接相手。

結果:
- 相手減速
- 自分もバランス減
- 双方耐久微減

### 泥はね
- 視界 / 判断デバフ
- 数秒

### 渦
- FlowSegment上に短時間DangerZone追加

### 葉力妨害
- 相手葉力獲得率低下

PvPで妨害によるCritical Lostは発生させない。

【v1.1】ルールを単純にするため、PvPに限らず **NPC戦でも妨害単独では Critical / Lost を発生させない**。妨害によるダメージで耐久は1未満にならない。
妨害の実装は M3 以降（M1 では行わない）。

## 11. ゴール

着順判定はSpline上の進捗値 + Finish crossing time。

同着対応:
ミリ秒順位。

【v1.1】
- レース中の順位: `progress`（Segmentの progressStart〜End を経路内距離で補間した 0〜1）の降順
- ゴール後: ゴールタイム（ミリ秒、Tick内補間）の昇順
- ミリ秒まで同じ場合: `entrantIndex` 昇順で確定（同着表示はするが内部順位は一意）
- リタイアは最下位扱い、リタイア順の逆順
- 全員ゴール or リタイア、もしくは `race.timeoutSeconds` 経過で終了（無限ループ防止）

## 12. Replay

MVP後半で、
- seed
- entrant snapshots
- player inputs

を保存し、簡易リプレイ可能にする。

非同期PvPの基礎になる。

【v1.1】player inputs は Tick番号で記録する（08 §6）。

## 13. NPC出走者【v1.1】

- 大会定義（TournamentDefinition）に NPC の強さ帯（能力レンジ）と系統プールを持たせる
- NPC は SpeciesDefinition の baseRanges から、レースSeedとは別の **大会Seed** で生成する（同じ大会に同じ顔ぶれが出せるように）
- NPC の葉脈炉は AI が操作する（M3）。AI作戦に応じて SafeDuration の 70% / 95% / 115% 付近で解除
- M1 ではテスト用の固定8体 + Seed生成の両方を用意する

## 14. M1 実装で確定した事項【v1.1】

M1 のバランス調整（10 A-02〜A-05 を満たすための調整）で、上記仕様から次の点を具体化・変更した。数値はすべて設定ファイルにある。

| 項目 | 内容 | 理由 |
|---|---|---|
| 姿勢崩れ（Stumble） | 荒れた水面では毎Tick `roughness × stumbleRatePerSecond × 安定補正` の確率で姿勢を崩し、減速＋短いスタン | 接触だけでは「安定」の差が結果に出ず、レースの波乱も不足したため。荒れた区間ほど起きるので、負けた理由が説明できる |
| 区間ごとの流れの当たり外れ | Segment に入るたびに速度 ±4% のゆらぎ。+2.5% 以上なら「流れに乗った」イベント | 能力差だけで順位がほぼ固定になっていたため（流速+15 で勝率 80〜100%） |
| 判断と流れ | 判断が高いほど、上のゆらぎで良い側を引きやすい（一様乱数を指数で偏らせる） | 進路選択だけでは判断の効きが小さすぎたため |
| 進路の見積もり | 所要時間に姿勢崩れの期待損失を含める | 浮力・安定が低い個体は荒れたルートを避けるのが正解、という判断を成立させるため |
| 横位置の流速差 | 中央・端の流速差は既定 0（設定で変更可） | 全員が中央へ寄って外枠が詰まり、枠順の偏りが出たため（中央枠の勝率が約25%） |
| レーン復帰 | 詰まりを避けた後は、中央ではなく自分の枠のレーン（HomeLateral）へ戻る | 同上 |
| スタート速度 | 目標速度の 30% から加速 | 加速の差をスタートでも出すため |

M1 時点の標準コース（`test_branch`）での計測値: 流速+15 の1着率 39%、全能力+15 の1着率 62.5%・3着以内 88.5%、枠別1着率 9〜16.5%。

## 15. 妨害スキルの実装（2026-10-05）【v1.1】

- 使うのは悪葉っぱ系統（`obstruction.speciesIds`）の出走者だけで、AI が判断する。プレイヤーが悪葉っぱを育てた場合は「おまかせ炉」ON のときに AI が使う（手動のスキル入力は M9 以降に検討）
- スキル4種（04 §10）: 体当たり（隣の相手を減速・スタン、自分もバランスを崩す）/ 泥はね（後ろの相手の判断を3秒鈍らせる: 進路ノイズ×3・詰まりへの反応+0.5秒・速度−2%）/ 渦おこし（後方5〜20mの水面を4秒間荒らす）/ 葉力妨害（相手の葉力の溜まり方を5秒間×0.4）
- 代償: 葉力 8〜15、スキルごとのクールダウン 20〜30秒、全体クールダウン 14秒。炉の稼働中は体当たりの影響が半分（「一部妨害耐性」）
- 妨害によるダメージは耐久を1未満にしない。渦の中での姿勢崩れは妨害扱いで、重大事故・Lost につながらない（10 C-03 をテストで確認）
- 計測: 悪葉っぱの平均着順は混成の出走表で中位（コースによって 4.7〜5.6 位 → 能力レンジを少し引き上げ）
