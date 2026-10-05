# ながレース — 統合要件定義・開発仕様書

> 自動生成ファイル。直接編集せず docs/ を編集して tools/build-master-doc.ps1 で再生成すること。

# 01. プロダクト要件定義

## 1. タイトル
**ながレース / NAGARE RACE**

## 2. ジャンル
育成レースシミュレーション / カジュアル観戦レース / コレクション

## 3. 対象プラットフォーム

### 第1段階
- Windows PC（開発・検証）
- iOS
- Android

### 第2段階
- Nintendo Switch
- PCストア展開

Switch固有開発は、MVPで商品性を確認してから着手する。

## 4. 画面方向
**横画面 / 16:9基準**

理由:
- 8体レースの横方向の流れを見せやすい
- 川・背景・順位表示との相性がよい
- 将来のSwitch展開とUI資産を共有しやすい

スマホのノッチ・セーフエリア対応を行う。

## 5. 想定プレイ時間

### 短時間
5〜10分:
- ならし
- 1〜3レース
- キャラ確認

### 中時間
20〜40分:
- 大会ローテーション
- 育成
- 接ぎ木
- 複数レース

### 長期
数週間〜数か月:
- 葉統作り
- MOST HIGHER RIVER制覇
- 葉しょう記録更新
- 図鑑・殿堂

## 6. ターゲット体験

「かわいい」「癒やされる」で入って、
「この個体を勝たせたい」で育て、
「ここで葉脈炉を使うか」で熱くなり、
「ロストしたけど忘れられない」で物語が残る。

## 7. コアループ

```text
ホーム
 ↓
天候・状態確認
 ↓
ならし / 休養 / 接ぎ木
 ↓
大会選択
 ↓
出走判断
 ↓
自動レース
 ↓
葉力蓄積
 ↓
葉脈炉判断
 ↓
結果 / ダメージ / 成長
 ↓
引退 or 続投
 ↓
次世代へ
```

## 8. 非目標

MVPでは以下を目標にしない。

- 本格操作レース
- オープンワールド
- MMORPG
- リアルタイム8人通信対戦
- リアル流体シミュレータ
- 写実的な葉っぱ生態シミュレーション
- 高難度アクションゲーム

## 9. 成功条件

### コアループ成功
テスターが、
- 自分の葉っぱに名前を付けたくなる
- もう1戦見たいと思う
- 育成方針を変えて試したくなる
- 葉脈炉発動を迷う
- 接ぎ木結果を楽しみにする

### ロスト成功
ロスト後の反応が
「意味不明に消された」
ではなく
「無理させた結果だけど惜しかった」
になること。

## 10. 収益化方針（商品化フェーズ）

Pay to Winを避ける。

候補:
- 広告削除
- 見た目スキン
- ホーム装飾
- 所持枠
- シーズンパス
- レース演出
- 記念フォトフレーム
- 追加ストーリー / コースパック

MVPでは課金機能を実装しない。

---

# 02. ゲームデザイン詳細

> **v1.1 改訂（2026-10-02 仕様レビュー）** 主能力「耐久」を「頑丈」に改名、性格と特性の分離、適性の定義、危険度4段階の定義を追記。追記箇所には【v1.1】を付ける。

## 1. 世界観

葉っぱ、花びら、木の実など「流れるもの」たちが、自分の流れを求めて各地の川を走る世界。

レースは危険な殺し合いではなく、文化・スポーツ・憧れ。

最高峰:
**MOST HIGHER RIVER**

伝説のレーサー:
**まつおうじ葉しょう**

記録（仮）:
`01:42.186`

## 2. 大会階層

1. 地方の小規模大会
2. Village Mountain City Cup
3. Mountain Shape Championship
4. Moon Mountain Masters
5. MOST HIGHER RIVER

最高峰はゲーム開始直後からワールドマップ遠景に表示する。

## 3. キャラクター系統

### モミジ
- 瞬発
- 高速
- 葉脈炉高出力
- 耐久低め

### イチョウ
- 安定
- 耐久
- 長距離
- 扱いやすい

### ハス
- 浮力
- 渦耐性
- 深水
- 高水量

### サクラ
- 軽量
- 回避
- 狭所
- 器用

### ドングリ / 木の実
- 重量
- 接触
- 安定
- 短距離は苦手

### 悪葉っぱ
- 妨害
- 接触
- 奇襲
- 不安定

## 4. 個体能力

MVPでは以下の6主能力を採用。

| 能力 | 内容 |
|---|---|
| 流速 | 通常速度 |
| 加速 | 流れ変更・復帰時の伸び |
| 浮力 | 荒れた水面、深水への強さ |
| 安定 | 接触、波、姿勢崩れへの強さ |
| 頑丈 | ダメージ耐性（【v1.1】旧称「耐久」） |
| 判断 | AIの流路選択精度 |

葉脈炉は別ステータス。

【v1.1】用語の整理（「耐久」の三重定義を解消）:

| 用語 | id | 意味 |
|---|---|---|
| 頑丈 | stats.toughness | 主能力。受けるダメージの軽減 |
| 耐久 | durability / maxDurability | HPに相当する現在値 / 最大値（§9） |
| 炉耐久 | furnace.durability | 葉脈炉の壊れにくさ |

能力表示ランク（暫定）: S 90以上 / A 75以上 / B 60以上 / C 45以上 / D 30以上 / E 30未満。内部値は整数0〜100。

### 葉脈炉
- 出力
- 起動
- 臨界
- 冷却
- 炉耐久

## 5. 隠し / 特性

能力値を完全公開しすぎない。

例:
- 激流好き
- 雨好き
- 風乗り
- 負けず嫌い
- 慎重
- 勝負師
- 接触嫌い
- 喧嘩葉
- 最後のひと伸び
- 逆境
- 冷却上手
- 二重葉脈

「育てたら特徴がわかってくる」余白を残す。

### 5.1 性格と特性の分離【v1.1】

上の例には「AIの判断傾向」と「能力補正」が混在しているため、データ上は分ける。

| 区分 | 個数 | 役割 | 例 |
|---|---|---|---|
| 性格 Personality | 1個体に1つ | レースAIの判断傾向（進路・炉の引っ張り方） | 慎重 / 標準 / 勝負師 / 負けず嫌い / 喧嘩葉 |
| 特性 Trait | 0〜数個 | 条件付きの能力補正 | 激流好き / 雨好き / 風乗り / 接触嫌い / 最後のひと伸び / 逆境 / 冷却上手 / 二重葉脈 |

性格は育成（ぶつかり稽古等）で変化しうる。接ぎ木では「性格傾向」として継承される（05 §7）。

### 5.2 適性 Affinity【v1.1】

レースの courseSuitability（04 §4）の元になる値。0〜100、表示はS〜E。

| 区分 | 項目（MVP） |
|---|---|
| 地形 | 激流 rapid / 渦 whirlpool / 狭所 narrow / 深水 deep |
| 距離 | 短 sprint（〜1,400m）/ 中 middle / 長 long（2,000m〜） |
| 天候 | 晴 / 雨 / 風（特性で補う。MVPでは特性経由のみでもよい） |

系統ごとの初期傾向は SpeciesDefinition の affinityBias で持つ。

## 6. 成長

### フェーズ
- 若葉
- 成長
- 最盛
- 紅葉
- 枯葉

固定日数ではなくキャラごとの成長タイプで幅を持たせる。

【v1.1】成長タイプ（growthType）は 早熟 / 普通 / 晩成 / 持続 の4種（暫定）。各フェーズのターン数レンジと成長係数を持ち、接ぎ木で「成長型」として継承される（05 §7）。

MVPの初期値例:
- 若葉: 8〜12ターン
- 成長: 18〜30ターン
- 最盛: 20〜35ターン
- 紅葉: 12〜20ターン
- 枯葉: 以降

全て外部データで変更可能にする。

## 7. 時間進行

リアル時間待ちを基本にしない。

**1アクション = 1ゲーム内日**

アクション:
- ならし
- 休養
- 大会
- 特殊イベント

プレイヤーは好きなペースでゲーム内時間を進める。

【v1.1】暦（2026-10-02 決定、値は設定で変更可）:

| 単位 | 長さ |
|---|---|
| 1週 | 4日 |
| 1季 | 3週 = 12日（春・夏・秋・冬） |
| 1年 | 4季 = 48日 |

- 大会はおおむね週1回（4日ごと）開催。1年で約12大会
- 成長フェーズ（§6）の若葉〜最盛の合計は 46〜77日 ≒ 約1〜1.6年。1個体の現役期間はおおむね1〜2年分の大会になる
- 大会に出ると、その日は1行動を消費する

## 8. 調子・疲労

### 調子
5段階:
- 絶好調
- 好調
- 普通
- 不調
- 絶不調

### 疲労
0〜100。

疲労増加:
- 強いならし
- レース
- 葉脈炉酷使

疲労が高いほど:
- 成長効率低下
- 判断ミス
- 故障率上昇

## 9. 耐久と傷

耐久は現在値 / 最大値。

最大耐久そのものがキャリアを通して低下するケースも設ける。

例:
大怪我:
`100/100 → 回復後 82/82`

「使い込んだエース」の歴史が数値にも残る。

## 10. 引退

プレイヤーが任意に引退できる。

メリット:
- 因子の安全な保存
- 親木への系統登録
- 殿堂
- 思い出写真

引退せず走らせ続ける:
- 記録を狙える
- ただしロストリスク増

## 11. ロスト

危険度を事前に見せる。

例:
- 安全
- 注意
- 危険
- 極危険

ロスト候補:
- 激流事故
- 葉脈炉重大破損
- 耐久0での大事故

通常の低危険レースでの即死級ロストは禁止。

【v1.1】危険度4段階の意味:

| 表示 | Lost | 目安 |
|---|---|---|
| 安全 | 発生しない | 低危険コース・耐久十分 |
| 注意 | 発生しない | 疲労・軽傷などで故障しやすい |
| 危険 | 条件次第で候補 | 危険コース、または耐久20未満 |
| 極危険 | 候補になりやすい | 危険コース + 低耐久 + 高疲労 等が重なる |

具体的な判定条件は 04 §9.1。

## 12. 思い出アルバム

ロスト / 引退後もキャラはゲームから消えた感覚にしない。

保存:
- 写真
- 生涯成績
- 勝利
- ライバル
- 好きだったコース
- 葉脈炉最大出力
- 最終レース
- 系譜
- コメント

## 13. 悪葉っぱ

「強い妨害キャラ」ではなく、戦術型。

スキル:
- 体当たり
- 泥はね
- 渦発生
- 流路ブロック
- 葉力減衰
- どんぐり落とし

使用には:
- 葉力
- クールダウン
- 自身のバランス悪化

などの代償を付ける。

## 14. レースのドラマ

毎レースに最低1回、
「順位が動きそうな見せ場」を作る。

例:
- 分岐
- 急流
- 滝
- 渦
- 狭い岩間
- 最終直線

完全ランダム演出ではなく、コース設計に組み込む。

---

# 03. 画面・UI仕様

> **v1.1 改訂（2026-10-02 仕様レビュー）** SC-11/SC-12 を Phase2 のまま維持することを明記、Race Entry に「おまかせ炉」を追加、Training カード例の能力名を修正。追記箇所には【v1.1】を付ける。

## 1. 画面一覧

MVP必須:

| ID | 画面 | 必須 |
|---|---|---|
| SC-01 | Boot | ○ |
| SC-02 | Title | ○ |
| SC-03 | Home | ○ |
| SC-04 | Character List | ○ |
| SC-05 | Character Detail | ○ |
| SC-06 | Training | ○ |
| SC-07 | Race Calendar | ○ |
| SC-08 | Race Entry | ○ |
| SC-09 | Race | ○ |
| SC-10 | Race Result | ○ |
| SC-11 | Parent Tree / Grafting | Phase2 |
| SC-12 | Album | Phase2 |
| SC-13 | World Map | Phase2 |
| SC-14 | Settings | ○ |
| SC-15 | Debug Balance | 開発用 |

【v1.1】SC-11 / SC-12 は **Phase2 のまま**とする（2026-10-02 決定）。MVPでの扱い:
- アルバム: 画面は作らない。ロスト・引退時の記録（08 の Album データ）は M4 から保存する
- 接ぎ木: ロジックは M7 で実装し、Debug Balance 画面（SC-15）から実行・確認する
- MVP検証項目4「ロストが思い出として機能するか」は §9 Lost / Retirement 演出（最後の写真・戦績）で確認する

## 2. Home

### 役割
ゲームの中心拠点。

### 表示
- 季節
- ゲーム内日付
- 天気
- 水量傾向
- 次の大会
- 育成中キャラ
- コンディション警告
- 親木園への入口
- アルバム入口

### ビジュアル
メニュー表ではなく「川辺の小さな拠点」。

葉っぱたちが:
- 眠る
- 日向ぼっこ
- 水に浮く
- 他キャラと話す

など短いIdleを行う。

## 3. Character Detail

上部:
- キャラ3D
- 名前
- 系統
- 世代
- 成長フェーズ
- 調子
- 耐久

タブ:
1. 能力
2. 適性
3. 葉脈炉
4. 特性
5. 戦績
6. 系譜
7. 思い出

### 能力
数字だけでなくS〜E程度の視認性を持たせる。
内部値は整数0〜100等でもよい。

## 4. Training

カードUI。

各カード:
- メニュー名
- 予想効果
- 疲労
- 耐久リスク
- 天候補正
- おすすめ度

例:
`激流ならし`
- 流速 +++
- 激流適性 +++（【v1.1】旧「胆力」。05 §2.1）
- 耐久 --
- 本日: 大雨 BONUS

実行前に危険度が高い場合のみ確認ダイアログ。

## 5. Race Calendar

大会一覧:
- 日付
- グレード
- 距離
- 水量
- コース種
- 推奨適性
- 賞
- 危険度

「次へ」だけの一本道にしない。

## 6. Race Entry

出走8体を表示。

自キャラ:
- 調子
- 適性
- 耐久
- 危険度
- AI作戦

AI作戦:
- 安全
- 標準
- 攻める

MVPでは3択。

【v1.1】おまかせ炉（ON/OFF、既定OFF）:
- ON の場合、AI作戦に従ってAIが葉脈炉を始動・解除する（04 §8.5）
- レース中にプレイヤーが炉を操作した時点で、そのレースは手動に切り替わる
- 前回の設定を記憶する

## 7. Race

### HUD
左上:
- 順位 3/8
- キャラ名

右上:
- ミニマップ
- 残距離 / セクション

下:
- 葉力ゲージ
- 葉脈炉ボタン
- 臨界カウント
- 耐久

通知:
- 流れに乗った
- 接触
- 特性発動
- 妨害
- WARNING

### 葉脈炉起動
画面中央を塞がない。
短いカメラ寄り + 葉脈発光。

### 臨界
- UI振動
- 警告音
- 色変化
- カウント

演出が強くてもキャラの位置を見失わないこと。

## 8. Result

表示:
- 順位
- タイム
- 獲得
- 能力成長
- 耐久変化
- 疲労
- 新特性
- 傷

ロスト時は通常のリザルト画面と分ける。

## 9. Lost / Retirement

ロスト演出:
- 過度に悲惨にしない
- 静かな水面
- 最後の写真
- 戦績
- 「思い出アルバムへ」

スキップ可能。

## 10. Parent Tree / Grafting

左:
親木A

右:
親木B

中央:
継承予測

表示:
- 系統
- 主因子
- 相性
- 予測幅
- 特異葉可能性（正確な確率を出さなくてもよい）

実行後:
若葉候補3体を提示。

## 11. World Map

下位大会から最高峰へ川がつながる構図。

MOST HIGHER RIVERは最初から表示するがロック。

## 12. レスポンシブ

基準:
1920x1080相当の16:9。

スマホ:
Safe Area。

Switch:
TV / 携帯モード双方で読める文字サイズ。

## 13. 参考

`art_reference/nagarace_loose_style.png`
- キャラの雰囲気

`art_reference/nagarace_screen_samples.png`
- 情報配置の参考

画面内テキストは最終実装で可読性を優先し、画像生成サンプルの文字をそのまま使用しない。

---

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

---

# 05. 育成・親木・接ぎ木仕様

> **v1.1 改訂（2026-10-02 仕様レビュー）** ならしの効果語彙とパラメータの対応表、育成でのLost条件を追記。追記箇所には【v1.1】を付ける。

## 1. 育成思想

「ステータス+3」だけの古典的コマンド育成にしない。

プレイヤーは、
**今日の自然条件を見て、その葉っぱをどんな体質に仕上げるか**
を考える。

## 2. ならし

### 朝露浴
向く:
回復 / 冷却 / 浮力。

体質候補:
瑞々しい。

### 日向ならし
向く:
軽量 / 最高速 / 炉出力。

リスク:
乾燥。

### 風ならし
向く:
姿勢 / 加速 / 横流れ。

特性:
風乗り。

### 小川流し
向く:
基礎流速 / 判断 / 経験。

### 激流ならし
向く:
高速 / 胆力 / 激流適性 / 炉。

リスク:
傷 / 耐久減 / ロスト（極端な条件のみ）。

【v1.1】「極端な条件」= 耐久が危険帯（20未満）かつ 疲労が高疲労閾値（70）以上 の両方。
この場合のみ危険度「極危険」として確認ダイアログを必ず出し、承認後に限りLost判定を行う。それ以外の育成ではLostは発生せず、傷・耐久減まで。

### 渦ならし
向く:
旋回 / 渦耐性。

### 石間抜け
向く:
反応 / 回避。

### ぶつかり稽古
向く:
接触 / 妨害耐性。

性格変化候補:
負けず嫌い / 喧嘩葉。

### 葉脈炉同調
向く:
葉脈炉各能力。

### 休養
疲労・調子回復。

### 2.1 効果語彙 → パラメータ対応表【v1.1】

上記の「向く」には主能力（02 §4）にない語が混在しているため、実装上は以下へ写像する。
UIの表示語は自由だが、内部では必ずこの表のパラメータを増減する。

| 表示語 | 内部パラメータ |
|---|---|
| 最高速 / 基礎流速 / 高速 | stats.flowSpeed |
| 加速 / 軽量 / 旋回 | stats.acceleration |
| 浮力 | stats.buoyancy |
| 姿勢 / 回避 / 接触 | stats.stability |
| 頑丈（旧: 耐久） | stats.toughness |
| 判断 / 反応 / 経験 | stats.judgement |
| 胆力 / 激流適性 | affinity.rapid |
| 渦耐性 | affinity.whirlpool |
| 狭所 | affinity.narrow |
| 炉 / 炉出力 | furnace.output（同調系は furnace.* 各能力） |
| 冷却 | furnace.cooling |
| 回復 | fatigue 減少 / 調子上昇（能力ではない） |
| 妨害耐性 | stats.stability ＋ 体質累積 collisionExperience |
| 乾燥 / 瑞々しい 等 | 体質累積値（§4） |

※ スクリーンサンプル画像の「旋回」「胆力」は上表で読み替える。

## 3. 成長結果

1回のならしで決めるもの:

```text
baseGrowth
x growthPhase
x talent
x weatherAffinity
x condition
x fatiguePenalty
x randomRange
```

加えて:
- trait event
- body type shift
- visual change
- injury

## 4. 体質

隠れ累積値を持つ。

例:
- moisture
- dryness
- toughness
- rapidExperience
- collisionExperience
- furnaceStress

閾値で体質を獲得。

例:
`dryness >= 70 -> 乾き葉`

体質は完全な上位互換にしない。

## 5. おまかせ育成

方針:
- スピード
- 安全
- 耐久
- 葉脈炉
- 急流
- 妨害

AIが危険度上限を守って育成。

ユーザー設定:
- 安全第一
- 標準
- 攻める

## 6. 親木

各ランナーは「葉統」に属する。

引退した優秀個体の実績から「継承因子」を親木系統へ登録できる。

「葉っぱそのものが生殖する」という表現にはしない。

世界観上:
**葉っぱが残した流れ・葉脈の記憶を親木が受け取り、次の若葉へ伝える。**

## 7. 接ぎ木

親木A + 親木B。

継承候補:
- 基礎能力レンジ
- 成長型
- 適性
- 葉脈炉
- 性格傾向
- 因子

候補3体を生成。

### 生成
各能力:
- Parent A contribution
- Parent B contribution
- species bias
- mutation
- random variance

## 8. 因子

因子例:
- 高速葉脈
- 長時間臨界
- 強冷却
- 急流巧者
- 雨巧者
- 高浮力
- 強靭
- 接触巧者

1個体に主要因子2〜4程度。

全部継承しない。

## 9. 突然変異

低確率。

- 二重葉脈
- 黄金
- 黒葉
- 斑入り
- 特殊シルエット

「レア = 必ず強い」にしない。

例:
二重葉脈:
出力 +++
炉耐久 --

## 10. 引退 vs 続投

引退:
因子を安全に登録。

続投:
記録を伸ばせる。
ロストすると一部の後天因子登録チャンスを失う。

この葛藤を重要なゲーム性とする。

---

# 06. キャラクターデザイン・3D制作仕様

## 1. 最優先アート方向

参考:
`art_reference/nagarace_loose_style.png`

### キーワード
- ゆるい
- まるい
- ぷにっとした印象
- 小さい手足
- シンプルな黒目
- 葉っぱの形がそのまま個性
- かわいい70 / 熱い20 / 少し変10

### 避ける
- 戦闘アニメのような険しい顔
- 鎧
- 武器
- 過剰なディテール
- 人型すぎる体
- リアルな人間の歯
- ホラー系の悪葉っぱ

悪葉っぱも「いたずら好き」に見える範囲。

## 2. シルエット

遠目でも種類がわかる。

- モミジ: ギザギザ・横広
- イチョウ: 扇形
- ハス: 丸
- サクラ: 小さく尖る
- 悪葉っぱ: やや尖るが丸みを残す
- 葉しょう: 古い葉、傷、落ち着き

## 3. 体格

葉っぱ本体が胴体。

手足:
短く小さい。

水上:
下半分が少し水に浸かる。

陸:
ぴょこぴょこ移動。

## 4. 顔

目:
シンプルな楕円 / 丸。

口:
小さい。

眉:
必要時のみ。

表情はテクスチャ差替えまたは簡易Mesh。

## 5. 3D制作方式

### 推奨
**共通リグ + 種類別Leaf Mesh**

共通:
- Root
- Body
- FaceAnchor
- Arm_L
- Arm_R
- Leg_L
- Leg_R
- LeafTip bones

種類ごと:
- body leaf mesh
- leaf vein texture
- silhouette
- color

## 6. ポリゴン

初期目安:
- 3,000〜6,000 tris / racer
- Hero/close-up: 最大8,000程度
- LOD: 40〜60%削減版

スマホ実機プロファイルを優先し、数値固定はしない。

## 7. テクスチャ

推奨:
- Base Color
- Normal（必要な場合）
- Mask（wet / damage / vein glow等）

512〜1024基準。

8体同時表示のためAtlas化を検討。

## 8. 状態表現

Shader / Maskで:
- 濡れ
- 乾燥
- 傷
- 紅葉
- 葉脈炉発光
- 臨界
- 汚れ

メッシュを状態ごとに別制作しすぎない。

## 9. アニメーション

MVP必須:
- Idle
- Hop
- FloatIdle
- FlowNormal
- FlowFast
- TurnLean
- Hit
- Stun
- BoostStart
- BoostLoop
- BoostEnd
- OverFlow
- Win
- Lose
- Sleep

### ゆるさ
動きはカチッとさせず:
- 少し遅れて葉先がついてくる
- 着地時に柔らかくつぶれる
- 水に浮く時に上下

## 10. 外注時の納品

必須:
- `.blend`
- `.fbx`
- textures PNG/TGA
- rig
- animation clips
- material setup memo
- commercial use rights confirmation

### 三面図
正面 / 横 / 背面。

### 共通リグ準拠
外注先にSkeleton templateを渡す。

## 11. 制作フロー

```text
コンセプト
 ↓
シルエット承認
 ↓
三面図
 ↓
Blender low-poly
 ↓
UV
 ↓
Texture
 ↓
Common Rig
 ↓
Animation check
 ↓
FBX
 ↓
Unity Prefab
 ↓
Shader mask設定
 ↓
実機確認
```

## 12. 背景

主役キャラ:
オリジナル推奨。

背景:
市販アセットを改造してよい。

既製品候補:
- 岩
- 草
- 木
- 民家
- 橋
- 小物
- 山

水と川のレース部分は独自セットアップ。

## 13. 水

「流体シミュレーション」でゲームを動かさない。

見た目:
- URP shader
- flow map
- foam
- ripple
- particles

内部:
Flow Graph。

## 14. 正式モデルを作るタイミング

Phase 1は仮モデル。

「葉脈炉を含むレースが面白い」と確認できたら:
- モミジ
- イチョウ
- 悪葉っぱ

の3体のみ正式化。

その後量産する。

---

# 07. 技術アーキテクチャ

> **v1.1 改訂（2026-10-02 仕様レビュー）** Unityバージョンの具体化、Assembly Definition の依存関係、乱数・バランス設定の配置、iOSビルド環境を追記。追記箇所には【v1.1】を付ける。

## 1. 基準エンジン

**Unity 6.3 LTS**

【v1.1】エディタは `6000.3.x`（6.3 LTS の最新パッチ）を使用し、`ProjectSettings/ProjectVersion.txt` で固定する。
6.4 等の非LTSでプロジェクトを開かない（アップグレードは別タスクとして判断する）。

レンダリング:
**URP**

入力:
**Unity Input System**

アセット:
正式運用では **Addressables** 採用候補。
MVP初期は直接Prefab参照でもよいが、Resourcesフォルダ乱用禁止。

## 2. DCC

**Blender 5.2 LTS** を基準。

## 3. Unity Scenes

```text
Boot
Title
Home
Training
RaceCalendar
Race
ParentTree
WorldMap
Album
```

ただし各UIを完全にScene分割するかはプロトタイプ後決定。
Raceは独立Scene必須。

## 4. Assembly Definitions

```text
NagaRace.Domain
NagaRace.Application
NagaRace.Infrastructure
NagaRace.Presentation
NagaRace.Tests
```

Domain -> Unity Presentationへの依存禁止。

【v1.1】参照関係（矢印 = 参照する側 → される側）:

```text
NagaRace.Presentation   → Application, Domain
NagaRace.Infrastructure → Application, Domain
NagaRace.Application    → Domain
NagaRace.Domain         → （なし）  ※ asmdef の noEngineReferences = true で UnityEngine 参照を禁止
NagaRace.Tests.EditMode → Domain, Application, Infrastructure
NagaRace.Tests.PlayMode → 全て（M2以降）
```

- テストは EditMode / PlayMode で asmdef を分ける（旧記載の `NagaRace.Tests` 1本を置換）
- Application が必要とする外部I/O（保存・時刻・設定読込）は Application 側にインターフェースを置き、Infrastructure が実装する

## 5. Domain

主要クラス候補:

```text
CharacterId
CharacterState
CharacterDefinition
CharacterStats
FurnaceStats
Condition
Fatigue
Durability
Trait
Affinity
GrowthProfile
RaceCourse
FlowSegment
RaceEntrant
RaceState
RaceSimulator
RaceEvent
TrainingDefinition
TrainingResolver
ParentTree
Lineage
GeneFactor
GraftingResolver
```

## 6. Application

UseCase:
- StartNewGame
- ExecuteTraining
- EnterRace
- StartRace
- ActivateFurnace
- StopFurnace
- ResolveRace
- RetireCharacter
- ExecuteGrafting
- SaveGame
- LoadGame

## 7. Presentation

Race:
- RacePresenter
- RacerView
- RaceCamera
- RaceHud
- FurnaceHud
- VfxController
- AudioController

Domain RaceEventをViewへ変換する。

## 8. ScriptableObject

定義データ:
- SpeciesDefinition
- TraitDefinition
- TrainingDefinition
- CourseDefinition
- TournamentDefinition
- BalanceConfig

可変状態をScriptableObjectへ保存しない。

## 9. セーブ

MVP:
ローカルJSON。

必須:
- schemaVersion
- checksum or validation
- backup slot

保存契機:
- 育成後
- レース後
- 接ぎ木後
- 引退後
- 設定変更後

レース途中の強制終了:
MVPではレース開始時点を保存し、再開時は再レースまたはSeed復元。

## 10. 乱数

`IRandomProvider`

本番:
Seeded PRNG。

テスト:
FixedRandomProvider。

UnityEngine.RandomをDomainから直接呼ばない。

【v1.1】`IRandomProvider` と Seeded PRNG 実装（PCG32 等）は **Domain に置く**（純粋な計算であり、レース再現性の中核のため）。
CLAUDE.md の Infrastructure 欄にある RandomProvider は、Seed の発行元（新規レースのSeed生成など）を指すものと読み替える。
`System.Random` は .NET 実装差があるため使用しない。サブストリーム派生は 04 §1.2。

### 10.1 バランス設定・定義データ【v1.1】

- 正本: `Assets/NagaRace/Config/*.json`（バランス値。M0 で `config/` から移動し一本化）。TextAsset として参照（Resources は使わない）
- 読込: Infrastructure で JSON → Domain の POCO（`BalanceConfig` 等）へ変換して渡す。Domain は JSON ライブラリに依存しない
- JSON ライブラリ: `com.unity.nuget.newtonsoft-json`（JsonUtility はネスト配列・Dictionary を扱えないため）
- EditMode テストも同じ JSON を読み、テスト専用の数値をコードに直書きしない（境界テスト用の上書きは可）
- コース・系統などの定義データも M1〜M7 は JSON で持つ。ScriptableObject 化（Inspector編集）はデザイナー運用が必要になる M8 で再判断する

## 11. Performance

目標:
- 8体レース
- 60fpsを理想
- 重い端末では30fps選択肢
- GC AllocをレースTickで極力出さない

ProfilerをPhase 1から使用。

## 12. Input

Action Map候補:

### UI
- Navigate
- Submit
- Cancel
- Point
- Click

### Race
- ChargeTap
- ChargeSwipe
- FurnaceActivate
- FurnaceRelease
- Skill

スマホTouchとGamepadを同じActionへ束ねる。

## 13. カメラ

Cinemachine系利用は候補。
ただしMVPは簡易追従でよい。

カメラ:
- Player follow
- Wide pack view
- Event cut
- Finish

演出過多で順位が見えない問題に注意。

## 14. Addressables

商品化フェーズで:
- Character visuals
- Course visuals
- Seasonal assets
- Event assets

をグループ化。

ゲームロジック定義はバージョン整合性に注意。

## 15. Backend

Phase 1〜2:
なし。

Phase 3:
抽象インターフェースを通して追加。

必要機能:
- Auth
- Cloud Save
- Async PvP snapshot
- Ranking
- Season
- Remote Config
- Telemetry

製品選定は実装時に再評価。

## 16. Switch

MVPの時点でSwitch SDK依存コードを入れない。

Input System / UI / Safe Area / aspect / performance設計をクロスプラットフォームに保つ。

商品性確認後、任天堂開発環境を取得して移植工程を切り出す。

## 17. ビルド環境【v1.1】

- iOS ビルドには macOS + Xcode が必要。現在の開発環境は Windows のため、iOS 実機検証の手段（Mac 端末 または Unity Build Automation 等のクラウドビルド）を M9 までに用意する
- Android は Windows で完結する

---

# 08. データモデル・セーブ仕様

> **v1.1 改訂（2026-10-02 仕様レビュー）** CharacterState に不足項目を追加、Replay入力をTick番号に変更。追記箇所には【v1.1】を付ける。

## 1. CharacterState

```json
{
  "id": "uuid",
  "name": "アカネ",
  "speciesId": "maple",
  "lineageId": "lineage-001",
  "generation": 3,
  "ageTurn": 27,
  "growthPhase": "Growth",
  "condition": "Good",
  "fatigue": 18,
  "durability": 76,
  "maxDurability": 92,
  "stats": {
    "flowSpeed": 72,
    "acceleration": 81,
    "buoyancy": 48,
    "stability": 45,
    "toughness": 55,
    "judgement": 68
  },
  "furnace": {
    "output": 84,
    "startup": 70,
    "criticalDuration": 52,
    "cooling": 40,
    "durability": 58
  },
  "traits": ["rapid_lover", "late_boost"],
  "bodyTraits": ["slightly_dry"],
  "geneFactors": ["high_output"],
  "record": {
    "starts": 12,
    "wins": 4
  },
  "status": "Active"
}
```

【v1.1】追加項目（上記JSONに加える）:

```json
{
  "personality": "gambler",
  "growthType": "Normal",
  "affinities": { "rapid": 70, "whirlpool": 40, "narrow": 55, "deep": 45,
                  "sprint": 60, "middle": 55, "long": 35 },
  "bodyAccumulation": { "moisture": 0, "dryness": 34, "toughness": 0,
                        "rapidExperience": 12, "collisionExperience": 5, "furnaceStress": 20 },
  "parentTreeIds": ["tree-a", "tree-b"],
  "bornDay": 1,
  "endedDay": null,
  "endReason": null
}
```

- `stats.toughness` は主能力「頑丈」。HP相当は `durability / maxDurability`（02 §4）
- `status`: `Active` / `Retired` / `Lost`。Lost・引退後もデータは削除せず Album から参照する
- `endReason`: `Retired` / `LostRapidAccident` / `LostFurnace` / `LostDurability` 等

## 2. SpeciesDefinition

```json
{
  "id": "maple",
  "displayName": "モミジ",
  "baseRanges": {},
  "growthBias": {},
  "affinityBias": {},
  "visualAddress": "character/maple/default"
}
```

## 3. RaceCourse

```json
{
  "id": "stream_001",
  "name": "公園の小川",
  "distance": 1200,
  "danger": 1,
  "weatherTags": [],
  "segments": []
}
```

## 4. TrainingResult

```json
{
  "characterId": "uuid",
  "trainingId": "rapid_training",
  "statDeltas": {},
  "fatigueDelta": 18,
  "durabilityDelta": -4,
  "traitEvents": [],
  "injury": null
}
```

## 5. RaceSnapshot

非同期PvPを見据える。

```json
{
  "characterId": "uuid",
  "version": 1,
  "stats": {},
  "furnace": {},
  "traits": [],
  "aiStyle": "Standard",
  "visualIds": {}
}
```

個人セーブ全体を相手へ配布しない。

## 6. RaceReplay

```json
{
  "raceSeed": 12345678,
  "courseId": "course_001",
  "entrants": [],
  "playerInputs": [
    {"tick": 428, "type": "FurnaceOn"},
    {"tick": 490, "type": "FurnaceOff"}
  ]
}
```

【v1.1】入力時刻は秒（float）ではなく **Tick番号（int）** で記録する。float秒は丸め誤差で再生時に1Tickずれる可能性があるため。
あわせて `simVersion`（シミュレーションロジックのバージョン）と `balanceHash`（使用したバランス設定のハッシュ）を記録し、不一致のリプレイは再生しない。

## 7. SaveGame

```text
schemaVersion
user
calendar
characters[]
lineages[]
parentTrees[]
inventory
album[]
raceHistory[]
settings
```

## 8. Migration

schemaVersion更新時:
`ISaveMigration`

例:
V1 -> V2。

古いセーブが読めない変更を無処理で入れない。

## 9. マスターデータ

ゲームバランス数値はコード直書きしない。

対象:
- stat cap
- training values
- furnace values
- durability
- growth
- race randomness
- lost risk
- tournament rewards

ScriptableObject または JSON/CSVインポートから管理。

---

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

### 【v1.1】M7 実装メモ（2026-10-05）
- 親木: 引退・ロストした個体から登録。引退なら現役中の能力（75/85/95 以上で Lv1〜3）から後天因子を得る。ロストでは後天因子を失う（05 §10 の葛藤）
- 因子10種・突然変異5種は `Assets/NagaRace/Data/Factors/factors.json`。突然変異はすべて長所と短所を持つ
- 子の能力 = 親の寄与（比率 0.3〜0.7）を系統の基準へ20%寄せ + ばらつき（相性で縮小）→ 若葉として ×0.8 で誕生 + 継承因子（Lv × 3）+ 突然変異（4%）
- 因子の継承率は Lv × 30%（上限90%）、主要因子は最大4つ。成長型・性格は親のどちらか（一部ランダム）
- 相性: 基本50 + 葉統が違えば+20 + 同系統なら+10 − 因子の重複×5。予測幅は 03 §10 の「継承予測」に使う
- 接ぎ木は日を消費しない。同じ日・同じ組み合わせなら同じ候補（Seed で再現）
- 計測: 流速90/85の親どうし → 子の流速 平均69.6、40/45の親どうし → 37.5

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

### 【v1.1】M8 実装メモ（2026-10-05）
- シーン: Main（タイトル・ホーム・葉っぱ一覧/詳細・育成・大会・出走登録・リザルト・設定・親木園）と Race（独立 Scene）。ビルドの先頭は Main
- シーン間の共有は `GameServices`（唯一の Singleton）。セーブは育成後・レース後・接ぎ木後・引退後・設定変更後に自動保存
- 大会: `Assets/NagaRace/Data/Tournaments/tournaments.json`。毎週4日目がレースの日で、週ごとに2〜3大会から選ぶ。前のグレードで1勝すると次のグレードに出られる。MOST HIGHER RIVER は表示するがロック
- 育成カード: 予想効果（+〜+++）・疲労・耐久リスク・危険度・本日 BONUS・おすすめ度。危険以上のみ確認ダイアログ
- 引退すると自動で親木に登録。ロストした個体も（後天因子なしで）親木に登録
- 親木園（SC-11 簡易版）: 親木A/Bを選び、継承予測（相性・能力の予測幅・因子）を見て候補3体から迎える
- UI はコードで組み立てた仮 UI（UiKit）。日本語は組み込みフォント＋OS フォールバックのため、スマホ実機では日本語フォントの導入が必要
- 検証: PlayMode テストで新規ゲームから24日間を画面操作で通しプレイ（レース5〜6回・育成18〜19回）し、保存→再読込まで確認

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

### 【v1.1】M10 の先行実装（2026-10-05）
- チュートリアル: 各画面を初めて開いたときに1回だけ説明を出す（ホーム・育成・大会・出走登録・リザルト・詳細・親木園）。初めてのレースは、スタート前に葉力・葉脈炉・葉力チャンスの説明を出す。既読はセーブし、設定から「もう一度見る」でリセット
- プレイログ（11 §8）: `persistentDataPath/save/logs/events.jsonl` に JSON Lines で記録（外部送信なし、2MB で1世代ローテーション）。new_game / continue / tutorial_step / training_choice / race_start / race_end（着順・炉の始動秒・OVER FLOW 回数・故障）/ lost / retire / grafting / session_start / session_end
- ビルド: `NagaRace > Build > Windows (Tester Build)` / `Android (APK)`。横画面固定。Windows ビルドは成功を確認（96MB）。Android は Android Build Support の導入が必要
- 未着手（人手が必要）: 第三者テスト、実機での性能測定と端末下限の確定（10 I）、正式な日本語フォント、バランスの本調整

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

---

# 10. 受入条件・テスト仕様

> **v1.1 改訂（2026-10-02 仕様レビュー）** 統計テストの合格基準を数値化し、Race Core のテストを追加。追記箇所には【v1.1】を付ける。
> 合格基準の数値は暫定。バランス調整で変える場合は本書を更新してからテストを変える。

## A. Race Core

### A-01
Given:
同じ8キャラ、同じコース、同じSeed。

When:
プレイヤー入力なし。

Then:
同じ着順・主要イベントになる。

### A-02
高流速キャラ100回 vs 低流速キャラ100回で、
高流速側が統計的に優位になる。
ただし100%固定勝利にはしない。

【v1.1】具体的な判定（方針: 波乱はあるが、負けた理由が説明できる。2026-10-02 決定）:
- 全能力60の8体のうち1体だけ流速75にする。Seed 1〜200 の200レース
- 流速75の個体の 1着率が 25%以上 かつ 45%以下（均等なら12.5%）
- 流速75の個体の平均着順が 3.5 以下

### A-02b【v1.1】しっかり育てた個体は勝ちやすい
- 全能力60の8体のうち1体だけ全主能力を +15 にする。Seed 1〜200
- その個体の 1着率が 50%以上 かつ 75%以下
- その個体の 3着以内率が 85%以上

波乱の源は進路選択・接触・葉脈炉の判断とし、Segmentごとの速度ゆらぎ（jitter）だけで順位が入れ替わる設計にしない。

### A-03【v1.1】枠順の公平性
全能力・適性・性格が同一の8体で Seed 1〜400。
各枠（entrantIndex）の1着率がすべて 12.5% ± 6ポイント以内。

### A-04【v1.1】レース時間
標準強度のNPC 8体で、各テストコースを Seed 1〜200 で走らせた際、
勝者タイムの95%以上が大会の `targetDurationSeconds` の範囲内。

### A-05【v1.1】全能力が効く
6主能力それぞれについて、1体だけその能力を +15 した場合、その能力が効く地形を含むテストコースで、
Seed 1〜200 の平均着順が基準（+15なし）より良くなる。「上げても意味のない能力」を作らない。

【v1.1 M1実装時の補足】
- 合格条件は「平均着順が基準より 0.15 以上良い」
- 頑丈（toughness）はダメージ軽減の能力のため、ダメージを実装する M4 で「受けるダメージ量が減る」ことを検証する（M1 では対象外）
- A-02 / A-02b / A-03 は標準コース `test_branch` で判定する

### A-06【v1.1】必ず終了する
全テストで、レースが `race.timeoutSeconds` 以内に終了状態（全員ゴール or リタイア）になる。

## B. Furnace

### B-01
葉力不足時に始動できない。

### B-02
SafeDuration内解除でOverFlowしない。

【v1.1】SafeDuration 内に解除した場合、故障抽選そのものが行われない（乱数を消費しない）ことを検証する。

### B-03
RiskZone滞在時間が長いほど重大故障率が上がる。

### B-04
同じ炉性能でも低耐久・高疲労の方が故障しやすい。

### B-05【v1.1】炉の判断で結果が動く
全能力60の8体（他7体はAI作戦「標準」で炉を使用）で、対象の1体について Seed 1〜200 で比較する。
- 炉を使わない場合と、中盤以降に葉力が溜まるたび SafeDuration 付近まで使う場合で、平均着順の差が 1.0 以上
- 遅すぎる始動（ゴール直前まで温存して使い残す）は、中盤以降に使う場合より平均着順が 0.5 以上悪い

【M3 実装時の変更】旧案の「スタート直後の始動」は、葉力50未満では始動できないため測定不能。M3 の計測では「溜めすぎて使い残す」ことの損が大きかったため、判定を「遅すぎ」に置き換えた。

## C. Lost

### C-01
安全コース + 耐久80以上 + 通常走行で突然Lostしない。

【v1.1】ハードガード（04 §9.1）の検証として、コース危険度2以下 かつ 耐久40以上 の条件では、
葉脈炉を最大まで引っ張る操作を含めて Seed 1〜10,000 で Lost が0件であること。

### C-02
耐久低下・極危険・OverFlow等が重なる場合のみLost候補になる。

### C-03
PvPの妨害スキル単独ではLostしない。

【v1.1】NPC 戦でも同じ。耐久3の自キャラを悪葉っぱ7体と1000レース走らせ、Lost が0件、妨害で耐久が1未満にならないことを検証する。

### C-04
Lost後にCharacter Listから現役として消えるが、Albumに存在する。

【v1.1】MVPではアルバム画面がないため、「セーブデータ上で status = Lost の記録として残り、削除されない」ことを検証する。

## D. Training

### D-01
天候により育成結果が変化。

### D-02
疲労高状態で高負荷訓練を繰り返すと効率が落ちる。

### D-03
特定ならしの累積で体質が付く。

## E. Growth

若葉と最盛期で成長係数が異なる。

紅葉期では一部能力が緩やかに下降可能。

## F. Grafting

### F-01
子候補が親の能力レンジを一定程度反映。

### F-02
3候補が完全同一にならない。

### F-03
突然変異はSeedでテスト可能。

## G. Save

### G-01
保存 -> 再起動 -> 読込で主要状態一致。

### G-02
schemaVersion不一致時にMigration処理。

### G-03
壊れたJSONでクラッシュせずバックアップを試行。

## H. UI

### H-01
レース中、自キャラが常に識別できる。

### H-02
葉脈炉状態が0.5秒以内に判断できる。

### H-03
危険な育成 / 出走前に危険度を確認できる。

## I. Performance

Phase 1の標準テスト端末群で8体レースが許容fpsを維持する。
具体的な端末下限は実機検証後に確定し、本書へ追記する。

## J. UXテスト質問

テスターへ:
1. 自分のキャラを応援したか？
2. 次のレースを見たかったか？
3. 葉脈炉をいつ押すか迷ったか？
4. 育成結果に納得感があったか？
5. ロストが理不尽だったか？
6. 次世代を作りたいか？

5が強くYesならロスト設計を修正。

---

# 11. 非同期PvP・バックエンド（MVP後）

## 1. 原則

コアループが面白いことを確認してから導入。

## 2. 非同期PvP

相手プレイヤーの現役キャラを「RaceSnapshot」として保存。

マッチング:
- Rating
- Character strength band
- League

相手本人はオフラインでもよい。

## 3. 改ざん対策

クライアント結果を全面信用しない。

将来:
- seed発行
- race input log
- result validation
- suspicious result detection

## 4. Ranking

- Weekly
- Season
- Course Time
- MOST HIGHER RIVER record

## 5. Ghost / Replay

Replayデータ:
- snapshot
- seed
- furnace input timestamps

動画そのものを保存しない。

## 6. Live Ops

候補:
- 季節
- 雨季
- 紅葉祭
- 特殊大会
- 限定外観

強さのインフレを避ける。

## 7. 課金

購入してしか勝てないキャラは禁止方針。

PvP公平性のため、
能力販売は慎重に扱う。

## 8. Analytics

最低限:
- tutorial completion
- training choices
- race starts
- furnace activation timing
- overflows
- retirements
- losses
- grafting
- session length

目的:
面白さ改善。
過度な課金誘導のためだけに使用しない。

---

# 12. コンテンツ追加テンプレート

## Character Species

```yaml
id: maple
name: モミジ
archetype: speed
visual: character/maple/default
base:
  flowSpeed: [55, 75]
  acceleration: [65, 85]
  buoyancy: [35, 55]
  stability: [30, 55]
  toughness: [30, 55]
  judgement: [45, 70]
furnace:
  output: [70, 90]
  criticalDuration: [35, 60]
traits:
  weighted:
    - rapid_lover
    - late_boost
```

## Training

```yaml
id: sun_training
name: 日向ならし
fatigue: 8
durabilityRisk: 1
growth:
  flowSpeed: 1.0
  furnaceOutput: 0.6
weather:
  sunny: 1.3
  rainy: 0.5
bodyAccumulation:
  dryness: 10
```

## Tournament

```yaml
id: village_mountain_cup
name: Village Mountain City Cup
courseId: stream_002
grade: 2
distance: 1200
danger: 1
entryRules: []
rewards: {}
```

## Trait

```yaml
id: rapid_lover
name: 激流好き
description: 激流区間で流速が上がるが、穏やかな区間では少し落ち着かない。
effects: []
```

実際のフォーマットはScriptableObjectまたはインポート形式に合わせて決定する。

---

# 13. 決定事項

## 決定済み

- タイトル: ながレース
- キャラは葉っぱ等の自然物
- キャラデザインはかなりゆるめ
- ダビスタ型育成の考える楽しさを参考にする
- ただし育成テーマは自然の「ならし」
- レースは基本オート
- 直接ハンドル操作なし
- レース中は葉力を貯める
- 任意タイミングで葉脈炉
- 葉脈炉は臨界までカウントダウン
- 引っ張りすぎで故障
- 完全ロストあり
- ロストはアルバムへ
- 悪葉っぱあり
- PvP妨害あり
- 妨害単独で相手完全ロストは禁止
- 親木・接ぎ木・葉統
- MOST HIGHER RIVERが最高峰
- まつおうじ葉しょうが伝説
- iOS / Android先行
- Switchは後続
- Unity + Blender
- MVPはアートよりゲーム性優先

## 決定済み（v1.1 仕様レビューで追加・2026-10-02）

- 主能力の「耐久」は「頑丈（toughness）」に改名。「耐久」はHP相当（durability）のみを指す
- 性格（1つ・AI傾向）と特性（複数・能力補正）を分離
- レースは固定Tick 20Hz、入力・イベントはTick番号で記録
- 乱数は Domain 内の自前 Seeded PRNG。用途別・出走者別にサブストリームを派生
- 距離単位は m。標準コース 1,200〜2,400m。最高峰のみ 90〜110秒を許容
- OVER FLOW は RiskZone 状態の表示名。故障結果は MinorDamage / FurnaceDamage / Blow / Critical に統一
- Lost はコース危険度2以下かつ耐久40以上では絶対に発生しない（ハードガード）
- 妨害単独での Critical / Lost は PvP・NPC戦とも発生させない
- 自キャラは1レース1体（MVP）
- Album / Parent Tree の画面は Phase2。MVPではアルバム記録データの保存と接ぎ木ロジック（Debug画面から実行）まで
- 「おまかせ炉」をオプションで用意（既定OFF、操作した時点で手動に切替）
- 暦: 1週4日・1季12日・1年48日。大会はおおむね週1
- 能力差の効き方は「波乱はあるが負けた理由が説明できる」方針（10 A-02 / A-02b / B-05）
- バランス値・定義データは M1〜M7 では JSON。ScriptableObject化は M8 で再判断
- Unity は 6000.3.x（6.3 LTS）で固定
- リポジトリ構成: パッケージ一式をリポジトリルートに置き、Unityプロジェクトも同じルートに作成

## 調整可能

- 能力数
- 成長期間
- レース秒数
- Lost確率
- 葉力獲得速度
- 炉SafeDuration
- 接ぎ木候補数
- 大会数
- 1シーズン長

これらはコード定数ではなくデータ化する。

---

# 14. 仕様レビュー記録（v1.0 → v1.1）

- 実施日: 2026-10-02
- 対象: 開発開始パッケージ v1.0 一式（CLAUDE.md / docs/01〜13 / config / art_reference）
- 目的: Milestone 0〜1 に着手する前に、実装時に解釈が割れる箇所・矛盾・不足を洗い出し、暫定値で埋める
- 方針: 既存の文章は削除せず、各ドキュメントに【v1.1】として追記・修正した。数値はすべて暫定で `config/game_balance_defaults.json` に置いた（M0 で `Assets/NagaRace/Config/` へ移動）

## 1. 総評

企画の軸（育成60 / レース25 / スローライフ15、直接操作なし、葉脈炉の「いつ始動し、いつ止めるか」、理不尽でないロスト）は一貫しており、MVPで検証すべき4点も明確。
一方で **レースコアを実装するための定量仕様**（単位・Tick・横位置・進捗・能力の効き方・炉の数値・Lost条件）が概念式止まりで、そのまま M1 に入ると実装者ごとに解釈が分かれる状態だった。
また、用語の衝突（「耐久」）と、ドキュメント間のフェーズ矛盾（Album / Parent Tree）があった。

## 2. 指摘と対応

重要度: **高** = M1 の実装が決められない / 仕様矛盾。**中** = M2〜M8 で問題化。**低** = 運用・表記。

| No | 重要度 | 指摘 | 対応 | 修正先 |
|---|---|---|---|---|
| R-01 | 高 | 「耐久」が主能力（ダメージ耐性）・HP相当（durability）・炉耐久の3つに使われている。UI・会話・コードで混同する | 主能力を「頑丈（toughness）」に改名。「耐久」はHP相当のみ | 02 §4, 04 §4.2, 05 §2.1, 08 |
| R-02 | 高 | 距離・速度の単位がない。`distance: 1200` と45〜90秒の関係が不明。葉しょう記録 01:42.186（102秒）が目標時間の範囲外 | 単位を m・m/s に定義。標準 1,200〜2,400m、平均約26m/s。最高峰のみ90〜110秒を許容 | 04 §1, §1.1 |
| R-03 | 高 | シミュレーションTick・入力時刻の扱いが未定義。Replay が float秒（`t: 21.42`）で、再生時にずれる恐れ | 固定20Hz。入力・イベントはTick番号（int）で記録。Replayに simVersion / balanceHash | 04 §1.1, 08 §6 |
| R-04 | 高 | 乱数の実装・配置が未定義。CLAUDE.md では RandomProvider が Infrastructure だが Domain が必要とする。単一乱数列だと、炉を1回押すだけで他7体の進路抽選まで全部変わり「早すぎ/遅すぎ」の比較ができない | IRandomProvider と PCG32 を Domain に配置。用途別・出走者別のサブストリーム。保証範囲は同一ビルド・同一プラットフォーム | 04 §1.2, 07 §10 |
| R-05 | 高 | 「AIが Flow Lane を選択」とあるが、横位置のモデルが無い。追い抜き・接触・枠順の扱いが決められない | lateral（-1〜+1）の連続値モデル、詰まり・横移動・接触条件を定義。枠順の公平性をテスト化 | 04 §5.1, §6, 10 A-03 |
| R-06 | 高 | 分岐で経路長が違うとき「Spline上の進捗値」で順位を比較できない | Segment に progressStart/End を持たせ、分岐〜合流区間を距離比で補間。グラフはDAG・合流必須 | 04 §2, §11 |
| R-07 | 高 | 6主能力がレース内で何に効くかが「流速」以外不明。効かない能力が生まれる恐れ | 能力ごとの役割表と計算手順を定義。全能力が効くことをテスト化 | 04 §4.1, §4.2, 10 A-05 |
| R-08 | 高 | 葉脈炉: 葉力消費量、「長く引っ張るほど強い」の曲線、炉5能力の意味（起動・冷却）、OVER FLOW と故障結果の関係が未定義。結果名も §8（5種）と §9（3種）で不一致 | ステートマシン、炉能力→秒/倍率の変換、ramp、故障抽選と重さを定義。結果名を MinorDamage / FurnaceDamage / Blow / Critical に統一。OVER FLOW は RiskZone の表示名 | 04 §8.1〜8.4 |
| R-09 | 高 | Lost 条件が「極めて低くする」等の定性表現のみ。受入テスト C-01/C-02 が書けない | 判定に進む条件とハードガード（危険度2以下かつ耐久40以上は0%）を定義。妨害単独ではNPC戦でも Lost なし | 04 §9.1, 02 §11, 10 C-01 |
| R-10 | 高 | 受入テスト A-02「統計的に優位」の合格基準がない | 1着率30〜85%・平均着順3.0以下など数値化。A-03〜A-06 を追加 | 10 |
| R-11 | 中 | 「性格」（慎重・勝負師）と「特性」が同じリストに混在。データモデルに性格の項目がない | 性格（1つ・AI傾向）と特性（複数・能力補正）を分離。CharacterState に personality を追加 | 02 §5.1, 08 |
| R-12 | 中 | 「適性」が courseSuitability で使われているが中身が未定義 | 地形4・距離3・天候の適性を定義 | 02 §5.2, 08 |
| R-13 | 中 | 育成の効果語（胆力・旋回・反応・回避・姿勢・軽量・経験 等）が主能力に存在しない | 表示語→内部パラメータ対応表を作成。UI例の「胆力」は「激流適性」へ | 05 §2.1, 03 §4 |
| R-14 | 中 | CLAUDE.md §7 は天候をレース要素に含めるが、レース仕様に天候・水量が無い | RaceConditions（天候・水量）をレース入力として定義 | 04 §2.1 |
| R-15 | 中 | 8体中7体のNPCの作り方が未定義（M1 でも必要） | 大会定義に強さ帯・系統プール、大会Seedで生成 | 04 §13 |
| R-16 | 中 | 画面一覧で Album / Parent Tree が Phase2 だが、M4・M7・M8 と MVP検証項目4はMVPで必要としている | Phase2 を維持。M4 は記録データ保存、M7 は Debug 画面から接ぎ木、検証項目4は Lost / Retirement 演出で確認（Q-1 決定） | 03 §1, 09, 10 C-04 |
| R-17 | 中 | 育成の激流ならしで「ロスト（極端な条件のみ）」の条件が未定義 | 耐久20未満かつ疲労70以上のときのみ、確認ダイアログ承認後に判定 | 05 §2 |
| R-18 | 中 | 成長型（growthType）が継承対象なのに定義が無い | 早熟/普通/晩成/持続の4種（暫定） | 02 §6, 08 |
| R-19 | 低 | asmdef が `NagaRace.Tests` 1本だが EditMode/PlayMode フォルダがある。Domain の UnityEngine 非依存を強制する手段がない | Tests を2つに分割。Domain は `noEngineReferences: true` | 07 §4 |
| R-20 | 低 | バランス値の置き場が ScriptableObject（07）/ JSON・CSV（08）/ YAML（12）で揺れている | M1〜M7 は JSON を正本。Newtonsoft で読込。SO化は M8 で再判断 | 07 §10.1 |
| R-21 | 低 | Unity 6.3 LTS 指定だが、開発PCには 6000.4.7f1（6.4）のみ | 6000.3.x LTS をインストールして固定（ユーザー判断済み） | 07 §1 |
| R-22 | 低 | iOS ビルドには Mac が必要だが開発環境は Windows | M9 までに Mac またはクラウドビルドを用意 | 07 §17 |
| R-23 | 低 | `NAGARACE_MASTER_REQUIREMENTS.md` が docs/01〜13 の単純連結で、二重管理になる | docs/01〜14 を正本とし、統合版は生成物とする（README に記載） | README |
| R-24 | 低 | パッケージが `project/docs/` に入っており、CLAUDE.md がリポジトリルートで読まれない | パッケージ一式をリポジトリルートへ移動（実施済み） | — |
| R-25 | 低 | ルートの `ながレース：葉っぱたちの大冒険.png` は `art_reference/nagarace_loose_style.png` と同一ファイル（MD5一致） | 削除済み（Q-4 決定） | — |
| R-26 | 低 | 設定JSONの `baseSpeedMultiplier` / `defaultSafeDurationSeconds` が、能力値による範囲指定と二重になる | 範囲指定（`outputBoostRange` / `safeDurationSecondsByCritical`）へ一本化し旧キーを削除 | config |

## 3. 確認事項と決定（2026-10-02 ユーザー回答）

| No | 内容 | 決定 | 反映先 |
|---|---|---|---|
| Q-1 | Album / Parent Tree をMVPで作るか | 画面は両方 Phase2。M4/M7/M8 の完了条件を合わせて調整 | 03 §1, 09, 10 C-04 |
| Q-2 | 葉脈炉を押さなかった場合の「おまかせ炉」 | オプションで用意（既定OFF、操作した時点で手動に切替） | 03 §6, 04 §8.5 |
| Q-3 | 1シーズンの長さ | 1週4日・1季12日・1年48日、大会はおおむね週1 | 02 §7, config calendar |
| Q-4 | 重複画像の削除 | 削除 | — |
| Q-5 | 能力差の効き方 | 波乱寄り。ただし負けた理由が説明できること。流速のみ+15で1着率25〜45%、全能力+15で50〜75%、炉の判断で平均着順が1以上動く | 10 A-02, A-02b, B-05 |

## 4. 後続マイルストーンへの申し送り

- **M3 葉脈炉**: 葉力チャンス（3〜5回のタイミング入力）の判定方式。連打にならない入力窓の幅。NPC炉AI
- **M4 耐久・Lost**: 接触・Blow のダメージ量、レース後の maxDurability 低下ルール（02 §9 の 100→82 の例）
- **M5 育成**: 天候の発生テーブル、ならし10種の数値、体質の閾値と効果
- **M7 接ぎ木**: 因子の継承確率、突然変異率、3候補のばらつき幅
- **PvP 着手時**: 浮動小数点の端末間差（固定小数点化かサーバ検証か）

## 5. Milestone 0 / 1 実装計画（案）

### 5.1 作成予定ファイル

```text
（リポジトリルート）
  .gitignore / .gitattributes
  Assets/NagaRace/
    Config/game_balance_defaults.json
    Data/Courses/test_straight.json, test_branch.json, test_rapid.json
    Scenes/Debug/RaceDebug.unity
    Scripts/
      Domain/                  NagaRace.Domain.asmdef（noEngineReferences）
        Common/   IRandomProvider, Pcg32Random, RandomStreams, MathUtil
        Character/ CharacterStats, FurnaceStats, Affinities, Personality, Condition
        Race/     FlowSegment, RaceCourse, CourseValidator, RaceConditions,
                  RaceEntrant, RacerState, RaceState, RaceEvent,
                  RaceSimulator, RouteSelector, SpeedCalculator, LaneResolver,
                  RaceResult
        Balance/  BalanceConfig（POCO）
      Application/             NagaRace.Application.asmdef
        Race/     RunRaceUseCase, INpcGenerator, IBalanceProvider
      Infrastructure/          NagaRace.Infrastructure.asmdef
        Config/   JsonBalanceLoader, JsonCourseLoader
        Race/     SpeciesBasedNpcGenerator
      Presentation/            NagaRace.Presentation.asmdef
        Debug/    RaceDebugRunner（MonoBehaviour）, RaceDebugView（Gizmo/OnGUI）
    Tests/EditMode/            NagaRace.Tests.EditMode.asmdef
      Pcg32RandomTests, CourseValidatorTests, RaceDeterminismTests(A-01),
      StatInfluenceTests(A-02, A-05), GateFairnessTests(A-03),
      RaceDurationTests(A-04, A-06), RouteSelectionTests（性格差）
```

### 5.2 クラス責務

| クラス | 責務 |
|---|---|
| RaceSimulator | 固定Tickで RaceState を進める。描画・時間・Unityに依存しない |
| SpeedCalculator | 04 §4.1 の targetSpeed 算出と加速追従 |
| RouteSelector | 分岐時の routeScore 計算と選択（性格・AI作戦・判断ノイズ） |
| LaneResolver | 横位置・詰まり・接触判定 |
| CourseValidator | DAG・合流・見せ場チェックと progress 計算 |
| RandomStreams | raceSeed から用途別・出走者別のサブストリームを派生 |
| RunRaceUseCase | 設定・コース・出走者を受け取り、レースを最後まで回して結果を返す |
| RaceDebugRunner / View | Unity 上で Tick を進め、カプセルと順位を表示するだけ（ルールを持たない） |

### 5.3 実装手順

1. Unity 6.3 LTS で URP 3D プロジェクトを作成、パッケージ・asmdef・Test Runner を確認（M0）
2. 乱数（PCG32・サブストリーム）とテスト
3. コース定義・バリデーション・progress 計算とテスト
4. 直線コースで8体が走り切る最小 RaceSimulator → A-01, A-06
5. 能力の効き（速度式・加速・浮力・安定・判断）→ A-02, A-05
6. 横位置・詰まり・接触 → A-03
7. 分岐・性格による進路差 → RouteSelectionTests
8. コース長・速度の調整 → A-04
9. Debug Scene と表示

### 5.4 テスト方針

- Domain のロジックは EditMode テストで網羅し、Unity Play を必要としない
- 統計テストは Seed 範囲を固定（1〜200 等）して実行するため、結果は毎回同一（フレーキーにならない）
- バランス値は本番と同じ JSON を読む。テスト専用の上書きは境界テストに限る
- 合格基準は 10 の数値に従い、基準を変えるときは 10 を先に更新する

---
