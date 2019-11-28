using System;
using System.Windows;
using System.Windows.Controls;

namespace DragAndDrop
{
    public class DragDropHelper
    {
        public static bool GetIsDragDropEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDragDropEnabledProperty);
        }

        public static void SetIsDragDropEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragDropEnabledProperty, value);
        }

        public static bool GetDragDropTarget(DependencyObject obj)
        {
            return (bool)obj.GetValue(DragDropTargetProperty);
        }

        public static void SetDragDropTarget(DependencyObject obj, bool value)
        {
            obj.SetValue(DragDropTargetProperty, value);
        }

        public static readonly DependencyProperty IsDragDropEnabledProperty = DependencyProperty.RegisterAttached("IsDragDropEnabled", typeof(bool), typeof(DragDropHelper), new PropertyMetadata(OnDragDropEnabled));

        public static readonly DependencyProperty DragDropTargetProperty = DependencyProperty.RegisterAttached("DragDropTarget", typeof(object), typeof(DragDropHelper), null);

        private static void OnDragDropEnabled(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == e.OldValue)
            {
                return;
            }

            if (d is Control control)
            {
                control.DragOver += OnDragOver;
                control.Drop += OnDrop;
            }
        }

        private static void OnDragOver(object sender, DragEventArgs e)
        {
            var effects = DragDropEffects.None;

            if (sender is DependencyObject d)
            {
                if (d.GetValue(DragDropTargetProperty) is IFileDragDropTarget target)
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        effects = target.GetFileDragDropEffects((string[])e.Data.GetData(DataFormats.FileDrop));
                    }
                }
                else
                {
                    throw new Exception("DragDropTarget object must implement an IDragDropTarget interface");
                }
            }

            e.Effects = effects;
            e.Handled = true;
        }

        private static void OnDrop(object sender, DragEventArgs e)
        {
            if (sender is DependencyObject d)
            {
                if (d.GetValue(DragDropTargetProperty) is IFileDragDropTarget target)
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        target.OnFileDrop((string[])e.Data.GetData(DataFormats.FileDrop));
                    }
                }
                else
                {
                    throw new Exception("DragDropTarget object must implement an IDragDropTarget interface");
                }
            }
        }
    }
}
