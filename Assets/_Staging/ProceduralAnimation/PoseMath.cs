using UnityEngine;

// Pose maths the Burst-compiled animation jobs share. Quaternion.Slerp, Lerp, AngleAxis and FromToRotation are native
// calls Burst can't compile, so they are written out here.

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

    // Rotation by an angle in radians about a unit axis.
    public static Quaternion AxisAngle(Vector3 axis, float radians)
    {
        float sin = Mathf.Sin(radians * 0.5f);
        return new Quaternion(axis.x * sin, axis.y * sin, axis.z * sin, Mathf.Cos(radians * 0.5f));
    }

    // The shortest rotation taking one direction onto another.
    public static Quaternion FromTo(Vector3 from, Vector3 to)
    {
        Vector3 f = from.normalized;
        Vector3 t = to.normalized;
        float dot = Vector3.Dot(f, t);

        if (dot < -0.9999f)
        {
            Vector3 axis = Vector3.Cross(Vector3.right, f);
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.Cross(Vector3.up, f);
            return AxisAngle(axis.normalized, Mathf.PI);
        }

        Vector3 c = Vector3.Cross(f, t);
        float x = c.x, y = c.y, z = c.z, w = 1f + dot;
        float length = Mathf.Sqrt(x * x + y * y + z * z + w * w);
        return new Quaternion(x / length, y / length, z / length, w / length);
    }
}
