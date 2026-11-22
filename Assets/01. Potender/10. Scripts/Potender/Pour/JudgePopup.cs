using TMPro;
using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 판정 결과를 쉐이커 위에 잠깐 띄움. 성공: "○○ 포션 완성!" / 실패: "실패! + 이유".
    /// showOnMixed: 다 흔든 순간(섞기 완료)에 미리 알려줌 → 잔에 따를 땐 성공 문구 생략, 실패(폭발)만 다시 표시.
    /// 쉐이커 자식 오브젝트(TextMeshPro + Billboard)에 붙이기.
    /// </summary>
    public class JudgePopup : MonoBehaviour
    {
        [SerializeField] MixingContainer container;
        [SerializeField] TMP_Text text;
        [SerializeField] float showTime = 3.5f;
        [Tooltip("쉐이커 기준 월드 오프셋 (쉐이커가 기울어져도 위쪽에 표시)")]
        [SerializeField] Vector3 worldOffset = new(0f, 0.35f, 0f);
        [Tooltip("다 흔든 순간에 완성/실패를 알려줌 (끄면 예전처럼 잔에 따를 때)")]
        [SerializeField] bool showOnMixed = true;

        float timer;

        void Awake()
        {
            if (!container) container = GetComponentInParent<MixingContainer>();
            if (!text) text = GetComponent<TMP_Text>();
            if (text) text.enabled = false;
        }

        void OnEnable()
        {
            if (!container) return;
            container.Judged += OnJudged;
            container.Mixed += OnMixed;
        }

        void OnDisable()
        {
            if (!container) return;
            container.Judged -= OnJudged;
            container.Mixed -= OnMixed;
        }

        void OnMixed(JudgeResult r)
        {
            if (!showOnMixed) return;
            Show(r.success
                ? $"<color=#7CFF7C><b>{(r.recipe ? r.recipe.potionName : "포션")} 완성!</b></color>\n<size=70%>뚜껑 열고 잔에 따르세요</size>"
                : $"<color=#FF6B6B><b>실패!</b></color>\n<size=70%>{r.reason}\n이대로 따르면 펑! — 바닥에 쏟고 다시</size>");
        }

        void OnJudged(JudgeResult r)
        {
            if (r.success && showOnMixed && container.MixedBy != MixMethod.None) return; // 이미 흔들 때 알려줌
            Show(r.success
                ? $"<color=#7CFF7C><b>{(r.recipe ? r.recipe.potionName : "포션")} 완성!</b></color>"
                : $"<color=#FF6B6B><b>실패!</b></color>\n<size=70%>{r.reason}</size>");
        }

        void Show(string msg)
        {
            if (!text) return;
            text.text = msg;
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
