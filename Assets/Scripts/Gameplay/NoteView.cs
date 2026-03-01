using System;
using System.Collections.Generic;
using UnityEngine;
using StellarRhythm.Core;

namespace StellarRhythm.Gameplay
{
    /// <summary>
    /// スポーン後のノーツ 1 個の視覚・移動・ライフサイクルを管理する。
    /// ChartLoader がスポーン直後に Initialize() を呼ぶ。
    ///
    /// ■ 移動モデル
    ///   ルートの position を spawnPos → hitPos へ線形移動。
    ///   hitDspTime までの残り時間 / lookAhead で [0,1] 進捗を求めるため、
    ///   フレーム落ちがあっても自動的に正しい位置へ補正される。
    ///
    /// ■ 座標系（GameScene 標準設定）
    ///   スポーン: Z = 26  →  ヒットライン: Z = 0
    ///   _travelDirection = Vector3.back (0,0,-1)、_travelDistance = 26
    ///
    /// ■ ビジュアル構造
    ///   子オブジェクト "Visual"（Cube）の localScale.z / localPosition.z を
    ///   Initialize() 時に設定する。
    ///     ・タップ: Z 長さ = _tapLength（固定、縦横比 1:7 に対応）
    ///     ・ホールド: Z 長さ = duration × (travelDistance / lookAhead)
    ///   Visual の localPosition.z = Z長さ / 2 にすることで、
    ///   Cube の前端（-Z 端）がルート位置（ノーツヘッド）と一致する。
    ///
    /// ■ ホールドノーツの判定モデル
    ///   HoldState.None          : 未押下
    ///   HoldState.Active        : 押下中。1 拍ごとに sync を自動判定する
    ///   HoldState.PendingEndJudge : キーを離した後、終端判定（miss）待ち
    ///   HoldState.Completed     : 判定完了。次の Update() で Destroy される
    /// </summary>
    public class NoteView : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("スポーン地点からヒットラインへの移動方向（正規化不要）")]
        [SerializeField] Vector3 _travelDirection = Vector3.back;

        [Tooltip("スポーン地点からヒットラインまでの距離（ワールド単位）")]
        [SerializeField] float _travelDistance = 26f;

        [Tooltip("ノーツ末端がこの Z 座標を下回ったら自動削除（カメラ Z=-3 より奥）")]
        [SerializeField] float _destroyBelowZ = -4f;

        [Header("Visual")]
        [Tooltip("タップノーツの Z 方向長さ（縦横比 1:7 → 0.85 / 7 ≈ 0.12）")]
        [SerializeField] float _tapLength = 0.12f;

        // ─── 静的レジストリ（全レーンのアクティブなノーツを追跡）─────────
        static readonly List<NoteView> _activeNotes = new List<NoteView>();

        // ── ランタイム状態 ───────────────────────────────────────────────
        NoteData _data;
        double   _hitDspTime;
        double   _lookAhead;
        Vector3  _spawnPos;
        Vector3  _hitPos;
        float    _noteZLength;
        bool     _initialized;

        // ─── ホールド判定ステート ─────────────────────────────────────────
        enum HoldState { None, Active, PendingEndJudge, Completed }

        bool      _isHold;
        double    _beatSeconds;       // 1 拍の秒数 (= 60 / BPM)
        double    _intervalSeconds;   // 内部拍判定の間隔（= _beatSeconds × HoldJudgeIntervalBeats）
        double    _holdEndDspTime;    // hitDspTime + duration（秒）
        bool      _hasEndJudge;       // 終端に sync/miss 判定を発行するか（duration > 0 の場合は常に true）
        HoldState _holdState;
        int       _nextBeatIndex;     // 次に判定するインデックス n（判定時刻 = hitDspTime + n * _intervalSeconds）

        /// <summary>ホールド内部拍の判定間隔（拍単位）。0.5 = 0.5 拍ごとに判定。</summary>
        const double HoldJudgeIntervalBeats = 0.5;

        // ─── 公開プロパティ（JudgeManager から参照）──────────────────────
        /// <summary>このノーツのヒット DSP 時刻。</summary>
        public double HitDspTime => _hitDspTime;

        /// <summary>このノーツのレーン番号（0–5）。</summary>
        public int Lane => _data.lane;

        /// <summary>既に判定済み（消費済み）かどうか。</summary>
        public bool IsConsumed { get; private set; }

        /// <summary>ホールドノーツかどうか。</summary>
        public bool IsHold => _isHold;

        /// <summary>ホールド判定が全て完了したかどうか。true になった次フレームで Destroy される。</summary>
        public bool IsHoldCompleted => _holdState == HoldState.Completed;

        // ================================================================
        //  初期化
        // ================================================================

        /// <summary>ChartLoader がスポーン直後に一度だけ呼ぶ。</summary>
        /// <param name="beatSeconds">1 拍の秒数（= 60 / BPM）。ホールド内部拍判定に使用。</param>
        public void Initialize(double hitDspTime, NoteData data, float lookAheadSeconds, float beatSeconds)
        {
            _hitDspTime  = hitDspTime;
            _data        = data;
            _lookAhead   = lookAheadSeconds;
            _initialized = true;

            _spawnPos = transform.position;
            _hitPos   = _spawnPos + _travelDirection.normalized * _travelDistance;

            SetupVisual(data, lookAheadSeconds);

            // ─── ホールド判定の初期化 ─────────────────────────────────
            _isHold          = data.type == NoteType.StepHold || data.type == NoteType.KillerHold;
            _beatSeconds     = beatSeconds;
            _intervalSeconds = _beatSeconds * HoldJudgeIntervalBeats;

            if (_isHold && data.duration > 0f)
            {
                _holdEndDspTime = hitDspTime + data.duration;

                // 終端には常に sync/miss 判定を付ける
                _hasEndJudge = true;
            }

            _activeNotes.Add(this);
        }

        void OnDestroy()
        {
            _activeNotes.Remove(this);
        }

        /// <summary>
        /// 判定が確定したノーツを消費する（タップ専用）。
        /// ホールドノーツは StartHold() → PollJudgment() で管理するため、Destroy を呼ばない。
        /// </summary>
        public void ConsumeNote()
        {
            if (IsConsumed) return;
            IsConsumed = true;
            if (!_isHold)
                Destroy(gameObject);
        }

        // ================================================================
        //  ホールド API
        // ================================================================

        /// <summary>
        /// ホールドの押下を開始する。IsConsumed を立てて FindNearestInLane から除外し、
        /// now 以降の最初の内部拍から判定を開始するようにインデックスを計算する。
        /// </summary>
        public void StartHold(double now)
        {
            if (!_isHold) return;
            IsConsumed  = true;         // FindNearestInLane の対象から外す
            _holdState  = HoldState.Active;

            // 次に判定すべきインデックス: now より後にある最初の n（n ≥ 1）
            double elapsed = now - _hitDspTime;
            _nextBeatIndex = elapsed <= 0.0
                ? 1
                : (int)Math.Floor(elapsed / _intervalSeconds) + 1;
        }

        /// <summary>
        /// キーを離した時に呼ぶ。
        /// 終端判定が残っていれば PendingEndJudge に移行し、miss を待つ。
        /// </summary>
        public void ReleaseKey()
        {
            if (_holdState != HoldState.Active) return;
            _holdState = HoldState.PendingEndJudge;
        }

        /// <summary>
        /// 毎フレーム JudgeManager から呼ぶ。
        /// 内部拍または終端の判定が発生した場合に判定文字列（"sync" / "miss"）を返す。
        /// 発生しなければ null。IsHoldCompleted が true になったら追跡を終了してよい。
        /// </summary>
        public string PollJudgment(double now)
        {
            if (_holdState == HoldState.None || _holdState == HoldState.Completed) return null;

            if (_holdState == HoldState.Active)
            {
                double nextBeatTime = _hitDspTime + _nextBeatIndex * _intervalSeconds;

                // 次の内部拍判定：判定時刻を越えており、かつ終端より前
                if (nextBeatTime < _holdEndDspTime && now >= nextBeatTime)
                {
                    _nextBeatIndex++;
                    return "sync";
                }

                // 終端を越えた
                if (now >= _holdEndDspTime)
                {
                    _holdState = HoldState.Completed;
                    return "sync";  // 押下継続のまま終端通過 → sync
                }
            }
            else // PendingEndJudge：キーを離した後、終端 miss を待つ
            {
                if (now >= _holdEndDspTime)
                {
                    _holdState = HoldState.Completed;
                    return "miss";
                }
            }

            return null;
        }

        // ─── 静的クエリ ──────────────────────────────────────────────────

        /// <summary>
        /// 指定レーンの未消費ノーツのうち、currentDspTime に最も近いものを返す。
        /// 時間差の絶対値が maxDiffSeconds を超える場合は null を返す。
        /// </summary>
        public static NoteView FindNearestInLane(int lane, double currentDspTime, double maxDiffSeconds)
        {
            NoteView nearest = null;
            double   minDiff = double.MaxValue;

            foreach (var note in _activeNotes)
            {
                if (note == null || note.IsConsumed || note.Lane != lane) continue;
                double diff = Math.Abs(note.HitDspTime - currentDspTime);
                if (diff < minDiff && diff <= maxDiffSeconds)
                {
                    minDiff = diff;
                    nearest = note;
                }
            }
            return nearest;
        }

        /// <summary>
        /// 途中押し用。指定レーンで、まだ始点判定を受けていない（IsConsumed=false かつ
        /// HoldState.None）進行中のホールドノーツを返す。
        /// now が hitDspTime より後かつ holdEndDspTime より前のノーツを対象とする。
        /// </summary>
        public static NoteView FindActiveHoldInLane(int lane, double now)
        {
            foreach (var note in _activeNotes)
            {
                if (note == null || note.IsConsumed || note.Lane != lane) continue;
                if (!note._isHold || note._holdState != HoldState.None) continue;
                if (now > note._hitDspTime && now < note._holdEndDspTime)
                    return note;
            }
            return null;
        }

        // ================================================================
        //  毎フレーム更新
        // ================================================================

        void Update()
        {
            if (!_initialized) return;

            // ホールド判定が完了したら破棄
            if (_isHold && _holdState == HoldState.Completed)
            {
                Destroy(gameObject);
                return;
            }

            // 進捗 t : 0 = スポーン地点、1 = ヒットライン（クランプしないので通過後も継続）
            double remaining = _hitDspTime - AudioSettings.dspTime;
            float t = 1f - (float)(remaining / _lookAhead);
            transform.position = Vector3.LerpUnclamped(_spawnPos, _hitPos, t);

            // ノーツ末端（ヘッド位置 + ノーツ長）がカメラより奥に出たら削除
            if (transform.position.z + _noteZLength < _destroyBelowZ)
                Destroy(gameObject);
        }

        // ================================================================
        //  ビジュアルセットアップ
        // ================================================================

        /// <summary>
        /// 子オブジェクト "Visual" の scale.z と localPosition.z を設定する。
        ///   ・前端（-Z 端）= ノーツヘッド（ルート位置）
        ///   ・後端（+Z 端）= ヘッドから noteZLength 奥
        /// </summary>
        void SetupVisual(NoteData data, float lookAheadSeconds)
        {
            Transform visual = transform.Find("Visual");
            if (visual == null) return;

            _noteZLength = data.duration > 0f
                ? data.duration * (_travelDistance / lookAheadSeconds)
                : _tapLength;

            // Z 方向の長さを設定
            Vector3 s = visual.localScale;
            s.z = _noteZLength;
            visual.localScale = s;

            // Cube 中心をヘッドから奥方向へ半分ずらし、前端をヘッドに合わせる
            visual.localPosition = new Vector3(0f, 0f, _noteZLength * 0.5f);
        }
    }
}
