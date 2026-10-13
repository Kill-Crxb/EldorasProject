using UnityEngine;
using UnityEngine.EventSystems;

// On each status bar icon: tells its StatusBarView which icon the pointer is over, for the tooltip.
public class StatusIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public StatusBarView Bar;
    public int Index;

    public void OnPointerEnter(PointerEventData eventData) => Bar.Hover(Index);

    public void OnPointerExit(PointerEventData eventData) => Bar.Unhover(Index);
}
