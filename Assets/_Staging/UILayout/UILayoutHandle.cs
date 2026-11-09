using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One movable element while the layout is being edited. Two parts:
// - this object, on the overlay above the HUD: invisible, it only catches the click and drag
//   (and carries the name label, just above the box);
// - a tinted backdrop slotted in as the element's sibling just before it, so it draws behind
//   the element and the element stays readable while it's moved.
// Both follow the element's full on-screen bounds (children included), so a hotbar anchor's
// box covers the bar spawned inside it.

namespace CrabSystem
{
    public class UILayoutHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color idleColor = new Color(0.3f, 0.7f, 1f, 0.35f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.8f, 0.2f, 0.5f);

        [Tooltip("Smallest box drawn, so empty anchors can still be grabbed.")]
        [SerializeField] private Vector2 minSize = new Vector2(48f, 32f);

        private UILayoutMode mode;
        private Image backdrop;
        private Vector2 dragStartPosition;
        private Vector2 dragStartPointer;

        public UIFrame Frame { get; private set; }

        public void Bind(UILayoutMode owner, UIFrame frame)
        {
            mode = owner;
            Frame = frame;
            label.text = frame.DisplayName;
            backdrop = CreateBackdrop(frame);
            SetSelected(false);
            Refresh();
        }

        private void OnDestroy()
        {
            if (backdrop != null) Destroy(backdrop.gameObject);
        }

        public void Refresh()
        {
            Fit((RectTransform)transform);
            Fit(backdrop.rectTransform);
        }

        public void SetSelected(bool selected) => backdrop.color = selected ? selectedColor : idleColor;

        public void OnPointerDown(PointerEventData eventData) => mode.Select(this);

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragStartPosition = Frame.Rect.anchoredPosition;
            dragStartPointer = PointerInFrameParent(eventData);
        }

        // Measured from where the drag started rather than added up per frame, so grid snapping
        // doesn't eat small movements.
        public void OnDrag(PointerEventData eventData)
        {
            Vector2 position = dragStartPosition + PointerInFrameParent(eventData) - dragStartPointer;
            Frame.Move(mode.Snap(position));
            Refresh();
        }

        private void Fit(RectTransform rect)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect.parent, Frame.transform);
            rect.sizeDelta = Vector2.Max(bounds.size, minSize);
            rect.localPosition = bounds.center;
        }

        // A layout group on the element's parent must not place the backdrop, so it ignores layout.
        private static Image CreateBackdrop(UIFrame frame)
        {
            var go = new GameObject("~LayoutBackdrop", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.layer = frame.gameObject.layer;
            go.GetComponent<LayoutElement>().ignoreLayout = true;

            var rect = (RectTransform)go.transform;
            rect.SetParent(frame.Rect.parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetSiblingIndex(frame.Rect.GetSiblingIndex());

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Vector2 PointerInFrameParent(PointerEventData eventData)
        {
            var parent = (RectTransform)Frame.Rect.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 local);
            return local;
        }
    }
}
