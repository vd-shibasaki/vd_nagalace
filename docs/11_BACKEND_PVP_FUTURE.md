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
