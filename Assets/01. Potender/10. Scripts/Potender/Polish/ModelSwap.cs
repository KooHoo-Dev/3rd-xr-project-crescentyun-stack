using UnityEngine;

namespace Potender
{
    /// <summary>
    /// Day 5 아트 교체: 캡슐/큐브로 된 손님·고블린 몸을 Synty 캐릭터 모델로 바꿔 보여줌.
    ///  - 생성될 때 models 중 하나를 랜덤으로 골라 자식으로 붙이고, 기존 도형 렌더러(hideRenderers)는 숨김
    ///  - 키는 targetHeight(월드 m)에 맞추고, 발바닥은 기존 도형의 바닥 높이에 맞춤
    ///  - 모델의 콜라이더는 꺼 버림 (투척·서빙 판정은 원래 콜라이더가 담당)
    ///  - 애니메이션이 없으므로 걷는 동안 위아래로 살짝 통통 튀게
    /// 말풍선 / 인내심 바 등 UI는 그대로 둠.
    /// </summary>
    public class ModelSwap : MonoBehaviour
    {
        [Tooltip("이 중 하나를 랜덤으로 사용")]
        [SerializeField] GameObject[] models;
        [Tooltip("모델이 붙으면 숨길 기존 도형 렌더러 (몸통·머리·코·귀)")]
        [SerializeField] Renderer[] hideRenderers;
        [Tooltip("모델 키 (월드 m). 0이면 원래 크기")]
        [SerializeField] float targetHeight = 1.7f;
        [Tooltip("모델 그림자 (Quest 성능 → 기본 끔)")]
        [SerializeField] bool castShadows = false;

        [Header("걷기 흔들림 (애니메이션 없는 모델용)")]
        [SerializeField] float bobHeight = 0.04f;
        [SerializeField] float bobSpeed = 10f;
        [SerializeField] float bobTilt = 4f;

        public Transform Model { get; private set; }

        Vector3 baseLocalPos;
        Quaternion baseLocalRot;
        Vector3 lastPos;
        float bobT, bobWeight;

        void Awake()
        {
            if (models == null || models.Length == 0) return;
            var prefab = models[Random.Range(0, models.Length)];
            if (!prefab) return;

            // 숨기기 전에 기존 도형의 바닥 높이 기록
            float footY = transform.position.y;
            if (TryGetBounds(hideRenderers, out var oldB)) footY = oldB.min.y;

            Model = Instantiate(prefab, transform).transform;
            Model.name = "Model_" + prefab.name;
            Model.localPosition = Vector3.zero;
            Model.localRotation = Quaternion.identity;

            foreach (var c in Model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in Model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            var rs = Model.GetComponentsInChildren<Renderer>(true);
            if (targetHeight > 0f && TryGetBounds(rs, out var b) && b.size.y > 0.01f)
                Model.localScale *= targetHeight / b.size.y;
            if (TryGetBounds(rs, out b))
                Model.position += Vector3.up * (footY - b.min.y);

            if (hideRenderers != null)
                foreach (var r in hideRenderers) if (r) r.enabled = false;

            baseLocalPos = Model.localPosition;
            baseLocalRot = Model.localRotation;
            lastPos = transform.position;
        }

        void LateUpdate()
        {
            if (!Model) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float speed = (transform.position - lastPos).magnitude / dt;
            lastPos = transform.position;
            bobWeight = Mathf.MoveTowards(bobWeight, speed > 0.15f ? 1f : 0f, dt * 6f);
            if (bobWeight <= 0f) { Model.localPosition = baseLocalPos; Model.localRotation = baseLocalRot; return; }

            bobT += dt * bobSpeed;
            float s = transform.lossyScale.y > 0.001f ? 1f / transform.lossyScale.y : 1f;
            Model.localPosition = baseLocalPos + Vector3.up * (Mathf.Abs(Mathf.Sin(bobT)) * bobHeight * s * bobWeight);
            Model.localRotation = baseLocalRot * Quaternion.Euler(0f, 0f, Mathf.Sin(bobT) * bobTilt * bobWeight);
        }

        static bool TryGetBounds(Renderer[] rs, out Bounds b)
        {
            b = default;
            bool any = false;
            if (rs == null) return false;
            foreach (var r in rs)
            {
                if (!r) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
