using UnityEngine;

namespace UI
{
    public sealed class UIBootstrap : MonoBehaviour
    {
        [SerializeField] UISettings settings;
        [SerializeField] bool useResourceManagerLoader = true;

        void Awake()
        {
            UIManager manager = UIManager.Instance;
            if (settings != null)
            {
                manager.SetSettings(settings);
            }

            if (useResourceManagerLoader)
            {
                manager.UseResourceManagerLoader();
            }
        }
    }
}
