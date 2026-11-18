using System.Collections.Generic;
using UnityEngine;

namespace Potender
{
    public struct JudgeResult
    {
        public bool success;
        public RecipeData recipe;   // 성공: 만든 포션 / 실패: 시도한 것으로 보이는 포션(없으면 null)
        public string reason;

        public static JudgeResult Ok(RecipeData r) => new() { success = true, recipe = r, reason = "성공" };
        public static JudgeResult Fail(RecipeData r, string why) => new() { success = false, recipe = r, reason = why };
    }

    /// <summary>레시피 목록 + 판정 규칙. Create > Potender > Recipe Book</summary>
    [CreateAssetMenu(menuName = "Potender/Recipe Book", fileName = "RecipeBook")]
    public class RecipeBook : ScriptableObject
    {
        public List<RecipeData> recipes = new();

        [Header("판정 기준")]
        [Tooltip("이 비율 미만으로 들어간 재료는 '몇 방울 튄 것'으로 보고 무시")]
        [Range(0f, 0.3f)] public float ignoreShare = 0.08f;
        [Tooltip("레시피 비율과의 허용 오차(비율 차이). 1이면 비율 무시, 재료 종류만 맞으면 OK")]
        [Range(0f, 1f)] public float ratioTolerance = 0.2f;
        [Tooltip("최소 총량(용기 용량 대비 아님, 절대량)")]
        public float minTotal = 0.3f;

        readonly HashSet<IngredientData> present = new();

        public JudgeResult Evaluate(IReadOnlyDictionary<IngredientData, float> contents, MixMethod method)
        {
            float total = 0f;
            foreach (var kv in contents) total += kv.Value;
            if (total < minTotal) return JudgeResult.Fail(null, "양이 부족함");

            present.Clear();
            foreach (var kv in contents)
                if (kv.Value / total >= ignoreShare) present.Add(kv.Key);

            foreach (var recipe in recipes)
            {
                if (recipe == null || !SameSet(recipe)) continue;

                float partsSum = 0f;
                foreach (var ri in recipe.ingredients) partsSum += ri.parts;

                foreach (var ri in recipe.ingredients)
                {
                    float want = ri.parts / partsSum;
                    float got = contents[ri.ingredient] / total;
                    if (Mathf.Abs(want - got) > ratioTolerance)
                        return JudgeResult.Fail(recipe, $"{ri.ingredient.displayName} 비율이 맞지 않음");
                }

                if (method == MixMethod.None) return JudgeResult.Fail(recipe, "섞지 않음");
                if (method != recipe.method) return JudgeResult.Fail(recipe, "제조법이 다름");
                return JudgeResult.Ok(recipe);
            }
            return JudgeResult.Fail(null, "알 수 없는 조합");
        }

        bool SameSet(RecipeData recipe)
        {
            if (recipe.ingredients.Count != present.Count) return false;
            foreach (var ri in recipe.ingredients)
                if (ri.ingredient == null || !present.Contains(ri.ingredient)) return false;
            return true;
        }
    }
}
