using UnityEngine;

/// <summary>
/// Makes a thing hung off a bone the size it was authored at, whatever that bone is scaled to.
///
/// Rigs arrive at whatever scale their exporter felt like — an FBX imported without unit
/// conversion commonly lands at 100, and every bone inherits it. Parenting to such a bone
/// multiplies the attached object by 100 as well, so a hand spark becomes a building and a
/// shortsword becomes a telegraph pole.
///
/// Fixing it at the import settings instead would be cleaner in principle, and would silently
/// move every socket offset and every piece of equipment already placed on that rig, so it is
/// not. Cancel it at attach time and the rig can stay whatever it is.
///
/// Dividing the authored scale by the parent's lossyScale cancels the inherited factor exactly,
/// and costs nothing on a rig that was scaled sensibly — lossyScale 1 divides out to the
/// authored value.
///
/// Shared by VFXSystem (effects on sockets) and ModelModule (equipment on sockets) because it
/// is the same problem: both parent an authored prefab to a bone and want it to stay its own
/// size.
/// </summary>
public static class SocketScale
{
    /// <summary>
    /// Set localScale so the object ends up at <paramref name="authoredScale"/> in world terms,
    /// cancelling whatever the new parent contributes.
    ///
    /// Call AFTER parenting. Passing the prefab's own localScale gives "the size the artist
    /// made it"; passing a previously captured lossyScale preserves the size it already had
    /// across a reparent.
    ///
    /// Per-axis, because a non-uniformly scaled bone would otherwise skew the object. Guarded,
    /// because a zero on any axis would be a divide by zero and a NaN transform, which Unity
    /// reports as a wall of unrelated errors from the renderer.
    /// </summary>
    public static void Normalise(Transform instance, Vector3 authoredScale)
    {
        if (instance == null) return;

        Vector3 inherited = instance.parent != null ? instance.parent.lossyScale : Vector3.one;

        instance.localScale = new Vector3(
            Cancel(authoredScale.x, inherited.x),
            Cancel(authoredScale.y, inherited.y),
            Cancel(authoredScale.z, inherited.z));
    }

    private static float Cancel(float authored, float inherited)
    {
        return Mathf.Approximately(inherited, 0f) ? authored : authored / inherited;
    }
}
