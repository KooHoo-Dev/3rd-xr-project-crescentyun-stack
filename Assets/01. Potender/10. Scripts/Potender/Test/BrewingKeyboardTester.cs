using UnityEngine;
using UnityEngine.InputSystem;

namespace Potender
{
    /// <summary>
    /// 헤드셋 없이 에디터에서 제조 시스템 테스트용 (키보드).
    ///  1, 2, 3 (누르고 있기) : 해당 재료 병 기울이기
    ///  L                     : 쉐이커 뚜껑 닫기/열기
    ///  Space (누르고 있기)    : 쉐이커 위아래로 흔들기
    ///  P (누르고 있기)        : 쉐이커 기울여서 따르기
    /// ※ Play 시 병/쉐이커 Rigidbody를 자동으로 Kinematic으로 바꿈. VR 테스트할 땐 이 오브젝트 끄기!
    /// </summary>
    public class BrewingKeyboardTester : MonoBehaviour
    {
        [SerializeField] MixingContainer shaker;
        [SerializeField] IngredientBottle[] bottles;
        [SerializeField] float tiltAngle = 120f;
        [SerializeField] float shakeAmplitude = 0.1f;
        [SerializeField] float shakeFrequency = 4f;

        Vector3 shakerHome;
        float shakeTime;

        void Start()
        {
            if (shaker)
            {
                shakerHome = shaker.transform.position;
                MakeKinematic(shaker.gameObject);
            }
            foreach (var b in bottles)
                if (b) MakeKinematic(b.gameObject);
        }

        static void MakeKinematic(GameObject go)
        {
            if (go.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            for (int i = 0; i < bottles.Length && i < 9; i++)
            {
                if (!bottles[i]) continue;
                bool pressed = kb[(Key)((int)Key.Digit1 + i)].isPressed;
                SetTilt(bottles[i].transform, pressed ? tiltAngle : 0f);
            }

            if (!shaker) return;

            if (kb.lKey.wasPressedThisFrame)
            {
                if (shaker.LidClosed) shaker.OnLidDetached();
                else shaker.OnLidAttached();
                Debug.Log($"[Tester] 뚜껑 {(shaker.LidClosed ? "닫힘" : "열림")}");
            }

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

            SetTilt(shaker.transform, kb.pKey.isPressed ? tiltAngle : 0f);
        }

        static void SetTilt(Transform t, float z)
        {
            var e = t.eulerAngles;
            t.rotation = Quaternion.Euler(e.x, e.y, z);
        }
    }
}
