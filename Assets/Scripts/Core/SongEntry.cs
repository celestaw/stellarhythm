using UnityEngine;

namespace StellarRhythm.Core
{
    /// <summary>
    /// 選曲画面に表示する 1 曲分のエントリ。
    /// 右クリック → Create → StellarRhythm → Song Entry で作成する。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSongEntry", menuName = "StellarRhythm/Song Entry")]
    public class SongEntry : ScriptableObject
    {
        [Tooltip("選曲画面に表示する曲名")]
        public string title = "Untitled";

        [Tooltip("アーティスト名")]
        public string artist = "";

        [Tooltip("楽曲 AudioClip")]
        public AudioClip music;

        [Tooltip("譜面 JSON（Assets/ChartData/ 以下）")]
        public TextAsset chartJson;
    }
}
