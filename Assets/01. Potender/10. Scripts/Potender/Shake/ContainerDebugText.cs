using System.Text;
using TMPro;
using UnityEngine;

namespace Potender
{
    /// <summary>헤드셋 테스트용: 용기 위에 내용물/진행도 표시. 시연 전엔 끄기.</summary>
    public class ContainerDebugText : MonoBehaviour
    {
        [SerializeField] MixingContainer container;
        [SerializeField] ShakeDetector shake;   // 선택
        [SerializeField] TMP_Text text;

        readonly StringBuilder sb = new();

        void Update()
        {
            if (!container || !text) return;
            sb.Clear();
            sb.Append($"총량 {container.Total:0.00}/{container.Capacity:0.00}  뚜껑 {(container.LidClosed ? "닫힘" : "열림")}\n");
            foreach (var kv in container.Contents)
                sb.Append($"· {kv.Key.displayName} {kv.Value / Mathf.Max(container.Total, 0.0001f):P0}\n");
            sb.Append($"섞기 {container.MixProgress:P0} ({container.MixedBy})");
            if (shake) sb.Append($"\n속도 {shake.CurrentSpeed:0.0} m/s {(shake.IsShaking ? "◀ 흔드는 중" : "")}");
            text.text = sb.ToString();
        }
    }
}
