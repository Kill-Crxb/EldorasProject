// PlayerDamageDiagnostics.cs
// Add this script to Player GameObject to diagnose damage reception issues
// Usage: Add to Player, click "Run Full Diagnostics" in Inspector context menu

using UnityEngine;

public class PlayerDamageDiagnostics : MonoBehaviour
{
    [Header("Auto-Found Components")]
    [SerializeField] private ControllerBrain brain;
    [SerializeField] private DamageIn damageIn;
    [SerializeField] private DamageCoordinator damageCoordinator;
    [SerializeField] private RPGHealthAdapter healthAdapter;
    [SerializeField] private RPGResources resources;
    [SerializeField] private CharacterController characterController;

    [Header("Diagnostic Results")]
    [TextArea(10, 20)]
    [SerializeField] private string diagnosticReport = "Click 'Run Full Diagnostics' to generate report";

    void Start()
    {
        FindComponents();
    }

    void FindComponents()
    {
        brain = GetComponentInChildren<ControllerBrain>();
        if (brain != null)
        {
            damageIn = brain.GetComponentInChildren<DamageIn>();
            damageCoordinator = brain.GetComponentInChildren<DamageCoordinator>();
            healthAdapter = brain.GetComponentInChildren<RPGHealthAdapter>();
            resources = brain.GetComponentInChildren<RPGResources>();
        }
        characterController = GetComponent<CharacterController>();
    }

    [ContextMenu("Run Full Diagnostics")]
    public void RunFullDiagnostics()
    {
        FindComponents();

        string report = "=== PLAYER DAMAGE RECEPTION DIAGNOSTICS ===\n\n";

        // Test 1: Component Hierarchy
        report += "TEST 1: COMPONENT HIERARCHY\n";
        report += CheckHierarchy();
        report += "\n";

        // Test 2: DamageIn Setup
        report += "TEST 2: DAMAGEIN CONFIGURATION\n";
        report += CheckDamageIn();
        report += "\n";

        // Test 3: Health System
        report += "TEST 3: HEALTH SYSTEM\n";
        report += CheckHealthSystem();
        report += "\n";

        // Test 4: Collider Setup
        report += "TEST 4: COLLIDER SETUP\n";
        report += CheckColliders();
        report += "\n";

        // Test 5: Layer Configuration
        report += "TEST 5: LAYER CONFIGURATION\n";
        report += CheckLayers();
        report += "\n";

        // Test 6: Damage Test
        if (Application.isPlaying)
        {
            report += "TEST 6: LIVE DAMAGE TEST\n";
            report += TestDamage();
            report += "\n";
        }
        else
        {
            report += "TEST 6: LIVE DAMAGE TEST\n";
            report += "⚠️ Enter Play Mode to run live damage test\n\n";
        }

        // Summary
        report += "=== DIAGNOSTIC SUMMARY ===\n";
        report += GenerateSummary();

        diagnosticReport = report;
        Debug.Log(report);
    }

    string CheckHierarchy()
    {
        string result = "";

        result += $"Player GameObject: {gameObject.name}\n";
        result += $"  Layer: {LayerMask.LayerToName(gameObject.layer)} (Layer {gameObject.layer})\n";
        result += $"  Path: {GetGameObjectPath(transform)}\n\n";

        result += $"ControllerBrain: {(brain != null ? "✅ FOUND" : "❌ MISSING")}\n";
        if (brain != null)
        {
            result += $"  Location: {GetGameObjectPath(brain.transform)}\n";
        }
        result += "\n";

        result += $"DamageIn: {(damageIn != null ? "✅ FOUND" : "❌ MISSING")}\n";
        if (damageIn != null)
        {
            result += $"  Location: {GetGameObjectPath(damageIn.transform)}\n";
            result += $"  Searchable from CharacterController: {TestDamageInSearchability()}\n";
        }
        result += "\n";

        result += $"DamageCoordinator: {(damageCoordinator != null ? "✅ FOUND" : "⚠️ OPTIONAL")}\n";
        if (damageCoordinator != null)
        {
            result += $"  Location: {GetGameObjectPath(damageCoordinator.transform)}\n";
        }

        return result;
    }

    string CheckDamageIn()
    {
        if (damageIn == null)
        {
            return "❌ DamageIn component not found!\n" +
                   "SOLUTION: Add DamageIn component under Component_Brain/Component_Damage\n";
        }

        string result = "";
        result += $"✅ DamageIn found at: {GetGameObjectPath(damageIn.transform)}\n\n";
        result += $"Enabled: {(damageIn.IsEnabled ? "✅ YES" : "❌ NO - ENABLE THIS!")}\n";

        if (Application.isPlaying)
        {
            result += $"Is Alive: {(damageIn.IsAlive ? "✅ YES" : "⚠️ DEAD")}\n";
            result += $"Current Health: {damageIn.CurrentHealth:F1} / {damageIn.MaxHealth:F1}\n";
            result += $"Health %: {damageIn.HealthPercentage:P0}\n";
        }
        else
        {
            result += "⚠️ Enter Play Mode to see live health status\n";
        }

        return result;
    }

    string CheckHealthSystem()
    {
        string result = "";

        result += $"RPGHealthAdapter: {(healthAdapter != null ? "✅ FOUND" : "❌ MISSING")}\n";
        if (healthAdapter != null)
        {
            result += $"  Location: {GetGameObjectPath(healthAdapter.transform)}\n";
        }
        result += "\n";

        result += $"RPGResources: {(resources != null ? "✅ FOUND" : "❌ MISSING")}\n";
        if (resources != null)
        {
            result += $"  Location: {GetGameObjectPath(resources.transform)}\n";

            if (Application.isPlaying)
            {
                result += $"  Current Health: {resources.CurrentHealth:F1}\n";
                result += $"  Max Health: {resources.MaxHealth:F1}\n";
            }
        }

        if (healthAdapter == null || resources == null)
        {
            result += "\n❌ CRITICAL: Health system incomplete!\n";
            result += "SOLUTION:\n";
            result += "1. Add RPGResources under Component_Brain/Component_Stats\n";
            result += "2. Add RPGHealthAdapter under Component_Brain/Component_Damage\n";
        }

        return result;
    }

    string CheckColliders()
    {
        string result = "";

        result += $"CharacterController: {(characterController != null ? "✅ FOUND" : "⚠️ MISSING")}\n";
        if (characterController != null)
        {
            result += $"  Enabled: {characterController.enabled}\n";
            result += $"  Height: {characterController.height:F2}\n";
            result += $"  Radius: {characterController.radius:F2}\n";
            result += $"  Center: {characterController.center}\n";
        }
        result += "\n";

        // Find all colliders on player
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        result += $"Total Colliders Found: {allColliders.Length}\n";

        foreach (var col in allColliders)
        {
            result += $"  • {col.name}: {col.GetType().Name}\n";
            result += $"    - IsTrigger: {col.isTrigger}\n";
            result += $"    - Enabled: {col.enabled}\n";
            result += $"    - Layer: {LayerMask.LayerToName(col.gameObject.layer)}\n";

            // Test if this collider can find DamageIn
            if (Application.isPlaying && damageIn != null)
            {
                var testFind = col.GetComponentInParent<DamageIn>();
                if (testFind == null) testFind = col.GetComponentInChildren<DamageIn>();
                result += $"    - Can Find DamageIn: {(testFind != null ? "✅ YES" : "❌ NO")}\n";
            }
        }

        return result;
    }

    string CheckLayers()
    {
        string result = "";

        result += $"Player Layer: {LayerMask.LayerToName(gameObject.layer)} (Layer {gameObject.layer})\n";

        // Check common layer names
        int playerLayer = LayerMask.NameToLayer("Player");
        int npcLayer = LayerMask.NameToLayer("NPC");
        int targetableLayer = LayerMask.NameToLayer("Targetable");

        result += $"\nLayer Name Checks:\n";
        result += $"  'Player' layer exists: {(playerLayer != -1 ? $"✅ YES (Layer {playerLayer})" : "❌ NO")}\n";
        result += $"  'NPC' layer exists: {(npcLayer != -1 ? $"✅ YES (Layer {npcLayer})" : "⚠️ NO")}\n";
        result += $"  'Targetable' layer exists: {(targetableLayer != -1 ? $"✅ YES (Layer {targetableLayer})" : "⚠️ NO")}\n";

        result += $"\nPlayer on correct layer: {(gameObject.layer == playerLayer ? "✅ YES" : "❌ NO - SHOULD BE ON 'Player' LAYER")}\n";

        // Check Physics collision matrix
        if (playerLayer != -1 && npcLayer != -1)
        {
            bool canCollide = !Physics.GetIgnoreLayerCollision(playerLayer, npcLayer);
            result += $"\nPhysics Matrix: Player <-> NPC collision: {(canCollide ? "✅ ENABLED" : "❌ DISABLED - ENABLE IN PROJECT SETTINGS!")}\n";
        }

        return result;
    }

    string TestDamage()
    {
        if (!Application.isPlaying)
        {
            return "⚠️ Must be in Play Mode to test damage\n";
        }

        if (damageIn == null)
        {
            return "❌ Cannot test - DamageIn not found\n";
        }

        string result = "";

        float healthBefore = damageIn.CurrentHealth;
        result += $"Health Before: {healthBefore:F1} / {damageIn.MaxHealth:F1}\n";

        // Apply 10 damage
        bool success = damageIn.TakeDamage(10f, null);

        float healthAfter = damageIn.CurrentHealth;
        float damageDealt = healthBefore - healthAfter;

        result += $"Damage Test: Applied 10 damage\n";
        result += $"Health After: {healthAfter:F1} / {damageIn.MaxHealth:F1}\n";
        result += $"Actual Damage Dealt: {damageDealt:F1}\n";
        result += $"Success: {(success ? "✅ YES" : "❌ NO")}\n";

        if (Mathf.Approximately(damageDealt, 10f))
        {
            result += "✅ DAMAGE SYSTEM WORKING CORRECTLY!\n";
        }
        else if (damageDealt == 0f)
        {
            result += "❌ NO DAMAGE DEALT! Check health adapter setup.\n";
        }
        else
        {
            result += "⚠️ Damage modified by defense/armor (this is normal)\n";
        }

        // Heal back
        if (healthAdapter != null)
        {
            healthAdapter.ApplyHealing(10f);
            result += $"Healed 10 HP back to: {damageIn.CurrentHealth:F1}\n";
        }

        return result;
    }

    string GenerateSummary()
    {
        string summary = "";
        int issueCount = 0;

        if (brain == null)
        {
            summary += "❌ CRITICAL: No ControllerBrain found\n";
            issueCount++;
        }

        if (damageIn == null)
        {
            summary += "❌ CRITICAL: No DamageIn found\n";
            issueCount++;
        }
        else if (!damageIn.IsEnabled)
        {
            summary += "❌ CRITICAL: DamageIn is disabled\n";
            issueCount++;
        }

        if (healthAdapter == null)
        {
            summary += "❌ CRITICAL: No RPGHealthAdapter found\n";
            issueCount++;
        }

        if (resources == null)
        {
            summary += "❌ CRITICAL: No RPGResources found\n";
            issueCount++;
        }

        if (!TestDamageInSearchability())
        {
            summary += "⚠️ WARNING: DamageIn may not be searchable from hit colliders\n";
            issueCount++;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (gameObject.layer != playerLayer)
        {
            summary += "⚠️ WARNING: Player not on 'Player' layer\n";
            issueCount++;
        }

        if (issueCount == 0)
        {
            summary += "✅ ALL CHECKS PASSED!\n";
            summary += "If enemy hits still don't work, check:\n";
            summary += "1. Enemy weapon's SimpleWeaponHit targetLayers includes 'Player'\n";
            summary += "2. Enemy weapon has Is Trigger = true\n";
            summary += "3. Physics collision matrix allows NPC <-> Player\n";
        }
        else
        {
            summary += $"\n{issueCount} issue(s) found - fix these first!\n";
        }

        return summary;
    }

    bool TestDamageInSearchability()
    {
        if (damageIn == null || characterController == null) return false;

        // Simulate what SimpleWeaponHit does when it hits the CharacterController
        Transform hitCollider = characterController.transform;

        // Try search up
        var foundUp = hitCollider.GetComponentInParent<DamageIn>();
        if (foundUp != null) return true;

        // Try search down
        var foundDown = hitCollider.GetComponentInChildren<DamageIn>();
        if (foundDown != null) return true;

        return false;
    }

    string GetGameObjectPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    [ContextMenu("Quick Test: Apply 10 Damage")]
    public void QuickDamageTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Must be in Play Mode!");
            return;
        }

        FindComponents();

        if (damageIn != null)
        {
            float before = damageIn.CurrentHealth;
            damageIn.TakeDamage(10f, null);
            float after = damageIn.CurrentHealth;

            Debug.Log($"Quick Damage Test: {before:F1} → {after:F1} (Dealt: {before - after:F1} damage)");
        }
        else
        {
            Debug.LogError("DamageIn not found!");
        }
    }

    [ContextMenu("Enable All Debug Logging")]
    public void EnableAllDebugLogging()
    {
        FindComponents();

        if (damageIn != null)
        {
            // Use reflection to enable debug (since it's likely a private field)
            var field = damageIn.GetType().GetField("debugDamageReceived",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(damageIn, true);
                Debug.Log("✅ Enabled DamageIn debug logging");
            }
        }

        if (healthAdapter != null)
        {
            var field = healthAdapter.GetType().GetField("debugLogs",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(healthAdapter, true);
                Debug.Log("✅ Enabled RPGHealthAdapter debug logging");
            }
        }

        Debug.Log("Debug logging enabled where possible. Check component inspectors to enable manually if needed.");
    }
}