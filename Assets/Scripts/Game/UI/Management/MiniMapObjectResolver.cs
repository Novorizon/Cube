using Game.Framework;
using UnityEngine;

namespace Game
{
    public readonly struct MiniMapObjectStyle
    {
        public readonly bool Visible;
        public readonly Sprite Icon;
        public readonly Color Color;
        public readonly Vector2 Size;

        public MiniMapObjectStyle(bool visible, Sprite icon, Color color, Vector2 size)
        {
            Visible = visible;
            Icon = icon;
            Color = color;
            Size = size;
        }
    }

    public static class MiniMapObjectResolver
    {
        public static MiniMapObjectStyle Resolve(MapObjectData mapObject, bool questTarget)
        {
            if (mapObject == null || mapObject.MiniMapVisibility == MiniMapVisibility.Hide)
            {
                return new MiniMapObjectStyle(false, null, Color.clear, Vector2.zero);
            }

            bool showByDefault;
            string iconLocation;
            Sprite directIcon = null;
            Color fallbackColor;
            Vector2 size;

            switch (mapObject.ObjectType)
            {
                case MapObjectType.Decoration:
                    if (!MapManager.Instance.TryGetDecorationConfig(mapObject.ConfigId, out MapDecorationPrefabConfig.DecorationPrefabItem decoration))
                    {
                        return ResolveMissingConfig(mapObject, questTarget);
                    }

                    showByDefault = decoration.ShowOnMiniMap;
                    directIcon = decoration.MiniMapIcon;
                    iconLocation = string.Empty;
                    fallbackColor = new Color(0.78f, 0.72f, 0.44f, 1f);
                    size = new Vector2(11f, 11f);
                    break;

                case MapObjectType.Resource:
                    if (DataManager.Instance.Resource == null ||
                        !DataManager.Instance.Resource.TryGet(mapObject.ConfigId, out ResourceConfig resource) ||
                        resource == null ||
                        !resource.Enable)
                    {
                        return ResolveMissingConfig(mapObject, questTarget);
                    }

                    showByDefault = resource.ShowOnMiniMap;
                    iconLocation = string.IsNullOrWhiteSpace(resource.MiniMapIconLocation)
                        ? resource.IconLocation
                        : resource.MiniMapIconLocation;
                    fallbackColor = new Color(0.26f, 0.78f, 0.42f, 1f);
                    size = new Vector2(13f, 13f);
                    break;

                case MapObjectType.Building:
                    if (DataManager.Instance.WorldBuilding == null ||
                        !DataManager.Instance.WorldBuilding.TryGet(mapObject.ConfigId, out WorldBuildingConfig building) ||
                        building == null ||
                        !building.Enable)
                    {
                        return ResolveMissingConfig(mapObject, questTarget);
                    }

                    showByDefault = building.ShowOnMiniMap;
                    iconLocation = string.IsNullOrWhiteSpace(building.MiniMapIconLocation)
                        ? building.IconLocation
                        : building.MiniMapIconLocation;
                    fallbackColor = new Color(0.30f, 0.62f, 0.94f, 1f);
                    size = new Vector2(16f, 16f);
                    break;

                case MapObjectType.Interactable:
                    showByDefault = false;
                    iconLocation = string.Empty;
                    fallbackColor = new Color(0.95f, 0.64f, 0.20f, 1f);
                    size = new Vector2(13f, 13f);
                    break;

                default:
                    return new MiniMapObjectStyle(false, null, Color.clear, Vector2.zero);
            }

            bool visible = mapObject.MiniMapVisibility == MiniMapVisibility.Show || questTarget || showByDefault;
            if (!visible)
            {
                return new MiniMapObjectStyle(false, null, Color.clear, Vector2.zero);
            }

            Sprite icon = directIcon;
            if (icon == null && !string.IsNullOrWhiteSpace(iconLocation))
            {
                icon = ResourceManager.Instance.LoadAsset<Sprite>(iconLocation);
            }

            return new MiniMapObjectStyle(true, icon, fallbackColor, size);
        }

        private static MiniMapObjectStyle ResolveMissingConfig(MapObjectData mapObject, bool questTarget)
        {
            bool visible = mapObject.MiniMapVisibility == MiniMapVisibility.Show || questTarget;
            return new MiniMapObjectStyle(
                visible,
                null,
                new Color(0.92f, 0.38f, 0.32f, 1f),
                new Vector2(12f, 12f));
        }

    }
}
