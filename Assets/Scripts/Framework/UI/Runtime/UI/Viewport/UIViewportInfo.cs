using System;
using Game.Framework;
using UnityEngine;

namespace UI
{
    public readonly struct UIViewportInfo : IEquatable<UIViewportInfo>
    {
        public UIViewportInfo(Vector2Int pixelSize, Rect safeAreaPixels, ScreenOrientation orientation)
        {
            PixelSize = pixelSize;
            SafeAreaPixels = NormalizeSafeArea(pixelSize, safeAreaPixels);
            Orientation = orientation;
        }

        public UIViewportInfo(DeviceScreenInfo screen)
            : this(screen.PixelSize, screen.SafeAreaPixels, screen.Orientation)
        {
        }

        public Vector2Int PixelSize { get; }
        public Rect SafeAreaPixels { get; }
        public ScreenOrientation Orientation { get; }

        public bool IsValid => PixelSize.x > 0 && PixelSize.y > 0;
        public bool IsPortrait => PixelSize.y > PixelSize.x;
        public float AspectRatio => PixelSize.y > 0 ? (float)PixelSize.x / PixelSize.y : 0f;

        public Rect SafeAreaNormalized
        {
            get
            {
                if (!IsValid)
                {
                    return new Rect(0f, 0f, 1f, 1f);
                }

                return new Rect(
                    SafeAreaPixels.xMin / PixelSize.x,
                    SafeAreaPixels.yMin / PixelSize.y,
                    SafeAreaPixels.width / PixelSize.x,
                    SafeAreaPixels.height / PixelSize.y);
            }
        }

        public Vector4 SafeInsetsPixels
        {
            get
            {
                if (!IsValid)
                {
                    return Vector4.zero;
                }

                return new Vector4(
                    SafeAreaPixels.xMin,
                    SafeAreaPixels.yMin,
                    PixelSize.x - SafeAreaPixels.xMax,
                    PixelSize.y - SafeAreaPixels.yMax);
            }
        }

        public static UIViewportInfo Capture()
        {
            return new UIViewportInfo(DeviceScreenInfo.Capture());
        }

        public bool Equals(UIViewportInfo other)
        {
            return PixelSize == other.PixelSize &&
                   SafeAreaPixels == other.SafeAreaPixels &&
                   Orientation == other.Orientation;
        }

        public override bool Equals(object obj)
        {
            return obj is UIViewportInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PixelSize.GetHashCode();
                hash = (hash * 397) ^ SafeAreaPixels.GetHashCode();
                hash = (hash * 397) ^ (int)Orientation;
                return hash;
            }
        }

        public static bool operator ==(UIViewportInfo left, UIViewportInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UIViewportInfo left, UIViewportInfo right)
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
