using UnityEngine;
using UnityEngine.InputSystem;

// プレイヤーの左右移動・ジャンプ・急降下を担当する。
// 移動は力ではなく速度を直接書き換えるため、摩擦に依存しない（壁に当たっても減速しにくい）。
// 処理はすべて FixedUpdate で行い、Update では入力の読み取りだけを行う。
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMover : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] BasicMoveSettings moveSettings; // 歩きのパラメータ
    [SerializeField] DashSettings dashSettings; // ダッシュ・ブレーキのパラメータ
    [SerializeField] JumpSettings jumpSettings; // ジャンプ・落下のパラメータ
    [SerializeField] DiveSettings diveSettings; // 急降下のパラメータ

    [Header("Ground Check")]
    [SerializeField] LayerMask groundLayerMask; // 接地判定の対象にするレイヤー
    [SerializeField] Transform groundCheckTransform; // 接地判定を行う位置
    [SerializeField] Vector2 groundCheckSize = new Vector2(0.8f, 0.05f); // 接地判定の箱の大きさ（プレイヤー幅より少し狭くする）
    [SerializeField] float groundCheckDistanceUnits = 0.05f; // 箱を下へ飛ばす距離（ユニット）
    [SerializeField] float minGroundNormalY = 0.7f; // 床とみなす面の法線Y下限（壁を接地扱いにしないため）

    [Header("Input")]
    [SerializeField] InputActionReference moveAction; // 左右移動に使うInput Action
    [SerializeField] InputActionReference jumpAction; // ジャンプに使うInput Action
    [SerializeField] InputActionReference diveAction; // 急降下ボタン。しゃがみと同じ Crouch を割り当てる（未設定でもスティックで発動できる）
    [SerializeField] InputActionReference dashAction; // 走行に使うInput Action

    Rigidbody2D playerRigidbody;
    GroundCheck groundCheck;
    Jump jump;
    BasicMove basicMove;
    Facing facing;
    Dash dash; // ダッシュの設定がなければ null
    Dive dive; // 急降下の設定がなければ null
    MoveInput input; // Update で更新し FixedUpdate で使う
    bool isGrounded; // このフレームの接地状態。FixedUpdate の先頭で1回だけ更新する

    public float FacingDirection => facing.Direction;
    public bool IsGrounded => isGrounded;

    // 歩行の最高速を超えているときだけ走りとする。壁に当たって止まっている間は走りにしない
    public bool IsRunning =>
        isGrounded && Mathf.Abs(playerRigidbody.linearVelocity.x) > moveSettings.MoveSpeed + RunSpeedTolerance;

    const float RunSpeedTolerance = 0.01f; // 歩行の最高速とみなす速度の誤差（ユニット/秒）
    const float RisingSpeedThreshold = 1f; // 上昇とみなす上向き速度の下限（ユニット/秒）

    void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody2D>();
        groundCheck = new GroundCheck(
            groundCheckTransform,
            groundCheckSize,
            groundCheckDistanceUnits,
            groundLayerMask,
            minGroundNormalY);
        jump = new Jump(playerRigidbody, jumpSettings);
        basicMove = new BasicMove(playerRigidbody, moveSettings);
        facing = new Facing(moveSettings);
        if (dashSettings != null) dash = new Dash(playerRigidbody, dashSettings, moveSettings);
        if (diveSettings != null) dive = new Dive(playerRigidbody, diveSettings);
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        if (diveAction != null) diveAction.action.Enable();
        if (dashAction != null) dashAction.action.Enable();
        jumpAction.action.performed += OnJump;
        jumpAction.action.canceled += OnJumpReleased;
    }

    void OnDisable()
    {
        jumpAction.action.performed -= OnJump;
        jumpAction.action.canceled -= OnJumpReleased;
        moveAction.action.Disable();
        jumpAction.action.Disable();
        if (diveAction != null) diveAction.action.Disable();
        if (dashAction != null) dashAction.action.Disable();
    }

    // 左右・ダッシュの入力を読み取る。急降下は、押した瞬間だけを先行入力として登録する
    void Update()
    {
        Vector2 moveValue = moveAction.action.ReadValue<Vector2>();
        bool isDashHeld = dashAction != null && dashAction.action.IsPressed();
        input = new MoveInput(moveValue.x, moveValue.y, isDashHeld);

        bool isDiveButtonPressed = diveAction != null && diveAction.action.WasPressedThisFrame();
        dive?.ReadInput(moveValue.y, isDiveButtonPressed);
    }

    // 順序に意味がある：接地判定 → ジャンプ → 急降下 → ダッシュの状態 → 向き → 重力 → 水平移動 → 落下速度の制限。
    // ジャンプ後の速度を水平移動が上書きしないよう、ジャンプを先に処理する
    void FixedUpdate()
    {
        UpdateGroundedState();
        if (jump.TryStart()) isGrounded = false;
        dive?.Step(isGrounded, jumpSettings.MaxFallSpeedUnits);
        dash?.UpdateState(input, isGrounded);
        UpdateFacing();
        jump.UpdateGravityScale(IsStandingStill());
        UpdateHorizontalMovement();
        jump.ClampFallSpeed(CurrentMaxFallSpeed());
    }

    void UpdateGroundedState()
    {
        // 上昇中は足元が床に触れていても接地扱いにしない（ジャンプ直後の二重発動を防ぐ）。
        // 床との接触解決で出る微小な上向き速度は、上昇とみなさない
        bool isRising = playerRigidbody.linearVelocity.y > RisingSpeedThreshold;
        isGrounded = !isRising && groundCheck.IsTouchingGround();

        if (isGrounded) jump.NotifyGrounded();
    }

    void UpdateFacing() =>
        facing.Step(input.Horizontal, dash != null && dash.IsBraking);

    // ダッシュが有効なときはダッシュが、そうでなければ歩きが水平速度を決める
    void UpdateHorizontalMovement()
    {
        if (IsDashActive()) dash.UpdateVelocity(input, isGrounded);
        else basicMove.UpdateVelocity(input, isGrounded);
    }

    bool IsDashActive() => dash != null && dash.IsActive;

    // 通常時は落下速度の上限、急降下中は急降下用の上限を返す
    float CurrentMaxFallSpeed() =>
        jumpSettings.MaxFallSpeedUnits * (dive != null ? dive.FallSpeedMultiplier : 1f);

    // 空中攻撃や一部の必殺技などから呼ぶ。急降下を解除して、通常の落下速度に戻す
    public void CancelDive() => dive?.Cancel();

    // 走行や減速の最中は止めない（減速はダッシュの処理で行う）
    bool IsStandingStill() =>
        !IsDashActive() && basicMove.IsStandingStill(isGrounded, input.Horizontal);

    void OnJump(InputAction.CallbackContext ctx) => jump.RequestJump();

    void OnJumpReleased(InputAction.CallbackContext ctx) => jump.ReleaseJump();

    // 外部（チェックポイントなど）から呼ぶ。移動前の速度や状態を引き継がないよう、すべて初期化する
    public void Respawn(Vector3 position)
    {
        transform.position = position;
        playerRigidbody.linearVelocity = Vector2.zero;
        dive?.Reset();
        dash?.Reset();
        facing.Reset();
    }
}
