// Melee Effects Companion - Updated for flexible hierarchy search
using UnityEngine;
using System.Collections.Generic;

public class MeleeEffects : MonoBehaviour, IPlayerModule
{
    [Header("Effect Transforms")]
    [SerializeField] private Transform weaponEffectsTransform;
    [SerializeField] private Transform impactEffectsTransform;
    [SerializeField] private Transform blockEffectsTransform;

    [Header("Attack Effects")]
    [SerializeField] private ParticleSystem lightAttackEffect;
    [SerializeField] private ParticleSystem heavyAttackEffect;
    [SerializeField] private ParticleSystem criticalHitEffect;

    [Header("Block Effects")]
    [SerializeField] private ParticleSystem blockEffect;
    [SerializeField] private ParticleSystem perfectBlockEffect;

    [Header("Combo Effects")]
    [SerializeField] private ParticleSystem[] comboEffects = new ParticleSystem[3];
    [SerializeField] private bool scaleBrightnessByCombo = true;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip[] lightAttackSounds;
    [SerializeField] private AudioClip[] heavyAttackSounds;
    [SerializeField] private AudioClip[] blockSounds;
    [SerializeField] private AudioClip comboFinisherSound;
    [SerializeField] private AudioClip heavyChargeSound;

    [Header("Screen Effects")]
    [SerializeField] private bool enableScreenShake = true;
    [SerializeField] private float lightAttackShake = 0.1f;
    [SerializeField] private float heavyAttackShake = 0.3f;
    [SerializeField] private float blockShake = 0.05f;

    [Header("Timing")]
    [SerializeField] private float effectCleanupDelay = 3f;

    [Header("Debug")]
    [SerializeField] private bool debugEffects = false;

    // Component references
    private MeleeModule meleeModule;
    private ControllerBrain brain;
    private AudioSource audioSource;

    // Effect management
    private Dictionary<string, GameObject> activeEffectInstances = new Dictionary<string, GameObject>();
    private AudioSource chargeAudioSource;

    // Screen shake (if available)
    private SimpleThirdPersonCamera cameraController;

    // Track last combo for effects
    private int lastComboCount = 0;

    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Search parent first (Component_Melee), then children
        meleeModule = GetComponentInParent<MeleeModule>();
        if (meleeModule == null)
        {
            meleeModule = GetComponentInChildren<MeleeModule>();
        }

        if (meleeModule == null)
        {
            Debug.LogError("[MeleeEffects] No MeleeModule found in parent or children!");
            return;
        }

        if (debugEffects)
        {
            Debug.Log($"[MeleeEffects] Found MeleeModule on: {meleeModule.gameObject.name}");
        }

        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }

        // Create dedicated charge audio source
        GameObject chargeAudioObj = new GameObject("ChargeAudioSource");
        chargeAudioObj.transform.SetParent(transform);
        chargeAudioSource = chargeAudioObj.AddComponent<AudioSource>();
        chargeAudioSource.playOnAwake = false;
        chargeAudioSource.loop = true;
        chargeAudioSource.spatialBlend = 1f;

        // Setup effect transforms if not assigned
        SetupEffectTransforms();

        // Get camera for screen shake
        cameraController = brain.CameraController;

        // Subscribe to melee events
        SubscribeToMeleeEvents();
    }

    public void UpdateModule()
    {
        // Handle charging audio
        HandleChargingAudio();
    }

    void SetupEffectTransforms()
    {
        if (weaponEffectsTransform == null)
        {
            var weaponEffectsObj = new GameObject("WeaponEffects");
            weaponEffectsObj.transform.SetParent(transform);
            weaponEffectsTransform = weaponEffectsObj.transform;
        }

        if (impactEffectsTransform == null)
        {
            var impactEffectsObj = new GameObject("ImpactEffects");
            impactEffectsObj.transform.SetParent(transform);
            impactEffectsTransform = impactEffectsObj.transform;
        }

        if (blockEffectsTransform == null)
        {
            var blockEffectsObj = new GameObject("BlockEffects");
            blockEffectsObj.transform.SetParent(transform);
            blockEffectsTransform = blockEffectsObj.transform;
        }
    }

    void SubscribeToMeleeEvents()
    {
        if (meleeModule == null) return;

        // Subscribe to existing events
        meleeModule.OnAttackBegin += HandleAttackBegin;
        meleeModule.OnAttackComplete += HandleAttackComplete;
        meleeModule.OnBlockBegin += HandleBlockBegin;
        meleeModule.OnBlockComplete += HandleBlockComplete;

        // Subscribe to new animation event forwarding
        meleeModule.OnWeaponEnabled += HandleWeaponEnabled;
        meleeModule.OnWeaponDisabled += HandleWeaponDisabled;
        meleeModule.OnComboIncremented += HandleComboIncremented;

        // Subscribe to attack module events directly for better control
        if (meleeModule.Attack != null)
        {
            meleeModule.Attack.OnHeavyAttackChargeStart += HandleHeavyAttackChargeStart;
            meleeModule.Attack.OnHeavyAttackChargeEnd += HandleHeavyAttackChargeEnd;
            meleeModule.Attack.OnAttackPerformed += HandleAttackPerformed;
        }
    }

    void UnsubscribeFromMeleeEvents()
    {
        if (meleeModule == null) return;

        meleeModule.OnAttackBegin -= HandleAttackBegin;
        meleeModule.OnAttackComplete -= HandleAttackComplete;
        meleeModule.OnBlockBegin -= HandleBlockBegin;
        meleeModule.OnBlockComplete -= HandleBlockComplete;
        meleeModule.OnWeaponEnabled -= HandleWeaponEnabled;
        meleeModule.OnWeaponDisabled -= HandleWeaponDisabled;
        meleeModule.OnComboIncremented -= HandleComboIncremented;

        if (meleeModule.Attack != null)
        {
            meleeModule.Attack.OnHeavyAttackChargeStart -= HandleHeavyAttackChargeStart;
            meleeModule.Attack.OnHeavyAttackChargeEnd -= HandleHeavyAttackChargeEnd;
            meleeModule.Attack.OnAttackPerformed -= HandleAttackPerformed;
        }
    }

    #region Event Handlers

    void HandleAttackBegin()
    {
        if (!IsEnabled) return;

        // Basic attack start effects (sound/shake)
        int comboCount = meleeModule.CurrentComboCount;
        bool isHeavyAttack = meleeModule.Attack?.IsChargingHeavyAttack ?? false;

        if (isHeavyAttack)
        {
            PlayAttackSound(heavyAttackSounds);
            TriggerScreenShake(heavyAttackShake);
        }
        else
        {
            PlayAttackSound(lightAttackSounds);
            TriggerScreenShake(lightAttackShake);
        }

        if (debugEffects)
            Debug.Log($"[MeleeEffects] Attack began - Combo: {comboCount}, Heavy: {isHeavyAttack}");
    }

    void HandleAttackComplete()
    {
        CleanupAttackEffects();

        if (debugEffects)
            Debug.Log("[MeleeEffects] Attack completed");
    }

    void HandleWeaponEnabled()
    {
        if (!IsEnabled) return;

        int comboCount = meleeModule.CurrentComboCount;
        bool isHeavyAttack = meleeModule.Attack?.IsChargingHeavyAttack ?? false;

        if (isHeavyAttack)
        {
            PlayHeavyAttackEffects();
        }
        else
        {
            PlayLightAttackEffects(comboCount);
        }

        if (debugEffects)
            Debug.Log($"[MeleeEffects] Weapon collision enabled - playing visual effects");
    }

    void HandleWeaponDisabled()
    {
        if (debugEffects)
            Debug.Log("[MeleeEffects] Weapon collision disabled");
    }

    void HandleComboIncremented()
    {
        if (!IsEnabled) return;

        int newComboCount = meleeModule.CurrentComboCount;

        PlayComboIncrementEffects(newComboCount);

        var comboModule = meleeModule.Combo;
        if (comboModule != null && newComboCount >= comboModule.MaxComboCount)
        {
            PlayComboFinisherEffects();
            PlaySound(comboFinisherSound);
        }

        lastComboCount = newComboCount;

        if (debugEffects)
            Debug.Log($"[MeleeEffects] Combo incremented to {newComboCount}");
    }

    void HandleAttackPerformed(bool isHeavyAttack, int comboCount)
    {
        if (debugEffects)
            Debug.Log($"[MeleeEffects] Attack performed - Heavy: {isHeavyAttack}, Combo: {comboCount}");
    }

    void HandleBlockBegin()
    {
        if (!IsEnabled) return;

        PlayBlockEffects();
        PlaySound(GetRandomSound(blockSounds));
        TriggerScreenShake(blockShake);

        if (debugEffects)
            Debug.Log("[MeleeEffects] Block began");
    }

    void HandleBlockComplete()
    {
        CleanupBlockEffects();

        if (debugEffects)
            Debug.Log("[MeleeEffects] Block completed");
    }

    void HandleHeavyAttackChargeStart()
    {
        if (heavyChargeSound != null)
        {
            chargeAudioSource.clip = heavyChargeSound;
            chargeAudioSource.Play();
        }

        if (debugEffects)
            Debug.Log("[MeleeEffects] Heavy attack charge started");
    }

    void HandleHeavyAttackChargeEnd()
    {
        if (chargeAudioSource.isPlaying)
        {
            chargeAudioSource.Stop();
        }

        if (debugEffects)
            Debug.Log("[MeleeEffects] Heavy attack charge ended");
    }

    #endregion

    #region Effect Methods

    void PlayLightAttackEffects(int comboCount)
    {
        if (lightAttackEffect != null)
        {
            PlayParticleEffect(lightAttackEffect, weaponEffectsTransform.position);
        }

        if (comboCount > 0 && comboCount <= comboEffects.Length)
        {
            int comboIndex = comboCount - 1;
            if (comboEffects[comboIndex] != null)
            {
                var comboEffect = comboEffects[comboIndex];
                PlayParticleEffect(comboEffect, weaponEffectsTransform.position);

                if (scaleBrightnessByCombo)
                {
                    var main = comboEffect.main;
                    var originalColor = main.startColor.color;
                    main.startColor = originalColor * (1f + (comboCount - 1) * 0.3f);
                }

                if (debugEffects)
                    Debug.Log($"[MeleeEffects] Played combo effect {comboIndex} for combo {comboCount}");
            }
        }
        else if (debugEffects && comboCount > 0)
        {
            Debug.LogWarning($"[MeleeEffects] Combo count {comboCount} out of range for effects array (length: {comboEffects.Length})");
        }
    }

    void PlayHeavyAttackEffects()
    {
        if (heavyAttackEffect != null)
        {
            PlayParticleEffect(heavyAttackEffect, weaponEffectsTransform.position);
        }
    }

    void PlayComboIncrementEffects(int comboCount)
    {
        if (debugEffects)
            Debug.Log($"[MeleeEffects] Combo increment effect for count: {comboCount}");
    }

    void PlayComboFinisherEffects()
    {
        if (criticalHitEffect != null)
        {
            PlayParticleEffect(criticalHitEffect, weaponEffectsTransform.position);
        }
    }

    void PlayBlockEffects()
    {
        if (blockEffect != null)
        {
            PlayParticleEffect(blockEffect, blockEffectsTransform.position);
        }
    }

    void PlayParticleEffect(ParticleSystem prefab, Vector3 position)
    {
        if (prefab == null) return;

        string effectKey = $"{prefab.name}_{Time.time}";

        GameObject effectInstance = Instantiate(prefab.gameObject, position, prefab.transform.rotation);
        effectInstance.transform.SetParent(weaponEffectsTransform);
        effectInstance.name = $"{prefab.name}_Instance";

        activeEffectInstances[effectKey] = effectInstance;

        StartCoroutine(CleanupEffectAfterDelay(effectKey, effectCleanupDelay));
    }

    System.Collections.IEnumerator CleanupEffectAfterDelay(string effectKey, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (activeEffectInstances.TryGetValue(effectKey, out GameObject instance))
        {
            if (instance != null)
            {
                Destroy(instance);
            }
            activeEffectInstances.Remove(effectKey);
        }
    }

    void CleanupAttackEffects()
    {
        if (debugEffects)
            Debug.Log("[MeleeEffects] Cleaned up attack effects");
    }

    void CleanupBlockEffects()
    {
        if (debugEffects)
            Debug.Log("[MeleeEffects] Cleaned up block effects");
    }

    #endregion

    #region Audio Methods

    void PlayAttackSound(AudioClip[] sounds)
    {
        AudioClip sound = GetRandomSound(sounds);
        PlaySound(sound);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    AudioClip GetRandomSound(AudioClip[] sounds)
    {
        if (sounds == null || sounds.Length == 0) return null;
        return sounds[Random.Range(0, sounds.Length)];
    }

    void HandleChargingAudio()
    {
        if (meleeModule?.Attack != null)
        {
            bool isCharging = meleeModule.Attack.IsChargingHeavyAttack;

            if (isCharging && !chargeAudioSource.isPlaying && heavyChargeSound != null)
            {
                chargeAudioSource.clip = heavyChargeSound;
                chargeAudioSource.Play();
            }
            else if (!isCharging && chargeAudioSource.isPlaying)
            {
                chargeAudioSource.Stop();
            }
        }
    }

    #endregion

    #region Screen Shake

    void TriggerScreenShake(float intensity)
    {
        if (!enableScreenShake || cameraController == null) return;

        // This would need to be implemented in SimpleThirdPersonCamera
        // cameraController.TriggerScreenShake(intensity);

        if (debugEffects)
            Debug.Log($"[MeleeEffects] Screen shake triggered with intensity: {intensity}");
    }

    #endregion

    #region Debug

    void OnGUI()
    {
        if (!debugEffects) return;

        GUILayout.BeginArea(new Rect(Screen.width - 300, 200, 290, 150));
        GUILayout.Label("=== MELEE EFFECTS DEBUG ===");
        GUILayout.Label($"Current Combo: {meleeModule?.CurrentComboCount ?? 0}");
        GUILayout.Label($"Effects Array Length: {comboEffects.Length}");
        GUILayout.Label($"Active Effects: {activeEffectInstances.Count}");
        GUILayout.Label($"Audio Source: {(audioSource != null ? "YES" : "NO")}");
        GUILayout.Label($"Charge Audio: {(chargeAudioSource?.isPlaying == true ? "PLAYING" : "SILENT")}");
        GUILayout.EndArea();
    }

    #endregion

    void OnDestroy()
    {
        UnsubscribeFromMeleeEvents();

        foreach (var kvp in activeEffectInstances)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        activeEffectInstances.Clear();
    }
}