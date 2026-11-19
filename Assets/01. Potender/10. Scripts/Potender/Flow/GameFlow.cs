using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Potender
{
    /// <summary>
    /// 게임 흐름: 타이틀 → 영업(제한 시간, 손님 스폰) → 마감(남은 손님 처리) → 정산 → 재시작.
    /// 씬에 1개. 버튼 onClick(또는 XRSimpleInteractable)에 StartGame / Restart 연결.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public enum State { Title, Open, Closing, Result }
        public static GameFlow Instance { get; private set; }

        [Header("데이터")]
        [SerializeField] RecipeBook recipeBook;

        [Header("손님")]
        [SerializeField] Customer customerPrefab;
        [SerializeField] CounterSpot[] spots;
        [Tooltip("손님이 나타나는 곳 (가게 입구)")]
        [SerializeField] Transform entrance;
        [Tooltip("손님이 나가는 곳. 비우면 입구로 나감")]
        [SerializeField] Transform exit;
        [Tooltip("손님 색 바리에이션")]
        [SerializeField] Color[] customerColors =
        {
            new(0.9f, 0.4f, 0.4f), new(0.4f, 0.7f, 0.95f), new(0.5f, 0.85f, 0.5f), new(0.95f, 0.8f, 0.35f)
        };

        [Header("영업 시간 / 스폰")]
        [SerializeField] float dayLength = 180f;
        [SerializeField] float firstCustomerDelay = 2f;
        [SerializeField] Vector2 spawnInterval = new(6f, 12f);
        [Tooltip("영업 종료 후 남은 손님을 기다리는 최대 시간")]
        [SerializeField] float closingGrace = 20f;
        [SerializeField] float resultDelay = 2f;

        [Header("손님 인내심 / 팁")]
        [SerializeField] float patienceSeconds = 60f;
        [Tooltip("맞는 포션 기본 팁")]
        [SerializeField] int baseTip = 10;
        [Tooltip("인내심이 가득일 때 추가 팁 (남은 인내심에 비례)")]
        [SerializeField] int speedBonus = 10;
        [Tooltip("총 팁이 이 값 이상이면 ★1, ★2, ★3")]
        [SerializeField] int[] starThresholds = { 20, 50, 90 };

        [Header("손님 반응 대사")]
        [SerializeField] string[] thanksLines = { "바로 이거야! 고마워!", "역시 소문대로네!" };
        [SerializeField] string[] wrongLines = { "이건 내가 말한 게 아닌데…", "음… 이게 맞아?" };
        [SerializeField] string[] angryLines = { "너무 늦잖아! 다른 데 갈래.", "됐어, 안 마셔!" };
        [SerializeField] string[] closingLines = { "벌써 문 닫는 거야?" };

        [Header("UI")]
        [SerializeField] GameObject titlePanel;
        [SerializeField] GameObject hudPanel;
        [SerializeField] GameObject resultPanel;
        [SerializeField] TMP_Text hudText;
        [SerializeField] TMP_Text resultText;
        [SerializeField] string starOn = "★";
        [SerializeField] string starOff = "☆";

        [Header("이벤트 (사운드/조명 연결용)")]
        public UnityEvent onOpen;
        public UnityEvent onClose;
        public UnityEvent onServeSuccess;
        public UnityEvent onServeWrong;
        public UnityEvent onCustomerAngry;

        public State Current { get; private set; } = State.Title;
        /// <summary>Day 4 강도 습격 중엔 true → 손님 인내심 정지</summary>
        public bool PatiencePaused { get; set; }
        public float PatienceSeconds => patienceSeconds;
        public float TimeLeft { get; private set; }

        // 통계
        public int TotalTips { get; private set; }
        public int Served { get; private set; }
        public int Wrong { get; private set; }
        public int Angry { get; private set; }
        public int Explosions { get; private set; }

        float spawnTimer, stateTimer;
        RecipeData lastOrder;
        readonly List<RecipeData> orderable = new();
        readonly StringBuilder sb = new();

        void Awake()
        {
            Instance = this;
            foreach (var r in recipeBook ? recipeBook.recipes : new List<RecipeData>())
                if (r && !r.isAttackPotion) orderable.Add(r);
            if (orderable.Count == 0) Debug.LogWarning("[Potender] 주문 가능한 레시피가 없음 (RecipeBook 확인)");
        }

        void Start()
        {
            // 폭발 횟수 집계 (씬의 모든 쉐이커/글라스)
            foreach (var c in FindObjectsByType<MixingContainer>(FindObjectsSortMode.None))
                c.Judged += r => { if (!r.success && Current == State.Open) Explosions++; };
            ShowPanels();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ───────── 버튼 ─────────
        public void StartGame()
        {
            if (Current != State.Title) return;
            TotalTips = Served = Wrong = Angry = Explosions = 0;
            TimeLeft = dayLength;
            spawnTimer = firstCustomerDelay;
            Current = State.Open;
            ShowPanels();
            onOpen?.Invoke();
            Debug.Log("[Potender] 영업 시작");
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        // ───────── 루프 ─────────
        void Update()
        {
            switch (Current)
            {
                case State.Open:
                    TimeLeft -= Time.deltaTime;
                    spawnTimer -= Time.deltaTime;
                    if (spawnTimer <= 0f && TrySpawn())
                        spawnTimer = Random.Range(spawnInterval.x, spawnInterval.y);
                    if (TimeLeft <= 0f) Close();
                    break;

                case State.Closing:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f && AnyCustomer()) DismissAll();
                    if (!AnyCustomer() && stateTimer <= closingGrace - resultDelay) ShowResult();
                    break;
            }
            UpdateHud();
        }

        bool TrySpawn()
        {
            if (!customerPrefab || orderable.Count == 0) return false;
            var free = new List<CounterSpot>();
            foreach (var s in spots) if (s && s.IsFree) free.Add(s);
            if (free.Count == 0) return false;

            var spot = free[Random.Range(0, free.Count)];
            var order = PickOrder();
            string line = order.orderLines is { Count: > 0 }
                ? order.orderLines[Random.Range(0, order.orderLines.Count)]
                : $"{order.potionName} 하나 주세요.";
            Color color = customerColors is { Length: > 0 }
                ? customerColors[Random.Range(0, customerColors.Length)] : Color.white;

            Vector3 from = entrance ? entrance.position : spot.StandPoint.position;
            var c = Instantiate(customerPrefab, from, Quaternion.identity);
            c.Init(this, spot, order, line, color, exit ? exit : entrance);
            spot.Current = c;
            return true;
        }

        RecipeData PickOrder()
        {
            if (orderable.Count == 1) return orderable[0];
            RecipeData r;
            do r = orderable[Random.Range(0, orderable.Count)]; while (r == lastOrder);
            lastOrder = r;
            return r;
        }

        // ───────── 서빙 / 반응 (CounterSpot, Customer가 호출) ─────────
        public void Serve(Customer c, RecipeData potion)
        {
            if (!c || !c.IsWaiting) return;
            bool correct = potion && potion == c.Order;
            if (correct)
            {
                int tip = baseTip + Mathf.RoundToInt(speedBonus * c.Patience01);
                TotalTips += tip;
                Served++;
                c.React($"{Pick(thanksLines)}\n<color=#FFD54A>+{tip}G</color>", true);
                onServeSuccess?.Invoke();
                Debug.Log($"[Potender 서빙] 성공 {c.Order.potionName} +{tip}G");
            }
            else
            {
                Wrong++;
                c.React(Pick(wrongLines), false);
                onServeWrong?.Invoke();
                Debug.Log($"[Potender 서빙] 틀림: 주문 {c.Order.potionName} / 받은 것 {(potion ? potion.potionName : "-")}");
            }
        }

        public void OnPatienceOut(Customer c)
        {
            Angry++;
            c.React(Pick(angryLines), false);
            onCustomerAngry?.Invoke();
        }

        // ───────── 마감 / 정산 ─────────
        void Close()
        {
            TimeLeft = 0f;
            Current = State.Closing;
            stateTimer = closingGrace;
            onClose?.Invoke();
            Debug.Log("[Potender] 영업 종료 — 남은 손님 처리 중");
        }

        void DismissAll()
        {
            foreach (var s in spots)
                if (s && s.Current && s.Current.IsWaiting) s.Current.React(Pick(closingLines), false);
        }

        bool AnyCustomer()
        {
            foreach (var s in spots) if (s && !s.IsFree) return true;
            return false;
        }

        void ShowResult()
        {
            Current = State.Result;
            ShowPanels();
            if (!resultText) return;

            int stars = 0;
            foreach (var t in starThresholds) if (TotalTips >= t) stars++;
            sb.Clear();
            sb.Append("<size=140%><b>오늘의 정산</b></size>\n\n");
            sb.Append($"총 팁  <b>{TotalTips} G</b>\n");
            sb.Append($"응대 손님  {Served}명\n");
            sb.Append($"잘못 서빙  {Wrong}  ·  화나서 떠남  {Angry}  ·  폭발  {Explosions}\n\n");
            sb.Append("<size=160%><color=#FFD54A>");
            for (int i = 0; i < starThresholds.Length; i++) sb.Append(i < stars ? starOn : starOff);
            sb.Append("</color></size>");
            resultText.text = sb.ToString();
        }

        void ShowPanels()
        {
            if (titlePanel) titlePanel.SetActive(Current == State.Title);
            if (hudPanel) hudPanel.SetActive(Current is State.Open or State.Closing);
            if (resultPanel) resultPanel.SetActive(Current == State.Result);
        }

        void UpdateHud()
        {
            if (!hudText || Current is not (State.Open or State.Closing)) return;
            int t = Mathf.CeilToInt(Mathf.Max(0f, TimeLeft));
            string time = Current == State.Closing ? "<color=#FF7043>마감</color>" : $"{t / 60}:{t % 60:00}";
            hudText.text = $"{time}    팁 {TotalTips}G    손님 {Served}";
        }

        static string Pick(string[] arr) => arr is { Length: > 0 } ? arr[Random.Range(0, arr.Length)] : "";

        // ───────── 테스트용 (FlowKeyboardTester) ─────────
        public void DebugServeFirst(bool correct)
        {
            foreach (var s in spots)
            {
                if (!s || !s.Current || !s.Current.IsWaiting) continue;
                RecipeData potion = s.Current.Order;
                if (!correct)
                {
                    potion = null;
                    foreach (var r in orderable) if (r != s.Current.Order) { potion = r; break; }
                }
                Serve(s.Current, potion);
                return;
            }
            Debug.Log("[Potender] 기다리는 손님 없음");
        }

        public void DebugSkipTime(float seconds)
        {
            if (Current == State.Open) TimeLeft -= seconds;
        }
    }
}
