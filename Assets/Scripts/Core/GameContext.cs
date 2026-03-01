namespace StellarRhythm.Core
{
    /// <summary>
    /// シーン間で選曲データを受け渡す静的コンテキスト。
    /// Unity の static は同一プレイセッション内で維持されるため、
    /// SceneManager.LoadScene をまたいでデータを渡せる。
    /// </summary>
    public static class GameContext
    {
        /// <summary>選曲シーンで選ばれた曲。GameScene で参照する。</summary>
        public static SongEntry SelectedSong { get; set; }

        /// <summary>
        /// ハイスピード設定値。5.0 を基準とし、値が大きいほどノーツが速く流れる。
        /// 10.0 = 2 倍速、2.5 = 0.5 倍速。範囲: 0.5–15.0。
        /// </summary>
        public static float HiSpeed { get; set; } = 5f;
    }
}
