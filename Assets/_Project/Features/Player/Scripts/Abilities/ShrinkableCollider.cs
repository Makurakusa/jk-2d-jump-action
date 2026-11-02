using UnityEngine;

// カプセルコライダーの高さを縮める・元に戻す。
// しゃがみとボディアタックは当たり判定を共有するので、1つのインスタンスを両方が使う
public class ShrinkableCollider
{
    readonly CapsuleCollider2D capsule;
    readonly Vector2 standingSize; // 立っているときの大きさ
    readonly Vector2 standingOffset; // 立っているときのオフセット
    readonly float shrunkHeight; // 縮めたときの高さ（ユニット）

    // 縮んでいるか。Shrink / Restore を重ねて呼んでも安全にするために持つ
    public bool IsShrunk { get; private set; }

    public ShrinkableCollider(CapsuleCollider2D capsule, float heightMultiplier)
    {
        this.capsule = capsule;
        standingSize = capsule.size;
        standingOffset = capsule.offset;
        shrunkHeight = standingSize.y * heightMultiplier;
    }

    // 高さ（y）だけを縮め、幅（x）は変えない。
    // 足元の位置を保つため、減った高さの半分だけ offset の y を下げる
    public void Shrink()
    {
        if (IsShrunk) return;

        float reducedHeight = standingSize.y - shrunkHeight;
        capsule.size = new Vector2(standingSize.x, shrunkHeight);
        capsule.offset = new Vector2(standingOffset.x, standingOffset.y - reducedHeight / 2f);
        IsShrunk = true;
    }

    // 立っているときの大きさに戻す
    public void Restore()
    {
        if (!IsShrunk) return;

        capsule.size = standingSize;
        capsule.offset = standingOffset;
        IsShrunk = false;
    }
}
