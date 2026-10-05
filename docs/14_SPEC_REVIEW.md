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
