using UnityEngine;

public static class Rigidbody2DExtensions
{
    // 水平速度だけを、指定した加速度で目標へ近づける。垂直速度はそのまま残す
    public static void MoveVelocityXTowards(this Rigidbody2D body, float targetX, float acceleration)
    {
        Vector2 currentVelocity = body.linearVelocity;
        float nextX = Mathf.MoveTowards(
            currentVelocity.x,
            targetX,
            acceleration * Time.fixedDeltaTime);

        body.linearVelocity = new Vector2(nextX, currentVelocity.y);
    }
}
