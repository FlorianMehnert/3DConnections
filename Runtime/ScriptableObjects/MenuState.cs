using UnityEngine;

[CreateAssetMenu(fileName = "MenuState", menuName = "3DConnections/ScriptableObjects/MenuState", order = 1)]
public class MenuState : ScriptableObject
{
    public bool menuOpen = false;
    public bool modularMenuOpen = false;
}