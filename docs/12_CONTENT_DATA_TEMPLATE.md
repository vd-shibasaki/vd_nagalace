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
