using UnityEngine;
using UnityEngine.UI;

// GI-19: a pale chunk behind a filled bar that marks what was just lost, holds for a beat, then drains down to the
// real fill. Gains show at once. Built at runtime as a sibling drawn under the fill, copying its shape, so no prefab
// needs a new child. Used by BarView (player HUD) and NPCNameplate (the target's bar).
public class GhostBar
{
    private readonly Image ghost;
    private readonly float hold;
    private readonly float drainPerSecond;

    private float shown = 1f;
    private float last = 1f;
    private float holdUntil;

    public GhostBar(Image fill, Color color, float holdSeconds, float drainPerSecond)
    {
        hold = holdSeconds;
        this.drainPerSecond = drainPerSecond;
        if (fill == null) return;

        var go = new GameObject($"{fill.name}_Ghost", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        var source = (RectTransform)fill.transform;
        rect.SetParent(source.parent, false);
        rect.SetSiblingIndex(source.GetSiblingIndex());
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta;
        rect.localScale = source.localScale;

        ghost = go.GetComponent<Image>();
        ghost.sprite = fill.sprite;
        ghost.type = Image.Type.Filled;
        ghost.fillMethod = fill.fillMethod;
        ghost.fillOrigin = fill.fillOrigin;
        ghost.color = color;
        ghost.raycastTarget = false;
        shown = last = fill.fillAmount;
        ghost.fillAmount = shown;
    }

    // True on the frame the fill dropped — a hit, for whoever wants to shake.
    public bool Tick(float fill)
    {
        bool dropped = fill < last - 0.0001f;
        if (dropped) holdUntil = Time.unscaledTime + hold;
        last = fill;

        if (fill >= shown) shown = fill;
        else if (Time.unscaledTime >= holdUntil) shown = Mathf.MoveTowards(shown, fill, drainPerSecond * Time.unscaledDeltaTime);

        if (ghost != null) ghost.fillAmount = shown;
        return dropped;
    }
}
