using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Potender
{
    /// <summary>
    /// 카운터 자리 하나 = 손님 서는 위치 + 서빙 트레이(소켓).
    /// A) 손님이 주문한 '뒤에' 트레이에 놓은(또는 채운) 잔만 받아감 → 미리 올려둔 잔은 들었다 다시 놓아야 함
    /// C) 손님이 서빙 없이 떠나면(시간 초과/마감) 트레이 위 잔을 비워서 제자리로
    /// 서빙 후 잔은 비워서 원래 자리로 돌려보냄(ReturnToHome).
    /// </summary>
    public class CounterSpot : MonoBehaviour
    {
        [Tooltip("손님이 서는 위치. 파란 화살표(forward)가 플레이어 쪽을 보게")]
        [SerializeField] Transform standPoint;
        [SerializeField] XRSocketInteractor serveSocket;
        [Tooltip("덜 찬 잔은 이 시간 동안 양이 안 변하면 서빙 (트레이에 둔 채 따르는 중이면 대기)")]
        [SerializeField] float settleTime = 0.5f;
        [Tooltip("손님이 잔을 받아가는 연출 시간")]
        [SerializeField] float takeDelay = 0.8f;
        [Tooltip("손님이 그냥 떠났을 때 트레이 위 잔을 치우기까지 시간")]
        [SerializeField] float clearDelay = 1.0f;

        public Transform StandPoint => standPoint ? standPoint : transform;
        public Customer Current { get; set; }
        public bool IsFree => !Current;
        /// <summary>손님 말풍선을 옆으로 미는 양 (월드). 자리가 여러 개면 GameFlow가 자동 계산</summary>
        public Vector3 BubbleOffset { get; set; }

        ServingGlass glass;
        float placedTime, lastFill, stillTimer;
        bool busy, wasWaiting;

        void OnEnable()
        {
            if (!serveSocket) return;
            serveSocket.selectEntered.AddListener(OnPlaced);
            serveSocket.selectExited.AddListener(OnRemoved);
        }

        void OnDisable()
        {
            if (!serveSocket) return;
            serveSocket.selectEntered.RemoveListener(OnPlaced);
            serveSocket.selectExited.RemoveListener(OnRemoved);
        }

        void OnPlaced(SelectEnterEventArgs args)
        {
            glass = args.interactableObject.transform.GetComponentInParent<ServingGlass>();
            placedTime = Time.time;
            stillTimer = 0f;
            if (glass) lastFill = glass.Fill;
        }

        void OnRemoved(SelectExitEventArgs args)
        {
            if (busy) return;
            glass = null;
        }

        void Update()
        {
            bool waiting = Current && Current.IsWaiting;

            // C) 손님이 서빙 없이 떠남 → 트레이 정리
            if (wasWaiting && !waiting && !busy && glass && glass.Potion)
                StartCoroutine(TakeAway(glass, clearDelay, null));
            wasWaiting = waiting;

            if (!glass || busy) return;

            if (!Mathf.Approximately(glass.Fill, lastFill)) stillTimer = 0f;
            else stillTimer += Time.deltaTime;
            lastFill = glass.Fill;

            if (!waiting || !GameFlow.Instance) return;
            bool ready = glass.Potion && (glass.IsFull || stillTimer >= settleTime);
            // A) 주문 이후에 놓았거나 채운 잔만
            bool fresh = Mathf.Max(placedTime, glass.LastFillTime) >= Current.WaitingSince;
            if (ready && fresh)
                StartCoroutine(TakeAway(glass, takeDelay, Current));
        }

        /// <summary>customer가 있으면 서빙 판정 후 회수, 없으면 그냥 회수</summary>
        IEnumerator TakeAway(ServingGlass g, float delay, Customer customer)
        {
            busy = true;
            if (customer) GameFlow.Instance.Serve(customer, g.Potion);
            yield return new WaitForSeconds(delay);

            // 소켓에서 빼고 → 비우고 → 제자리로
            serveSocket.socketActive = false;
            yield return null;
            g.Clear();
            if (g.TryGetComponent<ReturnToHome>(out var home)) home.Return();
            yield return new WaitForSeconds(0.2f);
            serveSocket.socketActive = true;

            glass = null;
            busy = false;
        }
    }
}
