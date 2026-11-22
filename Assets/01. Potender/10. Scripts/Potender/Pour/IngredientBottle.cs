using TMPro;
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

        [Header("재료 구분 표시")]
        [Tooltip("재료 색으로 칠할 렌더러 (병 몸통)")]
        [SerializeField] Renderer[] tintRenderers;
        [Tooltip("URP Lit = _BaseColor, Built-in = _Color")]
        [SerializeField] string colorProperty = "_BaseColor";
        [Tooltip("병 위에 뜨는 재료 이름")]
        [SerializeField] TMP_Text label;

        public IngredientData Ingredient => ingredient;
        public bool IsPouring { get; private set; }
        /// <summary>붓는 양 0~1 (PourFx 기울기 연출 꺼져 있으면 붓는 동안 1)</summary>
        public float Flow01 { get; private set; }

        readonly Vector3[] path = new Vector3[32];

        void Start()
        {
            ApplyTint();
            if (label && ingredient) label.text = ingredient.displayName;
        }

        void OnValidate() => ApplyTint();

        void ApplyTint()
        {
            if (!ingredient || tintRenderers == null) return;
            var mpb = new MaterialPropertyBlock();
            int id = Shader.PropertyToID(colorProperty);
            foreach (var r in tintRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(id, ingredient.liquidColor);
                r.SetPropertyBlock(mpb);
            }
        }

        void Update()
        {
            float angle = PourUtil.TiltAngle(transform);
            IsPouring = ingredient && spout && angle >= pourAngle;
            if (!IsPouring) { Flow01 = 0f; if (stream) stream.Hide(); return; }

            Flow01 = PourFx.FlowFor(angle, pourAngle);
            Vector3 v0 = PourFx.ExitVelocity(transform, Flow01);
            bool hitAny = PourUtil.Trace(transform, spout.position, v0, streamLength, pourMask, path, out int n, out var hit);

            MixingContainer container = null;
            if (hitAny)
            {
                container = hit.collider.GetComponentInParent<MixingContainer>();
                if (container)
                {
                    container.AddIngredient(ingredient, flowRate * Flow01 * Time.deltaTime);
                    MixingContainer.PourIntoLiquid(container.Liquid, ref n, ref hit, ingredient.liquidColor, Flow01, path);
                }
            }

            if (stream) stream.Show(path, n, ingredient.liquidColor, Flow01, hitAny, hit, !container);
        }
    }
}
