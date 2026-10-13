using UnityEngine;

/// <summary>
/// Creates a natural lantern/candle flicker using Perlin noise.
/// Assign the target Light manually.
/// </summary>
public class LanternFlicker : MonoBehaviour
{
    [Header("Light Reference")]
    [SerializeField] private Light targetLight;

    [Header("Base Settings")]
    [SerializeField] private float baseIntensity = 1.5f;
    [SerializeField] private float intensityVariation = 0.35f;

    [Header("Flicker")]
    [Tooltip("How quickly the light changes.")]
    [SerializeField] private float flickerSpeed = 8f;

    [Tooltip("Adds occasional stronger flickers.")]
    [SerializeField] private bool useRandomBursts = true;

    [Tooltip("Maximum additional intensity during a burst.")]
    [SerializeField] private float burstStrength = 0.2f;

    [Tooltip("Minimum and maximum time between bursts.")]
    [SerializeField] private Vector2 burstInterval = new Vector2(0.4f, 1.5f);

    [Header("Range Flicker")]
    [SerializeField] private bool flickerRange = true;
    [SerializeField] private float baseRange = 8f;
    [SerializeField] private float rangeVariation = 0.25f;

    private float noiseSeed;
    private float nextBurstTime;
    private float burstAmount;

    private void Awake()
    {
        if (targetLight == null)
        {
            Debug.LogWarning($"{nameof(LanternFlicker)} on '{gameObject.name}' has no Light assigned.", this);
            enabled = false;
            return;
        }

        noiseSeed = Random.Range(0f, 1000f);

        ScheduleNextBurst();

        targetLight.intensity = baseIntensity;
        targetLight.range = baseRange;
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);

        // Convert Perlin noise from 0-1 to -1 to 1
        noise = (noise - 0.5f) * 2f;

        float intensity = baseIntensity + noise * intensityVariation;

        if (useRandomBursts)
        {
            if (Time.time >= nextBurstTime)
            {
                burstAmount = Random.Range(0f, burstStrength);
                ScheduleNextBurst();
            }

            burstAmount = Mathf.Lerp(burstAmount, 0f, Time.deltaTime * 6f);
            intensity += burstAmount;
        }

        targetLight.intensity = Mathf.Max(0f, intensity);

        if (flickerRange)
        {
            targetLight.range = baseRange + noise * rangeVariation;
        }
    }

    private void ScheduleNextBurst()
    {
        nextBurstTime = Time.time + Random.Range(burstInterval.x, burstInterval.y);
    }
}