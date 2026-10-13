using UnityEngine;
using TMPro;

// DamageNumber — spawned at hit point, floats upward, fades out, self-destructs.
//
// Setup:
//   1. Create a GameObject with this component and a TextMeshPro (world space).
//   2. Assign the TextMeshPro reference in the inspector.
//   3. Save as DamageNumber.prefab.
//   4. Assign the prefab to DamageNumberManager in the scene.

namespace CrabSystem
{
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshPro text;

        [Header("Behaviour")]
        [SerializeField] private float floatSpeed = 2f;
        [SerializeField] private float lifetime = 1f;
        [SerializeField] private float randomSpread = 0.5f;

        private float elapsed;
        private float startAlpha = 1f;
        private Camera cam;

        public void Init(string label, Vector3 worldPos, float scale, Color color)
        {
            Vector3 offset = new Vector3(
                Random.Range(-randomSpread, randomSpread),
                0f,
                Random.Range(-randomSpread, randomSpread)
            );

            transform.position = worldPos + offset;
            text.text = label;
            text.fontSize *= scale;
            text.color = color;
            startAlpha = color.a;
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

            text.alpha = startAlpha * (1f - elapsed / lifetime);

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
}
