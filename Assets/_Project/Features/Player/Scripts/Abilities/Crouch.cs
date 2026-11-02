using UnityEngine;

// しゃがみの姿勢の状態
public enum CrouchState
{
    Standing, // 立っている
    Entering, // しゃがむ移行中（TransitionSeconds の間）。移動できず、コライダーはまだ立ちのまま
    Crouching, // しゃがんでいる（しゃがみ歩きを含む）。コライダーは小さい
    Exiting, // 立ち上がる解除中（TransitionSeconds の間）。移動できず、コライダーはまだ小さいまま
    Sliding, // しゃがみ滑り中。入ってから TransitionSeconds 後にコライダーが小さくなる
}

// UpdateState で起きた状態の変化。PlayerMover が、ダッシュとの受け渡しに使う
public enum CrouchTransition
{
    None, // 受け渡しが必要な変化はなかった
    StartedCrouching, // しゃがむ移行を始めた（ダッシュ状態を解除する）
    StartedSliding, // しゃがみ滑りに入った（ダッシュ状態を解除する）
    ReleasedSlideOnGround, // 地上でしゃがみ滑りを解除した（ダッシュに戻れるか試す）
    LeftSlideInAir, // しゃがみ滑りの途中で空中へ出た（空中でダッシュを再開できるか試す）
}

// しゃがみ・しゃがみ滑りを担当する。
// 流れ: 立ち → しゃがむ移行 → しゃがみ → 立ち上がる解除 → 立ち。ダッシュ中にしゃがむと、しゃがみ滑りに入る
public class Crouch
{
    const float StoppedSpeedTolerance = 0.01f; // 水平速度がこの値以下なら、止まったとみなす（ユニット/秒）

    readonly Rigidbody2D body;
    readonly CrouchSettings settings;
    readonly BasicMoveSettings moveSettings; // ブレーキ中にしゃがんだときの、滑りか通常のしゃがみかの境目に、歩きの最高速を使う
    readonly ShrinkableCollider bodyCollider; // ボディアタックと共有する当たり判定
    float transitionRemainingSeconds; // 移行・解除・滑りでコライダーを縮めるまでの、残り時間（秒）

    public CrouchState State { get; private set; } = CrouchState.Standing;
    public float SlideDirection { get; private set; } = 1f; // 滑っている向き（-1 / 1）

    public bool IsSliding => State == CrouchState.Sliding;
    public bool IsTransitioning => State == CrouchState.Entering || State == CrouchState.Exiting;
    public bool IsCrouched => State != CrouchState.Standing; // 移行中・解除中・滑りも含む

    public Crouch(
        Rigidbody2D body,
        CrouchSettings settings,
        BasicMoveSettings moveSettings,
        ShrinkableCollider bodyCollider)
    {
        this.body = body;
        this.settings = settings;
        this.moveSettings = moveSettings;
        this.bodyCollider = bodyCollider;
    }

    // 状態を進める。状態が変わるのはこのメソッドと ForceCrouching だけにする。
    // 起きた変化を返し、ダッシュとの受け渡しは呼び出し側が行う。
    // hasCeiling は、立ち上がれるかを決めるときだけ見る（地上の解除の途中では見ない）
    public CrouchTransition UpdateState(
        MoveInput input,
        bool isGrounded,
        bool isDashActive,
        bool isDashBraking,
        bool hasCeiling)
    {
        bool isHoldingDown = input.Vertical <= -settings.StickThreshold;
        if (!isGrounded) return UpdateInAir(isHoldingDown, hasCeiling);

        switch (State)
        {
            case CrouchState.Standing:
                return UpdateStanding(input, isHoldingDown, isDashActive, isDashBraking);

            case CrouchState.Entering:
                UpdateEntering(isHoldingDown);
                return CrouchTransition.None;

            case CrouchState.Crouching:
                UpdateCrouching(isHoldingDown, hasCeiling);
                return CrouchTransition.None;

            case CrouchState.Exiting:
                UpdateExiting();
                return CrouchTransition.None;

            case CrouchState.Sliding:
                return UpdateSliding(isHoldingDown, hasCeiling);

            default:
                return CrouchTransition.None;
        }
    }

    // 滑り中は摩擦で減速する。移行中・解除中は動かない（速度を0にする）。それ以外では何もしない
    public void UpdateVelocity()
    {
        switch (State)
        {
            case CrouchState.Sliding:
                body.MoveVelocityXTowards(0f, settings.SlideDeceleration);
                break;

            case CrouchState.Entering:
            case CrouchState.Exiting:
                body.linearVelocity = Vector2.zero;
                break;
        }
    }

    // ボディアタックが天井の下で着地したとき、立てないのでしゃがみにする。
    // コライダーはボディアタックが縮めたままなので、状態だけを合わせる
    public void ForceCrouching()
    {
        State = CrouchState.Crouching;
        bodyCollider.Shrink();
    }

    // リスポーンなどで、立ちの状態とコライダーに戻す
    public void Reset()
    {
        State = CrouchState.Standing;
        transitionRemainingSeconds = 0f;
        bodyCollider.Restore();
    }

    // 立っているとき: 下入力があれば、滑りに入るか、しゃがむ移行に入る
    CrouchTransition UpdateStanding(
        MoveInput input,
        bool isHoldingDown,
        bool isDashActive,
        bool isDashBraking)
    {
        if (!isHoldingDown) return CrouchTransition.None;

        if (CanSlide(isDashActive, isDashBraking))
        {
            StartSliding(input);
            return CrouchTransition.StartedSliding;
        }

        StartEntering();
        return CrouchTransition.StartedCrouching;
    }

    // ダッシュ中なら滑れる。ただしブレーキ中は、水平速度が歩きの最高速以下なら滑らず、通常のしゃがみにする。
    // ブレーキでは速度がすぐ落ちるので、ブレーキ中は基本的に通常のしゃがみになる
    bool CanSlide(bool isDashActive, bool isDashBraking)
    {
        if (!isDashActive) return false;

        bool isBrakingNotFasterThanWalk =
            isDashBraking && Mathf.Abs(body.linearVelocity.x) <= moveSettings.MoveSpeed;
        return !isBrakingNotFasterThanWalk;
    }

    // しゃがむ移行中: 下入力をやめたら立ちに戻る。時間が経ったらしゃがみになり、コライダーを縮める
    void UpdateEntering(bool isHoldingDown)
    {
        // 移行の途中でスティックを戻した。コライダーはまだ変えていないので、状態を戻すだけでよい
        if (!isHoldingDown)
        {
            State = CrouchState.Standing;
            return;
        }

        transitionRemainingSeconds -= Time.fixedDeltaTime;
        if (transitionRemainingSeconds > 0f) return;

        State = CrouchState.Crouching;
        bodyCollider.Shrink();
    }

    // しゃがんでいるとき: 下入力をやめて、頭上に天井がなければ、立ち上がる解除に入る。
    // 天井があれば立てないので、しゃがんだままにする
    void UpdateCrouching(bool isHoldingDown, bool hasCeiling)
    {
        bool canStandUp = !isHoldingDown && !hasCeiling;
        if (canStandUp) StartExiting();
    }

    // 立ち上がる解除中: 時間が経ったら立ちに戻り、コライダーを元に戻す
    void UpdateExiting()
    {
        transitionRemainingSeconds -= Time.fixedDeltaTime;
        if (transitionRemainingSeconds > 0f) return;

        State = CrouchState.Standing;
        bodyCollider.Restore();
    }

    // しゃがみ滑り中: 解除できるなら解除し、止まったらしゃがみにし、それ以外は滑りを続ける
    CrouchTransition UpdateSliding(bool isHoldingDown, bool hasCeiling)
    {
        bool canStandUp = !isHoldingDown && !hasCeiling;
        if (canStandUp)
        {
            StartExiting();
            return CrouchTransition.ReleasedSlideOnGround;
        }

        bool hasStopped = Mathf.Abs(body.linearVelocity.x) <= StoppedSpeedTolerance;
        if (hasStopped)
        {
            State = CrouchState.Crouching;
            bodyCollider.Shrink();
            return CrouchTransition.None;
        }

        ShrinkColliderAfterTransition();
        return CrouchTransition.None;
    }

    // 滑りに入ってから TransitionSeconds が経ったら、コライダーを縮める（滑り自体は入った時点から始まる）
    void ShrinkColliderAfterTransition()
    {
        transitionRemainingSeconds -= Time.fixedDeltaTime;
        if (transitionRemainingSeconds <= 0f) bodyCollider.Shrink();
    }

    // 空中では、しゃがみを始めず、解除だけできる。立てないときは、しゃがんだままにする
    CrouchTransition UpdateInAir(bool isHoldingDown, bool hasCeiling)
    {
        if (State == CrouchState.Standing) return CrouchTransition.None;

        // 滑りの途中で空中へ出たときは、下を押していてもダッシュを再開できるように知らせる
        bool wasSliding = IsSliding;
        bool canStandUp = !isHoldingDown && !hasCeiling;
        if (canStandUp)
        {
            State = CrouchState.Standing;
            bodyCollider.Restore();
        }
        else
        {
            State = CrouchState.Crouching;
            bodyCollider.Shrink();
        }
        return wasSliding ? CrouchTransition.LeftSlideInAir : CrouchTransition.None;
    }

    void StartEntering()
    {
        State = CrouchState.Entering;
        transitionRemainingSeconds = settings.TransitionSeconds;
    }

    void StartExiting()
    {
        State = CrouchState.Exiting;
        transitionRemainingSeconds = settings.TransitionSeconds;
    }

    // 滑りの向きは、今の水平速度の向きにする。ほぼ止まっているなら、左右入力の向きにする
    void StartSliding(MoveInput input)
    {
        State = CrouchState.Sliding;
        transitionRemainingSeconds = settings.TransitionSeconds;

        float velocityX = body.linearVelocity.x;
        bool isAlmostStopped = Mathf.Abs(velocityX) <= StoppedSpeedTolerance;
        SlideDirection = Mathf.Sign(isAlmostStopped ? input.Horizontal : velocityX);
    }
}

