using UnityEngine;
using UnityEngine.UIElements;

// Shared look for the dashboard's card grids. Graduates with WizardTab.

public static class CardStyle
{
    public static void Outline(VisualElement element, Color colour, float width)
    {
        element.style.borderTopColor = colour;
        element.style.borderBottomColor = colour;
        element.style.borderLeftColor = colour;
        element.style.borderRightColor = colour;
        element.style.borderTopWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftWidth = width;
        element.style.borderRightWidth = width;
    }
}
