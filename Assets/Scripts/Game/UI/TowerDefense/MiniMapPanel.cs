using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public enum MiniMapIconType
    {
        Enemy,
        Tower,
        Base,
        Path,
        Player
    }

    public sealed class MiniMapPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform mapRoot;
        [SerializeField] private Image iconPrefab;
        [SerializeField] private Sprite enemySprite;
        [SerializeField] private Sprite towerSprite;
        [SerializeField] private Sprite baseSprite;
        [SerializeField] private Sprite pathSprite;
        [SerializeField] private Sprite playerSprite;
        [SerializeField] private Color enemyColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color towerColor = new Color(0.2f, 0.55f, 1f, 1f);
        [SerializeField] private Color baseColor = new Color(0.1f, 0.8f, 1f, 1f);
        [SerializeField] private Color pathColor = new Color(1f, 0.8f, 0.25f, 1f);
        [SerializeField] private Color playerColor = new Color(0.2f, 0.75f, 1f, 1f);

        private readonly List<Image> icons = new List<Image>();
        private Vector2 minPosition;
        private Vector2 maxPosition = Vector2.one;

        public void SetMapBounds(Vector2 min, Vector2 max)
        {
            minPosition = min;
            maxPosition = max;
        }

        public void Clear()
        {
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] != null)
                {
                    Destroy(icons[i].gameObject);
                }
            }
            icons.Clear();
        }

        public void AddIcon(Vector2 mapPosition, MiniMapIconType type)
        {
            if (mapRoot == null || iconPrefab == null)
            {
                return;
            }

            Image icon = Instantiate(iconPrefab, mapRoot);
            icon.gameObject.SetActive(true);
            icon.enabled = true;
            icon.sprite = GetSprite(type);
            icon.color = icon.sprite != null ? Color.white : GetColor(type);
            icon.rectTransform.anchoredPosition = MapToUi(mapPosition);
            icon.rectTransform.sizeDelta = GetIconSize(type);
            icons.Add(icon);
        }

        private Vector2 MapToUi(Vector2 mapPosition)
        {
            Rect rect = mapRoot.rect;
            float width = Mathf.Max(0.01f, maxPosition.x - minPosition.x);
            float height = Mathf.Max(0.01f, maxPosition.y - minPosition.y);
            float x = Mathf.InverseLerp(minPosition.x, maxPosition.x, mapPosition.x) * rect.width;
            float y = Mathf.InverseLerp(minPosition.y, maxPosition.y, mapPosition.y) * rect.height;
            return new Vector2(x - rect.width * 0.5f, y - rect.height * 0.5f);
        }

        private Vector2 GetIconSize(MiniMapIconType type)
        {
            switch (type)
            {
                case MiniMapIconType.Base:
                    return new Vector2(22f, 22f);
                case MiniMapIconType.Player:
                    return new Vector2(18f, 18f);
                case MiniMapIconType.Path:
                    return new Vector2(8f, 8f);
                default:
                    return new Vector2(12f, 12f);
            }
        }

        private Sprite GetSprite(MiniMapIconType type)
        {
            switch (type)
            {
                case MiniMapIconType.Enemy:
                    return enemySprite;
                case MiniMapIconType.Tower:
                    return towerSprite;
                case MiniMapIconType.Base:
                    return baseSprite;
                case MiniMapIconType.Path:
                    return pathSprite;
                case MiniMapIconType.Player:
                    return playerSprite;
                default:
                    return null;
            }
        }

        private Color GetColor(MiniMapIconType type)
        {
            switch (type)
            {
                case MiniMapIconType.Enemy:
                    return enemyColor;
                case MiniMapIconType.Tower:
                    return towerColor;
                case MiniMapIconType.Base:
                    return baseColor;
                case MiniMapIconType.Path:
                    return pathColor;
                case MiniMapIconType.Player:
                    return playerColor;
                default:
                    return Color.white;
            }
        }
    }
}