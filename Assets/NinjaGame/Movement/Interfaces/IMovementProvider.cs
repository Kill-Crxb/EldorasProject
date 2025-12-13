using UnityEngine;

public interface IMovementProvider
{
    bool IsGrounded { get; }
    bool IsMoving { get; }
    bool IsSprinting { get; }
    Vector3 Velocity { get; }
    Vector3 MoveDirection { get; }

    void Move(Vector3 direction, float speed);
    void Rotate(Quaternion rotation);
    void Stop();

    // New methods for effects and abilities
    void ApplyImpulse(Vector3 direction, float force);
    void TeleportTo(Vector3 position);
}