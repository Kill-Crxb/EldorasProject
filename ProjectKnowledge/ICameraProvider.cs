using UnityEngine;

/// <summary>
/// Interface for camera providers. Allows different camera implementations
/// to be swapped while maintaining consistent access patterns.
/// </summary>
public interface ICameraProvider
{
    // State queries
    bool IsCameraLocked();
    Transform GetCameraLockTarget();
    float GetCameraHorizontalRotation();

    // Control methods
    void SetCameraInputEnabled(bool enabled);
    void SetMouseSensitivity(float sensitivity);
    void SetCameraOffset(Vector3 offset);

    // Optional properties for advanced access
    Transform CameraTransform { get; }
}