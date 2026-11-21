using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Potender.EditorTools
{
    /// <summary>
    /// Day 4 강도 이벤트 자동 세팅. 메뉴 Potender ▸ Day4 강도 세팅 (여러 번 실행해도 됨 — 이미 있는 건 건너뜀)
    ///  1) 불꽃 포션 레시피(Rcp_Fire: 꿀 + 용의 피, 흔들기, 공격 포션) → RecipeBook에 추가
    ///  2) Fx_FireBlast 프리팹 (Fx_Explosion 복제 + 주황색), 두 폭발 프리팹에 AudioSource
    ///  3) Goblin 프리팹 (Customer 프리팹 복제 → 초록 · 작게 · 귀 · 히트 콜라이더)
    ///  4) Main 씬: RaidEvent(+ 스폰/목표 지점, 경광등, BGM/SFX AudioSource), 잔에 PotionThrowable,
    ///     병·쉐이커에 LoopSfx
    ///  5) 오디오 파일 이름으로 자동 연결 (pour / shake / boom·explo / alarm·siren / raid·battle / bgm …)
    ///     → 사운드 파일을 프로젝트에 넣은 뒤 메뉴를 한 번 더 실행하면 빈 칸에 채워짐
    /// </summary>
    public static class PotenderDay4Setup
    {
        const string Root = "Assets/01. Potender";
        const string ScenePath = Root + "/01. Scenes/test/Main.unity";
        const string DataDir = Root + "/Data";
        const string PrefabDir = Root + "/Prefabs";

        static readonly List<string> log = new();
        static void Log(string s) => log.Add("• " + s);

        [MenuItem("Potender/Day4 강도 세팅")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            log.Clear();
            FindAudio();

            var fire = FireRecipe();
            var fireFx = FireFx();
            var goblin = GoblinPrefab();

            var scene = EditorSceneManager.OpenScene(ScenePath);
            Physics.SyncTransforms();
            SetupRaid(goblin, fire);
            SetupGlasses(fireFx);
            SetupLoopSfx();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string msg = string.Join("\n", log);
            Debug.Log("[Potender Day4] 완료\n" + msg);
            EditorUtility.DisplayDialog("Potender Day4 강도 세팅", "완료! 자세한 내용은 Console\n\n" + msg, "OK");
        }

        // ───────── 1) 불꽃 포션 ─────────
        static RecipeData FireRecipe()
        {
            var book = Load<RecipeBook>("t:RecipeBook");
            var existing = book ? book.recipes.FirstOrDefault(r => r && r.isAttackPotion) : null;
            if (existing) { Log($"공격 포션 이미 있음: {existing.potionName}"); return existing; }

            var honey = FindIngredient("Ing_Honey");
            var blood = FindIngredient("Ing_DragonBlood");
            string path = DataDir + "/Rcp_Fire.asset";
            var r = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
            if (!r)
            {
                r = ScriptableObject.CreateInstance<RecipeData>();
                r.potionName = "불꽃 포션";
                r.effectDescription = "던지면 펑! 불길이 터진다 (강도 퇴치용)";
                r.ingredients = new List<RecipeIngredient>
                {
                    new() { ingredient = honey, parts = 1 },
                    new() { ingredient = blood, parts = 1 },
                };
                r.method = MixMethod.Shake;
                r.potionColor = new Color(1f, 0.82f, 0.1f);
                r.isAttackPotion = true;
                AssetDatabase.CreateAsset(r, path);
                Log("불꽃 포션 레시피 생성: 꿀 1 : 용의 피 1, 흔들기 (Data/Rcp_Fire)");
            }
            if (book && !book.recipes.Contains(r))
            {
                book.recipes.Add(r);
                EditorUtility.SetDirty(book);
                Log("RecipeBook에 불꽃 포션 추가 → 메뉴판에 [비상용]으로 표시됨");
            }
            if (!honey || !blood) Log("⚠ Ing_Honey / Ing_DragonBlood 못 찾음 — Rcp_Fire 재료 직접 지정");
            return r;
        }

        static IngredientData FindIngredient(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:IngredientData", new[] { Root }))
            {
                var a = AssetDatabase.LoadAssetAtPath<IngredientData>(AssetDatabase.GUIDToAssetPath(g));
                if (a && a.name == name) return a;
            }
            return null;
        }

        // ───────── 2) 폭발 프리팹 ─────────
        static GameObject FireFx()
        {
            string src = PrefabDir + "/Fx_Explosion.prefab";
            string dst = PrefabDir + "/Fx_FireBlast.prefab";
            AddExplosionAudio(src);

            if (!AssetDatabase.LoadAssetAtPath<GameObject>(dst))
            {
                if (!AssetDatabase.CopyAsset(src, dst)) { Log("⚠ Fx_Explosion 복제 실패"); return null; }
                var root = PrefabUtility.LoadPrefabContents(dst);
                root.transform.localScale *= 1.6f;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0.05f));
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                PrefabUtility.SaveAsPrefabAsset(root, dst);
                PrefabUtility.UnloadPrefabContents(root);
                Log("Fx_FireBlast 프리팹 생성 (Fx_Explosion 복제, 주황 · 1.6배)");
            }
            AddExplosionAudio(dst);
            return AssetDatabase.LoadAssetAtPath<GameObject>(dst);
        }

        static void AddExplosionAudio(string path)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            var a = root.GetComponent<AudioSource>();
            bool changed = false;
            if (!a)
            {
                a = root.AddComponent<AudioSource>();
                a.playOnAwake = true;
                a.spatialBlend = 1f;
                a.minDistance = 1f;
                a.maxDistance = 15f;
                changed = true;
            }
            if (!a.clip && clipBoom) { a.clip = clipBoom; changed = true; }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Log($"{System.IO.Path.GetFileNameWithoutExtension(path)}: AudioSource {(a.clip ? $"+ '{a.clip.name}'" : "(clip 비어 있음 → '펑' 소리 넣기)")}");
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ───────── 3) 고블린 프리팹 ─────────
        static Goblin GoblinPrefab()
        {
            string path = PrefabDir + "/Goblin.prefab";
            var existingGo = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var existing = existingGo ? existingGo.GetComponent<Goblin>() : null;
            if (existing) { Log("Goblin 프리팹 이미 있음"); return existing; }

            var cust = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Customer.prefab");
            if (!cust) { Log("⚠ Customer 프리팹 없음 — Goblin 프리팹 직접 만들기"); return null; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(cust);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Goblin";

            var c = go.GetComponent<Customer>();
            var cso = new SerializedObject(c);
            Object Ref(string n) => cso.FindProperty(n)?.objectReferenceValue;
            var tints = new List<Renderer>();
            var tp = cso.FindProperty("tintRenderers");
            for (int i = 0; tp != null && i < tp.arraySize; i++)
                if (tp.GetArrayElementAtIndex(i).objectReferenceValue is Renderer r) tints.Add(r);
            var bubble = Ref("bubble");
            var bubbleText = Ref("bubbleText");
            var timerRoot = Ref("patienceRoot");
            var timerFill = Ref("patienceFill");
            Object.DestroyImmediate(c);

            // 머리·코도 초록으로, 뾰족 귀 추가
            foreach (var n in new[] { "Head", "Nose" })
            {
                var t = go.transform.Find(n);
                if (t && t.TryGetComponent(out Renderer r) && !tints.Contains(r)) tints.Add(r);
            }
            var head = go.transform.Find("Head");
            if (head)
            {
                var headMat = head.GetComponent<Renderer>() ? head.GetComponent<Renderer>().sharedMaterial : null;
                foreach (int side in new[] { -1, 1 })
                {
                    var ear = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ear.name = side < 0 ? "Ear_L" : "Ear_R";
                    Object.DestroyImmediate(ear.GetComponent<Collider>());
                    ear.transform.SetParent(go.transform, false);
                    ear.transform.localPosition = head.localPosition + new Vector3(side * 0.24f, 0.06f, 0f);
                    ear.transform.localRotation = Quaternion.Euler(0f, 0f, side * -25f);
                    ear.transform.localScale = new Vector3(0.28f, 0.07f, 0.05f);
                    if (headMat) ear.GetComponent<Renderer>().sharedMaterial = headMat;
                    tints.Add(ear.GetComponent<Renderer>());
                }
            }

            go.transform.localScale = Vector3.one * 0.75f;

            // 히트 콜라이더 (손님과 달리 맞아야 함) + 움직이는 트리거용 Kinematic Rigidbody
            var cap = go.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.radius = 0.4f;
            cap.height = 1.9f;
            cap.center = new Vector3(0f, 0.95f, 0f);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var g = go.AddComponent<Goblin>();
            var gso = new SerializedObject(g);
            var gt = gso.FindProperty("tintRenderers");
            gt.arraySize = tints.Count;
            for (int i = 0; i < tints.Count; i++) gt.GetArrayElementAtIndex(i).objectReferenceValue = tints[i];
            gso.FindProperty("bubble").objectReferenceValue = bubble;
            gso.FindProperty("bubbleText").objectReferenceValue = bubbleText;
            gso.FindProperty("timerRoot").objectReferenceValue = timerRoot;
            gso.FindProperty("timerFill").objectReferenceValue = timerFill;
            gso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Log("Goblin 프리팹 생성 (Customer 복제: 초록 · 0.75배 · 귀 · 트리거 콜라이더)");
            return null; // 씬 연결은 SetupRaid에서 경로로 다시 로드 (저장 직후 참조가 끊기는 문제)
        }

        // ───────── 4) 씬: RaidEvent ─────────
        static void SetupRaid(Goblin goblin, RecipeData fire)
        {
            var raid = Object.FindFirstObjectByType<RaidEvent>(FindObjectsInactive.Include);
            var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
            var spot = Object.FindFirstObjectByType<CounterSpot>(FindObjectsInactive.Include);

            bool created = false;
            if (!raid)
            {
                raid = new GameObject("RaidEvent").AddComponent<RaidEvent>();
                created = true;
            }
            Transform rt = raid.transform;
            var so = new SerializedObject(raid);

            Transform entrance = flow ? new SerializedObject(flow).FindProperty("entrance").objectReferenceValue as Transform : null;
            Transform stand = spot ? spot.StandPoint : null;

            // 지점
            var spawn = Child(rt, "GoblinSpawn", out bool newSpawn);
            var target = Child(rt, "GoblinTarget", out bool newTarget);
            if (newSpawn) spawn.position = entrance ? entrance.position : (stand ? stand.position + stand.forward * 4f : Vector3.forward * 5f);
            if (newTarget && stand)
            {
                // 손님 자리 옆 (입구 쪽으로 0.9m) — 손님과 겹치지 않게
                float sign = Vector3.Dot(spawn.position - stand.position, stand.right) >= 0f ? 1f : -1f;
                target.position = stand.position + stand.right * sign * 0.9f;
                target.rotation = stand.rotation;
            }
            SetRef(so, "spawnPoint", spawn);
            SetRef(so, "targetPoint", target);
            if (!so.FindProperty("exitPoint").objectReferenceValue) SetRef(so, "exitPoint", spawn);
            // 씬을 연 뒤 경로로 다시 로드해야 참조가 안 끊김
            var goblinGo = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Goblin.prefab");
            if (goblinGo) goblin = goblinGo.GetComponent<Goblin>();
            if (goblin && !so.FindProperty("goblinPrefab").objectReferenceValue) SetRef(so, "goblinPrefab", goblin);
            if (!goblin) Log("⚠ Goblin 프리팹을 못 찾음 — RaidEvent의 goblinPrefab에 직접 드래그");
            if (fire && !so.FindProperty("firePotion").objectReferenceValue) SetRef(so, "firePotion", fire);

            // 경광등
            var alarm = Child(rt, "RaidAlarmLight", out bool newAlarm);
            if (newAlarm)
            {
                var l = alarm.gameObject.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.1f, 0.05f);
                l.intensity = 3f;
                l.range = 6f;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                l.shadows = LightShadows.None;
                alarm.position = (spot ? spot.transform.position : Vector3.zero) + Vector3.up * 2.3f;
                alarm.gameObject.SetActive(false);
            }
            var show = so.FindProperty("showDuringRaid");
            if (show.arraySize == 0) { show.arraySize = 1; show.GetArrayElementAtIndex(0).objectReferenceValue = alarm.gameObject; }

            // 조명: 씬의 라이트 전부 (경광등 제외)
            var lightsProp = so.FindProperty("lights");
            if (lightsProp.arraySize == 0)
            {
                var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(l => !l.transform.IsChildOf(rt)).ToArray();
                lightsProp.arraySize = lights.Length;
                for (int i = 0; i < lights.Length; i++) lightsProp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
                int baked = lights.Count(l => l.lightmapBakeType == LightmapBakeType.Baked);
                Log($"조명: 라이트 {lights.Length}개 연결" + (baked > 0 ? $" ⚠ 그중 {baked}개가 Baked → 색이 안 바뀜, Mixed/Realtime으로" : ""));
            }

            // 사운드
            var bgmN = MakeAudio(rt, "Bgm_Normal", true, clipBgm);
            var bgmR = MakeAudio(rt, "Bgm_Raid", true, clipRaidBgm);
            var sfx = MakeAudio(rt, "Sfx", false, null);
            SetRefIfEmpty(so, "bgmNormal", bgmN);
            SetRefIfEmpty(so, "bgmRaid", bgmR);
            SetRefIfEmpty(so, "sfx", sfx);
            SetRefIfEmpty(so, "alarmClip", clipAlarm);
            SetRefIfEmpty(so, "repelClip", clipRepel);
            SetRefIfEmpty(so, "stealClip", clipSteal);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(raid);
            Log(created ? "RaidEvent 생성 (GoblinSpawn = 입구, GoblinTarget = 손님 자리 옆 — 위치는 직접 조정)" : "RaidEvent 이미 있음 — 빈 칸만 채움");
            if (!entrance) Log("⚠ GameFlow 입구 못 찾음 → GoblinSpawn 위치 직접 지정");
        }

        static AudioSource MakeAudio(Transform parent, string name, bool bgm, AudioClip clip)
        {
            var t = Child(parent, name, out bool isNew);
            var a = t.GetComponent<AudioSource>();
            if (!a)
            {
                a = t.gameObject.AddComponent<AudioSource>();
                a.loop = bgm;
                a.playOnAwake = name == "Bgm_Normal";
                a.spatialBlend = 0f;
                a.volume = bgm ? 0.5f : 1f;
            }
            if (!a.clip && clip) { a.clip = clip; Log($"{name} ← '{clip.name}'"); }
            return a;
        }

        // ───────── 4) 씬: 잔 ─────────
        static void SetupGlasses(GameObject fireFx)
        {
            int n = 0;
            foreach (var glass in Object.FindObjectsByType<ServingGlass>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var t = glass.GetComponent<PotionThrowable>();
                if (!t) { t = glass.gameObject.AddComponent<PotionThrowable>(); n++; }
                var so = new SerializedObject(t);
                if (fireFx) SetRefIfEmpty(so, "explosionPrefab", fireFx);
                so.ApplyModifiedPropertiesWithoutUndo();

                if (glass.TryGetComponent(out XRGrabInteractable grab)) { grab.throwOnDetach = true; EditorUtility.SetDirty(grab); }
                if (glass.TryGetComponent(out Rigidbody rb)) { rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; EditorUtility.SetDirty(rb); }
                if (!glass.GetComponent<ReturnToHome>()) Log($"⚠ {glass.name}에 ReturnToHome 없음 — 터진 뒤 제자리로 못 돌아감");
                EditorUtility.SetDirty(t);
            }
            Log($"잔: PotionThrowable {n}개 추가 (Throw On Detach 켬, 충돌 감지 Continuous Speculative)");
        }

        // ───────── 4) 씬: 붓기 / 흔들기 사운드 ─────────
        static void SetupLoopSfx()
        {
            int n = 0;
            foreach (var b in Object.FindObjectsByType<IngredientBottle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                n += AddLoop(b.gameObject, LoopSfx.Mode.Pour, clipPour);
            foreach (var c in Object.FindObjectsByType<MixingContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                n += AddLoop(c.gameObject, LoopSfx.Mode.Pour, clipPour);
                if (c.GetComponent<ShakeDetector>()) n += AddLoop(c.gameObject, LoopSfx.Mode.Shake, clipShake);
            }
            Log($"LoopSfx: {n}개 추가 (붓기 '{Name(clipPour)}', 흔들기 '{Name(clipShake)}')");
        }

        static int AddLoop(GameObject go, LoopSfx.Mode mode, AudioClip clip)
        {
            LoopSfx found = null;
            foreach (var l in go.GetComponents<LoopSfx>())
                if (new SerializedObject(l).FindProperty("mode").enumValueIndex == (int)mode) found = l;
            bool isNew = !found;
            if (isNew) found = go.AddComponent<LoopSfx>();
            var so = new SerializedObject(found);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            if (clip) SetRefIfEmpty(so, "clip", clip);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(found);
            return isNew ? 1 : 0;
        }

        // ───────── 5) 오디오 자동 찾기 ─────────
        static AudioClip clipPour, clipShake, clipBoom, clipAlarm, clipRaidBgm, clipBgm, clipRepel, clipSteal;

        static void FindAudio()
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c).ToList();
            AudioClip Pick(params string[] keys) =>
                clips.FirstOrDefault(c => keys.Any(k => c.name.ToLowerInvariant().Contains(k)));

            clipPour = Pick("pour", "붓", "liquid", "water");
            clipShake = Pick("shake", "흔들", "rattle");
            clipBoom = Pick("explo", "boom", "펑", "blast", "fire");
            clipAlarm = Pick("alarm", "siren", "경보", "alert", "warning");
            clipRaidBgm = Pick("raid", "battle", "danger", "습격", "combat");
            clipBgm = clips.FirstOrDefault(c => c != clipRaidBgm &&
                new[] { "bgm", "tavern", "music", "bar_", "ambient" }.Any(k => c.name.ToLowerInvariant().Contains(k)));
            clipRepel = Pick("victory", "success", "win", "퇴치", "cheer");
            clipSteal = Pick("laugh", "steal", "fail", "lose", "털");
            Log($"오디오 파일 {clips.Count}개 검색 → 붓기 {Name(clipPour)} / 흔들기 {Name(clipShake)} / 펑 {Name(clipBoom)} / 경보 {Name(clipAlarm)} / 습격BGM {Name(clipRaidBgm)} / BGM {Name(clipBgm)}");
        }

        // ───────── 헬퍼 ─────────
        static string Name(Object o) => o ? o.name : "없음";

        static T Load<T>(string filter) where T : Object
        {
            var g = AssetDatabase.FindAssets(filter, new[] { Root }).FirstOrDefault();
            return g != null ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)) : null;
        }

        static Transform Child(Transform parent, string name, out bool isNew)
        {
            var t = parent.Find(name);
            isNew = !t;
            if (isNew)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }
            return t;
        }

        static void SetRef(SerializedObject so, string prop, Object value) => so.FindProperty(prop).objectReferenceValue = value;

        static void SetRefIfEmpty(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null && !p.objectReferenceValue && value) p.objectReferenceValue = value;
        }
    }
}
