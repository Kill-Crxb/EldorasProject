// Editor-only. Put this file inside any folder named "Editor".
//
// Bakes averaged ("smoothed") normals into UV3 on model import so the inverted-hull
// outline has a continuous extrude direction. Without it, every hard edge and UV split
// tears the hull open — the outline comes out dashed and speckled instead of solid.
//
// Usage: select the FBX assets in the Project window, then
//   Tools > Toon > Enable Smooth Normals On Selection
// The flag is stored on the importer, so it survives reimports and source-file changes.
// Then tick "Extrude Along Baked Normals (UV3)" on the FlatToon material.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ToonSmoothNormals
{
    public const string Flag = "toonSmoothNormals";
    public const int UVChannel = 3;

    const float QuantizeScale = 10000f;

    public static void Bake(Mesh mesh)
    {
        if (mesh == null)
            return;

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        if (normals == null || normals.Length != vertices.Length)
            return;

        Dictionary<Vector3, Vector3> sums = new Dictionary<Vector3, Vector3>(vertices.Length);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 key = Quantize(vertices[i]);
            Vector3 current;
            sums.TryGetValue(key, out current);
            sums[key] = current + normals[i];
        }

        List<Vector3> smoothed = new List<Vector3>(vertices.Length);

        for (int i = 0; i < vertices.Length; i++)
            smoothed.Add(sums[Quantize(vertices[i])].normalized);

        mesh.SetUVs(UVChannel, smoothed);
    }

    static Vector3 Quantize(Vector3 v)
    {
        float x = Mathf.Round(v.x * QuantizeScale);
        float y = Mathf.Round(v.y * QuantizeScale);
        float z = Mathf.Round(v.z * QuantizeScale);
        return new Vector3(x, y, z);
    }

    [MenuItem("Tools/Toon/Enable Smooth Normals On Selection")]
    static void Enable()
    {
        ApplyToSelection(true);
    }

    [MenuItem("Tools/Toon/Disable Smooth Normals On Selection")]
    static void Disable()
    {
        ApplyToSelection(false);
    }

    static void ApplyToSelection(bool enabled)
    {
        Object[] selected = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
        int changed = 0;

        for (int i = 0; i < selected.Length; i++)
            changed += SetFlag(AssetDatabase.GetAssetPath(selected[i]), enabled) ? 1 : 0;

        Debug.Log("[ToonSmoothNormals] " + (enabled ? "enabled" : "disabled") + " on " + changed + " model(s).");
    }

    static bool SetFlag(string assetPath, bool enabled)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;

        if (importer == null)
            return false;

        bool already = importer.userData.Contains(Flag);

        if (already == enabled)
            return false;

        importer.userData = enabled ? importer.userData + Flag : importer.userData.Replace(Flag, "");
        importer.SaveAndReimport();
        return true;
    }
}

public class ToonSmoothNormalsPostprocessor : AssetPostprocessor
{
    void OnPostprocessModel(GameObject root)
    {
        if (!assetImporter.userData.Contains(ToonSmoothNormals.Flag))
            return;

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < filters.Length; i++)
            ToonSmoothNormals.Bake(filters[i].sharedMesh);

        SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        for (int i = 0; i < skinned.Length; i++)
            ToonSmoothNormals.Bake(skinned[i].sharedMesh);
    }
}
