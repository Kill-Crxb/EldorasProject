using UnityEngine;
using System.Collections;

/// <summary>
/// Projectile script for shuriken with configurable homing behavior.
/// Handles movement, tracking, collision, and damage dealing.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ShurikenProjectile : MonoBehaviour
{
    [Header("Movement")]
    //[SerializeField] private float initialSpeed = 15f;
    [SerializeField] private float turningSpeed = 45f; // Degrees per second
    [SerializeField] private float homingRange = 10f;

    [Header("Physics Behavior")]
    [SerializeField] private bool useGravity = false;
    [SerializeField] private float airDrag = 0f;
    [SerializeField] private float angularDrag = 0f;
    [SerializeField] private float heightThreshold = 1.5f; // Height difference before adjusting vertically
    [SerializeField] private bool constrainRotation = true; // Constrain to Y-axis rotation only

    [Header("Damage")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private bool canHitMultipleTargets = false;

    [Header("Visual")]
    [SerializeField] private float spinSpeed = 360f; // Degrees per second
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private GameObject trailEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip whizSound; // Sound while flying

    // Runtime state
    private Rigidbody rb;
    private Collider col;
    private AudioSource audioSource;
    private Transform currentTarget;
    private LayerMask targetLayers;
    private GameObject thrower;
    private Vector3 velocity;
    private bool hasHit = false;
    private float lifetime;
    private float timeAlive;

    // Homing behavior
    private bool isHoming = false;
    private Vector3 lastTargetPosition;

    // Hit tracking
    private System.Collections.Generic.HashSet<Collider> hitTargets = new System.Collections.Generic.HashSet<Collider>();

    #region Initialization

    public void Initialize(Vector3 initialVelocity, float turnSpeed, float range, Transform target, LayerMask layers, float maxLifetime, GameObject throwerObject)
    {
        // Set up components
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Configure physics based on inspector settings
        rb.useGravity = useGravity;
        rb.linearDamping = airDrag;
        rb.angularDamping = angularDrag;

        // Apply rotation constraints if enabled
        if (constrainRotation)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // Only spin on Y-axis
        }
        else
        {
            rb.constraints = RigidbodyConstraints.None; // Free rotation
        }

        // Set initial velocity
        rb.linearVelocity = initialVelocity;
        velocity = initialVelocity;

        // Configure collision
        col.isTrigger = true; // Use trigger for reliable hit detection

        // Set parameters
        turningSpeed = turnSpeed;
        homingRange = range;
        currentTarget = target;
        targetLayers = layers;
        lifetime = maxLifetime;
        thrower = throwerObject;

        // Set up homing if we have a target
        isHoming = currentTarget != null && turningSpeed > 0f;
        if (isHoming)
        {
            lastTargetPosition = currentTarget.position;
        }

        // Set up audio
        SetupAudio();

        // Set up visual effects
        SetupVisuals();

        // Start lifetime countdown
        StartCoroutine(LifetimeCountdown());

        Debug.Log($"Shuriken initialized - Homing: {isHoming}, Turn Speed: {turningSpeed}°/s, Target: {(currentTarget ? currentTarget.name : "none")}");
    }

    private void SetupAudio()
    {
        // Create audio source if sounds are assigned
        if (throwSound != null || hitSound != null || whizSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.volume = 0.7f;
            audioSource.pitch = Random.Range(0.9f, 1.1f); // Slight pitch variation

            // Play throw sound
            if (throwSound != null)
            {
                audioSource.PlayOneShot(throwSound);
            }

            // Play whiz sound looping
            if (whizSound != null)
            {
                audioSource.clip = whizSound;
                audioSource.loop = true;
                audioSource.volume = 0.3f;
                audioSource.Play();
            }
        }
    }

    private void SetupVisuals()
    {
        // Set up trail effect
        if (trailEffect != null)
        {
            GameObject trail = Instantiate(trailEffect, transform.position, transform.rotation);
            trail.transform.SetParent(transform);
        }
    }

    #endregion

    #region Movement & Homing

    void FixedUpdate()
    {
        if (hasHit && !canHitMultipleTargets) return;

        timeAlive += Time.fixedDeltaTime;

        // Handle spinning visual (only around Y-axis for realistic shuriken spin)
        if (spinSpeed > 0f)
        {
            transform.Rotate(Vector3.up * spinSpeed * Time.fixedDeltaTime, Space.World);
        }

        // Update homing behavior
        if (isHoming && currentTarget != null)
        {
            UpdateHomingMovement();
        }
        else
        {
            // Straight line movement - maintain exact velocity
            rb.linearVelocity = velocity;
        }

        // Update rotation to face movement direction (for visual only, not spin)
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            Vector3 lookDirection = rb.linearVelocity.normalized;
            transform.rotation = Quaternion.LookRotation(lookDirection) * Quaternion.Euler(0, transform.eulerAngles.y, 0);
        }
    }

    private void UpdateHomingMovement()
    {
        if (currentTarget == null)
        {
            isHoming = false;
            return;
        }

        // Check if target is still in range
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        if (distanceToTarget > homingRange * 2f) // Give some leeway
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

        // Calculate direction to target
        Vector3 targetDirection = (targetPosition - transform.position).normalized;

        // For flat flight: only adjust horizontal direction unless target is significantly above/below
        Vector3 currentDirection = velocity.normalized;
        float heightDifference = targetPosition.y - transform.position.y;

        // Only adjust vertical direction if target is more than the height threshold above/below
        if (Mathf.Abs(heightDifference) > heightThreshold)
        {
            // Full 3D homing when significant height difference
            // Calculate how much we can turn this frame
            float maxTurnAngle = turningSpeed * Time.fixedDeltaTime;

            // Smoothly rotate towards target
            Vector3 newDirection = Vector3.RotateTowards(currentDirection, targetDirection, Mathf.Deg2Rad * maxTurnAngle, 0f);

            // Maintain speed while changing direction
            velocity = newDirection * velocity.magnitude;
        }
        else
        {
            // Flat horizontal-only homing for targets at similar height
            Vector3 horizontalTargetDir = new Vector3(targetDirection.x, 0, targetDirection.z).normalized;
            Vector3 horizontalCurrentDir = new Vector3(currentDirection.x, 0, currentDirection.z).normalized;

            // Calculate horizontal turn angle
            float maxTurnAngle = turningSpeed * Time.fixedDeltaTime;
            Vector3 newHorizontalDir = Vector3.RotateTowards(horizontalCurrentDir, horizontalTargetDir, Mathf.Deg2Rad * maxTurnAngle, 0f);

            // Maintain original Y velocity for flat flight
            velocity = new Vector3(newHorizontalDir.x * velocity.magnitude, velocity.y, newHorizontalDir.z * velocity.magnitude);
        }

        rb.linearVelocity = velocity;

        // Update last known target position
        lastTargetPosition = targetPosition;

        // Debug visualization
        if (Application.isEditor)
        {
            Debug.DrawLine(transform.position, targetPosition, Color.green, 0.1f);
            Debug.DrawRay(transform.position, velocity.normalized * 2f, Color.red, 0.1f);
        }
    }

    #endregion

    #region Collision & Damage

    void OnTriggerEnter(Collider other)
    {
        // Don't hit the thrower
        if (thrower != null && other.transform.IsChildOf(thrower.transform))
            return;

        // Don't hit the same target twice (unless allowed)
        if (!canHitMultipleTargets && hitTargets.Contains(other))
            return;

        // Check if it's a valid target
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            ProcessHit(other, damageable);
        }
        else
        {
            // Hit environment - check if we should stick or bounce
            ProcessEnvironmentHit(other);
        }
    }

    private void ProcessHit(Collider target, IDamageable damageable)
    {
        // Calculate damage (could be modified by distance, charge time, etc.)
        float finalDamage = damage;

        // Apply damage using your existing IDamageable interface
        Vector3 damageSource = thrower != null ? thrower.transform.position : transform.position;
        bool targetSurvived = damageable.TakeDamage(finalDamage, damageSource);

        // Track hit target
        hitTargets.Add(target);

        // Play hit effects
        PlayHitEffects(target.transform.position);

        // Play hit sound
        if (audioSource != null && hitSound != null)
        {
            audioSource.Stop(); // Stop whiz sound
            audioSource.PlayOneShot(hitSound);
        }

        Debug.Log($"Shuriken hit {target.name} for {finalDamage} damage. Target survived: {targetSurvived}");

        // Destroy projectile if it can't hit multiple targets
        if (!canHitMultipleTargets)
        {
            hasHit = true;
            StartCoroutine(DestroyAfterSound());
        }
    }

    private void ProcessEnvironmentHit(Collider environment)
    {
        // Hit something that's not damageable (wall, ground, etc.)
        PlayHitEffects(transform.position);

        if (audioSource != null && hitSound != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(hitSound);
        }

        Debug.Log($"Shuriken hit environment: {environment.name}");

        // Stick to surface or destroy
        hasHit = true;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        StartCoroutine(DestroyAfterSound());
    }

    private void PlayHitEffects(Vector3 hitPosition)
    {
        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, hitPosition, transform.rotation);

            // Auto-destroy effect after a few seconds
            Destroy(effect, 3f);
        }
    }

    #endregion

    #region Lifetime Management

    private IEnumerator LifetimeCountdown()
    {
        yield return new WaitForSeconds(lifetime);

        if (!hasHit)
        {
            Debug.Log("Shuriken lifetime expired");
            DestroyProjectile();
        }
    }

    private IEnumerator DestroyAfterSound()
    {
        // Wait a moment for hit sound to play
        if (audioSource != null && hitSound != null)
        {
            yield return new WaitForSeconds(hitSound.length);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        // Stop any looping sounds
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        Destroy(gameObject);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Change the current target (useful for retargeting)
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        currentTarget = newTarget;
        isHoming = currentTarget != null && turningSpeed > 0f;
    }

    /// <summary>
    /// Disable homing behavior
    /// </summary>
    public void DisableHoming()
    {
        isHoming = false;
        currentTarget = null;
    }

    /// <summary>
    /// Get current speed
    /// </summary>
    public float GetCurrentSpeed()
    {
        return rb.linearVelocity.magnitude;
    }

    /// <summary>
    /// Check if projectile is currently homing
    /// </summary>
    public bool IsHoming()
    {
        return isHoming && currentTarget != null;
    }

    #endregion

    #region Debug

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Draw velocity vector
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, velocity.normalized * 2f);

        // Draw homing range
        if (isHoming)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, homingRange);

            // Draw line to target
            if (currentTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, currentTarget.position);
            }
        }
    }

    #endregion
}