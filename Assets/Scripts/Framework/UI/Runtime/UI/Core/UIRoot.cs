using UnityEngine;

namespace UI
{
    [System.Obsolete("Use UIManager instead. This class is kept only for old code compatibility.")]
    public sealed class UIRoot : MonoBehaviour
    {
        public static UIManager Instance => UIManager.Instance;
    }
}
