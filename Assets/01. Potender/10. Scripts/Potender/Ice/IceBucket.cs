using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Potender
{
    /// <summary>
    /// 얼음 통 (무한 공급): 통을 잡으면(Grip) 그 손에 얼음 큐브가 생겨서 쥐어짐.
    /// XRSimpleInteractable + 콜라이더와 같이 붙이기. 통 자체는 움직이지 않음.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class IceBucket : MonoBehaviour
    {
        [SerializeField] IceCube icePrefab;
        [Tooltip("동시에 존재할 수 있는 얼음 수. 넘으면 손에 없는 오래된 것부터 정리")]
        [SerializeField] int maxAlive = 10;

        [Header("이벤트 (사운드/햅틱 연결용)")]
        public UnityEvent onTake;

        XRSimpleInteractable bucket;
        readonly List<IceCube> alive = new();

        void Awake() => bucket = GetComponent<XRSimpleInteractable>();
        void OnEnable() => bucket.selectEntered.AddListener(OnSelected);
        void OnDisable() => bucket.selectEntered.RemoveListener(OnSelected);

        void OnSelected(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            StartCoroutine(HandOver(args.interactorObject, args.manager));
        }

        IEnumerator HandOver(IXRSelectInteractor interactor, XRInteractionManager mgr)
        {
            // 선택 이벤트 처리 도중에 선택을 바꾸면 꼬이므로 한 프레임 뒤에 처리
            yield return null;
            if (!mgr || interactor == null || !icePrefab) yield break;
            if (!interactor.IsSelecting(bucket)) yield break; // 그 사이 손을 놓음

            mgr.SelectExit(interactor, (IXRSelectInteractable)bucket);

            Prune();
            var at = interactor.GetAttachTransform(bucket);
            var ice = Instantiate(icePrefab, at.position, Random.rotation);
            alive.Add(ice);

            var grab = ice.GetComponent<XRGrabInteractable>();
            if (grab && !interactor.hasSelection) mgr.SelectEnter(interactor, (IXRSelectInteractable)grab);
            onTake?.Invoke();
        }

        void Prune()
        {
            alive.RemoveAll(i => !i);
            for (int k = 0; k < alive.Count && alive.Count >= maxAlive; )
            {
                if (alive[k].IsHeld) { k++; continue; }
                Destroy(alive[k].gameObject);
                alive.RemoveAt(k);
            }
        }
    }
}
