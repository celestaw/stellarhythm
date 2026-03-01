using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StellarRhythm.Gameplay
{
    /// <summary>
    /// レーンキー入力（W/E/R/U/I/O = Lane 0–5）を受け取り、
    /// AudioSettings.dspTime ベースでノーツとのタイミング差を計算して判定を下す。
    ///
    /// ■ 判定ウィンドウ（時間差の絶対値で最初に一致した閾値を適用する）
    ///   ≤ 41ms → sync  （最上位）
    ///   ≤ 85ms → great
    ///   ≤ 110ms → good
    ///   ≤ 125ms → miss
    ///   > 125ms → 判定なし（入力は無視）
    ///
    /// ■ ホールドノーツの判定フロー
    ///   1. キーダウン時に始点判定（通常の sync/great/good/miss）を実施し、
    ///      NoteView.StartHold() を呼んでホールド追跡を開始する。
    ///   2. 始点の miss 閾値を超えた後にキーを押した場合（途中押し）は
    ///      FindActiveHoldInLane() で進行中のホールドを探して追跡を開始する（始点判定なし）。
    ///   3. 追跡中は Update() で毎フレーム NoteView.PollJudgment() を呼び、
    ///      1 拍ごとの sync 判定を処理する。
    ///   4. キーアップ時に NoteView.ReleaseKey() を呼ぶ。
    ///      終端時刻を過ぎると PollJudgment() が miss を返す（キーを離したまま終端通過）。
    ///      キーを押したまま終端を迎えた場合は sync を返す。
    ///   5. IsHoldCompleted が true になったら追跡を終了する。
    /// </summary>
    public class JudgeManager : MonoBehaviour
    {
        // ─── 判定閾値（秒）─────────────────────────────────────────────
        const double SyncThreshold  = 0.041;
        const double GreatThreshold = 0.085;
        const double GoodThreshold  = 0.110;
        const double MissThreshold  = 0.125;

        // ─── レーン → キーバインディング ────────────────────────────────
        static readonly string[] LaneBindings =
        {
            "<Keyboard>/w",
            "<Keyboard>/e",
            "<Keyboard>/r",
            "<Keyboard>/u",
            "<Keyboard>/i",
            "<Keyboard>/o",
        };

        // ─── Inspector ──────────────────────────────────────────────────
        [SerializeField] JudgmentDisplay _display;

        // ─── ランタイム状態 ──────────────────────────────────────────────
        InputAction[] _laneActions;
        bool[]        _keyDown          = new bool[LaneBindings.Length];
        NoteView[]    _trackedHoldNotes = new NoteView[LaneBindings.Length];

        // ================================================================
        //  ライフサイクル
        // ================================================================

        void Awake()
        {
            _laneActions = new InputAction[LaneBindings.Length];
            for (int i = 0; i < LaneBindings.Length; i++)
            {
                int lane = i;   // ラムダキャプチャ用コピー
                _laneActions[i] = new InputAction(binding: LaneBindings[i]);
                _laneActions[i].performed += _ => OnKeyDown(lane);
                _laneActions[i].canceled  += _ => OnKeyUp(lane);
                _laneActions[i].Enable();
            }
        }

        void OnDestroy()
        {
            if (_laneActions == null) return;
            foreach (var action in _laneActions)
            {
                action?.Disable();
                action?.Dispose();
            }
        }

        // ================================================================
        //  毎フレーム：ホールド内部拍判定
        // ================================================================

        void Update()
        {
            double now = AudioSettings.dspTime;

            for (int lane = 0; lane < _trackedHoldNotes.Length; lane++)
            {
                NoteView hold = _trackedHoldNotes[lane];
                if (hold == null) continue;

                string judgment = hold.PollJudgment(now);
                if (judgment != null)
                    _display?.ShowJudgment(lane, judgment);

                if (hold.IsHoldCompleted)
                    _trackedHoldNotes[lane] = null;
            }
        }

        // ================================================================
        //  キー入力ハンドラ
        // ================================================================

        void OnKeyDown(int lane)
        {
            if (_keyDown[lane]) return;         // キーリピートを無視
            _keyDown[lane] = true;

            if (_trackedHoldNotes[lane] != null) return;  // 既にホールド追跡中

            ProcessLaneInput(lane);
        }

        void OnKeyUp(int lane)
        {
            _keyDown[lane] = false;
            _trackedHoldNotes[lane]?.ReleaseKey();
        }

        // ================================================================
        //  判定処理
        // ================================================================

        void ProcessLaneInput(int lane)
        {
            double now = AudioSettings.dspTime;

            // ── 1) 始点判定：miss 閾値内のノーツを探す ──────────────────
            NoteView note = NoteView.FindNearestInLane(lane, now, MissThreshold);
            if (note != null)
            {
                double diff     = Math.Abs(note.HitDspTime - now);
                string judgment = Evaluate(diff);
                if (judgment == null) return;

                if (note.IsHold)
                {
                    note.StartHold(now);
                    _trackedHoldNotes[lane] = note;
                }
                else
                {
                    note.ConsumeNote();
                }

                _display?.ShowJudgment(lane, judgment);
                return;
            }

            // ── 2) 途中押し：進行中のホールドノーツを探す ───────────────
            NoteView holdNote = NoteView.FindActiveHoldInLane(lane, now);
            if (holdNote != null)
            {
                holdNote.StartHold(now);
                _trackedHoldNotes[lane] = holdNote;
                // 始点判定なし（表示なし）
            }
        }

        /// <summary>時間差（秒）を判定文字列に変換する。範囲外は null。</summary>
        static string Evaluate(double diffSeconds)
        {
            if (diffSeconds <= SyncThreshold)  return "sync";
            if (diffSeconds <= GreatThreshold) return "great";
            if (diffSeconds <= GoodThreshold)  return "good";
            if (diffSeconds <= MissThreshold)  return "miss";
            return null;
        }
    }
}
