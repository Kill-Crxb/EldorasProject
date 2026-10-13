using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The character on the creation panel, in 3D. Sits on a RawImage; a camera on a stage out of the
/// menu's view renders the model into a texture made to the image's shape. Drag the image to turn
/// the model. The model has no brain: its Animator plays the idle and nothing else runs.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class CreationPreview : MonoBehaviour, IDragHandler
{
    [SerializeField] private Transform turntable;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private float degreesPerPixel = 0.4f;
    [SerializeField] private int textureHeight = 1024;

    private RawImage image;
    private RenderTexture texture;
    private GameObject instance;
    private ModelAppearance appearance;

    private void Awake()
    {
        image = GetComponent<RawImage>();
    }

    private void OnEnable()
    {
        CreateTexture();
    }

    private void OnDisable()
    {
        Clear();
        ReleaseTexture();
    }

    public void Show(GameObject prefab, IReadOnlyList<AppearanceChoice> choices)
    {
        Clear();
        if (prefab == null || turntable == null) return;

        instance = Instantiate(prefab, turntable, false);
        appearance = instance.GetComponent<ModelAppearance>();
        Apply(choices);
    }

    public void Apply(IReadOnlyList<AppearanceChoice> choices)
    {
        if (appearance != null) appearance.Apply(choices);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (turntable != null) turntable.Rotate(0f, -eventData.delta.x * degreesPerPixel, 0f, Space.Self);
    }

    private void Clear()
    {
        if (instance != null) Destroy(instance);
        instance = null;
        appearance = null;
    }

    private void CreateTexture()
    {
        if (previewCamera == null) return;

        Rect rect = ((RectTransform)transform).rect;
        float aspect = rect.height > 0f ? rect.width / rect.height : 1f;

        texture = new RenderTexture(Mathf.RoundToInt(textureHeight * aspect), textureHeight, 24, RenderTextureFormat.ARGB32);
        texture.antiAliasing = 4;
        texture.name = "CreationPreview";

        previewCamera.targetTexture = texture;
        previewCamera.enabled = true;
        image.texture = texture;
    }

    // The camera stays off without a texture, or it would draw over the menu.
    private void ReleaseTexture()
    {
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
            previewCamera.targetTexture = null;
        }

        if (image != null) image.texture = null;
        if (texture == null) return;

        texture.Release();
        Destroy(texture);
        texture = null;
    }
}
