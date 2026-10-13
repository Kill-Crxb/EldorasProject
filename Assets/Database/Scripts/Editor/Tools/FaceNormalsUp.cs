using UnityEngine;
using UnityEditor;
using System.IO;

public static class FaceNormalsUp
{
    [MenuItem("Tools/Force Selected Mesh Normals Up")]
    private static void ForceUp()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("Force Normals Up: No GameObject selected.");
            return;
        }

        MeshFilter meshFilter = selected.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning(
                "Force Normals Up: Selected GameObject does not have a MeshFilter with a mesh."
            );
            return;
        }

        Mesh sourceMesh = meshFilter.sharedMesh;

        // ------------------------------------------------------------
        // Find the FBX containing this mesh.
        // ------------------------------------------------------------

        string[] modelGuids = AssetDatabase.FindAssets("t:Model");

        string fbxPath = null;

        foreach (string guid in modelGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                continue;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (Object asset in assets)
            {
                if (asset == sourceMesh)
                {
                    fbxPath = path;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(fbxPath))
                break;
        }

        if (string.IsNullOrEmpty(fbxPath))
        {
            Debug.LogError(
                $"Force Normals Up: Could not find the FBX containing mesh '{sourceMesh.name}'."
            );

            return;
        }

        // ------------------------------------------------------------
        // Determine output path.
        // ------------------------------------------------------------

        string directory = Path.GetDirectoryName(fbxPath);

        string fileName = Path.GetFileNameWithoutExtension(fbxPath);

        string newAssetPath = Path.Combine(
            directory,
            fileName + "_NormalsUp.asset"
        ).Replace("\\", "/");

        // ------------------------------------------------------------
        // Handle existing asset.
        // ------------------------------------------------------------

        Mesh existingMesh =
            AssetDatabase.LoadAssetAtPath<Mesh>(newAssetPath);

        if (existingMesh != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Normals Up Already Exists",
                $"A mesh already exists at:\n\n{newAssetPath}\n\nReplace it?",
                "Replace",
                "Cancel"
            );

            if (!overwrite)
                return;

            AssetDatabase.DeleteAsset(newAssetPath);
        }

        // ------------------------------------------------------------
        // Create independent mesh.
        // ------------------------------------------------------------

        Mesh newMesh = Object.Instantiate(sourceMesh);

        newMesh.name = fileName + "_NormalsUp";

        // Force every vertex normal to point straight up.
        Vector3[] normals = new Vector3[newMesh.vertexCount];

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.up;
        }

        newMesh.normals = normals;

        // ------------------------------------------------------------
        // Save beside FBX.
        // ------------------------------------------------------------

        AssetDatabase.CreateAsset(newMesh, newAssetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ------------------------------------------------------------
        // Assign new mesh.
        // ------------------------------------------------------------

        Undo.RecordObject(
            meshFilter,
            "Assign Normals Up Mesh"
        );

        meshFilter.sharedMesh = newMesh;

        EditorUtility.SetDirty(meshFilter);

        // Select and ping the new asset.
        Selection.activeObject = newMesh;

        EditorGUIUtility.PingObject(newMesh);

        Debug.Log(
            $"Force Normals Up: Created\n" +
            $"{newAssetPath}\n\n" +
            $"Vertices: {newMesh.vertexCount}\n" +
            $"All normals set to Vector3.up."
        );
    }

    [MenuItem("Tools/Force Selected Mesh Normals Up", true)]
    private static bool ValidateForceUp()
    {
        GameObject selected = Selection.activeGameObject;

        return selected != null &&
               selected.GetComponent<MeshFilter>() != null &&
               selected.GetComponent<MeshFilter>().sharedMesh != null;
    }
}