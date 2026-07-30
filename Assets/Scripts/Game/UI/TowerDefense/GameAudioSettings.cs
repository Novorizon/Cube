using System;
using Game.Framework;

namespace Game
{
    /// <summary>
    /// Compatibility facade for existing UI. New playback and volume code should
    /// use AudioManager directly.
    /// </summary>
    public static class GameAudioSettings
    {
        public const string VolumeKey = AudioManager.MasterVolumeKey;

        public static event Action<float> VolumeChanged
        {
            add => AudioManager.Instance.MasterVolumeChanged += value;
            remove
            {
                if (AudioManager.HasInstance)
                {
                    AudioManager.Instance.MasterVolumeChanged -= value;
                }
            }
        }

        public static float Volume => AudioManager.Instance.MasterVolume;
        public static bool IsMuted => AudioManager.Instance.IsMuted;

        public static void Load()
        {
            AudioManager.Instance.LoadPreferences();
        }

        public static void SetVolume(float volume, bool save = true)
        {
            AudioManager.Instance.SetMasterVolume(volume, save);
        }

        public static void Mute()
        {
            AudioManager.Instance.Mute();
        }

        public static void Unmute()
        {
            AudioManager.Instance.Unmute();
        }

        public static void ToggleMute()
        {
            AudioManager.Instance.ToggleMute();
        }
    }
}
