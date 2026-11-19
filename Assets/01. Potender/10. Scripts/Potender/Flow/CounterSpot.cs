using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Potender
{
    /// <summary>
    /// 카운터 자리 하나 = 손님 서는 위치 + 서빙 소켓.
    /// 포션이 담긴 잔이 소켓에 놓이고 (가득 찼거나 / 잠시 그대로 있으면) 이 자리 손님에게 서빙.
    /// 서빙 후 잔은 비워서 원래 자리로 돌려보냄(ReturnToHome).
    /// </summary>
    public class CounterSpot : MonoBehaviour
    {
        [Tooltip("손님이 서는 위치. 파란 화살표(forward)가 플레이어 쪽을 보게")]
        [SerializeField] Transform standPoint;
        [SerializeField] XRSocketInteractor serveSocket;
        [Tooltip("덜 찬 잔은 이 시간 동안 양이 안 변하면 서빙 (소켓에 둔 채 따르는 중이면 대기)")]
        [SerializeField] float settleTime = 0.5f;
        [Tooltip("손님이 잔을 받아가는 연출 시간")]
        [SerializeField] float takeDelay = 0.8f;

        public Transform StandPoint => standPoint ? standPoint : transform;
        public Customer Current { get; set; }
        public bool IsFree => !Current;

        ServingGlass glass;
        float lastFill, stillTimer;
        bool busy;

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
            if (!glass || busy) return;

            if (!Mathf.Approximately(glass.Fill, lastFill)) stillTimer = 0f;
            else stillTimer += Time.deltaTime;
            lastFill = glass.Fill;

            bool ready = glass.Potion && (glass.IsFull || stillTimer >= settleTime);
            if (ready && Current && Current.IsWaiting && GameFlow.Instance)
                StartCoroutine(ServeRoutine(glass));
        }

        IEnumerator ServeRoutine(ServingGlass g)
        {
            busy = true;
            GameFlow.Instance.Serve(Current, g.Potion);
            yield return new WaitForSeconds(takeDelay);

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
