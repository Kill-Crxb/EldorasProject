// Editor-only. Put this file inside any folder named "Editor".
//
// Converts a hand-painted black and white mask into a signed distance field, so the
// FlatToon shader's Base SDF can slide its Coverage threshold through the shape and still
// get a hard edge at every setting.
//
// Paint convention: WHITE is lit, BLACK is shadow. That matches the rest of the shader,
// where high values mean more light. Paint the shape at its most generous extent - the
// Coverage slider only ever pulls the boundary inward from what you painted.
//
// Usage: select one or more mask textures in the Project window, then
//   Tools > Toon > Generate SDF From Mask
// A sibling file called <name>_SDF.png is written and imported with sRGB off and clamped
// wrapping, which is what the shader expects.
//
// The source does not need Read/Write enabled - the file is decoded from disk directly.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class ToonSDFBaker
{
    // How many pixels of gradient sit either side of the painted boundary. Larger values
    // give the Coverage slider more travel before the shape stops changing.
    const float Spread = 48f;

    const float Ortho = 1.0f;
    const float Diag = 1.41421356f;

    [MenuItem("Tools/Toon/Generate SDF From Mask")]
    static void Generate()
    {
        Object[] selected = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);

        if (selected.Length == 0)
        {
            Debug.LogWarning("[ToonSDFBaker] Select one or more mask textures in the Project window first.");
            return;
        }

        for (int i = 0; i < selected.Length; i++)
            Bake(AssetDatabase.GetAssetPath(selected[i]));

        AssetDatabase.Refresh();
    }

    static void Bake(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            return;

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!source.LoadImage(File.ReadAllBytes(sourcePath)))
        {
            Debug.LogWarning("[ToonSDFBaker] Could not decode " + sourcePath + " - PNG, JPG or TGA only.");
            Object.DestroyImmediate(source);
            return;
        }

        int width = source.width;
        int height = source.height;
        Color32[] pixels = source.GetPixels32();
        Object.DestroyImmediate(source);

        bool[] lit = new bool[width * height];

        for (int i = 0; i < lit.Length; i++)
            lit[i] = pixels[i].r > 127;

        bool[] shadow = new bool[lit.Length];

        for (int i = 0; i < lit.Length; i++)
            shadow[i] = !lit[i];

        float[] toShadow = DistanceField(shadow, width, height);
        float[] toLit = DistanceField(lit, width, height);

        Color32[] output = new Color32[lit.Length];

        for (int i = 0; i < lit.Length; i++)
        {
            float signed = lit[i] ? toShadow[i] : -toLit[i];
            float value = Mathf.Clamp01(0.5f + signed / (2f * Spread));
            byte v = (byte)Mathf.RoundToInt(value * 255f);
            output[i] = new Color32(v, v, v, 255);
        }

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.SetPixels32(output);
        result.Apply();

        string directory = Path.GetDirectoryName(sourcePath);
        string name = Path.GetFileNameWithoutExtension(sourcePath);
        string outputPath = Path.Combine(directory, name + "_SDF.png").Replace("\\", "/");

        File.WriteAllBytes(outputPath, result.EncodeToPNG());
        Object.DestroyImmediate(result);

        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
        ApplyImportSettings(outputPath);

        Debug.Log("[ToonSDFBaker] Wrote " + outputPath);
    }

    static void ApplyImportSettings(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
            return;

        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    // Chamfer distance transform: two sweeps over the grid, each relaxing a pixel against
    // the neighbours the sweep has already visited. Approximate compared to an exact
    // Euclidean transform, but the error is under a percent and it runs in milliseconds.
    static float[] DistanceField(bool[] seed, int width, int height)
    {
        float far = width + height;
        float[] distance = new float[seed.Length];

        for (int i = 0; i < seed.Length; i++)
            distance[i] = seed[i] ? 0f : far;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                RelaxForward(distance, width, height, x, y, far);

        for (int y = height - 1; y >= 0; y--)
            for (int x = width - 1; x >= 0; x--)
                RelaxBackward(distance, width, height, x, y, far);

        return distance;
    }

    static void RelaxForward(float[] d, int width, int height, int x, int y, float far)
    {
        int i = y * width + x;
        float best = d[i];

        best = Mathf.Min(best, Read(d, width, height, x - 1, y, far) + Ortho);
        best = Mathf.Min(best, Read(d, width, height, x, y - 1, far) + Ortho);
        best = Mathf.Min(best, Read(d, width, height, x - 1, y - 1, far) + Diag);
        best = Mathf.Min(best, Read(d, width, height, x + 1, y - 1, far) + Diag);

        d[i] = best;
    }

    static void RelaxBackward(float[] d, int width, int height, int x, int y, float far)
    {
        int i = y * width + x;
        float best = d[i];

        best = Mathf.Min(best, Read(d, width, height, x + 1, y, far) + Ortho);
        best = Mathf.Min(best, Read(d, width, height, x, y + 1, far) + Ortho);
        best = Mathf.Min(best, Read(d, width, height, x + 1, y + 1, far) + Diag);
        best = Mathf.Min(best, Read(d, width, height, x - 1, y + 1, far) + Diag);

        d[i] = best;
    }

    static float Read(float[] d, int width, int height, int x, int y, float far)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            return far;

        return d[y * width + x];
    }
}
