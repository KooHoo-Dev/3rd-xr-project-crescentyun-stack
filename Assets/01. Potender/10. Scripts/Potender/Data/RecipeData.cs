using UnityEngine;
using System;
using System.Collections.Generic;

namespace Potender
{
    [Serializable]
    public class RecipeIngredient
    {
        public IngredientData ingredient;

        [Tooltip("비율. 예 꿀 1 : 이슬 2")] [Min(0.01f)]
        public float parts = 1f;
    }

    [CreateAssetMenu(menuName = "Potender/Recipe", fileName = "RecipeNew")]
    public class RecipeData : ScriptableObject
    {
        public string potionName = "새 포션";
        [TextArea] public string effectDescription;
        public List<RecipeIngredient> ingredients = new();
        public MixMethod method = MixMethod.Shake;
        public Color potionColor = Color.magenta;
        public bool isAttackPotion;

        [Header("Day 3 — 손님 주문 대사 (포션당 2개)")]
        [Tooltip("손님이 이 포션을 원할 때 하는 말. 포션 이름은 직접 말하지 않게!")]
        [TextArea(2, 3)] public List<string> orderLines = new();
    }
}
