using UnityEngine;

namespace Potender
{
    /// <summary>
    /// (컷 대상) 믹싱 글라스 젓기: 스푼 끝이 글라스 안(원기둥 영역)에서 중심축을 도는 각도를 누적.
    /// 같은 방향으로 requiredTurns 바퀴 돌리면 완료. 왕복 휘젓기는 상쇄됨.
    /// </summary>
    [RequireComponent(typeof(MixingContainer))]
    public class StirDetector : MonoBehaviour
    {
        [SerializeField] Transform spoonTip;
        [Tooltip("글라스 바닥 중심, up축 = 글라스 위쪽")]
        [SerializeField] Transform glassBottomCenter;
        [SerializeField] float radius = 0.045f;
        [SerializeField] float height = 0.14f;
        [Tooltip("중심에 너무 가까우면 각도가 튀므로 무시")]
        [SerializeField] float minRadius = 0.008f;
        [SerializeField] float requiredTurns = 4f;

        MixingContainer container;
        float accumulated, best;
        float? lastAngle;

        void Awake() => container = GetComponent<MixingContainer>();

        void LateUpdate()
        {
            // 재료 추가로 진행도가 리셋되면 내 누적값도 리셋
            if (container.MixProgress <= 0f && best > 0f) accumulated = best = 0f;

            if (!spoonTip || !glassBottomCenter || container.Total <= 0f) { lastAngle = null; return; }

            Vector3 axis = glassBottomCenter.up;
            Vector3 d = spoonTip.position - glassBottomCenter.position;
            float h = Vector3.Dot(d, axis);
            Vector3 flat = d - axis * h;
            float r = flat.magnitude;

            if (h < 0f || h > height || r > radius || r < minRadius) { lastAngle = null; return; }

            float angle = Vector3.SignedAngle(glassBottomCenter.forward, flat, axis);
            if (lastAngle.HasValue) accumulated += Mathf.DeltaAngle(lastAngle.Value, angle);
            lastAngle = angle;

            float abs = Mathf.Abs(accumulated);
            if (abs > best)
            {
                container.AddMixProgress(MixMethod.Stir, (abs - best) / (360f * requiredTurns));
                best = abs;
            }
        }
    }
}
