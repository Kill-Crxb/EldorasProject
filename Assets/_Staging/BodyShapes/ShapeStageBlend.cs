using System.Collections.Generic;
using UnityEngine;

// Staging test: slides a staged shape-key family (see StagedShapeKeys) continuously instead of by step, fading
// between the two keys either side of the value. The shipped path stays the stepped creation slider; this is
// here to judge whether the in-between shapes hold up. Put it on the model root and drag Value, in edit mode or
// play. It writes every renderer carrying the family (body and clothing) after ModelAppearance, so it wins while
// enabled; switch it off to get the stepped shape back.

[ExecuteAlways]
public class ShapeStageBlend : MonoBehaviour
{
    private class Stage
    {
        public int percent;
        public string shape;
    }

    [Tooltip("The family's name before the percent, e.g. Chest for 'Chest 10%'.")]
    [SerializeField] private string family = "Chest";

    [Range(0f, 100f)] [SerializeField] private float value;

    private readonly List<Stage> stages = new List<Stage>();
    private SkinnedMeshRenderer[] renderers;
    private float applied = -1f;

    private void OnEnable()
    {
        renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        stages.Clear();
        applied = -1f;
    }

    private void OnValidate()
    {
        stages.Clear();
        applied = -1f;
    }

    private void LateUpdate()
    {
        if (value == applied || renderers == null) return;
        if (stages.Count == 0) ReadStages();
        if (stages.Count < 2) return;

        applied = value;
        int upper = UpperStage();
        Stage low = stages[upper - 1];
        Stage high = stages[upper];
        float t = Mathf.InverseLerp(low.percent, high.percent, value);

        foreach (var renderer in renderers)
            ApplyTo(renderer, low, high, t);
    }

    private void ReadStages()
    {
        stages.Add(new Stage { percent = 0, shape = null });
        foreach (var renderer in renderers)
            if (stages.Count == 1) AddStages(renderer.sharedMesh);

        stages.Sort((a, b) => a.percent.CompareTo(b.percent));
        if (stages.Count < 2) Debug.LogError($"[ShapeStageBlend] No '{family} N%' shape keys on {name}.", this);
    }

    private void AddStages(Mesh mesh)
    {
        if (mesh == null) return;

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string shape = mesh.GetBlendShapeName(i);
            if (StagedShapeKeys.Family(shape) == family) stages.Add(new Stage { percent = StagedShapeKeys.Percent(shape), shape = shape });
        }
    }

    private int UpperStage()
    {
        for (int i = 1; i < stages.Count; i++)
            if (value <= stages[i].percent) return i;

        return stages.Count - 1;
    }

    private void ApplyTo(SkinnedMeshRenderer renderer, Stage low, Stage high, float t)
    {
        Mesh mesh = renderer.sharedMesh;
        if (mesh == null) return;

        foreach (var stage in stages)
        {
            int index = stage.shape == null ? -1 : mesh.GetBlendShapeIndex(stage.shape);
            if (index >= 0) renderer.SetBlendShapeWeight(index, Weight(stage, low, high, t));
        }
    }

    private static float Weight(Stage stage, Stage low, Stage high, float t)
    {
        if (stage == high) return t * 100f;
        if (stage == low) return (1f - t) * 100f;
        return 0f;
    }
}
