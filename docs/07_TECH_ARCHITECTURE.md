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
