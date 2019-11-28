using System.Windows;

namespace DragAndDrop
{
    public interface IFileDragDropTarget : IDragDropTarget
    {
        DragDropEffects GetFileDragDropEffects(string[] paths);

        void OnFileDrop(string[] paths);
    }
}
