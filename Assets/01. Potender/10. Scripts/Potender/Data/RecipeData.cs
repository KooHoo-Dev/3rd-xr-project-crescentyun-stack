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
    }
}