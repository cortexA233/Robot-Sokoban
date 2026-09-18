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
                row.transform.Find("Label").GetComponent<Text>().text = $"{i + 1:00}   {Runner.GetCampaignTitle(i)}";
                Bind(row.GetComponent<Button>(), () => { selected = index; Refresh(); });
                rows.Add(row);
            }
            template.SetActive(false);
            Bind("Content/Enter", () => UI.EnterLevel(selected));
            Bind("Content/Back", () => Runner.CloseLevelSelect());
        }
        public void SelectCurrent()
        {
            selected = Mathf.Max(0, Runner.CampaignIndex);
            Get<ScrollRect>("Content/List").verticalNormalizedPosition = 1;
            Refresh();
        }
        public override void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].transform.Find("Marker").gameObject.SetActive(i == selected);
                rows[i].transform.Find("Selected").gameObject.SetActive(i == selected);
            }
            Get<Button>("Content/Enter").interactable = rows.Count > 0;
            Get<Text>("Content/Back/Label").text = Runner.Session == null ? "返回主菜单" : Runner.Completed ? "返回结算" : "返回游戏";
        }
    }
}
