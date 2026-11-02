using UnityEngine;

// 立ち上がったときに頭が入る領域に、天井（床と同じレイヤーのもの）があるかを調べる。
// GroundCheck と同じように、位置・大きさ・レイヤーは PlayerMover から渡す
public class CeilingCheck
{
    readonly Transform origin; // 調べる領域の中心
    readonly Vector2 boxSize; // 調べる領域の大きさ
    readonly LayerMask layerMask; // 天井とみなすレイヤー

    public CeilingCheck(Transform origin, Vector2 boxSize, LayerMask layerMask)
    {
        this.origin = origin;
        this.boxSize = boxSize;
        this.layerMask = layerMask;
    }

    // 領域の中に天井があれば、立ち上がれない
    public bool IsBlocked()
    {
        Collider2D ceiling = Physics2D.OverlapBox(origin.position, boxSize, 0f, layerMask);
        return ceiling != null;
    }
}
