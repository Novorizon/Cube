using System;

namespace UI
{
    [Flags]
    public enum UICloseTriggers
    {
        None = 0,
        CloseButton = 1 << 0,
        LeftOutside = 1 << 1,
        RightOutside = 1 << 2,
        Back = 1 << 3,
    }
}
