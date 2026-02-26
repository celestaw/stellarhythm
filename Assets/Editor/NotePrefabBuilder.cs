using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StellarRhythm.Gameplay;

namespace StellarRhythm.Editor
{
    /// <summary>
    /// ノーツ用マテリアルとプレファブを自動生成し、
    /// GameScene の ChartLoader へ自動アサインするエディタユーティリティ。
    ///
    /// メニュー: StellarRhythm → 5. Build Note Prefabs
    ///
    /// ■ 生成されるアセット
    ///   Assets/Materials/Notes/
    ///     Mat_StepNote.mat   (白・Unlit)
    ///     Mat_KillerNote.mat (赤・Unlit)
    ///   Assets/Prefabs/Notes/
    ///     StepTapNote.prefab    StepHoldNote.prefab
    ///     KillerTapNote.prefab  KillerHoldNote.prefab
    ///
    /// ■ プレファブ構造
    ///   [Root]          NoteView コンポーネント（移動 / 寿命管理）
    ///   └── [Visual]    Cube メッシュ（MeshRenderer のみ。Collider は削除）
    ///                   localScale = (0.85, 0.05, 1)  ← Z は NoteView が実行時に設定
    ///                   NoteView.SetupVisual() が localScale.z / localPosition.z を動的に設定：
    ///                     タップ  : Z = _tapLength  (≈ 0.12、縦横比 1:7)
    ///                     ホールド: Z = duration × speed
    /// </summary>
    public static class NotePrefabBuilder
    {
        const string MatDir    = "Assets/Materials/Notes";
        const string PrefabDir = "Assets/Prefabs/Notes";
        const string ScenePath = "Assets/Scenes/GameScene.unity";

        // ノーツの幅・厚み（ワールド単位）
        const float NoteWidth     = 0.85f;  // レーン幅 1.0 より少し小さい
        const float NoteThickness = 0.05f;  // 地面からの厚み（見た目用）
        const float TapLength     = 0.12f;  // 縦横比 1:7 → 0.85/7 ≈ 0.12

        // ================================================================

        [MenuItem("StellarRhythm/5. Build Note Prefabs")]
        public static void Build()
        {
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);

            // ── マテリアル生成 ─────────────────────────────────────────
            var stepMat   = CreateMat("Mat_StepNote",   Color.black);
            var killerMat = CreateMat("Mat_KillerNote", new Color(1f, 0.15f, 0.15f));

            // ── プレファブ生成 ─────────────────────────────────────────
            var stepTap    = BuildPrefab("StepTapNote",    stepMat);
            var stepHold   = BuildPrefab("StepHoldNote",   stepMat);
            var killerTap  = BuildPrefab("KillerTapNote",  killerMat);
            var killerHold = BuildPrefab("KillerHoldNote", killerMat);

            // ── GameScene の ChartLoader へアサイン ────────────────────
            AssignToChartLoader(stepTap, stepHold, killerTap, killerHold);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NotePrefabBuilder] ノーツプレファブ生成完了。");
        }

        // ================================================================
        //  プレファブ生成
        // ================================================================

        static GameObject BuildPrefab(string prefabName, Material mat)
        {
            string path = $"{PrefabDir}/{prefabName}.prefab";

            // ── ルート ──────────────────────────────────────────────────
            var root = new GameObject(prefabName);
            var nv   = root.AddComponent<NoteView>();

            // NoteView のデフォルト値をプレファブに焼き込む
            var so = new SerializedObject(nv);
            so.FindProperty("_travelDirection").vector3Value = Vector3.back;
            so.FindProperty("_travelDistance").floatValue    = 26f;
            so.FindProperty("_tapLength").floatValue         = TapLength;
            so.FindProperty("_destroyBelowZ").floatValue     = -4f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── Visual 子オブジェクト ─────────────────────────────────
            // Cube を生成し、Collider を削除して見た目専用にする
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // X=幅, Y=厚み, Z=1（NoteView.SetupVisual が実行時に上書き）
            visual.transform.localScale    = new Vector3(NoteWidth, NoteThickness, 1f);
            visual.transform.localPosition = Vector3.zero;

            // ── プレファブとして保存 ───────────────────────────────────
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log($"[NotePrefabBuilder] {path} 生成");
            return prefab;
        }

        // ================================================================
        //  ChartLoader へのアサイン
        // ================================================================

        static void AssignToChartLoader(
            GameObject stepTap, GameObject stepHold,
            GameObject killerTap, GameObject killerHold)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cl    = Object.FindFirstObjectByType<ChartLoader>();

            if (cl == null)
            {
                Debug.LogWarning("[NotePrefabBuilder] GameScene に ChartLoader が見つかりません。" +
                                 "先に 'StellarRhythm → 2. Add Game Scene UI' を実行してください。");
                return;
            }

            var so = new SerializedObject(cl);
            so.FindProperty("_stepTapPrefab").objectReferenceValue    = stepTap;
            so.FindProperty("_stepHoldPrefab").objectReferenceValue   = stepHold;
            so.FindProperty("_stepAerialPrefab").objectReferenceValue = stepTap;   // 暫定で StepTap を流用
            so.FindProperty("_killerTapPrefab").objectReferenceValue  = killerTap;
            so.FindProperty("_killerHoldPrefab").objectReferenceValue = killerHold;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NotePrefabBuilder] ChartLoader にプレファブをアサインしました。");
        }

        // ================================================================
        //  ユーティリティ
        // ================================================================

        static Material CreateMat(string name, Color color)
        {
            string path   = $"{MatDir}/{name}.mat";
            var    shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static void EnsureFolder(string assetPath)
        {
            string[] parts   = assetPath.Split('/');
            string   current = parts[0];
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
