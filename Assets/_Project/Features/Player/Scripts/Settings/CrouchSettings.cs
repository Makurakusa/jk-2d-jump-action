using UnityEngine;

// しゃがみ・しゃがみ滑りのパラメータ。値の意味は Docs/Specs/Crouch.md を参照
[CreateAssetMenu(fileName = "CrouchSettings", menuName = "Scriptable Objects/CrouchSettings")]
public class CrouchSettings : ScriptableObject
{
    [field: SerializeField] public float StickThreshold { get; private set; } = 0.6f; // しゃがむとみなすスティックの下方向の倒し量（0〜1）。急降下の閾値とは別に持つ
    [field: SerializeField] public float WalkSpeedMultiplier { get; private set; } = 0.4f; // しゃがみ歩きの最高速の倍率（歩きの MoveSpeed に掛ける）
    [field: SerializeField] public float SlideDeceleration { get; private set; } = 35f; // しゃがみ滑りで水平速度を0へ近づける強さ（ユニット/秒²）。滑る時間はこれだけで決まる
    [field: SerializeField] public float DashEntrySpeed { get; private set; } = 28f; // 滑りを解除してダッシュに移るときの水平速度（ユニット/秒）。減速した現在の速度ではなくこの値から入る
    [field: SerializeField] public float TransitionSeconds { get; private set; } = 0.08f; // しゃがむ移行と立ち上がる解除にかける時間（秒）。滑りでコライダーを縮めるまでの時間にも使う
    [field: SerializeField] public float CrouchColliderHeightMultiplier { get; private set; } = 0.5f; // しゃがんだときのコライダーの高さ（y）に掛ける倍率。幅（x）は変えない
}
