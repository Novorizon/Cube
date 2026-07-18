using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldFarmPanelView : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Transform seedContent;
        [SerializeField] private WorldFarmSeedView seedPrefab;

        public Button CloseButton => closeButton;
        public TMP_Text InfoText => infoText;
        public Transform SeedContent => seedContent;
        public WorldFarmSeedView SeedPrefab => seedPrefab;

        public void Configure(Button closeButton, TMP_Text infoText, Transform seedContent, WorldFarmSeedView seedPrefab)
        {
            this.closeButton = closeButton;
            this.infoText = infoText;
            this.seedContent = seedContent;
            this.seedPrefab = seedPrefab;
        }
    }
}
