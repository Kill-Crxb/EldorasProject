using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pooling for projectile instances, keyed by ARCHETYPE PREFAB.
///
/// Keyed by prefab rather than by ProjectileData on purpose: several data assets share one
/// archetype, so Archetype_Orb's pool serves Fireball, Iceball and every other orb-shaped
/// spell from one recycled set. What varies per cast is the runtime struct and the visual,
/// both of which are applied at Launch.
///
/// Deliberately static and self-creating — NOT a manager module. ManagerBrain is disabled
/// in the Zoo scene, so ManagerBrain.Instance?.GetManager&lt;T&gt;() returns null there and
/// anything routed through it would silently fail. This has no dependencies at all.
///
/// The pool root is an ordinary scene object, so it is destroyed on scene change; the
/// static maps are cleared on subsystem registration to match.
/// </summary>
public static class ProjectilePool
{
    private const string RootName = "[ProjectilePool]";

    private static readonly Dictionary<GameObject, Stack<GameObject>> Available =
        new Dictionary<GameObject, Stack<GameObject>>();

    private static readonly Dictionary<GameObject, GameObject> InstanceToPrefab =
        new Dictionary<GameObject, GameObject>();

    private static readonly HashSet<GameObject> Prewarmed = new HashSet<GameObject>();

    private static Transform root;

    // =========================================================================
    // Domain reset
    // =========================================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Available.Clear();
        InstanceToPrefab.Clear();
        Prewarmed.Clear();
        root = null;
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// An active instance of <paramref name="prefab"/>, recycled where possible.
    /// The caller is expected to call ProjectileBrain.Launch immediately after.
    /// </summary>
    public static GameObject Get(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[ProjectilePool] Get called with a null prefab");
            return null;
        }

        Stack<GameObject> stack = StackFor(prefab);

        while (stack.Count > 0)
        {
            GameObject recycled = stack.Pop();
            if (recycled == null) continue;

            recycled.SetActive(true);
            return recycled;
        }

        return CreateInstance(prefab);
    }

    /// <summary>Deactivate and return an instance. Called by ProjectileBrain.Release.</summary>
    public static void Release(GameObject instance)
    {
        if (instance == null) return;

        if (!InstanceToPrefab.TryGetValue(instance, out GameObject prefab))
        {
            // Not ours — a hand-placed projectile, or created before a domain reset.
            Object.Destroy(instance);
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(EnsureRoot(), false);

        StackFor(prefab).Push(instance);
    }

    /// <summary>
    /// Build instances up front so the first shot does not hitch. Runs once per prefab;
    /// repeat calls are ignored.
    /// </summary>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) return;
        if (count <= 0) return;
        if (!Prewarmed.Add(prefab)) return;

        Stack<GameObject> stack = StackFor(prefab);

        for (int i = 0; i < count; i++)
        {
            GameObject instance = CreateInstance(prefab);
            instance.SetActive(false);
            stack.Push(instance);
        }
    }

    // =========================================================================
    // Internals
    // =========================================================================

    private static GameObject CreateInstance(GameObject prefab)
    {
        GameObject instance = Object.Instantiate(prefab, EnsureRoot());
        instance.name = prefab.name;

        InstanceToPrefab[instance] = prefab;
        return instance;
    }

    private static Stack<GameObject> StackFor(GameObject prefab)
    {
        if (Available.TryGetValue(prefab, out Stack<GameObject> stack))
            return stack;

        stack = new Stack<GameObject>();
        Available[prefab] = stack;
        return stack;
    }

    private static Transform EnsureRoot()
    {
        if (root != null) return root;

        var go = new GameObject(RootName);
        root = go.transform;
        return root;
    }
}
