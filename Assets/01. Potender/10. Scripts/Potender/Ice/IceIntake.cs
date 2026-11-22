using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 쉐이커 입구의 얼음 투입 트리거 (쉐이커 자식, Trigger 콜라이더와 같이).
    /// 얼음 큐브가 닿으면 MixingContainer.AddIce() → 성공하면 큐브 제거.
    /// 뚜껑이 닫혀 있거나 maxIce를 넘으면 안 들어감.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class IceIntake : MonoBehaviour
    {
        [SerializeField] MixingContainer container;
        [Tooltip("켜면 손에 든 채로 입구에 대기만 해도 쏙 들어감. 끄면 입구 위에서 손을 놓아야 들어감")]
        [SerializeField] bool acceptWhileHeld = true;

        void Awake()
        {
            if (!container) container = GetComponentInParent<MixingContainer>();
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other) => TryTake(other);
        void OnTriggerStay(Collider other) => TryTake(other);

        void TryTake(Collider other)
        {
            if (!container) return;
            var ice = other.GetComponentInParent<IceCube>();
            if (!ice || ice.Used) return;
            if (!acceptWhileHeld && ice.IsHeld) return;
            if (container.AddIce()) ice.Consume();
        }
    }
}
