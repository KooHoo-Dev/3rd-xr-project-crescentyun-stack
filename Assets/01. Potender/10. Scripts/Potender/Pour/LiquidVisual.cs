using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 용기 안 액체 표시: 높이(스케일 Y) + 색 Lerp.
    /// fillPivot은 '바닥'에 피벗이 있는 빈 오브젝트, 그 자식에 실린더 메쉬를 위로 올려 배치.
    /// PourFx ▸ liquidSurface 켜면: 용기를 기울여도 수평을 유지하는 수면 + 움직이면 출렁임 (끄면 예전 그대로)
    /// </summary>
    public class LiquidVisual : MonoBehaviour
    {
        [SerializeField] Renderer liquidRenderer;
        [SerializeField] Transform fillPivot;
        [Tooltip("URP Lit/Unlit = _BaseColor, Built-in = _Color")]
        [SerializeField] string colorProperty = "_BaseColor";
        [SerializeField] float lerpSpeed = 4f;

        MaterialPropertyBlock mpb;
        int colorId;
        Vector3 baseScale;
        Color current, target;
        float currentFill, targetFill;

        // 수면 (PourFx 5)
        Transform surface;
        Renderer surfaceRenderer;
        Bounds meshBounds;
        Vector3 lastPos, lastVel;
        Vector2 wobble, wobbleVel;
        Color incoming;
        float pourT, pourFlow, lastPourTime = -10f;

        public float Fill01 => currentFill;

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            colorId = Shader.PropertyToID(colorProperty);
            if (!fillPivot) fillPivot = transform;
            baseScale = fillPivot.localScale;
            lastPos = transform.position;
            Apply();
        }

        public void SetTarget(Color color, float fill01)
        {
            target = color;
            targetFill = Mathf.Clamp01(fill01);
            if (currentFill <= 0.001f) current = color; // 빈 용기에 첫 재료: 색은 바로
        }

        /// <summary>보간 없이 즉시 목표 상태로 (잔이 깨졌다 다시 나타날 때 등)</summary>
        public void SnapToTarget()
        {
            current = target;
            currentFill = targetFill;
            wobble = wobbleVel = Vector2.zero;
            lastPos = transform.position;
            lastVel = Vector3.zero;
            Apply();
        }

        /// <summary>액체가 떨어질 때 수면 흔들기</summary>
        public void Disturb(float strength) => wobbleVel += Random.insideUnitCircle * strength;

        /// <summary>지금 이 용기에 붓는 중 (매 프레임 호출) — 수면이 들어오는 색으로 물들고 거품 · 들썩임 · 흔들림</summary>
        public void Pour(Color color, float flow01)
        {
            incoming = color;
            pourFlow = flow01;
            lastPourTime = Time.time;
            if (PourFx.LiquidSurface) Disturb(PourFx.I.pourWobble * flow01 * Time.deltaTime);
        }

        /// <summary>현재 수면 중심 (월드). 비어 있으면 바닥</summary>
        public Vector3 SurfacePoint
        {
            get
            {
                if (!liquidRenderer || currentFill <= 0.001f) return fillPivot ? fillPivot.position : transform.position;
                var mf = liquidRenderer.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) return liquidRenderer.bounds.center;
                var b = mf.sharedMesh.bounds;
                return liquidRenderer.transform.TransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            }
        }

        void Update()
        {
            float k = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            current = Color.Lerp(current, target, k);
            currentFill = Mathf.Lerp(currentFill, targetFill, k);
            Apply();
            UpdateSurface();
        }

        void Apply()
        {
            if (!liquidRenderer) return;
            bool visible = currentFill > 0.001f;
            liquidRenderer.enabled = visible;
            if (!visible) return;

            var s = baseScale;
            s.y *= currentFill;
            fillPivot.localScale = s;

            liquidRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(colorId, current);
            liquidRenderer.SetPropertyBlock(mpb);
        }

        // ───────── 수평 수면 + 출렁임 ─────────
        void UpdateSurface()
        {
            float dt = Time.deltaTime;
            bool on = PourFx.LiquidSurface && liquidRenderer && currentFill > 0.001f && dt > 0f;
            if (!on)
            {
                if (surfaceRenderer) surfaceRenderer.enabled = false;
                lastPos = transform.position;
                lastVel = Vector3.zero;
                return;
            }
            if (!surface && !CreateSurface()) return;

            var fx = PourFx.I;
            // 용기 가속도 → 출렁임 (액체가 뒤로 쏠림). 순간이동은 무시
            Vector3 vel = (transform.position - lastPos) / dt;
            Vector3 acc = Vector3.ClampMagnitude((vel - lastVel) / dt, 30f);
            if (vel.magnitude > 5f) acc = Vector3.zero;
            lastPos = transform.position;
            lastVel = vel;

            wobbleVel += (-wobble * fx.wobbleStiffness - new Vector2(acc.x, acc.z) * fx.wobbleFromMotion) * dt;
            wobbleVel *= Mathf.Exp(-fx.wobbleDamping * dt);
            wobble += wobbleVel * dt;
            wobble = Vector2.ClampMagnitude(wobble, fx.maxWobble);

            // 위치: 메쉬 윗면 중심(용기 축 위) / 회전: 월드 수평 + 출렁임 / 크기: 메쉬 단면 (수평 원은 기울어진 원통 안에 항상 들어감)
            var t = liquidRenderer.transform;
            Vector3 top = t.TransformPoint(new Vector3(meshBounds.center.x, meshBounds.max.y, meshBounds.center.z));
            Vector3 normal = new Vector3(wobble.x, 1f, wobble.y).normalized;
            // 붓는 중: 0.1초 안에 Pour가 또 오면 계속 붓는 걸로
            bool pouring = Time.time - lastPourTime < 0.1f;
            pourT = Mathf.MoveTowards(pourT, pouring ? 1f : 0f, dt * (pouring ? 6f : 0.8f));
            float bob = fx.pourBob * pourT * pourFlow * (1f + Mathf.Sin(Time.time * 23f) * 0.6f);
            surface.SetPositionAndRotation(top + Vector3.up * (0.0015f + bob), Quaternion.FromToRotation(Vector3.up, normal));

            Vector3 ls = t.lossyScale;
            float dia = Mathf.Min(meshBounds.size.x * ls.x, meshBounds.size.z * ls.z) * 0.97f;
            float thin = 0.002f / Mathf.Max(1e-4f, meshBounds.size.y);
            surface.localScale = new Vector3(dia, thin, dia); // 부모 없음 → 월드 크기 그대로

            surfaceRenderer.enabled = true;
            // 수면 색: 들어오는 재료 색 → (멈추면) 서서히 전체 색으로, 붓는 동안 거품(흰색)
            Color c = Color.Lerp(current, incoming, pourT * fx.pourTint);
            c = Color.Lerp(c, Color.white, pourT * pourFlow * fx.foam);
            c.a = current.a;
            surfaceRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(colorId, c);
            surfaceRenderer.SetPropertyBlock(mpb);
        }

        bool CreateSurface()
        {
            var mf = liquidRenderer.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return false;
            meshBounds = mf.sharedMesh.bounds;
            // 부모 없이 (fillPivot은 높이만큼 스케일이 변해서) — 매 프레임 월드 위치를 직접 맞춤
            var go = new GameObject($"LiquidSurface ({name})");
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            surfaceRenderer = go.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = liquidRenderer.sharedMaterial;
            surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            surface = go.transform;
            return true;
        }

        void OnDisable() { if (surfaceRenderer) surfaceRenderer.enabled = false; }
        void OnDestroy() { if (surface) Destroy(surface.gameObject); }
    }
}
