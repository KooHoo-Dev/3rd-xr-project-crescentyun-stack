using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Potender.EditorTools
{
    /// <summary>
    /// 4차 피드백. 메뉴 Potender ▸ Day3 Fix 5 (여러 번 실행해도 됨)
    ///  1) 서빙 트레이를 다시 '손님 바 테이블' 위로 — 플레이어 쪽 모서리 + 감지 범위를 플레이어 쪽으로 길게
    ///     (잔을 바 모서리 위로만 내밀면 테이블 위 트레이로 착 붙음). 손님은 바 건너편 모서리 바로 뒤에 섬
    ///  2) '별벼림' → '별조각' (데이터, 씬 텍스트, 병 이름)
    ///  3) 메뉴판 왼쪽에 같은 크기 판 하나 더 → 레시피를 나눠서 이어서 표시
    /// </summary>
    public static class PotenderDay3Fix5
    {
        const string Root = "Assets/01. Potender";
        const string ScenePath = Root + "/01. Scenes/test/Main.unity";
        const string OldName = "별벼림", NewName = "별조각";

        static readonly List<string> log = new();
        static void Log(string s) => log.Add("• " + s);

        [MenuItem("Potender/Day3 Fix 5 (4차 피드백)")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            log.Clear();

            RenameInData();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Physics.SyncTransforms();
            RenameInScene();
            ServeOnBar();
            SecondMenuPage();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string msg = string.Join("\n", log);
            Debug.Log("[Potender Fix5] 완료\n" + msg);
            EditorUtility.DisplayDialog("Potender Day3 Fix 5", "완료! 자세한 내용은 Console\n\n" + msg, "OK");
        }

        // ───────── 2) 이름 변경 ─────────
        static void RenameInData()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:IngredientData", new[] { Root }))
            {
                var ing = AssetDatabase.LoadAssetAtPath<IngredientData>(AssetDatabase.GUIDToAssetPath(guid));
                if (ing && ing.displayName.Contains(OldName))
                {
                    ing.displayName = ing.displayName.Replace(OldName, NewName);
                    EditorUtility.SetDirty(ing); n++;
                }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:RecipeData", new[] { Root }))
            {
                var r = AssetDatabase.LoadAssetAtPath<RecipeData>(AssetDatabase.GUIDToAssetPath(guid));
                if (!r) continue;
                bool changed = false;
                string Fix(string s) { if (s != null && s.Contains(OldName)) { changed = true; return s.Replace(OldName, NewName); } return s; }
                r.potionName = Fix(r.potionName);
                r.effectDescription = Fix(r.effectDescription);
                for (int i = 0; i < r.orderLines.Count; i++) r.orderLines[i] = Fix(r.orderLines[i]);
                if (changed) { EditorUtility.SetDirty(r); n++; }
            }
            Log($"데이터: '{OldName}' → '{NewName}' ({n}곳)");
        }

        static void RenameInScene()
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.text != null && t.text.Contains(OldName)) { t.text = t.text.Replace(OldName, NewName); EditorUtility.SetDirty(t); n++; }
            foreach (var b in Object.FindObjectsByType<IngredientBottle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.name.Contains("StarForged")) { b.name = "Bottle_Star"; n++; }
            if (n > 0) Log($"씬: 텍스트/병 이름 {n}곳 수정");
        }

        // ───────── 1) 서빙을 손님 바 테이블로 ─────────
        static void ServeOnBar()
        {
            var spot = Object.FindFirstObjectByType<CounterSpot>(FindObjectsInactive.Include);
            var socket = spot ? spot.GetComponentInChildren<XRSocketInteractor>(true) : null;
            var glass = Object.FindFirstObjectByType<ServingGlass>(FindObjectsInactive.Include);
            if (!spot || !socket || !glass) { Log("⚠ CounterSpot / ServeSocket / 잔 못 찾음"); return; }

            Transform st = spot.transform;
            Vector3 toCustomer = Flat(st.forward).normalized;
            float? topN = SurfaceY(st.position);
            if (!topN.HasValue) { Log("⚠ CounterSpot 아래 바 테이블 없음 — CounterSpot을 바 위로 옮긴 뒤 다시 실행"); return; }
            float top = topN.Value;

            float nearEdge = EdgeDistance(st.position, -toCustomer, top);   // 플레이어 쪽 모서리까지
            float farEdge = EdgeDistance(st.position, toCustomer, top);     // 손님 쪽 모서리까지

            var gcols = glass.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
            float pivotOff = gcols.Length > 0 ? glass.transform.position.y - gcols.Min(c => c.bounds.min.y) : 0f;

            // 트레이: 바 상판, 플레이어 쪽 모서리에서 6cm 안쪽
            Vector3 trayPos = Flat(st.position) - toCustomer * Mathf.Max(0f, nearEdge - 0.06f);
            socket.transform.position = trayPos + Vector3.up * (top + pivotOff + 0.002f);
            socket.transform.localRotation = Quaternion.identity;

            // 감지 범위: 트레이에서 플레이어 쪽으로 50cm 길게 (바 모서리 위로 내밀기만 하면 됨)
            foreach (var sc in socket.GetComponents<SphereCollider>()) Object.DestroyImmediate(sc);
            var box = socket.GetComponent<BoxCollider>();
            if (!box) box = socket.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.1f, -0.2f);
            box.size = new Vector3(0.35f, 0.3f, 0.55f);
            EditorUtility.SetDirty(box);
            EditorUtility.SetDirty(socket.transform);

            var mark = socket.transform.Find("TrayMark");
            if (mark) mark.localPosition = new Vector3(0, -pivotOff + 0.002f, 0);
            var label = socket.transform.Find("TrayLabel");
            if (label) label.localPosition = new Vector3(0, -pivotOff + 0.28f, 0);
            Log($"서빙 트레이: 손님 바 테이블 위 (플레이어 쪽 모서리 6cm 안쪽), 감지 범위 플레이어 쪽으로 50cm");

            // 손님: 바 건너편 모서리 바로 뒤
            var standProp = new SerializedObject(spot).FindProperty("standPoint");
            if (standProp.objectReferenceValue is Transform stand)
            {
                Vector3 p = Flat(st.position) + toCustomer * (farEdge + 0.3f);
                stand.position = new Vector3(p.x, stand.position.y, p.z);
                EditorUtility.SetDirty(stand);
                Log("손님 서는 위치: 바 건너편 모서리에서 30cm (트레이와 더 가깝게)");
            }

            // 잔 기본 위치가 감지 범위 안이면 옆으로 비킴 (소켓이 혼자 잔을 집어가는 것 방지)
            Physics.SyncTransforms();
            var zone = box.bounds; zone.Expand(0.1f);
            if (zone.Contains(glass.transform.position))
            {
                float? baseY = SurfaceY(glass.transform.position);
                Vector3 side = Flat(st.right).normalized;
                Vector3 start = glass.transform.position;
                bool moved = false;
                for (float d = 0.05f; d <= 0.6f && !moved; d += 0.05f)
                foreach (var dir in new[] { side, -side })
                {
                    Vector3 c = start + dir * d;
                    var y = SurfaceY(c);
                    if (zone.Contains(c) || !y.HasValue || !baseY.HasValue || Mathf.Abs(y.Value - baseY.Value) > 0.015f) continue;
                    glass.transform.position = c;
                    EditorUtility.SetDirty(glass.transform);
                    moved = true;
                    Log($"잔 기본 위치를 옆으로 {d * 100f:0}cm 비킴 (서빙 감지 범위 밖으로)");
                    break;
                }
                if (!moved) Log("⚠ 잔 기본 위치가 서빙 감지 범위 안 — 잔을 직접 옆으로 옮겨 주세요");
            }
        }

        static float EdgeDistance(Vector3 from, Vector3 dir, float top)
        {
            float edge = 0f;
            for (float d = 0f; d <= 1.2f; d += 0.02f)
            {
                var y = SurfaceY(from + dir * d);
                if (!y.HasValue || Mathf.Abs(y.Value - top) > 0.02f) break;
                edge = d;
            }
            return edge;
        }

        // ───────── 3) 메뉴판 두 번째 판 ─────────
        static void SecondMenuPage()
        {
            var board = Object.FindFirstObjectByType<MenuBoard>(FindObjectsInactive.Include);
            if (!board) { Log("⚠ MenuBoard 없음"); return; }
            var first = board.transform.Find("BoardCanvas") as RectTransform;
            if (!first) { Log("⚠ BoardCanvas 없음"); return; }

            var second = board.transform.Find("BoardCanvas2") as RectTransform;
            if (!second)
            {
                second = (RectTransform)Object.Instantiate(first.gameObject, board.transform).transform;
                second.name = "BoardCanvas2";
            }
            float width = first.sizeDelta.x * first.localScale.x;
            second.localPosition = first.localPosition + Vector3.left * (width + 0.04f); // 플레이어 기준 왼쪽
            second.localRotation = first.localRotation;
            second.localScale = first.localScale;
            second.sizeDelta = first.sizeDelta;

            var text2 = second.GetComponentInChildren<TMP_Text>(true);
            var so = new SerializedObject(board);
            var pages = so.FindProperty("extraPages");
            pages.arraySize = 1;
            pages.GetArrayElementAtIndex(0).objectReferenceValue = text2;
            so.ApplyModifiedPropertiesWithoutUndo();
            board.Refresh();
            Log("메뉴판: 왼쪽에 같은 크기 판 추가, 레시피를 반씩 나눠 표시");
        }

        // ───────── 헬퍼 ─────────
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static float? SurfaceY(Vector3 p)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 0.5f, p.z), Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (var h in hits)
            {
                if (h.collider.attachedRigidbody) continue;
                if (h.point.y > p.y + 0.15f) continue;
                best = Mathf.Max(best, h.point.y);
            }
            return best > float.MinValue ? best : null;
        }
    }
}
