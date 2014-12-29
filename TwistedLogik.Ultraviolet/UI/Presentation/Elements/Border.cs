﻿using System;
using TwistedLogik.Ultraviolet.Graphics.Graphics2D;
using TwistedLogik.Ultraviolet.UI.Presentation.Styles;

namespace TwistedLogik.Ultraviolet.UI.Presentation.Elements
{
    /// <summary>
    /// Represents an element which draws a border around another element.
    /// </summary>
    [UIElement("Border")]
    public class Border : ContentControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Border"/> element.
        /// </summary>
        /// <param name="uv">The Ultraviolet context.</param>
        /// <param name="id">The element's unique identifier within its view.</param>
        public Border(UltravioletContext uv, String id)
            : base(uv, id)
        {

        }

        /// <inheritdoc/>
        protected override void OnContentDrawn(UltravioletTime time, SpriteBatch spriteBatch)
        {
            var display = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;

            var thickness = BorderThickness;
            var left      = (Int32)display.DipsToPixels(thickness.Left);
            var top       = (Int32)display.DipsToPixels(thickness.Top);
            var right     = (Int32)display.DipsToPixels(thickness.Right);
            var bottom    = (Int32)display.DipsToPixels(thickness.Bottom);
            var color     = BorderColor;

            spriteBatch.Draw(UIElementResources.BlankTexture, new RectangleF(AbsoluteScreenX, AbsoluteScreenY, left, ActualHeight), color);
            spriteBatch.Draw(UIElementResources.BlankTexture, new RectangleF(AbsoluteScreenX, AbsoluteScreenY, ActualWidth, top), color);
            spriteBatch.Draw(UIElementResources.BlankTexture, new RectangleF(AbsoluteScreenX + ActualWidth - right, AbsoluteScreenY, right, ActualHeight), color);
            spriteBatch.Draw(UIElementResources.BlankTexture, new RectangleF(AbsoluteScreenX, AbsoluteScreenY + ActualHeight - bottom, ActualWidth, bottom), color);

            base.OnContentDrawn(time, spriteBatch);
        }

        /// <summary>
        /// Gets or sets the thickness of the element's border in device independent pixels (1/96 of an inch).
        /// </summary>
        public Thickness BorderThickness
        {
            get { return GetValue<Thickness>(BorderThicknessProperty); }
            set { SetValue<Thickness>(BorderThicknessProperty, value); }
        }

        /// <summary>
        /// Gets or sets the color of the element's border.
        /// </summary>
        public Color BorderColor
        {
            get { return GetValue<Color>(BorderColorProperty); }
            set { SetValue<Color>(BorderColorProperty, value); }
        }

        /// <summary>
        /// Occurs when the value of the <see cref="BorderThickness"/> property changes.
        /// </summary>
        public event UIElementEventHandler BorderThicknessChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="BorderColor"/> property changes.
        /// </summary>
        public event UIElementEventHandler BorderColorChanged;

        /// <summary>
        /// Raises the <see cref="BorderThicknessChanged"/> event.
        /// </summary>
        protected virtual void OnBorderThicknessChanged()
        {
            var temp = BorderThicknessChanged;
            if (temp != null)
            {
                temp(this);
            }
        }

        /// <summary>
        /// Raises the <see cref="BorderColorChanged"/> event.
        /// </summary>
        protected virtual void OnBorderColorChanged()
        {
            var temp = BorderColorChanged;
            if (temp != null)
            {
                temp(this);
            }
        }

        /// <summary>
        /// Occurs when the value of the <see cref="BorderThickness"/> dependency property changes.
        /// </summary>
        /// <param name="dobj">The object that raised the event.</param>
        private static void HandleBorderThicknessChanged(DependencyObject dobj)
        {
            var element = (Border)dobj;
            element.OnBorderThicknessChanged();
        }

        /// <summary>
        /// Occurs when the value of the <see cref="BorderColor"/> dependency property changes.
        /// </summary>
        /// <param name="dobj">The object that raised the event.</param>
        private static void HandleBorderColorChanged(DependencyObject dobj)
        {
            var element = (Border)dobj;
            element.OnBorderColorChanged();
        }

        /// <summary>
        /// Identifies the BorderThickness dependency property.
        /// </summary>
        [Styled("border-thickness")]
        public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register("BorderThickness", typeof(Thickness), typeof(Border),
            new DependencyPropertyMetadata(HandleBorderThicknessChanged, () => Thickness.One, DependencyPropertyOptions.None));

        /// <summary>
        /// Identifies the BorderColor dependency property.
        /// </summary>
        [Styled("border-color")]
        public static readonly DependencyProperty BorderColorProperty = DependencyProperty.Register("BorderColor", typeof(Color), typeof(Border),
            new DependencyPropertyMetadata(HandleBorderColorChanged, () => Color.Black, DependencyPropertyOptions.None));
    }
}
