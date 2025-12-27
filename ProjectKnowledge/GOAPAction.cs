using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// GOAP Action - Base class for Fate System combat actions
/// 
/// Fate System combines Dark Souls-style RNG with branching combos:
/// - Single action can have multiple possible outcomes (branches)
/// - Each branch has probability and move sequence
/// - Moves can be skipped for variety (e.g. [0, 2, 4] = skip moves 1 and 3)
/// 
/// Example:
/// Bear Claw Attack:
/// - Branch 1 (20%): Single quick swipe [move 0]
/// - Branch 2 (40%): Double swipe combo [moves 0, 1]
/// - Branch 3 (30%): Triple swipe with delay [moves 0, 2, 4] (skips 1 and 3)
/// - Branch 4 (10%): Full rage combo [all 5 moves]
/// 
/// Architecture:
/// - GOAPAction is a ScriptableObject (data-driven, reusable)
/// - References a Move Library (ability definitions)
/// - Defines branches with probabilities and move sequences
/// - FateSystem executes the selected branch
/// </summary>
[CreateAssetMenu(fileName = "New GOAP Action", menuName = "AI/GOAP/Action (Fate)")]
public class GOAPAction : ScriptableObject
{
    [Header("Action Settings")]
    [Tooltip("Display name for debugging")]
    public string actionName = "Unnamed Action";

    [Tooltip("Description of this action")]
    [TextArea(2, 4)]
    public string description;

    [Header("Move Library")]
    [Tooltip("Pool of moves this action can use")]
    public List<AbilityDefinition> moveLibrary = new List<AbilityDefinition>();

    [Header("Branches (Fate System)")]
    [Tooltip("Possible execution paths - probabilities should sum to ~100")]
    public List<FateBranch> branches = new List<FateBranch>();

    [Header("Execution Settings")]
    [Tooltip("Delay between moves in a combo (seconds)")]
    public float moveDelay = 0.3f;

    [Tooltip("Can this action be interrupted?")]
    public bool canBeInterrupted = false;

    #region Validation

    /// <summary>
    /// Validate that this action is properly configured
    /// </summary>
    public bool Validate()
    {
        if (branches.Count == 0)
        {
            Debug.LogError($"[GOAPAction] {actionName} has no branches!", this);
            return false;
        }

        if (moveLibrary.Count == 0)
        {
            Debug.LogError($"[GOAPAction] {actionName} has no moves in library!", this);
            return false;
        }

        // Check probability sum
        float totalProbability = 0f;
        foreach (var branch in branches)
        {
            totalProbability += branch.probability;
        }

        if (Mathf.Abs(totalProbability - 100f) > 1f)
        {
            Debug.LogWarning($"[GOAPAction] {actionName} branch probabilities sum to {totalProbability:F1}% (should be 100%)", this);
        }

        // Validate each branch
        foreach (var branch in branches)
        {
            if (!branch.Validate(moveLibrary.Count))
            {
                Debug.LogError($"[GOAPAction] {actionName} has invalid branch: {branch.branchName}", this);
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Branch Selection (Fate System)

    /// <summary>
    /// Roll the dice and select a branch based on probabilities
    /// This is the "Fate" in Fate System - RNG determines outcome
    /// </summary>
    public FateBranch SelectBranch()
    {
        if (branches.Count == 0)
            return null;

        if (branches.Count == 1)
            return branches[0];

        // Roll 0-100
        float roll = Random.Range(0f, 100f);

        float cumulative = 0f;
        foreach (var branch in branches)
        {
            cumulative += branch.probability;
            if (roll <= cumulative)
            {
                return branch;
            }
        }

        // Fallback to first branch (shouldn't reach here if probabilities sum to 100)
        return branches[0];
    }

    /// <summary>
    /// Get a specific branch by index (for testing/debugging)
    /// </summary>
    public FateBranch GetBranch(int index)
    {
        if (index >= 0 && index < branches.Count)
            return branches[index];

        return null;
    }

    #endregion

    #region Debug

    /// <summary>
    /// Get debug string showing all branches and probabilities
    /// </summary>
    public string GetDebugInfo()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Action: {actionName}");
        sb.AppendLine($"Moves: {moveLibrary.Count}");
        sb.AppendLine($"Branches: {branches.Count}");

        foreach (var branch in branches)
        {
            sb.AppendLine($"  - {branch.branchName}: {branch.probability:F1}% [{string.Join(", ", branch.moveSequence)}]");
        }

        return sb.ToString();
    }

    #endregion
}

/// <summary>
/// Fate Branch - A single execution path within a GOAPAction
/// Contains probability and move sequence
/// </summary>
[System.Serializable]
public class FateBranch
{
    [Tooltip("Name for debugging (e.g. 'Quick Strike', 'Full Combo')")]
    public string branchName = "Branch";

    [Tooltip("Probability of this branch (0-100)")]
    [Range(0f, 100f)]
    public float probability = 25f;

    [Tooltip("Sequence of move indices from the move library (can skip moves)")]
    public List<int> moveSequence = new List<int>();

    [Tooltip("Description of this branch")]
    public string description;

    /// <summary>
    /// Validate that this branch is properly configured
    /// </summary>
    public bool Validate(int moveLibrarySize)
    {
        if (moveSequence.Count == 0)
        {
            Debug.LogError($"[FateBranch] {branchName} has empty move sequence!");
            return false;
        }

        // Check all indices are valid
        foreach (int index in moveSequence)
        {
            if (index < 0 || index >= moveLibrarySize)
            {
                Debug.LogError($"[FateBranch] {branchName} has invalid move index {index} (library size: {moveLibrarySize})");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Get the abilities for this branch from the move library
    /// </summary>
    public List<AbilityDefinition> GetMoves(List<AbilityDefinition> moveLibrary)
    {
        List<AbilityDefinition> moves = new List<AbilityDefinition>();

        foreach (int index in moveSequence)
        {
            if (index >= 0 && index < moveLibrary.Count)
            {
                moves.Add(moveLibrary[index]);
            }
        }

        return moves;
    }
}