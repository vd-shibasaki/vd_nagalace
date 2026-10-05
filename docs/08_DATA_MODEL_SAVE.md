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
