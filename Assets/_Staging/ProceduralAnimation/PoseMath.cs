using UnityEngine;

// Pose maths the Burst-compiled animation jobs share. Quaternion.Slerp and Quaternion.Lerp are native calls Burst
// can't compile, so rotations blend with a normalised lerp along the shorter arc.

public static class PoseMath
{
    // Matches slerp at t 0 and 1; between, close enough for neighbouring poses and layer weights.
    public static Quaternion Nlerp(Quaternion from, Quaternion to, float t)
    {
        float dot = from.x * to.x + from.y * to.y + from.z * to.z + from.w * to.w;
        float sign = dot < 0f ? -1f : 1f;

        float x = from.x + (to.x * sign - from.x) * t;
        float y = from.y + (to.y * sign - from.y) * t;
        float z = from.z + (to.z * sign - from.z) * t;
        float w = from.w + (to.w * sign - from.w) * t;
        float length = Mathf.Sqrt(x * x + y * y + z * z + w * w);

        return new Quaternion(x / length, y / length, z / length, w / length);
    }
}
