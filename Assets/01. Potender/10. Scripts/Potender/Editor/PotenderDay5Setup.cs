using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Potender.EditorTools
{
    /// <summary>
    /// Day 5 아트 &amp; 폴리싱 자동 세팅.
    ///  Potender ▸ Day5 폴리싱 세팅        : 캐릭터 모델 교체 · 조명 · 성능 · UI 정리 · 효과음 연결 · 오디오 압축 (여러 번 실행해도 됨)
    ///  Potender ▸ Day5 방 꾸미기 (Synty)  : 바닥 · 벽 · 선반 · 소품 · 촛불 조명 (실행할 때마다 Day5_Environment를 새로 만듦)
    ///  Potender ▸ Day5 방 꾸미기 되돌리기 : Day5_Environment 삭제 + 원래 Plane 다시 보이게
    /// </summary>
    public static class PotenderDay5Setup
    {
        const string Root = "Assets/01. Potender";
        const string ScenePath = Root + "/01. Scenes/test/Main.unity";
        const string PrefabDir = Root + "/Prefabs";
        const string ExtRoot = "Assets/99. External Assets";
        const string EnvName = "Day5_Environment";
        const string PerfName = "Day5_Perf";

        static readonly string[] CustomerModels =
        {
            "SM_Chr_Peasant_Male_01", "SM_Chr_Peasant_Female_01", "SM_Chr_Merchant_01", "SM_Chr_Blacksmith_Male_01",
            "SM_Chr_Blacksmith_Female_01", "SM_Chr_Mage_01", "SM_Chr_Monk_01", "SM_Chr_FortuneTeller_01",
            "SM_Chr_Soldier_Male_01", "SM_Chr_Soldier_Female_01", "SM_Chr_Rider_01",
        };
        static readonly string[] GoblinModels = { "SM_Chr_Jester_01", "SM_Chr_Hermit_01", "SM_Chr_Peasant_Male_01" };

        static readonly List<string> log = new();
        static void Log(string s) => log.Add("• " + s);

        // ═════════════════════ 메뉴 1: 폴리싱 ═════════════════════
        [MenuItem("Potender/Day5 폴리싱 세팅", priority = 50)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            log.Clear();
            fixedMats.Clear();
            matFixed = 0;

            var font = FindFont();
            Characters(font);
            AudioImport();

            var scene = EditorSceneManager.OpenScene(ScenePath);
            Lighting();
            Performance(font);
            UiAndSound(font);
            if (matFixed > 0) Log($"머티리얼 {matFixed}개를 URP/Lit으로 변환 (분홍색 방지)");
            Save(scene, "Day5 폴리싱 세팅");
        }

        // ═════════════════════ 1) 캐릭터 ═════════════════════
        static void Characters(TMP_FontAsset font)
        {
            SetupModelSwap(PrefabDir + "/Customer.prefab", CustomerModels, 1.7f, false, font);
            SetupModelSwap(PrefabDir + "/Goblin.prefab", GoblinModels, 1.25f, true, font);
        }

        static void SetupModelSwap(string path, string[] names, float height, bool single, TMP_FontAsset font)
        {
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) { Log($"⚠ {file} 프리팹 없음 — 건너뜀"); return; }
            var models = names.Select(FindPrefab).Where(p => p).ToList();
            if (single && models.Count > 1) models = models.Take(1).ToList();
            if (models.Count == 0) { Log($"⚠ {file}: Synty 캐릭터 프리팹을 못 찾음 (PolygonFantasyKingdom/Prefabs/Characters)"); return; }
            foreach (var m in models) FixMaterials(m);

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var swap = root.GetComponent<ModelSwap>();
                bool isNew = !swap;
                if (isNew) swap = root.AddComponent<ModelSwap>();
                var so = new SerializedObject(swap);

                var mp = so.FindProperty("models");
                if (mp.arraySize == 0)
                {
                    mp.arraySize = models.Count;
                    for (int i = 0; i < models.Count; i++) mp.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
                }
                var hp = so.FindProperty("hideRenderers");
                if (hp.arraySize == 0)
                {
                    var prims = PrimitiveRenderers(root);
                    hp.arraySize = prims.Count;
                    for (int i = 0; i < prims.Count; i++) hp.GetArrayElementAtIndex(i).objectReferenceValue = prims[i];
                }
                if (isNew) so.FindProperty("targetHeight").floatValue = height;
                so.ApplyModifiedPropertiesWithoutUndo();

                int fonts = font ? ApplyFont(root.GetComponentsInChildren<TMP_Text>(true), font) : 0;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Log(isNew
                    ? $"{file}: ModelSwap 추가 — 모델 {models.Count}종 ({string.Join(", ", models.Select(m => m.name.Replace("SM_Chr_", "")))}) · 키 {height}m · 도형 {hp.arraySize}개 숨김"
                    : $"{file}: ModelSwap 이미 있음 — 빈 칸만 채움" + (fonts > 0 ? $" (폰트 {fonts}개 교체)" : ""));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>Unity 기본 도형(캡슐·큐브·구)으로 된 렌더러 = 숨길 대상. UI(캔버스) 밑은 제외</summary>
        static List<Renderer> PrimitiveRenderers(GameObject root)
        {
            var list = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponentInParent<Canvas>(true)) continue;
                if (!r.TryGetComponent(out MeshFilter mf) || !mf.sharedMesh) continue;
                if (AssetDatabase.GetAssetPath(mf.sharedMesh).StartsWith("Library/")) list.Add(r);
            }
            return list;
        }

        // ═════════════════════ 2) 조명 ═════════════════════
        static void Lighting()
        {
            var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional);
            if (sun)
            {
                bool isDefault = Near(sun.color, Color.white) || Near(sun.color, new Color(1f, 0.957f, 0.839f));
                if (isDefault)
                {
                    sun.color = new Color(1f, 0.84f, 0.66f);
                    sun.intensity = 0.7f;
                    sun.shadows = LightShadows.None;
                    Log("Directional Light: 따뜻한 색 · 밝기 0.7 · 그림자 끔 (성능)");
                }
                else Log("Directional Light: 이미 조정됨 — 색은 그대로 둠");
                if (sun.lightmapBakeType == LightmapBakeType.Baked)
                {
                    sun.lightmapBakeType = LightmapBakeType.Mixed;
                    Log("Directional Light: Baked → Mixed (습격 붉은 조명이 바뀌도록)");
                }
                EditorUtility.SetDirty(sun);
            }

            if (RenderSettings.ambientMode != AmbientMode.Flat)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.38f, 0.29f, 0.22f);
                Log("주변광: Source = Color, 따뜻한 갈색 (습격 때 붉게 바뀜)");
            }
            else Log("주변광: 이미 Color 모드 — 그대로 둠");
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.03f;

        // ═════════════════════ 3) 성능 ═════════════════════
        static void Performance(TMP_FontAsset font)
        {
            // URP 에셋 (Quest = Mobile)
            var urps = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a && a.name.Contains("Mobile")).ToList();
            if (urps.Count == 0 && GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset def) urps.Add(def);
            foreach (var a in urps)
            {
                var so = new SerializedObject(a);
                SetBool(so, "m_SupportsHDR", false);
                SetInt(so, "m_MSAA", 4);
                SetFloat(so, "m_RenderScale", 1f);
                SetFloat(so, "m_ShadowDistance", 10f);
                SetInt(so, "m_ShadowCascadeCount", 1);
                SetBool(so, "m_RequireDepthTexture", false);
                SetBool(so, "m_RequireOpaqueTexture", false);
                SetBool(so, "m_UseSRPBatcher", true);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(a);
                Log($"{a.name}: HDR 끔 · MSAA 4x · 그림자 거리 10m/1단 · 깊이/불투명 텍스처 끔 · SRP Batcher 켬");
            }

            // 카메라
            var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderByDescending(c => c.CompareTag("MainCamera")).FirstOrDefault();
            if (cam)
            {
                cam.farClipPlane = Mathf.Min(cam.farClipPlane, 30f);
                var data = cam.GetUniversalAdditionalCameraData();
                if (data)
                {
                    data.renderPostProcessing = false;
                    data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                    EditorUtility.SetDirty(data);
                }
                EditorUtility.SetDirty(cam);
                Log($"카메라: Far 30m · Post Processing 끔 · 후처리 AA 끔 (MSAA만 사용)");
            }

            // 움직이지 않는 물체 → Static Batching
            int st = MarkStaticScene();
            Log($"Static Batching: 움직이지 않는 렌더러 {st}개 표시 (잡는 물건·손님·UI 제외)");

            // OpenXR: Single Pass Instanced + 포비티드 렌더링 (리플렉션 — 패키지 버전 차이로 컴파일 에러 안 나게)
            OpenXrSettings();

            // 런타임: FPS 표시 + 포비티드
            var scene = EditorSceneManager.GetActiveScene();
            var perf = scene.GetRootGameObjects().FirstOrDefault(g => g.name == PerfName);
            if (!perf) perf = new GameObject(PerfName);
            var fps = perf.GetComponent<FpsCounter>();
            if (!fps) fps = perf.AddComponent<FpsCounter>();
            if (font)
            {
                var so = new SerializedObject(fps);
                SetRefIfEmpty(so, "font", font);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (!perf.GetComponent<QuestFoveation>()) perf.AddComponent<QuestFoveation>();
            EditorUtility.SetDirty(perf);
            Log("Day5_Perf: FpsCounter(시야 왼쪽 아래 FPS) + QuestFoveation 추가");

            // Android 그래픽 API
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (apis.Length > 0 && apis[0] != GraphicsDeviceType.Vulkan)
                Log($"⚠ Android 그래픽 API 첫 번째가 {apis[0]} — Quest는 Vulkan 권장 (Player Settings ▸ Other)");
        }

        static int MarkStaticScene()
        {
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || r is SkinnedMeshRenderer) continue;
                var t = r.transform;
                if (r.GetComponentInParent<Rigidbody>(true)) continue;
                if (r.GetComponentInParent<XRBaseInteractable>(true)) continue;
                if (r.GetComponentInParent<Canvas>(true)) continue;
                if (r.GetComponentInParent<Camera>(true)) continue;
                if (r.GetComponentInParent<Animator>(true)) continue;
                if (r.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>(true)) continue;
                if (r.GetComponentInParent<GameFlow>(true) || r.GetComponentInParent<RaidEvent>(true)) continue;
                if (r.GetComponentInParent<CounterSpot>(true)) continue; // 서빙 트레이 표시 등 스크립트가 만질 수 있음
                var f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((f & StaticEditorFlags.BatchingStatic) != 0) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, f | StaticEditorFlags.BatchingStatic);
                n++;
            }
            return n;
        }

        static void OpenXrSettings()
        {
            var type = System.Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
            var get = type?.GetMethod("GetSettingsForBuildTargetGroup", new[] { typeof(BuildTargetGroup) });
            var settings = get?.Invoke(null, new object[] { BuildTargetGroup.Android }) as Object;
            if (!settings) { Log("⚠ OpenXR 설정을 못 찾음 — Render Mode / Foveated Rendering 직접 확인"); return; }

            var rm = type.GetProperty("renderMode");
            if (rm != null)
            {
                var want = System.Enum.Parse(rm.PropertyType, "SinglePassInstanced");
                if (!Equals(rm.GetValue(settings), want))
                {
                    rm.SetValue(settings, want);
                    Log("OpenXR (Android): Render Mode → Single Pass Instanced (눈 2개를 한 번에 그림)");
                }
                else Log("OpenXR (Android): Render Mode 이미 Single Pass Instanced");
            }

            var getFeatures = type.GetMethod("GetFeatures", System.Type.EmptyTypes);
            if (getFeatures?.Invoke(settings, null) is System.Array features)
            {
                bool found = false;
                foreach (var f in features)
                {
                    if (f == null || !f.GetType().Name.Contains("Foveat")) continue;
                    found = true;
                    var en = f.GetType().GetProperty("enabled");
                    if (en != null && !(bool)en.GetValue(f))
                    {
                        en.SetValue(f, true);
                        if (f is Object fo) EditorUtility.SetDirty(fo);
                        Log($"OpenXR (Android): {f.GetType().Name} 켬");
                    }
                }
                if (!found) Log("OpenXR (Android): Foveated Rendering 기능 없음 — XR Plug-in Management ▸ OpenXR 에서 확인");
            }
            EditorUtility.SetDirty(settings);
        }

        // ═════════════════════ 4) UI · 효과음 ═════════════════════
        static void UiAndSound(TMP_FontAsset font)
        {
            // 폰트 통일
            if (font)
            {
                int n = ApplyFont(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None), font);
                Log($"폰트: 기본 폰트(LiberationSans) 쓰던 텍스트 {n}개 → {font.name}");
            }
            else Log("⚠ NotoSansKR SDF 폰트 에셋 못 찾음 — 폰트 통일 건너뜀");

            // 디버그 텍스트 끄기
            int dbg = 0;
            foreach (var d in Object.FindObjectsByType<ContainerDebugText>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (new SerializedObject(d).FindProperty("text").objectReferenceValue is TMP_Text t && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    EditorUtility.SetDirty(t.gameObject);
                }
                if (d.enabled) { d.enabled = false; dbg++; EditorUtility.SetDirty(d); }
            }
            Log($"디버그 텍스트(ContainerDebugText) {dbg}개 끔 — 다시 보려면 컴포넌트 체크");

            // 효과음: GameFlow 이벤트 → Sfx AudioSource.PlayOneShot
            var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
            if (!flow) { Log("⚠ GameFlow 없음 — 효과음 연결 건너뜀"); return; }
            var sfx = FindSfx(flow);
            FindClips();
            int hooked = 0;
            hooked += Hook(flow.onOpen, sfx, clipOpen, "영업 시작");
            hooked += Hook(flow.onServeSuccess, sfx, clipCoin, "서빙 성공");
            hooked += Hook(flow.onServeWrong, sfx, clipWrong, "잘못 서빙");
            hooked += Hook(flow.onCustomerAngry, sfx, clipAngry, "손님 화남");
            hooked += Hook(flow.onClose, sfx, clipClose, "영업 종료");
            EditorUtility.SetDirty(flow);
            Log($"효과음: GameFlow 이벤트 {hooked}개 연결 → {sfx.name} · 영업시작 {Name(clipOpen)} / 성공 {Name(clipCoin)} / 틀림 {Name(clipWrong)} / 화남 {Name(clipAngry)} / 종료 {Name(clipClose)}");
        }

        static int ApplyFont(IEnumerable<TMP_Text> texts, TMP_FontAsset font)
        {
            int n = 0;
            foreach (var t in texts)
            {
                if (!t || t.font == font) continue;
                if (t.font && !t.font.name.Contains("LiberationSans")) continue; // 일부러 바꾼 폰트는 그대로
                t.font = font;
                EditorUtility.SetDirty(t);
                n++;
            }
            return n;
        }

        static AudioSource FindSfx(GameFlow flow)
        {
            var raid = Object.FindFirstObjectByType<RaidEvent>(FindObjectsInactive.Include);
            if (raid && new SerializedObject(raid).FindProperty("sfx").objectReferenceValue is AudioSource a) return a;
            var t = flow.transform.Find("Sfx");
            if (!t)
            {
                t = new GameObject("Sfx").transform;
                t.SetParent(flow.transform, false);
            }
            var src = t.GetComponent<AudioSource>();
            if (!src)
            {
                src = t.gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
            }
            return src;
        }

        static int Hook(UnityEvent e, AudioSource src, AudioClip clip, string label)
        {
            if (e == null || !src || !clip) return 0;
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
                if (e.GetPersistentTarget(i) == src && e.GetPersistentMethodName(i) == "PlayOneShot") return 0;
            UnityEventTools.AddObjectPersistentListener<AudioClip>(e, src.PlayOneShot, clip);
            return 1;
        }

        static AudioClip clipOpen, clipCoin, clipWrong, clipAngry, clipClose;

        static void FindClips()
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" })
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => !p.StartsWith("Assets/Samples") && !p.StartsWith("Assets/XRI"))
                .Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p))
                .Where(c => c && c.length < 8f).ToList();
            AudioClip Pick(params string[] keys) =>
                clips.FirstOrDefault(c => keys.Any(k => c.name.ToLowerInvariant().Contains(k)));
            clipOpen = Pick("bell", "door", "open", "welcome", "종");
            clipCoin = Pick("coin", "cash", "money", "register", "ding", "동전");
            clipWrong = Pick("wrong", "buzz", "error", "incorrect", "nope", "땡");
            clipAngry = Pick("angry", "grumble", "hmph", "annoy", "sigh", "화남");
            clipClose = Pick("fanfare", "result", "jingle", "close", "마감");
        }

        // ═════════════════════ 5) 오디오 압축 (Android) ═════════════════════
        static void AudioImport()
        {
            int lng = 0, sht = 0;
            foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.StartsWith("Assets/Samples") || path.StartsWith("Assets/XRI")) continue;
                if (AssetImporter.GetAtPath(path) is not AudioImporter imp) continue;
                if (imp.ContainsSampleSettingsOverride("Android")) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!clip) continue;

                bool isLong = clip.length > 15f;
                var s = imp.defaultSampleSettings;
                s.loadType = isLong ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = isLong ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                s.quality = 0.5f;
                imp.SetOverrideSampleSettings("Android", s);
                if (isLong) imp.loadInBackground = true; else imp.forceToMono = true;
                imp.SaveAndReimport();
                if (isLong) lng++; else sht++;
            }
            if (lng + sht > 0) Log($"오디오 Android 압축: BGM {lng}개 (Streaming·Vorbis) / 효과음 {sht}개 (ADPCM·Mono)");
        }

        // ═════════════════════ 메뉴 2: 방 꾸미기 ═════════════════════
        [MenuItem("Potender/Day5 방 꾸미기 (Synty)", priority = 51)]
        public static void BuildRoom()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            log.Clear();
            fixedMats.Clear();
            matFixed = 0;
            var scene = EditorSceneManager.OpenScene(ScenePath);

            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EnvName);
            if (old) { CleanRaidLights(old); Object.DestroyImmediate(old); Log("기존 Day5_Environment 지우고 새로 만듦"); }

            var spot = Object.FindFirstObjectByType<CounterSpot>(FindObjectsInactive.Include);
            var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
            var raid = Object.FindFirstObjectByType<RaidEvent>(FindObjectsInactive.Include);
            if (!spot || !origin) { Log("⚠ CounterSpot 또는 XR Origin을 못 찾음 — 중단"); Save(scene, "Day5 방 꾸미기"); return; }

            var env = new GameObject(EnvName).transform;
            Vector3 player = origin.transform.position;
            Vector3 stand = spot.StandPoint.position;
            float floorY = player.y;
            Vector3 toCust = Flat(stand - player);
            toCust = toCust.sqrMagnitude > 0.01f ? toCust.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, toCust);

            Transform entrance = Ref<Transform>(flow, "entrance");
            Transform exit = Ref<Transform>(flow, "exit");
            Transform gSpawn = Ref<Transform>(raid, "spawnPoint");

            // 방 크기: 플레이어 · 손님 자리 · 입구/출구 · 월드 UI가 모두 들어가게
            var pts = new List<Vector3> { player, player - toCust * 2.2f, stand };
            foreach (var s in Object.FindObjectsByType<CounterSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) pts.Add(s.StandPoint.position);
            foreach (var t in new[] { entrance, exit, gSpawn }) if (t) pts.Add(t.position);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.WorldSpace && c.isRootCanvas && !c.GetComponentInParent<Camera>(true)) pts.Add(c.transform.position);
            const float margin = 1.2f;
            float minX = pts.Min(p => p.x) - margin, maxX = pts.Max(p => p.x) + margin;
            float minZ = pts.Min(p => p.z) - margin, maxZ = pts.Max(p => p.z) + margin;

            // ── 바닥
            var floorPf = FirstPrefab("SM_Bld_Castle_Floor_Wood_01", "SM_Bld_Base_Floor_01");
            if (floorPf && Measure(floorPf, Quaternion.identity, out var fb) && fb.size.x > 0.2f && fb.size.z > 0.2f)
            {
                FixMaterials(floorPf);
                int nx = Mathf.CeilToInt((maxX - minX) / fb.size.x), nz = Mathf.CeilToInt((maxZ - minZ) / fb.size.z);
                if (nx * nz <= 600)
                {
                    maxX = minX + nx * fb.size.x;
                    maxZ = minZ + nz * fb.size.z;
                    var floor = Group(env, "Floor");
                    for (int x = 0; x < nx; x++)
                    for (int z = 0; z < nz; z++)
                    {
                        var c = new Vector3(minX + (x + 0.5f) * fb.size.x, 0f, minZ + (z + 0.5f) * fb.size.z);
                        var go = PlaceTop(floorPf, floor, Quaternion.identity, c, floorY);
                        foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false; // 바닥 충돌은 원래 Plane이 담당
                    }
                    var plane = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Plane");
                    if (plane && plane.TryGetComponent(out MeshRenderer pr)) { pr.enabled = false; EditorUtility.SetDirty(pr); }
                    Log($"바닥: {floorPf.name} {nx}×{nz}장 ({maxX - minX:0.0}m × {maxZ - minZ:0.0}m) · 원래 Plane은 렌더러만 끔 (충돌 유지)");
                }
                else Log($"⚠ 바닥 타일이 너무 많음 ({nx}×{nz}) — 건너뜀");
            }
            else Log("⚠ 바닥 프리팹 못 찾음 — 건너뜀");

            // ── 벽 (입구·출구 쪽은 한 칸 비움)
            var wallPf = FirstPrefab("SM_Bld_Castle_Wall_01", "SM_Bld_Wall_Wood_Beams_01", "SM_Bld_Base_Wall_01");
            if (wallPf && Measure(wallPf, Quaternion.identity, out var wb))
            {
                FixMaterials(wallPf);
                bool alongX = wb.size.x >= wb.size.z;
                float seg = Mathf.Max(wb.size.x, wb.size.z), thick = Mathf.Min(wb.size.x, wb.size.z);
                if (seg < 0.5f) seg = 0.5f;
                var rotX = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                var rotZ = alongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                var walls = Group(env, "Walls");
                var doors = new List<Vector3>();
                if (entrance) doors.Add(entrance.position);
                if (exit && (!entrance || (exit.position - entrance.position).sqrMagnitude > 1f)) doors.Add(exit.position);

                rMinX = minX; rMaxX = maxX; rMinZ = minZ; rMaxZ = maxZ;
                int count = 0;
                count += WallRun(wallPf, walls, rotX, new Vector3(minX, 0, minZ - thick / 2), Vector3.right, maxX - minX, seg, floorY, doors, 2);
                count += WallRun(wallPf, walls, rotX, new Vector3(minX, 0, maxZ + thick / 2), Vector3.right, maxX - minX, seg, floorY, doors, 0);
                count += WallRun(wallPf, walls, rotZ, new Vector3(minX - thick / 2, 0, minZ), Vector3.forward, maxZ - minZ, seg, floorY, doors, 3);
                count += WallRun(wallPf, walls, rotZ, new Vector3(maxX + thick / 2, 0, minZ), Vector3.forward, maxZ - minZ, seg, floorY, doors, 1);
                Log($"벽: {wallPf.name} {count}개 (한 칸 {seg:0.0}m · 높이 {wb.size.y:0.0}m) — 입구/출구 쪽 한 칸씩 비움");
            }
            else Log("⚠ 벽 프리팹 못 찾음 — 건너뜀");

            // ── 소품
            var props = Group(env, "Props");
            var faceCust = Quaternion.LookRotation(toCust);
            Prop(props, "선반(플레이어 뒤)", faceCust, player - toCust * 1.6f, floorY, "SM_Prop_Drinks_Shelves_01", "SM_Gen_Prop_Shelf_01", "SM_Bld_Shelf_01");
            Prop(props, "가마솥", faceCust, player - toCust * 1.4f + right * 1.7f, floorY, "SM_Item_Cauldron_01", "SM_Item_Cauldron_02");
            Prop(props, "술통", faceCust, player - toCust * 1.4f - right * 1.7f, floorY, "SM_Prop_Barrel_01");
            var rug = Prop(props, "러그(손님 자리)", faceCust, stand + toCust * 0.5f, floorY + 0.005f, "SM_Prop_Rug_01", "SM_Prop_Rug_02");
            if (rug) foreach (var col in rug.GetComponentsInChildren<Collider>(true)) col.enabled = false;

            // 손님 쪽 구석 (입구에서 먼 쪽)에 술통 더미
            var corners = new[] { new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ) }
                .Where(c => Vector3.Dot(Flat(c - player), toCust) > 0f)
                .OrderByDescending(c => entrance ? Flat(c - entrance.position).magnitude : 0f).ToList();
            if (corners.Count > 0)
            {
                var c = corners[0];
                var inset = new Vector3(Mathf.Sign((minX + maxX) / 2 - c.x), 0, Mathf.Sign((minZ + maxZ) / 2 - c.z)) * 0.9f;
                Prop(props, "술통 더미(구석)", Quaternion.identity, c + inset, floorY, "SM_Prop_Barrel_Stack_01", "SM_Prop_Barrel_01");
            }

            // ── 카운터 위 촛대 2개 + 따뜻한 포인트 라이트
            var lightsMade = new List<Light>();
            var counterGo = spot.transform.parent ? spot.transform.parent.Find("Counter") : null;
            Transform counterRoot = counterGo ? counterGo : spot.transform.parent;
            if (counterRoot && TryBounds(counterRoot.gameObject, out var cb, true))
            {
                float half = Mathf.Abs(right.x) * cb.extents.x + Mathf.Abs(right.z) * cb.extents.z;
                foreach (int side in new[] { -1, 1 })
                {
                    var pos = new Vector3(cb.center.x, 0, cb.center.z) + right * side * Mathf.Max(0f, half - 0.15f);
                    var candle = Prop(props, side < 0 ? "촛대 L" : "촛대 R", faceCust, pos, cb.max.y, "SM_Item_Candlestick_01", "SM_Item_Candlestick_02");
                    float top = candle && TryBounds(candle, out var kb) ? kb.max.y : cb.max.y + 0.25f;
                    if (candle) foreach (var col in candle.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                    lightsMade.Add(MakeLight(env, side < 0 ? "CandleLight_L" : "CandleLight_R", new Vector3(pos.x, top + 0.15f, pos.z), 1.4f, 3f));
                }
            }
            else Log("⚠ 카운터를 못 찾음 — 촛대 건너뜀");
            // 손님 쪽 천장 등불
            lightsMade.Add(MakeLight(env, "HallLight", stand + toCust * 1.2f + Vector3.up * 2.6f, 1.2f, 6f));
            Log($"조명: 따뜻한 포인트 라이트 {lightsMade.Count}개 (그림자 없음, Realtime)");

            // 습격 때 같이 붉어지도록 RaidEvent에 등록
            if (raid)
            {
                var so = new SerializedObject(raid);
                var lp = so.FindProperty("lights");
                foreach (var l in lightsMade)
                {
                    lp.arraySize++;
                    lp.GetArrayElementAtIndex(lp.arraySize - 1).objectReferenceValue = l;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(raid);
                Log("RaidEvent 조명 목록에 새 라이트 추가 (습격 때 붉게)");
            }

            // 실내 느낌: 하늘 대신 어두운 색 (천장 없음 → 위를 보면 어두운 천장처럼)
            var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderByDescending(c => c.CompareTag("MainCamera")).FirstOrDefault();
            if (cam)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.04f, 0.035f);
                EditorUtility.SetDirty(cam);
            }

            // Static Batching
            foreach (var t in env.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Light>()) continue;
                var f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, f | StaticEditorFlags.BatchingStatic);
                foreach (var r in t.GetComponents<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (matFixed > 0) Log($"머티리얼 {matFixed}개를 URP/Lit으로 변환 (분홍색 방지)");
            Log("배치가 이상하면: Day5_Environment 안에서 직접 옮기기 (메뉴 다시 실행하면 새로 만들어지니 손으로 고친 뒤엔 재실행 X)");
            Save(scene, "Day5 방 꾸미기");
        }

        [MenuItem("Potender/Day5 방 꾸미기 되돌리기", priority = 52)]
        public static void RemoveRoom()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            log.Clear();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var env = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EnvName);
            if (env) { CleanRaidLights(env); Object.DestroyImmediate(env); Log("Day5_Environment 삭제"); }
            var plane = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Plane");
            if (plane && plane.TryGetComponent(out MeshRenderer pr)) { pr.enabled = true; EditorUtility.SetDirty(pr); Log("Plane 다시 보이게"); }
            Save(scene, "Day5 방 꾸미기 되돌리기");
        }

        static void CleanRaidLights(GameObject env)
        {
            var raid = Object.FindFirstObjectByType<RaidEvent>(FindObjectsInactive.Include);
            if (!raid) return;
            var so = new SerializedObject(raid);
            var lp = so.FindProperty("lights");
            for (int i = lp.arraySize - 1; i >= 0; i--)
            {
                var o = lp.GetArrayElementAtIndex(i).objectReferenceValue as Light;
                if (!o || o.transform.IsChildOf(env.transform))
                {
                    lp.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    lp.DeleteArrayElementAtIndex(i);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 방 경계 (벽 세울 때 문이 어느 벽에 가장 가까운지 판단용)
        static float rMinX, rMaxX, rMinZ, rMaxZ;

        /// <summary>sideId: 0 = +Z, 1 = +X, 2 = -Z, 3 = -X</summary>
        static int WallRun(GameObject pf, Transform parent, Quaternion rot, Vector3 start, Vector3 dir, float length,
                           float seg, float floorY, List<Vector3> doors, int sideId)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(length / seg - 0.05f));
            float step = length / n; // 빈틈 없이 균등하게
            var skip = new HashSet<int>();
            foreach (var d in doors)
            {
                if (NearestSide(d) != sideId) continue; // 각 문은 가장 가까운 벽 한 곳에서만 비움
                float along = Vector3.Dot(Flat(d - start), dir);
                skip.Add(Mathf.Clamp(Mathf.FloorToInt(along / step), 0, n - 1));
            }
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (skip.Contains(i)) continue;
                Place(pf, parent, rot, start + dir * (step * (i + 0.5f)), floorY);
                made++;
            }
            return made;
        }

        static int NearestSide(Vector3 d)
        {
            float[] dist = { Mathf.Abs(rMaxZ - d.z), Mathf.Abs(rMaxX - d.x), Mathf.Abs(d.z - rMinZ), Mathf.Abs(d.x - rMinX) };
            int best = 0;
            for (int i = 1; i < 4; i++) if (dist[i] < dist[best]) best = i;
            return best;
        }

        static GameObject Prop(Transform parent, string label, Quaternion rot, Vector3 pos, float bottomY, params string[] names)
        {
            var pf = FirstPrefab(names);
            if (!pf) { Log($"⚠ {label}: 프리팹 못 찾음 ({names[0]})"); return null; }
            FixMaterials(pf);
            var go = Place(pf, parent, rot, pos, bottomY);
            Log($"{label}: {pf.name}");
            return go;
        }

        static Light MakeLight(Transform parent, string name, Vector3 pos, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.3f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        // ═════════════════════ 헬퍼 ═════════════════════
        static GameObject Place(GameObject pf, Transform parent, Quaternion rot, Vector3 center, float bottomY)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.transform.SetPositionAndRotation(Vector3.zero, rot);
            if (TryBounds(go, out var b)) go.transform.position = new Vector3(center.x - b.center.x, bottomY - b.min.y, center.z - b.center.z);
            else go.transform.position = new Vector3(center.x, bottomY, center.z);
            return go;
        }

        static GameObject PlaceTop(GameObject pf, Transform parent, Quaternion rot, Vector3 center, float topY)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.transform.SetPositionAndRotation(Vector3.zero, rot);
            if (TryBounds(go, out var b)) go.transform.position = new Vector3(center.x - b.center.x, topY - b.max.y, center.z - b.center.z);
            else go.transform.position = new Vector3(center.x, topY, center.z);
            return go;
        }

        static bool Measure(GameObject pf, Quaternion rot, out Bounds b)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            go.transform.SetPositionAndRotation(Vector3.zero, rot);
            bool ok = TryBounds(go, out b);
            Object.DestroyImmediate(go);
            return ok;
        }

        static bool TryBounds(GameObject go, out Bounds b, bool skipUi = false)
        {
            b = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (skipUi && r.GetComponentInParent<Canvas>(true)) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject FindPrefab(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Prefab", new[] { ExtRoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }

        static GameObject FirstPrefab(params string[] names)
        {
            foreach (var n in names)
            {
                var p = FindPrefab(n);
                if (p) return p;
            }
            return null;
        }

        static TMP_FontAsset FindFont()
        {
            foreach (var g in AssetDatabase.FindAssets("NotoSansKR t:TMP_FontAsset"))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g));
                if (f) return f;
            }
            return null;
        }

        // Synty 머티리얼이 Built-in(Standard) 셰이더면 URP에서 분홍색 → URP/Lit으로 변환
        static readonly HashSet<Material> fixedMats = new();
        static int matFixed;
        static void FixMaterials(GameObject prefab)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit || !prefab) return;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (!m || !fixedMats.Add(m)) continue;
                var sh = m.shader;
                bool broken = !sh || sh.name == "Hidden/InternalErrorShader" || sh.name == "Standard" || sh.name.StartsWith("Legacy Shaders/");
                if (!broken || !AssetDatabase.GetAssetPath(m).StartsWith("Assets/")) continue;
                m.shader = lit;
                if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex")) m.SetTexture("_BaseMap", m.GetTexture("_MainTex"));
                if (m.HasProperty("_Color")) m.SetColor("_BaseColor", m.GetColor("_Color"));
                EditorUtility.SetDirty(m);
                matFixed++;
            }
        }

        static T Ref<T>(Object o, string prop) where T : Object
            => o ? new SerializedObject(o).FindProperty(prop)?.objectReferenceValue as T : null;

        static void SetBool(SerializedObject so, string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; }
        static void SetInt(SerializedObject so, string n, int v) { var p = so.FindProperty(n); if (p != null) p.intValue = v; }
        static void SetFloat(SerializedObject so, string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; }
        static void SetRefIfEmpty(SerializedObject so, string n, Object v)
        {
            var p = so.FindProperty(n);
            if (p != null && !p.objectReferenceValue && v) p.objectReferenceValue = v;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static string Name(Object o) => o ? o.name : "없음";

        static void Save(UnityEngine.SceneManagement.Scene scene, string title)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            string msg = string.Join("\n", log);
            Debug.Log($"[Potender {title}] 완료\n" + msg);
            EditorUtility.DisplayDialog("Potender " + title, "완료! 자세한 내용은 Console\n\n" + msg, "OK");
        }
    }
}
