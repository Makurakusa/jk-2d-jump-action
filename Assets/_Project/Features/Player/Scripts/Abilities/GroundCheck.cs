using UnityEngine;

public class GroundCheck
{
    readonly Transform origin; // 接地判定を行う位置
    readonly Vector2 boxSize; // 箱の大きさ（プレイヤー幅より少し狭くする）
    readonly float distanceUnits; // 箱を下へ飛ばす距離（ユニット）
    readonly LayerMask layerMask; // 接地判定の対象にするレイヤー
    readonly float minNormalY; // 床とみなす面の法緝Y下限（壁を接地扱いにしないため）

    public GroundCheck(
        Transform origin,
        Vector2 boxSize,
        float distanceUnits,
        LayerMask layerMask,
        float minNormalY)
    {
        this.origin = origin;
        this.boxSize = boxSize;
        this.distanceUnits = distanceUnits;
        this.layerMask = layerMask;
        this.minNormalY = minNormalY;
    }

    // 足元から箱を短く下に飛ばして床を探す。
    // 面の向きも見るので、壁（法線がほぼ水平）は接地扱いにならない
    public bool IsTouchingGround()
    {
        RaycastHit2D hit = Physics2D.BoxCast(
            origin.position,
            boxSize,
            0f,
            Vector2.down,
            distanceUnits,
            layerMask);

        return hit.collider != null && hit.normal.y >= minNormalY;
    }
}
