using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Framework
{
    /// <summary>
    /// 项目统一音频入口。内部使用一个背景音乐声源和可复用的普通音效池，
    /// 业务层只需要区分 <see cref="PlayMusic"/> 与 <see cref="PlaySound"/>。
    /// </summary>
    public sealed class AudioManager : MonoSingleton<AudioManager>
    {
        /// <summary>主音量的 PlayerPrefs 键。保留旧设置页使用的键以兼容已有存档。</summary>
        public const string MasterVolumeKey = "World.Sound.Volume";

        /// <summary>背景音乐分类音量的 PlayerPrefs 键。</summary>
        public const string MusicVolumeKey = "Audio.Music.Volume";

        /// <summary>普通音效分类音量的 PlayerPrefs 键。</summary>
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

        /// <summary>主音量发生变化时触发，参数为 0 到 1 的新值。</summary>
        public event Action<float> MasterVolumeChanged;

        /// <summary>背景音乐分类音量发生变化时触发，参数为 0 到 1 的新值。</summary>
        public event Action<float> MusicVolumeChanged;

        /// <summary>普通音效分类音量发生变化时触发，参数为 0 到 1 的新值。</summary>
        public event Action<float> SoundVolumeChanged;

        /// <summary>当前主音量，直接对应 <see cref="AudioListener.volume"/>。</summary>
        public float MasterVolume => AudioListener.volume;

        /// <summary>当前背景音乐分类音量。</summary>
        public float MusicVolume => musicVolume;

        /// <summary>当前普通音效分类音量。</summary>
        public float SoundVolume => soundVolume;

        /// <summary>主音量是否处于静音阈值内。</summary>
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

        /// <summary>
        /// 异步加载并播放背景音乐。默认循环；新资源加载成功前，当前音乐继续播放。
        /// 新音乐成功开始后会替换当前音乐。
        /// </summary>
        /// <param name="location">YooAsset 可加载的 AudioClip 资源完整路径。</param>
        /// <param name="options">单次播放参数；传 null 使用默认值。</param>
        /// <returns>可用于取消加载或停止播放的句柄；路径为空时返回 null。</returns>
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

        /// <summary>
        /// 异步加载并播放普通音效。默认不循环，未指定位置时作为 2D 音频播放。
        /// </summary>
        /// <param name="location">YooAsset 可加载的 AudioClip 资源完整路径。</param>
        /// <param name="options">单次播放参数；传 null 使用默认值。</param>
        /// <returns>可用于取消加载或停止播放的句柄；路径为空时返回 null。</returns>
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

        /// <summary>
        /// 停止当前背景音乐，并取消尚未完成的背景音乐加载请求。
        /// </summary>
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

        /// <summary>
        /// 停止指定句柄对应的播放；句柄已结束或不属于本管理器时不执行操作。
        /// </summary>
        /// <param name="handle">由 PlayMusic 或 PlaySound 返回的句柄。</param>
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

        /// <summary>
        /// 停止所有已经开始播放的普通音效。
        /// 已取消但仍在返回途中的异步加载会通过句柄状态阻止迟到播放。
        /// </summary>
        public void StopAllSounds()
        {
            for (int i = soundVoices.Count - 1; i >= 0; i--)
            {
                StopVoice(soundVoices[i]);
            }
        }

        /// <summary>
        /// 从 PlayerPrefs 读取主音量、背景音乐音量和普通音效音量。
        /// </summary>
        public void LoadPreferences()
        {
            SetMusicVolume(PlayerPrefs.GetFloat(MusicVolumeKey, 1f), false);
            SetSoundVolume(PlayerPrefs.GetFloat(SoundVolumeKey, 1f), false);
            SetMasterVolume(PlayerPrefs.GetFloat(MasterVolumeKey, AudioListener.volume), false);
        }

        /// <summary>
        /// 设置主音量，影响所有 Unity 音频。
        /// </summary>
        /// <param name="volume">目标音量，自动限制在 0 到 1。</param>
        /// <param name="save">是否立即保存到 PlayerPrefs。</param>
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

        /// <summary>
        /// 设置背景音乐分类音量，不改变每次播放设置的基础音量。
        /// </summary>
        /// <param name="volume">目标音量，自动限制在 0 到 1。</param>
        /// <param name="save">是否立即保存到 PlayerPrefs。</param>
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

        /// <summary>
        /// 设置普通音效分类音量，并立即更新当前正在播放的音效。
        /// </summary>
        /// <param name="volume">目标音量，自动限制在 0 到 1。</param>
        /// <param name="save">是否立即保存到 PlayerPrefs。</param>
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

        /// <summary>将主音量设为 0，并记住恢复时使用的音量。</summary>
        public void Mute()
        {
            if (!IsMuted)
            {
                volumeBeforeMute = MasterVolume;
            }

            SetMasterVolume(0f);
            mutedFromButton = true;
        }

        /// <summary>恢复静音前的主音量；没有可恢复值时使用默认音量。</summary>
        public void Unmute()
        {
            float restoreVolume = mutedFromButton && volumeBeforeMute > MutedThreshold
                ? volumeBeforeMute
                : DefaultUnmuteVolume;
            SetMasterVolume(restoreVolume);
        }

        /// <summary>在静音和非静音状态之间切换。</summary>
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

        private async Task LoadAndPlayMusicAsync(AudioHandle handle, string location, AudioPlayOptions options)
        {
            AudioClip clip = await ResourceManager.Instance.LoadAssetAsync<AudioClip>(location);
            // 请求可能在加载期间被 Stop，或被更新的 PlayMusic 请求替代。
            // 先检查句柄可避免异步结果返回后发生“迟到播放”。
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
                // 保留旧音乐直到新资源确认加载成功，避免加载期间出现静音空档。
                Stop(previous);
            }

            pendingMusicHandle = null;
            currentMusicHandle = handle;
            currentMusicBaseVolume = Mathf.Clamp01(options.Volume);
            musicEndAtRealtime = options.Duration > 0f
                ? Time.realtimeSinceStartup + options.Duration
                : 0f;

            ConfigureSource(musicSource, clip, options.Loop ?? true, currentMusicBaseVolume * musicVolume, options.Pitch, null);
            musicSource.Play();
        }

        private async Task LoadAndPlaySoundAsync(AudioHandle handle, string location, AudioPlayOptions options)
        {
            AudioClip clip = await ResourceManager.Instance.LoadAssetAsync<AudioClip>(location);
            // 音效在异步加载完成前也可以通过句柄取消。
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

            ConfigureSource(voice.Source, clip, options.Loop ?? false, voice.BaseVolume * soundVolume, options.Pitch, options.Position);
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

            // 达到并发上限时抢占最早开始播放的音效，保证声源数量有明确上限。
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

        private static void ConfigureSource(AudioSource source, AudioClip clip, bool loop, float volume, float pitch, Vector3? position)
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
