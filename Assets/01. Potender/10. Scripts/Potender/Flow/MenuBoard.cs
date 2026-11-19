using System.Text;
using TMPro;
using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 벽 메뉴판: RecipeBook 내용으로 텍스트 자동 생성 (이름 · 효과 · 재료 비율 · 제조법).
    /// 레시피를 고치면 컴포넌트 ⋮ 메뉴 → Refresh 로 에디터에서도 확인 가능.
    /// </summary>
    public class MenuBoard : MonoBehaviour
    {
        [SerializeField] RecipeBook recipeBook;
        [SerializeField] TMP_Text text;
        [SerializeField] string title = "~ 오늘의 포션 ~";
        [SerializeField] string shakeLabel = "흔들기 (쉐이커)";
        [SerializeField] string stirLabel = "젓기 (믹싱 글라스)";
        [Tooltip("공격 포션은 Day 4 강도 이벤트용. 메뉴판에 '비상용'으로 표시")]
        [SerializeField] bool showAttackPotions = true;

        void Start() => Refresh();

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            if (!recipeBook || !text) return;
            var sb = new StringBuilder();
            sb.Append($"<align=center><size=130%><b>{title}</b></size></align>\n\n");

            foreach (var r in recipeBook.recipes)
            {
                if (!r || (r.isAttackPotion && !showAttackPotions)) continue;
                string hex = ColorUtility.ToHtmlStringRGB(r.potionColor);

                sb.Append($"<size=115%><b><color=#{hex}>■</color> {r.potionName}</b></size>");
                if (r.isAttackPotion) sb.Append("  <color=#FF7043>[비상용]</color>");
                sb.Append('\n');

                if (!string.IsNullOrWhiteSpace(r.effectDescription))
                    sb.Append($"<i>{r.effectDescription}</i>\n");

                sb.Append("재료: ");
                for (int i = 0; i < r.ingredients.Count; i++)
                {
                    var ri = r.ingredients[i];
                    if (i > 0) sb.Append(" : ");
                    sb.Append($"{(ri.ingredient ? ri.ingredient.displayName : "?")} {ri.parts:0.#}");
                }
                sb.Append($"\n제조: {(r.method == MixMethod.Stir ? stirLabel : shakeLabel)}\n\n");
            }
            text.text = sb.ToString();
        }
    }
}
