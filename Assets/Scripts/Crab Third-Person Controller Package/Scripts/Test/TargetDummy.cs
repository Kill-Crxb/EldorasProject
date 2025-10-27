// Target Dummy - For testing combat system
using UnityEngine;
using System.Collections;

public class TargetDummy : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private bool autoRespawn = true;
    [SerializeField] private float respawnDelay = 3f;

    [Header("Animation Parameters")]
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string deathTrigger = "Death";
    [SerializeField] private string isAliveBool = "IsAlive";

    [Header("Visual Feedback")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Transform damageTextSpawnPoint;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color criticalColor = Color.yellow;
    [SerializeField] private float colorFlashDuration = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] hitSounds;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip respawnSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField] private ParticleSystem respawnEffect;

    [Header("Debug")]
   // [SerializeField] private bool debugDamage = true;
    [SerializeField] private bool showHealthBar = true;

    // Component references
    private Animator animator;
    private AudioSource audioSource;
    private Renderer[] renderers;
    private Collider targetCollider;

    // State tracking
    private bool isAlive = true;
    private bool isFlashing = false;
    private Material[] originalMaterials;
    private Color[] originalColors;

    // Events
    public System.Action<float> OnDamageTaken;
    public System.Action OnTargetKilled;
    public System.Action OnTargetRespawned;
    public System.Action<float, bool> OnDamageDealt; // damage, isCritical

    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => currentHealth / maxHealth;
    public bool IsAlive => isAlive;

    void Start()
    {
        // Get component references
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        targetCollider = GetComponent<Collider>();

        // Get all renderers for color flashing
        renderers = GetComponentsInChildren<Renderer>();

        // Store original materials and colors
        StoreOriginalMaterials();

        // Setup damage text spawn point
        if (damageTextSpawnPoint == null)
        {
            var spawnPoint = new GameObject("DamageTextSpawn");
            spawnPoint.transform.SetParent(transform);
            spawnPoint.transform.localPosition = Vector3.up * 2f;
            damageTextSpawnPoint = spawnPoint.transform;
        }

        // Create audio source if needed
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.playOnAwake = false;
        }

        // Initialize health
        currentHealth = maxHealth;
    }

    void StoreOriginalMaterials()
    {
        if (renderers == null || renderers.Length == 0) return;

        var materialsList = new System.Collections.Generic.List<Material>();
        var colorsList = new System.Collections.Generic.List<Color>();

        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.materials)
            {
                materialsList.Add(material);

                // Try different color properties
                if (material.HasProperty("_Color"))
                    colorsList.Add(material.color);
                else if (material.HasProperty("_BaseColor"))
                    colorsList.Add(material.GetColor("_BaseColor"));
                else
                    colorsList.Add(Color.white);
            }
        }

        originalMaterials = materialsList.ToArray();
        originalColors = colorsList.ToArray();
    }

    #region IDamageable Implementation

    public bool TakeDamage(float damage, Vector3 source = default)
    {
        if (!isAlive) return false;

        // Determine if this is a critical hit (simple random for testing)
        bool isCritical = Random.Range(0f, 1f) < 0.15f; // 15% crit chance

        if (isCritical)
        {
            damage *= 1.5f; // Critical hit multiplier
        }

        // Apply damage
        currentHealth = Mathf.Max(0, currentHealth - damage);

        // Fire events
        OnDamageTaken?.Invoke(damage);
        OnDamageDealt?.Invoke(damage, isCritical);

        // Visual and audio feedback
        PlayHitEffects(damage, isCritical, source);

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
            return false; // Target died
        }
        else
        {
            // Play hit animation
            if (animator != null)
            {
                animator.SetTrigger(hitTrigger);
            }
        }

        return true; // Target survived
    }

    public void Heal(float amount)
    {
        if (!isAlive) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    #endregion

    #region Effects and Feedback

    void PlayHitEffects(float damage, bool isCritical, Vector3 source)
    {
        // Play hit sound
        if (hitSounds.Length > 0 && audioSource != null)
        {
            AudioClip hitSound = hitSounds[Random.Range(0, hitSounds.Length)];
            audioSource.PlayOneShot(hitSound);
        }

        // Play hit particle effect - FIXED VERSION
        if (hitEffect != null && damageTextSpawnPoint != null)
        {
            // If hitEffect is a prefab, instantiate it
            if (hitEffect.gameObject.scene.name == null) // This means it's a prefab
            {
                GameObject effectInstance = Instantiate(hitEffect.gameObject, damageTextSpawnPoint.position, Quaternion.identity);
                ParticleSystem effectParticles = effectInstance.GetComponent<ParticleSystem>();

                if (effectParticles != null)
                {
                    effectParticles.Play();

                    // Auto-destroy after the effect finishes
                    float destroyTime = effectParticles.main.duration + effectParticles.main.startLifetime.constantMax;
                    Destroy(effectInstance, destroyTime);
                }
            }
            else
            {
                // If it's already in the scene, just move and play it
                hitEffect.transform.position = damageTextSpawnPoint.position;
                hitEffect.Play();
            }
        }

        // Color flash
        Color flashColor = isCritical ? criticalColor : hitColor;
        StartCoroutine(FlashColor(flashColor));

        // Damage text
        ShowDamageText(damage, isCritical);
    }

    void ShowDamageText(float damage, bool isCritical)
    {
        if (damageTextPrefab == null || damageTextSpawnPoint == null) return;

        GameObject damageTextObj = Instantiate(damageTextPrefab, damageTextSpawnPoint.position, Quaternion.identity);

        // Try to set the damage text
        var textComponent = damageTextObj.GetComponent<UnityEngine.UI.Text>();
        if (textComponent == null)
            textComponent = damageTextObj.GetComponentInChildren<UnityEngine.UI.Text>();

        if (textComponent != null)
        {
            string damageText = isCritical ? $"{damage:F0}!" : $"{damage:F0}";
            textComponent.text = damageText;
            textComponent.color = isCritical ? criticalColor : Color.white;
        }

        // Auto-destroy after a few seconds
        Destroy(damageTextObj, 2f);
    }

    System.Collections.IEnumerator FlashColor(Color flashColor)
    {
        if (isFlashing || renderers == null) yield break;

        isFlashing = true;

        // Flash to hit color
        SetAllColors(flashColor);

        yield return new WaitForSeconds(colorFlashDuration);

        // Return to original colors
        RestoreOriginalColors();

        isFlashing = false;
    }

    void SetAllColors(Color color)
    {
        int materialIndex = 0;
        foreach (var renderer in renderers)
        {
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                var material = renderer.materials[i];
                if (material.HasProperty("_Color"))
                    material.color = color;
                else if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);

                materialIndex++;
            }
        }
    }

    void RestoreOriginalColors()
    {
        if (originalColors == null) return;

        int materialIndex = 0;
        foreach (var renderer in renderers)
        {
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                if (materialIndex < originalColors.Length)
                {
                    var material = renderer.materials[i];
                    if (material.HasProperty("_Color"))
                        material.color = originalColors[materialIndex];
                    else if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", originalColors[materialIndex]);
                }
                materialIndex++;
            }
        }
    }

    #endregion

    #region Death and Respawn

    void Die()
    {
        if (!isAlive) return;

        isAlive = false;

        // Stop any color flashing
        StopAllCoroutines();
        RestoreOriginalColors();

        // Play death animation
        if (animator != null)
        {
            animator.SetTrigger(deathTrigger);
            animator.SetBool(isAliveBool, false);
        }

        // Play death sound
        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        // Play death effect - FIXED VERSION
        if (deathEffect != null)
        {
            Vector3 deathPosition = damageTextSpawnPoint != null ? damageTextSpawnPoint.position : transform.position;

            // If deathEffect is a prefab, instantiate it
            if (deathEffect.gameObject.scene.name == null) // This means it's a prefab
            {
                GameObject effectInstance = Instantiate(deathEffect.gameObject, deathPosition, Quaternion.identity);
                ParticleSystem effectParticles = effectInstance.GetComponent<ParticleSystem>();

                if (effectParticles != null)
                {
                    effectParticles.Play();

                    // Auto-destroy after the effect finishes
                    float destroyTime = effectParticles.main.duration + effectParticles.main.startLifetime.constantMax;
                    Destroy(effectInstance, destroyTime);
                }
            }
            else
            {
                // If it's already in the scene, just move and play it
                deathEffect.transform.position = deathPosition;
                deathEffect.Play();
            }
        }

        // Disable collider
        if (targetCollider != null)
        {
            targetCollider.enabled = false;
        }

        OnTargetKilled?.Invoke();

        // Auto-respawn if enabled
        if (autoRespawn)
        {
            StartCoroutine(RespawnAfterDelay());
        }
    }

    System.Collections.IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    public void Respawn()
    {
        // Reset health
        currentHealth = maxHealth;
        isAlive = true;

        // Re-enable collider
        if (targetCollider != null)
        {
            targetCollider.enabled = true;
        }

        // Reset animation
        if (animator != null)
        {
            animator.SetBool(isAliveBool, true);
        }

        // Play respawn sound
        if (respawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(respawnSound);
        }

        // Play respawn effect
        if (respawnEffect != null)
        {
            respawnEffect.Play();
        }

        OnTargetRespawned?.Invoke();
    }

    #endregion

    #region Public API

    public void SetHealth(float newHealth)
    {
        currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);

        if (currentHealth <= 0 && isAlive)
        {
            Die();
        }
        else if (currentHealth > 0 && !isAlive)
        {
            Respawn();
        }
    }

    public void ResetTarget()
    {
        if (isAlive)
        {
            currentHealth = maxHealth;
        }
        else
        {
            Respawn();
        }
    }

    public void SetAutoRespawn(bool enable)
    {
        autoRespawn = enable;
    }

    #endregion

    #region Debug UI

    void OnGUI()
    {
        if (!showHealthBar) return;

        // Calculate screen position
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 3f);

        if (screenPos.z > 0) // Only show if in front of camera
        {
            // Health bar background
            Rect healthBarBG = new Rect(screenPos.x - 50, Screen.height - screenPos.y - 10, 100, 20);
            GUI.color = Color.black;
            GUI.DrawTexture(healthBarBG, Texture2D.whiteTexture);

            // Health bar fill
            float healthPercent = HealthPercentage;
            Rect healthBarFill = new Rect(screenPos.x - 48, Screen.height - screenPos.y - 8, 96 * healthPercent, 16);
            GUI.color = Color.Lerp(Color.red, Color.green, healthPercent);
            GUI.DrawTexture(healthBarFill, Texture2D.whiteTexture);

            // Health text
            GUI.color = Color.white;
            GUI.Label(new Rect(screenPos.x - 30, Screen.height - screenPos.y + 15, 60, 20),
                     $"{currentHealth:F0}/{maxHealth:F0}");

            GUI.color = Color.white; // Reset color
        }
    }

    void OnDrawGizmosSelected()
    {
        // Show damage text spawn point
        if (damageTextSpawnPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(damageTextSpawnPoint.position, 0.2f);
        }

        // Show hit range (approximate)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 1.5f);
    }

    #endregion
}