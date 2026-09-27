using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the persistent side panels and the single key that toggles them.
/// Lives on an always-active object so it can still listen while the panels are hidden.
/// </summary>
public class UIPanelController : MonoBehaviour
{
    #region Inspector

    [Header("Panels")]
    [Tooltip("Panels toggled together. Empty slots are ignored, so future panels can be added here.")]
    [SerializeField] private List<GameObject> panels = new();

    [Header("Input")]
    [Tooltip("Key that toggles the panels.")]
    [SerializeField] private Key toggleKey = Key.I;

    [Tooltip("Close the panels when Escape is pressed.")]
    [SerializeField] private bool closeOnEscape = true;

    [Header("Startup")]
    [Tooltip("Hide the panels on Awake. Leaves them visible in edit mode for layout work.")]
    [SerializeField] private bool startHidden = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging;

    #endregion

    public static UIPanelController Instance { get; private set; }

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (startHidden) SetOpen(false);
        else IsOpen = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[toggleKey].wasPressedThisFrame)
            Toggle();

        if (closeOnEscape && IsOpen && keyboard.escapeKey.wasPressedThisFrame)
            SetOpen(false);
    }

    public void Toggle() => SetOpen(!IsOpen);

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);

    public void SetOpen(bool open)
    {
        IsOpen = open;

        for (int i = 0; i < panels.Count; i++)
        {
            if (panels[i] == null) continue;
            panels[i].SetActive(open);
        }

        if (debugLogging)
            Debug.Log($"[UIPanelController] panels {(open ? "opened" : "closed")}");
    }
}
