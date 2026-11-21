using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Potender
{
    /// <summary>
    /// 강도 습격 (영업 중 1회).
    ///  발생 → 붉은 조명 + BGM 전환 + 손님 인내심/영업 시계 정지 → 고블린 접근
    ///  → 제한 시간 안에 불꽃 포션 명중: 퇴치(보너스) / 실패: 금고 털림(팁 감소)
    ///  → 잠시 후 조명·BGM 복구, 영업 재개.
    /// 씬에 1개. GameFlow와 같은 씬에 두기.
    /// </summary>
    public class RaidEvent : MonoBehaviour
    {
        public enum State { Idle, Active, Resolving, Done }
        public static RaidEvent Instance { get; private set; }

        [Header("발생 시점")]
        [SerializeField] bool autoTrigger = true;
        [Tooltip("영업 시간 중 몇 % 지났을 때 습격할지 (랜덤 범위). 0.4 = 40%")]
        [SerializeField] Vector2 triggerAt = new(0.35f, 0.5f);

        [Header("고블린")]
        [SerializeField] Goblin goblinPrefab;
        [Tooltip("고블린이 나타나는 곳 (입구)")]
        [SerializeField] Transform spawnPoint;
        [Tooltip("고블린이 도착하면 금고를 터는 곳 (카운터 앞)")]
        [SerializeField] Transform targetPoint;
        [Tooltip("도망가는 곳. 비우면 spawnPoint")]
        [SerializeField] Transform exitPoint;
        [Tooltip("이 시간 안에 맞혀야 함 (= 입구→카운터 걸리는 시간)")]
        [SerializeField] float timeLimit = 55f;
        [Tooltip("컷 버전: 고블린이 targetPoint에 바로 나타나서 가만히 서 있음 (시간 제한은 그대로)")]
        [SerializeField] bool stationaryGoblin = false;

        [Tooltip("불꽃 포션 레시피 (키보드 F 테스트용)")]
        [SerializeField] RecipeData firePotion;

        [Header("결과")]
        [Tooltip("털리면 현재 팁의 몇 %를 잃는지")]
        [Range(0f, 1f)] [SerializeField] float penaltyRatio = 0.5f;
        [Tooltip("최소 벌금 (팁이 적을 때도 이만큼은 잃음, 단 0 아래로는 X)")]
        [SerializeField] int penaltyMin = 20;
        [Tooltip("퇴치 보너스 팁")]
        [SerializeField] int repelBonus = 10;
        [Tooltip("결과가 나온 뒤 조명 복구 · 영업 재개까지")]
        [SerializeField] float endDelay = 2.5f;

        [Header("조명 (Realtime / Mixed 라이트만 바뀜 — Baked는 안 바뀜)")]
        [SerializeField] Light[] lights;
        [SerializeField] Color raidLightColor = new(1f, 0.12f, 0.08f);
        [Tooltip("습격 중 밝기 배율")]
        [SerializeField] float raidIntensity = 0.9f;
        [Tooltip("Environment Lighting Source가 Color일 때 주변광도 붉게")]
        [SerializeField] bool tintAmbient = true;
        [SerializeField] Color raidAmbient = new(0.3f, 0.04f, 0.04f);
        [SerializeField] float fadeTime = 0.8f;
        [Tooltip("경광등처럼 깜빡임 속도 (0 = 끔)")]
        [SerializeField] float pulseSpeed = 3f;
        [Range(0f, 1f)] [SerializeField] float pulseAmount = 0.35f;
        [Tooltip("습격 중에만 켜질 오브젝트 (빨간 포인트 라이트, 경고 문구 등)")]
        [SerializeField] GameObject[] showDuringRaid;

        [Header("사운드 (비워도 됨)")]
        [Tooltip("평소 BGM (Loop) — 습격 중엔 페이드 아웃")]
        [SerializeField] AudioSource bgmNormal;
        [Tooltip("습격 BGM (Loop, Play On Awake 끄기)")]
        [SerializeField] AudioSource bgmRaid;
        [Tooltip("효과음용 AudioSource (2D)")]
        [SerializeField] AudioSource sfx;
        [SerializeField] AudioClip alarmClip;
        [SerializeField] AudioClip repelClip;
        [SerializeField] AudioClip stealClip;

        [Header("HUD / 정산 문구")]
        [SerializeField] string hudAlert = "<color=#FF5252><b>고블린 습격!</b>  불꽃 포션을 던져라!  {0}초</color>";
        [SerializeField] string repelText = "<color=#7CFC7C>고블린 퇴치! +{0}G</color>";
        [SerializeField] string stealText = "<color=#FF5252>금고를 털렸다… -{0}G</color>";
        [SerializeField] string repelSummary = "강도 고블린  <color=#7CFC7C>퇴치 성공 (+{0}G)</color>";
        [SerializeField] string stealSummary = "강도 고블린  <color=#FF5252>금고 털림 (-{0}G)</color>";

        [Header("이벤트")]
        public UnityEvent onRaidStart;
        public UnityEvent onRepelled;
        public UnityEvent onStolen;
        public UnityEvent onRaidEnd;

        public State Current { get; private set; } = State.Idle;
        public Goblin ActiveGoblin { get; private set; }
        public RecipeData FirePotion => firePotion;

        GameFlow flow;
        float triggerElapsed = -1f;
        float weight;           // 0 = 평소, 1 = 습격 조명
        float targetWeight;
        Color[] baseColors;
        float[] baseIntensity;
        Color baseAmbient;
        float bgmNormalVol = 1f, bgmRaidVol = 1f;

        void Awake()
        {
            Instance = this;
            lights ??= new Light[0];
            baseColors = new Color[lights.Length];
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                if (!lights[i]) continue;
                baseColors[i] = lights[i].color;
                baseIntensity[i] = lights[i].intensity;
            }
            baseAmbient = RenderSettings.ambientLight;
            if (bgmNormal) bgmNormalVol = bgmNormal.volume;
            if (bgmRaid) { bgmRaidVol = bgmRaid.volume; bgmRaid.volume = 0f; }
            SetShown(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (tintAmbient) RenderSettings.ambientLight = baseAmbient;
        }

        void Update()
        {
            flow = GameFlow.Instance;

            // 자동 발생
            if (Current == State.Idle && flow && flow.Current == GameFlow.State.Open)
            {
                if (triggerElapsed < 0f)
                    triggerElapsed = flow.DayLength * Random.Range(triggerAt.x, triggerAt.y);
                float elapsed = flow.DayLength - flow.TimeLeft;
                if (autoTrigger && elapsed >= triggerElapsed) StartRaid();
            }

            if (Current == State.Active && flow && ActiveGoblin)
                flow.HudAlert = string.Format(hudAlert, Mathf.CeilToInt(ActiveGoblin.TimeLeft));

            UpdateLighting();
        }

        // ───────── 시작 ─────────
        [ContextMenu("Start Raid (Play 중)")]
        public void StartRaid()
        {
            flow = GameFlow.Instance;
            if (Current != State.Idle || !flow || flow.Current != GameFlow.State.Open) return;
            if (!goblinPrefab)
            {
                // 매 프레임 경고가 쌓이지 않게 한 번만 알리고 이번 판은 습격 없음
                Debug.LogError("[Potender 강도] RaidEvent의 goblinPrefab이 비어 있어서 습격 취소 — Prefabs/Goblin 을 드래그하세요");
                Current = State.Done;
                return;
            }

            Current = State.Active;
            flow.PatiencePaused = true;

            Transform from = stationaryGoblin && targetPoint ? targetPoint : (spawnPoint ? spawnPoint : targetPoint);
            Vector3 pos = from ? from.position : transform.position;
            Quaternion rot = targetPoint && from != targetPoint
                ? Quaternion.LookRotation(Flat(targetPoint.position - pos).normalized + Vector3.forward * 1e-4f)
                : Quaternion.identity;
            ActiveGoblin = Instantiate(goblinPrefab, pos, rot);
            ActiveGoblin.Init(this, targetPoint, exitPoint ? exitPoint : spawnPoint, timeLimit, stationaryGoblin);

            targetWeight = 1f;
            SetShown(true);
            if (bgmRaid && !bgmRaid.isPlaying) bgmRaid.Play();
            PlaySfx(alarmClip);
            onRaidStart?.Invoke();
            Debug.Log("[Potender 강도] 습격 시작");
        }

        // ───────── 결과 (Goblin이 호출) ─────────
        public void OnGoblinRepelled()
        {
            if (Current != State.Active) return;
            int got = flow ? flow.AddTips(repelBonus) : 0;
            Resolve(string.Format(repelText, got), string.Format(repelSummary, got));
            PlaySfx(repelClip);
            onRepelled?.Invoke();
            Debug.Log($"[Potender 강도] 퇴치 성공 +{got}G");
        }

        public void OnGoblinStole()
        {
            if (Current != State.Active) return;
            int lost = 0;
            if (flow)
            {
                int want = Mathf.Max(penaltyMin, Mathf.RoundToInt(flow.TotalTips * penaltyRatio));
                lost = -flow.AddTips(-want);
            }
            Resolve(string.Format(stealText, lost), string.Format(stealSummary, lost));
            PlaySfx(stealClip);
            onStolen?.Invoke();
            Debug.Log($"[Potender 강도] 금고 털림 -{lost}G");
        }

        public void OnGoblinGone(Goblin g)
        {
            if (g != ActiveGoblin) return;
            ActiveGoblin = null;
            // 결과 없이 사라진 경우 (씬 종료 등) → 그냥 종료 처리
            if (Current == State.Active) StartCoroutine(EndAfter(0f));
        }

        void Resolve(string hud, string summary)
        {
            Current = State.Resolving;
            if (flow)
            {
                flow.HudAlert = hud;
                flow.RaidSummary = summary;
            }
            StartCoroutine(EndAfter(endDelay));
        }

        IEnumerator EndAfter(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Current = State.Done;
            targetWeight = 0f;
            SetShown(false);
            if (flow)
            {
                flow.PatiencePaused = false;
                flow.HudAlert = null;
            }
            onRaidEnd?.Invoke();
            Debug.Log("[Potender 강도] 종료 — 영업 재개");
        }

        // ───────── 조명 / BGM ─────────
        void UpdateLighting()
        {
            float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
            weight = Mathf.MoveTowards(weight, targetWeight, step);

            float pulse = 1f;
            if (pulseSpeed > 0f && weight > 0f)
                pulse = 1f - pulseAmount * weight * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f));

            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (!l) continue;
                l.color = Color.Lerp(baseColors[i], raidLightColor, weight);
                l.intensity = baseIntensity[i] * Mathf.Lerp(1f, raidIntensity, weight) * pulse;
            }
            if (tintAmbient) RenderSettings.ambientLight = Color.Lerp(baseAmbient, raidAmbient, weight);

            if (bgmNormal) bgmNormal.volume = bgmNormalVol * (1f - weight);
            if (bgmRaid)
            {
                bgmRaid.volume = bgmRaidVol * weight;
                if (weight <= 0f && targetWeight <= 0f && bgmRaid.isPlaying) bgmRaid.Stop();
            }
        }

        void SetShown(bool on)
        {
            if (showDuringRaid == null) return;
            foreach (var go in showDuringRaid) if (go) go.SetActive(on);
        }

        void PlaySfx(AudioClip clip)
        {
            if (sfx && clip) sfx.PlayOneShot(clip);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ───────── 테스트용 ─────────
        public void DebugHitGoblin()
        {
            if (ActiveGoblin) ActiveGoblin.Hit(ActiveGoblin.transform.position + ActiveGoblin.transform.forward);
            else Debug.Log("[Potender 강도] 고블린 없음 (R로 습격 시작)");
        }
    }
}
