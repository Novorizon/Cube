using UnityEngine;

namespace Game.Framework
{
    /// <summary>
    /// 单次播放参数。背景音乐和普通音效共用这一组参数。
    /// </summary>
    public sealed class AudioPlayOptions
    {
        /// <summary>
        /// 是否循环。null 表示使用接口默认值：背景音乐循环，普通音效不循环。
        /// </summary>
        public bool? Loop { get; set; }

        /// <summary>
        /// 本次播放的基础音量，取值会限制在 0 到 1，默认 1。
        /// 最终音量还会乘以对应的 Music 或 Sound 分类音量。
        /// </summary>
        public float Volume { get; set; } = 1f;

        /// <summary>
        /// 播放音高，取值会限制在 0.01 到 3，默认 1。
        /// </summary>
        public float Pitch { get; set; } = 1f;

        /// <summary>
        /// 最长播放时长（秒），使用真实时间计时，不受 Time.timeScale 影响。
        /// 小于等于 0 时，非循环音频自然结束，循环音频持续到主动停止。
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// null 表示 2D 音频；传入世界坐标则播放固定位置的 3D 音频。
        /// 播放后不会自动跟随 Transform。
        /// </summary>
        public Vector3? Position { get; set; }

        internal AudioPlayOptions Copy()
        {
            return new AudioPlayOptions
            {
                Loop = Loop,
                Volume = Volume,
                Pitch = Pitch,
                Duration = Duration,
                Position = Position,
            };
        }

        internal static AudioPlayOptions CopyOrDefault(AudioPlayOptions options)
        {
            return options != null ? options.Copy() : new AudioPlayOptions();
        }
    }
}
