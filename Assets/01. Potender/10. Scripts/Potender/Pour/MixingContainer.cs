using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Potender
{
    /// <summary>
    /// 쉐이커 / 믹싱 글라스 공용.
    /// 재료 받기 (+ 얼음: IceIntake가 AddIce 호출) → (흔들기/젓기 진행도) → 기울여 잔에 따르면 판정 → 성공: 잔 채움 / 실패: 폭발 후 비움.
    /// 잔이 아닌 곳(바닥 등)에 따르면 판정 없이 그냥 버려짐 = 리셋 수단.
    /// </summary>
    public class MixingContainer : MonoBehaviour
    {
        [Header("용량 / 참조")]
        [SerializeField] float capacity = 1f;
        [SerializeField] RecipeBook recipeBook;
        [SerializeField] LiquidVisual liquid;

        [Header("따르기")]
        [SerializeField] Transform spout;
        [SerializeField] float pourAngle = 95f;
        [SerializeField] float pourRate = 0.5f;
        [SerializeField] float streamLength = 1.2f;
        [SerializeField] LayerMask pourMask = ~0;
        [SerializeField] PourStream stream;

        [Header("실패 연출")]
        [SerializeField] GameObject explosionPrefab;
        [SerializeField] float explosionLifetime = 3f;

        [Header("얼음")]
        [SerializeField] int maxIce = 6;
        [Tooltip("쉐이커 안에 보이는 얼음 (넣은 개수만큼 앞에서부터 켜짐)")]
        [SerializeField] GameObject[] iceVisuals;
        [Tooltip("얼음 비주얼 부모 — 액체 높이를 따라 위아래로 떠다님 (선택)")]
        [SerializeField] Transform iceFloatRoot;
        [Tooltip("iceFloatRoot 로컬 Y: 비었을 때 ~ 가득 찼을 때")]
        [SerializeField] Vector2 iceFloatY = new(0.03f, 0.19f);
        [Tooltip("액체 없이 얼음만 있을 때 이 각도 이상 기울이면 얼음을 쏟아 버림")]
        [SerializeField] float iceDumpAngle = 110f;

        [Header("이벤트 (사운드/햅틱 연결용)")]
        public UnityEvent onIceAdded;
        public UnityEvent onMixed;
        public UnityEvent onSuccess;
        public UnityEvent onFail;
        public event Action<JudgeResult> Judged;
        /// <summary>섞기(흔들기/젓기) 완료 순간 — 지금 내용물로 판정 미리보기 (실제 판정은 잔에 따를 때)</summary>
        public event Action<JudgeResult> Mixed;

        readonly Dictionary<IngredientData, float> contents = new();
        readonly List<IngredientData> keyBuffer = new();
        JudgeResult? cachedResult;
        bool warnedFull, warnedIce;
        bool mixWasReset; // 섞은 뒤 재료를 더 넣어서 진행도가 0이 됨

        public IReadOnlyDictionary<IngredientData, float> Contents => contents;
        public float Total { get; private set; }
        public float Capacity => capacity;
        public bool LidClosed { get; private set; }
        public float MixProgress { get; private set; }          // 0~1
        public MixMethod MixedBy { get; private set; }          // 1 도달 시 확정
        public bool IsPouring { get; private set; }
        /// <summary>따르는 양 0~1 (PourFx 기울기 연출 꺼져 있으면 따르는 동안 1)</summary>
        public float Flow01 { get; private set; }
        readonly Vector3[] path = new Vector3[32];
        public bool CanReceive => !LidClosed && Total < capacity;
        public LiquidVisual Liquid => liquid;
        public int IceCount { get; private set; }
        public int MaxIce => maxIce;
        public bool CanReceiveIce => !LidClosed && IceCount < maxIce;

        void Awake() => RefreshIce();

        // ───────── 재료 ─────────
        public void AddIngredient(IngredientData ing, float amount)
        {
            if (!ing || amount <= 0f) return;
            if (!CanReceive)
            {
                if (!warnedFull)
                {
                    Debug.Log(LidClosed ? "[Potender] 뚜껑이 닫혀 있어서 재료가 안 들어감" : "[Potender] 쉐이커가 가득 참 — 바닥에 버려서 비우세요");
                    warnedFull = true;
                }
                return;
            }
            warnedFull = false;
            amount = Mathf.Min(amount, capacity - Total);
            contents.TryGetValue(ing, out float cur);
            contents[ing] = cur + amount;
            Total += amount;

            ResetMix();
            RefreshVisual();
        }

        // ───────── 얼음 (IceIntake가 호출) ─────────
        /// <summary>얼음 1개 넣기. 성공하면 true → 호출한 쪽에서 얼음 큐브를 없앰</summary>
        public bool AddIce()
        {
            if (!CanReceiveIce)
            {
                if (!warnedIce)
                {
                    Debug.Log(LidClosed ? "[Potender] 뚜껑이 닫혀 있어서 얼음이 안 들어감" : $"[Potender] 얼음은 {maxIce}개까지만");
                    warnedIce = true;
                }
                return false;
            }
            warnedIce = false;
            IceCount++;
            ResetMix();
            RefreshIce();
            Debug.Log($"[Potender] 얼음 추가 ({IceCount}개)");
            onIceAdded?.Invoke();
            return true;
        }

        // 재료/얼음을 더 넣으면 다시 섞어야 함
        void ResetMix()
        {
            if (MixProgress > 0f) mixWasReset = true;
            MixProgress = 0f;
            MixedBy = MixMethod.None;
            cachedResult = null;
        }

        // ───────── 섞기 (ShakeDetector / StirDetector가 호출) ─────────
        public void AddMixProgress(MixMethod method, float delta)
        {
            if (Total <= 0f || MixProgress >= 1f || delta <= 0f) return;
            MixProgress = Mathf.Min(1f, MixProgress + delta);
            if (MixProgress >= 1f)
            {
                MixedBy = method;
                mixWasReset = false;
                Debug.Log($"[Potender] 섞기 완료 ({method})");
                onMixed?.Invoke();
                if (recipeBook) Mixed?.Invoke(recipeBook.Evaluate(contents, MixedBy, IceCount));
            }
        }

        // ───────── 뚜껑 (Socket Interactor의 Select Entered / Exited에 연결) ─────────
        public void OnLidAttached() { LidClosed = true; warnedIce = false; Debug.Log("[Potender] 뚜껑 닫힘"); }
        public void OnLidDetached() { LidClosed = false; warnedIce = false; Debug.Log("[Potender] 뚜껑 열림"); }

        public void Clear()
        {
            contents.Clear();
            Total = 0f;
            IceCount = 0;
            MixProgress = 0f;
            MixedBy = MixMethod.None;
            cachedResult = null;
            mixWasReset = false;
            RefreshVisual();
            RefreshIce();
        }

        // ───────── 따르기 ─────────
        void Update()
        {
            // 얼음만 들어 있을 때 거꾸로 들면 얼음을 쏟아 버림 (리셋 수단)
            if (Total <= 0f && IceCount > 0 && !LidClosed && PourUtil.IsTilted(transform, iceDumpAngle))
            {
                Debug.Log("[Potender] 얼음을 쏟아 버림");
                Clear();
            }

            float angle = PourUtil.TiltAngle(transform);
            IsPouring = !LidClosed && Total > 0f && spout && angle >= pourAngle;
            if (!IsPouring) { Flow01 = 0f; if (stream) stream.Hide(); return; }

            Flow01 = PourFx.FlowFor(angle, pourAngle);
            float amount = Mathf.Min(pourRate * Flow01 * Time.deltaTime, Total);
            Vector3 v0 = PourFx.ExitVelocity(transform, Flow01);
            bool hitAny = PourUtil.Trace(transform, spout.position, v0, streamLength, pourMask, path, out int n, out var hit);
            ServingGlass glass = hitAny ? hit.collider.GetComponentInParent<ServingGlass>() : null;
            if (glass) PourIntoLiquid(glass.Liquid, ref n, ref hit, CurrentColor(), Flow01, path);

            if (stream) stream.Show(path, n, CurrentColor(), Flow01, hitAny, hit, !glass);

            if (glass)
            {
                cachedResult ??= Judge();
                if (!cachedResult.Value.success) { Explode(); return; }
                glass.Receive(cachedResult.Value.recipe, amount);
            }

            Drain(amount);
        }

        /// <summary>PourFx 5: 물줄기를 받는 용기의 수면까지 늘이고, 튐 위치를 수면으로, 수면 흔들기</summary>
        public static void PourIntoLiquid(LiquidVisual target, ref int n, ref RaycastHit hit, Color color, float flow01, Vector3[] pts)
        {
            if (!PourFx.LiquidSurface || !target) return;
            Vector3 s = target.SurfacePoint;
            if (pts != null && n < pts.Length) pts[n++] = s;
            hit.point = s;
            hit.normal = Vector3.up;
            target.Pour(color, flow01);
        }

        JudgeResult Judge()
        {
            var r = recipeBook
                ? recipeBook.Evaluate(contents, MixedBy, IceCount)
                : JudgeResult.Fail(null, "RecipeBook 미지정");
            if (!r.success && MixedBy == MixMethod.None && mixWasReset)
                r.reason += "\n(섞은 뒤 재료/얼음을 더 넣어서 섞기가 초기화됨)";

            Debug.Log($"[Potender 판정] {(r.success ? "성공" : "실패")} / {(r.recipe ? r.recipe.potionName : "-")} / {r.reason}");
            Judged?.Invoke(r);
            if (r.success) onSuccess?.Invoke();
            return r;
        }

        void Explode()
        {
            if (stream) stream.Hide();
            if (explosionPrefab)
            {
                var fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                Destroy(fx, explosionLifetime);
            }
            onFail?.Invoke();
            Clear();
        }

        void Drain(float amount)
        {
            if (Total - amount <= 0.001f) { Clear(); return; }

            float factor = (Total - amount) / Total;
            keyBuffer.Clear();
            keyBuffer.AddRange(contents.Keys);
            foreach (var k in keyBuffer) contents[k] *= factor;
            Total -= amount;
            RefreshVisual();
        }

        // ───────── 색 ─────────
        public Color CurrentColor()
        {
            if (Total <= 0f) return Color.clear;
            Color c = Color.black;
            foreach (var kv in contents) c += kv.Key.liquidColor * (kv.Value / Total);
            return c;
        }

        void RefreshVisual()
        {
            if (liquid) liquid.SetTarget(CurrentColor(), Total / capacity);
            if (iceFloatRoot)
            {
                var p = iceFloatRoot.localPosition;
                p.y = Mathf.Lerp(iceFloatY.x, iceFloatY.y, Total / Mathf.Max(capacity, 0.0001f));
                iceFloatRoot.localPosition = p;
            }
        }

        void RefreshIce()
        {
            if (iceVisuals == null) return;
            for (int i = 0; i < iceVisuals.Length; i++)
                if (iceVisuals[i]) iceVisuals[i].SetActive(i < IceCount);
        }
    }
}
