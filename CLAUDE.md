# CLAUDE.md — ながレース開発ルール

このリポジトリでは、最初に `/docs` を読み、仕様を理解してから実装すること。

## 1. 最重要方針

本作は「葉っぱを直接運転するレースゲーム」ではない。

プレイヤーの主な楽しさは以下。

- 個体を見つける
- 自然環境で育てる
- 系統を継承する
- 出走を判断する
- レースを見守る
- 勝負所だけ葉脈炉で介入する
- 引退またはロストを受け入れ、次世代につなぐ

直接ステアリング操作を追加しないこと。

## 2. MVPで絶対に先行しないもの

以下はコアゲームの面白さを確認するまで実装しない。

- リアルタイムPvP
- 課金SDK
- 広告SDK
- 大規模バックエンド
- ガチャ
- 大量キャラクター
- 大量コース
- 高価な3Dアセット大量導入
- ボイス収録
- Switch固有対応

## 3. 開発順序

必ず以下の順序で進める。

1. Unityプロジェクトの最小構成
2. Pure C# のドメインモデル
3. 8体オートレースのデバッグ版
4. Seed固定による再現可能なレース
5. 葉脈炉
6. 耐久・故障・ロスト判定
7. 育成「ならし」
8. 成長フェーズ
9. 親木・接ぎ木・継承
10. 保存
11. ホーム・育成・レース等UI
12. 正式3Dモデル差替え
13. 非同期PvP

## 4. コーディング原則

- C#。
- Race / Training / Breeding の計算は可能な限り Pure C#。
- MonoBehaviourにゲームルールを書き込まない。
- UIとゲームロジックを分離する。
- ScriptableObjectは「定義データ」に使用し、プレイヤーの可変セーブ状態を直接保存しない。
- `Resources.Load` に依存しない。
- 乱数はレースごとに Seed を明示する。
- public field乱用禁止。SerializeField + propertyを基本とする。
- 巨大なManagerクラスを作らない。
- Singletonは必要最小限。
- 依存方向は Presentation -> Application -> Domain とする。
- DomainはUnityEngine型への依存を可能な限り避ける。
- 重要計算にEditModeテストを付ける。
- TODOを残す場合は理由と次工程を書く。

## 5. アーキテクチャ

推奨:

```text
Assets/NagaRace/
  Art/
  Audio/
  Prefabs/
  Scenes/
  ScriptableObjects/
  Scripts/
    Domain/
    Application/
    Infrastructure/
    Presentation/
  Tests/
    EditMode/
    PlayMode/
```

### Domain
- Character
- Stats
- Trait
- Growth
- Durability
- VeinFurnace
- Race
- Training
- Lineage
- Grafting

### Application
- RaceUseCase
- TrainingUseCase
- GraftingUseCase
- SaveUseCase

### Infrastructure
- JsonSaveRepository
- TimeProvider
- RandomProvider
- BackendAdapter（将来）

### Presentation
- Scene
- View
- Presenter / Controller
- VFX
- Animation

## 6. 開発中の見た目

正式3Dモデルがなくても開発を止めない。

- カプセル
- 板ポリ
- 色違いPrefab

で最初のレースを成立させる。

アート差替え可能な設計を守る。

## 7. レースの原則

- 同時8体。
- プレイヤーは進路を直接操作しない。
- AIがFlow Laneを選択する。
- レースは同Seed + 同入力なら再現可能にする。
- 基本的なレース時間目標は45〜90秒。
- 勝敗は能力だけでは100%固定しない。
- ただし完全運ゲーにしない。
- 強い個体は長期的に勝率が上がる。
- コース適性、天候、調子、性格、進路判断、ランダムイベントを含める。

## 8. 葉脈炉

葉脈炉は本作の重要操作。

- 葉力が一定以上で始動可能
- 始動すると加速
- 臨界までカウントダウン
- 任意解除可能
- 長く引っ張るほど強い
- 限界を超えるとOVER FLOW
- 耐久・炉性能・状態によって故障リスク
- 重度故障時はリタイアまたはロスト判定

連打速度が勝敗を決める設計は禁止。
「いつ発動し、いつ止めるか」を主役にする。

## 9. ロスト

完全ロストあり。

ただし以下を守る。

- 低リスク行動で突然ロストさせない
- 危険度を事前表示する
- プレイヤーがリスクを理解できる
- PvP相手の妨害だけで完全ロストさせない
- ロスト後はアルバム・系譜・戦績に残す
- 課金復活を前提にしない

## 10. キャラクターデザイン

`art_reference/nagarace_loose_style.png` を優先。

方向性:
- ゆるい
- 丸い
- 小さな手足
- 黒目中心のシンプルな顔
- 葉っぱ本来のシルエットが主役
- 悪葉っぱも怖すぎず、ちょっと悪そうでかわいい
- 戦闘ゲームのような鎧・武器・筋肉表現は避ける

## 11. 判断に迷った場合

新要素を追加する前に以下を確認する。

> それは「育てた葉っぱに愛着を持ち、次のレースを見たくなる」ことに寄与するか？

寄与しない場合はMVPへ追加しない。

## 12. 用語（v1.1）

- 「頑丈」= 主能力 toughness（ダメージ軽減）
- 「耐久」= HP相当の durability / maxDurability のみ
- 「炉耐久」= furnace.durability
- 「OVER FLOW」= 葉脈炉が SafeDuration を超えた RiskZone 状態の表示名

`IRandomProvider` と Seeded PRNG は Domain に置く（07 §10）。Infrastructure の RandomProvider は Seed 発行元を指す。

## 13. 実装完了時

各タスクごとに以下を報告する。

- 変更ファイル
- 実装内容
- テスト内容
- Unity Editorでの確認手順
- 残課題
- 次に着手すべきタスク
