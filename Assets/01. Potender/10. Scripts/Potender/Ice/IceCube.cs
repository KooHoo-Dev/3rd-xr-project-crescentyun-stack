using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Potender
{
    /// <summary>
    /// 얼음 큐브: 잡아서 쉐이커 입구(IceIntake)에 갖다 대면 사라지고 쉐이커 IceCount +1.
    /// 손에서 떨어진 채로 despawnDelay초 지나면 자동 정리 (바닥에 흘린 얼음 = 녹음).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class IceCube : MonoBehaviour
    {
        [Tooltip("손에서 놓인 뒤 이 시간(초)이 지나면 사라짐. 0이면 안 사라짐")]
        [SerializeField] float despawnDelay = 8f;

        XRGrabInteractable grab;
        float idle;

        public bool Used { get; private set; }
        public bool IsHeld => grab && grab.isSelected;

        void Awake() => grab = GetComponent<XRGrabInteractable>();

        void Update()
        {
            if (Used) return;
            if (IsHeld) { idle = 0f; return; }
            idle += Time.deltaTime;
            if (despawnDelay > 0f && idle >= despawnDelay) Destroy(gameObject);
        }

        /// <summary>쉐이커에 들어감 → 손에서 떼고 제거</summary>
        public void Consume()
        {
            if (Used) return;
            Used = true;
            PourFx.Pulse(grab, PourFx.I ? PourFx.I.iceAmp : 0f, 0.06f); // 얼음 넣는 손에 톡
            if (grab && grab.isSelected && grab.interactionManager)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
