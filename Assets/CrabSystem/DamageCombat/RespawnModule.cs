using UnityEngine;

// Player death and respawn (decided 13 Oct, for now): after respawnDelay the player comes back where they entered
// the scene — the spawn point PlayerSpawner placed them at — with every pool full and their gear kept (nothing
// drops). A character saved dead loads the same way instead of standing at 0 health (Known Issues B26).
// Player-only: an NPC brain declines it, so NPCs stay dead until they have a respawn rule of their own.
public class RespawnModule : MonoBehaviour, IBrainModule
{
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Seconds from death to respawn.")]
    [SerializeField] private float respawnDelay = 3f;

    private ControllerBrain brain;
    private DamageSystem damage;
    private ResourceSystem resources;
    private Vector3 spawnPosition;
    private float respawnAt = -1f;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public bool PlayerOnly => true;

    // Initialize runs in the brain's Awake, right after PlayerSpawner instantiated the player at the spawn point.
    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        spawnPosition = brain.EntityRoot.position;
    }

    public void LateInitialize()
    {
        damage = brain.Damage;
        resources = brain.GetModule<ResourceSystem>();
        if (damage == null || resources == null)
        {
            Debug.LogError($"[RespawnModule] {brain.EntityName} needs a DamageSystem and a ResourceSystem.", this);
            isEnabled = false;
            return;
        }

        damage.OnDeath += HandleDeath;
        brain.OnLoaded += HandleLoaded;
    }

    private void OnDestroy()
    {
        if (damage != null) damage.OnDeath -= HandleDeath;
        if (brain != null) brain.OnLoaded -= HandleLoaded;
    }

    public void UpdateModule()
    {
        if (respawnAt < 0f || Time.time < respawnAt) return;
        respawnAt = -1f;
        Respawn();
    }

    private void HandleDeath() => respawnAt = Time.time + respawnDelay;

    private void HandleLoaded()
    {
        if (resources.GetCurrentHealth() > 0f) return;
        Respawn();
    }

    private void Respawn()
    {
        brain.Movement?.Locomotion?.TeleportTo(spawnPosition);
        resources.RefillAll();
        damage.Revive();
    }
}
