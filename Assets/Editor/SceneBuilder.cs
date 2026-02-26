using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using StellarRhythm.Core;
using StellarRhythm.Gameplay;
using StellarRhythm.UI;

namespace StellarRhythm.Editor
{
    /// <summary>
    /// 3 シーンを自動構築するエディタユーティリティ。
    ///
    /// 実行順序:
    ///   1. StellarRhythm → 1. Build SongSelect Scene
    ///   2. StellarRhythm → 2. Add Game Scene UI
    ///   3. StellarRhythm → 3. Build Result Scene
    ///   4. StellarRhythm → 4. Update Build Settings
    /// </summary>
    public static class SceneBuilder
    {
        // ── パス定数 ────────────────────────────────────────────────────
        const string SongSelectPath = "Assets/Scenes/SongSelectScene.unity";
        const string GameScenePath  = "Assets/Scenes/GameScene.unity";
        const string ResultPath     = "Assets/Scenes/ResultScene.unity";
        const string SongDataDir    = "Assets/SongData";
        const string AudioPath      = "Assets/Refernces/canon_sample.mp3";
        const string ChartJsonPath  = "Assets/ChartData/test_chart.json";

        // GameScene のレーン定数（GameSceneBuilder と一致させること）
        const int   LaneCount     = 6;
        const float LaneUnitWidth = 1f;
        const float LaneHalfW    = LaneCount * LaneUnitWidth / 2f; // 3.0
        const float LaneLength   = 26f;   // スポーン Z 座標
        const float SpawnY       = 0.1f;  // 床上の高さ

        // ── 1. SongSelectScene ──────────────────────────────────────────

        [MenuItem("StellarRhythm/1. Build SongSelect Scene")]
        public static void BuildSongSelectScene()
        {
            EnsureFolder(SongDataDir);

            // SongEntry ScriptableObject を生成
            var songEntry = CreateOrLoad<SongEntry>($"{SongDataDir}/SongEntry_CanonSample.asset");
            songEntry.title    = "Canon in D";
            songEntry.artist   = "Pachelbel";
            songEntry.music    = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
            songEntry.chartJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ChartJsonPath);
            EditorUtility.SetDirty(songEntry);

            // 新規シーンを作成
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // カメラ
            var cam = CreateGO("Main Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cam.GetComponent<Camera>().backgroundColor = new Color(0.06f, 0.06f, 0.10f);
            cam.AddComponent<AudioListener>();
            cam.AddComponent<UniversalAdditionalCameraData>();

            // Canvas
            var canvas = CreateCanvas("SongSelectCanvas", sortOrder: 0);
            CreateEventSystem();

            // 背景
            var bg = CreateImage(canvas.transform, "Background",
                new Color(0.06f, 0.06f, 0.10f, 1f), Vector2.zero, Vector2.one);

            // タイトルテキスト
            var title = CreateText(canvas.transform, "TitleText", "STELLAR RHYTHM", 56, Color.white);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin        = new Vector2(0.5f, 1f);
            titleRt.anchorMax        = new Vector2(0.5f, 1f);
            titleRt.pivot            = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -80f);
            titleRt.sizeDelta        = new Vector2(900f, 80f);

            // 選曲サブタイトル
            var sub = CreateText(canvas.transform, "SubText", "SELECT SONG", 26,
                new Color(0.6f, 0.6f, 0.6f));
            var subRt = sub.GetComponent<RectTransform>();
            subRt.anchorMin        = new Vector2(0.5f, 1f);
            subRt.anchorMax        = new Vector2(0.5f, 1f);
            subRt.pivot            = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -175f);
            subRt.sizeDelta        = new Vector2(600f, 50f);

            // 曲ボタン（1 曲ぶん）
            var (btn, _) = CreateButton(canvas.transform, "CanonButton",
                "CANON IN D  /  Pachelbel",
                new Color(0.15f, 0.15f, 0.22f),
                new Vector2(600f, 90f),
                new Vector2(0f, 0f));

            // SongSelectDirector をセットアップ
            var directorGO = CreateGO("SongSelectDirector", canvas.transform);
            var director   = directorGO.AddComponent<SongSelectDirector>();

            SerializedObject so = new SerializedObject(director);
            so.FindProperty("_songs").arraySize = 1;
            so.FindProperty("_songs").GetArrayElementAtIndex(0).objectReferenceValue = songEntry;
            so.FindProperty("_songButtons").arraySize = 1;
            so.FindProperty("_songButtons").GetArrayElementAtIndex(0).objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SongSelectPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] SongSelectScene → {SongSelectPath}");
        }

        // ── 2. GameScene UI 追加 ────────────────────────────────────────

        [MenuItem("StellarRhythm/2. Add Game Scene UI")]
        public static void AddGameSceneUI()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // 二重追加を防止
            if (GameObject.Find("GameSceneCanvas") != null)
            {
                Debug.LogWarning("[SceneBuilder] GameSceneCanvas が既に存在します。スキップ。");
                return;
            }

            // ── オーバーレイ Canvas ────────────────────────────────────
            var canvas = CreateCanvas("GameSceneCanvas", sortOrder: 10);
            CreateEventSystem();

            // 半透明パネル + CanvasGroup（メッセージ表示用）
            var overlayGO = CreateGO("OverlayGroup", canvas.transform);
            var overlayRt = overlayGO.AddComponent<RectTransform>();
            StretchFull(overlayRt);
            var overlayGroup  = overlayGO.AddComponent<CanvasGroup>();
            overlayGroup.alpha          = 0f;
            overlayGroup.blocksRaycasts = false;
            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.65f);

            // 中央テキスト
            var msgText = CreateText(overlayGO.transform, "CenterText", "", 46, Color.white);
            var msgRt   = msgText.GetComponent<RectTransform>();
            msgRt.anchorMin        = new Vector2(0.5f, 0.5f);
            msgRt.anchorMax        = new Vector2(0.5f, 0.5f);
            msgRt.pivot            = new Vector2(0.5f, 0.5f);
            msgRt.anchoredPosition = Vector2.zero;
            msgRt.sizeDelta        = new Vector2(900f, 80f);
            msgText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // ── ChartLoader ────────────────────────────────────────────
            var clRoot = CreateGO("ChartLoaderRoot");
            var cl     = clRoot.AddComponent<ChartLoader>();

            // スポーンポイントを 6 レーン分作成
            var spawnParent = CreateGO("NoteSpawnPoints", clRoot.transform);
            var spawnPoints = new Transform[LaneCount];
            for (int i = 0; i < LaneCount; i++)
            {
                float x      = -LaneHalfW + (i + 0.5f) * LaneUnitWidth;
                var   sp     = CreateGO($"SpawnPoint_Lane{i}", spawnParent.transform);
                sp.transform.localPosition = new Vector3(x, SpawnY, LaneLength);
                spawnPoints[i]             = sp.transform;
            }

            SerializedObject clSo = new SerializedObject(cl);
            clSo.FindProperty("_autoStart").boolValue = false;
            var arr = clSo.FindProperty("_laneSpawnPoints");
            arr.arraySize = LaneCount;
            for (int i = 0; i < LaneCount; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            clSo.FindProperty("_lookAheadSeconds").floatValue = 3f;
            clSo.ApplyModifiedPropertiesWithoutUndo();

            // ── GameSceneManager ───────────────────────────────────────
            var managerGO = CreateGO("GameSceneManager");
            var gsd       = managerGO.AddComponent<GameSceneDirector>();

            // 音楽用 AudioSource
            var musicSrc = managerGO.AddComponent<AudioSource>();
            musicSrc.playOnAwake = false;

            // プリカウント用 AudioSource
            var preCountSrc = managerGO.AddComponent<AudioSource>();
            preCountSrc.playOnAwake = false;

            // GameSceneDirector の参照をセット
            SerializedObject gsdSo = new SerializedObject(gsd);
            gsdSo.FindProperty("_chartLoader").objectReferenceValue    = cl;
            gsdSo.FindProperty("_musicSource").objectReferenceValue    = musicSrc;
            gsdSo.FindProperty("_preCountSource").objectReferenceValue = preCountSrc;
            gsdSo.FindProperty("_overlayGroup").objectReferenceValue   = overlayGroup;
            gsdSo.FindProperty("_overlayText").objectReferenceValue    = msgText.GetComponent<Text>();

            // Fallback: SongEntry_CanonSample があれば割り当て
            var fallback = AssetDatabase.LoadAssetAtPath<SongEntry>(
                $"{SongDataDir}/SongEntry_CanonSample.asset");
            if (fallback != null)
                gsdSo.FindProperty("_fallbackSong").objectReferenceValue = fallback;

            gsdSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, GameScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] GameScene UI 追加完了 → {GameScenePath}");
        }

        // ── 3. ResultScene ──────────────────────────────────────────────

        [MenuItem("StellarRhythm/3. Build Result Scene")]
        public static void BuildResultScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = CreateGO("Main Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cam.GetComponent<Camera>().backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.AddComponent<AudioListener>();
            cam.AddComponent<UniversalAdditionalCameraData>();

            var canvas = CreateCanvas("ResultCanvas", sortOrder: 0);
            CreateEventSystem();

            // 背景
            CreateImage(canvas.transform, "Background",
                new Color(0.05f, 0.05f, 0.08f, 1f), Vector2.zero, Vector2.one);

            // RESULT テキスト
            var resultText = CreateText(canvas.transform, "ResultText", "RESULT", 72, Color.white);
            var rt = resultText.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 80f);
            rt.sizeDelta        = new Vector2(600f, 100f);
            resultText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // 戻るボタン
            var (btn, _) = CreateButton(canvas.transform, "BackButton",
                "Back to Song Select",
                new Color(0.18f, 0.18f, 0.26f),
                new Vector2(420f, 70f),
                new Vector2(0f, -100f));

            // ResultDirector
            var dirGO = CreateGO("ResultDirector", canvas.transform);
            var dir   = dirGO.AddComponent<ResultDirector>();
            SerializedObject so = new SerializedObject(dir);
            so.FindProperty("_backButton").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ResultPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] ResultScene → {ResultPath}");
        }

        // ── 4. Build Settings 更新 ──────────────────────────────────────

        [MenuItem("StellarRhythm/4. Update Build Settings")]
        public static void UpdateBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(SongSelectPath, true),
                new EditorBuildSettingsScene(GameScenePath,  true),
                new EditorBuildSettingsScene(ResultPath,     true),
            };

            EditorBuildSettings.scenes = scenes;
            Debug.Log("[SceneBuilder] Build Settings 更新完了:\n" +
                      "  [0] SongSelectScene\n  [1] GameScene\n  [2] ResultScene");
        }

        // ================================================================
        //  ユーティリティ
        // ================================================================

        static GameObject CreateGO(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go     = CreateGO(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920, 1080);
            scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight   = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = CreateGO("EventSystem");
            go.AddComponent<EventSystem>();
            // 新しい Input System (activeInputHandler=1) に対応したモジュールを使う
            go.AddComponent<InputSystemUIInputModule>();
        }

        static GameObject CreateImage(Transform parent, string name,
            Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go  = CreateGO(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        static GameObject CreateText(Transform parent, string name,
            string text, int fontSize, Color color)
        {
            var go  = CreateGO(name, parent);
            var txt = go.AddComponent<Text>();
            txt.text      = text;
            txt.fontSize  = fontSize;
            txt.color     = color;
            txt.alignment = TextAnchor.MiddleCenter;

            // Unity 組み込みフォントを使用（フォントアセット不要）
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return go;
        }

        static (Button btn, Text label) CreateButton(Transform parent, string name,
            string labelText, Color bgColor, Vector2 size, Vector2 anchoredPos)
        {
            var go  = CreateGO(name, parent);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = size;
            rt.anchoredPosition = anchoredPos;

            // ボタン色遷移を設定（ホバー/押下で若干明るく）
            var colors = btn.colors;
            colors.normalColor      = bgColor;
            colors.highlightedColor = bgColor * 1.25f;
            colors.pressedColor     = bgColor * 0.8f;
            btn.colors = colors;

            // ラベルテキスト
            var labelGO  = CreateGO("Label", go.transform);
            var labelTxt = labelGO.AddComponent<Text>();
            labelTxt.text      = labelText;
            labelTxt.fontSize  = 28;
            labelTxt.color     = Color.white;
            labelTxt.alignment = TextAnchor.MiddleCenter;
            labelTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var labelRt = labelGO.GetComponent<RectTransform>();
            StretchFull(labelRt);

            return (btn, labelTxt);
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var obj = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        static void EnsureFolder(string assetPath)
        {
            var    parts   = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
