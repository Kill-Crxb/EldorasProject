using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Tools → Characters → Build Creation Preview. In MenuScene: a stage far below the menu (turntable +
// camera), a Preview image on the creation panel, the panel's preview field, and the appearance
// rows moved beside it. Safe to run again: it finds what it made last time and only re-wires.
public static class CreationPreviewBuilder
{
    const string ScenePath = "Assets/- Scenes/MenuScene.unity";
    const string StageName = "CreationPreviewStage";
    const string PreviewName = "Preview";

    static readonly Vector3 StagePosition = new Vector3(0f, -1000f, 0f);
    static readonly Vector3 CameraOffset = new Vector3(0f, 0.9f, 3.8f); // frames a 1.7–1.9 m character
    const float FieldOfView = 30f;

    static readonly Vector2 PreviewPosition = new Vector2(100f, 20f);
    static readonly Vector2 PreviewSize = new Vector2(600f, 900f);
    static readonly Vector2 RowsPosition = new Vector2(640f, 300f);
    static readonly Vector2 RowsSize = new Vector2(440f, 300f);

    [MenuItem("Tools/Characters/Build Creation Preview")]
    static void Build()
    {
        if (!OpenMenuScene()) return;

        var panel = Object.FindAnyObjectByType<CharacterCreationPanel>(FindObjectsInactive.Include);
        if (panel == null) { Log("no CharacterCreationPanel in the scene; nothing done."); return; }

        Transform stage = FindOrCreate(null, StageName);
        stage.position = StagePosition;
        Transform turntable = FindOrCreate(stage, "Turntable");
        Camera camera = BuildCamera(stage);

        CreationPreview preview = BuildPreviewImage(panel.transform);
        SetReference(preview, "turntable", turntable);
        SetReference(preview, "previewCamera", camera);
        SetReference(panel, "preview", preview);
        PlaceRows(panel);

        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        EditorSceneManager.SaveScene(panel.gameObject.scene);
        Log("Done. Open character creation: drag the image to turn her; the hair row changes her live.");
    }

    static bool OpenMenuScene()
    {
        if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(ScenePath);
        return true;
    }

    // Off until CreationPreview hands it a texture, or it would draw over the menu.
    static Camera BuildCamera(Transform stage)
    {
        Transform holder = FindOrCreate(stage, "PreviewCamera");
        holder.localPosition = CameraOffset;
        holder.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var camera = holder.GetComponent<Camera>();
        if (camera == null) camera = holder.gameObject.AddComponent<Camera>();

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.fieldOfView = FieldOfView;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 20f;
        camera.enabled = false;

        var urp = camera.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = false;
        urp.renderShadows = false;
        return camera;
    }

    static CreationPreview BuildPreviewImage(Transform panel)
    {
        Transform existing = panel.Find(PreviewName);
        if (existing != null) return existing.GetComponent<CreationPreview>();

        var go = new GameObject(PreviewName, typeof(RectTransform), typeof(RawImage), typeof(CreationPreview));
        var rect = (RectTransform)go.transform;
        rect.SetParent(panel, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = PreviewPosition;
        rect.sizeDelta = PreviewSize;

        // Just above the panel's background, below everything else.
        Transform background = panel.Find("CharacterBG");
        rect.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 : 0);

        Log($"made {PreviewName} on the creation panel ({PreviewSize.x}×{PreviewSize.y}). Move it freely; the texture follows its shape.");
        return go.GetComponent<CreationPreview>();
    }

    // Only moves the rows while they still sit where they were created (centre, 100×100).
    static void PlaceRows(CharacterCreationPanel panel)
    {
        var rows = new SerializedObject(panel).FindProperty("rowParent").objectReferenceValue as RectTransform;
        if (rows == null || rows.sizeDelta != new Vector2(100f, 100f) || rows.anchoredPosition != Vector2.zero) return;

        rows.pivot = new Vector2(0.5f, 1f);
        rows.anchoredPosition = RowsPosition;
        rows.sizeDelta = RowsSize;
        Log("appearance rows moved to the right of the preview.");
    }

    static Transform FindOrCreate(Transform parent, string name)
    {
        Transform found = parent != null ? parent.Find(name) : FindRoot(name);
        if (found != null) return found;

        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Transform FindRoot(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name) return root.transform;
        return null;
    }

    static void SetReference(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Log(string message) => Debug.Log($"[CreationPreview] {message}");
}
