using UnityEngine;
using TMPro;

/// <summary>
/// DamageNumber — spawned at hit point, floats upward, fades out, self-destructs.
///
/// Setup:
///   1. Create a GameObject with this component and a TextMeshPro (world space).
///   2. Assign the TextMeshPro reference in the inspector.
///   3. Save as DamageNumber.prefab.
///   4. Assign the prefab to DamageNumberManager in the scene.
/// </summary>
public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TextMeshPro text;

    [Header("Behaviour")]
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private float randomSpread = 0.5f;

    private float elapsed;
    private Camera cam;

    public void Init(float damage, Vector3 worldPos)
    {
        Vector3 offset = new Vector3(
            Random.Range(-randomSpread, randomSpread),
            0f,
            Random.Range(-randomSpread, randomSpread)
        );

        transform.position = worldPos + offset;
        text.text = Mathf.RoundToInt(damage).ToString();
        cam = Camera.main;

        // Set correct facing immediately on spawn so there's no one-frame pop
        if (cam != null)
            BillboardToCamera();
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        if (cam != null)
            BillboardToCamera();

        text.alpha = 1f - (elapsed / lifetime);

        if (elapsed >= lifetime)
            Destroy(gameObject);
    }

    private void BillboardToCamera()
    {
        transform.LookAt(
            transform.position + cam.transform.rotation * Vector3.forward,
            cam.transform.rotation * Vector3.up
        );
    }
}