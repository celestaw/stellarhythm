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

        // ── ランタイム状態 ───────────────────────────────────────────────
        NoteData _data;
        double   _hitDspTime;
        double   _lookAhead;
        Vector3  _spawnPos;
        Vector3  _hitPos;
        float    _noteZLength;
        bool     _initialized;

        // ================================================================
        //  初期化
        // ================================================================

        /// <summary>ChartLoader がスポーン直後に一度だけ呼ぶ。</summary>
        public void Initialize(double hitDspTime, NoteData data, float lookAheadSeconds)
        {
            _hitDspTime  = hitDspTime;
            _data        = data;
            _lookAhead   = lookAheadSeconds;
            _initialized = true;

            _spawnPos = transform.position;
            _hitPos   = _spawnPos + _travelDirection.normalized * _travelDistance;

            SetupVisual(data, lookAheadSeconds);
        }

        // ================================================================
        //  毎フレーム更新
        // ================================================================

        void Update()
        {
            if (!_initialized) return;

            double remaining = _hitDspTime - AudioSettings.dspTime;

            // 進捗 t : 0 = スポーン地点、1 = ヒットライン（クランプしないので通過後も継続）
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
