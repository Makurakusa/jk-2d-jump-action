using UnityEngine;

public enum DashState
{
    Inactive, // ダッシュしていない
    Dash, // ダッシュボタン押下中の走行。接地中に入り、空中では維持する
    Brake, // 最高速からの反転時の減速。BrakeDuration の間続く
}

public class Dash
{
    const float SpeedTolerance = 0.01f; // 最高速とみなす速度の誤差（ユニット/秒）

    readonly Rigidbody2D body;
    readonly DashSettings dashSettings;
    readonly BasicMoveSettings moveSettings; // 空中の加減速と HoldThreshold を歩きと共有する
    float dashDirection; // ダッシュの向き（-1 / 1）
    float brakeRemainingSeconds; // ブレーキ状態の残り時間（秒）

    public DashState State { get; private set; } = DashState.Inactive;
    public bool IsActive => State != DashState.Inactive;
    public bool IsBraking => State == DashState.Brake;

    public Dash(Rigidbody2D body, DashSettings dashSettings, BasicMoveSettings moveSettings)
    {
        this.body = body;
        this.dashSettings = dashSettings;
        this.moveSettings = moveSettings;
    }

    // 状態を進める。状態が変わるのはこのメソッドだけにする
    public void UpdateState(MoveInput input, bool isGrounded)
    {
        bool isHoldingInput = Mathf.Abs(input.Horizontal) >= moveSettings.HoldThreshold;
        float inputDirection = Mathf.Sign(input.Horizontal);
        float currentX = body.linearVelocity.x;
        // 逆向きに走っている途中は、速度が0になるまでダッシュに入らない
        bool canStartDash = isGrounded && input.IsDashHeld && isHoldingInput
            && (Mathf.Approximately(currentX, 0f) || Mathf.Sign(currentX) == inputDirection);

        switch (State)
        {
            case DashState.Inactive:
                if (canStartDash) StartDash(inputDirection);
                break;

            case DashState.Dash:
                if (!input.IsDashHeld)
                {
                    State = DashState.Inactive;
                }
                else if (isHoldingInput && inputDirection != dashDirection)
                {
                    // 最高速からの反転だけブレーキを挿む。それ以外は歩きの反転として扱う
                    bool isAtMaxSpeed = Mathf.Abs(currentX) >= dashSettings.MaxDashSpeed - SpeedTolerance;
                    if (isAtMaxSpeed) StartBrake();
                    else State = DashState.Inactive;
                }
                else if (!isHoldingInput && isGrounded)
                {
                    State = DashState.Inactive;
                }
                break;

            case DashState.Brake:
                brakeRemainingSeconds -= Time.fixedDeltaTime;
                if (brakeRemainingSeconds <= 0f)
                {
                    if (input.IsDashHeld && isHoldingInput) StartDash(inputDirection);
                    else State = DashState.Inactive;
                }
                break;
        }
    }

    // ダッシュ中は最高速へ近づける。空中では加速を弱め、入力を離した空中では弱い減速で速度を残す。
    // ブレーキ中は速度を0へ近づける
    public void UpdateVelocity(MoveInput input, bool isGrounded)
    {
        if (State == DashState.Brake)
        {
            body.MoveVelocityXTowards(0f, dashSettings.BrakeAcceleration);
            return;
        }

        if (Mathf.Abs(input.Horizontal) < moveSettings.HoldThreshold)
        {
            body.MoveVelocityXTowards(0f, moveSettings.AirDeceleration);
            return;
        }

        float acceleration = isGrounded ? dashSettings.DashAcceleration : moveSettings.AirAcceleration;
        body.MoveVelocityXTowards(dashDirection * dashSettings.MaxDashSpeed, acceleration);
    }

    public void Reset() => State = DashState.Inactive;

    void StartDash(float direction)
    {
        State = DashState.Dash;
        dashDirection = direction;
    }

    void StartBrake()
    {
        State = DashState.Brake;
        brakeRemainingSeconds = dashSettings.BrakeDuration;
    }
}
