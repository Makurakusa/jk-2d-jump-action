# PlayerMover リファクタリング方針
## 1. 問題
### 1.1. 責務が大きい
[PlayerMover.cs](../../Assets/_Project/Features/Player/Scripts/PlayerMover.cs) が次をすべて持っている（約400行、`[SerializeField]` 約35個）。

| 責務 | 主な処理 |
|---|---|
| 入力 | `OnEnable` / `OnDisable` の購読、`Update` の読み取り、`OnJump` / `OnJumpReleased` |
| 接地判定 | `UpdateGroundedState`、`IsTouchingGround` |
| ジャンプ | `ConfigureJumpPhysics`、`JumpIfRequested`、先行入力、コヨーテタイム |
| 重力・落下 | `UpdateGravityScale`、`ClampFallSpeed` |
| 急降下 | `DiveIfRequested`、`CancelDive` |
| 水平移動 | `MoveState`、`UpdateMoveState`、`UpdateHorizontalMovement`、`UpdateDashVelocity` |
| 向き | `UpdateFacing`、`SetFacing`（スプライトの反転を含む） |
| 外部 API | `Respawn`、`CancelDive` |

今後ボディアタック・しゃがみ・スプライト切り替えが加わると、さらに膨らむ。

### 1.2. パラメータの実値とコードが分離していない
Unity はコンポーネントを付けた時点の値をシーンに保存する。スクリプトの既定値を変えても、既存のプレイヤーには反映されない。
実際に値が食い違っている（[TestArea.unity](../../Assets/_Project/Scenes/TestArea.unity) のプレイヤーと比較）。

| 名前 | シーンの値（実際に動く値） | コードの既定値 | [Dash.md](../Specs/Dash.md) |
|---|---|---|---|
| `maxDashSpeed` | 28 | 18 | 18 |
| `brakeAcceleration` | 60 | 90 | 90 |
| `jumpHeightUnits` | 9 | 6 | なし |

- どれが正しい値か、コードを見ても Inspector を見ないと分からない。
- シーンの `brakeAcceleration` 60 は、`maxDashSpeed / brakeDuration`（28 / 0.2 = 140）を下回っている。コメントの条件（最高速を `brakeDuration` で止める）を満たさず、ブレーキに約0.47秒かかる。
- 値がシーンに埋まっているので、プレハブ化や別シーンへの複製のたびに値が散らばる。

## 2. 作業の分担
- `.cs` の追加・編集は私が行える。`.meta` は Unity が開いたときに生成する。
- Unity エディタでの作業はこちらで行う必要がある。
  - ScriptableObject アセットの作成（または、生成された `.meta` の GUID を使って私が `.asset` を書く）
  - コンポーネントのアタッチ、参照の割り当て
- 各ステップは、Unity で再コンパイルして動作確認できる区切りにする。

## 3. 方針
アセットの型、クラス名、置き場所、`MonoBehaviour` かどうかの区分は [Responsibilities.md](Responsibilities.md) を正とする。ここでは概要だけを書く。

### 3.1. パラメータは ScriptableObject に出す
関心ごとに4つに分け、`PlayerMover` はアセットへの参照だけを持つ。

| アセットの型 | 中身 |
|---|---|
| `BasicMoveSettings` | 歩きと `holdThreshold` |
| `DashSettings` | ダッシュとブレーキ |
| `JumpSettings` | ジャンプ、落下、先行入力、コヨーテタイム |
| `DiveSettings` | 急降下 |

- シーン固有のものはコンポーネント側に残す。具体的には、`groundLayerMask`、`groundCheckTransform`、接地判定の箱の大きさ、`InputActionReference`、`bodySprite`。
- アセットの初期値は、シーンの値（1.2 の表の左列）に合わせる。実際に遊んでいる値を正とする。
- `brakeAcceleration` は、`OnValidate` で下限（`maxDashSpeed / brakeDuration`）を下回ると警告する。
- 値の正本はアセットにする。[Dash.md](../Specs/Dash.md) のパラメータ表から既定値の列を外し、値はアセットを参照する形にする。

### 3.2. 責務ごとにクラスを分ける
`PlayerMover` は `MonoBehaviour` のまま残し、`Rigidbody2D`・入力・`FixedUpdate` の呼び出し順だけを持つ。
順序には意味がある（接地 → ジャンプ → 急降下 → 水平の状態 → 向き → 重力 → 水平移動 → 落下制限）ため、`FixedUpdate` は1か所に置く。

各責務は `MonoBehaviour` にせず、普通のクラスにして `PlayerMover` から呼ぶ。理由は次のとおり。
- 各コンポーネントに `FixedUpdate` を持たせると実行順が不定になる。
- Unity 側でコンポーネントを追加・割り当てする手間が増える。

| クラス | 責務 |
|---|---|
| `GroundCheck` | 接地判定（箱を下へ飛ばす、面の向きを見る） |
| `Jump` | ジャンプの開始、先行入力、コヨーテタイム、重力スケール、落下速度の制限 |
| `Dive` | 急降下の先行入力と開始・解除 |
| `BasicMove` | 歩き、空中の加減速、反転 |
| `Dash` | ダッシュとブレーキの状態遷移と速度 |
| `Facing` | 向きの更新（反転はしない） |
| `PlayerVisual`（`MonoBehaviour`） | 状態から選んだスプライトの表示と左右反転。ここだけ `SpriteRenderer` に触れる |

`PlayerVisual` は `PlayerMover` を読むだけにする（逆の参照はしない）。Unity 側でプレイヤーに追加する必要がある。

## 4. 手順
各ステップで挙動を変えず、Unity で確認してから次へ進む。

1. **パラメータを ScriptableObject に出す**（3.1）
   1. 設定クラスを追加する（私）
   2. Unity でアセットを作成し、値をシーンの値に合わせる（こちら。`.meta` ができていれば私が `.asset` を書くこともできる）
   3. `PlayerMover` の Inspector にアセットを割り当てる（こちら）
   4. `PlayerMover` の古い `[SerializeField]` を削除する（私）。削除するとシーンに保存された古い値は消えるため、アセットに値を移してから行う
2. **責務を分ける**（3.2）。結合が弱い順に、1つずつ切り出す。
   1. `GroundCheck`
   2. `Jump`
   3. `Dive`
   4. `BasicMove`
   5. `Dash`
   6. `Facing`
3. **見た目を分ける**: `PlayerVisual` を追加し、`bodySprite` と反転処理を `PlayerMover` から移す
4. **ドキュメントを更新する**: [MotionState.md](../Specs/MotionState.md)（実装方針）と [Dash.md](../Specs/Dash.md)（パラメータ表）

## 5. 確認項目（挙動が変わっていないこと）
ステップごとに、次をプレイして確認する。
- 歩き・ダッシュの最高速、ダッシュの反転とブレーキ
- ジャンプの高さと頂点までの時間、短押しジャンプ
- 先行入力とコヨーテタイム
- 急降下の開始と接地での解除
- 停止中に足場の端で滑らない
- `KillZone` からの `Respawn`

## 6. 決まっていないこと
- `brakeAcceleration` の扱い（シーンの60のまま遊ぶか、仕様どおり140以上に直すか）
- クラス名やフォルダ名の未決事項は [Responsibilities.md](Responsibilities.md) の「決まっていないこと」にまとめる
- ボディアタック・しゃがみは、分割後に新しいクラスとして追加する（今回の対象外）
