// NaturalWeaponDebugger.cs
// Attach this to your Bear to debug natural weapon instantiation
using UnityEngine;

public class NaturalWeaponDebugger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ControllerBrain brain;
    [SerializeField] private WeaponModule weaponModule;

    [Header("Debug Display")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logDetailedInfo = true;
    [SerializeField] private Color socketColor = Color.green;
    [SerializeField] private Color weaponColor = Color.red;
    [SerializeField] private float gizmoSize = 0.3f;

    private void OnValidate()
    {
        if (brain == null)
            brain = GetComponentInChildren<ControllerBrain>();

        if (weaponModule == null)
            weaponModule = GetComponentInChildren<WeaponModule>();
    }

    private void Start()
    {
        if (logDetailedInfo)
        {
            LogWeaponSetup();
        }
    }

    [ContextMenu("Debug: Log Weapon Setup")]
    private void LogWeaponSetup()
    {
        Debug.Log("=== NATURAL WEAPON DEBUG REPORT ===");

        if (weaponModule == null)
        {
            Debug.LogError("❌ No WeaponModule found!");
            return;
        }

        Debug.Log($"✓ WeaponModule found: {weaponModule.name}");

        // Check settings
        var useMultipleSockets = GetPrivateField<bool>(weaponModule, "useMultipleSockets");
        var preInstantiate = GetPrivateField<bool>(weaponModule, "preInstantiateNaturalWeapons");

        Debug.Log($"  - useMultipleSockets: {useMultipleSockets}");
        Debug.Log($"  - preInstantiateNaturalWeapons: {preInstantiate}");

        // Check available weapons
        var availableWeapons = GetPrivateField<WeaponData[]>(weaponModule, "availableWeapons");
        if (availableWeapons == null || availableWeapons.Length == 0)
        {
            Debug.LogWarning("❌ No weapons in availableWeapons array!");
            return;
        }

        Debug.Log($"✓ Found {availableWeapons.Length} weapons:");
        for (int i = 0; i < availableWeapons.Length; i++)
        {
            var weapon = availableWeapons[i];
            if (weapon == null)
            {
                Debug.LogWarning($"  - Weapon [{i}]: NULL");
                continue;
            }

            Debug.Log($"  - Weapon [{i}]: {weapon.weaponName}");
            Debug.Log($"    Type: {weapon.weaponType}");
            Debug.Log($"    IsNaturalWeapon: {weapon.isNaturalWeapon}");
            Debug.Log($"    PreferredSocket: {weapon.preferredSocketName}");
        }

        // Check socket configs
        var socketConfigs = GetPrivateField<WeaponSocketConfig[]>(weaponModule, "socketConfigs");
        if (socketConfigs == null || socketConfigs.Length == 0)
        {
            Debug.LogWarning("❌ No socket configs found!");
            return;
        }

        Debug.Log($"✓ Found {socketConfigs.Length} socket configs:");
        for (int i = 0; i < socketConfigs.Length; i++)
        {
            var config = socketConfigs[i];
            Debug.Log($"  - Socket [{i}]: {config.socketName}");
            Debug.Log($"    Transform: {(config.socketTransform != null ? config.socketTransform.name : "NULL")}");
            Debug.Log($"    WeaponType: {config.weaponType}");
            Debug.Log($"    IsNaturalSocket: {config.isNaturalWeaponSocket}");
        }

        // Check instantiated weapons (only in play mode)
        if (Application.isPlaying)
        {
            var naturalWeaponModels = GetPrivateField<System.Collections.Generic.Dictionary<string, GameObject>>(weaponModule, "naturalWeaponModels");
            if (naturalWeaponModels != null && naturalWeaponModels.Count > 0)
            {
                Debug.Log($"✓ Found {naturalWeaponModels.Count} instantiated natural weapons:");
                foreach (var kvp in naturalWeaponModels)
                {
                    Debug.Log($"  - {kvp.Key}: {kvp.Value.name}");
                }
            }
            else
            {
                Debug.LogWarning("❌ No natural weapons instantiated!");
            }

            var naturalWeaponHitboxes = GetPrivateField<System.Collections.Generic.Dictionary<string, SimpleWeaponHit>>(weaponModule, "naturalWeaponHitboxes");
            if (naturalWeaponHitboxes != null && naturalWeaponHitboxes.Count > 0)
            {
                Debug.Log($"✓ Found {naturalWeaponHitboxes.Count} weapon hitboxes:");
                foreach (var kvp in naturalWeaponHitboxes)
                {
                    var hitbox = kvp.Value;
                    Debug.Log($"  - {kvp.Key}: {(hitbox != null ? "Active" : "NULL")}");
                    if (hitbox != null)
                    {
                        Debug.Log($"    Can Hit: {hitbox.CanCurrentlyHit}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("❌ No weapon hitboxes found!");
            }

            // Check current weapon
            var currentWeapon = weaponModule.CurrentWeaponHitbox;
            if (currentWeapon != null)
            {
                Debug.Log($"✓ Current Weapon Hitbox: {currentWeapon.name}");
                Debug.Log($"  Can Currently Hit: {currentWeapon.CanCurrentlyHit}");
            }
            else
            {
                Debug.LogWarning("❌ No current weapon hitbox set!");
            }
        }

        Debug.Log("=== END WEAPON DEBUG REPORT ===");
    }

    [ContextMenu("Debug: Find All Sockets")]
    private void FindAllSockets()
    {
        Debug.Log("=== SEARCHING FOR SOCKET TRANSFORMS ===");

        var allTransforms = GetComponentsInChildren<Transform>(true);
        int socketCount = 0;

        foreach (var t in allTransforms)
        {
            if (t.name.ToLower().Contains("socket") ||
                t.name.ToLower().Contains("paw") ||
                t.name.ToLower().Contains("claw") ||
                t.name.ToLower().Contains("hand"))
            {
                Debug.Log($"✓ Potential socket: {GetFullPath(t)}");
                socketCount++;
            }
        }

        if (socketCount == 0)
        {
            Debug.LogWarning("❌ No potential socket transforms found! Create empty GameObjects named 'LeftPaw_Socket' and 'RightPaw_Socket'");
        }
        else
        {
            Debug.Log($"Found {socketCount} potential sockets");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        if (weaponModule == null)
            weaponModule = GetComponentInChildren<WeaponModule>();

        if (weaponModule == null) return;

        // Draw sockets
        var socketConfigs = GetPrivateField<WeaponSocketConfig[]>(weaponModule, "socketConfigs");
        if (socketConfigs != null)
        {
            foreach (var config in socketConfigs)
            {
                if (config.socketTransform != null)
                {
                    Gizmos.color = socketColor;
                    Gizmos.DrawWireSphere(config.socketTransform.position, gizmoSize);
                    Gizmos.DrawLine(config.socketTransform.position,
                                    config.socketTransform.position + config.socketTransform.forward * gizmoSize * 2);
                }
            }
        }

        // Draw instantiated weapons (play mode only)
        if (Application.isPlaying)
        {
            var naturalWeaponModels = GetPrivateField<System.Collections.Generic.Dictionary<string, GameObject>>(weaponModule, "naturalWeaponModels");
            if (naturalWeaponModels != null)
            {
                foreach (var weapon in naturalWeaponModels.Values)
                {
                    if (weapon != null)
                    {
                        Gizmos.color = weaponColor;
                        Gizmos.DrawWireCube(weapon.transform.position, Vector3.one * gizmoSize * 1.5f);
                    }
                }
            }
        }
    }

    // Utility: Get private field via reflection
    private T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
            return (T)field.GetValue(obj);

        return default(T);
    }

    // Utility: Get full hierarchy path
    private string GetFullPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}