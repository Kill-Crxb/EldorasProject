using System.Collections.Generic;
using MagicaCloth2;
using UnityEditor;
using UnityEngine;

// One-shot setup for the Porphi breast bones using Magica Cloth 2 BoneSpring.
// Idempotent: running it again reconfigures the existing component rather than stacking a second.
public static class PorphiJiggleSetup
{
    const string PrefabPath = "Assets/Database/Characters/MC/Porphi.prefab";
    const string ClothObjectName = "BreastCloth";

    const string ColliderObjectName = "ChestCollider";
    const string ChestBoneName = "UpperChest";

    const string ButtObjectName = "ButtCloth";
    const string HipColliderObjectName = "HipCollider";
    const string HipBoneName = "Hips";

    static readonly string[] RootBoneNames = { "Breast_01_L", "Breast_01_R" };
    static readonly string[] TipBoneNames = { "Breast_02_L", "Breast_02_R" };
    static readonly string[] ButtBoneNames = { "Butt_L", "Butt_R" };

    [MenuItem("Tools/Crab/Jiggle/Setup Porphi Breast Spring")]
    static void Setup()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        if (root == null)
        {
            Debug.LogError($"[PorphiJiggleSetup] Could not load prefab at {PrefabPath}");
            return;
        }

        try
        {
            List<Transform> rootBones = FindBones(root.transform, RootBoneNames);
            List<Transform> tipBones = FindBones(root.transform, TipBoneNames);

            if (rootBones == null || tipBones == null) return;

            float chainLength = MeasureChain(rootBones, tipBones);

            if (chainLength <= 0f)
            {
                Debug.LogError("[PorphiJiggleSetup] Measured a zero-length breast chain. The bones are coincident, which means the rig imported at a scale the spring distances cannot be derived from.");
                return;
            }

            Transform holder = FindOrCreateHolder(root.transform);
            MagicaCloth cloth = holder.GetComponent<MagicaCloth>();
            if (cloth == null) cloth = holder.gameObject.AddComponent<MagicaCloth>();

            Configure(cloth, rootBones, chainLength, root);

            EditorUtility.SetDirty(cloth);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            Debug.Log($"[PorphiJiggleSetup] Configured BoneSpring on '{ClothObjectName}'.\n" +
                      $"  root bones:      {string.Join(", ", RootBoneNames)}\n" +
                      $"  chain length:    {chainLength:0.0000} m (measured in the prefab, world space)\n" +
                      $"  limit distance:  {cloth.SerializeData.springConstraint.limitDistance:0.0000} m\n" +
                      $"  spring power:    {cloth.SerializeData.springConstraint.springPower:0.000}\n" +
                      $"  radius:          {cloth.SerializeData.radius.value:0.0000} m\n" +
                      $"  culling:         Off ({cloth.SerializeData.cullingSettings.cameraCullingRenderers.Count} renderers registered)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Separate from Setup on purpose. Setup rewrites every spring parameter, so running it again
    // would discard hand tuning done in the inspector. This touches collision only.
    [MenuItem("Tools/Crab/Jiggle/Add Porphi Chest Collider")]
    static void AddChestCollider()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        if (root == null)
        {
            Debug.LogError($"[PorphiJiggleSetup] Could not load prefab at {PrefabPath}");
            return;
        }

        try
        {
            Transform holder = root.transform.Find(ClothObjectName);
            MagicaCloth cloth = holder != null ? holder.GetComponent<MagicaCloth>() : null;

            if (cloth == null)
            {
                Debug.LogError($"[PorphiJiggleSetup] No '{ClothObjectName}' with a MagicaCloth on the prefab. Run Setup Porphi Breast Spring first.");
                return;
            }

            Transform chest = FindDeep(root.transform, ChestBoneName);

            if (chest == null)
            {
                Debug.LogError($"[PorphiJiggleSetup] Bone '{ChestBoneName}' not found. The collider needs a chest bone to ride on.");
                return;
            }

            List<Transform> rootBones = FindBones(root.transform, RootBoneNames);
            List<Transform> tipBones = FindBones(root.transform, TipBoneNames);

            if (rootBones == null || tipBones == null) return;

            float separation = Vector3.Distance(rootBones[0].position, rootBones[1].position);

            if (separation <= 0f)
            {
                Debug.LogError("[PorphiJiggleSetup] The two breast root bones are coincident, so the capsule cannot be sized from them.");
                return;
            }

            // Sized from the gap between the breast roots, which is the only chest measurement the
            // skeleton actually gives. It is a starting point, not a fit — the capsule gizmo is
            // visible in the scene view and wants eyeballing against the mesh.
            float radius = separation * 0.6f;
            float length = separation * 0.9f;

            MagicaCapsuleCollider capsule = BuildCapsule(root.transform, chest, radius, length, ColliderObjectName);

            ClothSerializeData data = cloth.SerializeData;

            data.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Point;

            if (!data.colliderCollisionConstraint.colliderList.Contains(capsule))
                data.colliderCollisionConstraint.colliderList.Add(capsule);

            // BoneSpring collides only the transforms named here, and they must already be part of
            // the spring — RenderSetupData resolves them by IndexOf against the spring's own
            // transform list. The tips are the end that presses into the torso.
            data.colliderCollisionConstraint.collisionBones = new List<Transform>(tipBones);

            // How far a collider may push a particle off its origin. Left at the 0.05 m default it
            // would be an arbitrary absolute on a rig whose import scale is not obvious.
            data.colliderCollisionConstraint.limitDistance = new CurveSerializeData(separation * 0.5f);

            EditorUtility.SetDirty(cloth);
            EditorUtility.SetDirty(capsule);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            Debug.Log($"[PorphiJiggleSetup] Added '{ColliderObjectName}' under '{chest.name}'.\n" +
                      $"  breast separation: {separation:0.0000} m (measured)\n" +
                      $"  capsule radius:    {radius:0.0000} m\n" +
                      $"  capsule length:    {length:0.0000} m\n" +
                      $"  collision bones:   {string.Join(", ", TipBoneNames)}\n" +
                      $"  push limit:        {data.colliderCollisionConstraint.limitDistance.value:0.0000} m\n" +
                      "  Check the capsule gizmo against the torso mesh and adjust radius by hand.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static MagicaCapsuleCollider BuildCapsule(Transform root, Transform chest, float radius, float length, string objectName)
    {
        Transform existing = chest.Find(objectName);

        if (existing == null)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(chest, false);
            existing = go.transform;
        }

        existing.localPosition = Vector3.zero;

        // Aligned to the character, not to the bone. This rig exports with primary_bone_axis='Y',
        // so the chest bone's own X is not reliably the body's left-right — and the capsule's
        // Direction is read in its local space. Matching the root's rotation makes X mean across
        // the body, whatever the bone axes are. It still follows the chest, because it is parented
        // to it.
        existing.rotation = root.rotation;

        MagicaCapsuleCollider capsule = existing.GetComponent<MagicaCapsuleCollider>();
        if (capsule == null) capsule = existing.gameObject.AddComponent<MagicaCapsuleCollider>();

        capsule.direction = MagicaCapsuleCollider.Direction.X;
        capsule.alignedOnCenter = true;
        capsule.radiusSeparation = false;
        capsule.center = Vector3.zero;
        capsule.SetSize(radius, radius, length);

        return capsule;
    }

    [MenuItem("Tools/Crab/Jiggle/Remove All Porphi Jiggle")]
    static void Remove()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        if (root == null)
        {
            Debug.LogError($"[PorphiJiggleSetup] Could not load prefab at {PrefabPath}");
            return;
        }

        try
        {
            Transform chest = FindDeep(root.transform, ChestBoneName);
            Transform hips = FindDeep(root.transform, HipBoneName);

            Transform[] targets =
            {
                root.transform.Find(ClothObjectName),
                root.transform.Find(ButtObjectName),
                chest != null ? chest.Find(ColliderObjectName) : null,
                hips != null ? hips.Find(HipColliderObjectName) : null,
            };

            int removed = 0;

            foreach (Transform target in targets)
            {
                if (target == null) continue;
                Object.DestroyImmediate(target.gameObject);
                removed++;
            }

            if (removed == 0)
            {
                Debug.Log("[PorphiJiggleSetup] Nothing to remove on the prefab.");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            Debug.Log($"[PorphiJiggleSetup] Removed {removed} jiggle object(s) from {PrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void Configure(MagicaCloth cloth, List<Transform> rootBones, float chainLength, GameObject root)
    {
        ClothSerializeData data = cloth.SerializeData;

        // BoneSpring, not BoneCloth. BoneCloth pins the root and swings the children, which is
        // right for hair and skirts. BoneSpring lets the registered bones themselves travel a
        // bounded distance and spring back, which is what flesh does. It also ignores gravity
        // entirely, so there is no sag to fight.
        data.clothType = ClothProcess.ClothType.BoneSpring;
        data.rootBones = new List<Transform>(rootBones);

        // Every distance below is derived from the measured bone length rather than typed in
        // metres, because this rig's FBX import scale is not obvious from the file.
        data.radius = new CurveSerializeData(chainLength * 0.35f);
        data.damping = new CurveSerializeData(0.05f);

        data.springConstraint.useSpring = true;
        data.springConstraint.springPower = 0.03f;
        data.springConstraint.limitDistance = chainLength * 0.5f;
        data.springConstraint.normalLimitRatio = 1.0f;
        data.springConstraint.springNoise = 0.0f;

        data.angleRestorationConstraint.useAngleRestoration = true;
        data.angleRestorationConstraint.stiffness = new CurveSerializeData(0.2f, 1.0f, 0.2f, true);

        data.angleLimitConstraint.useAngleLimit = true;
        data.angleLimitConstraint.limitAngle = new CurveSerializeData(30.0f, 0.0f, 1.0f);

        // What actually drives the motion: the body moving and turning underneath the bones.
        data.inertiaConstraint.worldInertia = 1.0f;
        data.inertiaConstraint.localInertia = 1.0f;
        data.inertiaConstraint.depthInertia = 0.0f;
        data.inertiaConstraint.centrifualAcceleration = 0.0f;

        // Camera culling defaults to AnimatorLinkage, which decides whether to simulate from the
        // visibility of registered renderers. BoneSpring has no sourceRenderers to derive them
        // from, so the list starts empty and the component can sit permanently culled — running,
        // reporting no error, and never moving a bone. Register the character's renderers so the
        // setting means something, then turn it off anyway: one player character simulating
        // off-screen costs nothing next to a jiggle that mysteriously does not.
        data.cullingSettings.cameraCullingRenderers = new List<Renderer>(root.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        data.cullingSettings.cameraCullingMode = CullingSettings.CameraCullingMode.Off;
    }

    static Transform FindOrCreateHolder(Transform root) => FindOrCreateChild(root, ClothObjectName);

    static List<Transform> FindBones(Transform root, string[] names)
    {
        List<Transform> found = new List<Transform>();

        foreach (string name in names)
        {
            Transform bone = FindDeep(root, name);

            if (bone == null)
            {
                Debug.LogError($"[PorphiJiggleSetup] Bone '{name}' not found under '{root.name}'. The rig may have been re-exported with different bone names.");
                return null;
            }

            found.Add(bone);
        }

        return found;
    }


    // ── Butt ──────────────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Crab/Jiggle/Setup Porphi Butt Spring")]
    static void SetupButt()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        if (root == null)
        {
            Debug.LogError($"[PorphiJiggleSetup] Could not load prefab at {PrefabPath}");
            return;
        }

        try
        {
            List<Transform> bones = FindBones(root.transform, ButtBoneNames);
            if (bones == null) return;

            float separation = Vector3.Distance(bones[0].position, bones[1].position);

            if (separation <= 0f)
            {
                Debug.LogError("[PorphiJiggleSetup] The two butt bones are coincident, so nothing can be sized from them.");
                return;
            }

            // Butt_L/R are leaf bones with no children, so there is no chain to measure. The gap
            // between them is the only scale the skeleton offers.
            Transform holder = FindOrCreateChild(root.transform, ButtObjectName);
            MagicaCloth cloth = holder.GetComponent<MagicaCloth>();
            if (cloth == null) cloth = holder.gameObject.AddComponent<MagicaCloth>();

            ClothSerializeData data = cloth.SerializeData;

            data.clothType = ClothProcess.ClothType.BoneSpring;
            data.rootBones = new List<Transform>(bones);

            // Outward for a backside is behind the character. Measured rather than assumed — see
            // LogNormalAxes.
            ClothNormalAxis axis = PickNormalAxis(bones[0], -root.transform.forward, out float alignment);
            ClothNormalAxis mirrored = PickNormalAxis(bones[1], -root.transform.forward, out float mirroredAlignment);
            data.normalAxis = axis;

            // normalAxis is one value for the whole component. A mirrored rig can give the two
            // sides different best axes, and then one side is clamped along a direction nobody
            // meant. Splitting into a left component and a right one is the only fix.
            if (axis != mirrored)
                Debug.LogError($"[PorphiJiggleSetup] '{ButtBoneNames[0]}' wants {axis} but '{ButtBoneNames[1]}' wants {mirrored} ({mirroredAlignment:0.00}). One MagicaCloth cannot serve both — split them into two components.", cloth);

            data.radius = new CurveSerializeData(separation * 0.3f);
            data.damping = new CurveSerializeData(0.15f);

            data.springConstraint.useSpring = true;

            // Firmer and shorter-travelled than the chest on purpose. A backside carries more mass
            // over a shorter lever, so it moves less and settles faster.
            data.springConstraint.springPower = 0.09f;
            data.springConstraint.limitDistance = separation * 0.10f;
            data.springConstraint.normalLimitRatio = 0.3f;
            data.springConstraint.springNoise = 0.1f;

            data.angleRestorationConstraint.useAngleRestoration = true;
            data.angleRestorationConstraint.stiffness = new CurveSerializeData(0.3f, 1.0f, 0.3f, true);

            data.angleLimitConstraint.useAngleLimit = true;
            data.angleLimitConstraint.limitAngle = new CurveSerializeData(20.0f, 0.0f, 1.0f);

            data.inertiaConstraint.worldInertia = 1.0f;
            data.inertiaConstraint.localInertia = 1.0f;
            data.inertiaConstraint.depthInertia = 0.0f;
            data.inertiaConstraint.centrifualAcceleration = 0.0f;

            EditorUtility.SetDirty(cloth);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            Debug.Log($"[PorphiJiggleSetup] Configured BoneSpring on '{ButtObjectName}'.\n" +
                      $"  root bones:      {string.Join(", ", ButtBoneNames)} (leaf bones, no children)\n" +
                      $"  separation:      {separation:0.0000} m (measured)\n" +
                      $"  normal axis:     {axis} (alignment {alignment:0.00} — below ~0.7 means the rig does not have a clean axis for this and it wants checking by hand)\n" +
                      $"  limit distance:  {data.springConstraint.limitDistance:0.0000} m\n" +
                      $"  spring power:    {data.springConstraint.springPower:0.000}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Crab/Jiggle/Add Porphi Hip Collider")]
    static void AddHipCollider()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        if (root == null)
        {
            Debug.LogError($"[PorphiJiggleSetup] Could not load prefab at {PrefabPath}");
            return;
        }

        try
        {
            Transform holder = root.transform.Find(ButtObjectName);
            MagicaCloth cloth = holder != null ? holder.GetComponent<MagicaCloth>() : null;

            if (cloth == null)
            {
                Debug.LogError($"[PorphiJiggleSetup] No '{ButtObjectName}' with a MagicaCloth on the prefab. Run Setup Porphi Butt Spring first.");
                return;
            }

            Transform hips = FindDeep(root.transform, HipBoneName);

            if (hips == null)
            {
                Debug.LogError($"[PorphiJiggleSetup] Bone '{HipBoneName}' not found.");
                return;
            }

            List<Transform> bones = FindBones(root.transform, ButtBoneNames);
            if (bones == null) return;

            float separation = Vector3.Distance(bones[0].position, bones[1].position);
            float radius = separation * 0.55f;
            float length = separation * 0.7f;

            MagicaCapsuleCollider capsule = BuildCapsule(root.transform, hips, radius, length, HipColliderObjectName);

            ClothSerializeData data = cloth.SerializeData;

            data.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Point;

            if (!data.colliderCollisionConstraint.colliderList.Contains(capsule))
                data.colliderCollisionConstraint.colliderList.Add(capsule);

            // The butt bones are leaves, so the roots ARE the particles — unlike the chest, where
            // the tips collide and the roots stay put.
            data.colliderCollisionConstraint.collisionBones = new List<Transform>(bones);
            data.colliderCollisionConstraint.limitDistance = new CurveSerializeData(separation * 0.4f);

            EditorUtility.SetDirty(cloth);
            EditorUtility.SetDirty(capsule);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            Debug.Log($"[PorphiJiggleSetup] Added '{HipColliderObjectName}' under '{hips.name}'.\n" +
                      $"  capsule radius: {radius:0.0000} m\n" +
                      $"  capsule length: {length:0.0000} m\n" +
                      "  A hip capsule that is too fat shoves the butt bones permanently outward. Check the gizmo before trusting it.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Diagnostics ───────────────────────────────────────────────────────────────────────────

    // Changes nothing. Prints the axis each spring SHOULD use, so the normalAxis guess can be
    // checked without re-running a setup and losing inspector tuning.
    [MenuItem("Tools/Crab/Jiggle/Log Normal Axes")]
    static void LogNormalAxes()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        if (root == null)
        {
            Debug.LogError($"[PorphiJiggleSetup] Could not load prefab at {PrefabPath}");
            return;
        }

        try
        {
            string report = "[PorphiJiggleSetup] Recommended normalAxis per bone, measured from the bind pose:\n";

            report += DescribeAxis(root.transform, "Breast_01_L", root.transform.forward, "outward = character forward");
            report += DescribeAxis(root.transform, "Breast_01_R", root.transform.forward, "outward = character forward");
            report += DescribeAxis(root.transform, "Butt_L", -root.transform.forward, "outward = character back");
            report += DescribeAxis(root.transform, "Butt_R", -root.transform.forward, "outward = character back");

            report += "\nAlignment is the dot product between the bone's chosen local axis and the outward\n" +
                      "direction. Near 1.0 is a clean match. Below ~0.7 the rig has no axis pointing cleanly\n" +
                      "out of the body there, and normalLimitRatio will squash a direction you did not mean.";

            Debug.Log(report);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string DescribeAxis(Transform root, string boneName, Vector3 outward, string note)
    {
        Transform bone = FindDeep(root, boneName);

        if (bone == null) return $"  {boneName,-14} NOT FOUND\n";

        ClothNormalAxis axis = PickNormalAxis(bone, outward, out float alignment);
        return $"  {boneName,-14} {axis,-16} alignment {alignment:0.00}   ({note})\n";
    }

    // Which of the bone's six signed local axes points most nearly along `outward` in world space.
    // MC2 rotates the chosen axis by the particle's base rotation, which in the bind pose is the
    // bone's own world rotation, so this is the same basis the simulation uses.
    static ClothNormalAxis PickNormalAxis(Transform bone, Vector3 outward, out float alignment)
    {
        outward = outward.normalized;
        ClothNormalAxis best = ClothNormalAxis.Up;
        alignment = -2f;

        foreach (ClothNormalAxis axis in (ClothNormalAxis[])System.Enum.GetValues(typeof(ClothNormalAxis)))
        {
            float dot = Vector3.Dot(bone.rotation * AxisVector(axis), outward);
            if (dot <= alignment) continue;

            alignment = dot;
            best = axis;
        }

        return best;
    }

    static Vector3 AxisVector(ClothNormalAxis axis)
    {
        switch (axis)
        {
            case ClothNormalAxis.Right: return Vector3.right;
            case ClothNormalAxis.Up: return Vector3.up;
            case ClothNormalAxis.Forward: return Vector3.forward;
            case ClothNormalAxis.InverseRight: return Vector3.left;
            case ClothNormalAxis.InverseUp: return Vector3.down;
            case ClothNormalAxis.InverseForward: return Vector3.back;
        }

        return Vector3.up;
    }

    static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null) return existing;

        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform hit = FindDeep(parent.GetChild(i), name);
            if (hit != null) return hit;
        }

        return null;
    }

    static float MeasureChain(List<Transform> rootBones, List<Transform> tipBones)
    {
        float total = 0f;

        for (int i = 0; i < rootBones.Count; i++)
            total += Vector3.Distance(rootBones[i].position, tipBones[i].position);

        return total / rootBones.Count;
    }
}
