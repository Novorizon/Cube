using System;
using UnityEngine;

namespace Game.Framework
{
    public readonly struct DeviceScreenInfo : IEquatable<DeviceScreenInfo>
    {
        public DeviceScreenInfo(
            Vector2Int pixelSize,
            Vector2Int displayResolution,
            Rect safeAreaPixels,
            ScreenOrientation orientation,
            FullScreenMode fullScreenMode,
            float dpi)
        {
            PixelSize = pixelSize;
            DisplayResolution = displayResolution;
            SafeAreaPixels = NormalizeSafeArea(pixelSize, safeAreaPixels);
            Orientation = orientation;
            FullScreenMode = fullScreenMode;
            Dpi = Mathf.Max(0f, dpi);
        }

        public Vector2Int PixelSize { get; }
        public Vector2Int DisplayResolution { get; }
        public Rect SafeAreaPixels { get; }
        public ScreenOrientation Orientation { get; }
        public FullScreenMode FullScreenMode { get; }
        public float Dpi { get; }

        public bool IsValid => PixelSize.x > 0 && PixelSize.y > 0;
        public bool IsPortrait => PixelSize.y > PixelSize.x;
        public float AspectRatio => PixelSize.y > 0 ? (float)PixelSize.x / PixelSize.y : 0f;

        public static DeviceScreenInfo Capture()
        {
            Resolution display = UnityEngine.Screen.currentResolution;
            return new DeviceScreenInfo(
                new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height),
                new Vector2Int(display.width, display.height),
                UnityEngine.Screen.safeArea,
                UnityEngine.Screen.orientation,
                UnityEngine.Screen.fullScreenMode,
                UnityEngine.Screen.dpi);
        }

        public bool Equals(DeviceScreenInfo other)
        {
            return PixelSize == other.PixelSize &&
                   DisplayResolution == other.DisplayResolution &&
                   SafeAreaPixels == other.SafeAreaPixels &&
                   Orientation == other.Orientation &&
                   FullScreenMode == other.FullScreenMode &&
                   Mathf.Approximately(Dpi, other.Dpi);
        }

        public override bool Equals(object obj)
        {
            return obj is DeviceScreenInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PixelSize.GetHashCode();
                hash = (hash * 397) ^ DisplayResolution.GetHashCode();
                hash = (hash * 397) ^ SafeAreaPixels.GetHashCode();
                hash = (hash * 397) ^ (int)Orientation;
                hash = (hash * 397) ^ (int)FullScreenMode;
                hash = (hash * 397) ^ Dpi.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(DeviceScreenInfo left, DeviceScreenInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DeviceScreenInfo left, DeviceScreenInfo right)
        {
            return !left.Equals(right);
        }

        private static Rect NormalizeSafeArea(Vector2Int pixelSize, Rect safeArea)
        {
            if (pixelSize.x <= 0 || pixelSize.y <= 0)
            {
                return Rect.zero;
            }

            Rect screen = new Rect(0f, 0f, pixelSize.x, pixelSize.y);
            if (safeArea.width <= 0f || safeArea.height <= 0f)
            {
                return screen;
            }

            float xMin = Mathf.Clamp(safeArea.xMin, screen.xMin, screen.xMax);
            float yMin = Mathf.Clamp(safeArea.yMin, screen.yMin, screen.yMax);
            float xMax = Mathf.Clamp(safeArea.xMax, xMin, screen.xMax);
            float yMax = Mathf.Clamp(safeArea.yMax, yMin, screen.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
