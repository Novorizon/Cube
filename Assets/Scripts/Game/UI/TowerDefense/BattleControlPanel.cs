using System;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class BattleControlPanel : UIPanel
    {
        [SerializeField] private Button speed1Button;
        [SerializeField] private Button speed2Button;
        [SerializeField] private Button speed3Button;
        [SerializeField] private Toggle autoNextWaveToggle;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button soundButton;
        [SerializeField] private Button settingButton;
        [SerializeField] private GameObject speed1SelectedState;
        [SerializeField] private GameObject speed2SelectedState;
        [SerializeField] private GameObject speed3SelectedState;
        [SerializeField] private GameObject mutedState;

        public event Action<float> SpeedChanged;
        public event Action<bool> AutoNextWaveChanged;
        public event Action SettingClicked;

        private float lastSpeed = 1f;
        private bool paused;

        public bool AutoNextWaveEnabled => autoNextWaveToggle != null && autoNextWaveToggle.isOn;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.None;

        protected override void OnCreate()
        {
            if (speed1Button != null)
            {
                speed1Button.onClick.AddListener(SetSpeed1);
            }
            if (speed2Button != null)
            {
                speed2Button.onClick.AddListener(SetSpeed2);
            }
            if (speed3Button != null)
            {
                speed3Button.onClick.AddListener(SetSpeed3);
            }
            if (autoNextWaveToggle != null)
            {
                autoNextWaveToggle.onValueChanged.AddListener(OnAutoNextWaveChanged);
            }
            if (pauseButton != null)
            {
                pauseButton.onClick.AddListener(Pause);
            }
            if (playButton != null)
            {
                playButton.onClick.AddListener(Resume);
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

        protected override void OnOpen(object args)
        {
            GameAudioSettings.Load();
            GameAudioSettings.VolumeChanged += OnVolumeChanged;
            RefreshState();
        }

        protected override void OnClose()
        {
            GameAudioSettings.VolumeChanged -= OnVolumeChanged;
        }

        protected override void OnDestroyed()
        {
            GameAudioSettings.VolumeChanged -= OnVolumeChanged;

            speed1Button?.onClick.RemoveListener(SetSpeed1);
            speed2Button?.onClick.RemoveListener(SetSpeed2);
            speed3Button?.onClick.RemoveListener(SetSpeed3);
            autoNextWaveToggle?.onValueChanged.RemoveListener(OnAutoNextWaveChanged);
            pauseButton?.onClick.RemoveListener(Pause);
            playButton?.onClick.RemoveListener(Resume);
            soundButton?.onClick.RemoveListener(SetSound);
            settingButton?.onClick.RemoveListener(OpenSetting);

            SpeedChanged = null;
            AutoNextWaveChanged = null;
            SettingClicked = null;
        }

        public void SetSpeed(float speed)
        {
            paused = false;
            lastSpeed = Mathf.Max(0.01f, speed);
            Time.timeScale = lastSpeed;
            RefreshState();
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

        private void SetSpeed1()
        {
            SetSpeed(1f);
        }

        private void SetSpeed2()
        {
            SetSpeed(2f);
        }

        private void SetSpeed3()
        {
            SetSpeed(3f);
        }

        public void SetPause()
        {
            SetPaused(!paused);
        }

        private void Pause()
        {
            SetPaused(true);
        }

        private void Resume()
        {
            SetPaused(false);
        }

        private void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = paused ? 0f : lastSpeed;
            RefreshState();
            SpeedChanged?.Invoke(Time.timeScale);
        }

        public void SetSound()
        {
            GameAudioSettings.ToggleMute();
        }

        public void OpenSetting()
        {
            SettingClicked?.Invoke();
        }

        private void OnVolumeChanged(float volume)
        {
            RefreshState();
        }

        private void RefreshState()
        {
            SetActive(speed1SelectedState, !paused && Mathf.Approximately(lastSpeed, 1f));
            SetActive(speed2SelectedState, !paused && Mathf.Approximately(lastSpeed, 2f));
            SetActive(speed3SelectedState, !paused && Mathf.Approximately(lastSpeed, 3f));
            SetActive(pauseButton != null ? pauseButton.gameObject : null, !paused);
            SetActive(playButton != null ? playButton.gameObject : null, paused);
            SetActive(mutedState, GameAudioSettings.IsMuted);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
