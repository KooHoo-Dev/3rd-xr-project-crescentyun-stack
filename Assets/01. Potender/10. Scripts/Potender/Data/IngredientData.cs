using UnityEngine;

namespace Potender
{
    [CreateAssetMenu(menuName = "Potender/Data/Ingredient Data", fileName = "Ing_New")]
    public class IngredientData : ScriptableObject
    {
        public string displayName = "새 재료";
        public Color liquidColor = Color.white;
    }
}