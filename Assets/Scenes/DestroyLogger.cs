using UnityEngine;

public class DestroyLogger : MonoBehaviour
{
    private void OnDestroy()
    {
        Debug.LogError($"[DestroyLogger] {gameObject.name} was destroyed!");
    }
}