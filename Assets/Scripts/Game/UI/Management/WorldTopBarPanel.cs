using System;
using TMPro;
using UnityEngine;

namespace Game
{
    internal sealed class WorldTopBarPanel
    {
        private Transform root;
        private Transform resourcesRoot;
        private TMP_Text resourceText;
        private TMP_Text dateText;
        private TMP_Text timeText;
        private RectTransform dayNightPointer;

        public TMP_Text ResourceText => resourceText;
        public TMP_Text DateText => dateText;
        public TMP_Text TimeText => timeText;
        public RectTransform DayNightPointer => dayNightPointer;

        public bool Bind(Transform rootTransform, Transform resourcesTransform, Action menuClicked)
        {
            root = rootTransform;
            resourcesRoot = resourcesTransform;
            if (root == null)
            {
                Clear();
                return false;
            }

            resourceText = WorldPanelBindingUtility.FindText(resourcesRoot, "Resources");
            Transform calendarWidget = root.Find("CalendarWidget");
            dateText = WorldPanelBindingUtility.FindText(calendarWidget, "DateText");
            timeText = WorldPanelBindingUtility.FindText(calendarWidget, "TimeText");
            Transform pointer = calendarWidget != null ? calendarWidget.Find("DayNightDisk/Pointer") : null;
            dayNightPointer = pointer as RectTransform;

            WorldPanelBindingUtility.BindButton(
                WorldPanelBindingUtility.FindFirst(root, "Entry_Menu", "Menu", "Setting"),
                () => menuClicked?.Invoke(),
                "Menu entry");

            return true;
        }

        public void RefreshCells(Func<int, int> getItemCount)
        {
            if (root == null || getItemCount == null)
            {
                return;
            }

            Transform resourceCellsRoot = resourcesRoot != null ? resourcesRoot : root;
            WorldPanelBindingUtility.SetChildText(resourceCellsRoot, "GoldCell", $"{LocalizationManager.Get("ui.topbar.gold")}\n{getItemCount(ItemIds.Gold)}");
            WorldPanelBindingUtility.SetChildText(resourceCellsRoot, "WoodCell", $"{LocalizationManager.Get("ui.topbar.wood")}\n{getItemCount(ItemIds.Wood)}");
            WorldPanelBindingUtility.SetChildText(resourceCellsRoot, "StoneCell", $"{LocalizationManager.Get("ui.topbar.stone")}\n{getItemCount(ItemIds.Stone)}");
            WorldPanelBindingUtility.SetChildText(resourceCellsRoot, "FoodCell", $"{LocalizationManager.Get("ui.topbar.food")}\n--");
            WorldPanelBindingUtility.SetChildText(resourceCellsRoot, "OreCell", $"{LocalizationManager.Get("ui.topbar.ore")}\n--");
            WorldPanelBindingUtility.SetChildText(resourceCellsRoot, "CropCell", $"{LocalizationManager.Get("ui.topbar.crop")}\n--");

            CalendarManager calendar = CalendarManager.Instance;
            WorldPanelBindingUtility.SetChildText(root, "DateCell", $"{LocalizationManager.Get("ui.topbar.date")}\n{calendar.GetDateText()}");
            WorldPanelBindingUtility.SetChildText(root, "TimeCell", $"{LocalizationManager.Get("ui.topbar.time")}\n{calendar.GetTimeText()}");
            WorldPanelBindingUtility.SetChildText(root, "SeasonCell", $"{LocalizationManager.Get("ui.topbar.season")}\n{CalendarManager.GetSeasonName(calendar.Season)}");
            WorldPanelBindingUtility.SetChildText(root, "WeatherCell", $"{LocalizationManager.Get("ui.topbar.weather")}\n{LocalizationManager.Format("ui.topbar.weather_value", LocalizationManager.Get("ui.weather.sunny"), 22)}");
            WorldPanelBindingUtility.SetChildText(root, "Setting", LocalizationManager.Get("ui.topbar.setting"));
            WorldPanelBindingUtility.SetChildText(root, "Entry_Menu", LocalizationManager.Get("ui.topbar.setting"));
            WorldPanelBindingUtility.SetChildText(root, "Menu", LocalizationManager.Get("ui.topbar.setting"));
        }

        public void RefreshCalendarWidget()
        {
            CalendarManager calendar = CalendarManager.Instance;
            if (dateText != null)
            {
                dateText.text = calendar.GetDateText();
            }

            if (timeText != null)
            {
                timeText.text = calendar.GetTimeText();
            }

            if (dayNightPointer != null)
            {
                dayNightPointer.localEulerAngles = new Vector3(0f, 0f, -calendar.DayNightProgress * 360f);
            }
        }

        private void Clear()
        {
            resourcesRoot = null;
            resourceText = null;
            dateText = null;
            timeText = null;
            dayNightPointer = null;
        }
    }
}
