# モーション状態仕様
## 前提
歩き・走り・しゃがみ・ジャンプを1本のステートマシンに並べず、性質の違う軸に分けて持つ。
並べると「走りジャンプ」「しゃがみジャンプ」などの組み合わせごとに状態が必要になり、遷移が増え続けるため。
アニメーションの状態は、各軸の組み合わせから導出する。

| 軸 | 状態 | 実装 |
|---|---|---|
| 垂直 | 接地 / 空中（上昇・下降はY速度、急降下は別フラグ） | `isGrounded`、`isDiving` |
| 水平 | `Normal` / `Dash` / `Brake` | `MoveState`（[Dash.md](Dash.md) 参照） |
| 姿勢 | 立ち / しゃがみ（しゃがみ滑りを含む） | 未実装（[Crouch.md](Crouch.md) 参照） |
| アクション | なし / ボディアタック | 未実装 |
| 向き | 左 / 右 | `facingDirection`（2.5 参照） |

## 1. 確定仕様
パラメータの名前は、設定アセットの項目名（例: `JumpSettings.JumpHeightUnits`）に対応する。以下のキャメルケースは、その項目を指す。

### 1.1. 導出する状態
| 状態 | 条件 |
|---|---|
| 待機・歩き | 接地、水平速度が歩行の最高速以下（スプライトは待機を共用する） |
| 走り | 接地、水平速度が歩行の最高速を超える（`IsRunning`） |
| ブレーキ | ダッシュが `Brake` |
| ジャンプ上昇 | 空中、Y速度が正 |
| 落下 | 空中、Y速度が0以下 |
| 急降下 | 空中、`isDiving` |

走りジャンプのように、水平と垂直は独立して重なる。ダッシュの速度は空中・着地後も維持する。

### 1.2. ジャンプ
- 高さ（`jumpHeightUnits`）と頂点までの時間（`timeToApexSeconds`）から重力と初速を逆算する。
- 上昇中にボタンを離すと上昇速度を `jumpReleaseVelocityMultiplier` 倍にする（短押しで低いジャンプ）。
- 落下中は重力を `fallGravityMultiplier` 倍にし、落下速度は `maxFallSpeedUnits` で制限する。
- 先行入力（`jumpBufferSeconds`）とコヨーテタイム（`coyoteSeconds`）の両方を満たすときにジャンプする。
- 上昇中は接地扱いにしない（ジャンプ直後の二重発動を防ぐ）。

### 1.3. 急降下
- 下はじき（`diveStickThreshold`）または `diveAction` の押下を `diveBufferFrames` フレーム先行受付する。
- 空中かつ落下中（Y速度0以下）に受け付けると、落下速度を `maxFallSpeedUnits * diveSpeedMultiplier` まで上げる。
- 接地すると解除する。`CancelDive()` で外部からも解除できる。
- ダッシュの加速には影響しない。

### 1.4. 処理順
`FixedUpdate` は次の順に処理する。順序には意味がある。

1. 接地判定
2. ジャンプ（後続の水平移動が上書きしないよう先に行う）
3. 急降下
4. ダッシュの状態遷移（`Dash.UpdateState()`）
5. 向き（`Facing.Step()`）
6. 重力スケール（`Jump.UpdateGravityScale()`）
7. 水平移動
8. 落下速度の制限

### 1.5. ボディアタック
[BodyAttack.md](BodyAttack.md) に分けた。

## 2. 決まっていない仕様
### 2.1. 入力の割り当て（決定済み）
- ボディアタックは `buttonEast` で維持する。
- 急降下に `buttonEast` を割り当てていたのは誤り。急降下は下はじきだけで、`PlayerMover` の `diveAction` は未設定にする。
- しゃがみはスティック下で行うので、`Crouch` アクションの `buttonEast`（と `C` キー）の割り当ては外す。`BodyAttack` アクションを新設して `buttonEast` に割り当てる。
- Input Action の変更と `PlayerMover` の `diveAction` の割り当ての解除は、Unity で行う。

### 2.2. ボディアタック
[BodyAttack.md](BodyAttack.md) に分けた。

### 2.3. しゃがみ
[Crouch.md](Crouch.md) に分けた。

### 2.4. 状態の組み合わせ
| | 立ち | しゃがみ |
|---|---|---|
| 接地 + 歩き | 可 | 可（しゃがみ歩き） |
| 接地 + ダッシュ | 可 | しゃがみ滑りに移る |
| 空中 | 可 | 不可（下入力は急降下） |
| ボディアタック | 空中だけ（[BodyAttack.md](BodyAttack.md)） | 重ならない（コライダーだけ共有） |

### 2.5. キャラの向き
実装済み。`Facing` クラスが向き（-1 / 1）を持ち、`PlayerMover.FacingDirection` で外部から読める。

- 更新: ダッシュの状態遷移の後に、左右を `HoldThreshold` 以上倒している間、その方向に更新する。入力を離しても維持する
- 空中: 地上と同じく、逆入力ですぐ向きを変える
- ブレーキ中: 更新しない。滑り終わるまで元の向きを保つ
- しゃがみ滑り中: 更新しない（[Crouch.md](Crouch.md)）
- ボディアタック中: 発動時に向きを設定し、発動中は更新しない（[BodyAttack.md](BodyAttack.md)）
- スプライト: `PlayerVisual` が `flipX` で反転する。右向きの絵を前提にする
- リスポーン: `Respawn()` で右に戻す

## 3. 実装方針
実装先は `PlayerMover.cs` と、そこから呼ぶ各機能のクラス（[Responsibilities.md](../Notes/Responsibilities.md)）。状態の遷移は軸ごとに1か所にまとめ、速度は `linearVelocity` を直接書き換える。

### 3.1. ボディアタックの実装
[BodyAttack.md](BodyAttack.md) の「実装方針」を参照。

### 3.2. しゃがみの実装
[Crouch.md](Crouch.md) の「実装方針」を参照。

## 4. 今後のタスク
- Input Action の変更（2.1）
- ボディアタックのヒット判定と、ヒット時の挙動
- しゃがみの実装（[Crouch.md](Crouch.md)）
- アニメーション状態の導出ロジックは `PlayerVisual` で実装済み（しゃがみのスプライトを除く）
