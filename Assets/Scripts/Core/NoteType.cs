namespace StellarRhythm.Core
{
    /// <summary>
    /// ノーツの種別。
    /// Step系（白）: 叩くと相手にダメージ、見逃してもノーペナルティ。
    /// Killer系（赤）: 相手の攻撃。成功でカウンターダメージ、失敗で自分にダメージ。
    /// </summary>
    public enum NoteType
    {
        // ---- ステップノーツ（白系） ----
        StepTap,      // 単押し
        StepHold,     // 長押し（終端判定なし）
        StepAerial,   // 横矢印型。叩くと横スライドしてキラーノーツを回避できる

        // ---- キラーノーツ（赤系） ----
        KillerTap,    // 単押し
        KillerHold,   // 長押し
    }

    /// <summary>
    /// エアリアルノーツのスライド方向（見た目のみ）。難易度とは無関係。
    /// </summary>
    public enum AerialDirection
    {
        Left,   // 左スライド
        Right,  // 右スライド
    }

    /// <summary>
    /// エアリアルノーツの色タイプ。難易度変化を決定する。
    /// AerialDirection とは独立したパラメータ。
    /// </summary>
    public enum AerialDifficultyType
    {
        A,  // 色A: 難易度上昇
        B,  // 色B: 難易度低下
        C,  // 色C: 難易度変化なし
    }
}
