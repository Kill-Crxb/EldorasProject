using UnityEngine;

public class SimpleRotator : MonoBehaviour
{
    [SerializeField] private float rotationX = 0f;
    [SerializeField] private float rotationY = 45f;
    [SerializeField] private float rotationZ = 0f;

    private void Update()
    {
        transform.Rotate(rotationX * Time.deltaTime, rotationY * Time.deltaTime, rotationZ * Time.deltaTime);
    }
}