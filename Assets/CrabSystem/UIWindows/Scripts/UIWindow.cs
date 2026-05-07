using UnityEngine;
using UnityEngine.EventSystems;

public abstract class UIWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
{
    [Header("Window Components")]
    [SerializeField] protected RectTransform windowBackground;
    [SerializeField] protected RectTransform headerBar;

    [Header("Window Positioning")]
    [SerializeField] protected bool rememberPosition = true;
    [SerializeField] protected Vector2 defaultPosition = Vector2.zero; // (0,0) = center

    [Header("Debug")]
    [SerializeField] protected bool debugMode = false;

    protected string windowId;
    protected ControllerBrain playerBrain;
    protected UIWindowManager windowManager;
    protected bool isInitialized = false;

    private Vector2 dragOffset;
    private string positionPrefsKey;

    public string WindowId => windowId;
    public bool IsOpen => gameObject.activeSelf;

    public virtual void Initialize(string id, ControllerBrain player, UIWindowManager manager)
    {
        if (isInitialized) return;

        windowId = id;
        playerBrain = player;
        windowManager = manager;
        positionPrefsKey = $"UIWindow_{windowId}_Position";

        SetupWindow();
        RestoreOrSetDefaultPosition();

        isInitialized = true;

        if (debugMode)
            Debug.Log($"[UIWindow] {windowId} initialized");
    }

    protected abstract void SetupWindow();

    public virtual void OnClose()
    {
        SavePosition();

        if (debugMode)
            Debug.Log($"[UIWindow] {windowId} closing");
    }

    private void RestoreOrSetDefaultPosition()
    {
        if (windowBackground == null) return;

        Vector2 position;

        // Try to restore saved position
        if (rememberPosition && PlayerPrefs.HasKey(positionPrefsKey + "_x"))
        {
            float x = PlayerPrefs.GetFloat(positionPrefsKey + "_x");
            float y = PlayerPrefs.GetFloat(positionPrefsKey + "_y");
            position = new Vector2(x, y);

            if (debugMode)
                Debug.Log($"[UIWindow] {windowId} restored position: {position}");
        }
        else
        {
            // Use default position
            position = defaultPosition;

            if (debugMode)
                Debug.Log($"[UIWindow] {windowId} using default position: {position}");
        }

        windowBackground.anchoredPosition = position;
    }

    private void SavePosition()
    {
        if (!rememberPosition || windowBackground == null) return;

        PlayerPrefs.SetFloat(positionPrefsKey + "_x", windowBackground.anchoredPosition.x);
        PlayerPrefs.SetFloat(positionPrefsKey + "_y", windowBackground.anchoredPosition.y);
        PlayerPrefs.Save();

        if (debugMode)
            Debug.Log($"[UIWindow] {windowId} saved position: {windowBackground.anchoredPosition}");
    }

    [ContextMenu("Reset Saved Position")]
    private void ResetSavedPosition()
    {
        PlayerPrefs.DeleteKey(positionPrefsKey + "_x");
        PlayerPrefs.DeleteKey(positionPrefsKey + "_y");
        PlayerPrefs.Save();

        if (debugMode)
            Debug.Log($"[UIWindow] {windowId} position reset - will use default next time");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (windowManager != null)
            windowManager.BringWindowToFront(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (windowBackground == null || headerBar == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            windowBackground,
            eventData.position,
            eventData.pressEventCamera,
            out dragOffset
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (windowBackground == null) return;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            windowBackground.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint))
        {
            windowBackground.anchoredPosition = localPoint - dragOffset;
        }
    }

    protected void CloseThisWindow()
    {
        if (windowManager != null)
            windowManager.CloseWindow(windowId);
    }
}