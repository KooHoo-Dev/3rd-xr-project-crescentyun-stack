using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 벽 메뉴판: RecipeBook 내용으로 텍스트 자동 생성 (이름 · 효과 · 재료 비율 · 제조법).
    /// extraPages에 판을 더 연결하면 레시피를 나눠서 이어서 씀 (첫 판 → 둘째 판 …).
    /// singlesPage를 연결하면 단품(재료 1개 레시피)만 그 판에 따로 적음.
    /// pageTitles: 판마다 제목 (text → extraPages → singlesPage 순서). 예: 당신도 / 할 수 있다 / 포션 마스터
    /// 레시피를 고치면 컴포넌트 ⋮ 메뉴 → Refresh 로 에디터에서도 확인 가능.
    /// </summary>
    public class MenuBoard : MonoBehaviour
    {
        [SerializeField] RecipeBook recipeBook;
        [SerializeField] TMP_Text text;
        [Tooltip("이어서 쓸 판 (예: 왼쪽에 붙인 두 번째 메뉴판)")]
        [SerializeField] TMP_Text[] extraPages;
        [SerializeField] string title = "~ 오늘의 포션 ~";
        [SerializeField] string continuedTitle = "~ 오늘의 포션 (계속) ~";
        [Tooltip("판마다 제목 (text → extraPages → singlesPage 순서). 비우면 title / continuedTitle")]
        [SerializeField] string[] pageTitles;
        [Tooltip("단품만 따로 적을 판. 비우면 레시피 판 맨 끝에 한 칸으로 묶음")]
        [SerializeField] TMP_Text singlesPage;
        [SerializeField] string shakeLabel = "흔들기 (쉐이커)";
        [SerializeField] string stirLabel = "젓기 (믹싱 글라스)";
        [SerializeField] string iceLabel = " + 얼음";
        [Tooltip("재료 1개짜리 레시피(꿀 한 잔 등)를 메뉴판 맨 끝에 한 칸으로 묶어서 표시")]
        [SerializeField] bool groupSingles = true;
        [SerializeField] string singlesTitle = "단품 한 잔";
        [Tooltip("공격 포션은 Day 4 강도 이벤트용. 메뉴판에 '비상용'으로 표시")]
        [SerializeField] bool showAttackPotions = true;

        void Start() => Refresh();

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            if (!recipeBook || !text) return;

            var pages = new List<TMP_Text> { text };
            if (extraPages != null) foreach (var p in extraPages) if (p) pages.Add(p);

            var entries = new List<string>();
            var singles = new List<RecipeData>();
            foreach (var r in recipeBook.recipes)
            {
                if (!r || (r.isAttackPotion && !showAttackPotions)) continue;
                if (groupSingles && r.ingredients.Count == 1) singles.Add(r);
                else entries.Add(Entry(r));
            }
            bool separate = singlesPage && groupSingles;
            if (singles.Count > 0 && !separate) entries.Add(SinglesEntry(singles));

            int perPage = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)pages.Count));
            for (int p = 0; p < pages.Count; p++)
            {
                var sb = new StringBuilder();
                sb.Append(Header(TitleFor(p, p == 0 ? title : continuedTitle)));
                for (int i = p * perPage; i < Mathf.Min(entries.Count, (p + 1) * perPage); i++)
                    sb.Append(entries[i]);
                pages[p].text = sb.ToString();
            }

            if (separate) singlesPage.text = Header(TitleFor(pages.Count, singlesTitle)) + SinglesPage(singles);
        }

        string TitleFor(int index, string fallback) =>
            pageTitles != null && index < pageTitles.Length && !string.IsNullOrWhiteSpace(pageTitles[index])
                ? pageTitles[index] : fallback;

        static string Header(string t) => $"<align=center><size=130%><b>{t}</b></size></align>\n\n";

        // 단품 전용 판: 한 줄에 하나씩
        string SinglesPage(List<RecipeData> singles)
        {
            var sb = new StringBuilder();
            sb.Append($"<size=115%><b>■ {singlesTitle}</b></size>\n");
            if (singles.Count == 0) return sb.Append("(없음)").ToString();
            var first = singles[0];
            sb.Append($"<i>재료 하나{(first.needsIce ? iceLabel : "")} → {(first.method == MixMethod.Stir ? stirLabel : shakeLabel)}</i>\n\n");
            foreach (var r in singles)
            {
                string hex = ColorUtility.ToHtmlStringRGB(r.potionColor);
                sb.Append($"<color=#{hex}>●</color> {r.potionName}\n");
            }
            return sb.ToString();
        }

        string SinglesEntry(List<RecipeData> singles)
        {
            var sb = new StringBuilder();
            sb.Append($"<size=115%><b>■ {singlesTitle}</b></size>\n");
            for (int i = 0; i < singles.Count; i++)
            {
                var ing = singles[i].ingredients[0].ingredient;
                string hex = ColorUtility.ToHtmlStringRGB(singles[i].potionColor);
                if (i > 0) sb.Append(" · ");
                sb.Append($"<color=#{hex}>●</color> {(ing ? ing.displayName : singles[i].potionName)}");
            }
            var first = singles[0];
            sb.Append($"\n제조: 재료 하나{(first.needsIce ? iceLabel : "")} → {(first.method == MixMethod.Stir ? stirLabel : shakeLabel)}\n\n");
            return sb.ToString();
        }

        string Entry(RecipeData r)
        {
            var sb = new StringBuilder();
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
            if (r.needsIce) sb.Append(iceLabel);
            sb.Append($"\n제조: {(r.method == MixMethod.Stir ? stirLabel : shakeLabel)}\n\n");
            return sb.ToString();
        }
    }
}
