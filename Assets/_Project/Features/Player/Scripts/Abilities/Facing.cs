using UnityEngine;

public class Facing
{
    readonly BasicMoveSettings settings;

    public float Direction { get; private set; } = 1f; // -1 / 1。入力を離しても維持する

    public Facing(BasicMoveSettings settings)
    {
        this.settings = settings;
    }

    // 左右を倒している間だけ向きを更新する。ブレーキ中は逆入力でも、滑り終わるまで元の向きを保つ
    public void Step(float horizontalInput, bool isBraking)
    {
        if (isBraking) return;
        if (Mathf.Abs(horizontalInput) < settings.HoldThreshold) return;

        Direction = Mathf.Sign(horizontalInput);
    }

    public void Reset() => Direction = 1f;
}
