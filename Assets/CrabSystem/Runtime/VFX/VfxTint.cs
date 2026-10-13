using UnityEngine;

/// <summary>
/// Recolouring an effect to a school's colour, wherever that effect lives.
///
/// Extracted from ProjectileVisual the moment a second thing needed it. There are three places
/// a colour can hide in a Unity effect and getting any of them wrong makes a tint silently do
/// nothing:
///
///   Renderer        MaterialPropertyBlock, never renderer.material — the latter instances a
///                   material per object and leaks under pooling.
///   ParticleSystem  main.startColor. A particle shader multiplies by VERTEX colour, so a
///                   MaterialPropertyBlock on a ParticleSystemRenderer usually does nothing at
///                   all. This is where a VFX asset's colour actually lives.
///   Light           light.color, straight.
/// </summary>
public static class VfxTint
{
    /// <summary>
    /// Take the HUE and SATURATION from the tint, keep the original's BRIGHTNESS and its
    /// relative saturation.
    ///
    /// A flat replace would work, and it would look wrong. A fireball is not one orange — it is
    /// a near-white core inside saturated embers inside dark smoke, and that brightness ladder
    /// is what makes it read as fire rather than as a coloured blob. Replacing every layer with
    /// the same blue flattens it into a sticker.
    ///
    /// Keeping V means the core stays the brightest thing; scaling the original's S by the
    /// tint's means a pale centre stays pale. The shape survives, only the colour moves.
    /// </summary>
    public static Color Recolor(Color original, Color tint)
    {
        Color.RGBToHSV(original, out _, out float originalS, out float originalV);
        Color.RGBToHSV(tint, out float tintH, out float tintS, out _);

        Color result = Color.HSVToRGB(tintH, tintS * originalS, originalV);
        result.a = original.a;

        return result;
    }

    /// <summary>
    /// Tint a freshly instantiated effect and forget about it. For spawn-and-destroy VFX, where
    /// the instance is new so its current colours ARE the prefab's.
    ///
    /// Do not call this repeatedly on the same instance — tinting an already-tinted result
    /// compounds, and a pooled object would drift to black. Pooled things cache their base
    /// colours and use the per-type methods below.
    /// </summary>
    public static void ApplyOnce(GameObject root, Color tint, string colorProperty)
    {
        if (root == null) return;

        var particles = root.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;

            // A GRADIENT is a deliberate colour design — white-hot fading to smoke — and
            // recolouring it would need the whole ramp rebuilt. A flat colour is a tint slot.
            if (main.startColor.mode != ParticleSystemGradientMode.Color) continue;

            main.startColor = Recolor(main.startColor.color, tint);
        }

        var lights = root.GetComponentsInChildren<Light>(true);

        for (int i = 0; i < lights.Length; i++)
            lights[i].color = Recolor(lights[i].color, tint);

        if (string.IsNullOrEmpty(colorProperty)) return;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var block = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            renderers[i].GetPropertyBlock(block);
            block.SetColor(colorProperty, tint);
            renderers[i].SetPropertyBlock(block);
        }
    }

    // =========================================================================
    // Cached forms — for pooled objects that re-tint every reuse
    // =========================================================================

    public static void TintRenderers(Renderer[] renderers, MaterialPropertyBlock block,
                                     Color tint, string colorProperty)
    {
        if (renderers == null || block == null) return;
        if (string.IsNullOrEmpty(colorProperty)) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            renderers[i].GetPropertyBlock(block);
            block.SetColor(colorProperty, tint);
            renderers[i].SetPropertyBlock(block);
        }
    }

    /// <summary>Base colours are the PREFAB's, captured once — never the last tint applied.</summary>
    public static void TintParticles(ParticleSystem[] particles, Color[] baseColors, Color tint)
    {
        if (particles == null || baseColors == null) return;

        for (int i = 0; i < particles.Length && i < baseColors.Length; i++)
        {
            if (particles[i] == null) continue;

            ParticleSystem.MainModule main = particles[i].main;

            if (main.startColor.mode != ParticleSystemGradientMode.Color) continue;

            main.startColor = Recolor(baseColors[i], tint);
        }
    }

    public static void TintLights(Light[] lights, Color[] baseColors, Color tint)
    {
        if (lights == null || baseColors == null) return;

        for (int i = 0; i < lights.Length && i < baseColors.Length; i++)
        {
            if (lights[i] == null) continue;

            lights[i].color = Recolor(baseColors[i], tint);
        }
    }

    /// <summary>The flat start colour of each system, for capturing at build time.</summary>
    public static Color[] CaptureParticleColors(ParticleSystem[] particles)
    {
        if (particles == null) return new Color[0];

        var colors = new Color[particles.Length];

        for (int i = 0; i < particles.Length; i++)
            colors[i] = particles[i] != null ? particles[i].main.startColor.color : Color.white;

        return colors;
    }

    public static Color[] CaptureLightColors(Light[] lights)
    {
        if (lights == null) return new Color[0];

        var colors = new Color[lights.Length];

        for (int i = 0; i < lights.Length; i++)
            colors[i] = lights[i] != null ? lights[i].color : Color.white;

        return colors;
    }
}
