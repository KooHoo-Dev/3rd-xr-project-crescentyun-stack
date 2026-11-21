using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Potender
{
    /// <summary>
    /// 강도 고블린: 입구 → 카운터로 다가옴(제한 시간 동안) → 시간 안에 불꽃 포션에 맞으면 도망 / 못 맞히면 금고 털고 도망.
    /// RaidEvent가 생성하고 Init 해 줌.
    /// ※ 손님과 달리 '맞아야' 하므로 콜라이더(Is Trigger 권장)가 꼭 있어야 함.
    /// </summary>
    public class Goblin : MonoBehaviour
    {
        enum Phase { Approaching, Stealing, Fleeing }

        [Header("체력 / 이동")]
        [Tooltip("몇 번 맞아야 도망가는지")]
        [SerializeField] int hitsToRepel = 1;
        [SerializeField] float fleeSpeed = 3f;
        [SerializeField] float turnSpeed = 540f;
        [Tooltip("맞았을 때 뒤로 밀려나는 거리")]
        [SerializeField] float knockback = 0.4f;
        [Tooltip("제한 시간 중 이 비율만큼 걸려서 카운터에 도착 → 남은 시간은 금고 따는 중")]
        [Range(0.2f, 1f)] [SerializeField] float arriveRatio = 0.6f;
        [Tooltip("금고 터는 연출 시간")]
        [SerializeField] float stealTime = 1.5f;

        [Header("외형")]
        [SerializeField] Renderer[] tintRenderers;
        [Tooltip("URP Lit = _BaseColor, Built-in = _Color")]
        [SerializeField] string colorProperty = "_BaseColor";
        [SerializeField] Color bodyColor = new(0.35f, 0.65f, 0.25f);
        [SerializeField] Color flashColor = new(1f, 0.55f, 0.1f);
        [SerializeField] float flashTime = 0.25f;
        [Tooltip("선택: 걷기 bool / 맞음 trigger 이름 (비우면 무시)")]
        [SerializeField] Animator animator;
        [SerializeField] string walkBool = "";
        [SerializeField] string hitTrigger = "";

        [Header("UI (월드 캔버스)")]
        [SerializeField] GameObject bubble;
        [SerializeField] TMP_Text bubbleText;
        [Tooltip("남은 시간 바 (Image Type = Filled)")]
        [SerializeField] GameObject timerRoot;
        [SerializeField] Image timerFill;
        [SerializeField] Color timerFull = new(1f, 0.85f, 0.2f);
        [SerializeField] Color timerEmpty = new(0.95f, 0.15f, 0.1f);

        [Header("대사")]
        [SerializeField] string[] enterLines = { "크크크… 금고 내놔!", "이 가게 돈 냄새가 나는군!" };
        [SerializeField] string pickingLine = "어디 보자… 금고가 여기 있군!";
        [SerializeField] string[] hitLines = { "앗 뜨거!", "끼에엑!" };
        [SerializeField] string[] repelLines = { "으아아 뜨거워! 기억해 두겠다!", "히이익, 도망쳐!" };
        [SerializeField] string[] stealLines = { "크하하! 다 털었다!", "고마워~ 잘 쓸게!" };

        [Header("이벤트 (사운드 연결용)")]
        public UnityEvent onHit;
        public UnityEvent onRepelled;
        public UnityEvent onSteal;

        RaidEvent raid;
        Transform target, exitPoint;
        Phase phase;
        float timeLimit, timer, speed, flashTimer, stealTimer;
        int hp;
        bool stationary, arrived;
        MaterialPropertyBlock mpb;
        int colorId;

        public float TimeLeft => Mathf.Max(0f, timer);
        public float Time01 => timeLimit > 0f ? Mathf.Clamp01(timer / timeLimit) : 0f;
        public bool CanBeHit => phase == Phase.Approaching;

        public void Init(RaidEvent raid, Transform target, Transform exit, float timeLimit, bool stationary)
        {
            this.raid = raid;
            this.target = target;
            exitPoint = exit;
            this.timeLimit = timer = Mathf.Max(1f, timeLimit);
            this.stationary = stationary;
            hp = Mathf.Max(1, hitsToRepel);
            phase = Phase.Approaching;

            // 제한 시간의 arriveRatio 만큼 걸려 카운터 도착 → 남은 시간 동안 금고 따기
            float dist = target ? Flat(target.position - transform.position).magnitude : 0f;
            speed = dist / (this.timeLimit * arriveRatio);
            arrived = stationary || dist < 0.05f;

            mpb = new MaterialPropertyBlock();
            colorId = Shader.PropertyToID(colorProperty);
            // Day 5: Synty 모델로 바뀌었으면 그 모델도 초록 + 피격 번쩍 대상에 포함
            if (TryGetComponent(out ModelSwap swap) && swap.Model)
            {
                var extra = swap.Model.GetComponentsInChildren<Renderer>(true);
                int n = tintRenderers != null ? tintRenderers.Length : 0;
                var all = new Renderer[n + extra.Length];
                if (n > 0) tintRenderers.CopyTo(all, 0);
                extra.CopyTo(all, n);
                tintRenderers = all;
            }
            SetTint(bodyColor);
            Say(Pick(enterLines));
            if (timerRoot) timerRoot.SetActive(true);
            SetWalking(!stationary);
        }

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                SetTint(Color.Lerp(bodyColor, flashColor, Mathf.Clamp01(flashTimer / flashTime)));
            }

            switch (phase)
            {
                case Phase.Approaching:
                    timer -= Time.deltaTime;
                    if (arrived || !target) FacePlayer();
                    else if (MoveTo(target.position, speed))
                    {
                        arrived = true;
                        SetWalking(false);
                        Say(pickingLine);
                    }
                    UpdateTimer();
                    if (timer <= 0f) StartSteal();
                    break;

                case Phase.Stealing:
                    FacePlayer();
                    stealTimer -= Time.deltaTime;
                    if (stealTimer <= 0f) Flee();
                    break;

                case Phase.Fleeing:
                    if (!exitPoint || MoveTo(exitPoint.position, fleeSpeed)) Destroy(gameObject);
                    break;
            }
        }

        /// <summary>불꽃 포션 폭발에 맞음 (PotionThrowable이 호출). 맞았으면 true</summary>
        public bool Hit(Vector3 from)
        {
            if (!CanBeHit) return false;
            hp--;
            flashTimer = flashTime;
            if (animator && !string.IsNullOrEmpty(hitTrigger)) animator.SetTrigger(hitTrigger);
            onHit?.Invoke();

            Vector3 push = Flat(transform.position - from);
            if (push.sqrMagnitude > 1e-4f) transform.position += push.normalized * knockback;

            if (hp > 0) { Say(Pick(hitLines)); return true; }

            Say(Pick(repelLines));
            onRepelled?.Invoke();
            if (raid) raid.OnGoblinRepelled();
            Flee();
            return true;
        }

        void StartSteal()
        {
            phase = Phase.Stealing;
            stealTimer = stealTime;
            SetWalking(false);
            Say(Pick(stealLines));
            if (timerRoot) timerRoot.SetActive(false);
            onSteal?.Invoke();
            if (raid) raid.OnGoblinStole();
        }

        void Flee()
        {
            phase = Phase.Fleeing;
            if (timerRoot) timerRoot.SetActive(false);
            SetWalking(true);
            if (animator) animator.speed = 1.5f;
        }

        void OnDestroy()
        {
            if (raid) raid.OnGoblinGone(this);
        }

        // ───────── 헬퍼 ─────────
        bool MoveTo(Vector3 dest, float spd)
        {
            Vector3 p = transform.position;
            dest.y = p.y;
            Vector3 dir = dest - p;
            if (dir.sqrMagnitude < 0.0004f) return true;
            Face(dir);
            transform.position = Vector3.MoveTowards(p, dest, spd * Time.deltaTime);
            return false;
        }

        void FacePlayer()
        {
            var cam = Camera.main;
            if (cam) Face(cam.transform.position - transform.position);
        }

        void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
        }

        void UpdateTimer()
        {
            if (!timerFill) return;
            float t = Time01;
            timerFill.fillAmount = t;
            timerFill.color = Color.Lerp(timerEmpty, timerFull, t);
        }

        void Say(string line)
        {
            if (bubble) bubble.SetActive(!string.IsNullOrEmpty(line));
            if (bubbleText) bubbleText.text = line;
        }

        void SetTint(Color c)
        {
            if (tintRenderers == null || mpb == null) return;
            foreach (var r in tintRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(colorId, c);
                r.SetPropertyBlock(mpb);
            }
        }

        void SetWalking(bool on)
        {
            if (animator && !string.IsNullOrEmpty(walkBool)) animator.SetBool(walkBool, on);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static string Pick(string[] arr) => arr is { Length: > 0 } ? arr[Random.Range(0, arr.Length)] : "";
    }
}
