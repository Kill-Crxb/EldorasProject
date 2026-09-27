// Pushes a Unity Terrain's splat map, layer textures and layer tiling to shader globals,
// so any shader can ask what colour the ground is at a world position. Grass uses it to
// blend its root into the terrain; the terrain shader will use the same globals later.
//
// Setup: drop this on the Terrain GameObject. Nothing to assign.
//
// Four layers maximum - one splat control texture covers four, and past four Unity starts
// adding passes, which the flat-shading spec rules out anyway.

using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class TerrainColorGlobals : MonoBehaviour
{
    const int MaxLayers = 4;

    static readonly int ControlId = Shader.PropertyToID("_TerrainControl");
    static readonly int OriginSizeId = Shader.PropertyToID("_TerrainOriginSize");
    static readonly int LayerCountId = Shader.PropertyToID("_TerrainLayerCount");

    static readonly int[] TexIds =
    {
        Shader.PropertyToID("_TerrainLayerTex0"),
        Shader.PropertyToID("_TerrainLayerTex1"),
        Shader.PropertyToID("_TerrainLayerTex2"),
        Shader.PropertyToID("_TerrainLayerTex3"),
    };

    static readonly int[] TilingIds =
    {
        Shader.PropertyToID("_TerrainLayerTiling0"),
        Shader.PropertyToID("_TerrainLayerTiling1"),
        Shader.PropertyToID("_TerrainLayerTiling2"),
        Shader.PropertyToID("_TerrainLayerTiling3"),
    };

    static readonly int[] TintIds =
    {
        Shader.PropertyToID("_TerrainLayerTint0"),
        Shader.PropertyToID("_TerrainLayerTint1"),
        Shader.PropertyToID("_TerrainLayerTint2"),
        Shader.PropertyToID("_TerrainLayerTint3"),
    };

    Terrain terrain;

    void OnEnable()
    {
        terrain = GetComponent<Terrain>();
        Push();
    }

    // Painting the terrain in the editor fires no callback, so re-push while not playing.
    // At runtime the terrain does not change, so OnEnable is enough.
    void Update()
    {
        if (Application.isPlaying)
            return;

        Push();
    }

    [ContextMenu("Push To Shader Globals")]
    public void Push()
    {
        if (terrain == null)
            terrain = GetComponent<Terrain>();

        TerrainData data = terrain.terrainData;

        if (data == null)
            return;

        if (data.alphamapTextureCount == 0)
            return;

        Vector3 origin = terrain.GetPosition();
        Vector3 size = data.size;

        Shader.SetGlobalTexture(ControlId, data.alphamapTextures[0]);
        Shader.SetGlobalVector(OriginSizeId, new Vector4(origin.x, origin.z, size.x, size.z));

        TerrainLayer[] layers = data.terrainLayers;
        int count = Mathf.Min(layers.Length, MaxLayers);

        Shader.SetGlobalFloat(LayerCountId, count);

        for (int i = 0; i < count; i++)
            PushLayer(i, layers[i]);

        // Unassigned globals keep whatever the last terrain left behind, so clear the
        // tail. Weight is zero on those channels, but a stale texture reference is worse
        // than a white one if a control map ever disagrees.
        for (int i = count; i < MaxLayers; i++)
            ClearLayer(i);
    }

    void PushLayer(int index, TerrainLayer layer)
    {
        if (layer == null)
        {
            ClearLayer(index);
            return;
        }

        Vector2 tileSize = layer.tileSize;
        tileSize.x = Mathf.Approximately(tileSize.x, 0f) ? 1f : tileSize.x;
        tileSize.y = Mathf.Approximately(tileSize.y, 0f) ? 1f : tileSize.y;

        Shader.SetGlobalTexture(TexIds[index], layer.diffuseTexture != null ? layer.diffuseTexture : Texture2D.whiteTexture);
        Shader.SetGlobalVector(TilingIds[index], new Vector4(tileSize.x, tileSize.y, layer.tileOffset.x, layer.tileOffset.y));
        Shader.SetGlobalVector(TintIds[index], layer.diffuseRemapMax == Vector4.zero ? Vector4.one : layer.diffuseRemapMax);
    }

    void ClearLayer(int index)
    {
        Shader.SetGlobalTexture(TexIds[index], Texture2D.whiteTexture);
        Shader.SetGlobalVector(TilingIds[index], new Vector4(1f, 1f, 0f, 0f));
        Shader.SetGlobalVector(TintIds[index], Vector4.one);
    }
}
