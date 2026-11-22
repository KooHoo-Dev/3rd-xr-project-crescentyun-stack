using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Potender
{
    /// <summary>
    /// 손님: 입구 → 카운터 자리로 걸어옴 → 대사로 주문 + 인내심 감소 → 서빙/시간초과 반응 → 출구로 나가서 삭제.
    /// GameFlow가 생성하고 Init 해 줌. 프리팹 하나 + 색 바리에이션.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        enum Phase { Entering, Waiting, Reacting, Leaving }

        [Header("이동")]
        [SerializeField] float moveSpeed = 1.2f;
        [SerializeField] float turnSpeed = 360f;

        [Header("외형")]
        [Tooltip("색을 바꿀 렌더러들 (몸통 등)")]
        [SerializeField] Renderer[] tintRenderers;
        [Tooltip("URP Lit = _BaseColor, Built-in = _Color")]
        [SerializeField] string colorProperty = "_BaseColor";
        [Tooltip("선택: 걷기 bool 파라미터 이름 (비우면 무시)")]
        [SerializeField] Animator animator;
        [SerializeField] string walkBool = "";

        [Header("UI (월드 캔버스)")]
        [SerializeField] GameObject bubble;
        [SerializeField] TMP_Text bubbleText;
        [SerializeField] GameObject patienceRoot;
        [Tooltip("Image Type = Filled, Fill Method = Horizontal")]
        [SerializeField] Image patienceFill;
        [SerializeField] Color patienceFull = new(0.4f, 0.9f, 0.4f);
        [SerializeField] Color patienceEmpty = new(0.95f, 0.3f, 0.25f);
        [Tooltip("반응 대사 보여주는 시간")]
        [SerializeField] float reactTime = 2.5f;

        RectTransform uiRoot;
        Vector3 uiBaseLocal;
        GameFlow flow;
        CounterSpot spot;
        Transform exitPoint;
        Phase phase;
        float patience, reactTimer;

        public RecipeData Order { get; private set; }
        public string OrderLine { get; private set; }
        public bool IsWaiting => phase == Phase.Waiting;
        /// <summary>자리에 도착해서 주문한 시각 (Time.time)</summary>
        public float WaitingSince { get; private set; } = float.MaxValue;
        public float Patience01 => flow ? Mathf.Clamp01(patience / flow.PatienceSeconds) : 1f;

        public void Init(GameFlow flow, CounterSpot spot, RecipeData order, string line, Color tint, Transform exit)
        {
            this.flow = flow;
            this.spot = spot;
            Order = order;
            OrderLine = line;
            exitPoint = exit;
            patience = flow.PatienceSeconds;
            phase = Phase.Entering;

            var mpb = new MaterialPropertyBlock();
            int id = Shader.PropertyToID(colorProperty);
            foreach (var r in tintRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(id, tint);
                r.SetPropertyBlock(mpb);
            }

            if (bubble) bubble.SetActive(false);
            if (patienceRoot) patienceRoot.SetActive(false);
            SetWalking(true);

            var canvas = bubble ? bubble.GetComponentInParent<Canvas>(true) : null;
            if (canvas)
            {
                uiRoot = (RectTransform)canvas.transform;
                uiBaseLocal = uiRoot.localPosition;
            }
        }

        void Update()
        {
            switch (phase)
            {
                case Phase.Entering:
                    if (MoveTo(spot.StandPoint.position)) Arrive();
                    break;

                case Phase.Waiting:
                    FaceForward(spot.StandPoint.forward);
                    if (!flow.PatiencePaused) patience -= Time.deltaTime;
                    UpdateGauge();
                    if (patience <= 0f) flow.OnPatienceOut(this);
                    break;

                case Phase.Reacting:
                    reactTimer -= Time.deltaTime;
                    if (reactTimer <= 0f) { phase = Phase.Leaving; SetWalking(true); }
                    break;

                case Phase.Leaving:
                    if (!exitPoint || MoveTo(exitPoint.position))
                    {
                        spot.Current = null;
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        void Arrive()
        {
            phase = Phase.Waiting;
            WaitingSince = Time.time;
            SetWalking(false);
            ApplyBubbleOffset();
            if (bubble) bubble.SetActive(true);
            if (bubbleText) bubbleText.text = OrderLine;
            if (patienceRoot) patienceRoot.SetActive(true);
            UpdateGauge();
        }

        /// <summary>서빙 결과 / 시간 초과 / 마감 시 반응 → 잠시 후 퇴장</summary>
        public void React(string line, bool happy)
        {
            if (phase is Phase.Reacting or Phase.Leaving) return;
            phase = Phase.Reacting;
            reactTimer = reactTime;
            if (patienceRoot) patienceRoot.SetActive(false);
            if (bubble) bubble.SetActive(true);
            if (bubbleText) bubbleText.text = line;
            // happy 여부로 애니/이펙트를 바꾸고 싶으면 여기서
        }

        // 옆 자리 손님과 말풍선이 겹치지 않게 (자리가 바라보는 방향 기준으로 로컬 변환)
        void ApplyBubbleOffset()
        {
            if (!uiRoot || spot.BubbleOffset == Vector3.zero) return;
            Vector3 f = spot.StandPoint.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return;
            Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(f)) * spot.BubbleOffset;
            Vector3 s = transform.lossyScale;
            uiRoot.localPosition = uiBaseLocal + new Vector3(local.x / s.x, local.y / s.y, local.z / s.z);
        }

        bool MoveTo(Vector3 target)
        {
            Vector3 p = transform.position;
            target.y = p.y;
            Vector3 dir = target - p;
            if (dir.sqrMagnitude < 0.0004f) return true;
            FaceForward(dir);
            transform.position = Vector3.MoveTowards(p, target, moveSpeed * Time.deltaTime);
            return false;
        }

        void FaceForward(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            var rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, turnSpeed * Time.deltaTime);
        }

        void UpdateGauge()
        {
            if (!patienceFill) return;
            float p = Patience01;
            patienceFill.fillAmount = p;
            patienceFill.color = Color.Lerp(patienceEmpty, patienceFull, p);
        }

        void SetWalking(bool on)
        {
            if (animator && !string.IsNullOrEmpty(walkBool)) animator.SetBool(walkBool, on);
        }
    }
}
