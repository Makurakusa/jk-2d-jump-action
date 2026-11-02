using UnityEngine;

// ボディアタックの終わり方。PlayerMover が、しゃがみとの受け渡しに使う
public enum BodyAttackEnd
{
    NotEnded, // 終わっていない（または発動していない）
    Ended, // 終わった。コライダーは元に戻した
    EndedUnderCeiling, // 天井の下に着地して終わった。立てないので、コライダーは小さいまま残す
}

// ボディアタックを担当する。空中で前方へ、やや上に大きく飛び込む。
// 放物線（初速 + 重力）で飛び、着地か壁で終わる。重力は PlayerMover 側（Jump）が通常どおり掛ける
public class BodyAttack
{
    // 「まだ一度も押されていない」を表す。経過時間が無限大になり、受付時間内と判定されない
    const float NoTime = float.NegativeInfinity;

    readonly Rigidbody2D body;
    readonly BodyAttackSettings settings;
    readonly BasicMoveSettings moveSettings; // 左右入力を「倒している」とみなす HoldThreshold を使う
    readonly ShrinkableCollider bodyCollider; // しゃがみと共有する当たり判定
    float lastAttackPressedTime = NoTime; // 最後にボタンを押した時刻（先行入力用）
    bool hasUsedInThisAirtime; // 今の滞空で、すでに発動したか。着地で false に戻る
    float colliderShrinkRemainingSeconds; // コライダーを縮めるまでの残り時間（秒）
    float horizontalSpeed; // 発動中に保つ水平速度の大きさ（ユニット/秒。0以上）

    public bool IsActive { get; private set; }
    public float Direction { get; private set; } = 1f; // 飛ぶ向き（-1 / 1）

    public BodyAttack(
        Rigidbody2D body,
        BodyAttackSettings settings,
        BasicMoveSettings moveSettings,
        ShrinkableCollider bodyCollider)
    {
        this.body = body;
        this.settings = settings;
        this.moveSettings = moveSettings;
        this.bodyCollider = bodyCollider;
    }

    // 押した時刻を記録するだけにする。実際の発動は TryStart で、
    // 空中かどうかと合わせて決める（先行入力のため）
    public void RequestAttack() => lastAttackPressedTime = Time.time;

    // 発動できる条件がそろっていれば発動する。発動したかを返す。
    // 条件: 押した直後（BufferSeconds 以内）、発動中でない、空中、今の滞空でまだ使っていない。
    // maxHorizontalSpeed は、引き継ぐ速度の上限（ダッシュの最高速）
    public bool TryStart(MoveInput input, float facingDirection, bool isGrounded, float maxHorizontalSpeed)
    {
        bool wasPressedRecently = Time.time - lastAttackPressedTime <= settings.BufferSeconds;
        bool canStart = wasPressedRecently && !IsActive && !isGrounded && !hasUsedInThisAirtime;
        if (!canStart) return false;

        // 向きを先に決める。引き継ぐ速度は、この向きに対する速度で比べるため
        Direction = DecideDirection(input, facingDirection);
        horizontalSpeed = DecideHorizontalSpeed(maxHorizontalSpeed);
        body.linearVelocity = new Vector2(Direction * horizontalSpeed, settings.UpwardSpeed);

        IsActive = true;
        hasUsedInThisAirtime = true;
        lastAttackPressedTime = NoTime; // 同じ押下で2回発動しない
        colliderShrinkRemainingSeconds = settings.TransitionSeconds;
        return true;
    }

    // 発動中は、水平速度を一定に保つ（左右入力や空中の減速で変えない）。垂直方向は重力に任せる
    public void UpdateVelocity()
    {
        body.linearVelocity = new Vector2(Direction * horizontalSpeed, body.linearVelocity.y);
    }

    // 毎フレームの終わりに呼ぶ。発動中なら終了条件を見て、発動中でなければ着地で回復する。
    // isTouchingWallAhead は発動中だけ意味を持つ。hasCeiling は着地で終わるときだけ使う
    public BodyAttackEnd Step(bool isGrounded, bool isTouchingWallAhead, bool hasCeiling)
    {
        if (!IsActive)
        {
            RecoverIfGrounded(isGrounded);
            return BodyAttackEnd.NotEnded;
        }

        if (isTouchingWallAhead) return EndByWall();
        if (isGrounded) return EndByLanding(hasCeiling);

        ShrinkColliderAfterTransition();
        return BodyAttackEnd.NotEnded;
    }

    // リスポーンなどで、発動前の状態に戻す
    public void Reset()
    {
        IsActive = false;
        hasUsedInThisAirtime = false;
        lastAttackPressedTime = NoTime;
        bodyCollider.Restore();
    }

    // 左右を倒していればその方向、倒していなければキャラの向き
    float DecideDirection(MoveInput input, float facingDirection)
    {
        bool isHoldingHorizontal = Mathf.Abs(input.Horizontal) >= moveSettings.HoldThreshold;
        return isHoldingHorizontal ? Mathf.Sign(input.Horizontal) : facingDirection;
    }

    // 基準値（Speed）と、発動時点の速度（同じ向きのもの）のうち、速いほうにする。
    // ただし上限（ダッシュの最高速）は超えない。逆向きの速度は、負の値になるので基準値が選ばれる
    float DecideHorizontalSpeed(float maxHorizontalSpeed)
    {
        float inheritedSpeed = body.linearVelocity.x * Direction;
        float baseOrInheritedSpeed = Mathf.Max(settings.Speed, inheritedSpeed);
        return Mathf.Min(baseOrInheritedSpeed, maxHorizontalSpeed);
    }

    // 発動してから TransitionSeconds が経ったら、コライダーを縮める
    void ShrinkColliderAfterTransition()
    {
        colliderShrinkRemainingSeconds -= Time.fixedDeltaTime;
        if (colliderShrinkRemainingSeconds <= 0f) bodyCollider.Shrink();
    }

    // 壁に当たったら終わる。水平速度を0にして、壁に押し付け続けないようにする
    BodyAttackEnd EndByWall()
    {
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        IsActive = false;
        bodyCollider.Restore();
        return BodyAttackEnd.Ended;
    }

    // 着地したら終わる。頭上に天井があって立てないなら、コライダーを縮めたまま残して知らせる
    BodyAttackEnd EndByLanding(bool hasCeiling)
    {
        IsActive = false;
        if (hasCeiling) return BodyAttackEnd.EndedUnderCeiling;

        bodyCollider.Restore();
        return BodyAttackEnd.Ended;
    }

    // 着地したら、次の滞空でまた発動できるようにする
    void RecoverIfGrounded(bool isGrounded)
    {
        if (isGrounded) hasUsedInThisAirtime = false;
    }
}
