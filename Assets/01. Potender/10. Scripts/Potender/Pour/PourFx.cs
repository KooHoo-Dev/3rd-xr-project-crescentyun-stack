using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Potender
{
    /// <summary>
    /// 붓기 연출 스위치 (씬에 1개, 오브젝트 "PourFx").
    ///  1) 포물선 물줄기 (굵게→가늘게, 판정도 곡선을 따라감)
    ///  2) 기울기만큼 붓는 양 + 물줄기 굵기, 켜고 끌 때 부드럽게
    ///  3) 떨어지는 곳에 물방울 튐
    ///  4) 햅틱 (붓기 · 흔들기 · 얼음)
    ///  5) 액체 수면: 기울여도 수평 + 움직이면 출렁임, 물줄기가 수면까지 들어가고 그 자리에서 튐
    /// ★ 되돌리기: 이 컴포넌트를 끄거나 오브젝트를 지우면 예전 붓기 그대로 (메뉴 Potender ▸ Day8 붓기 연출 되돌리기).
    ///   항목별 체크박스로 하나씩 끌 수도 있음 — Play 중에도 바로 반영돼서 비교 가능.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PourFx : MonoBehaviour
    {
        public static PourFx I { get; private set; }
        static bool On => I && I.isActiveAndEnabled;

        [Header("1) 포물선 물줄기")]
        public bool arc = true;
        [Tooltip("주둥이에서 나가는 속도 (m/s). 클수록 멀리 휨")]
        public float exitSpeed = 0.35f;
        [Tooltip("곡선 계산 간격(초). 작을수록 부드러움")]
        public float arcStep = 0.03f;
        [Tooltip("물줄기 끝 굵기 비율 (주둥이 = 1)")]
        [Range(0.1f, 1f)] public float endWidth = 0.4f;

        [Header("2) 기울기 → 붓는 양")]
        public bool tiltFlow = true;
        [Tooltip("붓기 시작 각도에서 이만큼 더 기울이면 최대로 콸콸")]
        public float tiltRange = 50f;
        [Tooltip("막 기울였을 때 양 (최대 = 1)")]
        [Range(0.05f, 1f)] public float minFlow = 0.25f;
        [Tooltip("물줄기가 나타나고 사라지는 시간")]
        public float fadeTime = 0.12f;

        [Header("3) 튐")]
        public bool splash = true;
        [Tooltip("물방울 머티리얼 (비우면 물줄기 머티리얼 사용)")]
        public Material splashMaterial;
        [Tooltip("초당 물방울 수 (최대로 부을 때)")]
        public float splashRate = 45f;
        [Tooltip("용기가 아닌 곳(바닥 등)에 쏟을 때 배수")]
        public float spillMultiplier = 2.5f;

        [Header("4) 햅틱")]
        public bool haptics = true;
        [Range(0f, 1f)] public float pourAmp = 0.15f;
        [Range(0f, 1f)] public float shakeAmp = 0.45f;
        [Range(0f, 1f)] public float iceAmp = 0.5f;

        [Header("5) 액체 수면")]
        public bool liquidSurface = true;
        [Tooltip("출렁임 복원력 (클수록 빨리 출렁)")]
        public float wobbleStiffness = 90f;
        [Tooltip("출렁임 감쇠 (클수록 빨리 잔잔해짐)")]
        public float wobbleDamping = 3.5f;
        [Tooltip("용기 움직임 → 출렁임 세기")]
        public float wobbleFromMotion = 0.6f;
        [Tooltip("최대 기울기 (tan, 0.4 ≒ 22°)")]
        public float maxWobble = 0.4f;
        [Tooltip("붓는 동안 수면 흔들림 세기")]
        public float pourWobble = 5f;
        [Tooltip("붓는 동안 수면이 들어오는 재료 색으로 물드는 정도 (멈추면 서서히 섞임)")]
        [Range(0f, 1f)] public float pourTint = 0.75f;
        [Tooltip("붓는 동안 수면 거품 (흰색 섞기)")]
        [Range(0f, 1f)] public float foam = 0.3f;
        [Tooltip("붓는 동안 수면 들썩임 높이 (m)")]
        public float pourBob = 0.003f;

        public static bool Arc => On && I.arc;
        public static bool LiquidSurface => On && I.liquidSurface;
        public static bool TiltFlow => On && I.tiltFlow;
        public static bool Splash => On && I.splash;
        public static bool Haptics => On && I.haptics;

        ParticleSystem splashPs;
        readonly List<(IngredientBottle b, XRGrabInteractable g)> bottles = new();
        readonly List<(MixingContainer c, XRGrabInteractable g)> containers = new();
        readonly List<(ShakeDetector s, XRGrabInteractable g)> shakers = new();
        readonly Dictionary<XRGrabInteractable, float> lastPulse = new();

        void OnEnable() => I = this;
        void OnDisable() { if (I == this) I = null; }

        void Start()
        {
            foreach (var b in FindObjectsByType<IngredientBottle>(FindObjectsSortMode.None)) bottles.Add((b, b.GetComponent<XRGrabInteractable>()));
            foreach (var c in FindObjectsByType<MixingContainer>(FindObjectsSortMode.None))
            {
                var g = c.GetComponent<XRGrabInteractable>();
                containers.Add((c, g));
                c.onIceAdded.AddListener(() => Pulse(g, iceAmp * 0.6f, 0.05f));
            }
            foreach (var s in FindObjectsByType<ShakeDetector>(FindObjectsSortMode.None)) shakers.Add((s, s.GetComponent<XRGrabInteractable>()));
        }

        // ───────── 1·2) 흐름 계산 ─────────
        /// <summary>붓는 양 0~1. tiltFlow 꺼져 있으면 항상 1 (예전과 같음)</summary>
        public static float FlowFor(float angle, float pourAngle)
        {
            if (!TiltFlow) return 1f;
            float t = Mathf.InverseLerp(pourAngle, pourAngle + Mathf.Max(1f, I.tiltRange), angle);
            return Mathf.Lerp(I.minFlow, 1f, t);
        }

        /// <summary>주둥이에서 나가는 초기 속도 (용기 위쪽 방향). arc 꺼져 있으면 0</summary>
        public static Vector3 ExitVelocity(Transform container, float flow01) =>
            Arc ? container.up * (I.exitSpeed * Mathf.Lerp(0.6f, 1f, flow01)) : Vector3.zero;

        // ───────── 3) 튐 ─────────
        public void EmitSplash(Vector3 point, Vector3 normal, Color color, int count, bool spill, Material fallback)
        {
            if (count <= 0) return;
            if (!splashPs) CreateSplash(fallback);
            if (!splashPs) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float up = Random.Range(0.25f, 0.6f) * (spill ? 1.5f : 1f);
                ep.position = point + normal * 0.004f;
                ep.velocity = normal * up + Random.insideUnitSphere * (spill ? 0.45f : 0.25f);
                ep.startColor = color;
                ep.startSize = Random.Range(0.005f, 0.011f);
                ep.startLifetime = Random.Range(0.25f, 0.45f);
                splashPs.Emit(ep, 1);
            }
        }

        void CreateSplash(Material fallback)
        {
            var mat = splashMaterial ? splashMaterial : fallback;
            if (!mat) return;
            var go = new GameObject("PourFx_Splash");
            go.transform.SetParent(transform, false);
            splashPs = go.AddComponent<ParticleSystem>();
            splashPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = splashPs.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;
            main.startSpeed = 0f;
            main.maxParticles = 400;
            var em = splashPs.emission; em.enabled = false;
            var sh = splashPs.shape; sh.enabled = false;
            var sol = splashPs.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            splashPs.Play();
        }

        // ───────── 4) 햅틱 ─────────
        void Update()
        {
            if (!haptics) return;
            foreach (var (b, g) in bottles)
                if (b && b.IsPouring) Pulse(g, pourAmp * Mathf.Lerp(0.4f, 1f, b.Flow01), 0.08f, true);
            foreach (var (c, g) in containers)
                if (c && c.IsPouring) Pulse(g, pourAmp * Mathf.Lerp(0.4f, 1f, c.Flow01), 0.08f, true);
            foreach (var (s, g) in shakers)
                if (s && s.IsShaking) Pulse(g, shakeAmp * Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(0.8f, 4f, s.CurrentSpeed)), 0.08f, true);
        }

        /// <summary>그 물건을 잡고 있는 손에 진동. throttle=true면 0.06초에 한 번만</summary>
        public static void Pulse(XRGrabInteractable grab, float amp, float duration, bool throttle = false)
        {
            if (!Haptics || !grab || !grab.isSelected || amp <= 0f) return;
            if (throttle)
            {
                if (I.lastPulse.TryGetValue(grab, out float t) && Time.time - t < 0.06f) return;
                I.lastPulse[grab] = Time.time;
            }
            foreach (var it in grab.interactorsSelecting)
                if (it is XRBaseInputInteractor input) input.SendHapticImpulse(amp, duration);
        }
    }
}
