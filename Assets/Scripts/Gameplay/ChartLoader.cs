using System;
using System.Collections.Generic;
using UnityEngine;
using StellarRhythm.Core;

namespace StellarRhythm.Gameplay
{
    /// <summary>
    /// 譜面 JSON を読み込み、AudioSettings.dspTime ベースで音声と同期しながら
    /// ノーツを正確なタイミングでスポーンするコンポーネント。
    ///
    /// ■ タイミングモデル
    ///   dspStartTime : 音声が鳴り始める DSP 時刻（外部から StartChartAt で渡す）
    ///   chartTime    : dspStartTime 起点の経過秒 − offset
    ///                  = AudioSettings.dspTime − dspStartTime − offset
    ///   スポーン条件 : note.time ≤ chartTime + lookAheadSeconds
    ///
    /// ■ 2 通りの使い方
    ///   1. スタンドアロン (_autoStart=true, _chartJson / _audioSource をInspectorで設定)
    ///      → Start() が自動的に StartChart() を呼び、音声も再生する。
    ///   2. Director 制御 (_autoStart=false)
    ///      → GameSceneDirector が SetChart() → StartChartAt() を呼ぶ。
    ///         音声再生は GameSceneDirector 側の AudioSource が行う。
    /// </summary>
    public class ChartLoader : MonoBehaviour
    {
        // ─── JSON デシリアライズ用 DTO ───────────────────────────────────
        [Serializable]
        private class ChartDto
        {
            public string         songTitle = "";
            public string         artist    = "";
            public float          bpm       = 120f;
            public float          offset    = 0f;
            public List<NoteData> notes     = new List<NoteData>();
        }

        // ─── Inspector ──────────────────────────────────────────────────
        [Header("Chart")]
        [Tooltip("譜面 JSON を TextAsset としてアサイン（スタンドアロン用）")]
        [SerializeField] TextAsset _chartJson;

        [Header("Auto Start (スタンドアロン用)")]
        [Tooltip("true にすると Start() で自動再生。GameSceneDirector 使用時は false")]
        [SerializeField] bool _autoStart = false;

        [Header("Audio (スタンドアロン用)")]
        [Tooltip("自動再生時に使う AudioSource。Director 制御時は空でよい")]
        [SerializeField] AudioSource _audioSource;

        [Header("Note Prefabs")]
        [SerializeField] GameObject _stepTapPrefab;
        [SerializeField] GameObject _stepHoldPrefab;
        [SerializeField] GameObject _stepAerialPrefab;
        [SerializeField] GameObject _killerTapPrefab;
        [SerializeField] GameObject _killerHoldPrefab;

        [Header("Lane Spawn Points")]
        [Tooltip("Lane 0 (左端) → Lane 5 (右端) の順に 6 つセット")]
        [SerializeField] Transform[] _laneSpawnPoints = new Transform[6];

        [Header("Timing")]
        [Tooltip("ノーツをヒット時刻の何秒前にスポーンするか。NoteView の travelDistance / speed と一致させること")]
        [SerializeField] float _lookAheadSeconds = 3f;

        [Tooltip("スタンドアロン自動再生時のスケジュール余裕時間（秒）")]
        [SerializeField] double _scheduleDelay = 0.5;

        // ─── プロパティ ──────────────────────────────────────────────────
        /// <summary>チャートが現在再生中かどうか（全ノーツを処理し終えると false になる）。</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>読み込み済みチャートの BPM。未ロード時は 120。</summary>
        public float Bpm => _chart?.bpm ?? 120f;

        /// <summary>最終ノーツのヒット DSP 時刻（ホールド tail 含む）。GameSceneDirector の終了待ちに使う。</summary>
        public double ChartEndDspTime { get; private set; }

        /// <summary>スポーン先読み時間（外部から調整可）。</summary>
        public float LookAheadSeconds
        {
            get => _lookAheadSeconds;
            set => _lookAheadSeconds = value;
        }

        // ─── ランタイム状態 ──────────────────────────────────────────────
        ChartDto _chart;
        int      _nextNoteIndex;
        double   _dspStartTime;
        bool     _isPlaying;

        // ================================================================
        //  ライフサイクル
        // ================================================================

        void Awake()
        {
            if (_chartJson != null)
                ParseChart(_chartJson);
        }

        void Start()
        {
            if (_autoStart && _chart != null)
                StartChart();
        }

        // ================================================================
        //  公開 API
        // ================================================================

        /// <summary>
        /// 外部から譜面を差し替える（GameSceneDirector から呼ぶ）。
        /// </summary>
        public void SetChart(TextAsset json)
        {
            _chartJson = json;
            ParseChart(json);
        }

        /// <summary>
        /// スタンドアロン再生（AudioSource も含め全部 ChartLoader が管理）。
        /// </summary>
        public void StartChart()
        {
            double dspTime = AudioSettings.dspTime + _scheduleDelay;
            if (_audioSource != null)
                _audioSource.PlayScheduled(dspTime);
            StartChartAt(dspTime);
        }

        /// <summary>
        /// 外部からスタート時刻を指定して再生開始（音声再生は呼び出し元が行う）。
        /// GameSceneDirector はこちらを使う。
        /// </summary>
        public void StartChartAt(double dspStartTime)
        {
            if (_chart == null)
            {
                Debug.LogError("[ChartLoader] チャートが読み込まれていません。SetChart() を先に呼んでください。");
                return;
            }

            _nextNoteIndex = 0;
            _dspStartTime  = dspStartTime;
            _isPlaying     = true;

            // 終了 DSP 時刻を算出（GameSceneDirector の終了待ちに使用）
            if (_chart.notes.Count > 0)
            {
                var last = _chart.notes[_chart.notes.Count - 1];
                ChartEndDspTime = dspStartTime + _chart.offset + last.time + last.duration;
            }
            else
            {
                ChartEndDspTime = dspStartTime;
            }

            Debug.Log($"[ChartLoader] StartChartAt dsp={dspStartTime:F4}  ChartEnd={ChartEndDspTime:F4}");
        }

        // ================================================================
        //  毎フレーム処理
        // ================================================================

        void Update()
        {
            if (!_isPlaying || _chart == null) return;

            double chartTime = AudioSettings.dspTime - _dspStartTime - _chart.offset;

            while (_nextNoteIndex < _chart.notes.Count)
            {
                NoteData note = _chart.notes[_nextNoteIndex];
                if ((double)note.time > chartTime + _lookAheadSeconds) break;

                SpawnNote(in note);
                _nextNoteIndex++;
            }

            if (_nextNoteIndex >= _chart.notes.Count)
                _isPlaying = false;
        }

        // ================================================================
        //  スポーン
        // ================================================================

        void SpawnNote(in NoteData note)
        {
            GameObject prefab = PrefabFor(note.type);
            if (prefab == null)
            {
                Debug.LogWarning($"[ChartLoader] NoteType.{note.type} のプレファブが未設定です。スキップします。");
                return;
            }

            if ((uint)note.lane >= (uint)_laneSpawnPoints.Length || _laneSpawnPoints[note.lane] == null)
            {
                Debug.LogWarning($"[ChartLoader] lane={note.lane} のスポーン地点が未設定です。スキップします。");
                return;
            }

            Transform spawnPoint = _laneSpawnPoints[note.lane];
            GameObject go = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);

            double hitDspTime = _dspStartTime + _chart.offset + note.time;
            if (go.TryGetComponent<NoteView>(out var view))
                view.Initialize(hitDspTime, note, _lookAheadSeconds);
        }

        GameObject PrefabFor(NoteType type) => type switch
        {
            NoteType.StepTap    => _stepTapPrefab,
            NoteType.StepHold   => _stepHoldPrefab,
            NoteType.StepAerial => _stepAerialPrefab,
            NoteType.KillerTap  => _killerTapPrefab,
            NoteType.KillerHold => _killerHoldPrefab,
            _                   => _stepTapPrefab,
        };

        // ================================================================
        //  内部
        // ================================================================

        void ParseChart(TextAsset json)
        {
            if (json == null) { _chart = null; return; }

            _chart = JsonUtility.FromJson<ChartDto>(json.text);
            if (_chart?.notes == null)
            {
                Debug.LogError("[ChartLoader] JSON のパースに失敗しました。");
                _chart = null;
                return;
            }

            // time / duration はビート単位で記述されているため秒に変換する
            float beatLen = 60f / _chart.bpm;
            for (int i = 0; i < _chart.notes.Count; i++)
            {
                var n = _chart.notes[i];
                n.time     *= beatLen;
                n.duration *= beatLen;
                _chart.notes[i] = n;
            }

            _chart.notes.Sort((a, b) => a.time.CompareTo(b.time));
            Debug.Log($"[ChartLoader] ロード完了: {_chart.songTitle} / {_chart.notes.Count} notes / BPM {_chart.bpm}");
        }
    }
}
