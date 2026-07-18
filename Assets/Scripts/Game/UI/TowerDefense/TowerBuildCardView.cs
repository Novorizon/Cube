using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class TowerBuildCardView : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Image selectedFrame;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text costText;

        [SerializeField]
        private RectTransform skillContentRoot;

        public int TowerId { get; private set; }

        private Action<int> clickedCallback;
        private readonly List<GameObject> skillInstances = new List<GameObject>();
        private bool affordable = true;

        public void Init(int towerId, string towerName, int cost, Action<int> clickedCallback)
        {
            TowerId = towerId;
            this.clickedCallback = clickedCallback;

            if (nameText != null)
            {
                nameText.text = towerName;
            }

            if (costText != null)
            {
                costText.text = cost.ToString();
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
                button.onClick.AddListener(OnButtonClicked);
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
            {
                selectedFrame.gameObject.SetActive(selected);
            }
        }

        public void SetAffordable(bool value)
        {
            affordable = value;
            if (button != null)
            {
                button.interactable = affordable;
            }
        }

        public void SetSkills(IReadOnlyList<SkillConfig> skills, GameObject skillPrefab, Func<string, Sprite> loadIcon)
        {
            ClearSkills();

            if (skillContentRoot == null || skillPrefab == null || skills == null)
            {
                return;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                SkillConfig config = skills[i];
                if (config == null)
                {
                    continue;
                }

                GameObject instance = Instantiate(skillPrefab, skillContentRoot, false);
                instance.name = $"Skill_{config.Id}";
                skillInstances.Add(instance);

                BattleSlotContentView contentView = instance.GetComponent<BattleSlotContentView>();
                if (contentView != null)
                {
                    Sprite icon = loadIcon?.Invoke(config.IconLocation);
                    contentView.SetIcon(icon);
                }
                else
                {
                    Debug.LogError($"Tower skill prefab is missing {nameof(BattleSlotContentView)}.", skillPrefab);
                }
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }

            ClearSkills();
            clickedCallback = null;
        }

        private void OnButtonClicked()
        {
            clickedCallback?.Invoke(TowerId);
        }

        private void ClearSkills()
        {
            for (int i = skillInstances.Count - 1; i >= 0; i--)
            {
                if (skillInstances[i] != null)
                {
                    Destroy(skillInstances[i]);
                }
            }

            skillInstances.Clear();
        }
    }
}
