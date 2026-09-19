using System.Collections.Generic;
using KToolkit;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban.UI
{
    [KUI_Info("screens/LevelSelect", nameof(LevelSelectPage))]
    public sealed class LevelSelectPage : StationPage
    {
        private readonly List<GameObject> rows = new List<GameObject>();
        private int selected;
        public override void OnStart()
        {
            var template = transform.Find("Content/List/Viewport/Rows/Template").gameObject;
            for (int i = 0; i < Runner.CampaignLevelCount; i++)
            {
                int index = i;
                var row = Object.Instantiate(template, template.transform.parent);
                row.name = "Level" + (i + 1).ToString("00");
                row.SetActive(true);
                row.transform.Find("Label").GetComponent<Text>().text = $"关卡 {i + 1:00}";
                Bind(row.GetComponent<Button>(), () => { selected = index; Refresh(); });
                rows.Add(row);
            }
            template.SetActive(false);
            Bind("Content/Enter", () => UI.EnterLevel(selected));
            Bind("Content/Back", () => Runner.CloseLevelSelect());
            for (int i = 0; i < rows.Count; i++)
                rows[i].GetComponent<Button>().navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? rows[i - 1].GetComponent<Button>() : Get<Button>("Content/Back"),
                    selectOnDown = i + 1 < rows.Count ? rows[i + 1].GetComponent<Button>() : Get<Button>("Content/Enter"),
                    selectOnLeft = Get<Button>("Content/Back"), selectOnRight = Get<Button>("Content/Enter")
                };
        }
        public void SelectCurrent()
        {
            selected = Mathf.Max(0, Runner.CampaignIndex >= 0 ? Runner.CampaignIndex : Runner.ContinueIndex);
            Get<ScrollRect>("Content/List").verticalNormalizedPosition = 1;
            Refresh();
        }
        public override void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].transform.Find("Marker").gameObject.SetActive(i == selected);
                rows[i].transform.Find("Status").GetComponent<Text>().text = Runner.GetCampaignStatus(i);
            }
            Get<Button>("Content/Enter").interactable = rows.Count > 0;
            Get<Text>("Content/Back/Label").text = Runner.Session == null ? "返回主菜单" : Runner.Completed ? "返回结算" : "返回游戏";
            Get<Text>("Content/Summary").text = $"{Runner.CompletedLevelCount} / {Runner.CampaignLevelCount} 已完成";
            if (rows.Count > 0)
            {
                var enter = Get<Button>("Content/Enter"); var back = Get<Button>("Content/Back");
                enter.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rows[selected].GetComponent<Button>(), selectOnLeft = back };
                back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rows[selected].GetComponent<Button>(), selectOnRight = enter };
            }
        }
        public override void FocusFirst()
        { if (rows.Count > 0) rows[selected].GetComponent<Button>().Select(); else base.FocusFirst(); }
    }
}
