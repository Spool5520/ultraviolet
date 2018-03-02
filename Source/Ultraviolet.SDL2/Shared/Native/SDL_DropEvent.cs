﻿using System;
using System.Runtime.InteropServices;
using Ultraviolet.Core;

namespace Ultraviolet.SDL2.Native
{
#pragma warning disable 1591
    [Preserve]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SDL_DropEvent
    {
        public UInt32 type;
        public UInt32 timestamp;
        public char* file;
    }
#pragma warning restore 1591
}
