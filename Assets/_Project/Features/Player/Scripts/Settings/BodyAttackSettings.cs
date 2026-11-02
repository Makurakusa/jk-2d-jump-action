using UnityEngine;

// ボディアタックのパラメータ。値の意味は Docs/Specs/BodyAttack.md を参照
[CreateAssetMenu(fileName = "BodyAttackSettings", menuName = "Scriptable Objects/BodyAttackSettings")]
public class BodyAttackSettings : ScriptableObject
{
    [field: SerializeField] public float Speed { get; private set; } = 22f; // 水平速度の基準値（ユニット/秒）。歩きの最高速とダッシュの最高速の間にする
    [field: SerializeField] public float UpwardSpeed { get; private set; } = 22f; // 発動時に与える上向き速度（ユニット/秒）。「やや上に」飛び込む高さを決める
    [field: SerializeField] public float TransitionSeconds { get; private set; } = 0.08f; // 発動してからコライダーを縮めるまでの時間（秒）。しゃがみの TransitionSeconds と同じ数値だが、設定は別に持つ
    [field: SerializeField] public float BufferSeconds { get; private set; } = 0.08f; // 発動できる前に押したボタンを保持する時間（秒）。ジャンプの先行入力と同じ数値
}
