using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI layout mode, WoW Edit Mode style. Escape > UI opens it: every UIFrame on screen gets a box
// to drag. The toolbar shows the options the selected frame allows (scale, width, height),
// snaps to a grid and resets to defaults. Save
// writes the layout to the account; Cancel, or Escape, puts everything back as it was.
//
// Sits on an always-active object with a UIPanel (side Layout) whose content is the overlay, so
// the panel system frees the cursor and pauses gameplay input while it's open. Loading lives
// here too: the saved layout is applied when the character finishes loading.

namespace CrabSystem
{
    [RequireComponent(typeof(UIPanel))]
    public class UILayoutMode : MonoBehaviour
    {
        private const string FileName = "ui_layout";

        [Header("Overlay")]
        [SerializeField] private RectTransform handleContainer;
        [SerializeField] private UILayoutHandle handleTemplate;

        [Header("Toolbar")]
        [SerializeField] private TMP_Text selectedLabel;
        [SerializeField] private GameObject scaleRow;
        [SerializeField] private Slider scaleSlider;
        [SerializeField] private GameObject widthRow;
        [SerializeField] private Slider widthSlider;
        [SerializeField] private GameObject heightRow;
        [SerializeField] private Slider heightSlider;
        [SerializeField] private Button gridButton;
        [SerializeField] private TMP_Text gridLabel;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button saveButton;

        [Header("Tuning")]
        [SerializeField] private float gridSize = 16f;
        [SerializeField] private bool gridOn = true;
        [SerializeField] private float minScale = 0.5f;
        [SerializeField] private float maxScale = 2f;

        private UIPanel panel;
        private readonly List<UILayoutHandle> handles = new();
        private UILayoutHandle selected;
        private UILayoutData snapshot;
        private bool committed;
        private bool syncing;

        private void Awake()
        {
            panel = GetComponent<UIPanel>();

            if (handleContainer == null || handleTemplate == null || selectedLabel == null ||
                scaleRow == null || scaleSlider == null || widthRow == null || widthSlider == null ||
                heightRow == null || heightSlider == null ||
                gridButton == null || gridLabel == null || resetButton == null || cancelButton == null || saveButton == null)
            {
                Debug.LogError("[UILayoutMode] Overlay or toolbar references not assigned", this);
                enabled = false;
                return;
            }

            handleTemplate.gameObject.SetActive(false);
        }

        private void Start()
        {
            GameEvents.OnLoadCompleted += HandleLoadCompleted;
            panel.OpenChanged += HandleOpenChanged;

            scaleSlider.onValueChanged.AddListener(HandleScale);
            widthSlider.onValueChanged.AddListener(HandleWidth);
            heightSlider.onValueChanged.AddListener(HandleHeight);
            gridButton.onClick.AddListener(ToggleGrid);
            resetButton.onClick.AddListener(ResetAll);
            cancelButton.onClick.AddListener(Cancel);
            saveButton.onClick.AddListener(Save);
        }

        private void OnDestroy()
        {
            GameEvents.OnLoadCompleted -= HandleLoadCompleted;
            if (panel != null) panel.OpenChanged -= HandleOpenChanged;
            if (!enabled) return;

            scaleSlider.onValueChanged.RemoveListener(HandleScale);
            widthSlider.onValueChanged.RemoveListener(HandleWidth);
            heightSlider.onValueChanged.RemoveListener(HandleHeight);
            gridButton.onClick.RemoveListener(ToggleGrid);
            resetButton.onClick.RemoveListener(ResetAll);
            cancelButton.onClick.RemoveListener(Cancel);
            saveButton.onClick.RemoveListener(Save);
        }

        // The escape menu's "UI" entry. Opening before closing the menu keeps a panel open
        // throughout, so the cursor doesn't lock for a frame in between.
        public void Enter()
        {
            panel.Open();
            UIPanelRegistry.Menu?.Close();
        }

        public void Cancel() => panel.Close();

        public void Save()
        {
            committed = true;

            // Applying it makes it the remembered layout, so frames that register later take it too.
            var data = UILayout.Capture();
            UILayout.Apply(data);
            Write(data);

            panel.Close();
        }

        public void ResetAll()
        {
            foreach (var frame in UILayout.Frames) frame.ResetToDefault();
            foreach (var handle in handles) handle.Refresh();

            Select(selected);
        }

        public void ToggleGrid()
        {
            gridOn = !gridOn;
            gridLabel.text = gridOn ? "Grid: On" : "Grid: Off";
        }

        public Vector2 Snap(Vector2 position)
        {
            if (!gridOn || gridSize <= 0f) return position;
            return new Vector2(Mathf.Round(position.x / gridSize) * gridSize, Mathf.Round(position.y / gridSize) * gridSize);
        }

        public void Select(UILayoutHandle handle)
        {
            if (selected != null) selected.SetSelected(false);
            selected = handle;

            UIFrame frame = selected != null ? selected.Frame : null;
            scaleRow.SetActive(frame != null && frame.CanScale);
            widthRow.SetActive(frame != null && frame.CanResizeWidth);
            heightRow.SetActive(frame != null && frame.CanResizeHeight);

            if (frame == null)
            {
                selectedLabel.text = "Click an element";
                return;
            }

            selected.SetSelected(true);
            selectedLabel.text = frame.DisplayName;
            SetSlider(scaleSlider, minScale, maxScale, frame.Scale);
            SetSlider(widthSlider, frame.MinSize.x, frame.MaxSize.x, frame.Size.x);
            SetSlider(heightSlider, frame.MinSize.y, frame.MaxSize.y, frame.Size.y);
        }

        // Changing a slider's range re-clamps its value and fires onValueChanged, which would
        // resize the newly selected frame; the flag keeps that from reaching the handlers.
        private void SetSlider(Slider slider, float min, float max, float value)
        {
            syncing = true;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            syncing = false;
        }

        private void HandleScale(float scale)
        {
            if (selected == null || syncing) return;

            selected.Frame.SetScale(scale);
            selected.Refresh();
        }

        // Whole pixels, so a bar's edges stay sharp.
        private void HandleWidth(float width)
        {
            if (selected == null || syncing) return;

            selected.Frame.SetSize(new Vector2(Mathf.Round(width), selected.Frame.Size.y));
            selected.Refresh();
        }

        private void HandleHeight(float height)
        {
            if (selected == null || syncing) return;

            selected.Frame.SetSize(new Vector2(selected.Frame.Size.x, Mathf.Round(height)));
            selected.Refresh();
        }

        private void HandleOpenChanged(UIPanel _)
        {
            if (panel.IsOpen) BeginEdit();
            else EndEdit();
        }

        private void BeginEdit()
        {
            snapshot = UILayout.Capture();
            committed = false;

            foreach (var frame in UILayout.Frames)
            {
                var handle = Instantiate(handleTemplate, handleContainer);
                handle.gameObject.SetActive(true);
                handle.Bind(this, frame);
                handles.Add(handle);
            }

            gridLabel.text = gridOn ? "Grid: On" : "Grid: Off";
            Select(null);
        }

        // Every way out that isn't Save (Cancel, Escape, the panel closing for any reason) reverts.
        private void EndEdit()
        {
            if (!committed && snapshot != null) UILayout.Apply(snapshot);

            foreach (var handle in handles) Destroy(handle.gameObject);
            handles.Clear();
            selected = null;
        }

        private async void HandleLoadCompleted()
        {
            string account = AccountFolder();
            ISaveProvider provider = Provider();
            if (account == null || provider == null) return;

            string json = await provider.Load(account, FileName);
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<UILayoutData>(json);
            if (data != null) UILayout.Apply(data);
        }

        private async void Write(UILayoutData data)
        {
            string account = AccountFolder();
            ISaveProvider provider = Provider();

            if (account == null || provider == null)
            {
                Debug.LogError("[UILayoutMode] No account or save provider — the layout was not saved.", this);
                return;
            }

            // The provider's file calls take a path under Saves/; the account's own folder sits
            // beside its characters' folders, so account-wide files go there.
            bool ok = await provider.Save(account, FileName, JsonUtility.ToJson(data, prettyPrint: true));
            if (!ok) Debug.LogError($"[UILayoutMode] Saving the layout for '{account}' failed.", this);
        }

        private static ISaveProvider Provider() => ManagerBrain.Instance?.GetManager<AccountManager>()?.SaveProvider;

        // Scenes started directly (SceneQuickStart) skip login, but a character id always starts
        // with its account ("Crxb/Hanzo_1740220800"), so the account comes from there instead.
        private static string AccountFolder()
        {
            var accounts = ManagerBrain.Instance?.GetManager<AccountManager>();
            if (accounts != null && accounts.IsLoggedIn) return accounts.ActiveAccountName;

            string character = ManagerBrain.Instance?.GetManager<SaveManager>()?.ActiveCharacterId;
            if (string.IsNullOrEmpty(character)) return null;

            int slash = character.IndexOf('/');
            return slash > 0 ? character.Substring(0, slash) : null;
        }
    }
}
