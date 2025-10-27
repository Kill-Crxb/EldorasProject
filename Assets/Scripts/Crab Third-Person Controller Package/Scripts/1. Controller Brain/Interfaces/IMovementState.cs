using UnityEngine;
public interface IMovementState
{
    bool IsGrounded { get; }
    bool IsMoving { get; }
    bool IsSprinting { get; }
    Vector3 Velocity { get; }
}