using UnityEngine;

/// <summary>
/// Marker interface for modules that need FixedUpdate physics updates.
/// Modules implementing this interface will be added to the physics update loop.
/// </summary>
public interface IPhysicsModule : IBrainModule
{
    void PhysicsUpdate();
}