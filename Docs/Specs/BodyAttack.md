# ボディアタック仕様
## 前提
空中で前方へ、やや上に大きく飛び込む短いアクション。しゃがみ（[Crouch.md](Crouch.md)）・ダッシュ（[Dash.md](Dash.md)）とは別の、アクション軸（[MotionState.md](MotionState.md)）の状態として持つ。
前方の攻撃判定は予定しているが、まだ攻撃の概念がないので、今回は作らない。

## 1. 確定仕様
### 1.1. 発動
- ボタンは `<Gamepad>/buttonEast`（`BodyAttack` アクション）。押した瞬間に発動する。
- 空中だけで発動できる。接地中は発動しない。
- 1回の滞空につき1回まで。着地すると回復する。
- 着地の直前に押した発動も受け付ける（先行入力）。受け付ける時間は、ジャンプの先行入力と同じ（0.08秒、約5フレーム）。
- 発動時に急降下を解除する。

### 1.2. 向き
- 発動時に、左右を倒していればその方向、倒していなければキャラの向き（[MotionState.md](MotionState.md) 2.5）へ飛ぶ。
- 発動時にキャラの向きをその方向に更新し、発動中は向きを変えない。

### 1.3. 動き
- 斜め上への初速を与え、重力で放物線を描いて落ちる。
- 水平速度は、基本速度（12）からダッシュ最高速（28）の間の値で、基準値は22にする。発動時点でそれより速い（ダッシュの速度が基準値より速い）場合は、その速度を引き継ぐ。ただしダッシュ最高速は超えない。
- 発動中は、左右入力で水平速度を変えない（空中の加減速を適用しない）。

### 1.4. 終了
- 着地したら終了する。終了後は、通常の地上のモーションへ移る。頭上に天井があって立てないときは、しゃがみになる（[Crouch.md](Crouch.md)）。
- 壁に当たったら終了する。壁はコライダーの接触で判定する。このとき水平速度は0にする。
- ジャンプボタンでは中断できない。

### 1.5. 当たり判定
- ボディアタック中のコライダーは、しゃがみと共有する（しゃがみのコライダーを使う）。
- コライダーを切り替える移行時間は、しゃがみと同じ数値（0.08秒）にする。設定はしゃがみとは別にし、`BodyAttackSettings.TransitionSeconds` として持つ。
- 前方の攻撃判定は、前方に円で作る。まだ攻撃の概念がないので、今回は実装しない。

### 1.6. 他の状態との関係
- 急降下: 発動時に解除する。発動中は急降下に入らない。
- ダッシュ: 発動時にダッシュ状態を解除する（速度は 1.3 のとおり引き継ぐ）。
- しゃがみ: 空中だけの動きなので、しゃがみ状態とは重ならない。コライダーだけを共有する。

## 2. 決まっていない仕様
- 初速の上向きの大きさ（案: 15。頂点まで約1ユニット、滞空約0.27秒）
- 発動中の重力（案: 通常の重力。落下の倍率は掛けない）
- ダッシュボタンを押したまま終了したとき、地上のモーションに移った後の速度（案: 通常の地上の歩きの規則に従う）
- 攻撃判定の円の大きさ・位置・ダメージ（攻撃の概念ができてから）
- スプライト（専用の絵がないので、案: `PlayerJumping` か `PlayerCrouching` を共用する）

## 3. 実装方針
[Responsibilities.md](../Notes/Responsibilities.md) の形に合わせて、設定は `ScriptableObject`、処理は普通のクラスにする。

### 3.1. パラメータ（案）
`BodyAttackSettings`（`ScriptableObject`）に置く。

| 名前 | 既定値 | 内容 |
|---|---|---|
| `Speed` | 22 | 水平速度の基準値（ユニット/秒）。基本速度（`BasicMoveSettings.MoveSpeed` 12）からダッシュ最高速（`DashSettings.MaxDashSpeed` 28）の間にする |
| `UpwardSpeed` | 15 | 発動時に与える上向き速度（ユニット/秒） |
| `TransitionSeconds` | 0.08 | コライダーを切り替える移行時間（秒）。しゃがみの `TransitionSeconds` と同じ数値だが、設定は別に持つ |
| `BufferSeconds` | 0.08 | 着地の直前に押した発動を受け付ける時間（秒）。ジャンプの `JumpBufferSeconds` と同じ数値だが、設定は別に持つ |

入力は `BodyAttack` アクション（`buttonEast`）を `PlayerMover` の Inspector に割り当てる。

### 3.2. クラスと状態（案）
`BodyAttack`（普通のクラス）が、発動・移動・終了を担当する。

| メンバー | 役割 |
|---|---|
| `IsActive` | 発動中か |
| `RequestAttack()` | 押された時刻を記録する（ボタンのイベントから呼ぶ。`BufferSeconds` の間は有効） |
| `TryStart(input, facingDirection, isGrounded, currentVelocityX, maxDashSpeed)` | 発動できるときに発動して、向きを返す。空中1回の制限もここで管理する |
| `UpdateVelocity()` | 発動中の水平速度を保つ |
| `Step(isGrounded, isTouchingWall)` | 着地・壁で終了させる。着地で空中1回の制限を回復する。`isTouchingWall` は `Rigidbody2D` の接触（法線がほぼ水平な面）から、`PlayerMover` が求めて渡す |
| `Reset()` | 非発動に戻す（リスポーン用） |

`PlayerMover` では次の順にする。
1. 接地判定
2. ジャンプ
3. ボディアタックの開始（`TryStart`）。開始したフレームに `Dive.Cancel()`・`Dash.Reset()` を呼び、`Facing` に向きを設定する
4. 急降下、ダッシュの状態遷移（ボディアタック中は呼ばない）
5. 向き（ボディアタック中は更新しない）
6. 重力スケール
7. 水平移動（ボディアタック中は `BodyAttack.UpdateVelocity`）
8. ボディアタックの終了判定（`Step`）
9. 落下速度の制限

### 3.3. 変更するファイル
| ファイル | 内容 |
|---|---|
| `Scripts/Settings/BodyAttackSettings.cs`（新規） | 3.1 のパラメータ |
| `Scripts/Abilities/BodyAttack.cs`（新規） | 3.2 のクラス |
| `Scripts/Abilities/Facing.cs` | 向きを外から設定する入口を追加する |
| `Scripts/Abilities/Crouch.cs` | コライダーの切り替えを、ボディアタックからも使えるようにする |
| `Scripts/PlayerMover.cs` | `BodyAttack` を作って呼ぶ。入力のイベントを購読する |
| `Scripts/PlayerVisual.cs` | ボディアタック中のスプライトを追加する |
| `Data/BodyAttackSettings.asset`（新規、Unity で作成） | `BodyAttackSettings` の実体 |
| Input Action（`InputSystem_Actions`） | `BodyAttack` アクションを新設して `buttonEast` に割り当てる（Unity で作業） |

ボディアタックの実装は、しゃがみのコライダー切り替え（[Crouch.md](Crouch.md)）ができてからにする。

## 4. 今後のタスク
- 2 の決まっていない仕様を決める
- 攻撃の概念ができたら、前方の攻撃判定を作る