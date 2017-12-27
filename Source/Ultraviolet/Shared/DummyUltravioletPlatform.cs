﻿using System;
using Ultraviolet.Core;
using Ultraviolet.Platform;

namespace Ultraviolet
{
    /// <summary>
    /// Represents a dummy implementation of <see cref="IUltravioletPlatform"/>.
    /// </summary>
    public class DummyUltravioletPlatform : UltravioletResource, IUltravioletPlatform
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DummyUltravioletPlatform"/> class.
        /// </summary>
        /// <param name="uv">The Ultraviolet context.</param>
        public DummyUltravioletPlatform(UltravioletContext uv)
            : base(uv)
        {
            this.clipboard = new DummyClipboardService();
            this.windows = new DummyUltravioletWindowInfo();
            this.displays = new DummyUltravioletDisplayInfo();
        }

        /// <inheritdoc/>
        public void Update(UltravioletTime time)
        {
            Contract.EnsureNotDisposed(this, Disposed);

            Updating?.Invoke(this, time);
        }

        /// <inheritdoc/>
        public void ShowMessageBox(MessageBoxType type, String title, String message, IUltravioletWindow parent = null)
        {
            Contract.EnsureNotDisposed(this, Disposed);
        }

        /// <inheritdoc/>
        public Cursor Cursor
        {
            get
            {
                Contract.EnsureNotDisposed(this, Disposed);

                return default(Cursor);
            }
            set
            {
                Contract.EnsureNotDisposed(this, Disposed);
            }
        }

        /// <inheritdoc/>
        public ClipboardService Clipboard
        {
            get
            {
                Contract.EnsureNotDisposed(this, Disposed);

                return clipboard;
            }
        }

        /// <inheritdoc/>
        public IUltravioletWindowInfo Windows
        {
            get
            {
                Contract.EnsureNotDisposed(this, Disposed);

                return windows;
            }
        }

        /// <inheritdoc/>
        public IUltravioletDisplayInfo Displays
        {
            get
            {
                Contract.EnsureNotDisposed(this, Disposed);

                return displays;
            }
        }

        /// <inheritdoc/>
        public event UltravioletSubsystemUpdateEventHandler Updating;

        // Property values.
        private ClipboardService clipboard;
        private IUltravioletWindowInfo windows;
        private IUltravioletDisplayInfo displays;
    }
}