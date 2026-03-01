using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StellarRhythm.Gameplay
{
    /// <summary>
    /// 各レーンに対応する判定テキストを画面下部に 0.1 秒表示し、
    /// その後フェードアウトさせる UI コンポーネント。
    ///
    /// ■ セットアップ
    ///   JudgeSystemBuilder エディタメニュー（StellarRhythm/Setup Judge System）で
    ///   Canvas と Text 6 個が自動生成される。
    ///   レーンごとの表示位置は Start() でカメラ投影により自動計算される。
    /// </summary>
    public class JudgmentDisplay : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────────
        [Tooltip("レーン 0–5 に対応する Text（JudgeSystemBuilder で自動設定）")]
        [SerializeField] Text[] _laneTexts = new Text[6];

        // ─── 判定カラー ──────────────────────────────────────────────────
        static readonly Color SyncColor  = new Color(1.00f, 0.84f, 0.00f); // ゴールド
        static readonly Color GreatColor = new Color(0.00f, 1.00f, 1.00f); // シアン
        static readonly Color GoodColor  = new Color(0.56f, 0.93f, 0.56f); // ライトグリーン
        static readonly Color MissColor  = new Color(0.55f, 0.55f, 0.55f); // グレー

        // ─── 表示タイミング ──────────────────────────────────────────────
        const float DisplaySeconds = 0.1f;
        const float FadeSeconds    = 0.15f;

        // ─── ランタイム状態 ──────────────────────────────────────────────
        Coroutine[] _fadeRoutines;

        // ================================================================
        //  ライフサイクル
        // ================================================================

        void Awake()
        {
            _fadeRoutines = new Coroutine[_laneTexts.Length];

            // 全テキストを不可視で初期化
            foreach (var t in _laneTexts)
            {
                if (t == null) continue;
                Color c = t.color;
                c.a     = 0f;
                t.color = c;
            }
        }

        void Start()
        {
            PositionTextsOnScreen();
        }

        // ================================================================
        //  公開 API
        // ================================================================

        /// <summary>
        /// 指定レーンに判定文字列を表示する。
        /// 既に表示中の場合は上書きして再スタートする。
        /// </summary>
        public void ShowJudgment(int lane, string judgment)
        {
            if (lane < 0 || lane >= _laneTexts.Length) return;
            if (_laneTexts[lane] == null) return;

            if (_fadeRoutines[lane] != null)
                StopCoroutine(_fadeRoutines[lane]);

            _fadeRoutines[lane] = StartCoroutine(ShowAndFade(lane, judgment));
        }

        // ================================================================
        //  内部コルーチン
        // ================================================================

        IEnumerator ShowAndFade(int lane, string judgment)
        {
            Text  t     = _laneTexts[lane];
            Color color = ColorFor(judgment);
            color.a     = 1f;

            t.text  = judgment.ToUpper();
            t.color = color;

            // 0.1 秒間フル表示
            yield return new WaitForSeconds(DisplaySeconds);

            // フェードアウト
            float elapsed = 0f;
            while (elapsed < FadeSeconds)
            {
                elapsed += Time.deltaTime;
                color.a  = Mathf.Lerp(1f, 0f, elapsed / FadeSeconds);
                t.color  = color;
                yield return null;
            }

            color.a = 0f;
            t.color = color;
            _fadeRoutines[lane] = null;
        }

        // ================================================================
        //  ヘルパー
        // ================================================================

        /// <summary>カメラ投影でレーン中心をスクリーン座標に変換してテキストを配置。</summary>
        void PositionTextsOnScreen()
        {
            Camera cam = Camera.main;

            for (int lane = 0; lane < _laneTexts.Length; lane++)
            {
                if (_laneTexts[lane] == null) continue;

                if (cam != null)
                {
                    // レーン中心 X = lane - 2.5（world space）、判定ライン Z=0 の位置を投影
                    float   worldX    = lane - 2.5f;
                    Vector3 worldPos  = new Vector3(worldX, 0f, 0f);
                    Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                    // 判定ラインより少し上に文字を出す（+50 px）
                    _laneTexts[lane].rectTransform.position =
                        new Vector3(screenPos.x, screenPos.y + 50f, 0f);
                }
                else
                {
                    // カメラが取得できない場合は均等配置（フォールバック）
                    float normalizedX = (lane + 0.5f) / _laneTexts.Length;
                    _laneTexts[lane].rectTransform.anchorMin = new Vector2(normalizedX, 0.2f);
                    _laneTexts[lane].rectTransform.anchorMax = new Vector2(normalizedX, 0.2f);
                    _laneTexts[lane].rectTransform.anchoredPosition = Vector2.zero;
                }
            }
        }

        static Color ColorFor(string judgment) => judgment switch
        {
            "sync"  => SyncColor,
            "great" => GreatColor,
            "good"  => GoodColor,
            _       => MissColor,
        };
    }
}
