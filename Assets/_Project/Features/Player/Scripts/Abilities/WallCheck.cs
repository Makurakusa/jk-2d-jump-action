using UnityEngine;

// 進む向きの先に、壁がコライダーで接しているかを調べる。
// ボディアタックが壁に当たって終わるかを決めるために使う
public class WallCheck
{
    const int MaxContactCount = 8; // 一度に調べる接触点の最大数。毎フレームの確保（GC）を避けるため、配列を使い回す

    readonly Rigidbody2D body;
    readonly float minWallNormalAbsX; // 壁とみなす面の法線 x の絶対値の下限（床や斜面を壁扱いにしないため）
    readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[MaxContactCount];

    public WallCheck(Rigidbody2D body, float minWallNormalAbsX)
    {
        this.body = body;
        this.minWallNormalAbsX = minWallNormalAbsX;
    }

    // 進む向き（-1 / 1）の先に、法線がほぼ水平な面が接しているか。
    // 接触の法線は「相手から自分へ向く」ので、壁が進む向きにあるなら、法線は進む向きと逆を向く。
    // 逆向きの壁（背中側）には反応させない。動作確認（手順 8）で向きが逆なら、比較の符号を反転する
    public bool IsTouchingWallAhead(float moveDirection)
    {
        int contactCount = body.GetContacts(contactBuffer);

        for (int contactIndex = 0; contactIndex < contactCount; contactIndex++)
        {
            float normalAlongMove = contactBuffer[contactIndex].normal.x * moveDirection;
            bool isWallAhead = normalAlongMove <= -minWallNormalAbsX;
            if (isWallAhead) return true;
        }

        return false;
    }
}
