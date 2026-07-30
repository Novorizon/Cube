using System;
using UnityEngine;

namespace Game.Framework
{
    public sealed class DeviceManager : MonoSingleton<DeviceManager>
    {
        public RuntimePlatform Platform { get; private set; }
        public DeviceType DeviceType { get; private set; }
        public string DeviceModel { get; private set; }
        public string OperatingSystem { get; private set; }
        public string ProcessorType { get; private set; }
        public int ProcessorCount { get; private set; }
        public int SystemMemoryMb { get; private set; }
        public string GraphicsDeviceName { get; private set; }
        public int GraphicsMemoryMb { get; private set; }
        public DeviceScreenInfo Screen { get; private set; }

        public bool IsMobile => Application.isMobilePlatform || DeviceType == UnityEngine.DeviceType.Handheld;

        public event Action<DeviceScreenInfo> ScreenChanged;

        protected override void OnSingletonAwake()
        {
            Platform = Application.platform;
            DeviceType = SystemInfo.deviceType;
            DeviceModel = SystemInfo.deviceModel ?? string.Empty;
            OperatingSystem = SystemInfo.operatingSystem ?? string.Empty;
            ProcessorType = SystemInfo.processorType ?? string.Empty;
            ProcessorCount = SystemInfo.processorCount;
            SystemMemoryMb = SystemInfo.systemMemorySize;
            GraphicsDeviceName = SystemInfo.graphicsDeviceName ?? string.Empty;
            GraphicsMemoryMb = SystemInfo.graphicsMemorySize;
            Screen = DeviceScreenInfo.Capture();
        }

        private void Update()
        {
            DeviceScreenInfo next = DeviceScreenInfo.Capture();
            if (next == Screen)
            {
                return;
            }

            Screen = next;
            ScreenChanged?.Invoke(Screen);
        }

        protected override void OnDestroy()
        {
            ScreenChanged = null;
            base.OnDestroy();
        }
    }
}
