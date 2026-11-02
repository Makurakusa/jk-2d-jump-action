using UnityEngine;

public class Facing
{
    readonly BasicMoveSettings settings;

    public float Direction { get; private set; } = 1f; // -1 / 1。入力を離しても維持する

    public Facing(BasicMoveSettings settings)
    {
        this.settings = settings;
    }

    // 左右を倒している間だけ向きを更新する。
    // isLocked が true の間（ブレーキ・しゃがみ滑り・ボディアタック）は、逆入力でも元の向きを保つ
    public void Step(float horizontalInput, bool isLocked)
    {
        if (isLocked) return;
        if (Mathf.Abs(horizontalInput) < settings.HoldThreshold) return;

        Direction = Mathf.Sign(horizontalInput);
    }

    // 入力とは関係なく、向きを決める（ボディアタックの発動時に使う）
    public void Set(float direction) => Direction = direction;

    public void Reset() => Direction = 1f;
}
