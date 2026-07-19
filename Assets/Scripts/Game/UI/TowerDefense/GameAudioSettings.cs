using System;
using UnityEngine;

namespace Game
{
    public static class GameAudioSettings
    {
        public const string VolumeKey = "World.Sound.Volume";

        private const float MutedThreshold = 0.01f;
        private const float DefaultUnmuteVolume = 0.5f;
        private static float volumeBeforeMute = 1f;
        private static bool mutedFromButton;

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
            mutedFromButton = false;

            AudioListener.volume = clamped;
            if (save)
            {
                PlayerPrefs.SetFloat(VolumeKey, clamped);
                PlayerPrefs.Save();
            }

            VolumeChanged?.Invoke(clamped);
        }

        public static void Mute()
        {
            if (!IsMuted)
            {
                volumeBeforeMute = Volume;
            }

            SetVolume(0f);
            mutedFromButton = true;
        }

        public static void Unmute()
        {
            float restoreVolume = mutedFromButton && volumeBeforeMute > MutedThreshold
                ? volumeBeforeMute
                : DefaultUnmuteVolume;
            SetVolume(restoreVolume);
        }

        public static void ToggleMute()
        {
            if (IsMuted)
            {
                Unmute();
            }
            else
            {
                Mute();
            }
        }
    }
}
