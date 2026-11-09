using System.Collections.Generic;
using MagicaCloth2;
using UnityEditor;
using UnityEngine;

// Tools → Characters → Body Springs → Set Up Selected Model. Staging: retire once body springs graduate.
//
// Gives the selected model's BreastCloth and ButtCloth (Magica Cloth 2 BoneSpring) a starting tune and the
// colliders that stop limbs clipping through them: capsules on the upper arms and forearms for the chest (hand
// signs cross it), on the thighs for the butt. Every bone in a cloth's chains becomes a collision bone, and its
// Normal Axis is set to the bone axis pointing out of the body, so Normal Limit Ratio squashes in-and-out travel
// rather than the bounce. Capsules are built along each limb bone to its child, scaled from the bone length so
// rig scale doesn't matter. Rerunning overwrites the tune and rebuilds the colliders; tune on from the inspector.

public static class BodySpringSetup
{
    private const string ColliderPrefix = "Collider_";

    private class Limb
    {
        public string bone;
        public string child;
        public float start;   // fraction of the bone left uncovered at the joint end
        public float radius;  // fraction of the bone's length
    }

    private class Tune
    {
        public string cloth;
        public float outward; // +1 out of the front, -1 out of the back
        public float springPower;
        public float limitDistance;
        public float normalLimitRatio;
        public float springNoise;
        public float damping;
        public Limb[] limbs;
    }

    private static readonly Tune[] Tunes =
    {
        new Tune
        {
            cloth = "BreastCloth", outward = 1f,
            springPower = 0.04f, limitDistance = 0.025f, normalLimitRatio = 0.4f, springNoise = 0.15f, damping = 0.2f,
            limbs = new[]
            {
                new Limb { bone = "Bicep.L", child = "Forearm.L", start = 0.15f, radius = 0.16f },
                new Limb { bone = "Bicep.R", child = "Forearm.R", start = 0.15f, radius = 0.16f },
                new Limb { bone = "Forearm.L", child = "Hand.L", start = 0f, radius = 0.14f },
                new Limb { bone = "Forearm.R", child = "Hand.R", start = 0f, radius = 0.14f },
            }
        },
        new Tune
        {
            cloth = "ButtCloth", outward = -1f,
            springPower = 0.09f, limitDistance = 0.017f, normalLimitRatio = 0.3f, springNoise = 0.1f, damping = 0.22f,
            limbs = new[]
            {
                new Limb { bone = "Thigh.L", child = "Shin.L", start = 0.25f, radius = 0.17f },
                new Limb { bone = "Thigh.R", child = "Shin.R", start = 0.25f, radius = 0.17f },
            }
        },
    };

    private static readonly Vector3[] NormalAxes =
    {
        Vector3.right, Vector3.up, Vector3.forward, Vector3.left, Vector3.down, Vector3.back
    };

    [MenuItem("Tools/Characters/Body Springs/Set Up Selected Model")]
    private static void SetUpSelectedModel()
    {
        string path = Selection.activeGameObject != null ? AssetDatabase.GetAssetPath(Selection.activeGameObject) : "";
        if (!path.EndsWith(".prefab"))
        {
            Debug.LogError("[BodySpringSetup] Select a model prefab in the Project window.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        if (Build(root))
            PrefabUtility.SaveAsPrefabAsset(root, path);

        PrefabUtility.UnloadPrefabContents(root);
    }

    // False leaves the prefab unsaved, so a failed run changes nothing.
    private static bool Build(GameObject root)
    {
        var bones = BonesByName(root);
        var cloths = ClothsByName(root);

        foreach (var tune in Tunes)
        {
            if (!cloths.TryGetValue(tune.cloth, out var cloth))
            {
                Debug.LogError($"[BodySpringSetup] {root.name} has no MagicaCloth on a GameObject named {tune.cloth}.", root);
                return false;
            }

            var colliders = BuildColliders(tune, bones);
            if (colliders == null) return false;

            if (!Apply(cloth, tune, colliders, root.transform)) return false;
        }

        return true;
    }

    private static Dictionary<string, Transform> BonesByName(GameObject root)
    {
        var bones = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            bones.TryAdd(t.name, t);

        return bones;
    }

    private static Dictionary<string, MagicaCloth> ClothsByName(GameObject root)
    {
        var cloths = new Dictionary<string, MagicaCloth>();
        foreach (var cloth in root.GetComponentsInChildren<MagicaCloth>(true))
            cloths.TryAdd(cloth.name, cloth);

        return cloths;
    }

    private static List<ColliderComponent> BuildColliders(Tune tune, Dictionary<string, Transform> bones)
    {
        var colliders = new List<ColliderComponent>();
        foreach (var limb in tune.limbs)
        {
            bones.TryGetValue(limb.bone, out var bone);
            bones.TryGetValue(limb.child, out var child);
            if (bone == null || child == null)
            {
                Debug.LogError($"[BodySpringSetup] Missing bone {limb.bone} or {limb.child} for {tune.cloth}.");
                return null;
            }

            colliders.Add(Capsule(bone, child, limb));
        }

        return colliders;
    }

    // Its own child of the limb bone, turned so local Y runs down the bone and starting partway along it.
    private static MagicaCapsuleCollider Capsule(Transform bone, Transform child, Limb limb)
    {
        Transform existing = bone.Find(ColliderPrefix + bone.name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        Vector3 along = child.localPosition;
        float length = along.magnitude;

        var go = new GameObject(ColliderPrefix + bone.name);
        go.transform.SetParent(bone, false);
        go.transform.localPosition = along * limb.start;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, along);

        var capsule = go.AddComponent<MagicaCapsuleCollider>();
        capsule.direction = MagicaCapsuleCollider.Direction.Y;
        capsule.reverseDirection = false;
        capsule.alignedOnCenter = false;
        float radius = length * limb.radius;
        capsule.SetSize(radius, radius, length * (1f - limb.start));
        return capsule;
    }

    private static bool Apply(MagicaCloth cloth, Tune tune, List<ColliderComponent> colliders, Transform model)
    {
        var data = cloth.SerializeData;
        if (data.clothType != ClothProcess.ClothType.BoneSpring || data.rootBones.Count == 0 || data.rootBones[0] == null)
        {
            Debug.LogError($"[BodySpringSetup] {tune.cloth} must be a BoneSpring with its root bones set.", cloth);
            return false;
        }

        data.normalAxis = OutwardAxis(data.rootBones[0], model.forward * tune.outward);
        data.damping.SetValue(tune.damping);
        data.stablizationTimeAfterReset = 0.1f;

        data.springConstraint.useSpring = true;
        data.springConstraint.springPower = tune.springPower;
        data.springConstraint.limitDistance = tune.limitDistance;
        data.springConstraint.normalLimitRatio = tune.normalLimitRatio;
        data.springConstraint.springNoise = tune.springNoise;

        // Flat, not curved by depth: the curves were zeroing the limit on the root bones.
        data.angleRestorationConstraint.useAngleRestoration = true;
        data.angleRestorationConstraint.stiffness.SetValue(0.4f);
        data.angleRestorationConstraint.velocityAttenuation = 0.8f;
        data.angleLimitConstraint.useAngleLimit = true;
        data.angleLimitConstraint.limitAngle.SetValue(30f);
        data.angleLimitConstraint.stiffness = 1f;

        // Local inertia below 1 and capped speeds, so dashes, lunges and the stepped pose don't fling them.
        var inertia = data.inertiaConstraint;
        inertia.worldInertia = 1f;
        inertia.movementInertiaSmoothing = 0.5f;
        inertia.movementSpeedLimit.SetValue(true, 5f);
        inertia.rotationSpeedLimit.SetValue(true, 720f);
        inertia.localInertia = 0.8f;
        inertia.localMovementSpeedLimit.SetValue(true, 3f);
        inertia.localRotationSpeedLimit.SetValue(true, 540f);
        inertia.centrifualAcceleration = 0.2f;
        inertia.particleSpeedLimit.SetValue(true, 3f);
        inertia.teleportMode = InertiaConstraint.TeleportMode.Reset;
        inertia.teleportDistance = 0.5f;
        inertia.teleportRotation = 90f;

        var collision = data.colliderCollisionConstraint;
        collision.colliderList = colliders;
        collision.collisionBones = ChainBones(data.rootBones);
        collision.limitDistance.SetValue(0.04f);

        EditorUtility.SetDirty(cloth);
        return true;
    }

    private static List<Transform> ChainBones(List<Transform> roots)
    {
        var chain = new List<Transform>();
        foreach (var root in roots)
            if (root != null) chain.AddRange(root.GetComponentsInChildren<Transform>(true));

        return chain;
    }

    // The bone's local axis (in ClothNormalAxis order) that points most nearly out of the body.
    private static ClothNormalAxis OutwardAxis(Transform bone, Vector3 outward)
    {
        int best = 0;
        float bestDot = float.MinValue;
        for (int i = 0; i < NormalAxes.Length; i++)
        {
            float dot = Vector3.Dot(bone.rotation * NormalAxes[i], outward);
            if (dot <= bestDot) continue;

            best = i;
            bestDot = dot;
        }

        return (ClothNormalAxis)best;
    }
}
