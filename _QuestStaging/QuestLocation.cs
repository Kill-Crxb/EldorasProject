using UnityEngine;

/// <summary>
/// A place a Reach objective points at. Raises a Reach event when a player walks in;
/// QuestSystem ignores it unless an active stage wants this locationId.
/// Needs a trigger collider on the same object.
/// </summary>
[RequireComponent(typeof(Collider))]
public class QuestLocation : MonoBehaviour
{
    [SerializeField] private string locationId;

    public string LocationId => locationId;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var brain = other.GetComponentInParent<ControllerBrain>();
        if (brain == null || !brain.IsPlayer) return;

        GameEvents.RaiseGameplayEvent(new GameplayEvent(ObjectiveType.Reach, brain, locationId));
    }
}
