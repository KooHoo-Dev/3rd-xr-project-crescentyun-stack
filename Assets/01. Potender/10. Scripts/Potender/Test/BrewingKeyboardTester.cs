using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Potender
{
    /// <summary>
    /// 헤드셋 없이 에디터에서 제조 시스템 테스트용 (키보드).
    ///  1, 2 (누르고 있기) : 해당 병을 쉐이커 입구 위로 옮겨서 기울이기 (떼면 제자리)
    ///  L                  : 뚜껑 빼기 / 끼우기 (실제 Shaker_Lid를 소켓에서 빼고 넣음)
    ///  Space (누르고 있기) : 쉐이커 위아래로 흔들기
    ///  P (누르고 있기)     : 쉐이커를 서빙 잔 위로 옮겨서 따르기 (Glass 슬롯 비우면 제자리에서 기울임)
    /// ※ Play 시 병/쉐이커 Rigidbody를 자동으로 Kinematic으로 바꿈. VR 테스트할 땐 이 오브젝트 끄기!
    /// </summary>
    public class BrewingKeyboardTester : MonoBehaviour
    {
        [SerializeField] MixingContainer shaker;
        [SerializeField] IngredientBottle[] bottles;
        [SerializeField] ServingGlass glass;
        [SerializeField] float tiltAngle = 120f;
        [Tooltip("붓는 병 주둥이를 쉐이커 입구보다 이만큼 위에 둠")]
        [SerializeField] float pourHeight = 0.12f;
        [SerializeField] float shakeAmplitude = 0.1f;
        [SerializeField] float shakeFrequency = 4f;

        Vector3 shakerHome;
        Vector3[] bottleHomes;
        Quaternion[] bottleHomeRots;
        float shakeTime;
        XRSocketInteractor lidSocket;
        Transform lid;

        void Start()
        {
            if (shaker)
            {
                shakerHome = shaker.transform.position;
                MakeKinematic(shaker.gameObject);
                lidSocket = shaker.GetComponentInChildren<XRSocketInteractor>();
            }

            if (glass) MakeKinematic(glass.gameObject);

            bottleHomes = new Vector3[bottles.Length];
            bottleHomeRots = new Quaternion[bottles.Length];
            for (int i = 0; i < bottles.Length; i++)
            {
                if (!bottles[i]) continue;
                bottleHomes[i] = bottles[i].transform.position;
                bottleHomeRots[i] = bottles[i].transform.rotation;
                MakeKinematic(bottles[i].gameObject);
            }
        }

        static void MakeKinematic(GameObject go)
        {
            if (go.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
            // Kinematic 물체는 던질 수 없어서 놓을 때 경고가 뜸 → 테스트 중엔 던지기 끔
            if (go.TryGetComponent<XRGrabInteractable>(out var grab)) grab.throwOnDetach = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !shaker) return;

            // 소켓에 끼워진 뚜껑 기억해두기
            if (!lid && lidSocket && lidSocket.hasSelection)
                lid = lidSocket.firstInteractableSelected.transform;

            for (int i = 0; i < bottles.Length && i < 9; i++)
            {
                if (!bottles[i]) continue;
                bool pressed = kb[(Key)((int)Key.Digit1 + i)].isPressed;
                if (pressed) PourOverShaker(bottles[i].transform);
                else bottles[i].transform.SetPositionAndRotation(bottleHomes[i], bottleHomeRots[i]);
            }

            if (kb.lKey.wasPressedThisFrame) ToggleLid();

            if (kb.spaceKey.isPressed)
            {
                shakeTime += Time.deltaTime;
                float y = Mathf.Sin(shakeTime * shakeFrequency * 2f * Mathf.PI) * shakeAmplitude;
                shaker.transform.position = shakerHome + Vector3.up * y;
            }
            else
            {
                shakeTime = 0f;
                shaker.transform.position = shakerHome;
            }

            if (kb.pKey.isPressed)
            {
                if (glass) PourOver(shaker.transform, glass.transform, "MouthTrigger");
                else SetTilt(shaker.transform, tiltAngle);
            }
            else SetTilt(shaker.transform, 0f);
        }

        void SetTilt(Transform t, float z)
        {
            var e = t.eulerAngles;
            t.rotation = Quaternion.Euler(e.x, e.y, z);
        }

        /// <summary>pourer를 기울인 뒤, pourer의 Spout이 target 입구 바로 위에 오도록 옮김</summary>
        void PourOver(Transform pourer, Transform target, string mouthName)
        {
            SetTilt(pourer, tiltAngle);
            Transform spout = pourer.Find("Spout");
            Transform mouth = target.Find(mouthName);
            Vector3 targetPos = (mouth ? mouth.position : target.position) + Vector3.up * pourHeight;
            Vector3 offset = spout ? spout.position - pourer.position : Vector3.zero;
            pourer.position = targetPos - offset;
        }

        void PourOverShaker(Transform bottle) => PourOver(bottle, shaker.transform, "Spout");

        void ToggleLid()
        {
            if (!lidSocket || !lid)
            {
                // 소켓/뚜껑이 없으면 상태값만 바꿈
                if (shaker.LidClosed) shaker.OnLidDetached(); else shaker.OnLidAttached();
                return;
            }

            if (shaker.LidClosed)
            {
                // 뚜껑 빼기: 소켓 끄면 자동으로 OnLidDetached 호출됨 → 뚜껑을 옆에 내려놓음
                lidSocket.socketActive = false;
                lid.position = shaker.transform.position + shaker.transform.forward * -0.15f + Vector3.up * 0.05f;
            }
            else
            {
                // 뚜껑 끼우기: 소켓 위치로 옮기고 소켓 켜면 자동으로 끼워짐 → OnLidAttached
                if (lid.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                lid.SetPositionAndRotation(lidSocket.transform.position, lidSocket.transform.rotation);
                lidSocket.socketActive = true;
            }
        }
    }
}