using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers which one-shot dialogue options this character has already taken, so they
/// stop being offered. Lives on the player and saves as dialogue.json — the memory belongs
/// to the character, not to the NPC, so a second character still gets the first-time reward.
/// </summary>
public class DialogueMemory : MonoBehaviour, IBrainModule, ISaveable
{
    [Header("Module")]
    [SerializeField] private bool isEnabled = true;

    [Header("Debug")]
    [Tooltip("Read-only view of what this character has used up. Populated at runtime.")]
    [SerializeField] private List<string> usedOptions = new List<string>();

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    private readonly HashSet<string> used = new HashSet<string>();

    public void Initialize(ControllerBrain brain) { }

    public void UpdateModule() { }

    public bool HasUsed(string key) => !string.IsNullOrEmpty(key) && used.Contains(key);

    public void MarkUsed(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!used.Add(key)) return;

        usedOptions.Add(key);
    }

    /// <summary>Wipes a single memory. Handy for a quest reset or a debug button.</summary>
    public void Forget(string key)
    {
        if (!used.Remove(key)) return;

        usedOptions.Remove(key);
    }

    #region ISaveable

    public string GetSaveId() => "dialogue";

    public int GetSaveVersion() => 1;

    public string GetSaveData()
    {
        return JsonUtility.ToJson(new DialogueMemorySaveData { usedOptions = new List<string>(used) });
    }

    public void LoadSaveData(string json)
    {
        used.Clear();
        usedOptions.Clear();

        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<DialogueMemorySaveData>(json);
        if (data == null || data.usedOptions == null) return;

        foreach (string key in data.usedOptions)
        {
            if (string.IsNullOrEmpty(key)) continue;
            if (used.Add(key)) usedOptions.Add(key);
        }
    }

    #endregion
}

[System.Serializable]
public class DialogueMemorySaveData
{
    public List<string> usedOptions = new List<string>();
}
