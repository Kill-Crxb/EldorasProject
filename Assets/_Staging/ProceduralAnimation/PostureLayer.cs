using System.Collections.Generic;
using Unity.Collections;

// The posture stack inside ProceduralPoseModule. Any number of sources (the model's base posture, a status, an
// equipped item) each add their profile at their own weight; the sum is what the job adds to the pose, and the job
// clamps each muscle to its range (sources stack additively, decided 2026-10-22). A source fades in over its
// profile's FadeSeconds when set and fades out the same way when cleared, so nothing pops. Keys are whatever the
// caller owns the source by: a status instance, an equipment slot, the model.

public class PostureLayer
{
    private class Source
    {
        public object key;
        public PostureProfile profile;
        public float weight;
        public bool leaving;
    }

    private readonly List<Source> sources = new List<Source>();

    // Setting a key that is already present swaps its profile and keeps its current weight, so a swap blends.
    public void Set(object key, PostureProfile profile)
    {
        if (profile == null)
        {
            Clear(key);
            return;
        }

        Source source = Find(key);
        if (source == null)
        {
            source = new Source { key = key };
            sources.Add(source);
        }

        source.profile = profile;
        source.leaving = false;
    }

    public void Clear(object key)
    {
        Source source = Find(key);
        if (source != null) source.leaving = true;
    }

    public void ClearAll()
    {
        sources.Clear();
    }

    public void Tick(float deltaTime)
    {
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            Source source = sources[i];
            float step = deltaTime / source.profile.FadeSeconds;
            source.weight = source.leaving ? source.weight - step : source.weight + step;

            if (source.weight > 1f) source.weight = 1f;
            if (source.weight <= 0f && source.leaving) sources.RemoveAt(i);
        }
    }

    public void Write(NativeArray<float> offsets, float weight)
    {
        for (int i = 0; i < offsets.Length; i++)
            offsets[i] = 0f;

        foreach (var source in sources)
            source.profile.Add(offsets, source.weight * weight);
    }

    private Source Find(object key)
    {
        foreach (var source in sources)
            if (Equals(source.key, key)) return source;

        return null;
    }
}
