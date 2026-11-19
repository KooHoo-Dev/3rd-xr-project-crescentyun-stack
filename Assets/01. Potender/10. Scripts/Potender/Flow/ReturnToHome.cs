using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Potender
{
    /// <summary>
    /// 시작 위치 기억 → 바닥에 떨어뜨리면 잠시 후 제자리로 복귀. 서빙 후 잔 회수에도 사용.
    /// 병 / 쉐이커 / 뚜껑 / 잔에 붙이기.
    /// </summary>
    public class ReturnToHome : MonoBehaviour
    {
        [Tooltip("시작 높이보다 이만큼 아래로 떨어지면 '바닥에 떨어짐'으로 판단")]
        [SerializeField] float dropDistance = 0.5f;
        [SerializeField] float returnDelay = 1.5f;

        Vector3 homePos;
        Quaternion homeRot;
        Rigidbody rb;
        XRBaseInteractable interactable;
        float fallenTimer;

        void Awake()
        {
            homePos = transform.position;
            homeRot = transform.rotation;
            rb = GetComponent<Rigidbody>();
            interactable = GetComponent<XRBaseInteractable>();
        }

        void Update()
        {
            bool held = interactable && interactable.isSelected;
            bool fallen = !held && transform.position.y < homePos.y - dropDistance;
            fallenTimer = fallen ? fallenTimer + Time.deltaTime : 0f;
            if (fallenTimer >= returnDelay) Return();
        }

        public void Return()
        {
            fallenTimer = 0f;
            if (rb)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.position = homePos;
                rb.rotation = homeRot;
            }
            transform.SetPositionAndRotation(homePos, homeRot);
        }
    }
}
