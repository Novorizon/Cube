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
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button soundButton;
        [SerializeField] private Button settingButton;

        public event Action<float> SpeedChanged;
        public event Action<bool> AutoNextWaveChanged;
        public event Action SettingClicked;

        private float lastSpeed = 1f;
        private bool paused;

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
            if (pauseButton != null)
            {
                pauseButton.onClick.AddListener(SetPause);
            }
            if (soundButton != null)
            {
                soundButton.onClick.AddListener(SetSound);
            }
            if (settingButton != null)
            {
                settingButton.onClick.AddListener(OpenSetting);
            }
        }

        public void SetSpeed(float speed)
        {
            paused = false;
            lastSpeed = Mathf.Max(0.01f, speed);
            Time.timeScale = lastSpeed;
            SpeedChanged?.Invoke(lastSpeed);
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
        public void SetPause()
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : lastSpeed;
            SpeedChanged?.Invoke(Time.timeScale);
        }

        public void SetSound()
        {
            // Audio mute will be routed here once an audio manager exists.
        }

        public void OpenSetting()
        {
            SettingClicked?.Invoke();
        }

    }
}
