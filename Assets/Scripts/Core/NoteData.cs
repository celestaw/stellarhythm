using System;
using UnityEngine;

namespace StellarRhythm.Core
{
    /// <summary>
    /// 1ノーツ分のデータ。ChartData のリストに格納される。
    /// </summary>
    [Serializable]
    public struct NoteData
    {
        [Tooltip("曲開始からの絶対時間（秒）")]
        public float time;

        [Range(0, 5), Tooltip("レーン番号（0 = 左端、5 = 右端）")]
        public int lane;

        [Tooltip("ノーツの種別")]
        public NoteType type;

        [Tooltip("ホールドノーツの押下継続時間（秒）。ホールド以外は 0")]
        public float duration;

        [Tooltip("エアリアルノーツのスライド方向（左右）。StepAerial 以外では無視される")]
        public AerialDirection aerialDirection;

        [Tooltip("エアリアルノーツの色タイプ（難易度変化）。StepAerial 以外では無視される")]
        public AerialDifficultyType aerialDifficultyType;
    }
}
