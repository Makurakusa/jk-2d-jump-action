using UnityEngine;

public class Jump
{
    // 「まだ一度も起きていない」を表す。経過時間が無限大になり、猶予時間内と判定されない
    const float NoTime = float.NegativeInfinity;

    readonly Rigidbody2D body;
    readonly JumpSettings settings;
    readonly float jumpInitialVelocity; // ジャンプ開始時に与える上向き速度（ユニット/秒）
    readonly float baseGravityScale; // 上昇中の重力スケール。落下時はこれに倍率を掛ける
    float lastJumpPressedTime = NoTime; // 最後にジャンプボタンを押した時刻（先行入力用）
    float lastGroundedTime = NoTime; // 最後に接地していた時刻（コヨーテタイム用）

    // 「高さ」と「頂点までの時間」から、必要な重力と初速を逆算する。
    // 公式: height = 1/2 * g * t^2, initialVelocity = g * t
    // プロジェクトの重力設定を変えても同じジャンプになるよう、重力はスケールで合わせる
    public Jump(Rigidbody2D body, JumpSettings settings)
    {
        this.body = body;
        this.settings = settings;

        float requiredGravityAcceleration =
            2f * settings.JumpHeightUnits / (settings.TimeToApexSeconds * settings.TimeToApexSeconds);
        jumpInitialVelocity = requiredGravityAcceleration * settings.TimeToApexSeconds;
        baseGravityScale = requiredGravityAcceleration / Mathf.Abs(Physics2D.gravity.y);
        body.gravityScale = baseGravityScale;
    }

    // 押した時刻を記録するだけにする。実際のジャンプは TryStart で、
    // 接地判定と合わせて決める（先行入力のため）
    public void RequestJump() => lastJumpPressedTime = Time.time;

    public void NotifyGrounded() => lastGroundedTime = Time.time;

    // 先行入力（着地前の押下）とコヨーテタイム（離地後の猶予）の両方を満たすときだけジャンプする。
    // ジャンプしたかを返す
    public bool TryStart()
    {
        bool jumpWasPressedRecently = Time.time - lastJumpPressedTime <= settings.JumpBufferSeconds;
        bool canStillJump = Time.time - lastGroundedTime <= settings.CoyoteSeconds;
        if (!jumpWasPressedRecently || !canStillJump) return false;

        body.linearVelocity = new Vector2(body.linearVelocity.x, jumpInitialVelocity);

        // 同じ入力・同じ接地で2回ジャンプしないよう消費する
        lastJumpPressedTime = NoTime;
        lastGroundedTime = NoTime;
        return true;
    }

    // 上昇中にボタンを離したら上昇を弱める（短押しで低いジャンプになる）
    public void ReleaseJump()
    {
        Vector2 currentVelocity = body.linearVelocity;
        if (currentVelocity.y <= 0f) return;

        body.linearVelocity = new Vector2(
            currentVelocity.x,
            currentVelocity.y * settings.JumpReleaseVelocityMultiplier);
    }

    // 停止中は重力を切る。丸いコライダーの底が足場の端で重力を横向きの力に変え、
    // 滑り落ちるのを防ぐため
    public void UpdateGravityScale(bool isStandingStill)
    {
        if (isStandingStill)
        {
            body.gravityScale = 0f;
            return;
        }

        // 落下を速くして、ジャンプに重みを出す
        body.gravityScale = body.linearVelocity.y < 0f
            ? baseGravityScale * settings.FallGravityMultiplier
            : baseGravityScale;
    }

    // 重力で加速し続けないよう、下向き速度に上限をかける。
    // 水平移動が速度を書き換えた後に行い、最終的な速度を制限する
    public void ClampFallSpeed(float maxFallSpeed)
    {
        Vector2 currentVelocity = body.linearVelocity;
        if (currentVelocity.y >= -maxFallSpeed) return;

        body.linearVelocity = new Vector2(currentVelocity.x, -maxFallSpeed);
    }
}
