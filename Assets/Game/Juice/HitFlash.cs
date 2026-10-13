using System.Collections.Generic;
using UnityEngine;

// Drives FlatToon's _HitFlash on every renderer under the model root. Juice.md §4.2.
//
// Written through a MaterialPropertyBlock so no material is instanced. A renderer holding a
// block falls out of the SRP batcher, so the block is cleared the moment the flash reaches 0 —
// the cost exists only while flashing.
//
// Renderers are collected at runtime, on the first flash, not assigned: ModelModule spawns and
// swaps the model, so there is nothing to assign in the prefab. A swap re-collects.
public class HitFlash : MonoBehaviour, IBrainModule
{
    private static readonly int FlashId = Shader.PropertyToID("_HitFlash");

    [Header("System State")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Shape of the fade over the flash duration. 1 at the start, 0 at the end.")]
    [SerializeField] private AnimationCurve fade = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    private ControllerBrain brain;
    private ModelModule model;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private MaterialPropertyBlock block;

    private float startTime;
    private float duration;
    private bool flashing;
    private bool collected;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        block = new MaterialPropertyBlock();
    }

    public void LateInitialize()
    {
        model = brain.Model;
        if (model != null) model.OnModelChanged += HandleModelChanged;
    }

    private void OnDestroy()
    {
        if (model != null) model.OnModelChanged -= HandleModelChanged;
    }

    private void OnDisable()
    {
        Clear();
    }

    public void Flash(float seconds)
    {
        if (!isEnabled || seconds <= 0f) return;
        if (!collected) Collect();

        startTime = Time.unscaledTime;
        duration = seconds;
        flashing = true;
        Write(1f);
    }

    // Deliberately not gated on isEnabled: a disabled flash must still fade out and clear.
    public void UpdateModule()
    {
        if (!flashing) return;

        float t = (Time.unscaledTime - startTime) / duration;
        if (t >= 1f)
        {
            Clear();
            return;
        }

        Write(fade.Evaluate(t));
    }

    private void Write(float value)
    {
        block.SetFloat(FlashId, value);
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].SetPropertyBlock(block);
        }
    }

    private void Clear()
    {
        flashing = false;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].SetPropertyBlock(null);
        }
    }

    private void Collect()
    {
        renderers.Clear();
        collected = true;

        Transform root = FindModel();
        if (root == null) return;

        root.GetComponentsInChildren(true, renderers);
        renderers.RemoveAll(r => r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer);
    }

    // Not brain.ModelRoot: on Base_PC that resolves to the entity root, which holds PlayerCam and
    // its speed-line quad — clearing that quad's block would wipe SpeedLineDriver's. The spawned
    // model is the right root; the Animator's object is the fallback for entities without one.
    private Transform FindModel()
    {
        if (model != null && model.CurrentModel != null) return model.CurrentModel.transform;
        Animator animator = brain.EntityAnimator;
        return animator != null ? animator.transform : null;
    }

    private void HandleModelChanged(string modelId)
    {
        Clear();
        collected = false;
    }
}
