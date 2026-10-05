# Claude Codeへ最初に渡すプロンプト

以下をそのまま使用できます。

---

このリポジトリはUnityゲーム「ながレース」の新規開発です。

最初に以下を必ず読んでください。

- CLAUDE.md
- docs/01_PRODUCT_REQUIREMENTS.md
- docs/02_GAME_DESIGN.md
- docs/04_RACE_SIMULATION.md
- docs/07_TECH_ARCHITECTURE.md
- docs/09_MVP_IMPLEMENTATION_PLAN.md
- docs/10_ACCEPTANCE_TESTS.md
- docs/14_SPEC_REVIEW.md（v1.1 レビュー。§5 に M0/M1 実装計画案あり）

今回は **Milestone 0 と Milestone 1 のみ** 着手してください。
UI、正式3Dモデル、バックエンド、課金はまだ作らないでください。

目的は、

「8体の葉っぱがFlow Graphに沿って自動レースし、能力・適性・性格・Seedによって着順が変わるPure C#のレースコア」

を作ることです。

要件:

1. DomainロジックをUnityEngineへ極力依存させない
2. Seed付きRandomProviderを用意
3. 8体同時レース
4. FlowSegment分岐
5. 順位計算
6. ゴールタイム
7. CharacterStats差を結果へ反映
8. 性格によるルート選択差
9. EditMode Unit Test
10. Debug表示用の最低限View

作業開始前に、
- 作成予定ファイル
- クラス責務
- 実装手順
- テスト方針

を簡潔に提示してください。

その後実装してください。

実装後は、
- 変更ファイル一覧
- Unity Editorでの起動手順
- テスト実行方法
- 現在できること
- 未実装
- 次Milestoneへの提案

を報告してください。

仕様にない大規模な機能追加は行わないでください。
判断が必要な箇所は、まずデータ調整可能な暫定値として実装してください。
