using UnityEngine;

namespace _3DConnections.Runtime.Selection
{
    public interface ISelectionHandler
    {
        void SelectObject(GameObject obj, bool addToSelection = false);
        void DeselectObject(GameObject obj);
        void DeselectAll();
        void ToggleSelection(GameObject obj);
        bool IsSelected(GameObject obj);
        int SelectionCount { get; }
        GameObject[] GetSelectedObjects();
        Bounds GetSelectionBounds();
    }
}