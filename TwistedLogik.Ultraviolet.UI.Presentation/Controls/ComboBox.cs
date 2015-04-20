﻿using System;
using TwistedLogik.Nucleus;
using TwistedLogik.Ultraviolet.Input;
using TwistedLogik.Ultraviolet.UI.Presentation.Controls.Primitives;
using TwistedLogik.Ultraviolet.UI.Presentation.Input;

namespace TwistedLogik.Ultraviolet.UI.Presentation.Controls
{
    /// <summary>
    /// Represents a combo box with a drop down list of selectable items.
    /// </summary>
    [UvmlKnownType(null, "TwistedLogik.Ultraviolet.UI.Presentation.Controls.Templates.ComboBox.xml")]
    public class ComboBox : Selector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ComboBox"/> class.
        /// </summary>
        /// <param name="uv">The Ultraviolet context.</param>
        /// <param name="name">The element's identifying name within its namescope.</param>
        public ComboBox(UltravioletContext uv, String name)
            : base(uv, name)
        {

        }

        /// <summary>
        /// Gets or sets a value indicating whether the combo box's drop-down list is currently open.
        /// </summary>
        public Boolean IsDropDownOpen
        {
            get { return GetValue<Boolean>(IsDropDownOpenProperty); }
            set { SetValue<Boolean>(IsDropDownOpenProperty, value); }
        }

        /// <summary>
        /// Gets the item that is displayed in the combo box's selection box.
        /// </summary>
        public Object SelectionBoxItem
        {
            get { return GetValue<Object>(SelectionBoxItemProperty); }
        }

        /// <summary>
        /// Occurs when the combo box's drop-down list is opened.
        /// </summary>
        public event UpfEventHandler DropDownOpened;

        /// <summary>
        /// Occurs when the combo box's drop-down list is closed.
        /// </summary>
        public event UpfEventHandler DropDownClosed;

        /// <summary>
        /// Identifies the <see cref="IsDropDownOpen"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register("IsDropDownOpen", typeof(Boolean), typeof(ComboBox),
            new PropertyMetadata<Boolean>(CommonBoxedValues.Boolean.False, HandleIsDropDownOpenChanged));

        /// <summary>
        /// The private access key for the <see cref="SelectionBoxItem"/> read-only dependency property.
        /// </summary>
        private static readonly DependencyPropertyKey SelectionBoxItemPropertyKey = DependencyProperty.RegisterReadOnly("SelectionBoxItem", typeof(Object), typeof(ComboBox),
            new PropertyMetadata<Boolean>(null));

        /// <summary>
        /// Identifies the <see cref="SelectionBoxItem"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectionBoxItemProperty = SelectionBoxItemPropertyKey.DependencyProperty;

        /// <summary>
        /// Called to inform the combo box that one of its items was clicked.
        /// </summary>
        /// <param name="container">The item container that was clicked.</param>
        internal void HandleItemClicked(ComboBoxItem container)
        {
            var item = ItemContainerGenerator.ItemFromContainer(container);
            if (item == null)
                return;

            var dobj = item as DependencyObject;
            if (dobj == null || !GetIsSelected(dobj))
            {
                BeginChangeSelection();

                UnselectAllItems();
                SelectItem(item);

                EndChangeSelection();
            }

            IsDropDownOpen = false;
        }

        /// <summary>
        /// Called to inform the combo box that one of its items was changed.
        /// </summary>
        /// <param name="container">The item container that was changed.</param>
        internal void HandleItemChanged(ComboBoxItem container)
        {
            UpdateSelectionBox();
        }

        /// <inheritdoc/>
        protected internal override Panel CreateItemsPanel()
        {
            return new StackPanel(Ultraviolet, null);
        }

        /// <inheritdoc/>
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ComboBoxItem(Ultraviolet, null);
        }

        /// <inheritdoc/>
        protected override Boolean IsItemContainer(DependencyObject element)
        {
            return element is ComboBoxItem;
        }

        /// <inheritdoc/>
        protected override Boolean IsItemContainerForItem(DependencyObject container, Object item)
        {
            var cbi = container as ComboBoxItem;
            if (cbi == null)
                return false;

            return cbi.Content == item;
        }

        /// <inheritdoc/>
        protected override void OnSelectionChanged()
        {
            UpdateSelectionBox();
            base.OnSelectionChanged();
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseDevice device, MouseButton button, ref RoutedEventData data)
        {
            if (button == TwistedLogik.Ultraviolet.Input.MouseButton.Left)
            {
                IsDropDownOpen = false;
            }
            base.OnMouseUp(device, button, ref data);
        }

        /// <summary>
        /// Raises the <see cref="DropDownOpened"/> event.
        /// </summary>
        protected virtual void OnDropDownOpened()
        {
            var temp = DropDownOpened;
            if (temp != null)
            {
                temp(this);
            }
        }

        /// <summary>
        /// Raises the <see cref="DropDownClosed"/> event.
        /// </summary>
        protected virtual void OnDropDownClosed()
        {
            var temp = DropDownClosed;
            if (temp != null)
            {
                temp(this);
            }
        }

        /// <summary>
        /// Occurs when the value of the <see cref="IsDropDownOpen"/> dependency property changes.
        /// </summary>
        private static void HandleIsDropDownOpenChanged(DependencyObject dobj, Boolean oldValue, Boolean newValue)
        {
            var comboBox = (ComboBox)dobj;

            if (newValue)
            {
                Mouse.Capture(comboBox.View, comboBox, CaptureMode.SubTree);

                if (comboBox.PART_Arrow != null)
                {
                    comboBox.PART_Arrow.Classes.Add("combobox-arrow-open");
                    comboBox.PART_Arrow.Classes.Remove("combobox-arrow-closed");
                }

                comboBox.OnDropDownOpened();
            }
            else
            {
                if (comboBox.IsMouseCaptured)
                    Mouse.Capture(comboBox.View, null, CaptureMode.None);

                if (comboBox.PART_Arrow != null)
                {
                    comboBox.PART_Arrow.Classes.Remove("combobox-arrow-open");
                    comboBox.PART_Arrow.Classes.Add("combobox-arrow-closed");
                }

                comboBox.OnDropDownClosed();
            }
        }

        /// <summary>
        /// Updates the selection box.
        /// </summary>
        private void UpdateSelectionBox()
        {
            var selectionBoxItem = (Object)null;

            var contentControl = SelectedItem as ContentControl;
            if (contentControl != null)
            {
                selectionBoxItem = contentControl.Content;
            }

            SetValue<Object>(SelectionBoxItemPropertyKey, selectionBoxItem ?? String.Empty);
        }

        // Component references.
        private readonly UIElement PART_Arrow = null;
    }
}
