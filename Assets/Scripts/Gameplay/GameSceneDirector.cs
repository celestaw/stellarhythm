using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using StellarRhythm.Core;

namespace StellarRhythm.Gameplay
{
    /// <summary>
    /// GameScene の進行全体を管理するコンポーネント。
    ///
    /// ■ シーケンス
    ///   1. "Synchronizing Field..."  表示 0.3s → フェード 0.2s
    ///   2. "Complete"               表示 0.2s → フェード 0.1s
    ///   3. 待機 0.5s
    ///   4. プリカウント 1小節（BPM に基づく）
    ///   5. 楽曲 + 譜面スタート（dspTime 同期）
    ///   6. 最終ノーツ通過を待つ
    ///   7. 2s 待機 → ResultScene へ遷移
    ///
    /// ■ dspTime 同期モデル
    ///   preCountDspStart  : プリカウントの第 1 拍を鳴らす DSP 時刻
    ///   chartDspStart     : 楽曲開始 = preCountDspStart + (1 小節分の秒数)
    ///   ChartLoader.StartChartAt(chartDspStart) でノーツ判定タイマーを渡す
    ///   AudioSource.PlayScheduled(chartDspStart) で楽曲を同時に予約
    /// </summary>
    public class GameSceneDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ChartLoader _chartLoader;
        [SerializeField] AudioSource _musicSource;
        [SerializeField] AudioSource _preCountSource;

        [Header("Overlay UI")]
        [Tooltip("メッセージの表示/フェードに使う CanvasGroup")]
        [SerializeField] CanvasGroup _overlayGroup;
        [SerializeField] Text        _overlayText;

        [Header("Scene Names")]
        [SerializeField] string _resultSceneName     = "ResultScene";
        [SerializeField] string _songSelectSceneName = "SongSelectScene";

        [Header("Pre-count")]
        [Tooltip("4/4 拍子の拍数")]
        [SerializeField] int   _beatsPerMeasure    = 4;
        [Tooltip("第 1 拍のクリック周波数（Hz）。他拍より高くして区別する")]
        [SerializeField] float _accentFrequency    = 1100f;
        [Tooltip("弱拍のクリック周波数（Hz）")]
        [SerializeField] float _weakFrequency      = 880f;
        [Tooltip("クリック音の長さ（秒）")]
        [SerializeField] float _clickDuration      = 0.08f;
        [Tooltip("プリカウント開始前のスケジュール余裕時間（秒）")]
        [SerializeField] double _scheduleBuffer    = 0.15;

        [Header("Fallback (直接テスト用)")]
        [Tooltip("GameContext.SelectedSong が null のとき使うフォールバック曲データ")]
        [SerializeField] SongEntry _fallbackSong;

        // ================================================================
        //  ライフサイクル
        // ================================================================

        InputAction _escAction;

        void Awake()
        {
            _escAction = new InputAction(binding: "<Keyboard>/escape");
            _escAction.performed += _ => ReturnToSongSelect();
            _escAction.Enable();
        }

        void OnDestroy()
        {
            _escAction?.Disable();
            _escAction?.Dispose();
        }

        void ReturnToSongSelect()
        {
            StopAllCoroutines();
            _musicSource.Stop();
            _preCountSource.Stop();
            SceneManager.LoadScene(_songSelectSceneName);
        }

        void Start()
        {
            SongEntry song = GameContext.SelectedSong ?? _fallbackSong;

            if (song == null)
            {
                Debug.LogWarning("[GameSceneDirector] 選曲データがありません。SongSelectScene へ戻ります。");
                SceneManager.LoadScene(_songSelectSceneName);
                return;
            }

            // オーバーレイを初期状態（非表示）にする
            if (_overlayGroup != null)
            {
                _overlayGroup.alpha          = 0f;
                _overlayGroup.blocksRaycasts = false;
            }

            StartCoroutine(GameSequence(song));
        }

        // ================================================================
        //  メインシーケンス
        // ================================================================

        IEnumerator GameSequence(SongEntry song)
        {
            // ── ChartLoader に曲データをセット ──────────────────────────
            _chartLoader.SetChart(song.chartJson);
            _musicSource.clip = song.music;

            float bpm = _chartLoader.Bpm;

            // LookAhead = 1 小節分の秒数 ÷ ハイスピード倍率
            // HiSpeed 5.0 が基準（倍率 1.0）。10.0 で 2 倍速、2.5 で 0.5 倍速。
            double beatDuration    = 60.0 / bpm;
            float  preCountSeconds = (float)(_beatsPerMeasure * beatDuration);
            float  speedFactor     = GameContext.HiSpeed / 5f;
            _chartLoader.LookAheadSeconds = preCountSeconds / speedFactor;

            // ── 1. "Synchronizing Field..." ──────────────────────────────
            yield return ShowMessage("Synchronizing Field...", displayTime: 0.3f, fadeTime: 0.2f);

            // ── 2. "Complete" ────────────────────────────────────────────
            yield return ShowMessage("Complete", displayTime: 0.2f, fadeTime: 0.1f);

            // ── 3. 0.5s 待機 ─────────────────────────────────────────────
            yield return new WaitForSeconds(0.5f);

            // ── 4. プリカウント + 5. 楽曲・譜面スタート ─────────────────
            double preCountDspStart = AudioSettings.dspTime + _scheduleBuffer;
            double chartDspStart    = preCountDspStart + preCountSeconds;

            // 全ビートを含む 1 本のクリック AudioClip を生成してスケジュール
            _preCountSource.clip = GeneratePreCountClip(bpm, _beatsPerMeasure);
            _preCountSource.PlayScheduled(preCountDspStart);

            // 楽曲と譜面を同一 dspTime で予約（音声とノーツが完全同期）
            _musicSource.PlayScheduled(chartDspStart);
            _chartLoader.StartChartAt(chartDspStart);

            // ── 6. 楽曲終了まで待機（曲の長さ vs 最終ノーツ、長い方に合わせる）────
            double musicEndDspTime = _musicSource.clip != null
                ? chartDspStart + _musicSource.clip.length
                : _chartLoader.ChartEndDspTime;
            double endDspTime = Math.Max(_chartLoader.ChartEndDspTime, musicEndDspTime);

            while (AudioSettings.dspTime < endDspTime)
                yield return null;

            // ── 7. 2s 待機 → ResultScene ─────────────────────────────────
            yield return new WaitForSeconds(2f);
            SceneManager.LoadScene(_resultSceneName);
        }

        // ================================================================
        //  UI ヘルパー
        // ================================================================

        IEnumerator ShowMessage(string message, float displayTime, float fadeTime)
        {
            if (_overlayGroup == null || _overlayText == null) yield break;

            _overlayText.text            = message;
            _overlayGroup.alpha          = 1f;
            _overlayGroup.blocksRaycasts = true;

            yield return new WaitForSeconds(displayTime);

            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed             += Time.deltaTime;
                _overlayGroup.alpha  = 1f - Mathf.Clamp01(elapsed / fadeTime);
                yield return null;
            }

            _overlayGroup.alpha          = 0f;
            _overlayGroup.blocksRaycasts = false;
        }

        // ================================================================
        //  プリカウント AudioClip 生成
        // ================================================================

        /// <summary>
        /// 全ビート分のクリック音を 1 本の AudioClip に焼き込んで返す。
        /// PlayScheduled で一度だけスケジュールすれば正確な拍間隔が得られる。
        /// </summary>
        AudioClip GeneratePreCountClip(float bpm, int beats)
        {
            int    sampleRate   = AudioSettings.outputSampleRate;
            double beatDuration = 60.0 / bpm;
            int    totalSamples = Mathf.Max(1, (int)(beats * beatDuration * sampleRate));
            int    clickSamples = Mathf.Max(1, (int)(_clickDuration * sampleRate));

            float[] data = new float[totalSamples];

            for (int beat = 0; beat < beats; beat++)
            {
                int    offset    = (int)(beat * beatDuration * sampleRate);
                float  freq      = beat == 0 ? _accentFrequency : _weakFrequency;
                int    available = Mathf.Min(clickSamples, totalSamples - offset);

                for (int i = 0; i < available; i++)
                {
                    float t   = (float)i / sampleRate;
                    float env = Mathf.Exp(-t * 40f);          // 鋭い立ち上がりと素早い減衰
                    data[offset + i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
                }
            }

            var clip = AudioClip.Create("PreCount", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
