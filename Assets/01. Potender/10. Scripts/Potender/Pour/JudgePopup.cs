using TMPro;
using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 판정 결과를 쉐이커 위에 잠깐 띄움. 성공: "○○ 포션 완성!" / 실패: "실패! + 이유".
    /// 쉐이커 자식 오브젝트(TextMeshPro + Billboard)에 붙이기.
    /// </summary>
    public class JudgePopup : MonoBehaviour
    {
        [SerializeField] MixingContainer container;
        [SerializeField] TMP_Text text;
        [SerializeField] float showTime = 3.5f;
        [Tooltip("쉐이커 기준 월드 오프셋 (쉐이커가 기울어져도 위쪽에 표시)")]
        [SerializeField] Vector3 worldOffset = new(0f, 0.35f, 0f);

        float timer;

        void Awake()
        {
            if (!container) container = GetComponentInParent<MixingContainer>();
            if (!text) text = GetComponent<TMP_Text>();
            if (text) text.enabled = false;
        }

        void OnEnable() { if (container) container.Judged += Show; }
        void OnDisable() { if (container) container.Judged -= Show; }

        void Show(JudgeResult r)
        {
            if (!text) return;
            text.text = r.success
                ? $"<color=#7CFF7C><b>{(r.recipe ? r.recipe.potionName : "포션")} 완성!</b></color>"
                : $"<color=#FF6B6B><b>실패!</b></color>\n<size=70%>{r.reason}</size>";
            text.enabled = true;
            timer = showTime;
        }

        void LateUpdate()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            if (container) transform.position = container.transform.position + worldOffset;
            if (timer <= 0f && text) text.enabled = false;
        }
    }
}
