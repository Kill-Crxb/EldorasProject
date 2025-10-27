using UnityEngine;

/// <summary>
/// Simple projectile data for physical weapons like shuriken and kunai.
/// Can optionally trigger spells on destruction for enhanced versions.
/// </summary>
[CreateAssetMenu(fileName = "New Projectile", menuName = "Combat/Projectile")]
public class ProjectileData : ScriptableObject
{
    [Header("Basic Properties")]
    public string projectileName = "Projectile";
    public float damage = 25f;
    public float speed = 15f;
    public float lifetime = 5f;

    [Header("Behavior")]
    public bool canHome = true;
    public bool canHitMultipleTargets = false;
    public bool sticksToSurfaces = true; // Kunai stick to walls, fireballs don't

    [Header("Visual & Audio")]
    public GameObject hitEffect;
    public GameObject trailEffect;
    public float spinSpeed = 360f;
    public AudioClip launchSound;
    public AudioClip flightSound;
    public AudioClip hitSound;

    [Header("Spell Integration (Optional)")]
    public bool triggersSpellOnDestruction = false;
    [SerializeField] private string spellToTrigger = ""; // Name/ID of spell in your spell system
    public bool triggersOnHit = true;
    public bool triggersOnTimeout = false;
    public bool triggersOnEnvironmentHit = false;

    [Header("Costs")]
    public float staminaCost = 10f;
    public float cooldownTime = 0.5f;

    // Public getter for spell system integration
    public string SpellToTrigger => spellToTrigger;
}