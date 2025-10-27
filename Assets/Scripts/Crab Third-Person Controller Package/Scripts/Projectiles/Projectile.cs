using UnityEngine;
using System.Collections;

/// <summary>
/// Simplified generic projectile for physical weapons (shuriken, kunai) with optional spell triggering.
/// Much simpler than the full magic system - focuses on core projectile behavior.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Projectile Data")]
    [SerializeField] private ProjectileData projectileData;

    [Header("Physics Behavior")]
    [SerializeField] private bool useGravity = false;
    [SerializeField] private float airDrag = 0f;
    [SerializeField] private float angularDrag = 0f;
    [SerializeField] private float heightThreshold = 1.5f;
    [SerializeField] private bool constrainRotation = true;

    [Header("Homing")]
    [SerializeField] private float turningSpeed = 45f;
    [SerializeField] private float homingRange = 10f;
    [SerializeField] private LayerMask targetLayers = -1;

    // Runtime state
    private Rigidbody rb;
    private Collider col;
    private AudioSource audioSource;
    private Transform currentTarget;
    private GameObject thrower;
    private Vector3 velocity;
    private bool hasHit = false;
    private float timeAlive;

    // Homing behavior
    private bool isHoming = false;

    // Hit tracking
    private System.Collections.Generic.HashSet<Collider> hitTargets = new System.Collections.Generic.HashSet<Collider>();

    // Properties
    public ProjectileData Data => projectileData;
    public GameObject Thrower => thrower;
    public Transform Target => currentTarget;

    // Events for spell system integration
    public System.Action<Vector3, GameObject> OnSpellTrigger; // Position, Thrower

    #region Initialization

    public void Initialize(Vector3 initialVelocity, Transform target, GameObject throwerObject, ProjectileData data = null)
    {
        // Use provided data or fall back to assigned data
        if (data != null) projectileData = data;

        if (projectileData == null)
        {
            Debug.LogError("Projectile: No ProjectileData assigned!");
            Destroy(gameObject);
            return;
        }

        // Set up components
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Configure physics
        rb.useGravity = useGravity;
        rb.linearDamping = airDrag;
        rb.angularDamping = angularDrag;

        if (constrainRotation)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // Set initial velocity
        rb.linearVelocity = initialVelocity.normalized * projectileData.speed;
        velocity = rb.linearVelocity;

        // Configure collision
        col.isTrigger = true;

        // Set parameters
        currentTarget = target;
        thrower = throwerObject;

        // Set up homing
        isHoming = currentTarget != null && turningSpeed > 0f && projectileData.canHome;

        // Set up audio
        SetupAudio();

        // Set up visuals
        SetupVisuals();

        // Start lifetime countdown
        StartCoroutine(LifetimeCountdown());

        Debug.Log($"{projectileData.projectileName} initialized - Target: {(currentTarget ? currentTarget.name : "none")}");
    }

    private void SetupAudio()
    {
        if (projectileData.launchSound != null || projectileData.hitSound != null || projectileData.flightSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.volume = 0.7f;
            audioSource.pitch = Random.Range(0.9f, 1.1f);

            // Play launch sound
            if (projectileData.launchSound != null)
            {
                audioSource.PlayOneShot(projectileData.launchSound);
            }

            // Play flight sound looping
            if (projectileData.flightSound != null)
            {
                audioSource.clip = projectileData.flightSound;
                audioSource.loop = true;
                audioSource.volume = 0.3f;
                audioSource.Play();
            }
        }
    }

    private void SetupVisuals()
    {
        // Set up trail effect
        if (projectileData.trailEffect != null)
        {
            GameObject trail = Instantiate(projectileData.trailEffect, transform.position, transform.rotation);
            trail.transform.SetParent(transform);
        }

        // Set up spinning
        if (projectileData.spinSpeed > 0f)
        {
            StartCoroutine(SpinProjectile());
        }
    }

    private IEnumerator SpinProjectile()
    {
        while (!hasHit)
        {
            if (constrainRotation)
            {
                transform.Rotate(Vector3.up * projectileData.spinSpeed * Time.deltaTime, Space.World);
            }
            else
            {
                transform.Rotate(Vector3.forward * projectileData.spinSpeed * Time.deltaTime);
            }
            yield return null;
        }
    }

    #endregion

    #region Movement & Homing

    void FixedUpdate()
    {
        if (hasHit && !projectileData.canHitMultipleTargets) return;

        timeAlive += Time.fixedDeltaTime;

        // Update homing behavior
        if (isHoming && currentTarget != null)
        {
            UpdateHomingMovement();
        }
        else
        {
            rb.linearVelocity = velocity;
        }

        // Update rotation to face movement direction
        if (rb.linearVelocity.magnitude > 0.1f && !constrainRotation)
        {
            Vector3 lookDirection = rb.linearVelocity.normalized;
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    private void UpdateHomingMovement()
    {
        if (currentTarget == null)
        {
            isHoming = false;
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        if (distanceToTarget > homingRange * 2f)
        {
            isHoming = false;
            return;
        }

        // Get target position
        Vector3 targetPosition = currentTarget.position;
        var targetable = currentTarget.GetComponent<ITargetable>();
        if (targetable != null)
        {
            targetPosition = targetable.GetTargetPoint();
        }

        Vector3 targetDirection = (targetPosition - transform.position).normalized;
        Vector3 currentDirection = velocity.normalized;
        float heightDifference = targetPosition.y - transform.position.y;

        // Smart height-based homing (same as before)
        if (Mathf.Abs(heightDifference) > heightThreshold)
        {
            // Full 3D homing for significant height differences
            float maxTurnAngle = turningSpeed * Time.fixedDeltaTime;
            Vector3 newDirection = Vector3.RotateTowards(currentDirection, targetDirection, Mathf.Deg2Rad * maxTurnAngle, 0f);
            velocity = newDirection * velocity.magnitude;
        }
        else
        {
            // Horizontal-only homing for same-height targets
            Vector3 horizontalTargetDir = new Vector3(targetDirection.x, 0, targetDirection.z).normalized;
            Vector3 horizontalCurrentDir = new Vector3(currentDirection.x, 0, currentDirection.z).normalized;

            float maxTurnAngle = turningSpeed * Time.fixedDeltaTime;
            Vector3 newHorizontalDir = Vector3.RotateTowards(horizontalCurrentDir, horizontalTargetDir, Mathf.Deg2Rad * maxTurnAngle, 0f);

            velocity = new Vector3(newHorizontalDir.x * velocity.magnitude, velocity.y, newHorizontalDir.z * velocity.magnitude);
        }

        rb.linearVelocity = velocity;
    }

    #endregion

    #region Collision & Damage

    void OnTriggerEnter(Collider other)
    {
        // Don't hit the thrower
        if (thrower != null && other.transform.IsChildOf(thrower.transform))
            return;

        // Don't hit the same target twice (unless allowed)
        if (!projectileData.canHitMultipleTargets && hitTargets.Contains(other))
            return;

        // Check if it's a valid target
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            ProcessTargetHit(other, damageable);
        }
        else
        {
            ProcessEnvironmentHit(other);
        }
    }

    private void ProcessTargetHit(Collider target, IDamageable damageable)
    {
        // Apply damage using your existing system
        Vector3 damageSource = thrower != null ? thrower.transform.position : transform.position;
        bool targetSurvived = damageable.TakeDamage(projectileData.damage, damageSource);

        // Track hit target
        hitTargets.Add(target);

        // Play hit effects
        PlayHitEffects(target.transform.position);

        // Play hit sound
        if (audioSource != null && projectileData.hitSound != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(projectileData.hitSound);
        }

        Debug.Log($"{projectileData.projectileName} hit {target.name} for {projectileData.damage} damage. Target survived: {targetSurvived}");

        // Trigger spell if configured
        if (projectileData.triggersSpellOnDestruction && projectileData.triggersOnHit)
        {
            TriggerSpell(target.transform.position, "hit");
        }

        // Check if projectile should be destroyed
        if (!projectileData.canHitMultipleTargets)
        {
            hasHit = true;
            StartCoroutine(DestroyAfterSound());
        }
    }

    private void ProcessEnvironmentHit(Collider environment)
    {
        PlayHitEffects(transform.position);

        if (audioSource != null && projectileData.hitSound != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(projectileData.hitSound);
        }

        Debug.Log($"{projectileData.projectileName} hit environment: {environment.name}");

        // Trigger spell if configured
        if (projectileData.triggersSpellOnDestruction && projectileData.triggersOnEnvironmentHit)
        {
            TriggerSpell(transform.position, "environment");
        }

        hasHit = true;

        // Stick to surface or destroy based on projectile type
        if (projectileData.sticksToSurfaces)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
            // Kunai sticks to wall and stays there
        }

        StartCoroutine(DestroyAfterSound());
    }

    private void PlayHitEffects(Vector3 hitPosition)
    {
        if (projectileData.hitEffect != null)
        {
            GameObject effect = Instantiate(projectileData.hitEffect, hitPosition, transform.rotation);
            Destroy(effect, 3f);
        }
    }

    #endregion

    #region Spell Integration

    private void TriggerSpell(Vector3 position, string trigger)
    {
        if (!projectileData.triggersSpellOnDestruction || string.IsNullOrEmpty(projectileData.SpellToTrigger))
            return;

        // Fire event for spell system integration
        OnSpellTrigger?.Invoke(position, thrower);

        // You can also directly integrate with your spell system here:
        // var spellSystem = thrower?.GetComponent<SpellSystem>();
        // spellSystem?.CastSpell(projectileData.SpellToTrigger, position);

        Debug.Log($"Triggering spell '{projectileData.SpellToTrigger}' at {position} due to {trigger}");
    }

    #endregion

    #region Lifetime Management

    private IEnumerator LifetimeCountdown()
    {
        yield return new WaitForSeconds(projectileData.lifetime);

        if (!hasHit)
        {
            // Trigger spell on timeout if configured
            if (projectileData.triggersSpellOnDestruction && projectileData.triggersOnTimeout)
            {
                TriggerSpell(transform.position, "timeout");
            }

            Debug.Log($"{projectileData.projectileName} lifetime expired");
            DestroyProjectile();
        }
    }

    private IEnumerator DestroyAfterSound()
    {
        // Wait for sound to finish
        float waitTime = projectileData.hitSound != null ? projectileData.hitSound.length : 0.5f;
        yield return new WaitForSeconds(waitTime);

        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        Destroy(gameObject);
    }

    #endregion

    #region Public API

    public void SetTarget(Transform newTarget)
    {
        currentTarget = newTarget;
        isHoming = currentTarget != null && turningSpeed > 0f && projectileData.canHome;
    }

    public void DisableHoming()
    {
        isHoming = false;
        currentTarget = null;
    }

    public float GetCurrentSpeed()
    {
        return rb.linearVelocity.magnitude;
    }

    public bool IsHoming()
    {
        return isHoming && currentTarget != null;
    }

    #endregion
}