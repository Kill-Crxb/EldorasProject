using UnityEngine;

/// <summary>
/// DamageNumberManager — singleton that spawns damage number prefabs.
///
/// Setup:
///   1. Add this component to a GameObject in the game scene (e.g. "Managers").
///   2. Assign the DamageNumber prefab in the inspector.
///
/// Usage:
///   DamageNumberManager.Spawn(damage, worldPosition);
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    [SerializeField] private DamageNumber damageNumberPrefab;

    private static DamageNumberManager instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void Spawn(float damage, Vector3 worldPos)
    {
        if (instance == null || instance.damageNumberPrefab == null)
            return;

        var number = Instantiate(instance.damageNumberPrefab);
        number.Init(damage, worldPos);
    }
}
