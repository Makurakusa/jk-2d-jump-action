## 責務分離方針
将来他の横スクロールアクションでも使い回すことを視野に入れつつ、今はこのプロジェクト内で分ける。パッケージ化や asmdef での分離は、設計が固まってから決める。
[Refactoring.md](Refactoring.md) の手順 1・2 をこの方針で行う。

### 1. 再利用のためのルール
今は `Core` のような別の場所へは分けず、`Features/Player/` の中で分ける（サブフォルダは `Settings/` と `Abilities/`）。ただし後で切り出せるように、次を守る。

- 機能のクラス（3）と設定（2）は、Input System・スプライト・`KillZone`・シーンの構成を参照しない
- 入力は値で受け取り、読み取りは `PlayerMover` だけが行う
- 機能のクラスは別の機能のクラスを直接参照しない（値の受け渡しは `PlayerMover` が行う）
- プレイヤー固有のもの（見た目、リスポーン、ボディアタック、しゃがみ）は機能のクラスに入れない

これを守っていれば、将来フォルダごと別の場所（別プロジェクトやパッケージ）へ移せる。

### 2. パラメータ（ScriptableObject）
機能ごとに分ける。アセット同士は参照しない。複数のアセットの値を使う機能は、それぞれを別々に受け取る（3）。

| アセットの型 | 中身 |
|---|---|
| `BasicMoveSettings` | 歩き（`moveSpeed`、`groundAcceleration`、`airAcceleration`、`airDeceleration`、`reverseAccelerationMultiplier`）、左右を「倒している」とみなす値（`holdThreshold`） |
| `DashSettings` | ダッシュとブレーキ（`maxDashSpeed`、`dashAcceleration`、`brakeAcceleration`、`brakeDuration`） |
| `JumpSettings` | ジャンプの高さ・時間、落下の倍率と上限（`maxFallSpeedUnits`）、ボタンを離したときの倍率、先行入力、コヨーテタイム |
| `DiveSettings` | 急降下の倍率（通常の落下速度の上限に掛ける）、スティックの閾値、先行入力フレーム数 |

- ブレーキは「最高速のダッシュからの反転」なので、ダッシュ側（`DashSettings`）に入れる。
- `holdThreshold` は歩きの反転判定・向きの更新・ダッシュの継続判定のすべてで使うため、`BasicMoveSettings` に置く。
- 型名は `Player` を付けない。敵や別のプレイヤーでも同じ型で動かせるようにするため。アセットの実体はプレイヤー用として `PlayerBasicMove.asset` のように名付ける。
- シーン固有のもの（`groundLayerMask`、`groundCheckTransform`、接地判定の箱の大きさ、`InputActionReference`、`bodySprite`）はアセットに入れず、`PlayerMover` 側に残す。

### 3. 機能（普通のクラス）
各機能は `MonoBehaviour` にせず、普通のクラスにする（種類の一覧は 5.1）。`PlayerMover` の `Awake` で、`Rigidbody2D` と設定アセットを渡して作る。入力は値で受け取り、`Rigidbody2D` の速度を書き換える。

| クラス | 責務 | 受け取る設定 | 必須か |
|---|---|---|---|
| `GroundCheck` | 接地判定（箱を下へ飛ばす、面の向きを見る） | なし（箱の大きさ・距離・レイヤーは `PlayerMover` から渡す） | 必須 |
| `BasicMove` | 歩き、空中の加減速、反転。停止中かどうかの判定 | `BasicMoveSettings` | 必須 |
| `Jump` | ジャンプの開始、先行入力、コヨーテタイム、重力スケール、落下速度の制限 | `JumpSettings` | 必須 |
| `Dash` | ダッシュとブレーキの状態遷移と速度 | `DashSettings`、`BasicMoveSettings` | 任意 |
| `Dive` | 急降下の先行入力と開始・解除 | `DiveSettings` | 任意 |
| `Facing` | 向きの更新 | `BasicMoveSettings` | 任意 |

- 「任意」の機能は、`PlayerMover` が作らなければその機能なしで動く（ダッシュのないゲームなら `Dash` を作らない）。
- 機能どうしは直接参照しない。必要な値は、`PlayerMover` が引数で渡す。例:
  - `Jump` の落下速度の上限に、`Dive` の倍率（急降下中でなければ1）を掛けて渡す
  - `Dive` の開始時の落下速度に、`Jump` の上限を渡す
  - `Facing` に、ブレーキ中かどうかを渡す
- 水平速度は、`PlayerMover` が `Dash` が有効なときは `Dash` を、そうでなければ `BasicMove` を呼んで決める。`BasicMove` は `Dash` を知らない。
- 重力スケールを書き換えるのは `Jump` だけ。停止中に重力を切るかどうかは、`BasicMove` の判定を `PlayerMover` が `Jump` に渡す。
- 入力は `MoveInput`（左右・上下の値、ダッシュボタンを押しているか）という値の型で渡す。Input System は知らない。
- ジャンプ・急降下の押下やボタンを離したことは、`RequestJump()`、`ReleaseJump()`、`RequestDive()` のようなメソッドで受け取る。
- 呼び出しの順序には意味があるため、`Update` や `FixedUpdate` は持たせない。

### 4. 組み立て側（このゲーム固有）
| クラス | 責務 |
|---|---|
| `PlayerMover`（`MonoBehaviour`） | 機能のクラスを作って、`FixedUpdate` の中で決まった順に呼ぶ。`InputActionReference` を読んで `MoveInput` として渡す。機能どうしの値の受け渡しを行う。`Respawn` などの外部 API を持つ |
| `PlayerVisual`（`MonoBehaviour`） | 状態から選んだスプライトの表示と左右反転。`PlayerMover` を読むだけ |
| ボディアタック、しゃがみ | 固有の機能。機能のクラスと同じ形で追加する（今回の対象外） |

### 5. 必要なファイル
置き場所は、特に書かない限り `Assets/_Project/Features/Player/` 配下。`.meta` は Unity が生成する。

```
Assets/_Project/
  Scenes/TestArea.unity
  Features/Player/
    Arts/ Materials/ Prefabs/        （既存）
    Data/                            （新規。5.3）
      PlayerBasicMove.asset
      PlayerDash.asset
      PlayerJump.asset
      PlayerDive.asset
    Scripts/
      PlayerMover.cs                 （既存。更新）
      PlayerVisual.cs                （新規）
      MoveInput.cs                   （新規）
      Settings/                      （新規。設定のクラス）
      Abilities/                     （新規。機能のクラス）
```

#### 5.1. 追加するスクリプト（`Scripts/`）
`MonoBehaviour` は `PlayerMover` と `PlayerVisual` の2つだけ。設定は `ScriptableObject`、機能は普通のクラス、`MoveInput` は値の型。

| ファイル | 種類 | 内容 | 手順 |
|---|---|---|---|
| `Settings/BasicMoveSettings.cs` | `ScriptableObject` | 歩きの設定（2） | 1 |
| `Settings/DashSettings.cs` | `ScriptableObject` | ダッシュ・ブレーキの設定 | 1 |
| `Settings/JumpSettings.cs` | `ScriptableObject` | ジャンプ・落下の設定 | 1 |
| `Settings/DiveSettings.cs` | `ScriptableObject` | 急降下の設定 | 1 |
| `MoveInput.cs` | `struct`（値の型） | 左右・上下の値とダッシュボタンの状態（3） | 2 |
| `Abilities/GroundCheck.cs` | 普通のクラス | 接地判定 | 2 |
| `Abilities/Jump.cs` | 普通のクラス | ジャンプ・重力・落下速度の制限 | 2 |
| `Abilities/Dive.cs` | 普通のクラス | 急降下 | 2 |
| `Abilities/BasicMove.cs` | 普通のクラス | 歩き、空中の加減速、反転、停止中の判定 | 2 |
| `Abilities/Dash.cs` | 普通のクラス | ダッシュとブレーキ。状態の `enum`（`DashState`: 非有効 / ダッシュ / ブレーキ）もここに置く | 2 |
| `Abilities/Facing.cs` | 普通のクラス | 向きの更新 | 2 |
| `Abilities/Rigidbody2DExtensions.cs` | `static` クラス | `BasicMove` と `Dash` が共有する、水平速度を目標へ近づける拡張メソッド | 2 |
| `PlayerVisual.cs` | `MonoBehaviour` | スプライトの表示と反転 | 3 |

#### 5.2. 更新するファイル
| ファイル | 内容 |
|---|---|
| `Assets/_Project/Features/Player/Scripts/PlayerMover.cs` | 設定アセットを参照する。機能のクラスを呼ぶ組み立て役にする。古い `[SerializeField]` と `bodySprite` を削除する |
| `Assets/_Project/Scenes/TestArea.unity` | Player の参照を Unity で割り当てる（テキストを直接は編集しない） |
| `Docs/Specs/Dash.md`、`Docs/Specs/MotionState.md` | 実装方針の表を、新しいクラス名とアセット参照に合わせる |

`KillZone.cs` は `Respawn` を呼ぶだけなので変更しない。

#### 5.3. Unity で作るアセット（`Data/`）
| ファイル | 内容 |
|---|---|
| `PlayerBasicMove.asset` | `BasicMoveSettings` の実体。値はシーンの実値に合わせる |
| `PlayerDash.asset` | `DashSettings` の実体 |
| `PlayerJump.asset` | `JumpSettings` の実体 |
| `PlayerDive.asset` | `DiveSettings` の実体 |

スクリプトの `.meta` ができた後なら、この `.asset` を私が書くこともできる。

### 6. 手順への反映
[Refactoring.md](Refactoring.md) の手順に対応する。表の「手順」列はこの番号。

1. パラメータを ScriptableObject に出す（5.1 の `Settings/`、5.3）
2. 機能を1つずつ切り出す。依存が少ない順は `GroundCheck`、`Jump`、`Dive`、`BasicMove`、`Dash`、`Facing`。先に `MoveInput` を作る
3. `PlayerVisual` を追加する
4. ドキュメントを更新する（5.2）

フォルダやパッケージでの切り分けは、この分割が動作を変えずに終わってから考える。

### 7. 決まっていないこと
- 機能のクラス名（`Jump`、`Dive` は短いが、メソッド名や状態名と紛らわしくないか）
- `GroundCheck` の判定の対象（床の法線の下限など）を、設定に含めるか
- `Data/` の名前（`Scripts/Settings/` と紛らわしくないか）
- 他のプロジェクトへの共有方法（コピーかパッケージか）。設計が固まってから決める
