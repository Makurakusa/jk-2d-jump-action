using UnityEngine;

[CreateAssetMenu(fileName = "BasicMoveSettings", menuName = "Scriptable Objects/BasicMoveSettings")]
public class BasicMoveSettings : ScriptableObject
{
    [field: SerializeField] public float MoveSpeed { get; private set; } = 12f; // 歩行時の最高速度（ユニット/秒）
    [field: SerializeField] public float GroundAcceleration { get; private set; } = 160f; // 地上で歩行速度へ変化させる強さ
    [field: SerializeField] public float AirAcceleration { get; private set; } = 80f; // 空中で左右入力に合わせて速度を変化させる強さ
    [field: SerializeField] public float AirDeceleration { get; private set; } = 15f; // 空中で左右を離したときの減速の強さ。小さいほどジャンプ前の慣性が残る
    [field: SerializeField] public float ReverseAccelerationMultiplier { get; private set; } = 2f; // 進行方向と逆へ入力したときの加速度の倍率。切り返しを素早くする
    [field: SerializeField] public float HoldThreshold { get; private set; } = 0.6f; // 左右を「倒している」とみなす値（0〜1）。反転・向き・ダッシュの継続の判定に使う
}
