using UnityEngine;

namespace Game.Framework
{
    /// <summary>
    /// Optional per-playback overrides shared by music and sound playback.
    /// Music loops by default; sounds do not. Set Loop to override that default.
    /// </summary>
    public sealed class AudioPlayOptions
    {
        public bool? Loop { get; set; }
        public float Volume { get; set; } = 1f;
        public float Pitch { get; set; } = 1f;

        /// <summary>
        /// Real-time playback limit in seconds. A value less than or equal to zero
        /// lets a non-looping clip finish naturally or a looping clip play until stopped.
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// Null plays as 2D audio. A value plays as 3D audio at the supplied world position.
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
