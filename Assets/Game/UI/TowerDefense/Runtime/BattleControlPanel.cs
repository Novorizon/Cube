using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class BattleControlPanel : MonoBehaviour
    {
        [SerializeField] private Button speed1Button;
        [SerializeField] private Button speed2Button;
        [SerializeField] private Button speed3Button;
        [SerializeField] private Toggle autoNextWaveToggle;

        public event Action<float> SpeedChanged;
        public event Action<bool> AutoNextWaveChanged;

        private void Awake()
        {
            if (speed1Button != null)
            {
                speed1Button.onClick.AddListener(() => SetSpeed(1f));
            }
            if (speed2Button != null)
            {
                speed2Button.onClick.AddListener(() => SetSpeed(2f));
            }
            if (speed3Button != null)
            {
                speed3Button.onClick.AddListener(() => SetSpeed(3f));
            }
            if (autoNextWaveToggle != null)
            {
                autoNextWaveToggle.onValueChanged.AddListener(OnAutoNextWaveChanged);
            }
        }

        public void SetSpeed(float speed)
        {
            Time.timeScale = Mathf.Max(0.01f, speed);
            SpeedChanged?.Invoke(speed);
        }

        public void SetAutoNextWave(bool value)
        {
            if (autoNextWaveToggle != null)
            {
                autoNextWaveToggle.SetIsOnWithoutNotify(value);
            }
            AutoNextWaveChanged?.Invoke(value);
        }

        private void OnAutoNextWaveChanged(bool value)
        {
            AutoNextWaveChanged?.Invoke(value);
        }
    }
}
