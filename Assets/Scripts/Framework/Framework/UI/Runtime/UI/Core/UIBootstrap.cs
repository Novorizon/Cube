using UnityEngine;

namespace UI
{
    public sealed class UIBootstrap : MonoBehaviour
    {
        [SerializeField] UISettings? settings;

        void Awake()
        {
            UIRoot root = UIRoot.Instance;
            if (settings != null)
            {
                root.SetSettings(settings);
            }
        }
    }
}
