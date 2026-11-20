using System.Collections.Generic;
using UnityEngine;

// Shared by HitFlash and PerilousTell. A flash writes into each renderer's own block, so whatever else sits on it (the
// hair tint from ModelAppearance) survives. On release, a renderer that had no block before the flash is cleared so it
// rejoins the SRP batcher; one that already had a block keeps it, with the flash back at 0.
public static class FlashBlocks
{
    public static void Remember(List<Renderer> renderers, HashSet<Renderer> hadBlock)
    {
        hadBlock.Clear();
        foreach (Renderer r in renderers)
        {
            if (r != null && r.HasPropertyBlock()) hadBlock.Add(r);
        }
    }

    public static void Write(List<Renderer> renderers, MaterialPropertyBlock block, int flashId, float value, int colourId = -1, Color colour = default)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(block);
            block.SetFloat(flashId, value);
            if (colourId != -1) block.SetColor(colourId, colour);
            r.SetPropertyBlock(block);
        }
    }

    public static void Release(List<Renderer> renderers, HashSet<Renderer> hadBlock, MaterialPropertyBlock block, int flashId)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;

            if (!hadBlock.Contains(r))
            {
                r.SetPropertyBlock(null);
                continue;
            }

            r.GetPropertyBlock(block);
            block.SetFloat(flashId, 0f);
            r.SetPropertyBlock(block);
        }
        hadBlock.Clear();
    }
}
