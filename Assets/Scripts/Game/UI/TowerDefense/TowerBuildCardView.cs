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

                Image skillIcon = FindIconImage(instance.transform);
                if (skillIcon != null)
                {
                    Sprite icon = loadIcon?.Invoke(config.IconLocation);
                    skillIcon.sprite = icon;
                    skillIcon.enabled = icon != null;
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
            if (skillContentRoot != null)
            {
                for (int i = skillContentRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(skillContentRoot.GetChild(i).gameObject);
                }
            }

            skillInstances.Clear();
        }

        private static Image FindIconImage(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == "Icon")
            {
                Image image = root.GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Image image = FindIconImage(root.GetChild(i));
                if (image != null)
                {
                    return image;
                }
            }

            return null;
        }
    }
}
