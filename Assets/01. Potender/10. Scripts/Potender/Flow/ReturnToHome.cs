using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Potender
{
    /// <summary>
    /// 시작 위치 기억 → 바닥에 떨어지거나 / 쓰러진 채로 놓여 있으면 잠시 후 제자리로 복귀.
    /// 서빙 후 잔 회수에도 사용. 병 / 쉐이커 / 뚜껑 / 잔에 붙이기.
    /// </summary>
    public class ReturnToHome : MonoBehaviour
    {
        [Tooltip("시작 높이보다 이만큼 아래로 떨어지면 '바닥에 떨어짐'으로 판단")]
        [SerializeField] float dropDistance = 0.5f;
        [SerializeField] float returnDelay = 1.5f;

        [Header("쓰러짐 (싱크대에 빠지거나 옆으로 누운 경우)")]
        [SerializeField] bool returnWhenTipped = true;
        [SerializeField] float tipAngle = 70f;
        [SerializeField] float tipDelay = 3f;

        [Header("고정 위치 대신 따라갈 기준 (예: 뚜껑 → 쉐이커의 LidSocket)")]
        [SerializeField] Transform homeAnchor;

        [Tooltip("homeAnchor(뚜껑 → 쉐이커)가 있을 땐 '쓰러짐'으로는 안 돌아감 (뚜껑을 옆으로 눕혀 두는 건 정상). 바닥에 떨어졌을 때만")]
        [SerializeField] bool anchorOnlyWhenFallen = true;

        Vector3 homePos;
        MixingContainer anchorContainer;
        Quaternion homeRot;
        Rigidbody rb;
        XRBaseInteractable interactable;
        float fallenTimer, tippedTimer;

        void Awake()
        {
            homePos = transform.position;
            homeRot = transform.rotation;
            rb = GetComponent<Rigidbody>();
            interactable = GetComponent<XRBaseInteractable>();
            if (homeAnchor) anchorContainer = homeAnchor.GetComponentInParent<MixingContainer>();
        }

        void Update()
        {
            bool held = interactable && interactable.isSelected;

            bool fallen = !held && transform.position.y < HomePos.y - dropDistance;
            fallenTimer = fallen ? fallenTimer + Time.deltaTime : 0f;

            bool tipped = returnWhenTipped && !held && !(homeAnchor && anchorOnlyWhenFallen) && Vector3.Angle(transform.up, Vector3.up) > tipAngle
                          && (!rb || rb.linearVelocity.sqrMagnitude < 0.01f);
            tippedTimer = tipped ? tippedTimer + Time.deltaTime : 0f;

            // 쉐이커가 따르는 중이거나 기울어져 있으면 뚜껑을 씌우지 않고 기다림
            if (anchorContainer && (anchorContainer.IsPouring || PourUtil.IsTilted(anchorContainer.transform, 45f))) return;

            if (fallenTimer >= returnDelay || tippedTimer >= tipDelay) Return();
        }

        Vector3 HomePos => homeAnchor ? homeAnchor.position : homePos;
        Quaternion HomeRot => homeAnchor ? homeAnchor.rotation : homeRot;

        public void Return()
        {
            fallenTimer = tippedTimer = 0f;
            Vector3 p = HomePos;
            Quaternion r = HomeRot;
            if (rb)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.position = p;
                rb.rotation = r;
            }
            transform.SetPositionAndRotation(p, r);
        }
    }
}
