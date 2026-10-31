using UnityEngine;

[CreateAssetMenu(fileName = "DiveSettings", menuName = "Scriptable Objects/DiveSettings")]
public class DiveSettings : ScriptableObject
{
    [field: SerializeField] public float SpeedMultiplier { get; private set; } = 1.6f; // 急降下中の落下速度倍率（通常の最大落下速度に対して）
    [field: SerializeField] public float StickThreshold { get; private set; } = 0.6f; // 下入力とみなすスティックの倒し量（0〜1）
    [field: SerializeField] public int BufferFrames { get; private set; } = 4; // 下はじきを先行受付するフレーム数
}
