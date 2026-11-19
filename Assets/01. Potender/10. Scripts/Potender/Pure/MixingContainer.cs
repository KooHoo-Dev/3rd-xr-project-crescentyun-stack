using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Potender
{
    /// <summary>
    /// 쉐이커 / 믹싱 글라스 공용.
    /// 재료 받기 → (흔들기/젓기 진행도) → 기울여 잔에 따르면 판정 → 성공: 잔 채움 / 실패: 폭발 후 비움.
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

        [Header("이벤트 (사운드/햅틱 연결용)")]
        public UnityEvent onMixed;
        public UnityEvent onSuccess;
        public UnityEvent onFail;
        public event Action<JudgeResult> Judged;

        readonly Dictionary<IngredientData, float> contents = new();
        readonly List<IngredientData> keyBuffer = new();
        JudgeResult? cachedResult;
        bool warnedFull;

        public IReadOnlyDictionary<IngredientData, float> Contents => contents;
        public float Total { get; private set; }
        public float Capacity => capacity;
        public bool LidClosed { get; private set; }
        public float MixProgress { get; private set; }          // 0~1
        public MixMethod MixedBy { get; private set; }          // 1 도달 시 확정
        public bool IsPouring { get; private set; }
        public bool CanReceive => !LidClosed && Total < capacity;

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

            // 재료를 더 넣으면 다시 섞어야 함
            MixProgress = 0f;
            MixedBy = MixMethod.None;
            cachedResult = null;
            RefreshVisual();
        }

        // ───────── 섞기 (ShakeDetector / StirDetector가 호출) ─────────
        public void AddMixProgress(MixMethod method, float delta)
        {
            if (Total <= 0f || MixProgress >= 1f || delta <= 0f) return;
            MixProgress = Mathf.Min(1f, MixProgress + delta);
            if (MixProgress >= 1f)
            {
                MixedBy = method;
                Debug.Log($"[Potender] 섞기 완료 ({method})");
                onMixed?.Invoke();
            }
        }

        // ───────── 뚜껑 (Socket Interactor의 Select Entered / Exited에 연결) ─────────
        public void OnLidAttached() { LidClosed = true; Debug.Log("[Potender] 뚜껑 닫힘"); }
        public void OnLidDetached() { LidClosed = false; Debug.Log("[Potender] 뚜껑 열림"); }

        public void Clear()
        {
            contents.Clear();
            Total = 0f;
            MixProgress = 0f;
            MixedBy = MixMethod.None;
            cachedResult = null;
            RefreshVisual();
        }

        // ───────── 따르기 ─────────
        void Update()
        {
            IsPouring = !LidClosed && Total > 0f && spout && PourUtil.IsTilted(transform, pourAngle);
            if (!IsPouring) { if (stream) stream.Hide(); return; }

            float amount = Mathf.Min(pourRate * Time.deltaTime, Total);
            Vector3 from = spout.position;
            Vector3 to = from + Vector3.down * streamLength;
            ServingGlass glass = null;

            if (PourUtil.CastDown(transform, from, streamLength, pourMask, out var hit))
            {
                to = hit.point;
                glass = hit.collider.GetComponentInParent<ServingGlass>();
            }

            if (stream) stream.Show(from, to, CurrentColor());

            if (glass)
            {
                cachedResult ??= Judge();
                if (!cachedResult.Value.success) { Explode(); return; }
                glass.Receive(cachedResult.Value.recipe, amount);
            }

            Drain(amount);
        }

        JudgeResult Judge()
        {
            var r = recipeBook
                ? recipeBook.Evaluate(contents, MixedBy)
                : JudgeResult.Fail(null, "RecipeBook 미지정");

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
        }
    }
}
