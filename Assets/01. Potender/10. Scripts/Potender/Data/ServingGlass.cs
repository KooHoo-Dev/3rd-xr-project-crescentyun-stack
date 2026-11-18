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

        public UnityEvent onFilled;
        public event Action<ServingGlass> Filled;

        public RecipeData Potion { get; private set; }
        public float Fill { get; private set; }
        public bool IsFull => Fill >= capacity * 0.95f;

        public void Receive(RecipeData recipe, float amount)
        {
            if (!recipe || IsFull) return;
            if (Potion && Potion != recipe) return; // 다른 포션은 섞지 않음(넘쳐 흐름)

            Potion = recipe;
            Fill = Mathf.Min(capacity, Fill + amount);
            if (liquid) liquid.SetTarget(recipe.potionColor, Fill / capacity);

            if (IsFull)
            {
                onFilled?.Invoke();
                Filled?.Invoke(this);
            }
        }

        public void Clear()
        {
            Potion = null;
            Fill = 0f;
            if (liquid) liquid.SetTarget(Color.clear, 0f);
        }
    }
}
