using UnityEngine;

public class StatSystemTest : MonoBehaviour
{
    [SerializeField] private ControllerBrain brain;

    void Start()
    {
        if (brain == null)
            brain = GetComponent<ControllerBrain>();

        if (brain == null || brain.Stats == null)
        {
            Debug.LogError("[StatSystemTest] Brain or StatSystem not found!");
            return;
        }

        // Wait one frame for initialization
        Invoke(nameof(RunTests), 0.1f);
    }

    void RunTests()
    {
        Debug.Log("========================================");
        Debug.Log("STAT SYSTEM INTEGRATION TEST");
        Debug.Log("========================================");

        TestCoreStats();
        TestResourceFormulas();
        TestCombatStats();
        TestModifiers();
        TestContributions();

        Debug.Log("========================================");
        Debug.Log("ALL TESTS COMPLETE");
        Debug.Log("========================================");
    }

    void TestCoreStats()
    {
        Debug.Log("\n--- TEST 1: Core Stats ---");

        var stats = brain.Stats;

        float body = stats.GetValue("character.body");
        float mind = stats.GetValue("character.mind");
        float spirit = stats.GetValue("character.spirit");

        Debug.Log($"Body: {body} (expected: 10)");
        Debug.Log($"Mind: {mind} (expected: 10)");
        Debug.Log($"Spirit: {spirit} (expected: 10)");

        if (body == 10 && mind == 10 && spirit == 10)
            Debug.Log("✓ Core Stats PASSED");
        else
            Debug.LogError("✗ Core Stats FAILED");
    }

    void TestResourceFormulas()
    {
        Debug.Log("\n--- TEST 2: Resource Formulas ---");

        var stats = brain.Stats;

        float body = stats.GetValue("character.body");
        float endurance = stats.GetValue("character.endurance");
        float mind = stats.GetValue("character.mind");
        float insight = stats.GetValue("character.insight");

        // Test max_health formula
        float expectedHealth = endurance * 10 + body * 5;
        float actualHealth = stats.GetValue("character.max_health");

        Debug.Log($"Expected Health: {expectedHealth} ({endurance}*10 + {body}*5)");
        Debug.Log($"Actual Health: {actualHealth}");

        if (Mathf.Approximately(expectedHealth, actualHealth))
            Debug.Log("✓ Formula Calculation PASSED");
        else
            Debug.LogError($"✗ Formula Calculation FAILED (off by {Mathf.Abs(expectedHealth - actualHealth)})");

        // Test max_mana formula
        float expectedMana = mind * 8 + insight * 4;
        float actualMana = stats.GetValue("character.max_mana");

        Debug.Log($"Expected Mana: {expectedMana} ({mind}*8 + {insight}*4)");
        Debug.Log($"Actual Mana: {actualMana}");
    }

    void TestCombatStats()
    {
        Debug.Log("\n--- TEST 3: Combat Stats (Contribution-Based) ---");

        var stats = brain.Stats;

        float attackPower = stats.GetValue("combat.attack_power");
        float critChance = stats.GetValue("combat.crit_chance");
        float armor = stats.GetValue("combat.armor");

        Debug.Log($"Attack Power: {attackPower} (expected: 0, no items equipped)");
        Debug.Log($"Crit Chance: {critChance} (expected: 5, base value)");
        Debug.Log($"Armor: {armor} (expected: 0, no items equipped)");

        if (attackPower == 0 && critChance == 5 && armor == 0)
            Debug.Log("✓ Combat Stats PASSED");
        else
            Debug.LogError("✗ Combat Stats FAILED");
    }

    void TestModifiers()
    {
        Debug.Log("\n--- TEST 4: Modifiers (Flat + Percentage) ---");

        var stats = brain.Stats;

        // Add flat modifier: +50 attack power
        stats.AddFlatModifier("combat.attack_power", "test.flat", 50f);
        float withFlat = stats.GetValue("combat.attack_power");
        Debug.Log($"After +50 flat: {withFlat} (expected: 50)");

        // Add percentage modifier: +50%
        stats.AddPercentModifier("combat.attack_power", "test.percent", 0.5f);
        float withPercent = stats.GetValue("combat.attack_power");
        Debug.Log($"After +50% percent: {withPercent} (expected: 75)");

        // Remove modifiers
        stats.RemoveAllModifiersFromSource("test.flat");
        stats.RemoveAllModifiersFromSource("test.percent");
        float afterRemove = stats.GetValue("combat.attack_power");
        Debug.Log($"After removing mods: {afterRemove} (expected: 0)");

        if (withFlat == 50 && withPercent == 75 && afterRemove == 0)
            Debug.Log("✓ Modifiers PASSED");
        else
            Debug.LogError("✗ Modifiers FAILED");
    }

    void TestContributions()
    {
        Debug.Log("\n--- TEST 5: Contribution Bonuses (X per Y) ---");

        var stats = brain.Stats;

        float body = stats.GetValue("character.body");

        // Add contribution: +2 attack power per Body
        stats.AddContributionBonus("combat.attack_power", "test.contrib", "character.body", 2.0f);

        float expected = body * 2;
        float actual = stats.GetValue("combat.attack_power");

        Debug.Log($"Body: {body}");
        Debug.Log($"Expected Attack (Body * 2): {expected}");
        Debug.Log($"Actual Attack: {actual}");

        if (Mathf.Approximately(expected, actual))
        {
            Debug.Log("✓ Contribution Bonus PASSED");

            // Test auto-recalculation
            stats.SetBaseValue("character.body", 15);
            float newAttack = stats.GetValue("combat.attack_power");
            Debug.Log($"Changed Body to 15, Attack auto-recalculated to: {newAttack} (expected: 30)");

            if (Mathf.Approximately(newAttack, 30))
                Debug.Log("✓ Auto-Recalculation PASSED");
            else
                Debug.LogError("✗ Auto-Recalculation FAILED");

            // Reset
            stats.SetBaseValue("character.body", 10);
        }
        else
        {
            Debug.LogError("✗ Contribution Bonus FAILED");
        }

        // Cleanup
        stats.RemoveAllModifiersFromSource("test.contrib");
    }

    [ContextMenu("Run Tests")]
    void RunTestsFromMenu()
    {
        RunTests();
    }
}