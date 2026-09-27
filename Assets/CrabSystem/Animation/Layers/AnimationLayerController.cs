using System.Collections.Generic;
using UnityEngine;

// Single owner of animator layer weights.
//
// Callers do not write weights. They CLAIM one with a priority and an owner token, and release
// their own claim when done. Each frame the highest-priority live claim per layer wins and the
// weight blends toward it. Releasing hands the layer back to the next-highest claim rather than
// to zero — which is the whole point: a system that zeroes a layer it does not own is how an
// ability completing kills an in-flight jump.
//
// A layer is only touched once something has claimed it. Layers nothing claims keep the weight
// the Animator Controller authored, so adding this component changes nothing on its own.
public class AnimationLayerController : MonoBehaviour, IBrainModule
{
    public const int PriorityLocomotion = 10;
    public const int PriorityAbility = 50;
    public const int PriorityReaction = 75;
    public const int PriorityOverride = 100;

    [Header("Profile")]
    [Tooltip("Optional. Per-layer fade times, rest weights and aliases.")]
    [SerializeField] private AnimationLayerProfile profile;

    [Header("Runtime")]
    [Tooltip("Off, the controller writes nothing and authored weights are left alone. Safe to " +
             "flip in play mode to see what the arbitration is actually doing.")]
    [SerializeField] private bool runtimeWeighting = true;

    [SerializeField] private float defaultFadeIn = 0.05f;
    [SerializeField] private float defaultFadeOut = 0.15f;

    private class LayerState
    {
        public string Name;
        public int Index;
        public float RestWeight;
        public float Current;
        public float FadeIn;
        public float FadeOut;
        public bool Driven;
        public bool Claimable = true;
    }

    private struct LayerClaim
    {
        public object Owner;
        public int LayerIndex;
        public float Weight;
        public int Priority;
        public int Serial;
    }

    private readonly List<LayerState> layers = new List<LayerState>();
    private readonly Dictionary<string, LayerState> byName = new Dictionary<string, LayerState>();
    private readonly List<LayerClaim> claims = new List<LayerClaim>();
    private readonly HashSet<string> warnedNames = new HashSet<string>();

    private ControllerBrain brain;
    private AnimationSystem animationSystem;
    private Animator boundAnimator;
    private int serial;
    private float readyWaitTime;
    private bool warnedNotReady;

    public bool IsEnabled { get; set; } = true;
    public int ClaimCount => claims.Count;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        animationSystem = brain?.Animation;

        if (animationSystem == null)
        {
            Debug.LogError($"[AnimationLayerController] No AnimationSystem on '{name}'. Layer arbitration is off.", this);
            IsEnabled = false;
            return;
        }

        // Deliberately not built here. ModelModule instantiates the model during the Brain's
        // InitializeModules, one step before this runs, so the new Animator has not bound its
        // controller yet and layerCount is still 0. Built on first use instead.
        EnsureTable();
    }

    public void UpdateModule()
    {
        if (!IsEnabled || !runtimeWeighting || animationSystem == null) return;

        if (!EnsureTable()) return;

        float dt = Time.deltaTime;

        for (int i = 0; i < layers.Count; i++)
        {
            LayerState layer = layers[i];
            if (!layer.Driven || !layer.Claimable) continue;

            float target = ResolveTarget(layer);
            float duration = target > layer.Current ? layer.FadeIn : layer.FadeOut;
            float step = duration > 0f ? dt / duration : 1f;

            layer.Current = Mathf.MoveTowards(layer.Current, target, step);
            animationSystem.SetLayerWeight(layer.Index, layer.Current);
        }
    }

    // ── Claims ────────────────────────────────────────────────────────────────────────────────

    public void Claim(object owner, string layerName, float weight, int priority)
    {
        if (owner == null) return;

        LayerState layer = Resolve(layerName);
        if (layer == null || !layer.Claimable) return;

        layer.Driven = true;

        // One claim per owner per layer. Re-claiming updates in place rather than stacking, so a
        // system calling this every frame is harmless.
        for (int i = 0; i < claims.Count; i++)
        {
            if (claims[i].Owner != owner || claims[i].LayerIndex != layer.Index) continue;

            LayerClaim existing = claims[i];
            existing.Weight = Mathf.Clamp01(weight);
            existing.Priority = priority;
            claims[i] = existing;
            return;
        }

        claims.Add(new LayerClaim
        {
            Owner = owner,
            LayerIndex = layer.Index,
            Weight = Mathf.Clamp01(weight),
            Priority = priority,
            Serial = serial++,
        });
    }

    public void Release(object owner, string layerName)
    {
        LayerState layer = Resolve(layerName);
        if (owner == null || layer == null) return;

        for (int i = claims.Count - 1; i >= 0; i--)
            if (claims[i].Owner == owner && claims[i].LayerIndex == layer.Index)
                claims.RemoveAt(i);
    }

    public void ReleaseAll(object owner)
    {
        if (owner == null) return;

        for (int i = claims.Count - 1; i >= 0; i--)
            if (claims[i].Owner == owner)
                claims.RemoveAt(i);
    }

    public bool HasClaim(object owner, string layerName)
    {
        LayerState layer = Resolve(layerName);
        if (owner == null || layer == null) return false;

        foreach (LayerClaim claim in claims)
            if (claim.Owner == owner && claim.LayerIndex == layer.Index) return true;

        return false;
    }

    // ── Resolution ────────────────────────────────────────────────────────────────────────────

    private float ResolveTarget(LayerState layer)
    {
        float target = layer.RestWeight;
        bool found = false;
        int bestPriority = 0;
        int bestSerial = 0;

        foreach (LayerClaim claim in claims)
        {
            if (claim.LayerIndex != layer.Index) continue;

            // Ties go to the newer claim. Two systems at the same priority is a design smell, but
            // last-in is at least predictable rather than list-order dependent.
            bool better = !found || claim.Priority > bestPriority || (claim.Priority == bestPriority && claim.Serial > bestSerial);
            if (!better) continue;

            target = claim.Weight;
            bestPriority = claim.Priority;
            bestSerial = claim.Serial;
            found = true;
        }

        return target;
    }

    private LayerState Resolve(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return null;

        // Not an error yet — the animator may simply not have bound its controller. A claim made
        // in that window is dropped, and the callers that matter re-claim on later frames.
        if (!EnsureTable()) return null;

        string key = Normalize(layerName);
        if (byName.TryGetValue(key, out LayerState layer)) return layer;

        if (warnedNames.Add(layerName))
            Debug.LogError($"[AnimationLayerController] No animator layer matches '{layerName}' on '{name}'. The animator has {layers.Count}: {string.Join(", ", LayerNames())}", this);

        return null;
    }

    // The layer table cannot be built until the Animator has bound its controller — until then
    // layerCount is 0 and every name resolves to nothing. Rebuilt when the animator changes, which
    // is what a ModelModule swap looks like from here.
    private bool EnsureTable()
    {
        Animator animator = brain?.EntityAnimator;

        if (animator == null || animator.layerCount == 0)
        {
            WarnIfStuck();
            return false;
        }

        if (animator == boundAnimator && layers.Count > 0) return true;

        BuildLayerTable(animator);
        boundAnimator = animator;
        return true;
    }

    private void WarnIfStuck()
    {
        if (warnedNotReady || !IsEnabled) return;

        readyWaitTime += Time.deltaTime;
        if (readyWaitTime < 2f) return;

        warnedNotReady = true;
        Debug.LogError($"[AnimationLayerController] '{name}' still has no animator layers after 2s. Either the Brain has no EntityAnimator or its Animator has no controller assigned.", this);
    }

    private string[] LayerNames()
    {
        string[] names = new string[layers.Count];

        for (int i = 0; i < layers.Count; i++)
            names[i] = layers[i].Name;

        return names;
    }

    private void BuildLayerTable(Animator animator)
    {
        layers.Clear();
        byName.Clear();

        for (int i = 0; i < animator.layerCount; i++)
        {
            float authored = animator.GetLayerWeight(i);

            LayerState layer = new LayerState
            {
                Name = animator.GetLayerName(i),
                Index = i,
                RestWeight = authored,
                Current = authored,
                FadeIn = defaultFadeIn,
                FadeOut = defaultFadeOut,
            };

            layers.Add(layer);
            byName[Normalize(layer.Name)] = layer;
        }

        ApplyProfile();
    }

    private void ApplyProfile()
    {
        if (profile == null || profile.layers == null) return;

        foreach (AnimationLayerProfile.LayerBinding binding in profile.layers)
        {
            if (binding == null || string.IsNullOrEmpty(binding.layerName)) continue;

            if (!byName.TryGetValue(Normalize(binding.layerName), out LayerState layer))
            {
                Debug.LogError($"[AnimationLayerController] Profile names layer '{binding.layerName}', which the animator on '{name}' does not have.", this);
                continue;
            }

            layer.FadeIn = binding.fadeIn;
            layer.FadeOut = binding.fadeOut;
            layer.Claimable = binding.driveAtRuntime;

            if (binding.overrideRestWeight) layer.RestWeight = binding.restWeight;

            if (binding.aliases == null) continue;

            foreach (string alias in binding.aliases)
            {
                if (string.IsNullOrEmpty(alias)) continue;
                byName[Normalize(alias)] = layer;
            }
        }
    }

    // Lowercase and strip separators, so "Fullbody Actions" binds to "Full Body Actions" rather
    // than resolving to -1 and skipping the write with no error.
    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length);

        foreach (char c in value)
        {
            if (c == ' ' || c == '_' || c == '-') continue;
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    // ── Diagnostics ───────────────────────────────────────────────────────────────────────────

    [ContextMenu("Log Layer Bindings")]
    public void LogLayerBindings()
    {
        string report = $"[AnimationLayerController] '{name}' — runtimeWeighting: {runtimeWeighting}, claims: {claims.Count}\n";

        foreach (LayerState layer in layers)
        {
            int count = 0;
            foreach (LayerClaim claim in claims)
                if (claim.LayerIndex == layer.Index) count++;

            string driven = layer.Claimable ? (layer.Driven ? "driven" : "idle") : "EXCLUDED";
            report += $"  [{layer.Index}] {layer.Name,-22} rest={layer.RestWeight:0.00}  now={layer.Current:0.00}  claims={count}  {driven}\n";
        }

        foreach (LayerClaim claim in claims)
        {
            string ownerName = claim.Owner != null ? claim.Owner.GetType().Name : "?";
            report += $"    claim: layer {claim.LayerIndex}  weight {claim.Weight:0.00}  priority {claim.Priority}  by {ownerName}\n";
        }

        Debug.Log(report, this);
    }
}
