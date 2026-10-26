using System;
using System.Collections.Generic;
using CrabSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Tools → Characters → Copy Model Parts From Katana Girl. Staging: retire once the new body graduates.
//
// Gives the selected model prefab what makes a model a playable character model: ModelSocketProvider,
// AnimationEventForwarder, RootMotionRelay and SocketEventRelay, with Katana Girl's settings, and her sockets.
// Each socket is rebuilt on the matching humanoid bone. Both rigs are put in the Avatar's zero-muscle pose while
// sockets are placed, so a socket keeps its place and facing on the body even though the two rigs' bone axes
// differ; the bones are put back afterwards. References into Katana Girl (sockets, bones) are remapped, anything
// without a counterpart (her sheath mesh) is cleared. Rerunning overwrites the parts and replaces same-named sockets.

public static class ModelPartsCopy
{
    private const string ReferencePath = "Assets/Database/Characters/KatanaGirl/KatanaGirl_Model.prefab";

    private static readonly Type[] Parts =
    {
        typeof(ModelSocketProvider), typeof(AnimationEventForwarder), typeof(RootMotionRelay), typeof(SocketEventRelay)
    };

    [MenuItem("Tools/Characters/Copy Model Parts From Katana Girl")]
    private static void CopyToSelectedModel()
    {
        string path = Selection.activeGameObject != null ? AssetDatabase.GetAssetPath(Selection.activeGameObject) : "";
        if (!path.EndsWith(".prefab") || path == ReferencePath)
        {
            Debug.LogError("[ModelPartsCopy] Select a model prefab (not Katana Girl) in the Project window.");
            return;
        }

        var reference = PrefabUtility.LoadPrefabContents(ReferencePath);
        var target = PrefabUtility.LoadPrefabContents(path);
        var referenceAnimator = reference.GetComponentInChildren<Animator>(true);
        var targetAnimator = target.GetComponentInChildren<Animator>(true);

        if (!IsHumanoid(referenceAnimator) || !IsHumanoid(targetAnimator))
        {
            Debug.LogError("[ModelPartsCopy] Both models need an Animator with a humanoid Avatar.");
            PrefabUtility.UnloadPrefabContents(reference);
            PrefabUtility.UnloadPrefabContents(target);
            return;
        }

        var map = BoneMap(referenceAnimator, targetAnimator);
        PlaceSockets(reference, referenceAnimator, targetAnimator, map);

        foreach (var type in Parts)
            CopyPart(type, reference, target, targetAnimator, map);

        PrefabUtility.SaveAsPrefabAsset(target, path);
        PrefabUtility.UnloadPrefabContents(target);
        PrefabUtility.UnloadPrefabContents(reference);
    }

    private static bool IsHumanoid(Animator animator)
    {
        return animator != null && animator.avatar != null && animator.avatar.isHuman;
    }

    // ── Sockets ───────────────────────────────────────────────────────────────

    private static void PlaceSockets(GameObject reference, Animator referenceAnimator, Animator targetAnimator, Dictionary<Object, Object> map)
    {
        var provider = reference.GetComponentInChildren<ModelSocketProvider>(true);
        if (provider == null) return;

        var referencePose = Snapshot(referenceAnimator.transform);
        var targetPose = Snapshot(targetAnimator.transform);
        ZeroMuscles(referenceAnimator);
        ZeroMuscles(targetAnimator);

        foreach (var socket in SocketsOf(provider))
            map[socket] = PlaceSocket(socket, referenceAnimator, targetAnimator, map);

        foreach (var socket in SocketsOf(provider))
            map[socket.gameObject] = ((Transform)map[socket]).gameObject;

        Restore(referencePose);
        Restore(targetPose);
    }

    private static List<Transform> SocketsOf(ModelSocketProvider provider)
    {
        var sockets = new List<Transform>();
        foreach (var mapping in provider.slotSockets)
            if (mapping.socket != null && !sockets.Contains(mapping.socket)) sockets.Add(mapping.socket);

        foreach (var mapping in provider.namedSockets)
            if (mapping.socket != null && !sockets.Contains(mapping.socket)) sockets.Add(mapping.socket);

        return sockets;
    }

    // Same offset and facing relative to the body, hung off the same humanoid bone.
    private static Transform PlaceSocket(Transform socket, Animator referenceAnimator, Animator targetAnimator, Dictionary<Object, Object> map)
    {
        Transform referenceBone = BoneAbove(socket, map);
        Transform targetBone = referenceBone != null ? (Transform)map[referenceBone] : targetAnimator.transform;
        if (referenceBone == null)
        {
            referenceBone = referenceAnimator.transform;
            Debug.LogError($"[ModelPartsCopy] Socket {socket.name} is not under a humanoid bone; placed on the model root.");
        }

        Transform placed = targetBone.Find(socket.name);
        if (placed == null)
        {
            placed = new GameObject(socket.name).transform;
            placed.SetParent(targetBone, false);
        }

        Transform referenceRoot = referenceAnimator.transform;
        Transform targetRoot = targetAnimator.transform;
        Vector3 offset = referenceRoot.InverseTransformVector(socket.position - referenceBone.position);
        Quaternion facing = Quaternion.Inverse(referenceRoot.rotation) * socket.rotation;

        placed.position = targetBone.position + targetRoot.TransformVector(offset);
        placed.rotation = targetRoot.rotation * facing;
        return placed;
    }

    private static Transform BoneAbove(Transform socket, Dictionary<Object, Object> map)
    {
        Transform current = socket.parent;
        while (current != null && !map.ContainsKey(current))
            current = current.parent;

        return current;
    }

    // ── Bones and pose ────────────────────────────────────────────────────────

    private static Dictionary<Object, Object> BoneMap(Animator reference, Animator target)
    {
        var map = new Dictionary<Object, Object>();
        for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
        {
            Transform from = reference.GetBoneTransform(bone);
            Transform to = target.GetBoneTransform(bone);
            if (from != null && to != null) map[from] = to;
        }

        return map;
    }

    private static void ZeroMuscles(Animator animator)
    {
        var handler = new HumanPoseHandler(animator.avatar, animator.transform);
        var pose = new HumanPose();
        handler.GetHumanPose(ref pose);

        for (int i = 0; i < pose.muscles.Length; i++)
            pose.muscles[i] = 0f;

        handler.SetHumanPose(ref pose);
        handler.Dispose();
    }

    private static Dictionary<Transform, Pose> Snapshot(Transform root)
    {
        var pose = new Dictionary<Transform, Pose>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            pose[t] = new Pose(t.localPosition, t.localRotation);

        return pose;
    }

    private static void Restore(Dictionary<Transform, Pose> pose)
    {
        foreach (var pair in pose)
            pair.Key.SetLocalPositionAndRotation(pair.Value.position, pair.Value.rotation);
    }

    // ── Parts ─────────────────────────────────────────────────────────────────

    // A part that sat with the Animator goes with the target's Animator; anything else goes on the target root.
    private static void CopyPart(Type type, GameObject reference, GameObject target, Animator targetAnimator, Dictionary<Object, Object> map)
    {
        var source = reference.GetComponentInChildren(type, true);
        if (source == null) return;

        GameObject host = source.GetComponent<Animator>() != null ? targetAnimator.gameObject : target;
        var part = host.GetComponent(type);
        if (part == null) part = host.AddComponent(type);

        EditorUtility.CopySerialized(source, part);
        Remap(part, map);
    }

    // Asset references (slot definitions) stay; references into the reference model follow the map or clear.
    private static void Remap(Component part, Dictionary<Object, Object> map)
    {
        var so = new SerializedObject(part);
        var property = so.GetIterator();

        while (property.NextVisible(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;

            Object value = property.objectReferenceValue;
            if (value == null || EditorUtility.IsPersistent(value)) continue;

            property.objectReferenceValue = map.TryGetValue(value, out Object mapped) ? mapped : null;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
