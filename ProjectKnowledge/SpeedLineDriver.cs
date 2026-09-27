using UnityEngine;

/// <summary>
/// Feeds the Screen Speed Lines shader from MovementSystem's published speed blend.
///
/// Lives on the QUAD, not on the character — speed lines are a camera effect, so the thing that
/// owns them is the view, and "only the player sees them" comes free because nothing else has a
/// camera.
///
/// It holds NO thresholds and NO smoothing of its own. Both live in MovementSystem, so the
/// camera's sprint framing and these lines respond to the same moment with the same curve. Two
/// effects that each decide privately what "fast" means will disagree on screen, and the seam
/// between them is exactly the sort of thing that reads as jank without ever looking like a bug.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class SpeedLineDriver : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Whose speed drives the lines. Left empty, this searches up the hierarchy for a " +
             "ControllerBrain, which works when the quad is parented under the player's camera.")]
    [SerializeField] private MovementSystem movement;

    [Header("Strength")]
    [Tooltip("Scales the published blend. Lower it to keep lines subtle without changing the " +
             "speed thresholds everything else shares.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxIntensity = 1f;

    private Renderer targetRenderer;
    private MaterialPropertyBlock block;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();

        if (movement == null)
            movement = GetComponentInParent<ControllerBrain>()?.GetModule<MovementSystem>();

        if (movement == null)
            Debug.LogWarning($"[SpeedLineDriver] {name} found no MovementSystem — the lines will " +
                             "never appear. Assign one, or parent this under the player.");
    }

    private void LateUpdate()
    {
        float intensity = movement != null ? movement.SpeedBlend * maxIntensity : 0f;

        // Stop submitting a fullscreen transparent quad that would draw nothing.
        targetRenderer.enabled = intensity > 0.001f;
        if (!targetRenderer.enabled) return;

        // MaterialPropertyBlock rather than renderer.material — the latter instantiates a copy
        // of the material per renderer and leaks it for the lifetime of the scene.
        targetRenderer.GetPropertyBlock(block);
        block.SetFloat(IntensityId, intensity);
        targetRenderer.SetPropertyBlock(block);
    }
}
