using UnityEngine;

[CreateAssetMenu(fileName = "JumpSettings", menuName = "Scriptable Objects/JumpSettings")]
public class JumpSettings : ScriptableObject
{
    [field: SerializeField] public float JumpHeightUnits { get; private set; } = 9f; // ジャンプの最高到達点（ユニット）
    [field: SerializeField] public float TimeToApexSeconds { get; private set; } = 0.4f; // ジャンプ開始から頂点までの時間（秒）
    [field: SerializeField] public float FallGravityMultiplier { get; private set; } = 1.5f; // 落下中に重力へ掛ける倍率
    [field: SerializeField] public float JumpReleaseVelocityMultiplier { get; private set; } = 0.5f; // 上昇中にボタンを離したときの速度倍率
    [field: SerializeField] public float MaxFallSpeedUnits { get; private set; } = 25f; // 落下速度の上限（ユニット/秒）。薄い床のすり抜けと速すぎる着地を防ぐ
    [field: SerializeField] public float JumpBufferSeconds { get; private set; } = 0.08f; // 着地前に押したジャンプを受け付ける時間（約5F）
    [field: SerializeField] public float CoyoteSeconds { get; private set; } = 0.1f; // 足場を離れた後もジャンプできる猶予時間（約6F）
}
