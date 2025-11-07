// Resource Bar Testing Utility - New Input System Version
// Add this to your player or a separate testing GameObject
// Provides quick hotkeys to test all three resource bars

using UnityEngine;
using UnityEngine.InputSystem;

public class ResourceBarTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RPGResources rpgResources;

    [Header("Test Settings")]
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float healAmount = 10f;
    [SerializeField] private float manaUseAmount = 15f;
    [SerializeField] private float staminaUseAmount = 20f;

    [Header("Quick Set Amounts")]
    [SerializeField] private float quickSetPercent = 50f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    [Header("On-Screen Display Position")]
    [SerializeField] private ScreenCorner displayPosition = ScreenCorner.BottomLeft;
    [SerializeField] private Vector2 offset = new Vector2(10f, 10f);
    [SerializeField] private Vector2 displaySize = new Vector2(300f, 190f);

    public enum ScreenCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    void Start()
    {
        // Auto-find RPGResources if not assigned
        if (rpgResources == null)
        {
            rpgResources = GetComponent<RPGResources>();

            if (rpgResources == null)
            {
                rpgResources = GetComponentInChildren<RPGResources>();
            }

            if (rpgResources == null)
            {
                Debug.LogWarning("[ResourceBarTester] RPGResources not found! Please assign it in the Inspector.");
            }
        }

        if (rpgResources != null && showDebugInfo)
        {
            Debug.Log("[ResourceBarTester] Connected to RPGResources. Press keys to test bars:");
            Debug.Log("  H = Damage Health | J = Heal Health");
            Debug.Log("  M = Use Mana | N = Restore Mana");
            Debug.Log("  K = Drain Stamina | L = Restore Stamina");
            Debug.Log("  1/2/3 = Set Health/Mana/Stamina to 50%");
            Debug.Log("  4/5/6 = Set Health/Mana/Stamina to 25% (test low warning colors)");
            Debug.Log("  0 = Restore all to 100%");
        }
    }

    void Update()
    {
        if (rpgResources == null) return;

        // Use Keyboard.current from new Input System
        if (Keyboard.current == null) return;

        // Health Tests
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            TestDamageHealth();
        }
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            TestHealHealth();
        }

        // Mana Tests
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            TestUseMana();
        }
        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            TestRestoreMana();
        }

        // Stamina Tests
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            TestDrainStamina();
        }
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            TestRestoreStamina();
        }

        // Quick Set Tests (Main number row)
        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
        {
            SetHealthPercent(quickSetPercent);
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
        {
            SetManaPercent(quickSetPercent);
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
        {
            SetStaminaPercent(quickSetPercent);
        }

        // Low Value Tests (to test warning colors)
        if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
        {
            SetHealthPercent(25f);
        }
        if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame)
        {
            SetManaPercent(25f);
        }
        if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame)
        {
            SetStaminaPercent(25f);
        }

        // Restore All
        if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame)
        {
            RestoreAll();
        }
    }

    #region Health Tests

    void TestDamageHealth()
    {
        float before = rpgResources.CurrentHealth;
        rpgResources.ModifyHealth(-damageAmount);
        float after = rpgResources.CurrentHealth;

        if (showDebugInfo)
        {
            Debug.Log($"[Health Test] Damage: {before:F0} → {after:F0} (-{damageAmount})");
        }
    }

    void TestHealHealth()
    {
        float before = rpgResources.CurrentHealth;
        rpgResources.ModifyHealth(healAmount);
        float after = rpgResources.CurrentHealth;

        if (showDebugInfo)
        {
            Debug.Log($"[Health Test] Heal: {before:F0} → {after:F0} (+{healAmount})");
        }
    }

    void SetHealthPercent(float percent)
    {
        rpgResources.SetHealthToPercentage(percent / 100f);

        if (showDebugInfo)
        {
            Debug.Log($"[Health Test] Set to {percent}% = {rpgResources.CurrentHealth:F0}/{rpgResources.MaxHealth:F0}");
        }
    }

    #endregion

    #region Mana Tests

    void TestUseMana()
    {
        float before = rpgResources.CurrentMana;
        rpgResources.ModifyMana(-manaUseAmount);
        float after = rpgResources.CurrentMana;

        if (showDebugInfo)
        {
            Debug.Log($"[Mana Test] Use: {before:F0} → {after:F0} (-{manaUseAmount})");
        }
    }

    void TestRestoreMana()
    {
        float before = rpgResources.CurrentMana;
        rpgResources.ModifyMana(manaUseAmount);
        float after = rpgResources.CurrentMana;

        if (showDebugInfo)
        {
            Debug.Log($"[Mana Test] Restore: {before:F0} → {after:F0} (+{manaUseAmount})");
        }
    }

    void SetManaPercent(float percent)
    {
        rpgResources.SetManaToPercentage(percent / 100f);

        if (showDebugInfo)
        {
            Debug.Log($"[Mana Test] Set to {percent}% = {rpgResources.CurrentMana:F0}/{rpgResources.MaxMana:F0}");
        }
    }

    #endregion

    #region Stamina Tests

    void TestDrainStamina()
    {
        float before = rpgResources.CurrentStamina;
        rpgResources.ModifyStamina(-staminaUseAmount);
        float after = rpgResources.CurrentStamina;

        if (showDebugInfo)
        {
            Debug.Log($"[Stamina Test] Drain: {before:F0} → {after:F0} (-{staminaUseAmount})");
        }
    }

    void TestRestoreStamina()
    {
        float before = rpgResources.CurrentStamina;
        rpgResources.ModifyStamina(staminaUseAmount);
        float after = rpgResources.CurrentStamina;

        if (showDebugInfo)
        {
            Debug.Log($"[Stamina Test] Restore: {before:F0} → {after:F0} (+{staminaUseAmount})");
        }
    }

    void SetStaminaPercent(float percent)
    {
        rpgResources.SetStaminaToPercentage(percent / 100f);

        if (showDebugInfo)
        {
            Debug.Log($"[Stamina Test] Set to {percent}% = {rpgResources.CurrentStamina:F0}/{rpgResources.MaxStamina:F0}");
        }
    }

    #endregion

    #region Utility

    void RestoreAll()
    {
        rpgResources.SetHealthToMax();
        rpgResources.SetManaToMax();
        rpgResources.SetStaminaToMax();

        if (showDebugInfo)
        {
            Debug.Log("[Resource Test] All resources restored to 100%");
        }
    }

    [ContextMenu("Print Current Resources")]
    void PrintCurrentResources()
    {
        if (rpgResources == null)
        {
            Debug.LogWarning("RPGResources not assigned!");
            return;
        }

        Debug.Log("=== CURRENT RESOURCES ===");
        Debug.Log($"Health: {rpgResources.CurrentHealth:F0}/{rpgResources.MaxHealth:F0} ({rpgResources.HealthPercentage:P0})");
        Debug.Log($"Mana: {rpgResources.CurrentMana:F0}/{rpgResources.MaxMana:F0} ({rpgResources.ManaPercentage:P0})");
        Debug.Log($"Stamina: {rpgResources.CurrentStamina:F0}/{rpgResources.MaxStamina:F0} ({rpgResources.StaminaPercentage:P0})");
    }

    [ContextMenu("Test Low Health Warning")]
    void TestLowHealthWarning()
    {
        SetHealthPercent(20f);
    }

    [ContextMenu("Test All Bars at 50%")]
    void TestAllBarsHalfway()
    {
        SetHealthPercent(50f);
        SetManaPercent(50f);
        SetStaminaPercent(50f);
    }

    [ContextMenu("Simulate Near Death")]
    void SimulateNearDeath()
    {
        SetHealthPercent(5f);
        Debug.Log("[Test] Simulating near death - health at 5%");
    }

    #endregion

    void OnGUI()
    {
        if (!showDebugInfo || rpgResources == null) return;

        // Calculate position based on corner selection
        Rect displayRect = GetDisplayRect();

        // Display hotkey hints on screen
        GUILayout.BeginArea(displayRect);

        GUILayout.Box("=== Resource Bar Tester ===");
        GUILayout.Label("H/J: Damage/Heal Health");
        GUILayout.Label("M/N: Use/Restore Mana");
        GUILayout.Label("K/L: Drain/Restore Stamina");
        GUILayout.Label("1/2/3: Set to 50%");
        GUILayout.Label("4/5/6: Set to 25% (warning)");
        GUILayout.Label("0: Restore all to 100%");

        GUILayout.Space(5);
        GUILayout.Label($"HP: {rpgResources.CurrentHealth:F0}/{rpgResources.MaxHealth:F0}");
        GUILayout.Label($"MP: {rpgResources.CurrentMana:F0}/{rpgResources.MaxMana:F0}");
        GUILayout.Label($"SP: {rpgResources.CurrentStamina:F0}/{rpgResources.MaxStamina:F0}");

        GUILayout.EndArea();
    }

    Rect GetDisplayRect()
    {
        float x = 0;
        float y = 0;

        switch (displayPosition)
        {
            case ScreenCorner.TopLeft:
                x = offset.x;
                y = offset.y;
                break;

            case ScreenCorner.TopRight:
                x = Screen.width - displaySize.x - offset.x;
                y = offset.y;
                break;

            case ScreenCorner.BottomLeft:
                x = offset.x;
                y = Screen.height - displaySize.y - offset.y;
                break;

            case ScreenCorner.BottomRight:
                x = Screen.width - displaySize.x - offset.x;
                y = Screen.height - displaySize.y - offset.y;
                break;
        }

        return new Rect(x, y, displaySize.x, displaySize.y);
    }
}