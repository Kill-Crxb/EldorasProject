using System;
using UnityEngine;
using UnityEngine.EventSystems;

// One cell of the talent grid: left click learns, right click unlearns, hover shows the detail.
// TalentPanelView builds these and owns what they mean.
public class TalentCell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int Tier;
    public int Column;

    public event Action<TalentCell, PointerEventData.InputButton> Clicked;
    public event Action<TalentCell, bool> Hovered;

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this, eventData.button);

    public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this, true);

    public void OnPointerExit(PointerEventData eventData) => Hovered?.Invoke(this, false);
}
