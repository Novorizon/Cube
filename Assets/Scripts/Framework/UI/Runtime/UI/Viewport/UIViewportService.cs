using System;
using Game.Framework;

namespace UI
{
    public sealed class UIViewportService
    {
        private readonly DeviceManager devices;

        public UIViewportInfo Current { get; private set; }

        public event Action<UIViewportInfo> Changed;

        internal UIViewportService(DeviceManager deviceManager)
        {
            devices = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            Current = new UIViewportInfo(devices.Screen);
            devices.ScreenChanged += OnDeviceScreenChanged;
        }

        private void OnDeviceScreenChanged(DeviceScreenInfo screen)
        {
            UIViewportInfo next = new UIViewportInfo(screen);
            if (next == Current)
            {
                return;
            }

            Current = next;
            Changed?.Invoke(Current);
        }

        internal void Shutdown()
        {
            devices.ScreenChanged -= OnDeviceScreenChanged;
            Changed = null;
        }
    }
}
