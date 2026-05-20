using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/Tower Defense UI Config", fileName = "TowerDefenseUIConfig")]
    public sealed class TdUiConfig : ScriptableObject
    {
        public TdTowerUiConfig[] Towers;
        public TdSkillUiConfig[] Skills;
    }
}
