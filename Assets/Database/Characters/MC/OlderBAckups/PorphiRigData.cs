using System;
using UnityEngine;

namespace Porphi
{
    /// Serialisable mirror of the *_rig.json written by the Blender addon.
    /// Field names must match the JSON keys exactly -- JsonUtility does not
    /// rename or map anything.
    [Serializable]
    public class RigData
    {
        public string format;
        public string rig;
        public string space;
        public string root_bone;
        public HumanoidEntry[] humanoid;
        public Corrective[] correctives;

        public static RigData Parse(TextAsset json)
        {
            if (json == null) return null;
            RigData data = JsonUtility.FromJson<RigData>(json.text);
            if (data == null || data.format != "porphi-unity-rig/1")
                Debug.LogWarning("Porphi: unexpected rig JSON format.");
            return data;
        }
    }

    [Serializable]
    public class HumanoidEntry
    {
        public string unity;
        public string bone;
    }

    [Serializable]
    public class Corrective
    {
        public string bone;
        public string parent;
        public float[] rest_world_position;
        public float[] rest_world_rotation;
        public Driver[] drivers;
    }

    [Serializable]
    public class Driver
    {
        public string name;
        public string source;
        public float[] source_axis_world;
        public float[] source_rest_rotation;
        public float angle_min_deg;
        public float angle_max_deg;
        public float[] offset_at_min_world;
        public float[] offset_at_max_world;
        public float influence;
    }

    public static class JsonVec
    {
        public static Vector3 V3(float[] v)
        {
            return v != null && v.Length >= 3 ? new Vector3(v[0], v[1], v[2]) : Vector3.zero;
        }

        public static Quaternion Q(float[] v)
        {
            return v != null && v.Length >= 4 ? new Quaternion(v[0], v[1], v[2], v[3]) : Quaternion.identity;
        }
    }
}
