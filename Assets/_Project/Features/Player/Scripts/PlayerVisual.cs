using UnityEngine;

// 状態に合わせてスプライトを切り替え、向きに合わせて左右反転する。絵は右向きを前提にする
[RequireComponent(typeof(PlayerMover), typeof(SpriteRenderer))]
public class PlayerVisual : MonoBehaviour
{
    [SerializeField] Sprite idleSprite; // 地上で走っていないとき（停止・歩き）
    [SerializeField] Sprite runningSprite; // 歩行の最高速を超えているとき
    [SerializeField] Sprite jumpingSprite; // 空中（上昇・落下・急降下）で共用する

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

    Sprite ChooseSprite()
    {
        if (!mover.IsGrounded) return jumpingSprite;
        return mover.IsRunning ? runningSprite : idleSprite;
    }
}
