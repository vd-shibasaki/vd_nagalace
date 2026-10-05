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
