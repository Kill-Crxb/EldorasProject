using UnityEngine;

public interface ICameraProvider
{
    Transform CameraTransform { get; }
    float GetCameraHorizontalRotation();
    bool CameraDrivesFacing { get; }

    // Where the screen-centre aim ray lands within range, ignoring the player and anything between the camera and
    // the player; the ray's end when it hits nothing. hit is what it landed on, or null. Projectiles fly at this.
    Vector3 AimPoint(float range, out Collider hit);
}
