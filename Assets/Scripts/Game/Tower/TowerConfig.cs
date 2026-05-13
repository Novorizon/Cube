using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "TowerConfig", menuName = "Game/Tower/TowerConfig")]
    public sealed class TowerConfig : ScriptableObject
    {
        [SerializeField]
        private List<TowerConfigItem> items = new List<TowerConfigItem>();

        public TowerConfigItem GetItem(TowerType type)
        {
            for (int i = 0; i < items.Count; i++)
            {
                TowerConfigItem item = items[i];

                if (item == null)
                {
                    continue;
                }

                if (item.Type == type)
                {
                    return item;
                }
            }

            return null;
        }
    }
}
