// Advanced Movement Sounds - Audio companion for AdvancedMovementModule
using UnityEngine;

public class AdvancedMovementSounds : MonoBehaviour, IPlayerModule
{
    [Header("Jump Audio")]
    [SerializeField] private AudioClip[] jumpSounds;
    [SerializeField] private float jumpVolume = 1f;
    [SerializeField] private float jumpPitchVariation = 0.1f;

    [Header("Air Jump Audio")]
    [SerializeField] private AudioClip[] airJumpSounds;
    [SerializeField] private float airJumpVolume = 0.8f;
    [SerializeField] private float airJumpPitchVariation = 0.15f;

    [Header("Landing Audio")]
    [SerializeField] private AudioClip[] landingSounds;
    [SerializeField] private float landingVolume = 0.9f;
    [SerializeField] private float landingPitchVariation = 0.1f;
    [SerializeField] private float minLandingVelocity = 5f; // Minimum fall speed to play landing sound

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Settings")]
    [SerializeField] private bool debugAudio = false;

    // Component references
    private AdvancedMovementModule movementModule;
    private ThirdPersonController controller;
    private ControllerBrain brain;

    // State tracking for landing sounds
    private bool wasInAir = false;
    private float lastAirTime = 0f;

    // IPlayerModule implementation
    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        controller = brain.Controller;

        // Get the main movement module on same GameObject
        movementModule = GetComponent<AdvancedMovementModule>();

        if (movementModule == null)
        {
            Debug.LogWarning("AdvancedMovementSounds: No AdvancedMovementModule found on same GameObject!");
            return;
        }

        // Subscribe to movement events
        movementModule.OnJumpPerformed += PlayJumpSound;
        movementModule.OnAirJumpPerformed += PlayAirJumpSound;

        // Subscribe to feet detection for landing sounds
        if (brain != null)
        {
            brain.OnFeetEnter += HandleFeetEnter;
        }

        // Setup audio sources if not assigned
        SetupAudioSources();

        if (debugAudio)
        {
            Debug.Log("AdvancedMovementSounds initialized and subscribed to events");
        }
    }

    public void UpdateModule()
    {
        // Track air state for landing sound logic
        TrackAirState();
    }

    void SetupAudioSources()
    {
        // Create audio source if not assigned
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.playOnAwake = false;

            if (debugAudio)
            {
                Debug.Log("AdvancedMovementSounds: Created audio source on Component_AdvancedMovement");
            }
        }
    }

    void TrackAirState()
    {
        if (brain == null) return;

        bool isGrounded = brain.IsGrounded;

        if (!isGrounded)
        {
            wasInAir = true;
            lastAirTime = Time.time;
        }
    }

    #region Event Handlers

    private void PlayJumpSound()
    {
        if (!IsEnabled || jumpSounds.Length == 0 || audioSource == null) return;

        AudioClip clipToPlay = jumpSounds[Random.Range(0, jumpSounds.Length)];

        if (clipToPlay != null)
        {
            // Apply pitch variation and play with volume
            audioSource.pitch = 1f + Random.Range(-jumpPitchVariation, jumpPitchVariation);
            audioSource.PlayOneShot(clipToPlay, jumpVolume);

            if (debugAudio)
            {
                Debug.Log($"Played jump sound: {clipToPlay.name}");
            }
        }
    }

    private void PlayAirJumpSound(int airJumpNumber)
    {
        if (!IsEnabled || airJumpSounds.Length == 0 || audioSource == null) return;

        AudioClip clipToPlay = airJumpSounds[Random.Range(0, airJumpSounds.Length)];

        if (clipToPlay != null)
        {
            // Apply pitch variation and play with volume
            audioSource.pitch = 1f + Random.Range(-airJumpPitchVariation, airJumpPitchVariation);
            audioSource.PlayOneShot(clipToPlay, airJumpVolume);

            if (debugAudio)
            {
                Debug.Log($"Played air jump sound: {clipToPlay.name} (Air jump #{airJumpNumber})");
            }
        }
    }

    private void HandleFeetEnter(Collider col, FeetContactType type)
    {
        // Only play landing sounds for ground contact
        if (type != FeetContactType.Ground) return;

        // Only play if we were in the air and have been airborne for a reasonable time
        if (wasInAir && Time.time - lastAirTime > 0.1f)
        {
            PlayLandingSound();
            wasInAir = false;
        }
    }

    private void PlayLandingSound()
    {
        if (!IsEnabled || landingSounds.Length == 0 || audioSource == null) return;

        // Check if we were falling fast enough to warrant a landing sound
        if (controller != null)
        {
            Vector3 verticalVelocity = controller.GetVerticalVelocity();
            float fallSpeed = Mathf.Abs(verticalVelocity.y);

            if (fallSpeed < minLandingVelocity)
            {
                if (debugAudio)
                {
                    Debug.Log($"Landing too soft for sound: {fallSpeed:F1} < {minLandingVelocity}");
                }
                return;
            }
        }

        AudioClip clipToPlay = landingSounds[Random.Range(0, landingSounds.Length)];

        if (clipToPlay != null)
        {
            // Apply pitch variation and play with volume
            audioSource.pitch = 1f + Random.Range(-landingPitchVariation, landingPitchVariation);
            audioSource.PlayOneShot(clipToPlay, landingVolume);

            if (debugAudio)
            {
                Debug.Log($"Played landing sound: {clipToPlay.name}");
            }
        }
    }

    #endregion

    #region Public API

    // Method to manually trigger jump sound (for external systems)
    public void ForcePlayJumpSound()
    {
        PlayJumpSound();
    }

    // Method to manually trigger landing sound
    public void ForcePlayLandingSound()
    {
        PlayLandingSound();
    }

    // Method to update audio settings at runtime
    public void SetJumpVolume(float volume)
    {
        jumpVolume = Mathf.Clamp01(volume);
    }

    public void SetLandingVolume(float volume)
    {
        landingVolume = Mathf.Clamp01(volume);
    }

    // Method to enable/disable all movement sounds
    public void SetMovementSoundsEnabled(bool enabled)
    {
        if (audioSource != null)
        {
            audioSource.mute = !enabled;
        }
    }

    #endregion

    void OnDestroy()
    {
        // Unsubscribe from events to prevent null reference errors
        if (movementModule != null)
        {
            movementModule.OnJumpPerformed -= PlayJumpSound;
            movementModule.OnAirJumpPerformed -= PlayAirJumpSound;
        }

        if (brain != null)
        {
            brain.OnFeetEnter -= HandleFeetEnter;
        }
    }

    #region Debug Visualization

    void OnDrawGizmosSelected()
    {
        if (!IsEnabled || !debugAudio) return;

        // Show audio range (if using 3D audio)
        if (audioSource != null && audioSource.spatialBlend > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, audioSource.maxDistance);
        }

        // Show air state
        if (wasInAir)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, Vector3.one * 0.2f);
        }
    }

    #endregion
}