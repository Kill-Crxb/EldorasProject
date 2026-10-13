using UnityEngine;

// GI-23: a press that does nothing is never silent. When the player's AbilitySystem refuses a press (cooldown, a short
// resource, a denying fact) this plays a dull click, shakes the hotbar and, when a resource was short, flashes that
// resource's bar. Put it anywhere under the HUD; it finds the player once one exists.
public class DeniedInputFeedback : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip deniedClip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.6f;

    [Tooltip("Shaken sideways on a refusal — the hotbar's root, usually.")]
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField] private float shakePixels = 6f;
    [SerializeField] private float shakeSeconds = 0.18f;

    [SerializeField] private Color flashColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] private float flashSeconds = 0.25f;

    private AbilitySystem abilities;
    private Vector2 shakeHome;
    private float shakeUntil;
    private float nextBindAt;
    private const float BindRetrySeconds = 0.5f;

    private void Awake()
    {
        if (shakeTarget != null) shakeHome = shakeTarget.anchoredPosition;
    }

    private void OnDestroy()
    {
        if (abilities != null) abilities.OnAbilityRefused -= HandleRefused;
    }

    private void Update()
    {
        if (abilities == null) TryBind();
        TickShake();
    }

    private void TryBind()
    {
        if (Time.unscaledTime < nextBindAt) return;
        nextBindAt = Time.unscaledTime + BindRetrySeconds;

        ControllerBrain player = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        abilities = player != null ? player.Abilities : null;
        if (abilities != null) abilities.OnAbilityRefused += HandleRefused;
    }

    private void HandleRefused(AbilityDefinition ability, AbilityRefusal why, ResourceDefinition shortOf)
    {
        if (audioSource != null && deniedClip != null) audioSource.PlayOneShot(deniedClip, volume);
        if (shakeTarget != null) shakeUntil = Time.unscaledTime + shakeSeconds;
        if (shortOf != null) FlashBar(shortOf);
    }

    private void FlashBar(ResourceDefinition resource)
    {
        foreach (BarView bar in FindObjectsByType<BarView>(FindObjectsSortMode.None))
        {
            if (bar.Resource == resource) bar.Flash(flashColor, flashSeconds);
        }
    }

    // A fast side-to-side wobble that dies out, then home.
    private void TickShake()
    {
        if (shakeTarget == null) return;

        float left = shakeUntil - Time.unscaledTime;
        if (left <= 0f)
        {
            shakeTarget.anchoredPosition = shakeHome;
            return;
        }

        float strength = left / shakeSeconds;
        float offset = Mathf.Sin(Time.unscaledTime * 90f) * shakePixels * strength;
        shakeTarget.anchoredPosition = shakeHome + new Vector2(offset, 0f);
    }
}
