using UnityEngine;

[CreateAssetMenu(fileName = "DashSettings", menuName = "Scriptable Objects/DashSettings")]
public class DashSettings : ScriptableObject
{
    [field: SerializeField] public float MaxDashSpeed { get; private set; } = 28f; // ダッシュ中の最高速度（ユニット/秒）
    [field: SerializeField] public float DashAcceleration { get; private set; } = 90f; // ダッシュへ加速する強さ
    [field: SerializeField] public float BrakeAcceleration { get; private set; } = 60f; // ブレーキ状態で速度を0へ近づける強さ
    [field: SerializeField] public float BrakeDuration { get; private set; } = 0.2f; // 最高速からの反転時に続くブレーキ状態の時間（秒）

    // 最高速を BrakeDuration で止めるには MaxDashSpeed / BrakeDuration 以上の加速度が要る
    void OnValidate()
    {
        if (BrakeDuration <= 0f) return;

        float requiredAcceleration = MaxDashSpeed / BrakeDuration;
        if (BrakeAcceleration < requiredAcceleration)
        {
            Debug.LogWarning(
                $"{name}: BrakeAcceleration {BrakeAcceleration} が必要値 {requiredAcceleration} を下回っている。ブレーキが BrakeDuration 内に終わらない",
                this);
        }
    }
}
