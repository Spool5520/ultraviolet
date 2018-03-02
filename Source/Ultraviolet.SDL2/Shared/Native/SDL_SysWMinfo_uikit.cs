﻿using System;
using System.Runtime.InteropServices;
using Ultraviolet.Core;

namespace Ultraviolet.SDL2.Native
{
#pragma warning disable 1591
    [Preserve]
    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_SysWMinfo_uikit
    {
        public IntPtr window;
        public UInt32 framebuffer;
        public UInt32 colorbuffer;
        public UInt32 resolveFramebuffer;
    }
#pragma warning restore 1591
}