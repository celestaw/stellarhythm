using System;
using UnityEngine;
using UnityEngine.UI;

namespace StellarRhythm.UI
{
    /// <summary>
    /// 選曲後に画面中央へ表示するハイスピード調整パネル。
    /// SpeedSelectPanel.Create() で Canvas 上に動的生成する。
    ///
    /// ■ レイアウト
    ///   上部   : "スピード調整" ラベル
    ///   中段   : [◀ -1.0] [◀ -0.1]  [5.0]  [▶ +0.1] [▶ +1.0]
    ///   下部   : "決定" ボタン
    ///
    /// ■ スピードの意味
    ///   5.0 = 基準速度（LookAhead 1 小節）
    ///   10.0 = 2 倍速（LookAhead 半分）、2.5 = 0.5 倍速（LookAhead 2 倍）
    ///   範囲 : 0.5 – 15.0（小数点 1 桁）
    /// </summary>
    public class SpeedSelectPanel : MonoBehaviour
    {
        const float MinSpeed = 0.5f;
        const float MaxSpeed = 15.0f;

        float         _currentSpeed;
        Action<float> _onConfirmed;
        Text          _speedValueText;

        // ================================================================
        //  公開 Factory
        // ================================================================

        /// <summary>
        /// Canvas 直下にオーバーレイ＋パネルを生成して返す。
        /// onConfirmed にハイスピード値が渡ったらパネルは自動破棄される。
        /// </summary>
        public static SpeedSelectPanel Create(
            Transform canvasTransform, float initialSpeed, Action<float> onConfirmed)
        {
            Font font = GetFont();

            // ── フルスクリーン背景（クリック貫通防止）──────────────────
            var overlayGo = new GameObject("SpeedSelectOverlay");
            overlayGo.transform.SetParent(canvasTransform, false);
            var overlayRt = overlayGo.AddComponent<RectTransform>();
            overlayRt.anchorMin        = Vector2.zero;
            overlayRt.anchorMax        = Vector2.one;
            overlayRt.sizeDelta        = Vector2.zero;
            overlayRt.anchoredPosition = Vector2.zero;
            var overlayImg = overlayGo.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.6f);

            // ── パネル本体 ────────────────────────────────────────────────
            var panelGo = new GameObject("SpeedPanel");
            panelGo.transform.SetParent(overlayGo.transform, false);

            var panel = panelGo.AddComponent<SpeedSelectPanel>();
            panel._currentSpeed = Mathf.Clamp(initialSpeed, MinSpeed, MaxSpeed);
            panel._onConfirmed  = onConfirmed;

            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin        = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot            = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta        = new Vector2(620f, 260f);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.15f, 0.95f);

            // ── タイトル ──────────────────────────────────────────────────
            var titleRt = AddText(panelGo.transform, "Title", "スピード調整", 26, font)
                .GetComponent<RectTransform>();
            titleRt.anchorMin        = new Vector2(0f, 1f);
            titleRt.anchorMax        = new Vector2(1f, 1f);
            titleRt.pivot            = new Vector2(0.5f, 1f);
            titleRt.sizeDelta        = new Vector2(0f, 48f);
            titleRt.anchoredPosition = new Vector2(0f, -12f);

            // ── 操作行（手動配置）──────────────────────────────────────
            // 要素幅: ボタン×4(90px) + 数値(140px)、間隔 12px → 合計 548px
            // 中央(x=0)が数値表示。各 center x: -229, -127, 0, +127, +229
            const float rowY    = 12f;  // パネル中心より少し上
            const float btnSize = 90f;
            const float valW    = 140f;

            // ◀ -1.0
            AddDeltaButton(panelGo.transform, panel, font,
                topLine: "◀", bottomLine: "-1.0",
                pos: new Vector2(-229f, rowY), size: new Vector2(btnSize, btnSize),
                delta: -1.0f);

            // ◀ -0.1
            AddDeltaButton(panelGo.transform, panel, font,
                topLine: "◀", bottomLine: "-0.1",
                pos: new Vector2(-127f, rowY), size: new Vector2(btnSize, btnSize),
                delta: -0.1f);

            // 数値表示（中央）
            var valGo = new GameObject("SpeedValue");
            valGo.transform.SetParent(panelGo.transform, false);
            var valRt = valGo.AddComponent<RectTransform>();
            valRt.anchorMin        = valRt.anchorMax = new Vector2(0.5f, 0.5f);
            valRt.pivot            = new Vector2(0.5f, 0.5f);
            valRt.sizeDelta        = new Vector2(valW, btnSize);
            valRt.anchoredPosition = new Vector2(0f, rowY);
            var valText = valGo.AddComponent<Text>();
            valText.text                = FormatSpeed(panel._currentSpeed);
            valText.font                = font;
            valText.fontSize            = 48;
            valText.fontStyle           = FontStyle.Bold;
            valText.alignment           = TextAnchor.MiddleCenter;
            valText.color               = Color.white;
            valText.resizeTextForBestFit = true;
            valText.resizeTextMinSize   = 20;
            valText.resizeTextMaxSize   = 48;
            panel._speedValueText = valText;

            // ▶ +0.1
            AddDeltaButton(panelGo.transform, panel, font,
                topLine: "▶", bottomLine: "+0.1",
                pos: new Vector2(127f, rowY), size: new Vector2(btnSize, btnSize),
                delta: 0.1f);

            // ▶ +1.0
            AddDeltaButton(panelGo.transform, panel, font,
                topLine: "▶", bottomLine: "+1.0",
                pos: new Vector2(229f, rowY), size: new Vector2(btnSize, btnSize),
                delta: 1.0f);

            // ── 決定ボタン ────────────────────────────────────────────────
            var confirmGo = new GameObject("Confirm");
            confirmGo.transform.SetParent(panelGo.transform, false);
            var confirmRt = confirmGo.AddComponent<RectTransform>();
            confirmRt.anchorMin        = confirmRt.anchorMax = new Vector2(0.5f, 0f);
            confirmRt.pivot            = new Vector2(0.5f, 0f);
            confirmRt.sizeDelta        = new Vector2(160f, 48f);
            confirmRt.anchoredPosition = new Vector2(0f, 18f);
            var confirmImg = confirmGo.AddComponent<Image>();
            confirmImg.color = new Color(0.15f, 0.40f, 0.15f, 1f);
            var confirmBtn = confirmGo.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            var ccb = confirmBtn.colors;
            ccb.normalColor      = new Color(0.15f, 0.40f, 0.15f, 1f);
            ccb.highlightedColor = new Color(0.25f, 0.60f, 0.25f, 1f);
            ccb.pressedColor     = new Color(0.08f, 0.22f, 0.08f, 1f);
            confirmBtn.colors = ccb;
            confirmBtn.onClick.AddListener(() => panel.OnConfirm());
            AddText(confirmGo.transform, "Label", "決定", 24, font, anchorFill: true);

            return panel;
        }

        // ================================================================
        //  ロジック
        // ================================================================

        void AdjustSpeed(float delta)
        {
            // 浮動小数点誤差を防ぐため 10 倍して丸めてから戻す
            float raw = _currentSpeed + delta;
            _currentSpeed = Mathf.Clamp(
                Mathf.Round(raw * 10f) / 10f,
                MinSpeed, MaxSpeed);
            _speedValueText.text = FormatSpeed(_currentSpeed);
        }

        void OnConfirm()
        {
            _onConfirmed?.Invoke(_currentSpeed);
            // SpeedSelectOverlay（親）ごと破棄してパネルを閉じる
            Destroy(transform.parent.gameObject);
        }

        static string FormatSpeed(float v) => v.ToString("F1");

        // ================================================================
        //  UI 生成ヘルパー（static）
        // ================================================================

        static void AddDeltaButton(
            Transform parent, SpeedSelectPanel panel, Font font,
            string topLine, string bottomLine,
            Vector2 pos, Vector2 size, float delta)
        {
            var go = new GameObject("DeltaBtn");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = size;
            rt.anchoredPosition = pos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.30f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor      = new Color(0.15f, 0.15f, 0.30f, 1f);
            cb.highlightedColor = new Color(0.28f, 0.28f, 0.55f, 1f);
            cb.pressedColor     = new Color(0.07f, 0.07f, 0.15f, 1f);
            btn.colors = cb;

            float captured = delta;
            btn.onClick.AddListener(() => panel.AdjustSpeed(captured));

            // ボタン内テキスト（三角形 + 数値の 2 行）
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin        = Vector2.zero;
            labelRt.anchorMax        = Vector2.one;
            labelRt.sizeDelta        = Vector2.zero;
            labelRt.anchoredPosition = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            text.text      = $"{topLine}\n{bottomLine}";
            text.font      = font;
            text.fontSize  = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color     = Color.white;
        }

        /// <summary>
        /// Text GameObject を追加して返す。anchorFill=true で親全体に引き伸ばす。
        /// </summary>
        static Text AddText(
            Transform parent, string name, string content,
            int fontSize, Font font, bool anchorFill = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            if (anchorFill)
            {
                rt.anchorMin        = Vector2.zero;
                rt.anchorMax        = Vector2.one;
                rt.sizeDelta        = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }
            var text = go.AddComponent<Text>();
            text.text      = content;
            text.font      = font;
            text.fontSize  = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color     = Color.white;
            return text;
        }

        static Font GetFont()
        {
            // Unity 6 組み込みフォント（フォールバックあり）
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
