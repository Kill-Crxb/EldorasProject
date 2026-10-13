using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrabSystem
{
    public enum StatusBarSubject { Player, Target }
    public enum StatusBarGrowth { Right, Left, Up, Down }

    // S5: a row (or grid) of status icons for the player or the player's soft target. Place the object where the first
    // icon should sit — the bar grows from there in `grow`, starting a new row every `perRow` icons in `wrap`. Each icon
    // shows the status icon (or its first letters when it has none), a harmful / beneficial border, a sweep for the time
    // left, the seconds, and the stack count. Hit states (tag hitstate) are hidden by default. Hovering an icon (with the
    // cursor free) shows a tooltip: name, stacks, description, crowd-control category and save, time left, source.
    //
    // Icons are built at runtime and never saved with the scene. Outside play mode the bar shows `previewIcons` blanks
    // so it can be placed and sized by eye. Tools → Crab → UI → Add Status Bar (Player / Target) makes one.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class StatusBarView : MonoBehaviour
    {
        [SerializeField] private StatusBarSubject subject = StatusBarSubject.Player;

        [Header("Layout")]
        [SerializeField] private StatusBarGrowth grow = StatusBarGrowth.Right;
        [Tooltip("Where new rows go. Must run across `grow` (Right / Left with Up / Down, or the other way).")]
        [SerializeField] private StatusBarGrowth wrap = StatusBarGrowth.Down;
        [SerializeField, Min(1)] private int perRow = 8;
        [SerializeField, Min(1)] private int maxIcons = 16;
        [SerializeField] private float iconSize = 36f;
        [SerializeField] private float spacing = 4f;
        [Tooltip("Blank icons shown outside play mode, to place and size the bar.")]
        [SerializeField, Min(0)] private int previewIcons = 6;

        [Header("Filter")]
        [SerializeField] private bool showBeneficial = true;
        [SerializeField] private bool showHarmful = true;
        [Tooltip("Beneficial first, then harmful. Off = in the order they were applied.")]
        [SerializeField] private bool beneficialFirst = true;
        [SerializeField] private List<string> hideTags = new() { "hitstate" };

        [Header("Look")]
        [SerializeField] private Color beneficialBorder = new Color(0.35f, 0.85f, 0.45f);
        [SerializeField] private Color harmfulBorder = new Color(0.9f, 0.3f, 0.3f);
        [SerializeField] private Color iconBackground = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        [SerializeField] private Color sweepColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private float borderWidth = 2f;
        [SerializeField] private bool showSeconds = true;
        [SerializeField] private bool showStacks = true;
        [SerializeField] private float fontSize = 13f;

        [Header("Tooltip")]
        [SerializeField] private bool showTooltip = true;
        [SerializeField] private float tooltipWidth = 230f;
        [SerializeField] private Color tooltipBackground = new Color(0.06f, 0.06f, 0.08f, 0.92f);

        private readonly List<Icon> icons = new();
        private readonly List<StatusInstance> shown = new();
        private ControllerBrain player;
        private int hovered = -1;
        private RectTransform tooltip;
        private TextMeshProUGUI tooltipText;

        private class Icon
        {
            public RectTransform Rect;
            public Image Border;
            public Image Art;
            public Image Sweep;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Seconds;
            public TextMeshProUGUI Stacks;
        }

        private void OnDisable()
        {
            foreach (Icon icon in icons)
            {
                if (icon.Rect == null) continue;
                if (Application.isPlaying) Destroy(icon.Rect.gameObject);
                else DestroyImmediate(icon.Rect.gameObject);
            }
            icons.Clear();

            if (tooltip != null && Application.isPlaying) Destroy(tooltip.gameObject);
            else if (tooltip != null) DestroyImmediate(tooltip.gameObject);
            tooltip = null;
            hovered = -1;
        }

        public void Hover(int index) => hovered = index;

        public void Unhover(int index)
        {
            if (hovered == index) hovered = -1;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                ShowPreview();
                return;
            }

            Collect(Subject());
            for (int i = 0; i < shown.Count; i++) Fill(IconAt(i), shown[i]);
            HideFrom(shown.Count);
            UpdateTooltip();
        }

        // ---- What to show ----

        private StatusSystem Subject()
        {
            if (player == null) player = PlayerBrainAccess.Find();
            if (player == null) return null;
            if (subject == StatusBarSubject.Player) return player.GetModule<StatusSystem>();

            TargetingModule targeting = player.GetModule<TargetingModule>();
            ControllerBrain target = targeting != null ? targeting.CurrentTarget : null;
            return target != null ? target.GetModule<StatusSystem>() : null;
        }

        private void Collect(StatusSystem statuses)
        {
            shown.Clear();
            if (statuses == null) return;

            if (beneficialFirst) AddWhere(statuses, true);
            AddWhere(statuses, false);
        }

        // First pass (beneficialFirst): the beneficial ones. Second pass: whatever is left, in order of application.
        private void AddWhere(StatusSystem statuses, bool beneficialOnly)
        {
            foreach (StatusInstance instance in statuses.Active)
            {
                if (shown.Count >= maxIcons) return;
                if (beneficialOnly && !instance.Definition.beneficial) continue;
                if (shown.Contains(instance) || !Shows(instance.Definition)) continue;
                shown.Add(instance);
            }
        }

        private bool Shows(StatusDefinition definition)
        {
            if (definition.beneficial ? !showBeneficial : !showHarmful) return false;
            foreach (string tag in hideTags)
            {
                if (definition.HasTag(tag)) return false;
            }
            return true;
        }

        // ---- Drawing ----

        private void Fill(Icon icon, StatusInstance instance)
        {
            StatusDefinition definition = instance.Definition;
            icon.Rect.gameObject.SetActive(true);
            icon.Border.color = definition.beneficial ? beneficialBorder : harmfulBorder;

            bool hasArt = definition.icon != null;
            icon.Art.sprite = definition.icon;
            icon.Art.color = hasArt ? Color.white : iconBackground;
            icon.Label.text = hasArt ? "" : Initials(definition);

            icon.Sweep.fillAmount = instance.IsPermanent ? 0f : 1f - instance.Progress;

            bool seconds = showSeconds && !instance.IsPermanent;
            icon.Seconds.enabled = seconds;
            if (seconds && instance.Remaining >= 10f) icon.Seconds.SetText("{0:0}", instance.Remaining);
            else if (seconds) icon.Seconds.SetText("{0:0.0}", instance.Remaining);

            bool stacks = showStacks && instance.Stacks > 1;
            icon.Stacks.enabled = stacks;
            if (stacks) icon.Stacks.SetText("{0}", instance.Stacks);
        }

        private static string Initials(StatusDefinition definition)
        {
            string name = string.IsNullOrEmpty(definition.displayName) ? definition.id : definition.displayName;
            return name.Length <= 3 ? name : name.Substring(0, 3);
        }

        // ---- Tooltip ----

        private void UpdateTooltip()
        {
            bool show = showTooltip && hovered >= 0 && hovered < shown.Count;
            if (!show)
            {
                if (tooltip != null) tooltip.gameObject.SetActive(false);
                return;
            }

            if (tooltip == null) MakeTooltip();
            tooltip.gameObject.SetActive(true);
            tooltipText.text = Describe(shown[hovered]);

            float height = tooltipText.GetPreferredValues(tooltipText.text, tooltipWidth - 16f, 0f).y + 12f;
            tooltip.sizeDelta = new Vector2(tooltipWidth, height);
            PlaceTooltip(icons[hovered].Rect);
        }

        private string Describe(StatusInstance instance)
        {
            StatusDefinition definition = instance.Definition;
            string name = string.IsNullOrEmpty(definition.displayName) ? definition.id : definition.displayName;
            string colour = ColorUtility.ToHtmlStringRGB(definition.beneficial ? beneficialBorder : harmfulBorder);
            string text = $"<b><color=#{colour}>{name}</color></b>";
            if (definition.stacking == StatusStacking.Stack) text += $"  ×{instance.Stacks}/{definition.maxStacks}";
            if (!string.IsNullOrEmpty(definition.description)) text += $"\n{definition.description}";

            string rules = "";
            if (definition.control != ControlCategory.None) rules += $"{definition.control} CC";
            if (definition.save != SaveType.None) rules += $"{(rules.Length > 0 ? " · " : "")}{definition.save} save";
            if (definition.breaksOnDamage) rules += $"{(rules.Length > 0 ? " · " : "")}breaks on damage";
            if (rules.Length > 0) text += $"\n<size=85%><color=#AAAAAA>{rules}</color></size>";

            text += instance.IsPermanent ? "\n<size=85%>Until removed</size>" : $"\n<size=85%>{instance.Remaining:0.0} s left</size>";
            if (instance.Source != null) text += $"<size=85%><color=#AAAAAA>  ·  from {instance.Source.EntityName}</color></size>";
            return text;
        }

        // Beside the hovered icon, on the side away from where new rows go, so it never covers the other icons.
        private void PlaceTooltip(RectTransform icon)
        {
            Vector2 across = Step(wrap);
            if (Mathf.Abs(Vector2.Dot(Step(grow), across)) > 0.5f) across = new Vector2(Step(grow).y, -Step(grow).x);

            Vector2 pivot = icon.pivot;
            Vector2 offset = Vector2.zero;
            if (across.y < 0f) { pivot.y = 0f; offset.y = spacing; }
            if (across.y > 0f) { pivot.y = 1f; offset.y = -spacing; }
            if (across.x > 0f) { pivot.x = 1f; offset.x = -spacing; }
            if (across.x < 0f) { pivot.x = 0f; offset.x = spacing; }

            tooltip.anchorMin = tooltip.anchorMax = icon.anchorMin;
            tooltip.pivot = pivot;
            tooltip.anchoredPosition = icon.anchoredPosition + offset;
            tooltip.SetAsLastSibling();
        }

        private void MakeTooltip()
        {
            Image background = Child<Image>("Tooltip", transform, Vector2.zero, Vector2.one);
            background.color = tooltipBackground;
            tooltip = background.rectTransform;

            tooltipText = Text("Text", tooltip, Vector2.zero, Vector2.one, TextAlignmentOptions.TopLeft);
            tooltipText.fontStyle = FontStyles.Normal;
            tooltipText.fontSize = fontSize;
            tooltipText.textWrappingMode = TextWrappingModes.Normal;
            tooltipText.rectTransform.offsetMin = new Vector2(8f, 6f);
            tooltipText.rectTransform.offsetMax = new Vector2(-8f, -6f);
        }

        private void ShowPreview()
        {
            for (int i = 0; i < previewIcons; i++)
            {
                Icon icon = IconAt(i);
                icon.Rect.gameObject.SetActive(true);
                icon.Border.color = i % 2 == 0 ? beneficialBorder : harmfulBorder;
                icon.Art.sprite = null;
                icon.Art.color = iconBackground;
                icon.Label.text = (i + 1).ToString();
                icon.Sweep.fillAmount = 0.35f;
                icon.Seconds.enabled = false;
                icon.Stacks.enabled = false;
            }
            HideFrom(previewIcons);
        }

        private void HideFrom(int count)
        {
            for (int i = count; i < icons.Count; i++) icons[i].Rect.gameObject.SetActive(false);
        }

        // Built on demand; re-laid out every time so inspector changes show at once.
        private Icon IconAt(int index)
        {
            while (icons.Count <= index) icons.Add(MakeIcon());

            Icon icon = icons[index];
            Place(icon.Rect, index);
            return icon;
        }

        private void Place(RectTransform rect, int index)
        {
            Vector2 along = Step(grow);
            Vector2 across = Step(wrap);
            if (Mathf.Abs(Vector2.Dot(along, across)) > 0.5f) across = new Vector2(along.y, -along.x);

            float pitch = iconSize + spacing;
            Vector2 pivot = new Vector2(along.x < 0f || across.x < 0f ? 1f : 0f, along.y < 0f || across.y < 0f ? 1f : 0f);

            RectTransform bar = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = bar.pivot;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(iconSize, iconSize);
            rect.anchoredPosition = along * (index % perRow) * pitch + across * (index / perRow) * pitch;
            rect.localScale = Vector3.one;

            float font = fontSize * iconSize / 36f;
            Icon icon = icons[index];
            icon.Label.fontSize = font;
            icon.Seconds.fontSize = font * 0.85f;
            icon.Stacks.fontSize = font;
            icon.Art.rectTransform.offsetMin = new Vector2(borderWidth, borderWidth);
            icon.Art.rectTransform.offsetMax = new Vector2(-borderWidth, -borderWidth);
        }

        private static Vector2 Step(StatusBarGrowth growth) => growth switch
        {
            StatusBarGrowth.Right => Vector2.right,
            StatusBarGrowth.Left => Vector2.left,
            StatusBarGrowth.Up => Vector2.up,
            _ => Vector2.down,
        };

        private Icon MakeIcon()
        {
            var icon = new Icon();
            icon.Border = Child<Image>("Icon", transform, Vector2.zero, Vector2.one);
            icon.Rect = icon.Border.rectTransform;
            icon.Rect.gameObject.hideFlags = HideFlags.DontSave;
            icon.Border.raycastTarget = true;
            StatusIconHover hover = icon.Rect.gameObject.AddComponent<StatusIconHover>();
            hover.Bar = this;
            hover.Index = icons.Count;

            icon.Art = Child<Image>("Art", icon.Rect, Vector2.zero, Vector2.one);
            icon.Sweep = Child<Image>("Sweep", icon.Art.rectTransform, Vector2.zero, Vector2.one);
            icon.Sweep.type = Image.Type.Filled;
            icon.Sweep.fillMethod = Image.FillMethod.Radial360;
            icon.Sweep.fillClockwise = false;
            icon.Sweep.color = sweepColor;

            icon.Label = Text("Label", icon.Rect, Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
            icon.Seconds = Text("Seconds", icon.Rect, Vector2.zero, new Vector2(1f, 0.45f), TextAlignmentOptions.Bottom);
            icon.Stacks = Text("Stacks", icon.Rect, new Vector2(0.5f, 0.5f), Vector2.one, TextAlignmentOptions.TopRight);
            return icon;
        }

        private static T Child<T>(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.hideFlags = HideFlags.DontSave;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            T graphic = go.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions align)
        {
            TextMeshProUGUI text = Child<TextMeshProUGUI>(name, parent, anchorMin, anchorMax);
            text.alignment = align;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
