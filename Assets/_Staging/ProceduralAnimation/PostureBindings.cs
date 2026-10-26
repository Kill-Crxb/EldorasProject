using System;
using CrabSystem;
using UnityEngine;

// Which statuses and items bring a posture with them: exhausted or injured statuses hunch, a two-hander sets a
// stance. Kept as its own table in staging so StatusDefinition and ItemDefinition stay untouched; once posture
// graduates into CrabSystem these can become fields on the definitions themselves.

[CreateAssetMenu(menuName = "CrabSystem/Animation/Posture Bindings", fileName = "PostureBindings")]
public class PostureBindings : ScriptableObject
{
    [Serializable]
    public class StatusPosture
    {
        [IdRef(IdKind.Status)] public string statusId;
        public PostureProfile posture;
    }

    [Serializable]
    public class ItemPosture
    {
        [IdRef(IdKind.Item)] public string itemId;
        public PostureProfile posture;
    }

    [SerializeField] private StatusPosture[] statuses = new StatusPosture[0];
    [SerializeField] private ItemPosture[] items = new ItemPosture[0];

    public PostureProfile ForStatus(string statusId)
    {
        foreach (var binding in statuses)
            if (binding.statusId == statusId) return binding.posture;

        return null;
    }

    public PostureProfile ForItem(string itemId)
    {
        foreach (var binding in items)
            if (binding.itemId == itemId) return binding.posture;

        return null;
    }
}
