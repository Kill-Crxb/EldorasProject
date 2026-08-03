using UnityEngine;

public interface ICameraProvider
{
    Transform CameraTransform { get; }
    float GetCameraHorizontalRotation();
    bool CameraDrivesFacing { get; }
}
