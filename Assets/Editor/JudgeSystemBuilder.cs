using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using StellarRhythm.Gameplay;

namespace StellarRhythm.Editor
{
    /// <summary>
    /// 判定システム（JudgeManager + JudgmentDisplay + Canvas）を
    /// 現在開いているシーンに自動セットアップするエディタユーティリティ。
    ///
    /// メニュー: StellarRhythm → Setup Judge System
    /// </summary>
    public static class JudgeSystemBuilder
    {
        const int   LaneCount   = 6;
        const float FontSizePt  = 52f;

        [MenuItem("StellarRhythm/Setup Judge System")]
        public static void SetupJudgeSystem()
        {
            // ── Canvas ──────────────────────────────────────────────────
            var canvasGO = new GameObject("JudgmentCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Setup Judge System");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Text × 6 ────────────────────────────────────────────────
            var texts = new Text[LaneCount];
            Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < LaneCount; i++)
            {
                var textGO = new GameObject($"JudgmentText_Lane{i}");
                textGO.transform.SetParent(canvasGO.transform, false);

                var text             = textGO.AddComponent<Text>();
                text.font            = builtinFont;
                text.fontSize        = (int)FontSizePt;
                text.fontStyle       = FontStyle.Bold;
                text.alignment       = TextAnchor.MiddleCenter;
                text.color           = new Color(1f, 1f, 1f, 0f);   // 初期は透明
                text.text            = "";
                text.raycastTarget   = false;

                // テキスト領域：横 170 × 縦 80（画面内に収まるサイズ）
                var rt          = textGO.GetComponent<RectTransform>();
                rt.sizeDelta    = new Vector2(170f, 80f);
                rt.anchorMin    = Vector2.zero;
                rt.anchorMax    = Vector2.zero;
                rt.pivot        = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                texts[i] = text;
            }

            // ── JudgmentDisplay ──────────────────────────────────────────
            var display = canvasGO.AddComponent<JudgmentDisplay>();
            var displaySO = new SerializedObject(display);
            var textsProp = displaySO.FindProperty("_laneTexts");
            textsProp.arraySize = LaneCount;
            for (int i = 0; i < LaneCount; i++)
                textsProp.GetArrayElementAtIndex(i).objectReferenceValue = texts[i];
            displaySO.ApplyModifiedPropertiesWithoutUndo();

            // ── JudgeManager ────────────────────────────────────────────
            var managerGO = new GameObject("JudgeManager");
            Undo.RegisterCreatedObjectUndo(managerGO, "Setup Judge System");

            var manager   = managerGO.AddComponent<JudgeManager>();
            var managerSO = new SerializedObject(manager);
            managerSO.FindProperty("_display").objectReferenceValue = display;
            managerSO.ApplyModifiedPropertiesWithoutUndo();

            // ── 完了メッセージ ────────────────────────────────────────────
            Selection.activeGameObject = managerGO;
            Debug.Log("[JudgeSystemBuilder] Setup complete.\n" +
                      "  JudgmentCanvas  — Canvas (ScreenSpace Overlay) + JudgmentDisplay\n" +
                      "  JudgeManager    — W/E/R/U/I/O → Lane 0–5\n" +
                      "レーンごとのテキスト位置は Play 開始時にカメラ投影で自動決定されます。");
        }
    }
}
