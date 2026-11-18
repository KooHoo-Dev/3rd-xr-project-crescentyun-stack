
using UnityEngine;

namespace Potender
{
    /// <summary>재료 병: 일정 각도 이상 기울이면 주둥이 아래 MixingContainer에 재료 투입 (무한 재료).</summary>
    public class IngredientBottle : MonoBehaviour
    {
        [SerializeField] IngredientData ingredient;
        [SerializeField] Transform spout;
        [Tooltip("병 up축이 이 각도 이상 기울면 붓기 시작")]
        [SerializeField] float pourAngle = 70f;
        [Tooltip("초당 투입량")]
        [SerializeField] float flowRate = 0.25f;
        [SerializeField] float streamLength = 1.2f;
        [Tooltip("손/컨트롤러 레이어는 빼 주세요")]
        [SerializeField] LayerMask pourMask = ~0;
        [SerializeField] PourStream stream;

        public bool IsPouring { get; private set; }

        void Update()
        {
            IsPouring = ingredient && spout && PourUtil.IsTilted(transform, pourAngle);
            if (!IsPouring) { if (stream) stream.Hide(); return; }

            Vector3 from = spout.position;
            Vector3 to = from + Vector3.down * streamLength;

            if (PourUtil.CastDown(transform, from, streamLength, pourMask, out var hit))
            {
                to = hit.point;
                var container = hit.collider.GetComponentInParent<MixingContainer>();
                if (container) container.AddIngredient(ingredient, flowRate * Time.deltaTime);
            }

            if (stream) stream.Show(from, to, ingredient.liquidColor);
        }
    }
}
