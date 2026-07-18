using System;
using UnityEngine;

namespace Game
{
    public static class GameAudioSettings
    {
        public const string VolumeKey = "World.Sound.Volume";

        private const float MutedThreshold = 0.01f;
        private static float volumeBeforeMute = 1f;

        public static event Action<float> VolumeChanged;

        public static float Volume => AudioListener.volume;
        public static bool IsMuted => Volume <= MutedThreshold;

        public static void Load()
        {
            SetVolume(PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume), false);
        }

        public static void SetVolume(float volume, bool save = true)
        {
            float clamped = Mathf.Clamp01(volume);
            if (clamped > MutedThreshold)
            {
                volumeBeforeMute = clamped;
            }

            AudioListener.volume = clamped;
            if (save)
            {
                PlayerPrefs.SetFloat(VolumeKey, clamped);
                PlayerPrefs.Save();
            }

            VolumeChanged?.Invoke(clamped);
        }

        public static void ToggleMute()
        {
            SetVolume(IsMuted ? Mathf.Max(MutedThreshold, volumeBeforeMute) : 0f);
        }
    }
}
