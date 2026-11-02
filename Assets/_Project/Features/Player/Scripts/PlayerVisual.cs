using UnityEngine;

// 状態に合わせてスプライトを切り替え、向きに合わせて左右反転する。絵は右向きを前提にする
[RequireComponent(typeof(PlayerMover), typeof(SpriteRenderer))]
public class PlayerVisual : MonoBehaviour
{
    [SerializeField] Sprite idleSprite; // 地上で走っていないとき（停止・歩き）
    [SerializeField] Sprite runningSprite; // 歩行の最高速を超えているとき
    [SerializeField] Sprite jumpingSprite; // 空中（上昇・落下・急降下・ボディアタック）で共用する
    [SerializeField] Sprite crouchingSprite; // しゃがみ・しゃがみ歩き・しゃがみ滑り・移行中・解除中で共用する

    PlayerMover mover;
    SpriteRenderer spriteRenderer;

    void Awake()
    {
        mover = GetComponent<PlayerMover>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // FixedUpdate で決まった状態を、描画の直前に反映する
    void LateUpdate()
    {
        Sprite sprite = ChooseSprite();
        if (sprite != null) spriteRenderer.sprite = sprite;

        spriteRenderer.flipX = mover.FacingDirection < 0f;
    }

    // しゃがみ → 空中 → 走り → 待機・歩き の順に調べる。しゃがみは接地中にしか起きないので、先に調べてよい
    Sprite ChooseSprite()
    {
        if (mover.IsCrouched) return crouchingSprite;
        if (!mover.IsGrounded) return jumpingSprite;

        return mover.IsRunning ? runningSprite : idleSprite;
    }
}
