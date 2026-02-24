using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StellarRhythm.Editor
{
    /// <summary>
    /// ゲームプレイシーンのプロトタイプを自動生成するエディタユーティリティ。
    /// メニュー: StellarRhythm → Build Game Scene
    /// </summary>
    public static class GameSceneBuilder
    {
        // ── レイアウト定数 ──────────────────────────────────────────────
        const int   LaneCount      = 6;
        const float LaneUnitWidth  = 1f;
        const float LaneHalfW     = LaneCount * LaneUnitWidth / 2f;  // 3.0
        const float LaneLength    = 26f;   // 判定ライン(Z=0) → 奥端
        const float LanePreJudge  = 4f;    // 判定ラインより手前に伸ばす距離
        const float JudgeZ        = 0f;
        const float EnemyZ        = 22f;
        const float EnemyY        = 4.5f;

        // ── カラー（白ベースのプロトタイプ）──────────────────────────────
        static readonly Color ColLane    = Color.white;
        static readonly Color ColDivider = new Color(0.65f, 0.65f, 0.65f); // 薄いグレー
        static readonly Color ColJudge   = new Color(0.40f, 0.40f, 0.40f); // 中グレー
        static readonly Color ColEnemy   = new Color(1.00f, 0.20f, 0.88f); // マゼンタ（仮）

        // ── パス ───────────────────────────────────────────────────────
        const string MatDir    = "Assets/Materials/GameScene";
        const string ScenePath = "Assets/Scenes/GameScene.unity";

        // ──────────────────────────────────────────────────────────────

        [MenuItem("StellarRhythm/Build Game Scene")]
        public static void Build()
        {
            EnsureFolder(MatDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // マテリアル生成 & 保存
            var matLane  = SaveMat(MakeUnlit("Mat_LaneFloor",  ColLane));
            var matDiv   = SaveMat(MakeUnlit("Mat_Divider",    ColDivider));
            var matJudge = SaveMat(MakeUnlit("Mat_JudgeLine",  ColJudge));
            var matEnemy = SaveMat(MakeUnlit("Mat_Enemy",      ColEnemy));

            // シーン構築
            var root = new GameObject("GameScene_Root");

            SetupCamera(root.transform);
            SetupLighting(root.transform);
            BuildLane(root.transform, matLane, matDiv, matJudge);
            BuildEnemy(root.transform, matEnemy);

            // フォグ無効
            RenderSettings.fog = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StellarRhythm] GameScene built → {ScenePath}");
        }

        // ── カメラ ─────────────────────────────────────────────────────
        static void SetupCamera(Transform root)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(root);

            go.transform.position = new Vector3(0f, 2.5f, -10f);
            go.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            var cam = go.AddComponent<Camera>();
            cam.fieldOfView     = 60f;
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;  // 白背景
            cam.nearClipPlane   = 0.1f;
            cam.farClipPlane    = 100f;

            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>();
        }

        // ── ライト ─────────────────────────────────────────────────────
        static void SetupLighting(Transform root)
        {
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;

            var go = new GameObject("Directional Light");
            go.transform.SetParent(root);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var l = go.AddComponent<Light>();
            l.type      = LightType.Directional;
            l.intensity = 1f;
            l.color     = Color.white;
        }

        // ── レーン ─────────────────────────────────────────────────────
        static void BuildLane(Transform root, Material matFloor, Material matDiv, Material matJudge)
        {
            var parent = new GameObject("Lane");
            parent.transform.SetParent(root);

            // レーン全長 = 奥方向 + 判定ライン後ろ側の余白
            float totalLen = LaneLength + LanePreJudge;
            float centerZ  = (LaneLength - LanePreJudge) * 0.5f; // = 11

            // 床 (Unity Plane = 10×10。スケールで実寸に合わせる)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "LaneFloor";
            floor.transform.SetParent(parent.transform);
            floor.transform.localPosition = new Vector3(0f, 0f, centerZ);
            floor.transform.localScale    = new Vector3(
                LaneCount * LaneUnitWidth * 0.1f,  // 幅 6 units
                1f,
                totalLen * 0.1f);                   // 奥行き 30 units
            floor.GetComponent<Renderer>().sharedMaterial = matFloor;
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            // レーン区切り線 7本（両端込み）
            for (int i = 0; i <= LaneCount; i++)
            {
                float x = -LaneHalfW + i * LaneUnitWidth;
                var   d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                d.name  = $"Divider_{i}";
                d.transform.SetParent(parent.transform);
                d.transform.localPosition = new Vector3(x, 0.005f, centerZ);
                d.transform.localScale    = new Vector3(0.04f, 0.015f, totalLen);
                d.GetComponent<Renderer>().sharedMaterial = matDiv;
                Object.DestroyImmediate(d.GetComponent<Collider>());
            }

            // 判定ライン（グレーの横バー）
            var j = GameObject.CreatePrimitive(PrimitiveType.Cube);
            j.name = "JudgeLine";
            j.transform.SetParent(parent.transform);
            j.transform.localPosition = new Vector3(0f, 0.015f, JudgeZ);
            j.transform.localScale    = new Vector3(LaneCount * LaneUnitWidth + 0.6f, 0.02f, 0.15f);
            j.GetComponent<Renderer>().sharedMaterial = matJudge;
            Object.DestroyImmediate(j.GetComponent<Collider>());
        }

        // ── 敵キャラ（仮：マゼンタキューブ）─────────────────────────────
        static void BuildEnemy(Transform root, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "EnemyPlaceholder";
            go.transform.SetParent(root);
            go.transform.localPosition = new Vector3(0f, EnemyY, EnemyZ);
            go.transform.localScale    = new Vector3(1.5f, 1.5f, 1.5f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        // ── ユーティリティ ─────────────────────────────────────────────

        static Material MakeUnlit(string name, Color col)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = name };
            mat.color = col;
            return mat;
        }

        static Material SaveMat(Material mat)
        {
            string path = $"{MatDir}/{mat.name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
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
