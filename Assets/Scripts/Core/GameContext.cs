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
    }
}
