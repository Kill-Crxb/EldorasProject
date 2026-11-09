using System;
using System.Collections.Generic;
using System.Linq;
using CrabSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// A character on a turntable inside a dashboard tab: a model with its looks and gear on, standing in its animator's
// idle. Renders through PreviewRenderUtility, so it has its own scene, camera and lights and never touches the open
// scene. Drag to turn. Body frames the whole model; Face frames the head, for tools that edit hair, face and sliders.
// Toolbar takes a tab's own buttons. Graduates with WizardTab.

public class CharacterStage : IDisposable
{
    public enum Framing { Body, Face }

    private const float FieldOfView = 30f;
    private const float Padding = 1.15f;
    private const float FaceHeight = 0.4f;
    private const float HeadLift = 0.08f;
    private const float DegreesPerPixel = 0.5f;

    public VisualElement Element { get; }
    public VisualElement Toolbar { get; }

    private readonly PreviewRenderUtility preview = new PreviewRenderUtility();
    private readonly IMGUIContainer view;
    private GameObject instance;
    private Transform head;
    private Bounds bounds;
    private Framing framing = Framing.Body;
    private float yaw;

    public CharacterStage(float height)
    {
        preview.camera.fieldOfView = FieldOfView;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.16f, 0.16f, 0.18f);
        preview.lights[0].intensity = 1.2f;
        preview.lights[0].transform.rotation = Quaternion.Euler(35f, 150f, 0f);
        preview.lights[1].intensity = 0.5f;
        preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);

        view = new IMGUIContainer(Draw) { style = { height = height } };
        view.RegisterCallback<PointerDownEvent>(e => view.CapturePointer(e.pointerId));
        view.RegisterCallback<PointerUpEvent>(e => view.ReleasePointer(e.pointerId));
        view.RegisterCallback<PointerMoveEvent>(Turn);

        Toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginBottom = 8 } };
        Toolbar.Add(new Button(() => Frame(Framing.Body)) { text = "Body" });
        Toolbar.Add(new Button(() => Frame(Framing.Face)) { text = "Face" });

        Element = new VisualElement();
        Element.Add(view);
        Element.Add(Toolbar);
    }

    // Looks are the creation groups' picks; gear goes on as ModelModule puts it on (CharacterDresser).
    public void Show(GameObject prefab, IReadOnlyList<AppearanceChoice> looks, IReadOnlyList<ItemDefinition> gear)
    {
        Clear();
        view.MarkDirtyRepaint();
        if (prefab == null) return;

        instance = UnityEngine.Object.Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(instance, preview.camera.scene);

        CharacterDresser.Dress(instance, looks, gear);
        Hide();
        Pose();
        Measure();
    }

    public void Frame(Framing newFraming)
    {
        framing = newFraming;
        view.MarkDirtyRepaint();
    }

    // One still of the stage as it stands, for thumbnails. The caller owns the texture.
    public Texture2D Snapshot(int width, int height)
    {
        preview.BeginStaticPreview(new Rect(0f, 0f, width, height));
        if (instance != null) Aim();
        preview.Render(true);
        return preview.EndStaticPreview();
    }

    public void Dispose()
    {
        Clear();
        preview.Cleanup();
    }

    private void Hide()
    {
        foreach (var child in instance.GetComponentsInChildren<Transform>(true))
            child.gameObject.hideFlags = HideFlags.HideAndDontSave;
    }

    // The animator's standing idle: the first layer that starts on a motion, and if that is a blend tree, its child at
    // the lowest threshold (speed 0 on a locomotion blend).
    private void Pose()
    {
        var animator = instance.GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        var clip = IdleClip(animator.runtimeAnimatorController as AnimatorController);
        if (clip != null) clip.SampleAnimation(animator.gameObject, 0f);
        head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
    }

    private static AnimationClip IdleClip(AnimatorController controller)
    {
        if (controller == null) return null;

        foreach (var layer in controller.layers)
        {
            var state = layer.stateMachine.defaultState;
            if (state != null && state.motion != null) return Standing(state.motion);
        }

        return null;
    }

    private static AnimationClip Standing(Motion motion)
    {
        if (motion is AnimationClip clip) return clip;
        if (!(motion is BlendTree tree) || tree.children.Length == 0) return null;

        var lowest = tree.children.OrderBy(c => c.threshold).First();
        return lowest.motion != null ? Standing(lowest.motion) : null;
    }

    // Measured once, facing forward; turning spins the model about its root, so only the height is used.
    private void Measure()
    {
        var renderers = instance.GetComponentsInChildren<Renderer>();
        bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(instance.transform.position, Vector3.one);
        foreach (var renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
    }

    private void Turn(PointerMoveEvent e)
    {
        if (!view.HasPointerCapture(e.pointerId)) return;

        yaw -= e.deltaPosition.x * DegreesPerPixel;
        view.MarkDirtyRepaint();
    }

    private void Draw()
    {
        Rect rect = new Rect(Vector2.zero, view.contentRect.size);
        if (Event.current.type != EventType.Repaint || rect.width < 1f || rect.height < 1f) return;

        preview.BeginPreview(rect, GUIStyle.none);
        if (instance != null) Aim();
        preview.Render(true);
        preview.EndAndDrawPreview(rect);
    }

    // The camera stays in front (+Z, looking back) and the model turns, so the lights stay put relative to the view.
    private void Aim()
    {
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 target = Target();
        float height = framing == Framing.Face ? FaceHeight : bounds.size.y;
        float distance = height * Padding * 0.5f / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);

        var camera = preview.camera;
        camera.nearClipPlane = 0.02f;
        camera.farClipPlane = distance + bounds.size.magnitude * 2f;
        camera.transform.position = target + Vector3.forward * distance;
        camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
    }

    private Vector3 Target()
    {
        Vector3 root = instance.transform.position;
        if (framing == Framing.Body) return new Vector3(root.x, bounds.center.y, root.z);
        if (head != null) return head.position + Vector3.up * HeadLift;

        return new Vector3(root.x, bounds.max.y - FaceHeight * 0.5f, root.z);
    }

    private void Clear()
    {
        if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
        instance = null;
        head = null;
    }
}
