using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Potender
{
    /// <summary>
    /// 서빙 잔에 붙이기. 공격 포션(isAttackPotion)이 든 잔을 '손에서 놓는 순간' 장전 →
    /// 세게 부딪히거나 고블린에 닿으면 펑! 반경 안 고블린 명중 처리, 잔은 깨진 것처럼 사라졌다가 제자리로.
    /// · 날아가는 중 잔이 기울어 포션이 쏟아져도 장전 상태는 유지 (놓을 때 판정)
    /// · 살살 내려놓으면(minImpactSpeed 미만) 안 터짐 / 다시 잡으면 장전 해제
    /// · 일반 포션은 아무 일도 없음
    /// </summary>
    [RequireComponent(typeof(ServingGlass))]
    public class PotionThrowable : MonoBehaviour
    {
        [Tooltip("이 속도(m/s) 이상으로 부딪히면 터짐. 살살 내려놓기는 무시")]
        [SerializeField] float minImpactSpeed = 2f;
        [Tooltip("폭발 반경 — 고블린 근처에만 맞아도 명중 (VR 조준 보정)")]
        [SerializeField] float blastRadius = 0.9f;
        [SerializeField] GameObject explosionPrefab;
        [SerializeField] float explosionLifetime = 3f;
        [Tooltip("터진 뒤 잔이 제자리에 다시 나타나기까지")]
        [SerializeField] float respawnDelay = 1f;

        [Header("이벤트 (사운드/햅틱 연결용)")]
        public UnityEvent onArmed;
        public UnityEvent onExplode;
        public UnityEvent onHitGoblin;

        ServingGlass glass;
        XRGrabInteractable grab;
        Rigidbody rb;
        ReturnToHome home;
        LiquidVisual liquid;
        Renderer[] renderers;
        Collider[] colliders;
        bool armed, hidden, pendingArm;
        readonly HashSet<Goblin> hitBuffer = new();

        public bool Armed => armed;

        void Awake()
        {
            glass = GetComponent<ServingGlass>();
            grab = GetComponent<XRGrabInteractable>();
            rb = GetComponent<Rigidbody>();
            home = GetComponent<ReturnToHome>();
            liquid = GetComponentInChildren<LiquidVisual>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        void OnEnable()
        {
            if (!grab) return;
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            if (!grab) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        void OnGrabbed(SelectEnterEventArgs _) { armed = false; pendingArm = false; }   // 손·소켓에 잡히면 해제

        // 소켓 → 손으로 옮겨 잡을 때도 Exit가 먼저 오므로, 한 프레임 뒤 아무도 안 잡고 있을 때만 장전
        void OnReleased(SelectExitEventArgs _) => pendingArm = true;

        void Update() => TryArm();

        void TryArm()
        {
            if (!pendingArm) return;
            pendingArm = false;
            if (hidden || (grab && grab.isSelected) || !glass.Potion || !glass.Potion.isAttackPotion) return;
            armed = true;
            onArmed?.Invoke();
        }

        void OnCollisionEnter(Collision c)
        {
            TryArm();
            if (!armed || hidden) return;
            bool goblin = c.collider.GetComponentInParent<Goblin>();
            if (goblin || c.relativeVelocity.magnitude >= minImpactSpeed)
                Explode(c.contactCount > 0 ? c.GetContact(0).point : transform.position);
        }

        void OnTriggerEnter(Collider other)
        {
            TryArm();
            if (armed && !hidden && other.GetComponentInParent<Goblin>()) Explode(transform.position);
        }

        void Explode(Vector3 point)
        {
            armed = false;
            if (explosionPrefab)
            {
                var fx = Instantiate(explosionPrefab, point, Quaternion.identity);
                Destroy(fx, explosionLifetime);
            }

            hitBuffer.Clear();
            foreach (var col in Physics.OverlapSphere(point, blastRadius, ~0, QueryTriggerInteraction.Collide))
            {
                var g = col.GetComponentInParent<Goblin>();
                if (g && hitBuffer.Add(g) && g.Hit(point)) onHitGoblin?.Invoke();
            }
            onExplode?.Invoke();
            Debug.Log($"[Potender 투척] 펑! 고블린 명중 {hitBuffer.Count}");

            StartCoroutine(Shatter());
        }

        /// <summary>잔이 깨진 것처럼 숨김 → 비우고 제자리로 → 다시 보이기</summary>
        IEnumerator Shatter()
        {
            hidden = true;
            glass.Clear();
            if (liquid) liquid.SnapToTarget();
            SetVisible(false);

            bool wasKinematic = rb && rb.isKinematic;
            if (rb)
            {
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.isKinematic = true;
            }
            if (home) home.Return();

            yield return new WaitForSeconds(respawnDelay);

            if (home) home.Return();
            if (rb) rb.isKinematic = wasKinematic;
            SetVisible(true);
            if (liquid) liquid.SnapToTarget();
            hidden = false;
        }

        void SetVisible(bool on)
        {
            foreach (var r in renderers) if (r) r.enabled = on;
            foreach (var c in colliders) if (c) c.enabled = on;
        }

        // ───────── 테스트용 (FlowKeyboardTester F키) ─────────
        /// <summary>잔에 공격 포션을 채우고 플레이어 앞에서 target으로 던짐</summary>
        public void DebugThrowAt(RecipeData potion, Vector3 target)
        {
            if (hidden || !potion || (grab && grab.isSelected)) return;
            var cam = Camera.main;
            Vector3 start = cam ? cam.transform.position + cam.transform.forward * 0.4f + Vector3.down * 0.25f : transform.position;

            glass.Clear();
            glass.Receive(potion, 1f);
            transform.position = start;
            if (rb)
            {
                rb.isKinematic = false;
                rb.position = start;
                const float t = 0.7f;
                Vector3 v = (target - start - 0.5f * Physics.gravity * t * t) / t;
                rb.linearVelocity = v;
            }
            armed = true;
            onArmed?.Invoke();
        }
    }
}
