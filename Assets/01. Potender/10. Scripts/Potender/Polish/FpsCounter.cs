using TMPro;
using UnityEngine;

namespace Potender
{
    /// <summary>
    /// Day 5 프레임 체크: 시야 왼쪽 아래에 FPS 표시 + 10초마다 Logcat에 평균/최저 기록.
    ///  초록 = 목표(72) 근처 / 노랑 = 60대 / 빨강 = 60 미만
    ///  Logcat 필터: "Potender FPS"
    /// 최종 녹화 전엔 show 끄기 (로그는 계속 남음).
    /// </summary>
    public class FpsCounter : MonoBehaviour
    {
        [SerializeField] bool show = true;
        [SerializeField] float targetFps = 72f;
        [SerializeField] TMP_FontAsset font;
        [Tooltip("카메라 기준 위치 (m)")]
        [SerializeField] Vector3 offset = new(-0.2f, -0.16f, 0.6f);
        [SerializeField] float refreshInterval = 0.5f;
        [SerializeField] float logInterval = 10f;

        TMP_Text label;
        GameObject canvasGo;
        float acc, worstDt;
        int frames;
        float logAcc, logWorstDt;
        int logFrames;

        public float CurrentFps { get; private set; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            acc += dt; frames++; worstDt = Mathf.Max(worstDt, dt);
            logAcc += dt; logFrames++; logWorstDt = Mathf.Max(logWorstDt, dt);

            if (show && !label) Build();
            if (canvasGo && canvasGo.activeSelf != show) canvasGo.SetActive(show);

            if (acc >= refreshInterval)
            {
                CurrentFps = frames / acc;
                float min = 1f / worstDt;
                if (label)
                {
                    string col = CurrentFps >= targetFps * 0.97f ? "#7CFC7C" : CurrentFps >= targetFps * 0.83f ? "#FFD54A" : "#FF5252";
                    label.text = $"<color={col}>{CurrentFps:0} fps</color>\n<size=70%>최저 {min:0}</size>";
                }
                acc = 0f; frames = 0; worstDt = 0f;
            }

            if (logAcc >= logInterval)
            {
                Debug.Log($"[Potender FPS] 평균 {logFrames / logAcc:0.0} / 최저 {1f / logWorstDt:0.0} (목표 {targetFps:0})");
                logAcc = 0f; logFrames = 0; logWorstDt = 0f;
            }
        }

        public void Toggle() => show = !show;

        void Build()
        {
            var cam = Camera.main;
            if (!cam) return;

            canvasGo = new GameObject("FpsCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(cam.transform, false);
            canvasGo.transform.localPosition = offset;
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one * 0.001f;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(220f, 70f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = textGo.AddComponent<TextMeshProUGUI>();
            if (font) t.font = font;
            t.fontSize = 26f;
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.text = "…";
            label = t;
        }
    }
}
