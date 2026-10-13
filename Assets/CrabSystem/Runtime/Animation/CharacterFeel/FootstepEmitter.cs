using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Footsteps driven by the foot IK's own ground contact rather than animation events, so they
// land when the foot actually touches and carry the surface it touched.
public class FootstepEmitter : MonoBehaviour
{
    [Serializable]
    public class SurfaceSound
    {
        [Tooltip("Collider tag this set plays for. Leave the list empty to fall back to defaultClips.")]
        public string surfaceTag;
        public AudioClip[] clips;
    }

    [Serializable]
    public class StepEvent : UnityEvent<Vector3, Vector3> { }

    [Header("Source")]
    [SerializeField] private FootIKSystem footIK;
    [SerializeField] private AudioSource audioSource;

    [Header("Gating")]
    [Tooltip("Steps below this speed are shuffles, not footfalls. Zero plays every contact.")]
    [SerializeField] private float minStepSpeed = 0.4f;

    [Tooltip("Shortest gap between two steps. Guards against a foot chattering across a seam.")]
    [SerializeField] private float minStepInterval = 0.12f;

    [Header("Volume")]
    [SerializeField] private float speedAtMinVolume = 1.5f;
    [SerializeField] private float speedAtMaxVolume = 8f;
    [SerializeField, Range(0f, 1f)] private float minVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1f;
    [SerializeField] private float pitchJitter = 0.06f;

    [Header("Clips")]
    [SerializeField] private AudioClip[] defaultClips;
    [SerializeField] private List<SurfaceSound> surfaces = new List<SurfaceSound>();

    [Header("Hooks")]
    [Tooltip("Position and surface normal of the contact. Wire dust, decals or ripples here.")]
    [SerializeField] private StepEvent onStep = new StepEvent();

    private float lastStepTime = -1f;
    private AudioClip lastClip;

    void Reset()
    {
        footIK = GetComponent<FootIKSystem>();
        audioSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        if (footIK == null) footIK = GetComponent<FootIKSystem>();

        if (footIK == null)
        {
            Debug.LogError($"[FootstepEmitter] No FootIKSystem assigned or found on '{name}'.", this);
            enabled = false;
            return;
        }

        footIK.OnFootPlanted += HandleFootPlanted;
    }

    void OnDisable()
    {
        if (footIK != null) footIK.OnFootPlanted -= HandleFootPlanted;
    }

    private void HandleFootPlanted(FootIKSystem.FootContact contact)
    {
        if (contact.Speed < minStepSpeed) return;
        if (Time.time - lastStepTime < minStepInterval) return;

        lastStepTime = Time.time;

        onStep.Invoke(contact.Position, contact.Normal);

        AudioClip clip = PickClip(contact.Collider);
        if (clip == null || audioSource == null) return;

        audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
        audioSource.PlayOneShot(clip, Volume(contact.Speed));
    }

    private float Volume(float speed)
    {
        float t = Mathf.InverseLerp(speedAtMinVolume, speedAtMaxVolume, speed);
        return Mathf.Lerp(minVolume, maxVolume, t);
    }

    private AudioClip PickClip(Collider surface)
    {
        AudioClip[] set = MatchSurface(surface);
        if (set == null || set.Length == 0) set = defaultClips;
        if (set == null || set.Length == 0) return null;
        if (set.Length == 1) return set[0];

        // Never the same clip twice running — repetition is what makes footsteps sound synthetic.
        AudioClip pick = set[UnityEngine.Random.Range(0, set.Length)];

        if (pick == lastClip)
            pick = set[(Array.IndexOf(set, pick) + 1) % set.Length];

        lastClip = pick;
        return pick;
    }

    private AudioClip[] MatchSurface(Collider surface)
    {
        if (surface == null) return null;

        for (int i = 0; i < surfaces.Count; i++)
        {
            SurfaceSound entry = surfaces[i];
            if (entry == null || string.IsNullOrEmpty(entry.surfaceTag)) continue;
            if (surface.CompareTag(entry.surfaceTag)) return entry.clips;
        }

        return null;
    }
}
