﻿
namespace TwistedLogik.Ultraviolet.UI.Presentation
{
    /// <summary>
    /// Initializes factory methods for the Ultraviolet Presentation Foundation.
    /// </summary>
    public sealed class PresentationFoundationFactoryInitializer : IUltravioletFactoryInitializer
    {
        /// <inheritdoc/>
        public void Initialize(UltravioletContext owner, UltravioletFactory factory)
        {
            factory.SetFactoryMethod<UIViewProviderInitializerFactory>(() => new PresentationFoundationInitializer());
            factory.SetFactoryMethod<UIViewFactory>((uv, uiPanel, uiPanelDefinition) => PresentationFoundationView.Load(uv, uiPanel, uiPanelDefinition));
            factory.SetFactoryMethod<MessageBoxScreenFactory>((mb, mbowner) => new MessageBoxScreen(mb, mbowner.GlobalContent));
        }
    }
}