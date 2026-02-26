using System.Collections.Generic;
using UnityEngine;

namespace StellarRhythm.Core
{
    /// <summary>
    /// 1曲分の譜面データ。
    /// Unity エディタ上で右クリック → Create → StellarRhythm → Chart Data から作成できる。
    /// </summary>
    [CreateAssetMenu(fileName = "NewChart", menuName = "StellarRhythm/Chart Data")]
    public class ChartData : ScriptableObject
    {
        [Header("Song Info")]
        [Tooltip("曲名")]
        public string songTitle;

        [Tooltip("アーティスト名")]
        public string artist;

        [Tooltip("音声クリップ")]
        public AudioClip audioClip;

        [Header("Timing")]
        [Tooltip("BPM（テンポ）")]
        public float bpm = 120f;

        [Tooltip("音声開始に対するノーツ開始のオフセット（秒）。正値で遅延、負値で先行")]
        public float offset = 0f;

        [Header("Notes")]
        [Tooltip("ノーツリスト（time 昇順で並べることを推奨）")]
        public List<NoteData> notes = new();
    }
}
