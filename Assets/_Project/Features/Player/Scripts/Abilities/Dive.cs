using UnityEngine;

public class Dive
{
    readonly Rigidbody2D body;
    readonly DiveSettings settings;
    bool wasDownInput; // 前フレームに下入力していたか。下はじきの立ち上がりを検出するため
    int bufferFramesLeft; // 先行入力が残っているフレーム数。0なら受付中ではない

    public bool IsDiving { get; private set; } // 接地・攻撃などで解除される

    // 通常の落下速度の上限に掛ける倍率。急降下中でなければ1
    public float FallSpeedMultiplier => IsDiving ? settings.SpeedMultiplier : 1f;

    public Dive(Rigidbody2D body, DiveSettings settings)
    {
        this.body = body;
        this.settings = settings;
    }

    // 押した瞬間だけを先行入力として登録する（スティックは下はじき、ボタンは押下）
    public void ReadInput(float verticalInput, bool isButtonPressed)
    {
        bool isDownInput = verticalInput <= -settings.StickThreshold;
        if ((isDownInput && !wasDownInput) || isButtonPressed)
        {
            bufferFramesLeft = settings.BufferFrames;
        }
        wasDownInput = isDownInput;
    }

    // 空中で下はじきした直後の数フレーム以内に、下降中（Y速度0以下）なら急降下を開始する。
    // 開始時に落下速度を急降下の最大値まで一気に上げる
    public void Step(bool isGrounded, float normalMaxFallSpeed)
    {
        if (isGrounded) IsDiving = false;
        if (bufferFramesLeft <= 0) return;

        bufferFramesLeft--;
        bool canDive = !isGrounded && body.linearVelocity.y <= 0f;
        if (!canDive) return;

        IsDiving = true;
        bufferFramesLeft = 0;
        body.linearVelocity = new Vector2(
            body.linearVelocity.x,
            -normalMaxFallSpeed * settings.SpeedMultiplier);
    }

    // 急降下を解除して、通常の落下速度に戻す
    public void Cancel() => IsDiving = false;

    public void Reset()
    {
        IsDiving = false;
        bufferFramesLeft = 0;
    }
}
