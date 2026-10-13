using System.Collections.Generic;
using UnityEngine;

// The warning on a Perilous move (unblockable, unparryable — dodge it). As the move starts, a sharp ring sounds from
// the attacker and their equipped weapon glows red, holding through the windup and fading as the strike comes out.
// World feedback: everyone sees and hears it, on every combatant. Put it on a child of the brain.
//
// Drives FlatToon's _HitFlash / _HitFlashColor on the weapon's renderers through a property block, cleared when the
// glow ends so the weapon rejoins the SRP batcher.
public class PerilousTell : MonoBehaviour, IBrainModule
{
    private static readonly int FlashId = Shader.PropertyToID("_HitFlash");
    private static readonly int ColorId = Shader.PropertyToID("_HitFlashColor");

    [SerializeField] private bool isEnabled = true;

    [Tooltip("Equipment slot whose visual glows.")]
    [IdRef(IdKind.EquipmentSlot)] [SerializeField] private string weaponSlotId = "mainwep";
    [ColorUsage(false, true)] [SerializeField] private Color glowColor = new Color(2.5f, 0.15f, 0.05f, 1f);
    [Tooltip("The glow holds while the move winds up, then fades over this many seconds.")]
    [SerializeField] private float fadeSeconds = 0.2f;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tellClip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.9f;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    private ControllerBrain brain;
    private AbilitySystem abilities;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private MaterialPropertyBlock block;
    private AbilityDefinition glowingMove;
    private float fadeStart = -1f;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        block = new MaterialPropertyBlock();
    }

    public void LateInitialize()
    {
        abilities = brain.Abilities;
        if (abilities != null) abilities.OnAbilityUsed += HandleUsed;
    }

    private void OnDestroy()
    {
        if (abilities != null) abilities.OnAbilityUsed -= HandleUsed;
    }

    private void OnDisable()
    {
        Clear();
    }

    public void UpdateModule()
    {
        if (glowingMove == null) return;

        bool windingUp = abilities.CurrentAbility == glowingMove && abilities.CurrentPhase == MovePhase.Startup;
        if (windingUp) return;

        if (fadeStart < 0f) fadeStart = Time.time;
        float t = (Time.time - fadeStart) / Mathf.Max(0.01f, fadeSeconds);
        if (t >= 1f)
        {
            Clear();
            return;
        }

        Write(1f - t);
    }

    private void HandleUsed(string abilityId)
    {
        AbilityDefinition move = abilities.CurrentAbility;
        if (!isEnabled || move == null || move.abilityId != abilityId) return;
        if (move.hit.guard != GuardType.Perilous) return;

        if (audioSource != null && tellClip != null) audioSource.PlayOneShot(tellClip, volume);

        Clear();
        CollectWeapon();
        if (renderers.Count == 0) return;

        glowingMove = move;
        fadeStart = -1f;
        block.SetColor(ColorId, glowColor);
        Write(1f);
    }

    private void CollectWeapon()
    {
        ModelModule model = brain.Model;
        Transform weapon = model != null ? model.GetEquippedVisual(weaponSlotId) : null;
        if (weapon == null) return;

        weapon.GetComponentsInChildren(true, renderers);
        renderers.RemoveAll(r => r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer);
    }

    private void Write(float value)
    {
        block.SetFloat(FlashId, value);
        foreach (Renderer r in renderers)
        {
            if (r != null) r.SetPropertyBlock(block);
        }
    }

    private void Clear()
    {
        foreach (Renderer r in renderers)
        {
            if (r != null) r.SetPropertyBlock(null);
        }
        renderers.Clear();
        glowingMove = null;
    }
}
