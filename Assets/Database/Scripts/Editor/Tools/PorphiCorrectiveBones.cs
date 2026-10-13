using System.Collections.Generic;
using UnityEngine;

namespace Porphi
{
    /// Reproduces the Blender rig's corrective (COR) bones at runtime.
    ///
    /// Each COR bone in the source rig is a Transformation constraint that reads
    /// one rotation axis of a nearby deform bone and writes a location offset --
    /// a plain 1D lerp, which is all this does.
    ///
    /// Bind pose comes from the SkinnedMeshRenderer's bindposes rather than from
    /// whatever pose the hierarchy happens to be in at Awake, so it does not
    /// matter when this initialises or what pose the prefab was saved in.
    [DefaultExecutionOrder(100)]
    public class PorphiCorrectiveBones : MonoBehaviour
    {
        [Tooltip("The *_rig.json written next to the FBX by the Blender addon.")]
        public TextAsset rigJson;

        [Tooltip("Any skinned renderer on the character; used to read the bind pose.")]
        public SkinnedMeshRenderer skin;

        [Range(0f, 1f)] public float globalWeight = 1f;

        struct Link
        {
            public Transform bone;
            public Transform source;
            public Vector3 restLocalPosition;
            public Quaternion sourceRestLocalRotation;
            public Vector3 axisInSourceFrame;
            public Vector3 offsetMin;
            public Vector3 offsetMax;
            public float angleMin;
            public float angleMax;
            public float influence;
        }

        readonly List<Link> links = new List<Link>();

        void Awake()
        {
            if (skin == null) skin = GetComponentInChildren<SkinnedMeshRenderer>();
            Build();
        }

        public void Build()
        {
            links.Clear();

            RigData data = RigData.Parse(rigJson);
            if (data == null || data.correctives == null) return;

            Dictionary<string, Transform> bones = MapBones();
            Dictionary<Transform, Matrix4x4> bind = MapBindMatrices();

            foreach (Corrective c in data.correctives)
            {
                Transform bone = Find(bones, c.bone);
                if (bone == null || c.drivers == null) continue;

                Quaternion parentBind = BindWorld(bind, bone.parent).rotation;
                Vector3 restLocal = BindLocal(bind, bone).GetColumn(3);

                foreach (Driver d in c.drivers)
                {
                    Transform source = Find(bones, d.source);
                    if (source == null) continue;

                    Quaternion sourceBind = BindWorld(bind, source).rotation;

                    links.Add(new Link
                    {
                        bone = bone,
                        source = source,
                        restLocalPosition = restLocal,
                        sourceRestLocalRotation = BindLocal(bind, source).rotation,
                        axisInSourceFrame = (Quaternion.Inverse(sourceBind)
                                             * JsonVec.V3(d.source_axis_world)).normalized,
                        offsetMin = Quaternion.Inverse(parentBind) * JsonVec.V3(d.offset_at_min_world),
                        offsetMax = Quaternion.Inverse(parentBind) * JsonVec.V3(d.offset_at_max_world),
                        angleMin = d.angle_min_deg,
                        angleMax = d.angle_max_deg,
                        influence = d.influence <= 0f ? 1f : d.influence,
                    });
                }
            }
        }

        void LateUpdate()
        {
            if (globalWeight <= 0f) return;

            // Drivers that share a bone accumulate, matching Blender's ADD mix mode.
            Transform current = null;
            Vector3 offset = Vector3.zero;

            for (int i = 0; i < links.Count; i++)
            {
                Link link = links[i];
                if (link.bone != current)
                {
                    Flush(current, offset);
                    current = link.bone;
                    offset = link.restLocalPosition;
                }

                Quaternion delta = Quaternion.Inverse(link.sourceRestLocalRotation) * link.source.localRotation;
                float angle = TwistAngle(delta, link.axisInSourceFrame);
                float t = Mathf.InverseLerp(link.angleMin, link.angleMax, angle);
                offset += Vector3.Lerp(link.offsetMin, link.offsetMax, t)
                          * (link.influence * globalWeight);
            }

            Flush(current, offset);
        }

        static void Flush(Transform bone, Vector3 localPosition)
        {
            if (bone != null) bone.localPosition = localPosition;
        }

        /// Angle, in degrees, that `q` turns about `axis` (swing-twist decomposition).
        static float TwistAngle(Quaternion q, Vector3 axis)
        {
            Vector3 imaginary = new Vector3(q.x, q.y, q.z);
            Vector3 projected = Vector3.Project(imaginary, axis);
            Quaternion twist = new Quaternion(projected.x, projected.y, projected.z, q.w);

            float magnitude = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y
                                         + twist.z * twist.z + twist.w * twist.w);
            if (magnitude < 1e-6f) return 0f;

            float w = twist.w / magnitude;
            float along = Vector3.Dot(new Vector3(twist.x, twist.y, twist.z) / magnitude, axis);
            return 2f * Mathf.Atan2(along, w) * Mathf.Rad2Deg;
        }

        Dictionary<string, Transform> MapBones()
        {
            var map = new Dictionary<string, Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                map[t.name] = t;
            return map;
        }

        /// Bind-pose world matrices, reconstructed from the skin's bindposes.
        Dictionary<Transform, Matrix4x4> MapBindMatrices()
        {
            var map = new Dictionary<Transform, Matrix4x4>();
            if (skin == null || skin.sharedMesh == null) return map;

            Matrix4x4[] bindposes = skin.sharedMesh.bindposes;
            Transform[] skinBones = skin.bones;
            Matrix4x4 root = (skin.rootBone != null ? skin.rootBone : transform).localToWorldMatrix;

            int count = Mathf.Min(bindposes.Length, skinBones.Length);
            for (int i = 0; i < count; i++)
            {
                if (skinBones[i] == null) continue;
                map[skinBones[i]] = root * bindposes[i].inverse;
            }
            return map;
        }

        /// Falls back to the live transform for bones the skin does not weight
        /// (Root and the synthetic Toes bones, typically).
        static Matrix4x4 BindWorld(Dictionary<Transform, Matrix4x4> bind, Transform bone)
        {
            if (bone == null) return Matrix4x4.identity;
            return bind.TryGetValue(bone, out Matrix4x4 m) ? m : bone.localToWorldMatrix;
        }

        static Matrix4x4 BindLocal(Dictionary<Transform, Matrix4x4> bind, Transform bone)
        {
            return BindWorld(bind, bone.parent).inverse * BindWorld(bind, bone);
        }

        static Transform Find(Dictionary<string, Transform> bones, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            bones.TryGetValue(name, out Transform t);
            return t;
        }
    }
}
