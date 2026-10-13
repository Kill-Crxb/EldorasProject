#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// Editor-only. Draws every strike in the Scene view: the cone at the bottom and the top of its height band,
// out to its reach (the attacker's hurtbox edge plus the weapon's reach plus the strike's bonus). Green when
// it landed on someone, yellow when it didn't. Spawns itself when play starts, like MoveClockTrace. Turn on
// Gizmos in the Game view to see it there too.
public class StrikeTrace : MonoBehaviour
{
    [SerializeField] private float rescanInterval = 1f;
    [SerializeField] private float showFor = 1f;
    [SerializeField] private int segments = 16;

    private readonly HashSet<AbilitySystem> tracked = new();
    private float nextScan;

    private void Awake()
    {
        if (FindObjectsByType<StrikeTrace>().Length > 1) Destroy(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindAnyObjectByType<StrikeTrace>() != null) return;

        GameObject host = new GameObject("[StrikeTrace]");
        DontDestroyOnLoad(host);
        host.AddComponent<StrikeTrace>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + rescanInterval;

        foreach (AbilitySystem abilities in FindObjectsByType<AbilitySystem>())
        {
            if (abilities.Strikes == null || !tracked.Add(abilities)) continue;
            abilities.Strikes.OnStrike += Draw;
        }
    }

    private void Draw(StrikeArea area)
    {
        if (this == null) return;

        Color color = area.hits > 0 ? Color.green : Color.yellow;
        DrawFan(area, area.bottom, color);
        DrawFan(area, area.top, color);
    }

    private void DrawFan(StrikeArea area, float height, Color color)
    {
        Vector3 centre = new Vector3(area.centre.x, height, area.centre.z);
        float half = area.arc * 0.5f;
        Vector3 previous = centre;

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-half, half, (float)i / segments);
            Vector3 point = centre + Quaternion.Euler(0f, angle, 0f) * area.forward * area.radius;
            Debug.DrawLine(previous, point, color, showFor);
            previous = point;
        }

        Debug.DrawLine(previous, centre, color, showFor);
    }
}
#endif
