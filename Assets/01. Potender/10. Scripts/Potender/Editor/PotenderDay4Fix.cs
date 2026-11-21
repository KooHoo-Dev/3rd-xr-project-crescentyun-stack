using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Potender.EditorTools
{
    /// <summary>
    /// Day 4 빌드 피드백. 메뉴 Potender ▸ Day4 Fix (빌드 피드백) — 여러 번 실행해도 됨
    ///  0) 고블린 제한 시간 35 → 55초
    ///  1) HUD를 메뉴판 반대쪽으로 (메뉴판 오른쪽 판과 겹침 해결)
    ///  2) 서빙 트레이의 "서빙" 글자 숨김 (주황 원판만)
    ///  3) 병 라벨 폭을 병 간격보다 좁게 + 줄바꿈 → 이름끼리 안 붙음
    ///  4) 별점 기준 20 / 50 / 90 → 20 / 35 / 50
    /// </summary>
    public static class PotenderDay4Fix
    {
        const string ScenePath = "Assets/01. Potender/01. Scenes/test/Main.unity";
        const float RaidTime = 55f;
        static readonly int[] Stars = { 20, 35, 50 };

        static readonly List<string> log = new();
        static void Log(string s) => log.Add("• " + s);

        [MenuItem("Potender/Day4 Fix (빌드 피드백)")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            log.Clear();

            var scene = EditorSceneManager.OpenScene(ScenePath);
            Physics.SyncTransforms();
            RaidTimeLimit();
            MoveHud();
            HideTrayLabel();
            BottleLabels();
            StarThresholds();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string msg = string.Join("\n", log);
            Debug.Log("[Potender Day4 Fix] 완료\n" + msg);
            EditorUtility.DisplayDialog("Potender Day4 Fix", "완료! 자세한 내용은 Console\n\n" + msg, "OK");
        }

        // ───────── 0) 고블린 시간 ─────────
        static void RaidTimeLimit()
        {
            var raid = Object.FindFirstObjectByType<RaidEvent>(FindObjectsInactive.Include);
            if (!raid) { Log("⚠ RaidEvent 없음 — 먼저 Day4 강도 세팅 실행"); return; }
            var so = new SerializedObject(raid);
            var p = so.FindProperty("timeLimit");
            float before = p.floatValue;
            if (before < RaidTime) p.floatValue = RaidTime;
            if (!so.FindProperty("goblinPrefab").objectReferenceValue)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/01. Potender/Prefabs/Goblin.prefab");
                if (go) { so.FindProperty("goblinPrefab").objectReferenceValue = go.GetComponent<Goblin>(); Log("RaidEvent goblinPrefab 연결"); }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Log($"고블린 제한 시간: {before:0}초 → {p.floatValue:0}초");
        }

        // ───────── 1) HUD 위치 ─────────
        static void MoveHud()
        {
            var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
            var hud = flow ? new SerializedObject(flow).FindProperty("hudPanel").objectReferenceValue as GameObject : null;
            var rt = hud ? hud.transform as RectTransform : null;
            var board = Object.FindFirstObjectByType<MenuBoard>(FindObjectsInactive.Include);
            if (!rt || !rt.parent) { Log("⚠ HudPanel 못 찾음"); return; }

            // 메뉴판(두 판의 중심)이 HUD 캔버스 기준 어느 쪽에 있는지 → 반대쪽으로
            float boardSide = -1f;
            if (board)
            {
                var canvases = board.GetComponentsInChildren<Canvas>(true);
                Vector3 c = canvases.Length > 0
                    ? canvases.Aggregate(Vector3.zero, (a, cv) => a + cv.transform.position) / canvases.Length
                    : board.transform.position;
                boardSide = Mathf.Sign(rt.parent.InverseTransformPoint(c).x);
            }

            var pos = rt.anchoredPosition;
            float x = Mathf.Abs(pos.x) < 100f ? 650f : Mathf.Abs(pos.x);
            float newX = -boardSide * x;
            if (Mathf.Approximately(newX, pos.x)) { Log("HUD: 이미 메뉴판 반대쪽"); return; }
            rt.anchoredPosition = new Vector2(newX, pos.y);
            EditorUtility.SetDirty(rt);
            Log($"HUD: 메뉴판 반대쪽으로 이동 (x {pos.x:0} → {newX:0})");
        }

        // ───────── 2) "서빙" 글자 ─────────
        static void HideTrayLabel()
        {
            int n = 0;
            foreach (var spot in Object.FindObjectsByType<CounterSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var t in spot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.gameObject.name != "TrayLabel" && t.text != "서빙") continue;
                if (!t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(false);
                EditorUtility.SetDirty(t.gameObject);
                n++;
            }
            Log(n > 0 ? $"서빙 트레이 글자 숨김 ({n}개) — 주황 원판만 남김" : "서빙 트레이 글자: 이미 숨겨져 있음");
        }

        // ───────── 3) 병 라벨 ─────────
        static void BottleLabels()
        {
            var bottles = Object.FindObjectsByType<IngredientBottle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bottles.Length == 0) { Log("⚠ 병 없음"); return; }

            // 가장 가까운 병 사이 간격
            float gap = 0.3f;
            foreach (var a in bottles)
            foreach (var b in bottles)
                if (a != b) gap = Mathf.Min(gap, Vector3.Distance(Flat(a.transform.position), Flat(b.transform.position)));
            float width = Mathf.Clamp(gap * 0.8f, 0.08f, 0.2f);

            int n = 0;
            foreach (var bottle in bottles)
            {
                var label = new SerializedObject(bottle).FindProperty("label").objectReferenceValue as TMP_Text;
                if (!label) continue;
                var rt = label.rectTransform;
                rt.sizeDelta = new Vector2(width, 0.09f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.05f;
                label.fontSizeMax = 0.35f;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.horizontalAlignment = HorizontalAlignmentOptions.Center;
                PrefabUtility.RecordPrefabInstancePropertyModifications(rt);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                EditorUtility.SetDirty(label);
                n++;
            }
            Log($"병 라벨 {n}개: 폭 {width * 100f:0}cm (병 간격 {gap * 100f:0}cm의 80%), 줄바꿈 켬");
        }

        // ───────── 4) 별점 ─────────
        static void StarThresholds()
        {
            var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
            if (!flow) { Log("⚠ GameFlow 없음"); return; }
            var so = new SerializedObject(flow);
            var p = so.FindProperty("starThresholds");
            p.arraySize = Stars.Length;
            for (int i = 0; i < Stars.Length; i++) p.GetArrayElementAtIndex(i).intValue = Stars[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Log($"별점 기준: {string.Join(" / ", Stars)}");
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
