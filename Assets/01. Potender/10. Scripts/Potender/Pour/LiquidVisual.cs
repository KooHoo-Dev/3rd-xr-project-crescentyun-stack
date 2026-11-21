using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 용기 안 액체 표시: 높이(스케일 Y) + 색 Lerp.
    /// fillPivot은 '바닥'에 피벗이 있는 빈 오브젝트, 그 자식에 실린더 메쉬를 위로 올려 배치.
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

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            colorId = Shader.PropertyToID(colorProperty);
            if (!fillPivot) fillPivot = transform;
            baseScale = fillPivot.localScale;
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
            Apply();
        }

        void Update()
        {
            float k = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            current = Color.Lerp(current, target, k);
            currentFill = Mathf.Lerp(currentFill, targetFill, k);
            Apply();
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
    }
}
