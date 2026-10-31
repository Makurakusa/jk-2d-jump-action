public readonly struct MoveInput
{
    public readonly float Horizontal; // 左右入力（-1〜1）
    public readonly float Vertical; // 上下入力（-1〜1）。下はじきの検出に使う
    public readonly bool IsDashHeld; // ダッシュボタンを押しているか

    public MoveInput(float horizontal, float vertical, bool isDashHeld)
    {
        Horizontal = horizontal;
        Vertical = vertical;
        IsDashHeld = isDashHeld;
    }
}
