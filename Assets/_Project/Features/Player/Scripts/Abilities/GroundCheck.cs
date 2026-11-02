using UnityEngine;

public class GroundCheck
{
    const int RayCount = 3; // 足元の幅に沿って飛ばす線の本数

    readonly Transform origin; // 接地判定を行う位置
    readonly Vector2 boxSize; // 箱の大きさ（プレイヤー幅より少し狭くする）
    readonly float distanceUnits; // 箱を下へ飛ばす距離（ユニット）
    readonly LayerMask layerMask; // 接地判定の対象にするレイヤー
    readonly float minNormalY; // 床とみなす面の法緝Y下限（壁を接地扱いにしないため）
    readonly float liftUnits; // 箱の始点を足元より上へ上げる量（ユニット）

    public GroundCheck(
        Transform origin,
        Vector2 boxSize,
        float distanceUnits,
        LayerMask layerMask,
        float minNormalY,
        float liftUnits)
    {
        this.origin = origin;
        this.boxSize = boxSize;
        this.distanceUnits = distanceUnits;
        this.layerMask = layerMask;
        this.minNormalY = minNormalY;
        this.liftUnits = liftUnits;
    }

    // 足元の左・中央・右から短く下へ飛ばして床を探す。床なら true と面の法線と足元から床までの距離を返す。
    // 面の向きも見るので、壁（法線がほぼ水平）は接地扱いにならない
    public bool TryGetGround(out Vector2 groundNormal, out float feetGapUnits, float horizontalStepUnits)
    {
        // 斜面の角が床に埋まった状態で始まらないよう、足元より上から飛ばす
        Vector2 rayOrigin = (Vector2)origin.position + Vector2.up * liftUnits;
        float halfWidth = boxSize.x * 0.5f;
        float stepUnits = Mathf.Abs(horizontalStepUnits);
        // 1フレームで進む分まで届かせ、速い移動で斜面を見失わないようにする
        float rayDistance = liftUnits + distanceUnits + stepUnits;

        groundNormal = Vector2.up;
        feetGapUnits = 0f;
        bool isGround = false;
        float bestNormalY = 0f;

        for (int i = 0; i < RayCount; i++)
        {
            float offsetX = Mathf.Lerp(-halfWidth, halfWidth, i / (float)(RayCount - 1));
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin + Vector2.right * offsetX, Vector2.down, rayDistance, layerMask);
            if (hit.collider == null || hit.normal.y < minNormalY) continue;

            // 床までの距離は中央の線で測る。左右の線は斜面の高さがずれるため使わない
            if (i == RayCount / 2) feetGapUnits = hit.distance - liftUnits;
            if (hit.normal.y <= bestNormalY) continue;

            isGround = true;
            bestNormalY = hit.normal.y;
            groundNormal = hit.normal;
        }

        // 進行方向の先の登り面のうち、今の床より急なものだけ使う（平面へ早く切り替えると跳ねるため）
        if (horizontalStepUnits != 0f)
        {
            float moveSign = Mathf.Sign(horizontalStepUnits);
            Vector2 aheadOrigin = rayOrigin + Vector2.right * (moveSign * halfWidth);
            RaycastHit2D aheadHit = Physics2D.Raycast(aheadOrigin, Vector2.down, rayDistance, layerMask);
            bool isSteeperRise = aheadHit.normal.x * moveSign <= 0f && (!isGround || aheadHit.normal.y < groundNormal.y);
            if (aheadHit.collider != null && aheadHit.normal.y >= minNormalY && isSteeperRise)
            {
                isGround = true;
                groundNormal = aheadHit.normal;
            }
        }

        return isGround;
    }
}
