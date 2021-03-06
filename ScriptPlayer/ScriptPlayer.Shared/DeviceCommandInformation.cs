﻿using System;

namespace ScriptPlayer.Shared
{
    public struct DeviceCommandInformation
    {
        public byte PositionFromTransformed;
        public byte PositionToTransformed;
        public byte SpeedTransformed;

        public byte PositionFromOriginal;
        public byte PositionToOriginal;
        public byte SpeedOriginal;

        public TimeSpan Duration;
    }
}
