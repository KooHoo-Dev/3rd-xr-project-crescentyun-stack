using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 쉐이커 흔들기: 뚜껑 닫힘 + 내용물 있음 상태에서 minSpeed 이상으로 움직인 '거리'를 누적.
    /// requiredDistance(m)만큼 쌓이면 섞기 완료.
    /// </summary>
    [RequireComponent(typeof(MixingContainer))]
    public class ShakeDetector : MonoBehaviour
    {
        [Tooltip("이 속도(m/s) 이상일 때만 누적 — 그냥 들고 옮기는 건 무시")]
        [SerializeField] float minSpeed = 0.8f;
        [Tooltip("이 속도 초과는 프레임 튐/순간이동으로 보고 무시")]
        [SerializeField] float maxSpeed = 8f;
        [Tooltip("완료까지 필요한 누적 이동거리(m). 6 ≒ 3~4초 세게 흔들기")]
        [SerializeField] float requiredDistance = 6f;

        MixingContainer container;
        Vector3 lastPos;

        public float CurrentSpeed { get; private set; }
        public bool IsShaking { get; private set; }

        void Awake() => container = GetComponent<MixingContainer>();
        void OnEnable() => lastPos = transform.position;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 p = transform.position;
            CurrentSpeed = Vector3.Distance(p, lastPos) / dt;
            lastPos = p;

            IsShaking = container.LidClosed && container.Total > 0f
                        && CurrentSpeed >= minSpeed && CurrentSpeed <= maxSpeed;
            if (IsShaking)
                container.AddMixProgress(MixMethod.Shake, CurrentSpeed * dt / requiredDistance);
        }
    }
}
