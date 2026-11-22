using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 따르는 물줄기(LineRenderer). 머티리얼은 버텍스 컬러를 쓰는 URP/Particles/Unlit 권장.
    /// PourFx가 없으면 예전 그대로(직선 2점, 굵기 고정, 즉시 켜고 끔).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PourStream : MonoBehaviour
    {
        LineRenderer lr;
        float baseWidth;
        AnimationCurve baseCurve;
        float fade, flowVis, targetFade, splashAcc;
        AnimationCurve taper;
        float taperEnd;

        void Awake()
        {
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.enabled = false;
            baseWidth = lr.widthMultiplier;
            baseCurve = lr.widthCurve;
        }

        /// <summary>예전 방식 (직선)</summary>
        public void Show(Vector3 from, Vector3 to, Color color)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            Apply(color, 1f);
        }

        /// <summary>경로 + 흐름 양 + (맞은 곳 튐)</summary>
        public void Show(Vector3[] pts, int count, Color color, float flow01, bool hasHit, RaycastHit hit, bool spill)
        {
            lr.positionCount = count;
            for (int i = 0; i < count; i++) lr.SetPosition(i, pts[i]);
            Apply(color, flow01);

            if (hasHit && PourFx.Splash)
            {
                splashAcc += PourFx.I.splashRate * flow01 * (spill ? PourFx.I.spillMultiplier : 1f) * Time.deltaTime;
                int n = Mathf.FloorToInt(splashAcc);
                splashAcc -= n;
                PourFx.I.EmitSplash(hit.point, hit.normal, color, n, spill, lr.sharedMaterial);
            }
        }

        void Apply(Color color, float flow01)
        {
            lr.startColor = lr.endColor = color;
            if (PourFx.Arc)
            {
                if (taper == null || !Mathf.Approximately(taperEnd, PourFx.I.endWidth))
                {
                    taperEnd = PourFx.I.endWidth;
                    taper = AnimationCurve.Linear(0f, 1f, 1f, taperEnd);
                }
                lr.widthCurve = taper;
            }
            else lr.widthCurve = baseCurve;
            flowVis = flow01;
            targetFade = 1f;
            if (!PourFx.TiltFlow) { fade = 1f; lr.widthMultiplier = baseWidth; lr.enabled = true; }
        }

        public void Hide()
        {
            if (!lr) return;
            targetFade = 0f;
            splashAcc = 0f;
            if (!PourFx.TiltFlow) { fade = 0f; lr.enabled = false; }
        }

        void LateUpdate()
        {
            if (!PourFx.TiltFlow) return;
            float speed = 1f / Mathf.Max(0.01f, PourFx.I.fadeTime);
            fade = Mathf.MoveTowards(fade, targetFade, speed * Time.deltaTime);
            lr.enabled = fade > 0.01f;
            lr.widthMultiplier = baseWidth * Mathf.Lerp(0.45f, 1f, flowVis) * fade;
        }
    }
}
