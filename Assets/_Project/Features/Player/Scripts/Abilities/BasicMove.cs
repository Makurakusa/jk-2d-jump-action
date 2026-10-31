using UnityEngine;

public class BasicMove
{
    readonly Rigidbody2D body;
    readonly BasicMoveSettings settings;

    public BasicMove(Rigidbody2D body, BasicMoveSettings settings)
    {
        this.body = body;
        this.settings = settings;
    }

    // 接地していて、左右入力もしない。走行や減速の最中かは呼び出し側が見る
    public bool IsStandingStill(bool isGrounded, float horizontalInput) =>
        isGrounded && horizontalInput == 0f;

    // 力を加えず速度を直接書き換えるので、摩擦や壁との接触の影響を受けにくい
    public void UpdateVelocity(MoveInput input, bool isGrounded)
    {
        // 端での押し出しによる滑りを止める
        if (IsStandingStill(isGrounded, input.Horizontal))
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        // 空中で左右を離している間は、弱い減速で速度を残す。入力があれば空中の加速で操作する
        float acceleration;
        if (isGrounded) acceleration = settings.GroundAcceleration;
        else if (input.Horizontal == 0f) acceleration = settings.AirDeceleration;
        else acceleration = settings.AirAcceleration;

        // 最高速ダッシュの反転はブレーキ状態が担当する。それ以外の反転はここで加速度を上げて素早く切り返す
        bool isReversing = Mathf.Abs(input.Horizontal) >= settings.HoldThreshold
            && input.Horizontal * body.linearVelocity.x < 0f;
        if (isReversing) acceleration *= settings.ReverseAccelerationMultiplier;

        body.MoveVelocityXTowards(input.Horizontal * settings.MoveSpeed, acceleration);
    }
}
