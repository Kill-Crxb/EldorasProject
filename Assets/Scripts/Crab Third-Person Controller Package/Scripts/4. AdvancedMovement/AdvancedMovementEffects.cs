// Advanced Movement Effects - Particle companion for AdvancedMovementModule
using UnityEngine;

public class AdvancedMovementEffects : MonoBehaviour, IPlayerModule
{
    [Header("Effect Locations")]
    [SerializeField] private Transform feetEffects;
    [SerializeField] private Transform backEffects;

    [Header("Jump Effects")]
    [SerializeField] private ParticleSystem jumpFeetParticles;
    [SerializeField] private ParticleSystem jumpBackParticles;
    [SerializeField] private bool playJumpFeetEffects = true;
    [SerializeField] private bool playJumpBackEffects = false;

    [Header("Air Jump Effects")]
    [SerializeField] private ParticleSystem airJumpFeetParticles;
    [SerializeField] private ParticleSystem airJumpBackParticles;
    [SerializeField] private bool playAirJumpFeetEffects = true;
    [SerializeField] private bool playAirJumpBackEffects = true;

    [Header("Dash Effects (Future)")]
    [SerializeField] private ParticleSystem dashFeetParticles;
    [SerializeField] private ParticleSystem dashBackParticles;
    [SerializeField] private bool playDashFeetEffects = true;
    [SerializeField] private bool playDashBackEffects = true;

    [Header("Landing Effects")]
    [SerializeField] private ParticleSystem landingFeetParticles;
    [SerializeField] private bool playLandingFeetEffects = true;
    [SerializeField] private float minLandingVelocity = 5f;

    // Component references
    private AdvancedMovementModule movementModule;
    private ThirdPersonController controller;
    private ControllerBrain brain;

    // State tracking for landing effects
    private bool wasInAir;
    private float lastAirTime;

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
            Debug.LogWarning("AdvancedMovementEffects: No AdvancedMovementModule found on same GameObject!");
            return;
        }

        // Setup effect transforms
        SetupEffectTransforms();

        // Subscribe to movement events
        movementModule.OnJumpPerformed += PlayJumpEffects;
        movementModule.OnAirJumpPerformed += PlayAirJumpEffects;

        // Subscribe to feet detection for landing effects
        if (brain != null)
        {
            brain.OnFeetEnter += HandleFeetEnter;
        }
    }

    public void UpdateModule()
    {
        TrackAirState();
    }

    void SetupEffectTransforms()
    {
        // Find or create FeetEffects transform
        if (feetEffects == null)
        {
            Transform existingFeet = transform.Find("FeetEffects");
            if (existingFeet != null)
            {
                feetEffects = existingFeet;
            }
            else
            {
                GameObject feetEffectsObj = new GameObject("FeetEffects");
                feetEffectsObj.transform.SetParent(transform);
                feetEffectsObj.transform.localPosition = Vector3.zero;
                feetEffects = feetEffectsObj.transform;
            }
        }

        // Find or create BackEffects transform
        if (backEffects == null)
        {
            Transform existingBack = transform.Find("BackEffects");
            if (existingBack != null)
            {
                backEffects = existingBack;
            }
            else
            {
                GameObject backEffectsObj = new GameObject("BackEffects");
                backEffectsObj.transform.SetParent(transform);
                backEffectsObj.transform.localPosition = Vector3.zero;
                backEffects = backEffectsObj.transform;
            }
        }
    }

    void TrackAirState()
    {
        if (brain == null) return;

        if (!brain.IsGrounded)
        {
            wasInAir = true;
            lastAirTime = Time.time;
        }
    }

    #region Event Handlers

    private void PlayJumpEffects()
    {
        if (!IsEnabled) return;

        if (playJumpFeetEffects && jumpFeetParticles != null)
        {
            PlayParticleEffect(jumpFeetParticles, feetEffects);
        }

        if (playJumpBackEffects && jumpBackParticles != null)
        {
            PlayParticleEffect(jumpBackParticles, backEffects);
        }
    }

    private void PlayAirJumpEffects(int airJumpNumber)
    {
        if (!IsEnabled) return;

        if (playAirJumpFeetEffects && airJumpFeetParticles != null)
        {
            PlayParticleEffect(airJumpFeetParticles, feetEffects);
        }

        if (playAirJumpBackEffects && airJumpBackParticles != null)
        {
            PlayParticleEffect(airJumpBackParticles, backEffects);
        }
    }

    private void HandleFeetEnter(Collider col, FeetContactType type)
    {
        if (type != FeetContactType.Ground) return;

        // Only play if we were in the air for a reasonable time
        if (wasInAir && Time.time - lastAirTime > 0.1f)
        {
            PlayLandingEffects();
            wasInAir = false;
        }
    }

    private void PlayLandingEffects()
    {
        if (!IsEnabled || !playLandingFeetEffects || landingFeetParticles == null) return;

        // Check if we were falling fast enough to warrant landing effects
        if (controller != null)
        {
            Vector3 verticalVelocity = controller.GetVerticalVelocity();
            float fallSpeed = Mathf.Abs(verticalVelocity.y);

            if (fallSpeed < minLandingVelocity) return;
        }

        PlayParticleEffect(landingFeetParticles, feetEffects);
    }

    #endregion

    #region Particle System Management

    private void PlayParticleEffect(ParticleSystem particles, Transform effectTransform)
    {
        if (particles == null || effectTransform == null) return;

        // Create instance name based on particle system name
        string instanceName = $"{particles.name}_Instance";

        // Look for existing instance as child of effect transform
        Transform existingInstance = effectTransform.Find(instanceName);
        ParticleSystem instance;

        if (existingInstance != null)
        {
            instance = existingInstance.GetComponent<ParticleSystem>();
        }
        else
        {
            // Create new instance as child of effect transform
            instance = Instantiate(particles, effectTransform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.name = instanceName;
        }

        if (instance != null)
        {
            instance.Play();
        }
    }

    public void StopAllEffects()
    {
        StopParticleInstancesInTransform(feetEffects);
        StopParticleInstancesInTransform(backEffects);
    }

    private void StopParticleInstancesInTransform(Transform effectTransform)
    {
        if (effectTransform == null) return;

        ParticleSystem[] particles = effectTransform.GetComponentsInChildren<ParticleSystem>();
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].isPlaying)
            {
                particles[i].Stop();
            }
        }
    }

    #endregion

    #region Public API

    public void ForcePlayJumpEffects()
    {
        PlayJumpEffects();
    }

    public void ForcePlayAirJumpEffects(int airJumpNumber = 1)
    {
        PlayAirJumpEffects(airJumpNumber);
    }

    public void ForcePlayLandingEffects()
    {
        PlayLandingEffects();
    }

    public void PlayDashEffects()
    {
        if (!IsEnabled) return;

        if (playDashFeetEffects && dashFeetParticles != null)
        {
            PlayParticleEffect(dashFeetParticles, feetEffects);
        }

        if (playDashBackEffects && dashBackParticles != null)
        {
            PlayParticleEffect(dashBackParticles, backEffects);
        }
    }

    public void SetJumpEffectsEnabled(bool enabled)
    {
        playJumpFeetEffects = enabled;
        playJumpBackEffects = enabled;
    }

    public void SetAirJumpEffectsEnabled(bool enabled)
    {
        playAirJumpFeetEffects = enabled;
        playAirJumpBackEffects = enabled;
    }

    public void SetLandingEffectsEnabled(bool enabled)
    {
        playLandingFeetEffects = enabled;
    }

    public void SetLandingVelocityThreshold(float threshold)
    {
        minLandingVelocity = threshold;
    }

    #endregion

    void OnDestroy()
    {
        // Unsubscribe from events to prevent null reference errors
        if (movementModule != null)
        {
            movementModule.OnJumpPerformed -= PlayJumpEffects;
            movementModule.OnAirJumpPerformed -= PlayAirJumpEffects;
        }

        if (brain != null)
        {
            brain.OnFeetEnter -= HandleFeetEnter;
        }
    }
}