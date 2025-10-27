// InventoryGridDataTests.cs - Test scenarios for the data layer
using UnityEngine;


public class InventoryGridDataTests : MonoBehaviour
{
    [ContextMenu("Run All Tests")]
    public void RunAllTests()
    {
        Debug.Log("=== RUNNING INVENTORY GRID DATA TESTS ===\n");

        TestBasicPlacement();
        TestMultiSlotPlacement();
        TestOverlapDetection();
        TestItemMovement();
        TestSpaceFinding();
        TestBoundaryConditions();

        Debug.Log("\n=== ALL TESTS COMPLETE ===");
    }

    [ContextMenu("Test 1: Basic Placement")]
    void TestBasicPlacement()
    {
        Debug.Log("--- Test 1: Basic Placement ---");

        InventoryGridData grid = new InventoryGridData(8, 10);
        Debug.Log($"Created {grid.GetDebugString()}");

        // Place single-slot item
        bool placed = grid.PlaceItem("item1", new GridPosition(0, 0), 1, 1);
        Debug.Log($"Place 1x1 item at (0,0): {(placed ? "SUCCESS" : "FAILED")}");

        // Place another single-slot item
        placed = grid.PlaceItem("item2", new GridPosition(1, 0), 1, 1);
        Debug.Log($"Place 1x1 item at (1,0): {(placed ? "SUCCESS" : "FAILED")}");

        // Try to place at occupied position (should fail)
        placed = grid.PlaceItem("item3", new GridPosition(0, 0), 1, 1);
        Debug.Log($"Place 1x1 item at occupied (0,0): {(placed ? "FAILED (should reject)" : "CORRECTLY REJECTED")}");

        Debug.Log($"Final state: {grid.GetDebugString()}\n");
    }

    [ContextMenu("Test 2: Multi-Slot Placement")]
    void TestMultiSlotPlacement()
    {
        Debug.Log("--- Test 2: Multi-Slot Placement ---");

        InventoryGridData grid = new InventoryGridData(8, 10);

        // Place 2x2 item
        bool placed = grid.PlaceItem("sword", new GridPosition(0, 0), 2, 2);
        Debug.Log($"Place 2x2 sword at (0,0): {(placed ? "SUCCESS" : "FAILED")}");

        // Place 1x3 item
        placed = grid.PlaceItem("bow", new GridPosition(3, 0), 1, 3);
        Debug.Log($"Place 1x3 bow at (3,0): {(placed ? "SUCCESS" : "FAILED")}");

        // Check occupancy
        string item = grid.GetItemAtPosition(0, 0);
        Debug.Log($"Item at (0,0): {item ?? "EMPTY"}");

        item = grid.GetItemAtPosition(1, 1);
        Debug.Log($"Item at (1,1): {item ?? "EMPTY"} (should be 'sword')");

        item = grid.GetItemAtPosition(3, 2);
        Debug.Log($"Item at (3,2): {item ?? "EMPTY"} (should be 'bow')");

        Debug.Log($"Final state: {grid.GetDebugString()}\n");
    }

    [ContextMenu("Test 3: Overlap Detection")]
    void TestOverlapDetection()
    {
        Debug.Log("--- Test 3: Overlap Detection ---");

        InventoryGridData grid = new InventoryGridData(8, 10);

        // Place 3x3 item
        grid.PlaceItem("armor", new GridPosition(2, 2), 3, 3);
        Debug.Log("Placed 3x3 armor at (2,2)");

        // Try to place overlapping items
        bool placed = grid.PlaceItem("helmet", new GridPosition(3, 3), 2, 2);
        Debug.Log($"Try place 2x2 helmet at (3,3) [overlaps armor]: {(placed ? "FAILED (should reject)" : "CORRECTLY REJECTED")}");

        placed = grid.PlaceItem("boots", new GridPosition(4, 1), 2, 2);
        Debug.Log($"Try place 2x2 boots at (4,1) [overlaps armor]: {(placed ? "FAILED (should reject)" : "CORRECTLY REJECTED")}");

        // Place adjacent item (should work)
        placed = grid.PlaceItem("gloves", new GridPosition(5, 2), 1, 1);
        Debug.Log($"Place 1x1 gloves at (5,2) [adjacent to armor]: {(placed ? "SUCCESS" : "FAILED")}");

        Debug.Log($"Final state: {grid.GetDebugString()}\n");
    }

    [ContextMenu("Test 4: Item Movement")]
    void TestItemMovement()
    {
        Debug.Log("--- Test 4: Item Movement ---");

        InventoryGridData grid = new InventoryGridData(8, 10);

        // Place items
        grid.PlaceItem("sword", new GridPosition(0, 0), 2, 2);
        grid.PlaceItem("potion", new GridPosition(3, 0), 1, 1);
        Debug.Log("Placed sword (2x2) at (0,0) and potion (1x1) at (3,0)");

        // Move sword
        bool moved = grid.MoveItem("sword", new GridPosition(5, 5));
        Debug.Log($"Move sword to (5,5): {(moved ? "SUCCESS" : "FAILED")}");

        // Try to move sword to invalid position
        moved = grid.MoveItem("sword", new GridPosition(3, 0)); // Where potion is
        Debug.Log($"Move sword to (3,0) [occupied by potion]: {(moved ? "FAILED (should reject)" : "CORRECTLY REJECTED")}");

        // Verify positions
        string item = grid.GetItemAtPosition(0, 0);
        Debug.Log($"Item at old sword position (0,0): {item ?? "EMPTY (correct)"}");

        item = grid.GetItemAtPosition(5, 5);
        Debug.Log($"Item at new sword position (5,5): {item ?? "EMPTY"}");

        Debug.Log($"Final state: {grid.GetDebugString()}\n");
    }

    [ContextMenu("Test 5: Space Finding")]
    void TestSpaceFinding()
    {
        Debug.Log("--- Test 5: Space Finding ---");

        InventoryGridData grid = new InventoryGridData(8, 10);

        // Fill some spaces
        grid.PlaceItem("item1", new GridPosition(0, 0), 2, 2);
        grid.PlaceItem("item2", new GridPosition(2, 0), 2, 2);
        grid.PlaceItem("item3", new GridPosition(4, 0), 2, 2);
        Debug.Log("Filled bottom row with 2x2 items");

        // Find space for 1x1
        GridPosition space = grid.FindEmptySpace(1, 1);
        Debug.Log($"Find space for 1x1: {space} (should be (6,0) or (0,2))");

        // Find space for 2x2
        space = grid.FindEmptySpace(2, 2);
        Debug.Log($"Find space for 2x2: {space} (should be (6,0) or (0,2))");

        // Find space for 3x3
        space = grid.FindEmptySpace(3, 3);
        Debug.Log($"Find space for 3x3: {space}");

        // Find closest space to (7,7)
        space = grid.FindClosestEmptySpace(new GridPosition(7, 7), 1, 1);
        Debug.Log($"Find space closest to (7,7): {space}");

        Debug.Log($"Final state: {grid.GetDebugString()}\n");
    }

    [ContextMenu("Test 6: Boundary Conditions")]
    void TestBoundaryConditions()
    {
        Debug.Log("--- Test 6: Boundary Conditions ---");

        InventoryGridData grid = new InventoryGridData(8, 10);

        // Try to place outside bounds
        bool placed = grid.PlaceItem("oob1", new GridPosition(7, 9), 2, 2);
        Debug.Log($"Place 2x2 at (7,9) [extends outside]: {(placed ? "FAILED (should reject)" : "CORRECTLY REJECTED")}");

        // Place at edge
        placed = grid.PlaceItem("edge", new GridPosition(6, 8), 2, 2);
        Debug.Log($"Place 2x2 at (6,8) [fits at edge]: {(placed ? "SUCCESS" : "FAILED")}");

        // Try to place at negative coordinates
        placed = grid.PlaceItem("negative", new GridPosition(-1, 0), 1, 1);
        Debug.Log($"Place at (-1,0): {(placed ? "FAILED (should reject)" : "CORRECTLY REJECTED")}");

        // Edge case: 0x0 item
        placed = grid.PlaceItem("invalid", new GridPosition(0, 0), 0, 0);
        Debug.Log($"Place 0x0 item: {(placed ? "FAILED (should reject)" : "CORRECTLY REJECTED")}");

        Debug.Log($"Final state: {grid.GetDebugString()}\n");
    }

    [ContextMenu("Test 7: Stress Test (Fill Grid)")]
    void TestStressTest()
    {
        Debug.Log("--- Test 7: Stress Test ---");

        InventoryGridData grid = new InventoryGridData(8, 10);

        int itemCount = 0;
        int attemptCount = 0;

        // Try to fill grid with 1x1 items
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                attemptCount++;
                if (grid.PlaceItem($"item_{itemCount}", new GridPosition(x, y), 1, 1))
                {
                    itemCount++;
                }
            }
        }

        Debug.Log($"Placed {itemCount} items in {attemptCount} attempts");
        Debug.Log($"Final state: {grid.GetDebugString()}");
        Debug.Log($"Grid integrity valid: {grid.ValidateIntegrity()}");

        // Now try to add multi-slot items
        GridPosition space = grid.FindEmptySpace(2, 2);
        Debug.Log($"Can we fit a 2x2 in full grid? {(space.IsValid ? $"Yes at {space}" : "No")}");

        Debug.Log("");
    }
}