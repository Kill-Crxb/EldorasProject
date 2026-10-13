// ItemTooltip.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Displays a richly formatted item tooltip.
//
// Prefab setup (everything on the ROOT object):
//   Root RectTransform
//     - Pivot: (0, 1)  Anchors: Min (0,1) Max (0,1)  — top-left, no stretch
//     - Vertical Layout Group (child force expand width ON, height OFF)
//     - Content Size Fitter (Horizontal = Preferred Size, Vertical = Preferred Size)
//   ├── Background  Image — anchors stretch (0,0,1,1), send to back
//   ├── NameText        TMP ~18pt bold
//   ├── TypeText        TMP ~11pt
//   ├── DividerTop      Image 1px height
//   ├── DescriptionText TMP ~13pt word-wrap
//   ├── DividerStats    Image 1px — DISABLED by default
//   └── StatsContainer  empty GO + Vertical Layout Group spacing 2

namespace CrabSystem
{
    public class ItemTooltip : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Stats")]
        [Tooltip("Prefab with a single TMP — instantiated into StatsContainer at runtime")]
        [SerializeField] private TextMeshProUGUI statRowPrefab;
        [SerializeField] private Transform statsContainer;
        [SerializeField] private GameObject dividerStats;

        [Header("Positioning")]
        [SerializeField] private Vector2 offset = new Vector2(18, -18);
        [SerializeField] private float edgePadding = 16f;
        [SerializeField] private bool followMouse = true;
        [SerializeField] private float followSpeed = 12f;

        [Header("Colours")]
        [SerializeField] private Color typeLabelColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color statPositiveColor = new Color(0.35f, 0.85f, 0.35f);
        [SerializeField] private Color statNegativeColor = new Color(0.90f, 0.30f, 0.30f);
        [SerializeField] private Color statScalingColor = new Color(0.55f, 0.78f, 1.00f);

        private RectTransform rectTransform;
        private Canvas canvas;
        private bool isVisible;

        private readonly System.Collections.Generic.List<TextMeshProUGUI> statRowPool
            = new System.Collections.Generic.List<TextMeshProUGUI>();

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            transform.SetAsLastSibling();

            if (canvas == null)
            {
                Debug.LogError("[ItemTooltip] Must be under a Canvas!");
            }

            Hide();
        }

        private void Update()
        {
            if (!isVisible || !followMouse) return;

            Vector2 mouse = UnityEngine.InputSystem.Mouse.current != null
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;

            if (followSpeed > 0)
                UpdatePositionSmooth(mouse);
            else
                UpdatePosition(mouse);
        }

        public void Show(ItemTooltipData data, Vector2 screenPosition)
        {
            gameObject.SetActive(true);
            isVisible = true;

            nameText.text = data.itemName;
            nameText.color = GetRarityColor(data.rarity);

            if (typeText != null)
            {
                typeText.color = typeLabelColor;
                typeText.text = FormatTypeLabel(data);
            }

            descriptionText.text = data.description;

            BuildStatRows(data.statModifiers);

            // Let layout calculate size, then position correctly
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            UpdatePosition(screenPosition);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            isVisible = false;
        }

        private void BuildStatRows(RuntimeItemStatModifier[] modifiers)
        {
            foreach (var row in statRowPool)
                row.gameObject.SetActive(false);

            bool hasStats = modifiers != null && modifiers.Length > 0;

            if (dividerStats != null)
                dividerStats.SetActive(hasStats);

            if (!hasStats || statsContainer == null || statRowPrefab == null) return;

            for (int i = 0; i < modifiers.Length; i++)
            {
                var row = GetOrCreateStatRow(i);
                row.gameObject.SetActive(true);
                row.text = FormatModifier(modifiers[i]);
                row.color = GetModifierColor(modifiers[i]);
            }
        }

        private TextMeshProUGUI GetOrCreateStatRow(int index)
        {
            if (index < statRowPool.Count)
                return statRowPool[index];

            var row = Instantiate(statRowPrefab, statsContainer);
            statRowPool.Add(row);
            return row;
        }

        private static string FormatTypeLabel(ItemTooltipData data)
        {
            if (string.IsNullOrEmpty(data.itemType)) return string.Empty;
            return data.stackCount > 1 ? $"{data.itemType}  x{data.stackCount}" : data.itemType;
        }

        private static string FormatModifier(RuntimeItemStatModifier mod)
        {
            if (mod == null) return string.Empty;

            string sign = mod.value >= 0 ? "+" : "";
            string value = mod.isPercentage
                ? $"{sign}{mod.value * 100:F0}%"
                : $"{sign}{mod.value:F1}";

            return $"{value}  {FormatStatId(mod.statName)}";
        }

        // "combat.attack_power" → "Attack Power"
        private static string FormatStatId(string statId)
        {
            if (string.IsNullOrEmpty(statId)) return statId;

            int dot = statId.LastIndexOf('.');
            string raw = dot >= 0 ? statId.Substring(dot + 1) : statId;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                   .ToTitleCase(raw.Replace('_', ' '));
        }

        private Color GetModifierColor(RuntimeItemStatModifier mod)
        {
            if (mod.isPercentage) return statScalingColor;
            return mod.value >= 0 ? statPositiveColor : statNegativeColor;
        }

        private static Color GetRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return new Color(0.75f, 0.75f, 0.75f);
                case ItemRarity.Uncommon: return new Color(0.15f, 0.85f, 0.15f);
                case ItemRarity.Rare: return new Color(0.15f, 0.45f, 1.00f);
                case ItemRarity.Epic: return new Color(0.80f, 0.15f, 0.85f);
                case ItemRarity.Legendary: return new Color(1.00f, 0.60f, 0.10f);
                default: return Color.white;
            }
        }

        private void UpdatePosition(Vector2 screenPos)
        {
            if (canvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                screenPos + offset,
                canvas.worldCamera,
                out Vector2 local);

            Vector2 clamped = ClampToCanvas(local);

            rectTransform.anchoredPosition = clamped;
        }

        private void UpdatePositionSmooth(Vector2 screenPos)
        {
            if (canvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                screenPos + offset,
                canvas.worldCamera,
                out Vector2 local);

            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition,
                ClampToCanvas(local),
                Time.deltaTime * followSpeed);
        }

        private Vector2 ClampToCanvas(Vector2 local)
        {
            Vector2 size = rectTransform.rect.size;
            Rect canvasRect = (canvas.transform as RectTransform).rect;

            // Guard: if layout hasn't run yet, skip clamping to avoid snapping to edge
            if (size.x < 1f || size.y < 1f)
            {
                return local;
            }

            local.x = Mathf.Clamp(local.x,
                canvasRect.xMin + edgePadding,
                canvasRect.xMax - size.x - edgePadding);

            local.y = Mathf.Clamp(local.y,
                canvasRect.yMin + size.y + edgePadding,
                canvasRect.yMax - edgePadding);

            return local;
        }
    }
}
