﻿using System;
using System.Reflection;
using Ultraviolet.Graphics;

namespace Ultraviolet
{
    /// <summary>
    /// Represents the Ultraviolet Framework's configuration settings.
    /// </summary>
    public abstract class UltravioletConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UltravioletConfiguration"/> class.
        /// </summary>
        public UltravioletConfiguration()
        {
            SelectAudioImplementation(AudioImplementation.BASS);

            WindowIsVisible = true;
            WindowIsResizable = true;
            InitialWindowPosition = new Rectangle(DefaultWindowPositionX, DefaultWindowPositionY, 
                DefaultWindowClientWidth, DefaultWindowClientHeight);
        }

        /// <summary>
        /// Selects which implementation of the Audio subsystem to use.
        /// </summary>
        /// <param name="impl">A <see cref="AudioImplementation"/> value corresponding to one of 
        /// the Ultraviolet Framework's Audio subsystem implementations.</param>
        public void SelectAudioImplementation(AudioImplementation impl)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var asmname = String.Empty;
            switch (impl)
            {
                case AudioImplementation.BASS:
                    asmname = "Ultraviolet.BASS";
                    break;

                case AudioImplementation.FMOD:
                    asmname = "Ultraviolet.FMOD";
                    break;

                default:
                    throw new ArgumentException(nameof(impl));
            }

#if SIGNED
            AudioSubsystemAssembly = $"{asmname}, Version={version}, Culture=neutral, PublicKeyToken=78da2f4877323311, processorArchitecture=MSIL";
#else
            AudioSubsystemAssembly = $"{asmname}, Version={version}, Culture=neutral, processorArchitecture=MSIL";
#endif
        }

        /// <summary>
        /// Gets the list of plugins which will be loaded by the Ultraviolet context.
        /// </summary>
        public UltravioletPluginCollection Plugins { get; } = new UltravioletPluginCollection();

        /// <summary>
        /// Gets or sets the <see cref="RenderTargetUsage"/> value which is used by the back buffer.
        /// </summary>
        public RenderTargetUsage BackBufferRenderTargetUsage { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the assembly that implements the audio subsystem.
        /// </summary>
        public String AudioSubsystemAssembly { get; set; }

        /// <summary>
        /// Gets or sets the full name of the assembly which is responsible for
        /// creating and managing instances of the <see cref="Ultraviolet.UI.UIView"/> class.
        /// </summary>
        public String ViewProviderAssembly { get; set; }

        /// <summary>
        /// Gets or sets an object which provides configuration values for the view provider.
        /// </summary>
        public Object ViewProviderConfiguration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to create a debug context.
        /// </summary>
        public Boolean Debug { get; set; }

        /// <summary>
        /// Gets or sets the levels of debug output which are enabled.
        /// </summary>
        public DebugLevels DebugLevels { get; set; }

        /// <summary>
        /// Gets or sets the delegate that is invoked when a debug message is logged.
        /// </summary>
        public DebugCallback DebugCallback { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether SRGB encoding should be enabled for
        /// textures and render buffers if it is supported. After device initialization, check the value
        /// of the <see cref="GraphicsCapabilities.SrgbEncodingEnabled"/> property to determine whether SRGB 
        /// encoding was actually enabled -- not all devices support it.
        /// </summary>
        public Boolean SrgbBuffersEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value which determines whether instances of the <see cref="Surface2D"/>
        /// class are treated as SRGB encoded by default.
        /// </summary>
        public Boolean SrgbDefaultForSurface2D { get; set; }

        /// <summary>
        /// Gets or sets a value which determines whether instances of the <see cref="Surface3D"/>
        /// class are treated as SRGB encoded by default.
        /// </summary>
        public Boolean SrgbDefaultForSurface3D { get; set; }

        /// <summary>
        /// Gets or sets a value which determines whether instances of the <see cref="Texture2D"/>
        /// class are treated as SRGB encoded by default.
        /// </summary>
        public Boolean SrgbDefaultForTexture2D { get; set; }

        /// <summary>
        /// Gets or sets a value which determines whether instances of the <see cref="Texture3D"/>
        /// class are treated as SRGB encoded by default.
        /// </summary>
        public Boolean SrgbDefaultForTexture3D { get; set; }

        /// <summary>
        /// Gets or sets a value which determines whether instances of the <see cref="RenderBuffer2D"/>
        /// class are treated as SRGB encoded by default.
        /// </summary>
        public Boolean SrgbDefaultForRenderBuffer2D { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Ultraviolet should watch view definition files
        /// and reload them dynamically if changes are detected.
        /// </summary>
        public Boolean WatchViewFilesForChanges { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the application supports high-density display modes
        /// such as Retina and Retina HD. This allows the application to make use of every physical pixel 
        /// on the screen, rather than being scaled to use logical pixels.
        /// </summary>
        public Boolean SupportsHighDensityDisplayModes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Ultraviolet context is headless.
        /// A headless context will not create a default window upon initialization.
        /// </summary>
        public Boolean Headless { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether service mode is enabled. 
        /// </summary>
        /// <remarks>
        /// <para>In service mode, the graphics subsystem is never intialized and no windows are ever created. 
        /// This is different from headless mode, where the context's primary window is invisible but
        /// still exists.</para>
        /// <para>This mode is primarily useful in circumstances where an Ultraviolet-based application has to run in the context of a
        /// Windows service, for example on a build server. The Windows security model prevents services from accessing the graphics device,
        /// so we need to avoid doing so or else we'll run into errors.</para>
        /// </remarks>
        public Boolean EnableServiceMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Ultraviolet context's default window is visible at startup.
        /// If the context is headless, this setting has no effect.
        /// </summary>
        public Boolean WindowIsVisible { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Ultraviolet context's default window can be resized.
        /// If the context is headless, this setting has no effect.
        /// </summary>
        public Boolean WindowIsResizable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Ultraviolet context's default window is borderless.
        /// If the context is headless, this setting has no effect.
        /// </summary>
        public Boolean WindowIsBorderless { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Ultraviolet context's default window is shown immediately.
        /// The default behavior is to hide the window until immediately before it is about to be rendered for the first time.
        /// If the context is headless, this setting has no effect.
        /// </summary>
        public Boolean WindowIsShownImmediately { get; set; }

        /// <summary>
        /// Gets or sets the intial size and position of the context's primary window.
        /// </summary>
        public Rectangle InitialWindowPosition { get; set; }

        /// <summary>
        /// The default x-position of a new window.
        /// </summary>
        public const Int32 DefaultWindowPositionX = -1;

        /// <summary>
        /// The default y-position of a new window.
        /// </summary>
        public const Int32 DefaultWindowPositionY = -1;

        /// <summary>
        /// The default client width of a new window.
        /// </summary>
        public const Int32 DefaultWindowClientWidth = 640;

        /// <summary>
        /// The default client height of a new window.
        /// </summary>
        public const Int32 DefaultWindowClientHeight = 480;
    }
}
