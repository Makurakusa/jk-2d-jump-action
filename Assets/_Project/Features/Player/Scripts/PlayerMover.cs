using UnityEngine;
using UnityEngine.InputSystem;

// プレイヤーの左右移動・ジャンプ・急降下・しゃがみ・ボディアタックを組み立てる。
// 移動は力ではなく速度を直接書き換えるため、摩擦に依存しない（壁に当たっても減速しにくい）。
// 処理はすべて FixedUpdate で行い、Update では入力の読み取りだけを行う。
// しゃがみとボディアタックは、設定を必ず割り当てる前提にする（ダッシュ・急降下だけが省略できる）
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class PlayerMover : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] BasicMoveSettings moveSettings; // 歩きのパラメータ
    [SerializeField] DashSettings dashSettings; // ダッシュ・ブレーキのパラメータ
    [SerializeField] JumpSettings jumpSettings; // ジャンプ・落下のパラメータ
    [SerializeField] DiveSettings diveSettings; // 急降下のパラメータ
    [SerializeField] CrouchSettings crouchSettings; // しゃがみ・しゃがみ滑りのパラメータ
    [SerializeField] BodyAttackSettings bodyAttackSettings; // ボディアタックのパラメータ

    [Header("Ground Check")]
    [SerializeField] LayerMask groundLayerMask; // 接地判定と天井判定の対象にするレイヤー
    [SerializeField] Transform groundCheckTransform; // 接地判定を行う位置
    [SerializeField] Vector2 groundCheckSize = new Vector2(0.8f, 0.05f); // 接地判定の箱の大きさ（プレイヤー幅より少し狭くする）
    [SerializeField] float groundCheckDistanceUnits = 0.05f; // 箱を下へ飛ばす距離（ユニット）
    [SerializeField] float minGroundNormalY = 0.7f; // 床とみなす面の法線Y下限（壁を接地扱いにしないため）
    [SerializeField] float groundCheckLiftUnits = 0.5f; // 箱の始点を足元より上へ上げる量（ユニット）。斜面の角が床に埋まらない高さ

    [Header("Ceiling Check")]
    [SerializeField] Transform ceilingCheckTransform; // 天井判定の領域の中心。しゃがんだコライダーの真上（立ち上がったときに頭が入る場所）に置く
    [SerializeField] Vector2 ceilingCheckSize = new Vector2(0.9f, 1.8f); // 天井判定の領域の大きさ。プレイヤー幅より少し狭くする

    [Header("Input")]
    [SerializeField] InputActionReference moveAction; // 左右移動としゃがみ（スティック下）に使うInput Action
    [SerializeField] InputActionReference jumpAction; // ジャンプに使うInput Action
    [SerializeField] InputActionReference diveAction; // 急降下ボタン。未設定でよい（下はじきで発動できる）
    [SerializeField] InputActionReference dashAction; // 走行に使うInput Action
    [SerializeField] InputActionReference bodyAttackAction; // ボディアタックに使うInput Action（buttonEast）

    Rigidbody2D playerRigidbody;
    ShrinkableCollider bodyCollider; // しゃがみとボディアタックが共有する当たり判定
    GroundCheck groundCheck;
    CeilingCheck ceilingCheck;
    WallCheck wallCheck;
    Jump jump;
    BasicMove basicMove;
    Facing facing;
    Crouch crouch;
    BodyAttack bodyAttack;
    Dash dash; // ダッシュの設定がなければ null
    Dive dive; // 急降下の設定がなければ null
    MoveInput input; // Update で更新し FixedUpdate で使う
    bool isGrounded; // このフレームの接地状態。FixedUpdate の先頭で1回だけ更新する
    Vector2 groundNormal = Vector2.up; // 接地中の床の法線（坂の向き）
    const int MaxContactCount = 8; // 接触点を調べる上限。毎フレームの確保を避けるため配列を使い回す
    readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[MaxContactCount];

    public float FacingDirection => facing.Direction;
    public bool IsGrounded => isGrounded;
    public bool IsCrouched => crouch.IsCrouched; // しゃがみ・歩き・滑り・移行中・解除中を含む

    // 歩行の最高速を超えているときだけ走りとする。壁に当たって止まっている間は走りにしない
    public bool IsRunning =>
        isGrounded && Mathf.Abs(playerRigidbody.linearVelocity.x) > moveSettings.MoveSpeed + RunSpeedTolerance;

    const float RunSpeedTolerance = 0.01f; // 歩行の最高速とみなす速度の誤差（ユニット/秒）
    const float MinWallNormalAbsX = 0.9f; // 壁とみなす面の法線 x の絶対値の下限

    void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody2D>();

        bodyCollider = new ShrinkableCollider(
            GetComponent<CapsuleCollider2D>(),
            crouchSettings.CrouchColliderHeightMultiplier);
        groundCheck = new GroundCheck(
            groundCheckTransform,
            groundCheckSize,
            groundCheckDistanceUnits,
            groundLayerMask,
            minGroundNormalY,
            groundCheckLiftUnits);
        ceilingCheck = new CeilingCheck(ceilingCheckTransform, ceilingCheckSize, groundLayerMask);
        wallCheck = new WallCheck(playerRigidbody, MinWallNormalAbsX);
        jump = new Jump(playerRigidbody, jumpSettings);
        basicMove = new BasicMove(playerRigidbody, moveSettings);
        facing = new Facing(moveSettings);
        crouch = new Crouch(playerRigidbody, crouchSettings, moveSettings, bodyCollider);
        bodyAttack = new BodyAttack(playerRigidbody, bodyAttackSettings, moveSettings, bodyCollider);
        if (dashSettings != null) dash = new Dash(playerRigidbody, dashSettings, moveSettings);
        if (diveSettings != null) dive = new Dive(playerRigidbody, diveSettings);
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        bodyAttackAction.action.Enable();
        if (diveAction != null) diveAction.action.Enable();
        if (dashAction != null) dashAction.action.Enable();
        jumpAction.action.performed += OnJump;
        jumpAction.action.canceled += OnJumpReleased;
        bodyAttackAction.action.performed += OnBodyAttack;
    }

    void OnDisable()
    {
        jumpAction.action.performed -= OnJump;
        jumpAction.action.canceled -= OnJumpReleased;
        bodyAttackAction.action.performed -= OnBodyAttack;
        moveAction.action.Disable();
        jumpAction.action.Disable();
        bodyAttackAction.action.Disable();
        if (diveAction != null) diveAction.action.Disable();
        if (dashAction != null) dashAction.action.Disable();
    }

    // 左右・上下・ダッシュの入力を読み取る。急降下は、押した瞬間だけを先行入力として登録する
    void Update()
    {
        Vector2 moveValue = moveAction.action.ReadValue<Vector2>();
        bool isDashHeld = dashAction != null && dashAction.action.IsPressed();
        input = new MoveInput(moveValue.x, moveValue.y, isDashHeld);

        bool isDiveButtonPressed = diveAction != null && diveAction.action.WasPressedThisFrame();
        dive?.ReadInput(moveValue.y, isDiveButtonPressed);
    }

    // 順序に意味がある：接地判定 → ジャンプ → しゃがみ → ボディアタックの開始 → 急降下 → ダッシュの状態
    // → 向き → 重力 → 水平移動 → 坂の速度 → ボディアタックの終了 → 落下速度の制限。
    // ジャンプ後の速度を水平移動が上書きしないよう、ジャンプを先に処理する。
    // しゃがみを先に更新するのは、空中での姿勢（しゃがんだままか解除か）を、ボディアタックの判定より前に決めるため
    void FixedUpdate()
    {
        UpdateGroundedState();
        if (jump.TryStart()) isGrounded = false;
        UpdateCrouchState();
        UpdateBodyAttackStart();
        UpdateDiveState();
        UpdateDashState();
        UpdateFacing();
        jump.UpdateGravityScale(IsStandingStill());
        UpdateHorizontalMovement();
        UpdateSlopeVelocity();
        UpdateBodyAttackEnd();
        jump.ClampFallSpeed(CurrentMaxFallSpeed());
    }

    void UpdateGroundedState()
    {
        jump.UpdateAscentState();
        // ジャンプで上昇中は、足元が床に触れていても接地扱いにしない（ジャンプ直後の二重発動を防ぐ）
        float horizontalStepUnits = playerRigidbody.linearVelocity.x * Time.fixedDeltaTime;
        bool isTouchingGround = groundCheck.TryGetGround(out groundNormal, out float feetGapUnits, horizontalStepUnits);

        // 実際に接している床のうち最も急な面を使う。緩い向きを使うと、頂点や折れ目で止まるため
        bool isTouchingContact = false;
        int contactCount = playerRigidbody.GetContacts(contactBuffer);
        for (int contactIndex = 0; contactIndex < contactCount; contactIndex++)
        {
            Vector2 contactNormal = contactBuffer[contactIndex].normal;
            if (contactNormal.y < minGroundNormalY) continue;

            isTouchingContact = true;
            if (isTouchingGround && contactNormal.y >= groundNormal.y) continue;
            groundNormal = contactNormal;
            isTouchingGround = true;
        }

        isGrounded = !jump.IsRisingFromJump && isTouchingGround;

        if (isGrounded) jump.NotifyGrounded();
        // 接触中の隙間は丸い下端の見かけなので、動かさない（動かすと毎回めり込む）
        if (isGrounded && feetGapUnits > 0f && !isTouchingContact) playerRigidbody.position += Vector2.down * feetGapUnits;
    }

    // しゃがみの状態を進め、ダッシュとの受け渡しが必要な変化を処理する。
    // 天井の判定は、しゃがんでいる間だけ行う（立っているときは使わないので、判定を省く）
    void UpdateCrouchState()
    {
        if (bodyAttack.IsActive) return;

        bool hasCeiling = crouch.IsCrouched && ceilingCheck.IsBlocked();
        bool isDashBraking = dash != null && dash.IsBraking;
        CrouchTransition transition = crouch.UpdateState(input, isGrounded, IsDashActive(), isDashBraking, hasCeiling);

        switch (transition)
        {
            case CrouchTransition.StartedCrouching:
            case CrouchTransition.StartedSliding:
                // しゃがむ前のダッシュ・ブレーキを引き継がない
                dash?.Reset();
                break;

            case CrouchTransition.ReleasedSlideOnGround:
                // 滑りを解除したら、ダッシュの基準速度からダッシュに戻れるか試す
                dash?.TryStartFromSlide(input, isGrounded, crouchSettings.DashEntrySpeed);
                break;

            case CrouchTransition.LeftSlideInAir:
                // 滑りの途中でジャンプなどで空中へ出たら、現在の速度のままダッシュを再開できるか試す
                dash?.TryResumeInAir(input, crouch.SlideDirection);
                break;
        }
    }

    // ボディアタックを発動できるなら発動し、発動前の急降下・ダッシュを引き継がないようにする
    void UpdateBodyAttackStart()
    {
        // しゃがんでいる間（空中でしゃがんだままのジャンプを含む）は発動しない
        if (crouch.IsCrouched) return;

        // 発動時に引き継ぐ水平速度の上限。ダッシュがないゲームでは、基準値より速くしない
        float maxAttackSpeed = dashSettings != null ? dashSettings.MaxDashSpeed : bodyAttackSettings.Speed;

        bool hasStarted = bodyAttack.TryStart(input, facing.Direction, isGrounded, maxAttackSpeed);
        if (!hasStarted) return;

        dive?.Reset();
        dash?.Reset();
        facing.Set(bodyAttack.Direction);
    }

    // ボディアタック中は、急降下に入らない
    void UpdateDiveState()
    {
        if (bodyAttack.IsActive) return;

        dive?.Step(isGrounded, jumpSettings.MaxFallSpeedUnits);
    }

    // しゃがんでいる間とボディアタック中は、ダッシュに新しく入らない
    void UpdateDashState()
    {
        // しゃがみダッシュジャンプで着地してしゃがみが続くときは、ダッシュを引き継がない
        if (isGrounded && crouch.State == CrouchState.Crouching) dash?.Reset();

        bool isStartAllowed = !crouch.IsCrouched && !bodyAttack.IsActive;
        dash?.UpdateState(input, isGrounded, isStartAllowed);
    }

    // ブレーキ・しゃがみ滑り・ボディアタックの間は、向きを変えない
    void UpdateFacing()
    {
        bool isBraking = dash != null && dash.IsBraking;
        bool isFacingLocked = isBraking || crouch.IsSliding || bodyAttack.IsActive;
        facing.Step(input.Horizontal, isFacingLocked);
    }

    // 優先順位は実装書 1.4。上の条件ほど優先して、水平速度を決める
    void UpdateHorizontalMovement()
    {
        if (bodyAttack.IsActive)
        {
            bodyAttack.UpdateVelocity();
        }
        else if (crouch.IsSliding)
        {
            crouch.UpdateVelocity();
        }
        else if (IsDashActive())
        {
            dash.UpdateVelocity(input, isGrounded);
        }
        else if (crouch.IsTransitioning)
        {
            crouch.UpdateVelocity();
        }
        else
        {
            // しゃがみ歩きの減速は接地中だけ。空中ではしゃがんでいても通常の空中操作にする
            float maxSpeedMultiplier = isGrounded && crouch.IsCrouched ? crouchSettings.WalkSpeedMultiplier : 1f;
            basicMove.UpdateVelocity(input, isGrounded, maxSpeedMultiplier);
        }
    }

    // 接地中は水平速度を床の接線に沿わせ、坂を登り下りできるようにする
    void UpdateSlopeVelocity()
    {
        if (!isGrounded || bodyAttack.IsActive) return;

        // 法線のYは床の下限以上なので、接線のXは0にならない
        Vector2 tangent = new Vector2(groundNormal.y, -groundNormal.x);
        float slopeY = tangent.y / tangent.x;
        Vector2 velocity = playerRigidbody.linearVelocity;
        playerRigidbody.linearVelocity = new Vector2(velocity.x, velocity.x * slopeY);
    }

    // 着地・壁でボディアタックが終わったかを見る。壁と天井の判定は、発動中だけ行う
    void UpdateBodyAttackEnd()
    {
        bool isTouchingWallAhead = bodyAttack.IsActive && wallCheck.IsTouchingWallAhead(bodyAttack.Direction);
        bool hasCeiling = bodyAttack.IsActive && ceilingCheck.IsBlocked();

        BodyAttackEnd attackEnd = bodyAttack.Step(isGrounded, isTouchingWallAhead, hasCeiling);
        if (attackEnd == BodyAttackEnd.EndedUnderCeiling) crouch.ForceCrouching();
    }

    bool IsDashActive() => dash != null && dash.IsActive;

    // 通常時は落下速度の上限、急降下中は急降下用の上限を返す
    float CurrentMaxFallSpeed() =>
        jumpSettings.MaxFallSpeedUnits * (dive != null ? dive.FallSpeedMultiplier : 1f);

    // 空中攻撃や一部の必殺技などから呼ぶ。急降下を解除して、通常の落下速度に戻す
    public void CancelDive() => dive?.Cancel();

    // 動かずに止まっているか。重力を切って、足場の端で滑り落ちないようにするために使う。
    // ダッシュ・滑り・ボディアタックの最中は、動いているので止まっているとはいえない。
    // しゃがむ移行中・解除中は動かないので、接地していれば止まっているとする
    bool IsStandingStill()
    {
        if (bodyAttack.IsActive || IsDashActive() || crouch.IsSliding) return false;
        if (crouch.IsTransitioning) return isGrounded;

        return basicMove.IsStandingStill(isGrounded, input.Horizontal);
    }

    void OnJump(InputAction.CallbackContext ctx) => jump.RequestJump();

    void OnJumpReleased(InputAction.CallbackContext ctx) => jump.ReleaseJump();

    void OnBodyAttack(InputAction.CallbackContext ctx) => bodyAttack.RequestAttack();

    // 外部（チェックポイントなど）から呼ぶ。移動前の速度や状態を引き継がないよう、すべて初期化する
    public void Respawn(Vector3 position)
    {
        transform.position = position;
        playerRigidbody.linearVelocity = Vector2.zero;
        dive?.Reset();
        dash?.Reset();
        crouch.Reset();
        bodyAttack.Reset();
        facing.Reset();
    }
}
