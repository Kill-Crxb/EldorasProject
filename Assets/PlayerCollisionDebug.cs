using UnityEngine;

public class PlayerCollisionDebug : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Player] Trigger hit by: {other.gameObject.name} (Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[Player] Collision hit by: {collision.gameObject.name} (Layer: {LayerMask.LayerToName(collision.gameObject.layer)})");
    }
}