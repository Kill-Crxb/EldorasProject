using UnityEngine;

// The model's own stance: a race or body's base posture, always on while this model is worn. Sits on the model
// prefab beside ModelAppearance. ProceduralPoseModule reads it whenever it binds to a new model.

public class ModelPosture : MonoBehaviour
{
    [SerializeField] private PostureProfile posture;

    public PostureProfile Posture => posture;
}
