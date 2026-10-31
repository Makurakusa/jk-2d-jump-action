# ダッシュ仕様
## 前提
快適性と機動力重視のジャンプアクションのため、ダッシュは次の役割を持つ。
- 水平方向の移動性能の向上（ローリスクハイリターン）
- ダッシュ中のジャンプの高さ上昇

## 1. 確定仕様
### 1.1. ダッシュ状態
- ダッシュボタンを押しながら移動すると、ダッシュ状態に移行する。
- ダッシュ状態では速度の上限が上がる（`maxDashSpeed`）。

### 1.2. 付与・解除条件
- 空中でダッシュ状態に移行することはできないが、空中で解除することはできる。
- 横からの力で加速した場合、速度が一定値を下回るまでダッシュ状態を継続する（力による加速の実装時に追加）。
- 着地時もダッシュ時の速度を維持する。

### 1.3. 反転（ブレーキ状態）
- 最速ダッシュで反転すると「ブレーキ状態」に移行する。
  - 0.2秒（`brakeDuration`）の間、強い減速をかける。
  - その後、最速で反転ダッシュを出せる。
- 最速でないときは即反転し、加速度を2倍（`reverseAccelerationMultiplier`）にして切り返しやすくする。歩きの反転も同じ。

### 1.4. 急降下との関係
- 急降下はダッシュによる加速に影響を与えない。

## 2. 決まっていない仕様

## 3. 実装方針
実装先は `PlayerMover.cs`。水平方向の状態を `MoveState`（`Normal` / `Dash` / `Brake`）で持ち、速度は力ではなく `linearVelocity` を直接書き換える。

### 3.1. パラメータ
| 名前 | 既定値 | 内容 |
|---|---|---|
| `maxDashSpeed` | 18 | ダッシュ中の最高速度（ユニット/秒）。旧 `dashSpeedUnits` |
| `dashAcceleration` | 90 | 接地中にダッシュ速度へ近づける強さ |
| `brakeAcceleration` | 90 | ブレーキ状態で速度を0へ近づける強さ。`maxDashSpeed / brakeDuration` 以上にして、最高速から `brakeDuration` 内に止める |
| `brakeDuration` | 0.2 | ブレーキ状態が続く時間（秒） |
| `holdThreshold` | 0.6 | 左右を「倒している」とみなす入力値（0〜1） |
| `reverseAccelerationMultiplier` | 2 | 進行方向と逆へ入力したときの加速度の倍率（最高速ダッシュの反転を除く） |
| `dashAction` | なし | ダッシュボタンの Input Action（Inspector で割り当て） |

空中のダッシュは既存の `airAcceleration`（入力あり）と `airDeceleration`（入力なし）を使う。

### 3.2. メソッド
| メソッド | 役割 |
|---|---|
| `Update()` | `dashAction` の押下状態を `isDashHeld` に読み取る |
| `UpdateMoveState()` | 状態遷移を行う唯一の場所（下記） |
| `StartDash(direction)` | `Dash` に入り、`dashDirection` を記録する |
| `StartBrake()` | `Brake` に入り、`brakeRemainingSeconds` を `brakeDuration` にする |
| `UpdateDashVelocity()` | `Dash` 中の水平速度を決める |
| `UpdateHorizontalMovement()` | 状態に応じて水平速度を決める（`Dash` / `Brake` / 歩き） |

#### 状態遷移（`UpdateMoveState()`）
- `Normal` → `Dash`: 接地中、ダッシュボタン押下、左右を倒している、かつ現在速度が0または入力と同じ向き
- `Dash` → `Normal`:
  - ダッシュボタンを離した（空中でも可）
  - 最高速未満で反転入力した
  - 接地中に左右入力を離した
- `Dash` → `Brake`: 最高速で反転入力した
- `Brake` → `Dash`: `brakeDuration` 経過後、ダッシュボタン押下かつ左右を倒している
- `Brake` → `Normal`: 上記以外で `brakeDuration` が経過した

#### 速度の決め方
- `Dash`（左右を倒している）: `dashDirection * maxDashSpeed` へ近づける。接地中は `dashAcceleration`、空中は `airAcceleration`
- `Dash`（左右を離している）: `airDeceleration` で0へ近づける
- `Brake`: `brakeAcceleration` で0へ近づける
- `Normal`（進行方向と逆へ入力）: 歩きの目標速度へ、通常の `reverseAccelerationMultiplier` 倍の加速度で近づける。速度が0を越えたあと、ダッシュボタン押下中なら `Dash` に入り直す
- 着地しても `Dash` は解除されないため、速度は維持される

## 4. 既存不具合
### 4.1. 着地時にダッシュ速度が下がる
#### 原因

#### 対応内容

## 5. 今後のタスク（実装が充実したら）
- ダッシュの汎用メソッド化
- ダッシュのパラメータのデータ分割