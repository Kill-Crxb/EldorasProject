using System.Collections.Generic;
using UnityEngine;

// A piece of HUD the player can move and scale in UI layout mode (Escape > UI). Put it on the
// element's own RectTransform: a hotbar anchor, a resource bar, a status bar.
//
// The position the element has in the scene is its default; a saved layout only ever replaces
// anchoredPosition and scale, so the anchor preset authored in the editor still decides which
// screen edge the element sticks to when the resolution changes.

namespace CrabSystem
{
    // Lookup for the live frames, and the layout last loaded or saved. A frame that turns up after
    // the layout loaded (a panel opened later) takes its saved spot when it registers.
    public static class UILayout
    {
        private static readonly Dictionary<string, UIFrame> frames = new();
        private static readonly Dictionary<string, UIFrameRecord> saved = new();

        public static IEnumerable<UIFrame> Frames => frames.Values;

        public static void Register(UIFrame frame)
        {
            if (string.IsNullOrEmpty(frame.Id))
            {
                Debug.LogError($"[UILayout] {frame.name} has no frame id — it can't be moved or saved.", frame);
                return;
            }

            if (frames.TryGetValue(frame.Id, out var existing) && existing != null && existing != frame)
            {
                Debug.LogError($"[UILayout] '{existing.name}' and '{frame.name}' share the frame id '{frame.Id}'. " +
                               "The second one wins; give each a unique id.", frame);
            }

            frames[frame.Id] = frame;

            if (saved.TryGetValue(frame.Id, out var record))
                frame.Apply(record);
        }

        public static void Unregister(UIFrame frame)
        {
            if (string.IsNullOrEmpty(frame.Id)) return;
            if (frames.TryGetValue(frame.Id, out var found) && found == frame)
                frames.Remove(frame.Id);
        }

        // Frames with no record go back to their default, so applying a layout is the whole layout.
        public static void Apply(UILayoutData data)
        {
            saved.Clear();

            foreach (var record in data.frames)
            {
                if (!string.IsNullOrEmpty(record.id)) saved[record.id] = record;
            }

            foreach (var frame in frames.Values)
            {
                if (saved.TryGetValue(frame.Id, out var record)) frame.Apply(record);
                else frame.ResetToDefault();
            }
        }

        // Live frames as they sit now, plus saved records for frames that aren't live (a hotbar
        // anchor switched off), so saving never drops a spot the player chose earlier.
        public static UILayoutData Capture()
        {
            var records = new Dictionary<string, UIFrameRecord>(saved);

            foreach (var frame in frames.Values)
                records[frame.Id] = frame.ToRecord();

            return new UILayoutData { frames = new List<UIFrameRecord>(records.Values) };
        }
    }

    [RequireComponent(typeof(RectTransform))]
    public class UIFrame : MonoBehaviour
    {
        [Tooltip("Save key for this element's spot. Write-once: changing it drops every player's saved spot for it.")]
        [SerializeField] private string frameId;

        [Tooltip("Shown on the element while the layout is being edited. Defaults to the object's name.")]
        [SerializeField] private string displayName;

        [Header("What the player can change")]
        [SerializeField] private bool canScale = true;
        [Tooltip("Bars: stretch the rect itself rather than scaling, so the fill and text stay crisp.")]
        [SerializeField] private bool canResizeWidth;
        [SerializeField] private bool canResizeHeight;
        [SerializeField] private Vector2 minSize = new Vector2(40f, 8f);
        [SerializeField] private Vector2 maxSize = new Vector2(1200f, 120f);

        public string Id => frameId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public bool CanScale => canScale;
        public bool CanResizeWidth => canResizeWidth;
        public bool CanResizeHeight => canResizeHeight;
        public Vector2 MinSize => minSize;
        public Vector2 MaxSize => maxSize;

        public RectTransform Rect { get; private set; }
        public float Scale => Rect.localScale.x;
        public Vector2 Size => Rect.rect.size;

        private Vector2 defaultPosition;
        private float defaultScale;
        private Vector2 defaultSize;

        private void Awake()
        {
            Rect = (RectTransform)transform;
            defaultPosition = Rect.anchoredPosition;
            defaultScale = Rect.localScale.x;
            defaultSize = Rect.rect.size;
        }

        private void OnEnable() => UILayout.Register(this);

        private void OnDisable() => UILayout.Unregister(this);

        public void Move(Vector2 position) => Rect.anchoredPosition = position;

        public void SetScale(float scale) => Rect.localScale = new Vector3(scale, scale, 1f);

        // Through the current anchors, so it works whether the rect is a fixed size or stretched.
        public void SetSize(Vector2 size)
        {
            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        public void ResetToDefault()
        {
            Move(defaultPosition);
            SetScale(defaultScale);
            SetSize(defaultSize);
        }

        // A record from before sizes were saved reads as size zero: keep the default size then.
        public void Apply(UIFrameRecord record)
        {
            Move(record.position);
            SetScale(record.scale);
            SetSize(record.size == Vector2.zero ? defaultSize : record.size);
        }

        public UIFrameRecord ToRecord() =>
            new UIFrameRecord { id = frameId, position = Rect.anchoredPosition, scale = Scale, size = Size };
    }
}
