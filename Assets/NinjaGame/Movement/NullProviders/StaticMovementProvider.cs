using UnityEngine;

public class StaticMovementProvider : MonoBehaviour, IMovementProvider
{
    public bool IsGrounded => true;
    public bool IsMoving => false;
    public bool IsSprinting => false;
    public Vector3 Velocity => Vector3.zero;
    public Vector3 MoveDirection => Vector3.zero;

    public void Move(Vector3 direction, float speed) { }
    public void Rotate(Quaternion rotation) { }
    public void Stop() { }

    // Static entities don't respond to impulses or teleports
    public void ApplyImpulse(Vector3 direction, float force) { }
    public void TeleportTo(Vector3 position) { }
}