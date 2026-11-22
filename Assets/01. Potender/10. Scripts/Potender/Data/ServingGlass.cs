using System;
using UnityEngine;
using UnityEngine.Events;

namespace Potender
{
    /// <summary>서빙 잔: 판정 통과한 포션만 받음. 가득 차면 onFilled (Day 3 서빙과 연결).</summary>
    public class ServingGlass : MonoBehaviour
    {
        [SerializeField] float capacity = 0.4f;
        [SerializeField] LiquidVisual liquid;
        [Tooltip("이 각도 이상 기울이면 내용물을 쏟아 버림 (잘못 만든 포션 버리기)")]
        [SerializeField] float discardAngle = 100f;
        [SerializeField] float discardRate = 0.6f;

        public UnityEvent onFilled;
        public event Action<ServingGlass> Filled;

        public RecipeData Potion { get; private set; }
        public LiquidVisual Liquid => liquid;
        public float Fill { get; private set; }
        public bool IsFull => Fill >= capacity * 0.95f;

        public void Receive(RecipeData recipe, float amount)
        {
            if (!recipe || IsFull) return;
            if (Potion && Potion != recipe) return; // 다른 포션은 섞지 않음(넘쳐 흐름)

            Potion = recipe;
            Fill = Mathf.Min(capacity, Fill + amount);
            LastFillTime = Time.time;
            if (liquid) liquid.SetTarget(recipe.potionColor, Fill / capacity);

            if (IsFull)
            {
                onFilled?.Invoke();
                Filled?.Invoke(this);
            }
        }

        /// <summary>마지막으로 양이 바뀐 시각 (서빙 판단용)</summary>
        public float LastFillTime { get; private set; }

        void Update()
        {
            if (Fill <= 0f || !PourUtil.IsTilted(transform, discardAngle)) return;
            Fill -= discardRate * Time.deltaTime;
            LastFillTime = Time.time;
            if (Fill <= 0.001f) { Clear(); return; }
            if (liquid && Potion) liquid.SetTarget(Potion.potionColor, Fill / capacity);
        }

        public void Clear()
        {
            Potion = null;
            Fill = 0f;
            if (liquid) liquid.SetTarget(Color.clear, 0f);
        }
    }
}
