using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// AnimatorDebugger
/// 
/// Attach to your player entity (or any GameObject with an Animator in children).
/// Shows in the Inspector and as an on-screen overlay:
///   - Which Animator is being read
///   - All parameter values live
///   - Active state per layer
///   - Whether key bones are actually moving (position delta per frame)
/// 
/// Usage:
///   1. Add to your player entity root
///   2. Assign the Animator (or leave null to auto-find in children)
///   3. Optionally add bone transforms to track in BonesToTrack
///   4. Enable showOnScreen for in-game overlay
/// </summary>
public class AnimatorDebugger : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Leave null to auto-find in children")]
    [SerializeField] private Animator targetAnimator;

    [Header("Bone Movement Tracking")]
    [Tooltip("Drag bones here to track their world position delta each frame")]
    [SerializeField] private Transform[] bonesToTrack = new Transform[0];

    [Header("Display")]
    [SerializeField] private bool showOnScreen = true;
    [SerializeField] private bool showParameters = true;
    [SerializeField] private bool showLayers = true;
    [SerializeField] private bool showBoneMovement = true;
    [SerializeField] private Key toggleKey = Key.Tab;

    // Runtime state
    private Vector3[] lastBonePositions;
    private float[] boneDeltas;
    private string onScreenText = "";
    private bool visible = true;

    // GUI style (built once)
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private bool stylesBuilt;

    void Start()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>();

        if (targetAnimator == null)
            Debug.LogWarning("[AnimatorDebugger] No Animator found. Assign one or place this on a GameObject with an Animator in children.");

        InitBoneTracking();
    }

    void InitBoneTracking()
    {
        if (bonesToTrack == null || bonesToTrack.Length == 0) return;
        lastBonePositions = new Vector3[bonesToTrack.Length];
        boneDeltas = new float[bonesToTrack.Length];
        for (int i = 0; i < bonesToTrack.Length; i++)
            if (bonesToTrack[i] != null)
                lastBonePositions[i] = bonesToTrack[i].position;
    }

    void Update()
    {
        try
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
                visible = !visible;
        }
        catch { /* invalid key value in inspector */ }

        if (targetAnimator == null || !visible) return;

        UpdateBoneDeltas();
        BuildDebugText();
    }

    void UpdateBoneDeltas()
    {
        if (bonesToTrack == null || lastBonePositions == null) return;
        for (int i = 0; i < bonesToTrack.Length; i++)
        {
            if (bonesToTrack[i] == null) continue;
            Vector3 current = bonesToTrack[i].position;
            boneDeltas[i] = (current - lastBonePositions[i]).magnitude;
            lastBonePositions[i] = current;
        }
    }

    void BuildDebugText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>AnimatorDebugger</b>  [{targetAnimator.gameObject.name}]");
        sb.AppendLine($"Enabled: {targetAnimator.enabled}  |  Speed: {targetAnimator.speed:F2}  |  RootMotion: {targetAnimator.applyRootMotion}");
        sb.AppendLine($"Controller: {(targetAnimator.runtimeAnimatorController != null ? targetAnimator.runtimeAnimatorController.name : "<none>")}");
        sb.AppendLine();

        if (showLayers)
        {
            sb.AppendLine("<b>── Layers ──</b>");
            for (int i = 0; i < targetAnimator.layerCount; i++)
            {
                var info = targetAnimator.GetCurrentAnimatorStateInfo(i);
                var clip = targetAnimator.GetCurrentAnimatorClipInfo(i);
                string clipName = clip.Length > 0 ? clip[0].clip?.name ?? "?" : "none";
                float weight = targetAnimator.GetLayerWeight(i);
                sb.AppendLine($"  [{i}] {targetAnimator.GetLayerName(i)}  w={weight:F2}");
                sb.AppendLine($"      State hash: {info.shortNameHash}  clip: {clipName}");
                sb.AppendLine($"      NormalizedTime: {info.normalizedTime:F3}  loop: {info.loop}");
            }
            sb.AppendLine();
        }

        if (showParameters)
        {
            sb.AppendLine("<b>── Parameters ──</b>");
            foreach (var p in targetAnimator.parameters)
            {
                string val = p.type switch
                {
                    AnimatorControllerParameterType.Float => targetAnimator.GetFloat(p.name).ToString("F3"),
                    AnimatorControllerParameterType.Int => targetAnimator.GetInteger(p.name).ToString(),
                    AnimatorControllerParameterType.Bool => targetAnimator.GetBool(p.name).ToString(),
                    AnimatorControllerParameterType.Trigger => "(trigger)",
                    _ => "?"
                };
                sb.AppendLine($"  {p.name} ({p.type}): {val}");
            }
            sb.AppendLine();
        }

        if (showBoneMovement && bonesToTrack != null && bonesToTrack.Length > 0)
        {
            sb.AppendLine("<b>── Bone Movement (delta/frame) ──</b>");
            for (int i = 0; i < bonesToTrack.Length; i++)
            {
                if (bonesToTrack[i] == null) continue;
                string moving = boneDeltas[i] > 0.0001f ? "MOVING" : "STILL";
                sb.AppendLine($"  {bonesToTrack[i].name}: {boneDeltas[i]:F5}  [{moving}]");
            }
        }

        onScreenText = sb.ToString();
    }

    void OnGUI()
    {
        if (!showOnScreen || !visible || string.IsNullOrEmpty(onScreenText)) return;

        BuildStyles();

        float w = 700f;
        float x = 10f;
        float y = 10f;

        // Measure height
        float h = labelStyle.CalcHeight(new GUIContent(onScreenText), w - 20f) + 20f;

        GUI.Box(new Rect(x, y, w, h), GUIContent.none, boxStyle);
        GUI.Label(new Rect(x + 10f, y + 10f, w - 20f, h - 20f), onScreenText, labelStyle);
    }

    void BuildStyles()
    {
        if (stylesBuilt) return;
        stylesBuilt = true;

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.75f));

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontSize = 18;
        labelStyle.richText = true;
        labelStyle.wordWrap = true;
    }

    Texture2D MakeTex(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h);
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }

    // ── Inspector context menu helpers ──────────────────────────────────────

    [ContextMenu("Auto-populate bones from Humanoid Avatar")]
    void AutoPopulateBones()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>();

        if (targetAnimator == null || !targetAnimator.isHuman)
        {
            Debug.LogWarning("[AnimatorDebugger] No humanoid Animator found.");
            return;
        }

        var bones = new System.Collections.Generic.List<Transform>
        {
            targetAnimator.GetBoneTransform(HumanBodyBones.Hips),
            targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
            targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
            targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
            targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg),
            targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot),
            targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot),
            targetAnimator.GetBoneTransform(HumanBodyBones.Spine),
        };

        bones.RemoveAll(b => b == null);
        bonesToTrack = bones.ToArray();
        InitBoneTracking();
        Debug.Log($"[AnimatorDebugger] Auto-populated {bonesToTrack.Length} humanoid bones.");
    }

    [ContextMenu("Log Full Animator State")]
    void LogFullState()
    {
        if (targetAnimator == null) { Debug.LogWarning("No animator."); return; }
        Debug.Log(onScreenText.Replace("<b>", "").Replace("</b>", ""));
    }
}