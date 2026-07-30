using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Framework
{
    /// <summary>
    /// Small project-wide audio facade with one music source and a reusable sound pool.
    /// Public playback is intentionally limited to PlayMusic and PlaySound.
    /// </summary>
    public sealed class AudioManager : MonoSingleton<AudioManager>
    {
        public const string MasterVolumeKey = "World.Sound.Volume";
        public const string MusicVolumeKey = "Audio.Music.Volume";
        public const string SoundVolumeKey = "Audio.Sound.Volume";

        private const int MaxSoundSources = 16;
        private const float MutedThreshold = 0.01f;
        private const float DefaultUnmuteVolume = 0.5f;
        private const float DefaultMaxDistance = 25f;

        private readonly Dictionary<int, AudioHandle> activeHandles = new Dictionary<int, AudioHandle>();
        private readonly List<SoundVoice> soundVoices = new List<SoundVoice>();

        private AudioSource musicSource;
        private AudioHandle currentMusicHandle;
        private AudioHandle pendingMusicHandle;
        private float currentMusicBaseVolume = 1f;
        private float musicEndAtRealtime;
        private float musicVolume = 1f;
        private float soundVolume = 1f;
        private float volumeBeforeMute = 1f;
        private bool mutedFromButton;
        private int nextHandleId = 1;
        private long nextPlaySequence = 1;

        public event Action<float> MasterVolumeChanged;
        public event Action<float> MusicVolumeChanged;
        public event Action<float> SoundVolumeChanged;

        public float MasterVolume => AudioListener.volume;
        public float MusicVolume => musicVolume;
        public float SoundVolume => soundVolume;
        public bool IsMuted => MasterVolume <= MutedThreshold;

        protected override void OnSingletonAwake()
        {
            musicSource = CreateSource("Music");
            musicSource.spatialBlend = 0f;
            LoadPreferences();
        }

        private void Update()
        {
            UpdateMusic();
            UpdateSounds();
        }

        protected override void OnDestroy()
        {
            ReleaseRuntime();
            base.OnDestroy();
        }

        public AudioHandle PlayMusic(string location, AudioPlayOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            AudioHandle handle = CreateHandle();
            if (pendingMusicHandle != null)
            {
                Stop(pendingMusicHandle);
            }

            pendingMusicHandle = handle;
            LoadAndPlayMusicAsync(handle, location, AudioPlayOptions.CopyOrDefault(options)).Forget();
            return handle;
        }

        public AudioHandle PlaySound(string location, AudioPlayOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            AudioHandle handle = CreateHandle();
            LoadAndPlaySoundAsync(handle, location, AudioPlayOptions.CopyOrDefault(options)).Forget();
            return handle;
        }

        public void StopMusic()
        {
            AudioHandle pending = pendingMusicHandle;
            AudioHandle current = currentMusicHandle;

            if (pending != null)
            {
                Stop(pending);
            }

            if (current != null && !ReferenceEquals(current, pending))
            {
                Stop(current);
            }
        }

        public void Stop(AudioHandle handle)
        {
            if (!OwnsActiveHandle(handle))
            {
                return;
            }

            if (ReferenceEquals(pendingMusicHandle, handle))
            {
                pendingMusicHandle = null;
            }

            if (ReferenceEquals(currentMusicHandle, handle))
            {
                StopMusicSource();
                ReleaseHandle(handle);
                return;
            }

            for (int i = 0; i < soundVoices.Count; i++)
            {
                SoundVoice voice = soundVoices[i];
                if (ReferenceEquals(voice.Handle, handle))
                {
                    StopVoice(voice);
                    return;
                }
            }

            ReleaseHandle(handle);
        }

        public void StopAllSounds()
        {
            for (int i = soundVoices.Count - 1; i >= 0; i--)
            {
                StopVoice(soundVoices[i]);
            }
        }

        public void LoadPreferences()
        {
            SetMusicVolume(PlayerPrefs.GetFloat(MusicVolumeKey, 1f), false);
            SetSoundVolume(PlayerPrefs.GetFloat(SoundVolumeKey, 1f), false);
            SetMasterVolume(PlayerPrefs.GetFloat(MasterVolumeKey, AudioListener.volume), false);
        }

        public void SetMasterVolume(float volume, bool save = true)
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
                SaveFloat(MasterVolumeKey, clamped);
            }

            MasterVolumeChanged?.Invoke(clamped);
        }

        public void SetMusicVolume(float volume, bool save = true)
        {
            musicVolume = Mathf.Clamp01(volume);
            ApplyMusicVolume();
            if (save)
            {
                SaveFloat(MusicVolumeKey, musicVolume);
            }

            MusicVolumeChanged?.Invoke(musicVolume);
        }

        public void SetSoundVolume(float volume, bool save = true)
        {
            soundVolume = Mathf.Clamp01(volume);
            for (int i = 0; i < soundVoices.Count; i++)
            {
                SoundVoice voice = soundVoices[i];
                if (voice.Handle != null)
                {
                    voice.Source.volume = voice.BaseVolume * soundVolume;
                }
            }

            if (save)
            {
                SaveFloat(SoundVolumeKey, soundVolume);
            }

            SoundVolumeChanged?.Invoke(soundVolume);
        }

        public void Mute()
        {
            if (!IsMuted)
            {
                volumeBeforeMute = MasterVolume;
            }

            SetMasterVolume(0f);
            mutedFromButton = true;
        }

        public void Unmute()
        {
            float restoreVolume = mutedFromButton && volumeBeforeMute > MutedThreshold
                ? volumeBeforeMute
                : DefaultUnmuteVolume;
            SetMasterVolume(restoreVolume);
        }

        public void ToggleMute()
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

        internal bool IsHandleActive(AudioHandle handle)
        {
            return OwnsActiveHandle(handle);
        }

        private async Task LoadAndPlayMusicAsync(
            AudioHandle handle,
            string location,
            AudioPlayOptions options)
        {
            AudioClip clip = await ResourceManager.Instance.LoadAssetAsync<AudioClip>(location);
            if (!OwnsActiveHandle(handle) || !ReferenceEquals(pendingMusicHandle, handle))
            {
                return;
            }

            if (clip == null)
            {
                Debug.LogWarning($"Audio music load failed. location: {location}");
                pendingMusicHandle = null;
                ReleaseHandle(handle);
                return;
            }

            AudioHandle previous = currentMusicHandle;
            if (previous != null)
            {
                Stop(previous);
            }

            pendingMusicHandle = null;
            currentMusicHandle = handle;
            currentMusicBaseVolume = Mathf.Clamp01(options.Volume);
            musicEndAtRealtime = options.Duration > 0f
                ? Time.realtimeSinceStartup + options.Duration
                : 0f;

            ConfigureSource(
                musicSource,
                clip,
                options.Loop ?? true,
                currentMusicBaseVolume * musicVolume,
                options.Pitch,
                null);
            musicSource.Play();
        }

        private async Task LoadAndPlaySoundAsync(
            AudioHandle handle,
            string location,
            AudioPlayOptions options)
        {
            AudioClip clip = await ResourceManager.Instance.LoadAssetAsync<AudioClip>(location);
            if (!OwnsActiveHandle(handle))
            {
                return;
            }

            if (clip == null)
            {
                Debug.LogWarning($"Audio sound load failed. location: {location}");
                ReleaseHandle(handle);
                return;
            }

            SoundVoice voice = AcquireVoice();
            voice.Handle = handle;
            voice.BaseVolume = Mathf.Clamp01(options.Volume);
            voice.EndAtRealtime = options.Duration > 0f
                ? Time.realtimeSinceStartup + options.Duration
                : 0f;
            voice.PlaySequence = nextPlaySequence++;

            ConfigureSource(
                voice.Source,
                clip,
                options.Loop ?? false,
                voice.BaseVolume * soundVolume,
                options.Pitch,
                options.Position);
            voice.Source.Play();
        }

        private void UpdateMusic()
        {
            if (currentMusicHandle == null)
            {
                return;
            }

            bool durationElapsed = musicEndAtRealtime > 0f &&
                                   Time.realtimeSinceStartup >= musicEndAtRealtime;
            if (durationElapsed || !musicSource.isPlaying)
            {
                Stop(currentMusicHandle);
            }
        }

        private void UpdateSounds()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = soundVoices.Count - 1; i >= 0; i--)
            {
                SoundVoice voice = soundVoices[i];
                if (voice.Handle == null)
                {
                    continue;
                }

                bool durationElapsed = voice.EndAtRealtime > 0f && now >= voice.EndAtRealtime;
                if (durationElapsed || !voice.Source.isPlaying)
                {
                    StopVoice(voice);
                }
            }
        }

        private AudioHandle CreateHandle()
        {
            int id = nextHandleId++;
            if (nextHandleId <= 0)
            {
                nextHandleId = 1;
            }

            AudioHandle handle = new AudioHandle(this, id);
            activeHandles[id] = handle;
            return handle;
        }

        private bool OwnsActiveHandle(AudioHandle handle)
        {
            return handle != null &&
                   ReferenceEquals(handle.Owner, this) &&
                   activeHandles.TryGetValue(handle.Id, out AudioHandle active) &&
                   ReferenceEquals(active, handle);
        }

        private void ReleaseHandle(AudioHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            if (activeHandles.TryGetValue(handle.Id, out AudioHandle active) &&
                ReferenceEquals(active, handle))
            {
                activeHandles.Remove(handle.Id);
            }

            handle.Invalidate();
        }

        private SoundVoice AcquireVoice()
        {
            for (int i = 0; i < soundVoices.Count; i++)
            {
                if (soundVoices[i].Handle == null)
                {
                    return soundVoices[i];
                }
            }

            if (soundVoices.Count < MaxSoundSources)
            {
                SoundVoice created = new SoundVoice(CreateSource($"Sound_{soundVoices.Count + 1}"));
                soundVoices.Add(created);
                return created;
            }

            SoundVoice oldest = soundVoices[0];
            for (int i = 1; i < soundVoices.Count; i++)
            {
                if (soundVoices[i].PlaySequence < oldest.PlaySequence)
                {
                    oldest = soundVoices[i];
                }
            }

            StopVoice(oldest);
            return oldest;
        }

        private void StopVoice(SoundVoice voice)
        {
            if (voice == null)
            {
                return;
            }

            AudioHandle handle = voice.Handle;
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Handle = null;
            voice.BaseVolume = 1f;
            voice.EndAtRealtime = 0f;
            voice.PlaySequence = 0;

            if (handle != null)
            {
                ReleaseHandle(handle);
            }
        }

        private void StopMusicSource()
        {
            musicSource.Stop();
            musicSource.clip = null;
            currentMusicHandle = null;
            currentMusicBaseVolume = 1f;
            musicEndAtRealtime = 0f;
        }

        private void ApplyMusicVolume()
        {
            if (musicSource != null)
            {
                musicSource.volume = currentMusicBaseVolume * musicVolume;
            }
        }

        private AudioSource CreateSource(string sourceName)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private static void ConfigureSource(
            AudioSource source,
            AudioClip clip,
            bool loop,
            float volume,
            float pitch,
            Vector3? position)
        {
            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Clamp(pitch, 0.01f, 3f);

            if (position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1f;
                source.maxDistance = DefaultMaxDistance;
            }
            else
            {
                source.transform.localPosition = Vector3.zero;
                source.spatialBlend = 0f;
            }
        }

        private static void SaveFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        private void ReleaseRuntime()
        {
            StopMusic();
            StopAllSounds();

            if (pendingMusicHandle != null)
            {
                ReleaseHandle(pendingMusicHandle);
                pendingMusicHandle = null;
            }

            foreach (AudioHandle handle in activeHandles.Values)
            {
                handle?.Invalidate();
            }

            activeHandles.Clear();
        }

        private sealed class SoundVoice
        {
            public SoundVoice(AudioSource source)
            {
                Source = source;
            }

            public AudioSource Source { get; }
            public AudioHandle Handle;
            public float BaseVolume = 1f;
            public float EndAtRealtime;
            public long PlaySequence;
        }
    }
}
